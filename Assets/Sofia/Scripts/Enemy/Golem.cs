using UnityEngine;
using UnityEngine.AI;

public class Golem : MonoBehaviour
{
    public Transform player;
    public NavMeshAgent agent;

    [Header("Vision Settings")]
    public float viewRadius = 300f;
    public float viewAngle = 360f;
    public LayerMask playerMask;
    public LayerMask obstacleMask;

    [Header("Attack Settings")]
    public float meleeRange = 20f;
    public float rangedRange = 280f;
    public float meleeCooldown = 2f;
    public float rangedCooldown = 3f;

    private float meleeTimer = 0f;
    private float rangedTimer = 0f;

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

    private bool isMeleeAttacking = false;
    private bool isRangedAttacking = false;

    private void Start()
    {
        currentHealth = maxHealth;
    }

    private void Update()
    {
        if (isDead || player == null) return;

        meleeTimer -= Time.deltaTime;
        rangedTimer -= Time.deltaTime;

        UpdatePlayerVisibility();

        float dist = Vector3.Distance(transform.position, player.position);

        // Ruota verso il player
        Vector3 dir = (player.position - transform.position).normalized;
        dir.y = 0;
        if (dir != Vector3.zero)
            transform.rotation = Quaternion.LookRotation(dir);

        if (!playerVisible)
        {
            MoveTowardsPlayer();
            return;
        }

        // Blocco durante attacchi
        if (isMeleeAttacking || isRangedAttacking)
            return;

        // Scegli attacco
        if (dist <= meleeRange && meleeTimer <= 0f)
        {
            DoMeleeAttack();
        }
        else if (dist <= rangedRange && rangedTimer <= 0f)
        {
            DoRangedAttack();
        }
        else
        {
            MoveTowardsPlayer();
        }
    }

    private void DoMeleeAttack()
    {
        StopAndFacePlayer();
        isMeleeAttacking = true;
        meleeTimer = meleeCooldown;
        animator.SetTrigger("AttackMelee");
    }

    private void DoRangedAttack()
    {
        StopAndFacePlayer();
        isRangedAttacking = true;
        rangedTimer = rangedCooldown;
        animator.SetTrigger("AttackRanged");
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

    // Evento animazione melee — applica danno area
    public void EnemyAttackHitbox()
    {
        if (isDead) return;

        Collider[] hits = Physics.OverlapBox(
            transform.position + transform.forward * (meleeRange * 0.5f),
            new Vector3(10f, 10f, 10f),
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

        isMeleeAttacking = false;
    }

    // Evento animazione ranged — istanzia il proiettile (particellare)
    public void SpawnProjectile()
    {
        if (isDead || projectilePrefab == null || projectileSpawnPoint == null) return;

        GameObject proj = Instantiate(projectilePrefab, projectileSpawnPoint.position, Quaternion.identity);
        Golem_Projectile projectile = proj.GetComponent<Golem_Projectile>();

        if (projectile != null)
            projectile.Initialize(player.position);

    }

    // Evento di fine animazione ranged
    public void EndRangedAttack()
    {
        Debug.Log("Ranged attack finished");
        isRangedAttacking = false;
    }

    public void TakeDamage(float amount)
{
    if (isDead) return;

    currentHealth -= amount;
    Debug.Log($"Golem ha subito {amount} danni. Vita rimanente: {currentHealth}");

    // Se stava attaccando, resetta flag per riprendere comportamento normale
    isMeleeAttacking = false;
    isRangedAttacking = false;

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

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, meleeRange);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, rangedRange);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, viewRadius);

        Vector3 viewAngleA = DirFromAngle(-viewAngle / 2);
        Vector3 viewAngleB = DirFromAngle(viewAngle / 2);
        Gizmos.color = Color.white;
        Gizmos.DrawLine(transform.position, transform.position + viewAngleA * viewRadius);
        Gizmos.DrawLine(transform.position, transform.position + viewAngleB * viewRadius);
    }

    private Vector3 DirFromAngle(float angleDegrees)
    {
        angleDegrees += transform.eulerAngles.y;
        return new Vector3(Mathf.Sin(angleDegrees * Mathf.Deg2Rad), 0, Mathf.Cos(angleDegrees * Mathf.Deg2Rad));
    }
    // Chiamalo alla fine dell'animazione Hit (tramite evento animazione)
public void EndHit()
{
    isMeleeAttacking = false;
    isRangedAttacking = false;
}

}
