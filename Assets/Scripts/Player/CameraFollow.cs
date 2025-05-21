using UnityEngine;

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
