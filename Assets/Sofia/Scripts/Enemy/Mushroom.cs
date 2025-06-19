using UnityEngine;

public class Mushroom : EnemyAI
{
    [Header("Mushroom Specific")]
    [Tooltip("Forza della spinta applicata al player")]
    public float pushForce = 12f;

    protected override void Awake()
    {
        base.Awake();

        // Valori personalizzati per il funghetto
        moveSpeed = 4f;
        maxHealth = 80f;
        timeBetweenAttacks = 1.5f;
        dizzyDuration = 1.8f;

        currentHealth = maxHealth;

        if (agent != null)
            agent.speed = moveSpeed;
    }

    protected override void AttackPlayer()
    {
        // Se morto, non fa nulla
        if (isDead || isDizzy) return;

        // Ferma il movimento durante l'attacco
        if (agent != null)
            agent.SetDestination(transform.position);

        transform.LookAt(player);

        if (!alreadyAttacked)
        {
            Debug.Log("Mushroom attacks the player!");

            alreadyAttacked = true;

            ThirdPersonController playerController = player.GetComponent<ThirdPersonController>();
            if (playerController != null)
            {
                Vector3 knockbackDir = (player.position - transform.position).normalized;
                playerController.ApplyKnockback(knockbackDir, 5f, 0.2f);
            }

            Invoke(nameof(ResetAttack), timeBetweenAttacks);
        }

        // Applica la spinta
        Vector3 pushDir = (player.position - transform.position).normalized;
        player.GetComponent<ThirdPersonController>()?.ApplyExternalPush(pushDir * pushForce);
    }
}
