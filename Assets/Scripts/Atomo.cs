using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Oculus.Interaction;

/// <summary>
/// Rappresenta un singolo atomo che puo' legarsi ad altri atomi rispettando
/// (in forma semplificata) la regola dell'ottetto.
///
/// Va applicato al prefab "Atomo Base", sullo stesso GameObject che ha gia'
/// i componenti di grab forniti dal Building Block Meta (Rigidbody +
/// Grabbable/HandGrabInteractable ecc.).
///
/// Setup richiesto sul GameObject:
/// - Un Rigidbody (di solito gia' presente per via del Building Block di grab).
///   Consigliato: Use Gravity = false, cosi' gli atomi restano dove li lasci.
/// - Un Collider "fisico" (quello gia' usato dal grab per l'afferramento).
/// - Un secondo SphereCollider con Is Trigger = true e raggio maggiore del
///   primo: e' la "zona di legame" usata per rilevare atomi vicini.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class Atomo : MonoBehaviour
{
    [Header("Identita' chimica")]
    [Tooltip("Nome per esteso, es. Ossigeno")]
    public string nomeElemento = "Idrogeno";

    [Tooltip("Simbolo chimico, es. H, O, C, N")]
    public string simbolo = "H";

    [Tooltip("Numero di elettroni di valenza dell'elemento (1-8). " +
             "Determina quanti legami puo' formare l'atomo.")]
    [Range(1, 8)]
    public int elettroniValenza = 1;

    [Header("Legame chimico")]
    [Tooltip("Prefab del cilindro usato per visualizzare il legame tra due atomi")]
    public GameObject prefabCilindroLegame;

    [Tooltip("Distanza (in metri) a cui viene agganciato l'atomo collegato")]
    public float distanzaLegame = 0.15f;

    [Tooltip("Se vero, quando si forma un legame l'altro atomo viene " +
             "riposizionato alla distanza ideale lungo la direzione di contatto")]
    public bool allineaAllaConnessione = true;

    /* [Header("Rilascio")]
    [Tooltip("Se vero, quando l'atomo viene rilasciato la velocita' residua " +
             "di tutta la molecola viene azzerata, cosi' resta ferma nel " +
             "punto esatto in cui viene lasciata (nessuna inerzia/lancio)")] */
    private bool fermaAlRilascio = true;

    [Header("Debug (sola lettura)")]
    [SerializeField]
    private List<Legame> legamiAttivi = new List<Legame>();

    private Rigidbody rb;
    private Grabbable grabbable;

    /// <summary>
    /// Numero massimo di legami singoli che questo atomo puo' formare.
    /// Regola dell'ottetto semplificata: un atomo "vuole" arrivare a 8
    /// elettroni nel guscio di valenza, quindi gli servono (8 - elettroniValenza)
    /// legami singoli. Eccezione: l'idrogeno segue la regola del duetto
    /// (1 solo legame). Il risultato e' limitato a 4 perche' nella pratica
    /// nessun elemento comune di questo tipo forma piu' di 4 legami singoli
    /// (come il carbonio).
    /// </summary>
    public int NumeroLegamiMassimi =>
        elettroniValenza <= 1 ? 1 : Mathf.Clamp(8 - elettroniValenza, 0, 4);

    public int LegamiDisponibili => NumeroLegamiMassimi - legamiAttivi.Count;

    public bool OttettoCompleto => LegamiDisponibili <= 0;

    [System.Serializable]
    public class Legame
    {
        public Atomo altroAtomo;
        public GameObject cilindroVisivo;
        // Il FixedJoint fisico esiste solo su UNO dei due atomi coinvolti
        // (quello che ha creato il legame), per evitare doppioni.
        public FixedJoint joint;
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        // Il Grabbable e' il componente del Building Block Meta che gestisce
        // l'afferramento; ci iscriviamo ai suoi eventi per sapere quando
        // l'atomo viene rilasciato (PointerEventType.Unselect).
        grabbable = GetComponent<Grabbable>();
        if (grabbable != null)
        {
            grabbable.WhenPointerEventRaised += GestisciEventoPuntatore;
        }
    }

    private void OnDestroy()
    {
        if (grabbable != null)
        {
            grabbable.WhenPointerEventRaised -= GestisciEventoPuntatore;
        }
    }

    private void GestisciEventoPuntatore(PointerEvent evento)
    {
        if (!fermaAlRilascio) return;
        if (evento.Type == PointerEventType.Unselect)
        {
            // Non azzeriamo la velocita' una volta sola: un eventuale
            // "lancio" applicato dall'SDK (componente ThrowWhenUnselected)
            // o il joint temporaneo verso il proxy di grab (che potrebbe
            // non essere ancora stato distrutto nello stesso istante
            // dell'evento Unselect) possono ridare velocita' all'atomo
            // subito dopo. Ripetiamo l'azzeramento per qualche step fisico
            // cosi' l'ultima parola resta sempre alla nostra chiamata.
            StopCoroutine(nameof(FermaMolecolaPerAlcuniFrame));
            StartCoroutine(nameof(FermaMolecolaPerAlcuniFrame));
        }
    }

    private IEnumerator FermaMolecolaPerAlcuniFrame()
    {
        const int numeroStepFisici = 6;
        for (int i = 0; i < numeroStepFisici; i++)
        {
            FermaMolecola();
            yield return new WaitForFixedUpdate();
        }
    }

    /// <summary>
    /// Azzera la velocita' lineare e angolare di tutti gli atomi della
    /// molecola a cui appartiene questo atomo, cosi' che l'intera molecola
    /// resti immobile nel punto in cui viene rilasciata invece di continuare
    /// a muoversi per inerzia.
    /// </summary>
    public void FermaMolecola()
    {
        foreach (var atomo in OttieniMolecola())
        {
            if (atomo.rb == null) continue;
            atomo.rb.linearVelocity = Vector3.zero;
            atomo.rb.angularVelocity = Vector3.zero;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        Atomo altro = other.GetComponentInParent<Atomo>();
        if (altro == null || altro == this) return;

        // Sia questo atomo che l'altro riceveranno un OnTriggerEnter quasi
        // contemporaneamente: lasciamo che sia solo quello con InstanceID
        // piu' basso a occuparsi di creare il legame, per evitare duplicati.
        if (GetEntityId() > altro.GetEntityId()) return;

        TentaLegame(altro);
    }

    /// <summary>
    /// Prova a formare un legame con un altro atomo, rispettando la regola
    /// dell'ottetto (entrambi devono avere ancora "posti liberi").
    /// </summary>
    public bool TentaLegame(Atomo altro)
    {
        if (altro == null || altro == this) return false;
        if (SonoGiaLegati(altro)) return false;
        if (LegamiDisponibili <= 0 || altro.LegamiDisponibili <= 0) return false;

        CreaLegame(altro);
        return true;
    }

    private bool SonoGiaLegati(Atomo altro)
    {
        foreach (var l in legamiAttivi)
        {
            if (l.altroAtomo == altro) return true;
        }
        return false;
    }

    private void CreaLegame(Atomo altro)
    {
        if (allineaAllaConnessione)
        {
            Vector3 direzione = (altro.transform.position - transform.position).normalized;
            altro.rb.position = transform.position + direzione * distanzaLegame;
        }

        // Joint fisico: collegando i due Rigidbody, quando afferri e sposti
        // UN atomo qualsiasi della molecola, tutti gli altri lo seguono
        // automaticamente tramite il motore fisico di Unity.
        FixedJoint joint = gameObject.AddComponent<FixedJoint>();
        joint.connectedBody = altro.rb;
        joint.breakForce = Mathf.Infinity;
        joint.breakTorque = Mathf.Infinity;

        // Cilindro visivo che unisce le due sfere.
        GameObject cilindro = null;
        if (prefabCilindroLegame != null)
        {
            cilindro = Instantiate(prefabCilindroLegame);
            LegameVisuale visuale = cilindro.GetComponent<LegameVisuale>();
            if (visuale == null) visuale = cilindro.AddComponent<LegameVisuale>();
            visuale.Imposta(transform, altro.transform);
        }

        legamiAttivi.Add(new Legame { altroAtomo = altro, cilindroVisivo = cilindro, joint = joint });
        altro.RegistraLegameRicevuto(this, cilindro);
    }

    /// <summary>
    /// Chiamato dall'atomo che ha fisicamente creato il FixedJoint, cosi'
    /// anche questo atomo sa di essere legato senza generare un secondo
    /// joint duplicato.
    /// </summary>
    internal void RegistraLegameRicevuto(Atomo altro, GameObject cilindro)
    {
        legamiAttivi.Add(new Legame { altroAtomo = altro, cilindroVisivo = cilindro, joint = null });
    }

    /// <summary>
    /// Restituisce (via visita in ampiezza del grafo dei legami) tutti gli
    /// atomi della molecola a cui appartiene questo atomo. Utile ad esempio
    /// per mostrare a schermo la formula/il nome della molecola completa.
    /// </summary>
    public List<Atomo> OttieniMolecola()
    {
        var visitati = new List<Atomo> { this };
        var daVisitare = new Queue<Atomo>();
        daVisitare.Enqueue(this);

        while (daVisitare.Count > 0)
        {
            Atomo corrente = daVisitare.Dequeue();
            foreach (var legame in corrente.legamiAttivi)
            {
                if (!visitati.Contains(legame.altroAtomo))
                {
                    visitati.Add(legame.altroAtomo);
                    daVisitare.Enqueue(legame.altroAtomo);
                }
            }
        }

        return visitati;
    }
}