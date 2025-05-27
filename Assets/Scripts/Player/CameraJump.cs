using UnityEngine;
using Unity.Cinemachine;

[RequireComponent(typeof(CinemachineCamera))]
public class SmoothOrbitRotation : MonoBehaviour
{
    public Transform target;
    public float distance = 5f;
    public float heightOffset = 1.5f;
    public float rotationLerpSpeed = 5f;

    private Quaternion currentRotation;

    void Start()
    {
        if (target != null)
            currentRotation = Quaternion.Euler(0, target.eulerAngles.y, 0);
    }

    void LateUpdate()
    {
        if (target == null) return;

        // Rotazione desiderata attorno al target
        Quaternion targetRotation = Quaternion.Euler(0f, target.eulerAngles.y, 0f);

        // Interpolazione fluida
        currentRotation = Quaternion.Slerp(currentRotation, targetRotation, Time.deltaTime * rotationLerpSpeed);

        // Calcolo della posizione orbitale
        Vector3 offset = currentRotation * new Vector3(0, 0, -distance);
        Vector3 targetPosition = target.position + Vector3.up * heightOffset;

        transform.position = targetPosition + offset;
        transform.LookAt(targetPosition);
    }
}
