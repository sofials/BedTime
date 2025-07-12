using UnityEngine;

public class DestructibleObject : MonoBehaviour
{
    public float maxHealth = 50f;
    private float currentHealth;

    public CFXR_EffectController deathEffect;

    private void Start()
    {
        currentHealth = maxHealth;
        if (deathEffect != null)
            deathEffect.StopEffect();
    }

    public void TakeDamage(float amount)
    {
        currentHealth -= amount;
        if (currentHealth <= 0f)
            Die();
    }

    private void Die()
    {
        if (deathEffect != null)
            deathEffect.PlayEffect();

        var renderer = GetComponentInChildren<Renderer>();
        if (renderer != null)
            renderer.enabled = false;

        Destroy(gameObject, 2f);
    }
}
