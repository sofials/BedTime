using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class Mushroom : MonoBehaviour
{
    [Header("Movement & Patrol")]
    public NavMeshAgent agent;
    public float waitTimeAtPoint = 4f;
    public float rotateTime = 2f;
    public float walkSpeed = 6f;
    public float runSpeed = 9f;
    public Transform[] waypoints;

    [Header("Vision & Attack")]
    public float viewRadius = 15f;
    public float viewAngle = 90f;
    public float attackRange = 2f;
    public LayerMask playerMask;
    public LayerMask obstacleMask;
    public float pushForce = 20f;
    public float damage = 25f;

    [Header("Health")]
    public float maxHealth = 100f;
    public float currentHealth;

    [Header("Status")]
    public bool isDizzy = false;
    public float dizzyDuration = 2.5f;
    [SerializeField] private float stunEffectEndOffset = 0.3f;

    [Header("VFX")]
    public ParticleSystem stunParticles;

    [Header("Model")]
    public Renderer Renderer;

    [Header("Death Effect Controller")]
    public DeathEffectController deathEffectController;

    // Internal state
    private int currentWaypoint = 0;
    private float waitTimer;
    private float rotateTimer;
    private Transform player;
    private bool playerVisible;
    private bool isPatrolling = true;
    private bool caughtPlayer = false;
    private bool isAttacking = false;
    private bool isDead = false;
    private Animator animator;

    private void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();

        if (stunParticles != null)
            stunParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        waitTimer = waitTimeAtPoint;
        rotateTimer = rotateTime;
        agent.speed = walkSpeed;
        currentHealth = maxHealth;

        if (waypoints != null && waypoints.Length > 0)
            agent.SetDestination(waypoints[currentWaypoint].position);

        if (deathEffectController != null)
            deathEffectController.StopDeathEffect();
    }

    private void Update()
{
    UpdatePlayerVisibility();

    if (isDead)
    {
        // Blocca tutto se morto
        agent.isStopped = true;
        agent.ResetPath();
        return;
    }

    if (isDizzy) return;

    if (playerVisible && !caughtPlayer)
    {
        isPatrolling = false;
        ChasePlayer();
    }
    else
    {
        if (!isPatrolling)
            ResetToPatrol();

        Patrol();
    }
}


    private void InterruptAttack()
    {
        if (isAttacking)
        {
            isAttacking = false;
            if (animator != null)
                animator.SetBool("isAttacking", false);
        }
    }

    public void StartDizzy()
    {
        if (isDizzy) return;

        isDizzy = true;
        InterruptAttack();

        if (animator != null)
            animator.SetTrigger("Dizzy");

        if (agent != null)
            agent.isStopped = true;

        if (stunParticles != null)
            stunParticles.Play();

        StartCoroutine(DizzyTimer());
    }

    private IEnumerator DizzyTimer()
    {
        if (stunParticles != null)
        {
            yield return new WaitForSeconds(dizzyDuration - stunEffectEndOffset);
            stunParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            yield return new WaitForSeconds(stunEffectEndOffset);
        }
        else
        {
            yield return new WaitForSeconds(dizzyDuration);
        }

        EndDizzy();
    }

    public void EndDizzy()
    {
        isDizzy = false;

        if (stunParticles != null)
            stunParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        if (agent != null)
            agent.isStopped = false;

        if (player != null)
        {
            float distanceToPlayer = Vector3.Distance(transform.position, player.position);

            if (distanceToPlayer <= attackRange)
            {
                isAttacking = true;
                if (animator != null)
                    animator.SetBool("isAttacking", true);
                agent.isStopped = true;
                return;
            }

            if (distanceToPlayer <= viewRadius)
            {
                isPatrolling = false;
                return;
            }
        }

        ResetToPatrol();
    }

    private void UpdatePlayerVisibility()
{
    // Se è morto, resetta tutto e ritorna
    if (isDead)
    {
        playerVisible = false;
        player = null;
        return;
    }

    playerVisible = false;
    Collider[] hits = Physics.OverlapSphere(transform.position, viewRadius, playerMask);

    foreach (var hit in hits)
    {
        Vector3 dir = (hit.transform.position - transform.position).normalized;
        if (Vector3.Angle(transform.forward, dir) < viewAngle / 2)
        {
            float dist = Vector3.Distance(transform.position, hit.transform.position);
            if (!Physics.Raycast(transform.position, dir, dist, obstacleMask))
            {
                playerVisible = true;
                player = hit.transform;
                return;
            }
        }
    }

    if (player != null && Vector3.Distance(transform.position, player.position) <= attackRange)
        return;

    player = null;
}

    private void ChasePlayer()
    {
        if (isDead) return;  // Blocca inseguimento se morto
        if (player == null) return;

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        if (distanceToPlayer <= attackRange)
        {
            if (!isAttacking)
            {
                isAttacking = true;
                if (animator != null)
                    animator.SetBool("isAttacking", true);
            }

            agent.isStopped = true;
            return;
        }
        else
        {
            InterruptAttack();
        }

        agent.isStopped = false;
        agent.speed = runSpeed;
        agent.SetDestination(player.position);

        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            if (waitTimer <= 0f && distanceToPlayer >= 6f)
            {
                ResetToPatrol();
            }
            else
            {
                agent.isStopped = true;
                waitTimer -= Time.deltaTime;
            }
        }
        else
        {
            waitTimer = waitTimeAtPoint;
        }
    }

    private void Patrol()
    {
        if (isDead) return; // Blocca movimento se morto

        agent.speed = walkSpeed;

        if (!agent.hasPath || agent.remainingDistance < agent.stoppingDistance + 0.1f)
        {
            if (waitTimer <= 0f)
            {
                GoToNextWaypoint();
                waitTimer = waitTimeAtPoint;
            }
            else
            {
                agent.isStopped = true;
                waitTimer -= Time.deltaTime;
            }
        }
        else
        {
            agent.isStopped = false;
        }
    }

    private void GoToNextWaypoint()
    {
        if (waypoints == null || waypoints.Length == 0) return;
        currentWaypoint = (currentWaypoint + 1) % waypoints.Length;
        agent.SetDestination(waypoints[currentWaypoint].position);
    }

    private void ResetToPatrol()
    {
        isPatrolling = true;
        agent.isStopped = false;
        waitTimer = waitTimeAtPoint;
        rotateTimer = rotateTime;
        caughtPlayer = false;
        isAttacking = false;

        if (animator != null)
            animator.SetBool("isAttacking", false);

        FindClosestWaypoint();
        agent.speed = walkSpeed;
    }

    private void FindClosestWaypoint()
    {
        if (waypoints == null || waypoints.Length == 0) return;

        float minDist = Mathf.Infinity;
        int closest = 0;

        for (int i = 0; i < waypoints.Length; i++)
        {
            float dist = Vector3.Distance(transform.position, waypoints[i].position);
            if (dist < minDist)
            {
                minDist = dist;
                closest = i;
            }
        }

        currentWaypoint = closest;
        agent.SetDestination(waypoints[currentWaypoint].position);
    }

    public void EnemyAttackHitbox()
    {
        if (isDead) return;  // Blocca la spinta se nemico è morto

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
    }

    public void TakeDamage(float amount)
{
    if (isDead) return;

    currentHealth -= amount;
    InterruptAttack();
    StartDizzy();

    if (currentHealth <= 0f)
    {
        currentHealth = 0f;
        isDead = true;

        // Reset variabili
        playerVisible = false;
        player = null;
        isPatrolling = false;
        caughtPlayer = false;
        isAttacking = false;

        if (animator != null)
            animator.SetTrigger("Die");

        if (agent != null)
        {
            agent.isStopped = true;
            agent.ResetPath();
            agent.enabled = false; // DISABILITO L'AGENTE
        }
    }
}

    // Metodo chiamato tramite Animation Event alla fine animazione morte
    public void DestroyAfterDeath()
    {
        if (!isDead) return;
        StartCoroutine(DestroyAfterDeathSequence());
    }

    private IEnumerator DestroyAfterDeathSequence()
    {
        yield return new WaitForSeconds(2f); // attesa animazione morte

        if (deathEffectController != null)
            deathEffectController.PlayDeathEffect(); // parte esplosione

        if (Renderer != null)
            Renderer.enabled = false; // disabilita mesh subito all'esplosione

        // Aspetta che l'effetto particellare termini
        if (deathEffectController != null)
        {
            ParticleSystem ps = deathEffectController.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                yield return new WaitUntil(() => !ps.isPlaying);
            }
        }
        else
        {
            yield return new WaitForSeconds(1.5f); // fallback
        }

        Destroy(gameObject); // distruggi tutto
    }
}
