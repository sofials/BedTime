using UnityEngine;

public class HurtBox_Enemy : MonoBehaviour
{
    private int lastAttackId = -1;

    private void OnTriggerEnter(Collider other)
    {
        TryApplyDamage(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryApplyDamage(other);
    }

    private void TryApplyDamage(Collider other)
    {
        if (other.gameObject.layer == LayerMask.NameToLayer("PlayerAttackHitbox") &&
            other.CompareTag("PlayerAttackHitbox"))
        {
            var playerAttack = other.GetComponentInParent<PlayerAttack>();
            if (playerAttack != null && playerAttack.isAttacking)
            {
                // Ignora se è lo stesso attacco già contato
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
                        break;
                    }
                }
            }
        }
    }
}
