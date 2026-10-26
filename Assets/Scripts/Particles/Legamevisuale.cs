using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Posiziona e adatta visivamente i cilindri per rappresentare i legami.
/// Supporta legami singoli, doppi e tripli sfalsando dei cilindri paralleli.
/// </summary>
public class LegameVisuale : MonoBehaviour
{
    [Tooltip("Raggio (spessore) del cilindro base")]
    public float spessore = 0.02f;
    
    [Tooltip("Distanza di sfalsamento per i legami doppi/tripli")]
    public float distanzaSfalsamento = 0.04f;
    
    [SerializeField]
    private Transform atomoA;
    [SerializeField]
    private Transform atomoB;
    [SerializeField]
    private int ordineLegame = 1;
    [SerializeField]
    private List<Transform> cilindriVisivi = new List<Transform>();

    public void Imposta(Transform a, Transform b, int ordine)
    {
        atomoA = a;
        atomoB = b;
        ordineLegame = ordine;

        // Il GameObject corrente fungerà da "container" e userà la mesh del primo cilindro.
        //cilindriVisivi.Clear();
        cilindriVisivi.Add(this.transform);

        // Se l'ordine è 2 o 3, generiamo cilindri addizionali come FIGLI di questo GameObject
        for (int i = 1; i < ordineLegame; i++)
        {
            GameObject cilindroExtra = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            
            // Rimuovi eventuali collider (puramente visivo)
            Destroy(cilindroExtra.GetComponent<Collider>());

            // Assegna il cilindro extra come figlio di QUESTO transform
            cilindroExtra.transform.SetParent(this.transform, false);
            
            // Applica il materiale del genitore (se presente)
            MeshRenderer rendererGenitore = GetComponent<MeshRenderer>();
            if (rendererGenitore != null)
            {
                cilindroExtra.GetComponent<MeshRenderer>().sharedMaterial = rendererGenitore.sharedMaterial;
            }

            cilindriVisivi.Add(cilindroExtra.transform);
        }
    }

    public void ImpostaGenitore(Transform genitore)
    {
        // Spostiamo solo il GameObject radice: i cilindri figli lo seguiranno automaticamente
        if (genitore != null)
        {
            transform.SetParent(genitore, true);
        }
    }

    private void OnDestroy()
    {
        // Garanzia: quando il LegameVisuale viene distrutto, distrugge esplicitamente tutti i cilindri extra
        for (int i = 1; i < cilindriVisivi.Count; i++)
        {
            if (cilindriVisivi[i] != null)
            {
                Destroy(cilindriVisivi[i].gameObject);
            }
        }
    }

    private void LateUpdate()
    {
        // Se uno dei due atomi è stato distrutto, distrugge l'intero oggetto del legame
        if (atomoA == null || atomoB == null)
        {
            Destroy(gameObject);
            return;
        }

        Vector3 direzione = atomoB.position - atomoA.position;
        float distanza = direzione.magnitude;
        Vector3 centro = atomoA.position + direzione * 0.5f;

        // Calcoliamo un asse perpendicolare al legame per sfalsare i cilindri
        Vector3 upRotazione = Vector3.up;
        if (Mathf.Abs(Vector3.Dot(direzione.normalized, Vector3.up)) > 0.99f)
        {
            upRotazione = Vector3.right; // Evita parallelismo perfetto con l'asse Y
        }
        
        Quaternion rotazioneCilindri = Quaternion.FromToRotation(Vector3.up, direzione.normalized);
        Vector3 assePerpendicolare = Vector3.Cross(direzione.normalized, upRotazione).normalized;

        for (int i = 0; i < cilindriVisivi.Count; i++)
        {
            Transform cil = cilindriVisivi[i];
            if (cil == null) continue;
            
            float offsetMultiplier = 0f;

            float spessoreCilindro = spessore;
            if (ordineLegame == 2)
            {
                // Due cilindri: sfalsati in opposte direzioni
                offsetMultiplier = (i == 0) ? -0.5f : 0.5f;
                spessoreCilindro = 0.015f; // Riduci lo spessore per legami doppi
            }
            else if (ordineLegame == 3)
            {
                // Tre cilindri: uno al centro, gli altri ai lati
                if (i == 1) offsetMultiplier = -0.75f;
                else if (i == 2) offsetMultiplier = 0.75f;
                spessoreCilindro = 0.01f; // Riduci ulteriormente lo spessore per legami tripli
            }

            Vector3 posizioneFinale = centro + (assePerpendicolare * (offsetMultiplier * distanzaSfalsamento));

            cil.position = posizioneFinale;
            cil.rotation = rotazioneCilindri;
            
            // Scala Y adatta all'altezza
            if (cil == this.transform)
            {
                cil.localScale = new Vector3(spessoreCilindro, distanza * 0.5f, spessoreCilindro);
            }
        }
    }
}