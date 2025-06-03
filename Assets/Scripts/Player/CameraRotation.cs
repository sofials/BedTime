using UnityEngine;

public class CameraRotation : MonoBehaviour
{
    public float rotationSpeed = 5f;
    public Transform player;

    private float yaw = 0f;

    void Update()
    {
        float mouseX = Input.GetAxis("Mouse X");
        yaw += mouseX * rotationSpeed;
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        player.forward = transform.forward;
    }
}

