using UnityEngine;

public class HurtBox_Golem : MonoBehaviour
{
    private int lastAttackId = -1;
    private Golem golem;

    private void Awake()
    {
        golem = GetComponentInParent<Golem>();
    }

    private void OnTriggerEnter(Collider other)
    {
        TryApplyDamage(other);
        // Rimosso TryApplySlowdown perché non serve
    }

    private void OnTriggerStay(Collider other)
    {
        TryApplyDamage(other);
        // Rimosso TryApplySlowdown perché non serve
    }

    private void TryApplyDamage(Collider other)
    {
        if (other.gameObject.layer == LayerMask.NameToLayer("PlayerAttackHitbox") &&
            other.CompareTag("PlayerAttackHitbox"))
        {
            var playerAttack = other.GetComponentInParent<PlayerAttack>();
            if (playerAttack != null && playerAttack.isAttacking)
            {
                if (playerAttack.AttackId == lastAttackId)
                    return;

                lastAttackId = playerAttack.AttackId;

                var allComponents = GetComponentsInParent<MonoBehaviour>();
                foreach (var comp in allComponents)
                {
                    var method = comp.GetType().GetMethod("TakeDamage");
                    if (method != null)
                    {
                        method.Invoke(comp, new object[] { 25f });
                        Debug.Log("Enemy colpito da PlayerAttack");
                        playerAttack.RegisterSuccessfulHit();
                        break;
                    }
                }
            }
        }
    }
}
