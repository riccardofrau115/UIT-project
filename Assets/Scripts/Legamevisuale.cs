using UnityEngine;

/// <summary>
/// Da mettere sul prefab del cilindro di legame (un Cylinder primitivo di
/// Unity va benissimo). Rimuovi il collider di default del cilindro: e'
/// puramente visivo e non deve interferire con la fisica.
///
/// Ogni frame riposiziona, ruota e scala il cilindro in modo che unisca
/// visivamente i due atomi collegati, qualunque sia la loro posizione.
/// </summary>
public class LegameVisuale : MonoBehaviour
{
    [Tooltip("Raggio (spessore) del cilindro")]
    public float spessore = 0.02f;

    private Transform atomoA;
    private Transform atomoB;

    public void Imposta(Transform a, Transform b)
    {
        atomoA = a;
        atomoB = b;
    }

    private void LateUpdate()
    {
        if (atomoA == null || atomoB == null)
        {
            Destroy(gameObject);
            return;
        }

        Vector3 direzione = atomoB.position - atomoA.position;
        float distanza = direzione.magnitude;

        transform.position = atomoA.position + direzione * 0.5f;
        transform.rotation = Quaternion.FromToRotation(Vector3.up, direzione.normalized);

        // Il cilindro primitivo di Unity e' alto 2 unita' di default
        // (scala 1 = altezza 2), quindi la scala su Y e' meta' della distanza.
        transform.localScale = new Vector3(spessore, distanza * 0.5f, spessore);
    }
}