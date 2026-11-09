using UnityEngine;
using Oculus.Interaction;
using Meta.XR;                 // EnvironmentRaycastManager, EnvironmentRaycastHit
using Meta.XR.MRUtilityKit;    // MRUK, MRUKRoom, MRUKAnchor

/// <summary>
/// Al rilascio del pinch fa "snap" del canvas su una superficie orizzontale.
/// Ordine di ricerca della superficie:
///   1) Environment Raycast (depth del visore): qualsiasi superficie reale, senza collider
///   2) MRUK scene model (se disponibile): superfici mappate con Space Setup / Synthetic Environment
///   3) Physics.Raycast su collider (fallback, es. desk con collider in scena)
/// </summary>
[RequireComponent(typeof(Grabbable))]
public class CanvasTableSnapper : MonoBehaviour
{
    [Header("Impostazioni Snap")]
    [Tooltip("Distanza massima del raycast verso il basso dal canvas (metri).")]
    [SerializeField] private float raycastDownDistance = 1.5f;
    [SerializeField] private float heightOffsetFromTable = 0.005f;

    [Tooltip("Soglia su normal.y per considerare una superficie orizzontale (1 = perfettamente piana).")]
    [Range(0.5f, 1f)]
    [SerializeField] private float minUpDot = 0.9f;

    [Tooltip("Se false, ignora il pavimento (altezza sotto 'minHeightAboveFloor' rispetto al pavimento della stanza MRUK).")]
    [SerializeField] private bool allowFloor = false;

    [Tooltip("Altezza minima sopra il pavimento per accettare la superficie (usata se allowFloor = false e la stanza MRUK è disponibile).")]
    [SerializeField] private float minHeightAboveFloor = 0.3f;

    [Header("Sorgenti superficie")]
    [Tooltip("Environment Raycast Manager presente in scena (Depth API, Quest 3/3S). Se vuoto viene cercato automaticamente.")]
    [SerializeField] private EnvironmentRaycastManager environmentRaycastManager;
    [SerializeField] private bool useEnvironmentRaycast = true;
    [SerializeField] private bool useMruk = true;
    [SerializeField] private bool usePhysicsFallback = true;
    [SerializeField] private LayerMask surfaceLayers = ~0;

    [Header("Debug")]
    [SerializeField] private bool logDiagnostics = true;

    private Grabbable _grabbable;

    private void Awake()
    {
        _grabbable = GetComponent<Grabbable>();

        if (environmentRaycastManager == null)
            environmentRaycastManager = FindFirstObjectByType<EnvironmentRaycastManager>();
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
        if (evt.Type == PointerEventType.Unselect)
        {
            TrySnapToSurface();
        }
    }

    private void TrySnapToSurface()
    {
        Ray ray = new Ray(transform.position, Vector3.down);
        Debug.DrawRay(ray.origin, Vector3.down * raycastDownDistance, Color.red, 2f);

        if (!TryFindSurface(ray, out Vector3 hitPoint, out Vector3 surfaceUp, out string source))
        {
            Debug.LogWarning("[CanvasTableSnapper] Nessuna superficie orizzontale trovata.");
            return;
        }

        // Mantiene la direzione in cui "guardava" il canvas (lontano da chi lo teneva)
        Vector3 awayFromUser = Vector3.ProjectOnPlane(transform.forward, surfaceUp);
        if (awayFromUser.sqrMagnitude < 0.0001f)
            awayFromUser = Vector3.ProjectOnPlane(Vector3.forward, surfaceUp);
        awayFromUser.Normalize();

        Quaternion snappedRotation = Quaternion.LookRotation(-surfaceUp, awayFromUser);

        // Calcola lo spessore verticale con la rotazione finale, poi ripristina
        Quaternion originalRotation = transform.rotation;
        transform.rotation = snappedRotation;
        float halfThickness = GetVerticalHalfExtent();
        transform.rotation = originalRotation;

        Vector3 snappedPosition = hitPoint + surfaceUp * (halfThickness + heightOffsetFromTable);

        transform.position = snappedPosition;
        transform.rotation = snappedRotation;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.ConfirmCalibration(snappedPosition, snappedRotation);
        }

        Debug.Log($"[CanvasTableSnapper] Snap ({source}) | punto: {hitPoint} | up: {surfaceUp} | " +
                  $"awayFromUser: {awayFromUser} | halfThickness: {halfThickness:F3}");
    }

    // ------------------------------------------------------------------
    // Ricerca superficie
    // ------------------------------------------------------------------

    private bool TryFindSurface(Ray ray, out Vector3 point, out Vector3 up, out string source)
    {
        point = default;
        up = Vector3.up;
        source = null;

        // 1) Environment Raycast (depth)
        if (useEnvironmentRaycast)
        {
            if (environmentRaycastManager == null)
            {
                Log("EnvRaycast: nessun EnvironmentRaycastManager in scena.");
            }
            else if (!environmentRaycastManager.enabled || !environmentRaycastManager.gameObject.activeInHierarchy)
            {
                Log("EnvRaycast: EnvironmentRaycastManager disabilitato.");
            }
            else if (!EnvironmentRaycastManager.IsSupported)
            {
                Log("EnvRaycast: NON supportato (tipico del simulatore o di un visore senza Depth API).");
            }
            else
            {
                bool envOk = environmentRaycastManager.Raycast(ray, out EnvironmentRaycastHit envHit, raycastDownDistance);
                Log($"EnvRaycast: ok={envOk} status={envHit.status} point={envHit.point} normal={envHit.normal}");

                if (envOk && IsAcceptable(envHit.point, envHit.normal))
                {
                    point = envHit.point;
                    up = envHit.normal;
                    source = "EnvironmentRaycast";
                    return true;
                }
            }
        }

        // 2) MRUK scene model
        if (useMruk)
        {
            if (MRUK.Instance == null)
            {
                Log("MRUK: nessun componente MRUK in scena.");
            }
            else
            {
                MRUKRoom room = MRUK.Instance.GetCurrentRoom();
                if (room == null)
                {
                    Log("MRUK: nessuna stanza caricata (controlla Scene Data Source / Synthetic Environment).");
                }
                else
                {
                    bool mrukOk = room.Raycast(ray, raycastDownDistance, out RaycastHit mrukHit, out MRUKAnchor anchor);
                    Log($"MRUK: stanza '{room.name}' anchors={room.Anchors.Count} raycast ok={mrukOk} " +
                        (mrukOk ? $"anchor={(anchor != null ? anchor.name : "null")} point={mrukHit.point} normal={mrukHit.normal}" : ""));

                    if (mrukOk && IsAcceptable(mrukHit.point, mrukHit.normal))
                    {
                        point = mrukHit.point;
                        up = mrukHit.normal;
                        source = $"MRUK ({(anchor != null ? anchor.name : "anchor")})";
                        return true;
                    }
                }
            }
        }

        // 3) Collider fisici (ignora i collider del canvas stesso)
        if (usePhysicsFallback)
        {
            RaycastHit[] hits = Physics.RaycastAll(ray, raycastDownDistance, surfaceLayers, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            Log($"Physics: {hits.Length} collider colpiti");

            foreach (RaycastHit h in hits)
            {
                if (h.collider.transform.IsChildOf(transform))
                    continue;

                Log($"Physics: '{h.collider.name}' a {h.distance:F2} m, normal={h.normal}");

                if (IsAcceptable(h.point, h.normal))
                {
                    point = h.point;
                    up = h.normal;
                    source = $"Physics ({h.collider.name})";
                    return true;
                }
            }
        }

        return false;
    }

    private bool IsAcceptable(Vector3 hitPoint, Vector3 normal)
    {
        // Solo superfici orizzontali con la normale verso l'alto
        float dot = Vector3.Dot(normal.normalized, Vector3.up);
        if (dot < minUpDot)
        {
            Log($"Scartata: normale non verso l'alto (dot={dot:F2} < {minUpDot:F2}). normal={normal}");
            return false;
        }

        if (!allowFloor && MRUK.Instance != null)
        {
            MRUKRoom room = MRUK.Instance.GetCurrentRoom();
            if (room != null && room.FloorAnchor != null)
            {
                float floorY = room.FloorAnchor.transform.position.y;
                if (hitPoint.y - floorY < minHeightAboveFloor)
                {
                    Log($"Scartata: troppo vicina al pavimento ({hitPoint.y - floorY:F2} m).");
                    return false;
                }
            }
        }

        return true;
    }

    private void Log(string message)
    {
        if (logDiagnostics)
            Debug.Log($"[CanvasTableSnapper] {message}");
    }

    // ------------------------------------------------------------------
    // Utility
    // ------------------------------------------------------------------

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
}