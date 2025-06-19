using UnityEngine;
using UnityEngine.AI;

public abstract class EnemyAI : MonoBehaviour
{
    public NavMeshAgent agent;
    public Transform player;

    public LayerMask whatIsGround, whatIsPlayer;

    [Header("Stats")]
    public float moveSpeed = 3.5f;
    public float maxHealth = 100f;
    public float timeBetweenAttacks = 2f;
    public float dizzyDuration = 2f;

    [Header("Patrolling")]
    public Vector3 walkPoint;
    protected bool walkPointSet;
    public float walkPointRange;

    protected float currentHealth;
    protected bool alreadyAttacked;
    public float sightRange, attackRange;
    public bool playerInSightRange, playerInAttackRange;

    protected Animator animator;

    private string currentAnimTrigger = "";
    protected bool isDizzy = false;
    protected bool isDead = false;

    [Tooltip("Durata stimata dell'animazione di morte in secondi")]
    public float dieAnimationDuration = 0f;  // 25 frame a 30fps

    [Tooltip("Secondi da aspettare dopo l'animazione di morte prima di distruggere l'oggetto")]
    public float delayBeforeDestroy = 5f;

    protected virtual void Awake()
    {
        player = GameObject.Find("Player").transform;
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();

        currentHealth = maxHealth;

        if(agent != null)
            agent.speed = moveSpeed;

        currentAnimTrigger = "Idle";
        SetAnimation("Idle");
    }

    protected virtual void Update()
    {
        if (isDead)
        {
            // Disabilita ogni azione se è morto
            if (agent != null && !agent.isStopped)
                agent.isStopped = true;
            return;
        }

        if (isDizzy)
        {
            if (agent != null) agent.isStopped = true;
            return;
        }
        else
        {
            if (agent != null && agent.isStopped)
                agent.isStopped = false;
        }

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

        if (!agent.hasPath && !alreadyAttacked && currentAnimTrigger != "Idle")
        {
            SetAnimation("Idle");
        }
    }

    protected virtual void Patroling()
    {
        if (!walkPointSet) SearchWalkPoint();

        if (walkPointSet)
            agent.SetDestination(walkPoint);

        Vector3 distanceToWalkPoint = transform.position - walkPoint;

        if (distanceToWalkPoint.magnitude < 1f)
            walkPointSet = false;
    }

    protected virtual void SearchWalkPoint()
    {
        float randomZ = Random.Range(-walkPointRange, walkPointRange);
        float randomX = Random.Range(-walkPointRange, walkPointRange);

        walkPoint = new Vector3(transform.position.x + randomX, transform.position.y, transform.position.z + randomZ);

        if (Physics.Raycast(walkPoint, -transform.up, 2f, whatIsGround))
            walkPointSet = true;
    }

    protected virtual void ChasePlayer()
    {
        if(agent != null)
            agent.SetDestination(player.position);
    }

    protected abstract void AttackPlayer();

    protected void ResetAttack()
    {
        alreadyAttacked = false;
    }

    public virtual void TakeDamage(int damage)
    {
        if (isDead) return;

        currentHealth -= damage;
        Debug.Log($"Enemy Health: {currentHealth}");

        if (currentHealth <= 0)
        {
            currentHealth = 0;
            PlayDieAnimation();
        }
        else
        {
            PlayDizzy();
        }
    }

    protected void PlayDizzy()
    {
        if (isDizzy) return;

        isDizzy = true;
        SetAnimation("Dizzy");

        if(agent != null)
            agent.isStopped = true;

        Invoke(nameof(StopDizzy), dizzyDuration);
    }

    protected void StopDizzy()
    {
        isDizzy = false;

        if (!isDead)
            SetAnimation("Idle");

        if(agent != null)
            agent.isStopped = false;
    }

    protected void PlayDieAnimation()
    {
       isDead = true;

       if(agent != null)
       {
         agent.isStopped = true;
         agent.ResetPath();
       }

       animator.ResetTrigger("Idle");
       animator.ResetTrigger("Walk");
       animator.ResetTrigger("Attack");
       animator.ResetTrigger("Dizzy");
       animator.ResetTrigger("Die");

       Debug.Log("Trigger Die impostato"); // ⬅ AGGIUNGI QUESTO
       animator.SetTrigger("Die");
       currentAnimTrigger = "Die";

       Invoke(nameof(DestroyEnemy), dieAnimationDuration + delayBeforeDestroy);
    }


    protected void DestroyEnemy()
    {
        Destroy(gameObject);
    }

    protected void SetAnimation(string triggerName)
    {
       if (currentAnimTrigger == triggerName || isDead) return;

       animator.ResetTrigger("Idle");
       animator.ResetTrigger("Walk");
       animator.ResetTrigger("Attack");
       animator.ResetTrigger("Dizzy");

       // NON resettare Die se è morto
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
