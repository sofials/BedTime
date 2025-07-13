using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Golem_Projectile : MonoBehaviour
{
    public float launchAngle = 10f; // in gradi
    public float lifetime = 5f;
    public float damage = 20f;
    public float pushForce = 10f;
    public float velocityMultiplier = 10f; // <-- Aggiunto per velocità extra
    private Rigidbody rb;
    public float extraGravityForce = 15f;  // forza extra verso il basso

private void FixedUpdate()
{
    if (rb != null)
    {
        rb.AddForce(Vector3.down * extraGravityForce, ForceMode.Acceleration);
    }
}


    public void Initialize(Vector3 targetPosition)
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = true;

        Vector3 launchVelocity;
        bool success = TryCalculateArcVelocity(targetPosition, launchAngle, out launchVelocity);

        if (success)
        {
            rb.linearVelocity = launchVelocity * velocityMultiplier;
            transform.rotation = Quaternion.LookRotation(launchVelocity);
        }
        else
        {
            Debug.LogWarning("Golem_Projectile: Traiettoria non calcolabile, uso lancio diretto.");
            rb.linearVelocity = (targetPosition - transform.position).normalized * 10f * velocityMultiplier;
        }

        Destroy(gameObject, lifetime);
    }

    private bool TryCalculateArcVelocity(Vector3 target, float angleDeg, out Vector3 velocity)
    {
        Vector3 origin = transform.position;
        Vector3 toTarget = target - origin;

        float g = Physics.gravity.y;
        float angleRad = angleDeg * Mathf.Deg2Rad;

        Vector3 toTargetXZ = new Vector3(toTarget.x, 0f, toTarget.z);
        float distance = toTargetXZ.magnitude;
        float yOffset = toTarget.y;

        float cosAngle = Mathf.Cos(angleRad);
        float sinAngle = Mathf.Sin(angleRad);

        float underSqrt = (g * distance * distance) / (2 * (yOffset - Mathf.Tan(angleRad) * distance) * cosAngle * cosAngle);

        if (underSqrt < 0)
        {
            velocity = Vector3.zero;
            return false;
        }

        float speed = Mathf.Sqrt(underSqrt);
        velocity = toTargetXZ.normalized * speed * cosAngle + Vector3.up * speed * sinAngle;
        return true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("PlayerHurtbox"))
        {
            HurtBox hurtbox = other.GetComponent<HurtBox>();
            if (hurtbox != null)
            {
                Vector3 pushDir = (other.transform.position - transform.position).normalized;
                hurtbox.OnHit(pushDir, pushForce, damage);
            }
            Destroy(gameObject);
        }
        else if (!other.isTrigger)
        {
            Destroy(gameObject);
        }
    }
}


