using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Golem_Projectile : MonoBehaviour
{
    public float launchAngle = 10f; // in gradi
    public float lifetime = 5f;
    public float damage = 20f;
    public float pushForce = 10f;
    public float velocityMultiplier = 1f; // Formula già calcola velocità corretta
    private Rigidbody rb;
    public float extraGravityForce = 15f;

    [Header("Target Adjustment")]
    [Tooltip("Offset verticale per correggere la mira (positivo = mira più alto, es. 1 per centro player)")]
    public float targetHeightOffset = 0f;

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

        // Applica offset per correggere l'altezza
        Vector3 adjustedTarget = targetPosition + Vector3.up * targetHeightOffset;

        Vector3 launchVelocity;
        bool success = TryCalculateArcVelocity(adjustedTarget, launchAngle, out launchVelocity);

        if (success)
        {
            rb.linearVelocity = launchVelocity * velocityMultiplier;
            transform.rotation = Quaternion.LookRotation(launchVelocity);
        }
        else
        {
            Debug.LogWarning("Golem_Projectile: Traiettoria non calcolabile, uso lancio diretto.");
            rb.linearVelocity = (adjustedTarget - transform.position).normalized * 10f * velocityMultiplier;
        }

        Destroy(gameObject, lifetime);
    }

    private bool TryCalculateArcVelocity(Vector3 target, float angleDeg, out Vector3 velocity)
    {
        Vector3 origin = transform.position;
        Vector3 toTarget = target - origin;

        // Gravità positiva (valore assoluto + extra gravity)
        float g = Mathf.Abs(Physics.gravity.y) + extraGravityForce;

        float angleRad = angleDeg * Mathf.Deg2Rad;

        Vector3 toTargetXZ = new Vector3(toTarget.x, 0f, toTarget.z);
        float distance = toTargetXZ.magnitude;
        float yOffset = toTarget.y;

        float cosAngle = Mathf.Cos(angleRad);
        float sinAngle = Mathf.Sin(angleRad);

        // Formula corretta: v² = (g * d²) / (2 * cos²θ * (d * tanθ - y))
        float numerator = g * distance * distance;
        float denominator = 2f * cosAngle * cosAngle * (distance * Mathf.Tan(angleRad) - yOffset);

        if (denominator <= 0)
        {
            velocity = Vector3.zero;
            return false;
        }

        float speedSquared = numerator / denominator;

        if (speedSquared <= 0)
        {
            velocity = Vector3.zero;
            return false;
        }

        float speed = Mathf.Sqrt(speedSquared);
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
