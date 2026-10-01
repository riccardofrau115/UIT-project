using UnityEngine;

public class CanvasGroupFader : MonoBehaviour
{
    [SerializeField] CanvasGroup canvasGroup;
    [SerializeField] float animationSpeed = 10f;
    [SerializeField] bool startVisible;

    bool visible;

    void Awake()
    {
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        SetVisible(startVisible, true);
    }

    public void ToggleVisible() => SetVisible(!visible, false);
    public void Show() => SetVisible(true, false);
    public void Hide() => SetVisible(false, false);

    void SetVisible(bool value, bool instant)
    {
        visible = value;
        canvasGroup.interactable = value;
        canvasGroup.blocksRaycasts = value;
        if (instant) canvasGroup.alpha = value ? 1f : 0f;
    }

    void Update()
    {
        canvasGroup.alpha = Mathf.Lerp(canvasGroup.alpha, visible ? 1f : 0f,
                                       animationSpeed * Time.deltaTime);
    }
}