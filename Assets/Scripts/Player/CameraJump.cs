using UnityEngine;

public class FollowWithVerticalThreshold : MonoBehaviour
{
    public Transform target;            // Il personaggio
    public float verticalThreshold = 2f;
    public float followSpeed = 5f;

    private float currentY;

    void Start()
    {
        if (target != null)
            currentY = target.position.y;

        // Assicura che il GameObject inizi alla stessa posizione del target
        transform.position = new Vector3(target.position.x, currentY, target.position.z);
    }

    void LateUpdate()
    {
        if (target == null) return;

        float targetY = target.position.y;
        float deltaY = targetY - currentY;

        if (Mathf.Abs(deltaY) > verticalThreshold)
        {
            currentY = Mathf.Lerp(currentY, targetY, Time.deltaTime * followSpeed);
        }

        // Muove il proxy solo sull’asse Y
        transform.position = new Vector3(target.position.x, currentY, target.position.z);
    }
}
