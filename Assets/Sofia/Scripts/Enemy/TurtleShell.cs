using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class TurtleShell : MonoBehaviour
{
    /*────────────────────  Movement & Patrol  ────────────────────*/
    [Header("Movement & Patrol")]
    public NavMeshAgent agent;
    public float waitTimeAtPoint = 3f;
    public float rotateTime      = 2f;
    public float walkSpeed       = 4f;
    public float runSpeed        = 6f;
    public Transform[] waypoints;

    /*─────────────────────  Vision & Attack  ─────────────────────*/
    [Header("Vision & Attack")]
    public float viewRadius  = 10f;
    public float viewAngle   = 90f;
    public float attackRange = 2f;
    public LayerMask playerMask;
    public LayerMask obstacleMask;
    public float pushForce   = 60f;   // spinta quando LUI attacca
    public float reflectDamage = 5f;  // danno al player quando LO colpisce

    /*───────────────────────  Internal  ──────────────────────────*/
    private int    currentWaypoint     = 0;
    private float  waitTimer;
    private float  rotateTimer;
    private Transform player;
    private bool   playerVisible       = false;
    private bool   isPatrolling        = true;
    private bool   isAttacking         = false;

    private Animator animator;

    /*──────────────────────────  Start  ──────────────────────────*/
    private void Start()
    {
        agent     = GetComponent<NavMeshAgent>();
        animator  = GetComponent<Animator>();

        waitTimer   = waitTimeAtPoint;
        rotateTimer = rotateTime;
        agent.speed = walkSpeed;

        if (waypoints != null && waypoints.Length > 0)
            agent.SetDestination(waypoints[currentWaypoint].position);
    }

    /*────────────────────────── Update ───────────────────────────*/
    private void Update()
    {
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

    /*────────────────── Vision / Targeting helpers ───────────────*/
    private void UpdatePlayerVisibility()
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
                    player        = hit.transform;
                    playerVisible = true;
                    return;
                }
            }
        }

        player = null;
    }

    /*────────────────────── Behaviour  ───────────────────────────*/
    private void ChasePlayer()
    {
        if (player == null) return;

        float distance = Vector3.Distance(transform.position, player.position);

        RotateTowards(player.position); // ← rotazione verso il player

        if (distance <= attackRange)
        {
            if (!isAttacking)
            {
                isAttacking = true;
                animator.SetBool("isAttacking", true);
                if (agent.isActiveAndEnabled && agent.isOnNavMesh)
                    agent.isStopped = true;
            }
            return; // fermo ad attaccare
        }

        isAttacking = false;
        animator.SetBool("isAttacking", false);

        if (agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.speed     = runSpeed;
            agent.SetDestination(player.position);
        }
    }

    private void Patrol()
    {
        if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh) return;

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
                waitTimer      -= Time.deltaTime;
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
        isPatrolling  = true;
        isAttacking   = false;
        waitTimer     = waitTimeAtPoint;
        rotateTimer   = rotateTime;

        animator.SetBool("isAttacking", false);

        if (agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.speed     = walkSpeed;
        }

        FindClosestWaypoint();
    }

    private void FindClosestWaypoint()
    {
        if (waypoints == null || waypoints.Length == 0) return;

        float minDist = Mathf.Infinity;
        int   closest = 0;

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

    /*────────────────────── Combat Logic ─────────────────────────*/
    /// <summary>
    /// Chiamato via Animation Event nell’attacco: spinge di 60, nessun danno.
    /// </summary>
    public void EnemyAttackHitbox()
    {
        if (!isAttacking) return;

        Collider[] hits = Physics.OverlapBox(
            transform.position + transform.forward * (attackRange * 0.5f),
            new Vector3(1f, 1f, 1f),
            transform.rotation,
            LayerMask.GetMask("PlayerHurtbox")
        );

        foreach (var hit in hits)
        {
            if (!hit.CompareTag("PlayerHurtbox")) continue;

            var hurtbox = hit.GetComponent<HurtBox>();
            if (hurtbox == null) continue;

            Vector3 pushDir = (hurtbox.transform.position - transform.position).normalized;
            hurtbox.OnHit(pushDir, pushForce, 5f);  // solo knockback
        }
    }

    /// <summary>
    /// Invulnerabile: gioca solo l’animazione di difesa.
    /// (Il danno di riflesso viene gestito da HurtBox_TurtleShell)
    /// </summary>
    public void TakeDamage(float _) => animator?.SetTrigger("GetHit");

    /*────────────────────── Utility ──────────────────────────────*/
    private void RotateTowards(Vector3 targetPosition)
    {
        Vector3 direction = (targetPosition - transform.position).normalized;
        direction.y = 0; // no rotazione verticale
        if (direction == Vector3.zero) return;

        Quaternion lookRotation = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * 5f);
    }
}
