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

        // Filtra per layer: solo la hitbox del player
        if (other.gameObject.layer == LayerMask.NameToLayer("PlayerAttackHitbox"))
        {
            Debug.Log("[HurtBox_Enemy] Layer corretto rilevato (PlayerAttackHitbox)");
            AttackHitBox playerHitbox = other.GetComponent<AttackHitBox>();
            if (playerHitbox != null && enemy != null)
            {
                Debug.Log("[HurtBox_Enemy] AttackHitBox rilevata, chiamo TakeDamage su Enemy");
                enemy.TakeDamage(25f);
            }
            else
            {
                Debug.LogWarning("[HurtBox_Enemy] Enemy reference o AttackHitBox è NULL!");
            }
        }
        else
        {
            Debug.Log("[HurtBox_Enemy] Collider NON è sul layer PlayerAttackHitbox");
        }
    }
}