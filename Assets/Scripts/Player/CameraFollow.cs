/*using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public float sensitivityX = 120f;
    public float sensitivityY = 100f;
    public float minY = -30f;
    public float maxY = 60f;
    public float headHeight = 2.0f;

    private Vector3 initialOffset;
    private float rotX;
    private float rotY;

    void Start()
    {
        initialOffset = transform.position - target.position;

        Vector3 angles = transform.eulerAngles;
        rotY = angles.y + 180f;
        rotX = angles.x;
    }

    void LateUpdate()
    {
        if (target == null) return;

        rotY += Input.GetAxis("Mouse X") * sensitivityX * Time.deltaTime;

        // Qui rotX aumenta quando muovi il mouse verso l’alto,
        // ma applichiamo il segno invertito nella rotazione
        rotX += Input.GetAxis("Mouse Y") * sensitivityY * Time.deltaTime;
        rotX = Mathf.Clamp(rotX, minY, maxY);

        // Invertiamo il segno solo qui per ottenere il comportamento corretto
        Quaternion rotation = Quaternion.Euler(-rotX, rotY, 0f);

        Vector3 rotatedOffset = rotation * initialOffset;

        transform.position = target.position + rotatedOffset;

        transform.LookAt(target.position + Vector3.up * headHeight);
    }
}
*/
using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Target")]
    public Transform target;
    public float headHeight = 2.0f;

    [Header("Sensitività")]
    public float sensitivityX = 120f;
    public float sensitivityY = 100f;

    [Header("Limiti rotazione verticale")]
    public float minY = -30f;
    public float maxY = 60f;

    [Header("Zoom")]
    public float zoomSpeed = 2f;
    public float minZoom = 2f;
    public float maxZoom = 10f;

    [Header("Smooth follow")]
    public float smoothSpeed = 10f;

    private Vector3 initialOffset;
    private Vector3 currentVelocity;
    private float rotX;
    private float rotY;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        initialOffset = transform.position - target.position;

        Vector3 angles = transform.eulerAngles;
        rotY = angles.y;
        rotX = -angles.x; // Invertito per coerenza con asse Y "in su = guarda in su"
    }

    void Update()
    {
        // Input rotazione
        rotY += Input.GetAxis("Mouse X") * sensitivityX * Time.deltaTime;
        rotX -= Input.GetAxis("Mouse Y") * sensitivityY * Time.deltaTime; // NOTA: "-" per movimento corretto
        rotX = Mathf.Clamp(rotX, minY, maxY);

        // Input zoom
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        float zoom = Mathf.Clamp(initialOffset.magnitude - scroll * zoomSpeed, minZoom, maxZoom);
        initialOffset = initialOffset.normalized * zoom;
    }

    void LateUpdate()
    {
        if (target == null) return;

        Quaternion rotation = Quaternion.Euler(rotX, rotY, 0f);
        Vector3 desiredPosition = target.position + rotation * initialOffset;

        // Raycast per gestire ostacoli
        Vector3 direction = desiredPosition - (target.position + Vector3.up * headHeight);
        float distance = direction.magnitude;

        if (Physics.Raycast(target.position + Vector3.up * headHeight, direction.normalized, out RaycastHit hit, distance))
        {
            desiredPosition = hit.point;
        }

        // Movimento fluido
        transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref currentVelocity, 1f / smoothSpeed);

        // Guarda il target (altezza testa)
        transform.LookAt(target.position + Vector3.up * headHeight);
    }
}
