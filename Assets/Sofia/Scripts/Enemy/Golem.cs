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
    public float attackRange = 10f;
    public float rangedRange = 100f;
    public float attackCooldown = 2f;
    private float attackTimer = 0f;

    [Header("Attack Effects")]
    public float damage = 20f;
    public float pushForce = 5f;

    [Header("Projectile Settings")]
    public GameObject projectilePrefab;
    public Transform projectileSpawnPoint;

    [Header("Animation")]
    public Animator animator;

    [Header("Stats")]
    public float maxHealth = 30f;
    private float currentHealth;
    private bool isDead = false;

    private bool playerVisible = false;
    private bool isAttacking = false;

    private void Start()
    {
        currentHealth = maxHealth;
    }

   private void Update()
{
    if (isDead || player == null) return;

    attackTimer -= Time.deltaTime;
    UpdatePlayerVisibility();

    if (!playerVisible)
    {
        MoveTowardsPlayer();
        return;
    }

    float dist = Vector3.Distance(transform.position, player.position);
    Vector3 dir = (player.position - transform.position).normalized;
    dir.y = 0;
    if (dir != Vector3.zero)
        transform.rotation = Quaternion.LookRotation(dir);

    // In range di attacco?
    if (!isAttacking && attackTimer <= 0f)
    {
        if (dist <= attackRange)
        {
            animator.SetTrigger("AttackMelee");
            isAttacking = true;
            attackTimer = attackCooldown;
            StopAndFacePlayer();
        }
        else if (dist <= rangedRange)
        {
            animator.SetTrigger("AttackRanged");
            isAttacking = true;
            attackTimer = attackCooldown;
            StopAndFacePlayer();
        }
        else
        {
            MoveTowardsPlayer();
        }
    }
    else
    {
        // È in cooldown → può comunque muoversi se fuori distanza
        if (dist > rangedRange)
            MoveTowardsPlayer();
        else
            StopAndFacePlayer();
    }
}

private void StopAndFacePlayer()
{
    if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        agent.isStopped = true;

    animator.SetBool("isWalking", false);
}

private void MoveTowardsPlayer()
{
    if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
    {
        agent.isStopped = false;
        agent.SetDestination(player.position);
    }

    animator.SetBool("isWalking", true);
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

    // ✳️ Evento nell'animazione melee
    public void EnemyAttackHitbox()
    {
        if (isDead) return;

        Collider[] hits = Physics.OverlapBox(
            transform.position + transform.forward * (attackRange * 0.5f),
            new Vector3(5f, 5f, 5f),
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

        isAttacking = false;
    }

    // ✳️ Evento nell'animazione ranged
    public void SpawnProjectile()
    {
        if (isDead || projectilePrefab == null || projectileSpawnPoint == null) return;

        GameObject proj = Instantiate(projectilePrefab, projectileSpawnPoint.position, Quaternion.identity);

        Projectile projectile = proj.GetComponent<Projectile>();
        if (projectile != null)
        {
            projectile.SetTarget(player.position);
        }

        isAttacking = false;
    }

    public void TakeDamage(float amount)
    {
        if (isDead) return;

        currentHealth -= amount;
        Debug.Log($"Golem ha subito {amount} danni. Vita rimanente: {currentHealth}");

        if (currentHealth <= 0)
        {
            Die();
        }
        else
        {
            animator.SetTrigger("Hit");
        }
    }

    private void Die()
    {
        isDead = true;
        Debug.Log("Golem è morto");

        if (animator != null)
            animator.SetTrigger("Die");

        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
            agent.isStopped = true;

        Collider col = GetComponent<Collider>();
        if (col != null)
            col.enabled = false;
    }
}
