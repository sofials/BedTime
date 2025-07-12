using UnityEngine;
using UnityEngine.AI;

public class Golem : MonoBehaviour
{
    public Transform player;
    public NavMeshAgent agent;

    [Header("Vision Settings")]
    public float viewRadius = 12f;
    public float viewAngle = 110f;
    public LayerMask playerMask;
    public LayerMask obstacleMask;

    [Header("Attack Settings")]
    public float attackRange = 2f;      // come il fungo
    public float attackCooldown = 2f;
    private float attackTimer = 0f;

    [Header("Attack Effects")]
    public float damage = 20f;
    public float pushForce = 5f;

    [Header("Animation")]
    public Animator animator;

    private bool playerVisible = false;
    private bool isAttacking = false;

    private void Update()
    {
        if (player == null) return;

        attackTimer -= Time.deltaTime;
        UpdatePlayerVisibility();

        if (playerVisible)
        {
            float dist = Vector3.Distance(transform.position, player.position);

            Vector3 dir = (player.position - transform.position).normalized;
            dir.y = 0;
            if (dir != Vector3.zero)
                transform.rotation = Quaternion.LookRotation(dir);

            if (dist <= attackRange)
            {
                if (!isAttacking && attackTimer <= 0f)
                {
                    isAttacking = true;
                    attackTimer = attackCooldown;

                    animator.SetTrigger("AttackMelee");

                    if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
                        agent.isStopped = true;
                }
            }
            else
            {
                isAttacking = false;

                if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
                {
                    agent.isStopped = false;
                    agent.SetDestination(player.position);
                }

                animator.SetBool("isWalking", true);
            }
        }
        else
        {
            if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                agent.isStopped = false;
                agent.SetDestination(player.position);
            }

            animator.SetBool("isWalking", true);
        }
    }

    private void UpdatePlayerVisibility()
    {
        playerVisible = false;
        Collider[] hits = Physics.OverlapSphere(transform.position, viewRadius, playerMask);
        foreach (var hit in hits)
        {
            Vector3 dirToPlayer = (hit.transform.position - transform.position).normalized;
            if (Vector3.Angle(transform.forward, dirToPlayer) < viewAngle / 2f)
            {
                float distanceToPlayer = Vector3.Distance(transform.position, hit.transform.position);
                if (!Physics.Raycast(transform.position, dirToPlayer, distanceToPlayer, obstacleMask))
                {
                    playerVisible = true;
                    return;
                }
            }
        }
    }

    // ✳️ Animation Event — chiama questo dentro animazione
    public void EnemyAttackHitbox()
    {
        Collider[] hits = Physics.OverlapBox(
            transform.position + transform.forward * (attackRange * 0.5f),
            new Vector3(1f, 1f, 1f),
            transform.rotation,
            LayerMask.GetMask("PlayerHurtbox")
        );

        foreach (var hit in hits)
        {
            if (hit.gameObject.CompareTag("PlayerHurtbox"))
            {
                var hurtbox = hit.GetComponent<HurtBox>();
                if (hurtbox != null)
                {
                    Vector3 pushDir = (hurtbox.transform.position - transform.position).normalized;
                    hurtbox.OnHit(pushDir, pushForce, damage);
                }
            }
        }

        // Fine attacco: pronto per nuovo attacco
        isAttacking = false;
    }
}
