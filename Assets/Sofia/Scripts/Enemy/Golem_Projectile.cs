using UnityEngine;

[RequireComponent(typeof(Collider))]
public class Golem_Projectile : MonoBehaviour
{
    public float speed = 10f;
    public float damage = 10f;
    public float pushForce = 5f;
    public float maxLifetime = 5f;

    private CFXR_EffectController fxController;
    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        fxController = GetComponentInChildren<CFXR_EffectController>();
    }

    private void OnEnable()
    {
        if (fxController != null)
            fxController.PlayEffect();

        // autodistruzione dopo un po' se non colpisce nulla
        Invoke(nameof(DestroySelf), maxLifetime);
    }

    private void FixedUpdate()
    {
        rb.MovePosition(transform.position + transform.forward * speed * Time.fixedDeltaTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            HurtBox hurtBox = other.GetComponent<HurtBox>();
            if (hurtBox != null)
            {
                Vector3 pushDir = (other.transform.position - transform.position).normalized;
                hurtBox.OnHit(pushDir, pushForce, damage);
            }
        }

        if (fxController != null)
            fxController.StopEffect();

        Destroy(gameObject);
    }

    private void DestroySelf()
    {
        if (fxController != null)
            fxController.StopEffect();

        Destroy(gameObject);
    }
}
