using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Oculus.Interaction;

/// <summary>
/// Rappresenta un singolo atomo che puo' legarsi ad altri atomi rispettando
/// (in forma semplificata) la regola dell'ottetto e
/// disponendoli secondo la geometria VSEPR dell'atomo centrale.
///
/// Va applicato al prefab "Atomo Base", sullo stesso GameObject che ha gia'
/// i componenti di grab forniti dal Building Block Meta (Rigidbody +
/// Grabbable/HandGrabInteractable ecc.).
/// 
/// Idea: ogni atomo ha un numero sterico = legami + coppie solitarie.
///   - 1 o 2 -> geometria lineare
///   - 3     -> trigonale planare (120 gradi)
///   - 4     -> tetraedrica (109,5 gradi)
/// Le direzioni ("slot") sono definite in spazio LOCALE dell'atomo, quindi
/// ruotano insieme alla molecola. Le coppie solitarie occupano gli slot che
/// restano liberi: cosi' l'acqua (2 legami + 2 coppie) risulta piegata e
/// l'ammoniaca (3 legami + 1 coppia) piramidale, senza alcuno switch sul
/// simbolo dell'elemento.
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
    [Tooltip("Nome per esteso, es. Idrogeno")]
    public string nomeElemento = "Idrogeno";

    [Tooltip("Simbolo chimico, es. H, O, C, N")]
    public string simbolo = "H";

    [Tooltip("Numero di elettroni di valenza dell'elemento (1-8). " +
             "Determina quanti legami puo' formare l'atomo.")]
    [Range(1, 8)]
    public int elettroniValenza = 1;

    [Tooltip("-1 = calcolo automatico con la regola dell'ottetto. " +
             "Un valore >= 0 forza il numero di legami (utile per B, Be, ecc.)")]
    [Min(-1)]
    public int legamiMassimiManuali = -1;

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
    private bool inMano;

    // ------------------------------------------------------------------
    // Geometria: direzioni degli slot in spazio locale
    // ------------------------------------------------------------------
 
    private static readonly Vector3[] DirezioniUno =
    {
        Vector3.forward
    };
 
    private static readonly Vector3[] DirezioniLineari =
    {
        Vector3.forward,
        Vector3.back
    };
 
    private static readonly Vector3[] DirezioniTrigonali =
    {
        new Vector3(0f, 0f, 1f),
        new Vector3(0.8660254f, 0f, -0.5f),
        new Vector3(-0.8660254f, 0f, -0.5f)
    };
 
    private static readonly Vector3[] DirezioniTetraedriche =
    {
        Vector3.up,
        new Vector3(0.9428091f, -0.3333333f, 0f),
        new Vector3(-0.4714045f, -0.3333333f, 0.8164966f),
        new Vector3(-0.4714045f, -0.3333333f, -0.8164966f)
    };
 
    private static Vector3[] DirezioniPerSterico(int numeroSterico)
    {
        switch (numeroSterico)
        {
            case 1: return DirezioniUno;
            case 2: return DirezioniLineari;
            case 3: return DirezioniTrigonali;
            default: return DirezioniTetraedriche;
        }
    }
 
    // ------------------------------------------------------------------
    // Proprieta' chimiche derivate
    // ------------------------------------------------------------------
 
    /// <summary>
    /// Numero massimo di legami singoli. Regola dell'ottetto semplificata:
    /// 8 - elettroni di valenza (limitato a 4); l'idrogeno segue il duetto
    /// (1 legame). Sovrascrivibile con legamiMassimiManuali.
    /// </summary>
    public int NumeroLegamiMassimi
    {
        get
        {
            if (legamiMassimiManuali >= 0) return Mathf.Clamp(legamiMassimiManuali, 0, 4);
            return elettroniValenza <= 1 ? 1 : Mathf.Clamp(8 - elettroniValenza, 0, 4);
        }
    }
 
    /// <summary>Coppie di elettroni non condivise sull'atomo.</summary>
    public int CoppieSolitarie =>
        Mathf.Max(0, (elettroniValenza - NumeroLegamiMassimi) / 2);
 
    /// <summary>Domini elettronici totali = legami + coppie solitarie (1-4).</summary>
    public int NumeroSterico =>
        Mathf.Clamp(NumeroLegamiMassimi + CoppieSolitarie, Mathf.Max(1, NumeroLegamiMassimi), 4);
 
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
        // Indice dello slot geometrico occupato da questo legame.
        public int indiceSlot;
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
        if (evento.Type == PointerEventType.Select)
        {
            inMano = true;
        }
        else if (evento.Type == PointerEventType.Cancel)
        {
            inMano = false;
        }
        else if (evento.Type == PointerEventType.Unselect)
        {
            inMano = false;
 
            if (fermaAlRilascio)
            {
                // Azzeriamo per qualche step fisico: un eventuale lancio
                // dell'SDK o il joint residuo verso il proxy di grab
                // potrebbero ridare velocita' subito dopo l'Unselect.
                StopCoroutine(nameof(FermaMolecolaPerAlcuniFrame));
                StartCoroutine(nameof(FermaMolecolaPerAlcuniFrame));
            }
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
            atomo.AzzeraVelocita();
        }
    }
 
    private void AzzeraVelocita()
    {
        if (rb == null || rb.isKinematic) return;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }

    /// <summary>
    /// True se almeno un atomo della molecola e' attualmente afferrato.
    /// </summary>
    private bool GruppoInMano()
    {
        foreach (var atomo in OttieniMolecola())
        {
            if (atomo.inMano) return true;
        }
        return false;
    }
 
    // ------------------------------------------------------------------
    // Formazione del legame
    // ------------------------------------------------------------------

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
    /// dell'ottetto. Atomi della stessa molecola non vengono legati tra loro 
    /// (non e' possibile riposizionarli senza deformare i legami esistenti).
    /// </summary>
    public bool TentaLegame(Atomo altro)
    {
        if (altro == null || altro == this) return false;
        if (SonoGiaLegati(altro)) return false;
        if (LegamiDisponibili <= 0 || altro.LegamiDisponibili <= 0) return false;
        if (OttieniMolecola().Contains(altro)) return false;

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

    private bool SlotOccupato(int indice)
    {
        foreach (var l in legamiAttivi)
        {
            if (l.indiceSlot == indice) return true;
        }
        return false;
    }

    /// <summary>
    /// Restituisce l'indice dello slot libero la cui direzione (in mondo) e'
    /// piu' vicina alla direzione indicata, oppure -1 se non ce ne sono.
    /// </summary>
    private int SlotLiberoPiuVicino(Vector3 direzioneMondo)
    {
        Vector3[] direzioni = DirezioniPerSterico(NumeroSterico);
        int migliore = -1;
        float miglioreAllineamento = -2f;
 
        for (int i = 0; i < direzioni.Length; i++)
        {
            if (SlotOccupato(i)) continue;
 
            float allineamento = Vector3.Dot(transform.TransformDirection(direzioni[i]), direzioneMondo);
            if (allineamento > miglioreAllineamento)
            {
                miglioreAllineamento = allineamento;
                migliore = i;
            }
        }
 
        return migliore;
    }

    private bool CreaLegame(Atomo altro)
    {
        // Chi fa da "ancora" resta fermo, l'altro (con tutta la sua molecola)
        // viene spostato. Se una delle due molecole e' in mano all'utente,
        // resta ferma quella: cosi' non spostiamo cio' che si sta afferrando.
        Atomo ancora = this;
        Atomo mobile = altro;
        // Se questo atomo non è in mano scambio i ruoli, cosi' l'altro atomo (in mano) resta fermo.
        if (altro.GruppoInMano() && !GruppoInMano())
        {
            ancora = altro;
            mobile = this;
        }
 
        Vector3 versoMobile = mobile.transform.position - ancora.transform.position;
        if (versoMobile.sqrMagnitude < 1e-8f) versoMobile = Vector3.forward;
        versoMobile.Normalize();
 
        int slotAncora = ancora.SlotLiberoPiuVicino(versoMobile);
        int slotMobile = mobile.SlotLiberoPiuVicino(-versoMobile);
        if (slotAncora < 0 || slotMobile < 0) return false;
 
        if (allineaAllaConnessione)
        {
            ancora.AllineaGruppo(mobile, slotAncora, slotMobile);
        }
 
        int slotQuesto = (ancora == this) ? slotAncora : slotMobile;
        int slotAltro = (ancora == this) ? slotMobile : slotAncora;
 
        // Joint fisico: creato DOPO il riposizionamento, cosi' blocca la
        // posa relativa corretta tra i due atomi.
        FixedJoint joint = gameObject.AddComponent<FixedJoint>();
        joint.connectedBody = altro.rb;
        joint.breakForce = Mathf.Infinity;
        joint.breakTorque = Mathf.Infinity;
 
        GameObject cilindro = null;
        if (prefabCilindroLegame != null)
        {
            cilindro = Instantiate(prefabCilindroLegame);
            LegameVisuale visuale = cilindro.GetComponent<LegameVisuale>();
            if (visuale == null) visuale = cilindro.AddComponent<LegameVisuale>();
            visuale.Imposta(transform, altro.transform);
        }
 
        legamiAttivi.Add(new Legame
        {
            altroAtomo = altro,
            cilindroVisivo = cilindro,
            joint = joint,
            indiceSlot = slotQuesto
        });
        altro.RegistraLegameRicevuto(this, cilindro, slotAltro);
        return true;
    }

    /// <summary>
    /// Sposta e ruota rigidamente l'intera molecola di "mobile" in modo che:
    /// - il suo atomo si trovi nello slot indicato di questo atomo (ancora),
    ///   a distanza distanzaLegame;
    /// - il suo slot libero punti verso questo atomo.
    /// La posa relativa degli atomi dentro la molecola mobile non cambia.
    /// </summary>
    private void AllineaGruppo(Atomo mobile, int slotAncora, int slotMobile)
    {
        Vector3 dirAncora = transform.TransformDirection(DirezioniPerSterico(NumeroSterico)[slotAncora]);
        Vector3 dirMobile = mobile.transform.TransformDirection(DirezioniPerSterico(mobile.NumeroSterico)[slotMobile]);
 
        Quaternion rotazione = Quaternion.FromToRotation(dirMobile, -dirAncora);
        Vector3 perno = mobile.transform.position;
        Vector3 nuovaPosizioneMobile = transform.position + dirAncora * distanzaLegame;
        Vector3 traslazione = nuovaPosizioneMobile - perno;
 
        foreach (var atomo in mobile.OttieniMolecola())
        {
            Vector3 nuovaPos = perno + rotazione * (atomo.transform.position - perno) + traslazione;
            Quaternion nuovaRot = rotazione * atomo.transform.rotation;
            atomo.transform.SetPositionAndRotation(nuovaPos, nuovaRot);
            atomo.AzzeraVelocita();
        }
 
        // Sincronizza i Rigidbody con i Transform appena modificati prima
        // di creare il joint.
        Physics.SyncTransforms();
    }

    /// <summary>
    /// Chiamato dall'atomo che ha creato il FixedJoint, cosi' anche questo
    /// atomo registra il legame (e lo slot occupato) senza un joint duplicato.
    /// </summary>
    internal void RegistraLegameRicevuto(Atomo altro, GameObject cilindro, int indiceSlot)
    {
        legamiAttivi.Add(new Legame
        {
            altroAtomo = altro,
            cilindroVisivo = cilindro,
            joint = null,
            indiceSlot = indiceSlot
        });
    }

    /// <summary>
    /// Tutti gli atomi della molecola a cui appartiene questo atomo
    /// (visita in ampiezza del grafo dei legami).
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

    // ------------------------------------------------------------------
    // Debug visivo: slot liberi (verde) e occupati (rosso) nella Scene view
    // ------------------------------------------------------------------
 
    private void OnDrawGizmosSelected()
    {
        Vector3[] direzioni = DirezioniPerSterico(NumeroSterico);
        for (int i = 0; i < direzioni.Length; i++)
        {
            Gizmos.color = SlotOccupato(i) ? Color.red : Color.green;
            Gizmos.DrawRay(transform.position, transform.TransformDirection(direzioni[i]) * distanzaLegame);
        }
    }
}