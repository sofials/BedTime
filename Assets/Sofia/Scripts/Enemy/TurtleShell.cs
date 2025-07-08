using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class TurtleShell : MonoBehaviour
{
    [Header("Movement & Patrol")]
    public NavMeshAgent agent;
    public float waitTimeAtPoint = 3f;
    public float rotateTime = 2f;
    public float walkSpeed = 4f;
    public float runSpeed = 6f;
    public Transform[] waypoints;

    [Header("Vision & Attack")]
    public float viewRadius = 10f;
    public float viewAngle = 90f;
    public float attackRange = 2f;
    public LayerMask playerMask;
    public LayerMask obstacleMask;
    public float pushForce = 60f;

    [Header("Slowdown Settings")]
    public bool isSlow = false;
    public float slowFactor = 0.5f;

    [Header("Attack Settings")]
    public float attackDamage = 10f;
    public float attackCooldown = 1.5f;

    private int currentWaypoint = 0;
    private float waitTimer;
    private Transform player;
    private bool playerVisible = false;
    private bool isPatrolling = true;
    private bool isAttacking = false;
    private float attackTimer = 0f;
    private Animator animator;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
    }

    private void Start()
    {
        waitTimer = waitTimeAtPoint;
        if (waypoints != null && waypoints.Length > 0)
            agent.SetDestination(waypoints[currentWaypoint].position);
        agent.speed = walkSpeed;
    }

    private void Update()
    {
        attackTimer -= Time.deltaTime;
        UpdatePlayerVisibility();

        if (playerVisible)
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

    private void UpdatePlayerVisibility()
    {
        playerVisible = false;
        Collider[] hits = Physics.OverlapSphere(transform.position, viewRadius, playerMask);
        foreach (var hit in hits)
        {
            Vector3 dir = (hit.transform.position - transform.position).normalized;
            if (Vector3.Angle(transform.forward, dir) < viewAngle / 2f)
            {
                float dist = Vector3.Distance(transform.position, hit.transform.position);
                if (!Physics.Raycast(transform.position, dir, dist, obstacleMask))
                {
                    player = hit.transform;
                    playerVisible = true;
                    return;
                }
            }
        }
        player = null;
    }

    private void ChasePlayer()
    {
        if (player == null) return;

        RotateTowards(player.position);
        float dist = Vector3.Distance(transform.position, player.position);

        if (dist <= attackRange)
        {
            if (attackTimer <= 0f)
            {
                // Rimuovo qui l'infliggi danno diretto
                // Lo facciamo invece tramite Animation Event
                // InflictDamageToPlayer();
                attackTimer = attackCooldown;
            }

            if (!isSlow)
            {
                if (!isAttacking)
                {
                    isAttacking = true;
                    animator.SetBool("isAttacking", true);
                    agent.isStopped = true;
                }
            }
            else
            {
                // Slow: segue lentamente E infligge danno (ma senza animazione attacco)
                if (isAttacking)
                {
                    isAttacking = false;
                    animator.SetBool("isAttacking", false);
                }
                agent.isStopped = false;
                agent.speed = walkSpeed * slowFactor;
                agent.SetDestination(player.position);
                // Danno già inflitto via attackTimer sopra
            }
            return;
        }

        // Fuori raggio attacco
        if (isAttacking)
        {
            isAttacking = false;
            animator.SetBool("isAttacking", false);
        }

        agent.isStopped = false;
        agent.speed = isSlow ? walkSpeed * slowFactor : runSpeed;
        agent.SetDestination(player.position);
    }

    // Questo metodo è chiamato dall'Animation Event "EnemyAttackHitbox"
    public void EnemyAttackHitbox()
    {
        InflictDamageToPlayer();
    }

    private void InflictDamageToPlayer()
    {
        if (player == null) return;

        HurtBox playerHurtBox = player.GetComponentInChildren<HurtBox>();
        if (playerHurtBox != null)
        {
            Vector3 pushDir = (player.position - transform.position).normalized;
            playerHurtBox.OnHit(pushDir, pushForce, attackDamage);
        }
        else
        {
            Debug.LogWarning("Player hurtbox non trovata!");
        }
    }

    private void Patrol()
    {
        if (!agent.isOnNavMesh) return;

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
        isAttacking = false;
        waitTimer = waitTimeAtPoint;
        animator.SetBool("isAttacking", false);
        agent.isStopped = false;
        agent.speed = walkSpeed;
        FindClosestWaypoint();
    }

    private void FindClosestWaypoint()
    {
        if (waypoints == null || waypoints.Length == 0) return;
        float minDist = float.MaxValue;
        for (int i = 0; i < waypoints.Length; i++)
        {
            float dist = Vector3.Distance(transform.position, waypoints[i].position);
            if (dist < minDist)
            {
                minDist = dist;
                currentWaypoint = i;
            }
        }
        agent.SetDestination(waypoints[currentWaypoint].position);
    }

    public void SetSlow(bool slow)
    {
        isSlow = slow;
        animator.SetBool("isSlow", slow);
        if (isSlow)
            agent.speed = walkSpeed * slowFactor;
        else
            agent.speed = walkSpeed;
    }

    public void TakeDamage(float damage)
    {
        if (isSlow)
        {
            animator.SetTrigger("GetHitReal");
            StartCoroutine(DieAfterHit());
        }
        else
        {
            animator.SetTrigger("GetHit");
        }
    }

    private IEnumerator DieAfterHit()
    {
        yield return new WaitForSeconds(animator.GetCurrentAnimatorStateInfo(0).length + 0.1f);
        Destroy(gameObject);
    }

    private void RotateTowards(Vector3 target)
    {
        Vector3 dir = (target - transform.position).normalized;
        dir.y = 0;
        if (dir == Vector3.zero) return;
        Quaternion look = Quaternion.LookRotation(dir);
        transform.rotation = Quaternion.Slerp(transform.rotation, look, Time.deltaTime * 5f);
    }
}
