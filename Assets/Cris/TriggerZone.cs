using UnityEngine;
using UnityEngine.Events;

public class TriggerZone : MonoBehaviour
{
    [Header("Trigger Settings")]
    public bool oneShot = false;
    [SerializeField] private bool alreadyEntered = false;
    [SerializeField] private bool alreadyExited = false;

    [Header("Filtering")]
    public string collisionTag;

    [Header("Events")]
    public UnityEvent<Collider> onTriggerEnter;
    public UnityEvent<Collider> onTriggerExit;
    public UnityEvent onTriggerStay;

    private void OnTriggerEnter(Collider other)
    {
        if (!ShouldProcessCollision(other) || alreadyEntered)
            return;

        onTriggerEnter?.Invoke(other);

        if (oneShot)
            alreadyEntered = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (!ShouldProcessCollision(other) || alreadyExited)
            return;

        onTriggerExit?.Invoke(other);

        if (oneShot)
            alreadyExited = true;
    }

    private void OnTriggerStay(Collider other)
    {
        if (!ShouldProcessCollision(other))
            return;

        onTriggerStay?.Invoke();
    }

    private bool ShouldProcessCollision(Collider other)
    {
        if (!string.IsNullOrEmpty(collisionTag) && !other.CompareTag(collisionTag))
            return false;

        return true;
    }

    public void ResetTrigger()
    {
        alreadyEntered = false;
        alreadyExited = false;
    }
}