using UnityEngine;

public class HurtBox_Enemy : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.layer == LayerMask.NameToLayer("PlayerAttackHitbox") &&
            other.CompareTag("PlayerAttackHitbox"))
        {
            var playerAttack = other.GetComponentInParent<PlayerAttack>();
            if (playerAttack != null && playerAttack.isAttacking)
            {
                var allComponents = GetComponentsInParent<MonoBehaviour>();
                foreach (var comp in allComponents)
                {
                    var method = comp.GetType().GetMethod("TakeDamage");
                    if (method != null)
                    {
                        method.Invoke(comp, new object[] { 25f });
                        break;
                    }
                }
            }
        }
    }
}