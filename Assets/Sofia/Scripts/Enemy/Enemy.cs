using UnityEngine;
using UnityEngine.AI;
using System.Collections;

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

    public float pushForce = 20f;
    public float damage = 25f;
    public float maxHealth = 100f;
    public float currentHealth;

    public bool isDizzy = false;
    public float dizzyDuration = 2.5f;

    [Header("VFX")]
    public ParticleSystem stunParticles;
    public ParticleSystem deathParticles; // <-- aggiungi questo

    private int currentWaypoint = 0;
    private float waitTimer;
    private float rotateTimer;

    private Transform player;
    private bool playerVisible;
    private bool isPatrolling = true;
    private bool caughtPlayer = false;
    private bool isAttacking = false;
    private bool isDead = false; // aggiungi questa variabile

    private Animator animator;

    void Start()
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

    void Update()
    {
        if (isDead)
            return; // Blocca tutta la logica se il nemico è morto

        if (isDizzy)
            return;

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

    private void InterruptAttack()
    {
        if (isAttacking)
        {
            Debug.Log("[Enemy] Interrompo attacco.");
            isAttacking = false;
            if (animator != null)
                animator.SetBool("isAttacking", false);
        }
    }

    public void StartDizzy()
    {
        if (isDizzy)
        {
            Debug.Log("[Enemy] StartDizzy chiamato ma già in Dizzy");
            return;
        }

        Debug.Log("[Enemy] Nemico entra in stato Dizzy");
        isDizzy = true;

        InterruptAttack();

        if (animator != null)
            animator.SetTrigger("Dizzy");

        if (agent != null)
            agent.isStopped = true;

        // ATTIVA PARTICLE SYSTEM
        if (stunParticles != null)
            stunParticles.Play();

        StartCoroutine(DizzyTimer());
    }

    [SerializeField] private float stunEffectEndOffset = 0.3f; // tempo prima della fine del dizzy per fermare l'effetto

    private IEnumerator DizzyTimer()
    {
        // Ferma la stun VFX poco prima della fine del dizzy
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
        Debug.Log("Nemico esce dallo stato Dizzy");
        isDizzy = false;

        // FERMA E PULISCI PARTICLE SYSTEM
        if (stunParticles != null)
            stunParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        if (agent != null)
            agent.isStopped = false;

        if (player != null)
        {
            float distanceToPlayer = Vector3.Distance(transform.position, player.position);

            if (distanceToPlayer <= attackRange)
            {
                Debug.Log("Player ancora nel range d'attacco dopo dizzy: riprendo attacco.");
                isAttacking = true;
                if (animator != null)
                    animator.SetBool("isAttacking", true);
                agent.isStopped = true;
                return;
            }

            if (distanceToPlayer <= viewRadius)
            {
                Debug.Log("Riprendo inseguimento dopo dizzy");
                isPatrolling = false;
                return;
            }
        }

        ResetToPatrol();
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
            InterruptAttack();
        }

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
        if (isDead)
        {
            Debug.Log("[Enemy] Colpito ma già morto, ignoro il danno.");
            return;
        }

        Debug.Log($"[Enemy] TakeDamage chiamato. Danno ricevuto: {amount}");

        currentHealth -= amount;
        Debug.Log($"[Enemy] Vita aggiornata: {currentHealth}");

        StartDizzy();

        if (currentHealth <= 0)
        {
            currentHealth = 0;
            isDead = true;
            Debug.Log("[Enemy] Nemico morto, setto trigger Die");
            if (animator != null)
                animator.SetTrigger("Die"); // Attiva animazione morte

            // FERMA IL NAVMESHAGENT
            if (agent != null)
            {
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
            }
            // La distruzione avverrà tramite Animation Event o Coroutine
        }
    }

    // Metodo da chiamare tramite Animation Event alla fine dell'animazione di morte
    public void DestroyAfterDeath()
    {
        Debug.Log("[Enemy] DestroyAfterDeath chiamato: attendo 5 secondi prima di distruggere GameObject.");
        StartCoroutine(DestroyAfterDelayCoroutine());
    }

    public Renderer mushroomRenderer; // Assegna il renderer del modello in Inspector

    private IEnumerator DestroyAfterDelayCoroutine()
    {
        float deathEffectOffset = 1.2f; // tempo in secondi in cui l'esplosione copre il funghetto
        float waitTime = 2.5f - deathEffectOffset; // tempo dopo animazione morte prima dell'esplosione

        if (waitTime > 0)
            yield return new WaitForSeconds(waitTime);

        if (deathParticles != null)
            deathParticles.Play();

        if (mushroomRenderer != null)
            mushroomRenderer.enabled = false;

        yield return new WaitForSeconds(deathEffectOffset);

        Destroy(gameObject);
    }
}
