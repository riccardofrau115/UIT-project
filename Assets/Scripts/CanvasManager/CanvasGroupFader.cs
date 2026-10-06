using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public class CanvasGroupFader : MonoBehaviour
{
    [SerializeField] CanvasGroup canvasGroup;
    [SerializeField] float animationSpeed = 10f;

    bool visible;

    void Awake()
    {
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
    }

    // Per gli UnityEvent (OnClick ecc.)
    public void ToggleVisible() => SetVisible(!visible);
    public void Show() => SetVisible(true);
    public void Hide() => SetVisible(false);

    // Da codice: instant = true salta la dissolvenza
    public void SetVisible(bool value, bool instant = false)
    {
        visible = value;
        canvasGroup.interactable = value;
        canvasGroup.blocksRaycasts = value;
        if (instant) canvasGroup.alpha = value ? 1f : 0f;
    }

    void Update()
    {
        float target = visible ? 1f : 0f;
        if (Mathf.Approximately(canvasGroup.alpha, target)) return;

        canvasGroup.alpha = Mathf.Lerp(canvasGroup.alpha, target,
                                       animationSpeed * Time.deltaTime);
        if (Mathf.Abs(canvasGroup.alpha - target) < 0.01f)
            canvasGroup.alpha = target;
    }
}
