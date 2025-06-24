using UnityEngine;
using UnityEngine.AI;

public class Enemy : MonoBehaviour
{
    [Header("Stats")]
    public float health = 100f;
    public float moveSpeed = 3.5f;
    public float chaseSpeed = 5f;
    public float attackRange = 2f;
    public float sightRange = 10f;
    public float attackCooldown = 2f;
    public float dizzyDuration = 1.5f;
    public float pushForce = 3f;

    [Header("References")]
    public Transform player;
    public NavMeshAgent agent;
    public Animator animator;

    [Header("Debug / Visualization")]
    public bool showGizmos = true;

    bool isDead = false;
    bool isDizzy = false;
    float lastAttackTime = -999f;

    void Start()
    {
        if (agent == null)
        {
            agent = GetComponent<NavMeshAgent>();
            Debug.Log("NavMeshAgent assegnato automaticamente.");
        }
        if (animator == null)
        {
            animator = GetComponent<Animator>();
            Debug.Log("Animator assegnato automaticamente.");
        }
        agent.speed = moveSpeed;

        if (player == null)
            Debug.LogError("Player non assegnato! Assegna il Transform del player nell'Inspector.");
        else
            Debug.Log("Player assegnato correttamente: " + player.name);
    }

    void Update()
    {
        if (isDead)
        {
            Debug.Log("Nemico morto, skip Update.");
            return;
        }

        if (player == null)
        {
            Debug.LogWarning("Player è null in Update, skip.");
            return;
        }

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
        Debug.Log($"Distanza dal player: {distanceToPlayer}");

        if (isDizzy)
        {
            Debug.Log("Nemico è stordito, non agisce.");
            return;
        }

        if (distanceToPlayer <= attackRange)
        {
            Debug.Log("Nemico in range attacco.");
            ChasePlayer();    // Continua a muoversi verso il player
            Attack();         // Attacca se possibile
            RotateTowards(player.position);
        }
        else if (distanceToPlayer <= sightRange)
        {
            Debug.Log("Nemico in range inseguimento.");
            ChasePlayer();
            RotateTowards(player.position);
        }
        else
        {
            Debug.Log("Nemico pattuglia.");
            Patrol();
        }

        UpdateAnimations(distanceToPlayer);
    }

    void Patrol()
    {
        Debug.Log("Patrol: controllando percorso...");

        if (!agent.hasPath || agent.remainingDistance < 0.5f)
        {
            Vector3 randomDirection = Random.insideUnitSphere * sightRange;
            randomDirection += transform.position;

            Debug.Log("Patrol: calcolata direzione casuale: " + randomDirection);

            if (NavMesh.SamplePosition(randomDirection, out NavMeshHit hit, sightRange, NavMesh.AllAreas))
            {
                Debug.Log("Patrol: impostata nuova destinazione: " + hit.position);
                agent.SetDestination(hit.position);
            }
            else
            {
                Debug.LogWarning("Patrol: NavMesh.SamplePosition fallito.");
            }
        }
        else
        {
            Debug.Log("Patrol: agente ha già un percorso.");
        }
        agent.speed = moveSpeed;
    }

    void ChasePlayer()
    {
        agent.speed = chaseSpeed;
        agent.SetDestination(player.position);
        Debug.Log("ChasePlayer: inseguo il player alla posizione " + player.position);
    }

    void Attack()
    {
        if (Time.time - lastAttackTime >= attackCooldown)
        {
            lastAttackTime = Time.time;
            Debug.Log("Attack: trigger animazione Attack.");
            animator.SetTrigger("Attack");
        }
        else
        {
            Debug.Log("Attack: in cooldown, non attacco.");
        }
    }

    void UpdateAnimations(float distanceToPlayer)
    {
        if (isDead)
        {
            Debug.Log("UpdateAnimations: trigger Die.");
            animator.SetTrigger("Die");
            return;
        }

        if (isDizzy)
        {
            Debug.Log("UpdateAnimations: trigger Dizzy.");
            animator.SetTrigger("Dizzy");
            return;
        }

        bool walking = distanceToPlayer > attackRange && distanceToPlayer <= sightRange;
        Debug.Log("UpdateAnimations: walking = " + walking);
        animator.SetBool("Walk", walking);
        // NON settiamo più "Idle" qui, perché è lo stato di default
    }

    public void TakeDamage(float damage, Vector3 hitDirection)
    {
        if (isDead)
        {
            Debug.Log("TakeDamage: nemico già morto, ignoro danno.");
            return;
        }

        health -= damage;
        Debug.Log($"TakeDamage: danno ricevuto {damage}, salute rimanente {health}");

        if (health <= 0)
        {
            Debug.Log("TakeDamage: salute <= 0, muoio.");
            Die();
        }
        else
        {
            Debug.Log("TakeDamage: nemico stordito.");
            Dizzy(hitDirection);
        }
    }

    void Dizzy(Vector3 hitDirection)
    {
        if (isDizzy)
        {
            Debug.Log("Dizzy: già stordito, ignoro.");
            return;
        }

        isDizzy = true;
        Debug.Log("Dizzy: attivo stato stordito.");
        animator.SetTrigger("Dizzy");
        agent.ResetPath();

        Invoke(nameof(EndDizzy), dizzyDuration);
    }

    void EndDizzy()
    {
        isDizzy = false;
        Debug.Log("EndDizzy: fine stato stordito.");
    }

    void Die()
    {
        isDead = true;
        agent.isStopped = true;
        Debug.Log("Die: nemico morto, animazione Die triggerata, distruzione in 5s.");
        animator.SetTrigger("Die");
        Destroy(gameObject, 5f);
    }

    // Animation Event chiamato nel momento preciso dell'animazione Attack
    public void OnAttackHit()
    {
        Debug.Log("OnAttackHit: controllo hitbox attacco.");

        Collider[] hits = Physics.OverlapSphere(transform.position + transform.forward * attackRange * 0.5f, 1f, LayerMask.GetMask("PlayerHurtbox"));
        Debug.Log("OnAttackHit: colpiti " + hits.Length + " colliders.");

        foreach (var hit in hits)
        {
            Hurtbox hurtbox = hit.GetComponent<Hurtbox>();
            if (hurtbox != null)
            {
                Vector3 pushDir = (hurtbox.transform.position - transform.position).normalized;
                Debug.Log("OnAttackHit: colpito hurtbox, applico spinta.");
                hurtbox.OnHit(pushDir, pushForce);
            }
            else
            {
                Debug.LogWarning("OnAttackHit: collider senza Hurtbox.");
            }
        }
    }

    // Rotazione dolce verso il player (asse Y solo)
    void RotateTowards(Vector3 targetPos)
    {
        Vector3 direction = (targetPos - transform.position).normalized;
        direction.y = 0;
        if (direction == Vector3.zero) return;
        Quaternion lookRotation = Quaternion.LookRotation(direction);
        float rotationSpeed = 5f;
        transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, rotationSpeed * Time.deltaTime);
    }

    private void OnDrawGizmosSelected()
    {
        if (!showGizmos) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, sightRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
