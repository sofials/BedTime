using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class EnemyBase : MonoBehaviour
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
    public ParticleSystem deathParticles;

    [Header("Model")]
    public Renderer Renderer; // Assegna il renderer del modello in Inspector

    // Internal state
    protected int currentWaypoint = 0;
    protected float waitTimer;
    protected float rotateTimer;
    protected Transform player;
    protected bool playerVisible;
    protected bool isPatrolling = true;
    protected bool caughtPlayer = false;
    protected bool isAttacking = false;
    protected bool isDead = false;
    protected Animator animator;

    protected virtual void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();

        waitTimer = waitTimeAtPoint;
        rotateTimer = rotateTime;
        agent.speed = walkSpeed;
        currentHealth = maxHealth;

        if (waypoints != null && waypoints.Length > 0)
            agent.SetDestination(waypoints[currentWaypoint].position);
    }

    protected virtual void Update()
    {
        if (isDead) return;
        if (isDizzy) return;

        UpdatePlayerVisibility();

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

    protected void InterruptAttack()
    {
        if (isAttacking)
        {
            isAttacking = false;
            if (animator != null)
                animator.SetBool("isAttacking", false);
        }
    }

    public virtual void StartDizzy()
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

    public virtual void EndDizzy()
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

    protected void UpdatePlayerVisibility()
    {
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

    protected void ChasePlayer()
    {
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

    protected void Patrol()
    {
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

    protected void GoToNextWaypoint()
    {
        if (waypoints == null || waypoints.Length == 0) return;
        currentWaypoint = (currentWaypoint + 1) % waypoints.Length;
        agent.SetDestination(waypoints[currentWaypoint].position);
    }

    protected void ResetToPatrol()
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

    protected void FindClosestWaypoint()
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

    public virtual void EnemyAttackHitbox()
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
    }

    public virtual void TakeDamage(float amount)
    {
        if (isDead) return;

        currentHealth -= amount;
        StartDizzy();

        if (currentHealth <= 0)
        {
            currentHealth = 0;
            isDead = true;
            if (animator != null)
                animator.SetTrigger("Die");

            if (agent != null)
            {
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
            }
            // La distruzione avverrà tramite Animation Event o Coroutine
        }
    }

    // Metodo da chiamare tramite Animation Event alla fine dell'animazione di morte
    public virtual void DestroyAfterDeath()
    {
        StartCoroutine(DestroyAfterDelayCoroutine());
    }

    private IEnumerator DestroyAfterDelayCoroutine()
    {
        float deathEffectOffset = 1.2f;
        float waitTime = 2.5f - deathEffectOffset;

        if (waitTime > 0)
            yield return new WaitForSeconds(waitTime);

        if (deathParticles != null)
            deathParticles.Play();

        if (Renderer != null)
            Renderer.enabled = false;

        yield return new WaitForSeconds(deathEffectOffset);

        Destroy(gameObject);
    }
}