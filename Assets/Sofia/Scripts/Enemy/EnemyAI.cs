using UnityEngine;
using UnityEngine.AI;

public class EnemyAI : MonoBehaviour
{
    public NavMeshAgent agent;
    public Transform player;

    public LayerMask whatIsGround, whatIsPlayer;

    public float health = 100f;

    // Patroling
    public Vector3 walkPoint;
    bool walkPointSet;
    public float walkPointRange;

    // Attacking
    public float timeBetweenAttacks;
    bool alreadyAttacked;

    // States
    public float sightRange, attackRange;
    public bool playerInSightRange, playerInAttackRange;

    private Animator animator;

    private string currentAnimTrigger = "";

    private void Awake()
    {
        player = GameObject.Find("Player").transform;
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();

        currentAnimTrigger = "Idle"; // stato di default
        SetAnimation("Idle");
    }

    private void Update()
    {
        // Check for sight and attack range
        playerInSightRange = Physics.CheckSphere(transform.position, sightRange, whatIsPlayer);
        playerInAttackRange = Physics.CheckSphere(transform.position, attackRange, whatIsPlayer);

        if (!playerInSightRange && !playerInAttackRange)
        {
            SetAnimation("Walk");
            Patroling();
        }
        else if (playerInSightRange && !playerInAttackRange)
        {
            SetAnimation("Walk");
            ChasePlayer();
        }
        else if (playerInAttackRange && playerInSightRange)
        {
            SetAnimation("Attack");
            AttackPlayer();
        }

        // Se l'agente è fermo e non attacca, torna a idle
        if (!agent.hasPath && !alreadyAttacked && currentAnimTrigger != "Idle")
        {
            SetAnimation("Idle");
        }
    }

    private void Patroling()
    {
        if (!walkPointSet) SearchWalkPoint();

        if (walkPointSet)
            agent.SetDestination(walkPoint);

        Vector3 distanceToWalkPoint = transform.position - walkPoint;

        // Walkpoint reached
        if (distanceToWalkPoint.magnitude < 1f)
            walkPointSet = false;
    }

    private void SearchWalkPoint()
    {
        // Calculate random point in range
        float randomZ = Random.Range(-walkPointRange, walkPointRange);
        float randomX = Random.Range(-walkPointRange, walkPointRange);

        walkPoint = new Vector3(transform.position.x + randomX, transform.position.y, transform.position.z + randomZ);

        if (Physics.Raycast(walkPoint, -transform.up, 2f, whatIsGround))
            walkPointSet = true;
    }

    private void ChasePlayer()
    {
        agent.SetDestination(player.position);
    }

    private void AttackPlayer()
    {
        // Blocca movimento nemico durante attacco
        agent.SetDestination(transform.position);

        transform.LookAt(player);

        if (!alreadyAttacked)
        {
            // Logica danno o altro qui
            alreadyAttacked = true;

            // Ottieni riferimento al ThirdPersonController del player
            ThirdPersonController playerController = player.GetComponent<ThirdPersonController>();
            if (playerController != null)
            {
                // Calcola direzione dal nemico al player
                Vector3 knockbackDir = (player.position - transform.position).normalized;

                // Applica knockback (forza e durata a scelta)
                playerController.ApplyKnockback(knockbackDir, 5f, 0.2f);
            }

            Invoke(nameof(ResetAttack), timeBetweenAttacks);
        }

        Vector3 pushDir = (player.transform.position - transform.position).normalized;
       float pushForce = 8f; // Aumenta questo valore se vuoi più forza

       player.GetComponent<ThirdPersonController>().ApplyExternalPush(pushDir * pushForce);

    }

    private void ResetAttack()
    {
        alreadyAttacked = false;
    }

    public void TakeDamage(int damage)
    {
        health -= damage;
        Debug.Log($"Enemy Health: {health}");

        if (health <= 0) Invoke(nameof(DestroyEnemy), 0.5f);
    }

    private void DestroyEnemy()
    {
        Destroy(gameObject);
    }

    private void SetAnimation(string triggerName)
    {
        if (currentAnimTrigger == triggerName) return;

        // Reset tutti i trigger per sicurezza
        animator.ResetTrigger("Idle");
        animator.ResetTrigger("Walk");
        animator.ResetTrigger("Attack");

        animator.SetTrigger(triggerName);
        currentAnimTrigger = triggerName;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, sightRange);
    }
    
}
