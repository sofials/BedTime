using UnityEngine;
using CartoonFX;  // Assicurati che sia il namespace corretto

public class Projectile : MonoBehaviour
{
    public float speed = 15f;
    public float damage = 15f;
    public float lifetime = 5f;

    private Vector3 direction;
    private Rigidbody rb;

    private CFXR_EffectController effectController;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        effectController = GetComponentInChildren<CFXR_EffectController>();

        if (effectController != null)
            effectController.StopEffect();
    }

    public void SetTarget(Vector3 targetPosition)
    {
        direction = (targetPosition - transform.position).normalized;
        rb.linearVelocity = direction * speed;

        if (effectController != null)
            effectController.PlayEffect();
    }

    private void Start()
    {
        Destroy(gameObject, lifetime);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("PlayerHurtbox"))
        {
            var hurtbox = other.GetComponent<HurtBox>();
            if (hurtbox != null)
            {
                Vector3 pushDir = (other.transform.position - transform.position).normalized;
                hurtbox.OnHit(pushDir, 0f, damage);
            }

            if (effectController != null)
                effectController.StopEffect();

            Destroy(gameObject);
        }
        else if (!other.CompareTag("Enemy"))
        {
            if (effectController != null)
                effectController.StopEffect();

            Destroy(gameObject);
        }
    }
}
