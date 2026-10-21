using UnityEngine;

public class CanvasFaceUser : MonoBehaviour
{
    [Header("Riferimenti")]
    [Tooltip("Se vuoto usa Camera.main (CenterEyeAnchor in OVRCameraRig)")]
    [SerializeField] private Transform target;

    [Header("Comportamento")]
    [Tooltip("Angolo (gradi) oltre il quale il canvas inizia a riorientarsi")]
    [SerializeField] private float angleThreshold = 15f;
    [Tooltip("Velocità di rotazione (gradi/secondo)")]
    [SerializeField] private float rotationSpeed = 180f;
    [Tooltip("Ruota solo sull'asse Y (il canvas resta verticale)")]
    [SerializeField] private bool yawOnly = true;
    [Tooltip("Se true il canvas guarda l'utente, se il testo risulta specchiato metti false")]
    [SerializeField] private bool flipForward = true;

    private bool _isRotating;

    private void Start()
    {
        if (target == null && Camera.main != null)
            target = Camera.main.transform;
    }

    private void LateUpdate()
    {
        if (target == null) return;

        Quaternion desired = GetDesiredRotation();
        float angle = Quaternion.Angle(transform.rotation, desired);

        // Isteresi: parte oltre la soglia, si ferma quando è quasi allineato
        if (!_isRotating && angle > angleThreshold)
            _isRotating = true;

        if (_isRotating)
        {
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, desired, rotationSpeed * Time.deltaTime);

            if (angle < 0.5f)
                _isRotating = false;
        }
    }

    private Quaternion GetDesiredRotation()
    {
        Vector3 dir = transform.position - target.position;
        /* if (yawOnly) dir.y = 0f; */
        if (dir.sqrMagnitude < 0.0001f) return transform.rotation;

        // Un Canvas World Space mostra il contenuto sul lato -Z, quindi
        // per "guardare" l'utente il forward deve puntare via da lui.
        if (!flipForward) dir = -dir;
        return Quaternion.LookRotation(dir, Vector3.up);
    }
}