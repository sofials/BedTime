using UnityEngine;

public class EnemyHurtBox : MonoBehaviour
{
    public Enemy enemy;

    private void Awake()
    {
        if (enemy == null)
            enemy = GetComponentInParent<Enemy>();
    }

    public void OnHit(float damage)
    {
        if (enemy != null)
            enemy.TakeDamage(damage);
    }

    private void OnTriggerEnter(Collider other)
    {
        // Esempio: se la hitbox del player ha un certo tag/layer
        if (other.CompareTag("PlayerAttackHitbox"))
        {
            OnHit(25f); // oppure prendi il danno dalla hitbox del player
        }
    }
}