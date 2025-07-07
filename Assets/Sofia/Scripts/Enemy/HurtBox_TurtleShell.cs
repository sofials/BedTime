using UnityEngine;

public class HurtBox_TurtleShell : MonoBehaviour
{
    public float reflectDamage = 5f;      // Danno riflesso visibile in Inspector

    [Tooltip("Assegna qui la HurtBox del player da Inspector")]
    public HurtBox playerHurtBox;         // Riferimento assegnato da Inspector

    private int lastAttackId = -1;

    private void OnTriggerEnter(Collider other) => ReflectAttack(other);
    private void OnTriggerStay(Collider other)  => ReflectAttack(other);

    private void ReflectAttack(Collider other)
    {
        if (!CompareTag("TurtleShellHurtbox"))
            return;

        if (other.gameObject.layer != LayerMask.NameToLayer("PlayerAttackHitbox") ||
            !other.CompareTag("PlayerAttackHitbox"))
            return;

        var playerAttack = other.GetComponentInParent<PlayerAttack>();
        if (playerAttack == null || !playerAttack.isAttacking)
            return;

        if (playerAttack.AttackId == lastAttackId)
            return;

        lastAttackId = playerAttack.AttackId;

        // Usa la HurtBox assegnata da Inspector per infliggere danno
        if (playerHurtBox != null)
        {
            Debug.Log("Infliggo danno riflesso al player");
            playerHurtBox.OnHit(Vector3.zero, 0f, reflectDamage);
        }
        else
        {
            Debug.LogWarning("HurtBox player non assegnata!");
        }

        GetComponentInParent<Animator>()?.SetTrigger("GetHit");
    }
}
