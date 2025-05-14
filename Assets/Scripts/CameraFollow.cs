using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target; // Il giocatore
    public Vector3 offset = new Vector3(0f, 3f, -6f); // Altezza e distanza
    public float sensitivityX = 120f; // Velocità rotazione orizzontale
    public float sensitivityY = 100f; // Velocità rotazione verticale
    public float minY = -30f; // Limite inferiore rotazione verticale
    public float maxY = 60f;  // Limite superiore rotazione verticale

    private float rotX = 0f; // Rotazione verticale (pitch)
    private float rotY = 0f; // Rotazione orizzontale (yaw)


    void LateUpdate()
    {
        // Input del mouse
        rotY += Input.GetAxis("Mouse X") * sensitivityX * Time.deltaTime;
        rotX -= Input.GetAxis("Mouse Y") * sensitivityY * Time.deltaTime;
        rotX = Mathf.Clamp(rotX, minY, maxY);

        // Calcola rotazione
        Quaternion rotation = Quaternion.Euler(rotX, rotY, 0);

        // Posiziona la camera dietro il target
        Vector3 desiredPosition = target.position + rotation * offset;
        transform.position = desiredPosition;

        // Guarda sempre il target
        transform.LookAt(target.position + Vector3.up * 1.5f); // Leggermente sopra il centro del player
    }
}
