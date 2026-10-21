using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
public class ZonaCestino : MonoBehaviour
{
    [SerializeField] private Image sfondo;
    [SerializeField] private Color coloreAllarme = new Color(1f, 0.2f, 0.2f, 0.85f);
    [SerializeField] private float velocitaLampeggio = 3f;
    [SerializeField] private float velocitaDissolvenza = 8f;
    [Tooltip("Distanza massima (m) dal piano del pannello per considerare l'atomo dentro")]
    [SerializeField] private float profondita = 0.15f;

    private RectTransform rect;
    private CanvasGroup gruppo;
    private Color coloreBase;
    private Atomo atomoAfferrato;

    private void Awake()
    {
        rect = (RectTransform)transform;
        gruppo = GetComponent<CanvasGroup>();
        gruppo.alpha = 0f;
        gruppo.interactable = false;
        gruppo.blocksRaycasts = false; // e' solo visivo, non intercetta il tocco
        if (sfondo != null) coloreBase = sfondo.color;
    }

    private void OnEnable()
    {
        Atomo.AtomoAfferrato += OnAtomoAfferrato;
        Atomo.AtomoRilasciato += OnAtomoRilasciato;
    }

    private void OnDisable()
    {
        Atomo.AtomoAfferrato -= OnAtomoAfferrato;
        Atomo.AtomoRilasciato -= OnAtomoRilasciato;
    }

    private void OnAtomoAfferrato(Atomo atomo) => atomoAfferrato = atomo;

    private void OnAtomoRilasciato(Atomo atomo)
    {
        if (atomoAfferrato == atomo) atomoAfferrato = null;

        if (Dentro(atomo))
            atomo.DistruggiMolecola();
    }

    private void Update()
    {
        // Il pannello compare finche' hai un atomo in mano
        float target = atomoAfferrato != null ? 1f : 0f;
        gruppo.alpha = Mathf.MoveTowards(gruppo.alpha, target, velocitaDissolvenza * Time.deltaTime);

        // Lampeggia in rosso quando l'atomo e' sopra il pannello
        if (sfondo != null)
        {
            sfondo.color = Dentro(atomoAfferrato)
                ? Color.Lerp(coloreBase, coloreAllarme, Mathf.PingPong(Time.time * velocitaLampeggio, 1f))
                : coloreBase;
        }
    }

    private bool Dentro(Atomo atomo)
    {
        if (atomo == null) return false;

        Vector3 p = rect.InverseTransformPoint(atomo.transform.position);
        float distanzaDalPiano = Mathf.Abs(p.z) * rect.lossyScale.z;

        return distanzaDalPiano <= profondita && rect.rect.Contains(new Vector2(p.x, p.y));
    }
}