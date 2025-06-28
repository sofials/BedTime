using UnityEngine;
using UnityEngine.AI;

public class Enemy : MonoBehaviour
{
    public NavMeshAgent agent;
    public float waitTimeAtPoint = 4f;
    public float rotateTime = 2f;
    public float walkSpeed = 6f;
    public float runSpeed = 9f;

    public float viewRadius = 15f;
    public float viewAngle = 90f;
    public float attackRange = 2f;
    public LayerMask playerMask;
    public LayerMask obstacleMask;
    public Transform[] waypoints;

    public float pushForce = 8f;
    public float damage = 25f; // opzionale

    private int currentWaypoint = 0;
    private float waitTimer;
    private float rotateTimer;

    private Transform player;
    private bool playerVisible;
    private bool isPatrolling = true;
    private bool caughtPlayer = false;

    private bool isAttacking = false;
    private Animator animator;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();

        waitTimer = waitTimeAtPoint;
        rotateTimer = rotateTime;
        agent.speed = walkSpeed;

        if (waypoints != null && waypoints.Length > 0)
            agent.SetDestination(waypoints[currentWaypoint].position);
    }

    void Update()
    {
        UpdatePlayerVisibility();

        if (playerVisible && !caughtPlayer)
        {
            isPatrolling = false;
            ChasePlayer();
        }
        else
        {
            if (!isPatrolling)
            {
                ResetToPatrol();
            }
            Patrol();
        }
    }

    void UpdatePlayerVisibility()
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
                    Debug.Log("Player avvistato: " + player.name);
                    return;
                }
            }
        }

        if (player != null && Vector3.Distance(transform.position, player.position) <= attackRange)
        {
            Debug.Log("Player fuori vista ma ancora nel range d'attacco, mantengo target.");
            return;
        }

        Debug.Log("Player perso, azzero target.");
        player = null;
    }

    void ChasePlayer()
    {
        if (player == null)
        {
            Debug.Log("ChasePlayer chiamato ma player è null.");
            return;
        }

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        // Gestione attacco
        if (distanceToPlayer <= attackRange)
        {
            if (!isAttacking)
            {
                Debug.Log("Inizio attacco");
                isAttacking = true;
                if (animator != null)
                    animator.SetBool("isAttacking", true);
            }

            agent.isStopped = true;
            return;
        }
        else
        {
            if (isAttacking)
            {
                Debug.Log("Esco dal range d'attacco, interrompo attacco");
                isAttacking = false;
                if (animator != null)
                    animator.SetBool("isAttacking", false);
            }
        }

        // Inseguimento normale
        agent.isStopped = false;
        agent.speed = runSpeed;
        agent.SetDestination(player.position);
        Debug.Log("Inseguimento: imposto destinazione su player");

        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            if (waitTimer <= 0f && distanceToPlayer >= 6f)
            {
                Debug.Log("Attesa completata e distanza > 6: torno in pattuglia");
                ResetToPatrol();
            }
            else
            {
                Debug.Log("Fermo e attendo: tempo rimanente " + waitTimer);
                agent.isStopped = true;
                waitTimer -= Time.deltaTime;
            }
        }
        else
        {
            waitTimer = waitTimeAtPoint;
        }
    }

    void Patrol()
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

    void GoToNextWaypoint()
    {
        currentWaypoint = (currentWaypoint + 1) % waypoints.Length;
        agent.SetDestination(waypoints[currentWaypoint].position);
    }

    void ResetToPatrol()
    {
        Debug.Log("Reset in pattuglia");
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

    void FindClosestWaypoint()
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

    // Da chiamare tramite Animation Event nell'attacco
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
        var hurtbox = hit.GetComponent<HurtBox>();
        if (hurtbox != null)
        {
            Vector3 pushDir = (hurtbox.transform.position - transform.position).normalized;
            hurtbox.OnHit(pushDir, pushForce, damage); // Passa anche il danno!
        }
    }
}
}
