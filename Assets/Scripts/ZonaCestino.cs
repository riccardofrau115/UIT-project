using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Zona "cestino": se una molecola viene rilasciata con almeno uno dei suoi
/// atomi dentro questa zona, l'intera molecola (atomi + cilindri) viene eliminata.
///
/// Setup: GameObject con un Collider convesso (Box/Sphere/Capsule); il
/// collider viene impostato automaticamente come Trigger.
/// </summary>
[RequireComponent(typeof(Collider))]
public class ZonaCestino : MonoBehaviour
{
    [Header("Feedback visivo")]
    [Tooltip("Renderer del volume semitrasparente che indica la zona")]
    public Renderer volumeVisivo;
    public Material materialeNormale;
    [Tooltip("Materiale mostrato quando un atomo si trova dentro la zona")]
    public Material materialeAttivo;
    [Tooltip("Testo 3D (es. TextMeshPro) che ruota per restare leggibile; opzionale")]
    public Transform etichetta;

    private Collider zona;
    private readonly HashSet<Collider> dentro = new HashSet<Collider>();
    private bool attivo;

    private void Awake()
    {
        zona = GetComponent<Collider>();
        zona.isTrigger = true;
        ApplicaMateriale(false);
    }

    private void OnEnable() => Atomo.AtomoRilasciato += GestisciRilascio;
    private void OnDisable() => Atomo.AtomoRilasciato -= GestisciRilascio;

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<Atomo>() != null) dentro.Add(other);
    }

    private void OnTriggerExit(Collider other)
    {
        dentro.Remove(other);
    }

    private void Update()
    {
        // Rimuove i collider distrutti o disattivati (OnTriggerExit non e'
        // affidabile in quel caso).
        dentro.RemoveWhere(c => c == null || !c.enabled || !c.gameObject.activeInHierarchy);

        bool adesso = dentro.Count > 0;
        if (adesso != attivo)
        {
            attivo = adesso;
            ApplicaMateriale(attivo);
        }

        // L'etichetta guarda sempre verso l'utente (solo rotazione orizzontale).
        if (etichetta != null && Camera.main != null)
        {
            Vector3 dir = etichetta.position - Camera.main.transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 1e-4f) etichetta.rotation = Quaternion.LookRotation(dir);
        }
    }

    private void ApplicaMateriale(bool evidenzia)
    {
        if (volumeVisivo == null) return;
        Material m = evidenzia ? materialeAttivo : materialeNormale;
        if (m != null) volumeVisivo.sharedMaterial = m;
    }

    private void GestisciRilascio(Atomo rilasciato)
    {
        if (rilasciato == null) return;

        // Basta che UN atomo qualsiasi della molecola sia dentro la zona.
        foreach (Atomo a in rilasciato.OttieniMolecola())
        {
            if (!ContienePunto(a.transform.position)) continue;
            StartCoroutine(EliminaDopoUnFrame(rilasciato));
            return;
        }
    }

    private bool ContienePunto(Vector3 punto)
    {
        return (zona.ClosestPoint(punto) - punto).sqrMagnitude < 1e-6f;
    }

    private IEnumerator EliminaDopoUnFrame(Atomo atomo)
    {
        // Attende un frame: l'SDK di grab deve finire di processare il
        // rilascio prima che l'oggetto afferrato venga distrutto.
        yield return null;
        if (atomo == null) yield break;

        atomo.DistruggiMolecola();
    }
}