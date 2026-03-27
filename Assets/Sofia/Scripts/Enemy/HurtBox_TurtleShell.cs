using UnityEngine;

public class HurtBox_TurtleShell : MonoBehaviour
{
    public float reflectDamage = 5f;
    public HurtBox playerHurtBox;

    private int lastAttackId = -1;
    private TurtleShell turtleShell;

    private void Awake()
    {
        turtleShell = GetComponentInParent<TurtleShell>();
        if (turtleShell == null)
            Debug.LogWarning("TurtleShell script non trovato nel genitore!");
    }

    private void OnTriggerEnter(Collider other)
    {
        TryReflectAttack(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryReflectAttack(other);
    }

    private void TryReflectAttack(Collider other)
    {
        if (other.gameObject.layer != LayerMask.NameToLayer("PlayerAttackHitbox") ||
            !other.CompareTag("PlayerAttackHitbox"))
            return;

        var playerAttack = other.GetComponentInParent<PlayerAttack>();
        if (playerAttack == null || !playerAttack.isAttacking)
            return;

        if (playerAttack.AttackId == lastAttackId)
            return;

        lastAttackId = playerAttack.AttackId;

        if (turtleShell == null) return;

        // Registra colpo per abilitare effetti punch
        playerAttack.RegisterSuccessfulHit();

        if (turtleShell.isSlow)
        {
            turtleShell.TakeDamage(5f); // infligge 5 danni durante slow
        }
        else
        {
            // Riflette danno al player
            if (playerHurtBox != null)
            {
                playerHurtBox.OnHit(Vector3.zero, 0f, reflectDamage);
            }
            else
            {
                Debug.LogWarning("HurtBox player non assegnata!");
            }

            turtleShell.GetComponent<Animator>()?.SetTrigger("GetHit");
        }
    }
}
