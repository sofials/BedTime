using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Golem_Projectile : MonoBehaviour
{
    public float launchAngle = 45f; // Gradi
    public float gravityMultiplier = 1f;
    public float lifetime = 5f;
    public float damage = 20f;
    public float pushForce = 5f;

    [Header("FX")]
    public CFXR_EffectController impactFX;

    private Rigidbody rb;

    public void Initialize(Vector3 targetPosition)
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = true;
        Physics.gravity *= gravityMultiplier;

        Vector3 velocity = CalculateLaunchVelocity(targetPosition, launchAngle);
        rb.linearVelocity = velocity;

        transform.rotation = Quaternion.LookRotation(velocity);
        Destroy(gameObject, lifetime);
    }

    private Vector3 CalculateLaunchVelocity(Vector3 target, float angle)
    {
        Vector3 dir = target - transform.position;
        float h = dir.y;
        dir.y = 0;
        float distance = dir.magnitude;
        float radAngle = angle * Mathf.Deg2Rad;
        dir.y = distance * Mathf.Tan(radAngle);

        distance += h / Mathf.Tan(radAngle);

        float velocity = Mathf.Sqrt(distance * Physics.gravity.magnitude / Mathf.Sin(2 * radAngle));
        return velocity * dir.normalized;
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

            TriggerImpactEffect();
            Destroy(gameObject);
        }
        else if (!other.isTrigger)
        {
            TriggerImpactEffect();
            Destroy(gameObject);
        }
    }

    private void TriggerImpactEffect()
    {
        if (impactFX != null)
        {
            impactFX.transform.parent = null; // stacca l'effetto dal proiettile
            impactFX.gameObject.SetActive(true);
            impactFX.PlayEffect();
            Destroy(impactFX.gameObject, 3f); // distruggi dopo che l'effetto è finito
        }
    }
}
