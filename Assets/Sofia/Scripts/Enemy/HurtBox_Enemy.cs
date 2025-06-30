using UnityEngine;

public class HurtBox_Enemy : MonoBehaviour
{
    public Enemy enemy;

    private void Awake()
    {
        if (enemy == null)
            enemy = GetComponentInParent<Enemy>();
    }

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("[HurtBox_Enemy] TriggerEnter con: " + other.name);

        // Controlla anche il layer
        if (other.gameObject.layer == LayerMask.NameToLayer("PlayerAttackHitbox") &&
            other.CompareTag("PlayerAttackHitbox"))
        {
            var playerAttack = other.GetComponentInParent<PlayerAttack>();
            if (playerAttack != null && playerAttack.isAttacking && enemy != null)
            {
                Debug.Log("[HurtBox_Enemy] Attacco del giocatore rilevato, chiamo TakeDamage su Enemy");
                enemy.TakeDamage(25f);
            }
            else
            {
                Debug.Log("[HurtBox_Enemy] Nessun attacco del giocatore rilevato o Enemy è NULL!");
            }
        }
        else
        {
            Debug.Log("[HurtBox_Enemy] Collider NON è sul layer PlayerAttackHitbox");
        }
    }
}