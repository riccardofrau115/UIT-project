using UnityEngine;
using Oculus.Interaction;

[RequireComponent(typeof(Grabbable))]
public class CanvasTableSnapper : MonoBehaviour
{
    [Header("Impostazioni Snap")]
    [SerializeField] private float raycastDownDistance = 0.5f;
    [SerializeField] private float heightOffsetFromTable = 0.005f;
    [SerializeField] private LayerMask surfaceLayers = ~0;

    [Header("Riferimenti Visuale")]
    [SerializeField] private Transform userHead;

    [Header("Opzioni Rotazione")]
    [Tooltip("Se true, la cima del testo punta lontano dall'utente (testo leggibile da chi sta al tavolo).")]
    [SerializeField] private bool faceUserOnTable = true;

    private Grabbable _grabbable;
    private void Awake()
    {
        _grabbable = GetComponent<Grabbable>();
    }

    private void OnEnable()
    {
        _grabbable.WhenPointerEventRaised += HandlePointerEvent;
    }

    private void OnDisable()
    {
        _grabbable.WhenPointerEventRaised -= HandlePointerEvent;
    }

    private void HandlePointerEvent(PointerEvent evt)
    {
        if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Calibration)
            return;

        if (evt.Type == PointerEventType.Unselect)
        {
            TrySnapToTable();
        }
    }

    private void TrySnapToTable()
    {
        Ray ray = new Ray(transform.position, Vector3.down);
        Debug.DrawRay(transform.position, Vector3.down * raycastDownDistance, Color.red, 2f);

        if (!Physics.Raycast(ray, out RaycastHit hit, raycastDownDistance, surfaceLayers))
        {
            Debug.LogWarning("[CanvasTableSnapper] Nessuna superficie trovata.");
            return;
        }

        if (!IsTable(hit.collider.gameObject))
        {
            Debug.Log($"[CanvasTableSnapper] '{hit.collider.gameObject.name}' non è un tavolo.");
            return;
        }

        Vector3 surfaceUp = hit.normal; // es. (0,1,0)

     
        Transform head = userHead != null ? userHead : (Camera.main != null ? Camera.main.transform : null);
        Vector3 awayFromUser;

        if (faceUserOnTable && head != null)
        {
            awayFromUser = -Vector3.ProjectOnPlane(head.position - hit.point, surfaceUp);
        }
        else
        {
            // Mantiene la direzione in cui "guardava" il canvas (lontano da chi lo teneva)
            awayFromUser = Vector3.ProjectOnPlane(transform.forward, surfaceUp);
        }

        if (awayFromUser.sqrMagnitude < 0.0001f)
            awayFromUser = Vector3.ProjectOnPlane(Vector3.forward, surfaceUp);
        awayFromUser.Normalize();

      
        Quaternion snappedRotation = Quaternion.LookRotation(-surfaceUp, awayFromUser);

       
        Quaternion originalRotation = transform.rotation;
        transform.rotation = snappedRotation;

        float halfThickness = GetVerticalHalfExtent();

        transform.rotation = originalRotation;

        Vector3 snappedPosition = hit.point + surfaceUp * (halfThickness + heightOffsetFromTable);

      
        transform.position = snappedPosition;
        transform.rotation = snappedRotation;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.ConfirmCalibration(snappedPosition, snappedRotation);
        }

        Debug.Log($"[CanvasTableSnapper] Snap su '{hit.collider.gameObject.name}' | " +
                  $"up: {surfaceUp} | awayFromUser: {awayFromUser} | halfThickness: {halfThickness:F3}");
    }

    private float GetVerticalHalfExtent()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return 0f;

        Bounds combined = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            combined.Encapsulate(renderers[i].bounds);

        return combined.extents.y;
    }

    private bool IsTable(GameObject hitObject)
    {
        foreach (var comp in hitObject.GetComponentsInParent<MonoBehaviour>())
        {
            if (comp != null && comp.GetType().Name.Contains("Bounded3DEntity"))
            {
                var prop = comp.GetType().GetProperty("SemanticClassification")
                           ?? comp.GetType().GetProperty("SemanticLabel");
                var field = comp.GetType().GetField("SemanticLabel")
                            ?? comp.GetType().GetField("semanticLabel");

                string labelValue = prop?.GetValue(comp)?.ToString() ?? field?.GetValue(comp)?.ToString();
                if (!string.IsNullOrEmpty(labelValue) && labelValue.ToUpper().Contains("TABLE"))
                    return true;
            }
        }

        string name = hitObject.name.ToLower();
        if (name.Contains("desk") || name.Contains("table"))
            return true;

        return false;
    }
}