using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class Slime : MonoBehaviour
{
    [Header("Movement & Patrol")]
    public NavMeshAgent agent;
    public float waitTimeAtPoint = 4f;
    public float rotateTime = 2f;
    public float walkSpeed = 6f;
    public float runSpeed = 9f;
    public Transform[] waypoints;
    
    [Header("Custom Audio")]
    public AudioSource customAudioSource;
    public AudioClip customAudioClip;

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
    public CFXR_EffectController deathEffectController;

    [Header("Chase Audio & VFX")]
    public AudioSource chaseAudioSource;
    public AudioClip chaseAudioClip;
    public GameObject enemyChildObjectToActivate; // Oggetto figlio del nemico da attivare

    [Header("Death Audio")]
    public AudioSource deathAudioSource;
    public AudioClip deathAudioClip;

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
    private bool hasEverSeenPlayer = false; // Flag per la prima volta che vede il player (mai resettato)
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
            deathEffectController.StopEffect();

        // Trova automaticamente l'oggetto figlio del nemico se non assegnato
        if (enemyChildObjectToActivate == null)
        {
            // Cerca un oggetto figlio chiamato "ChaseIndicator" o simile
            Transform chaseIndicator = transform.Find("ChaseIndicator");
            if (chaseIndicator != null)
                enemyChildObjectToActivate = chaseIndicator.gameObject;
        }

        // Assicurati che l'oggetto sia disattivato all'inizio
        if (enemyChildObjectToActivate != null)
            enemyChildObjectToActivate.SetActive(false);

        // Se non è assegnato un AudioSource per la morte, usa quello del chase come fallback
        if (deathAudioSource == null && chaseAudioSource != null)
            deathAudioSource = chaseAudioSource;
    }

    private void Update()
    {
        UpdatePlayerVisibility();

        if (isDead)
        {
            StopAgentSafely();
            return;
        }

        if (isDizzy) return;

        if (playerVisible && !caughtPlayer)
        {
            if (isPatrolling)
            {
                // Primo momento in cui inizia il chase
                isPatrolling = false;
                
                // Solo se non ha mai visto il player prima
                if (!hasEverSeenPlayer)
                {
                    hasEverSeenPlayer = true;
                    StartChaseSequence();
                }
            }
            ChasePlayer();
        }
        else
        {
            if (!isPatrolling)
                ResetToPatrol();

            Patrol();
        }
    }

    private void StartChaseSequence()
    {
        // Attiva l'oggetto figlio del nemico
        if (enemyChildObjectToActivate != null)
            enemyChildObjectToActivate.SetActive(true);

        // Riproduci l'audio
        if (chaseAudioSource != null && chaseAudioClip != null)
        {
            chaseAudioSource.PlayOneShot(chaseAudioClip);
            
            // Avvia la coroutine per spegnere l'oggetto quando finisce l'audio
            StartCoroutine(DisableEnemyObjectAfterAudio());
        }
        else
        {
            // Se non c'è audio, disattiva dopo un tempo fisso (es. 2 secondi)
            StartCoroutine(DisableEnemyObjectAfterDelay(2f));
        }
    }

    private IEnumerator DisableEnemyObjectAfterAudio()
    {
        if (chaseAudioClip != null)
        {
            // Aspetta per la durata dell'audio clip
            yield return new WaitForSeconds(chaseAudioClip.length);
        }
        else
        {
            // Fallback se non c'è clip
            yield return new WaitForSeconds(2f);
        }

        // Disattiva l'oggetto figlio del nemico
        if (enemyChildObjectToActivate != null)
            enemyChildObjectToActivate.SetActive(false);
    }

    private IEnumerator DisableEnemyObjectAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        
        if (enemyChildObjectToActivate != null)
            enemyChildObjectToActivate.SetActive(false);
    }

    private void StopAgentSafely()
    {
        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
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

        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
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

        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
            agent.isStopped = false;

        if (player != null)
        {
            float distanceToPlayer = Vector3.Distance(transform.position, player.position);

            if (distanceToPlayer <= attackRange)
            {
                isAttacking = true;
                if (animator != null)
                    animator.SetBool("isAttacking", true);
                if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
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
        if (isDead) return;
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

            if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
                agent.isStopped = true;
            return;
        }
        else
        {
            InterruptAttack();
        }

        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.speed = runSpeed;
            agent.SetDestination(player.position);
        }

        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            if (waitTimer <= 0f && distanceToPlayer >= 6f)
            {
                ResetToPatrol();
            }
            else
            {
                if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
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
        if (isDead) return;

        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
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
    }

    private void GoToNextWaypoint()
    {
        if (waypoints == null || waypoints.Length == 0) return;
        currentWaypoint = (currentWaypoint + 1) % waypoints.Length;
        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
            agent.SetDestination(waypoints[currentWaypoint].position);
    }

    private void ResetToPatrol()
    {
        isPatrolling = true;
        
        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
            agent.isStopped = false;

        waitTimer = waitTimeAtPoint;
        rotateTimer = rotateTime;
        caughtPlayer = false;
        isAttacking = false;

        if (animator != null)
            animator.SetBool("isAttacking", false);

        // Assicurati che l'oggetto del nemico sia disattivato quando torna al patrol
        if (enemyChildObjectToActivate != null)
            enemyChildObjectToActivate.SetActive(false);

        FindClosestWaypoint();

        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
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
        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
            agent.SetDestination(waypoints[currentWaypoint].position);
    }

    public void EnemyAttackHitbox()
    {
        if (isDead) return;

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

        animator?.SetTrigger("GetHit");

        if (currentHealth <= 0f)
        {
            currentHealth = 0f;
            isDead = true;

            playerVisible = false;
            player = null;
            isPatrolling = false;
            caughtPlayer = false;
            isAttacking = false;

            // Disattiva l'oggetto del nemico se è attivo
            if (enemyChildObjectToActivate != null)
                enemyChildObjectToActivate.SetActive(false);

            // Setta trigger Die DOPO GetHit
            animator?.SetTrigger("Die");

            if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.ResetPath();
                agent.enabled = false;
            }
            else if (agent != null)
            {
                agent.enabled = false;
            }

            // NON far partire dizzy quando muore
            return;
        }

        // Se ancora vivo, setta trigger Dizzy DOPO GetHit
        animator?.SetTrigger("Dizzy");

        StartDizzy();
    }

    // Metodo chiamato tramite Animation Event alla fine animazione morte
    public void DestroyAfterDeath()
    {
        if (!isDead) return;
        StartCoroutine(DestroyAfterDeathSequence());
    }

    private IEnumerator DestroyAfterDeathSequence()
    {
        yield return new WaitForSeconds(2f);

        // Riproduci il suono di morte
        if (deathAudioSource != null && deathAudioClip != null)
        {
            deathAudioSource.PlayOneShot(deathAudioClip);
        }

        // Avvia l'effetto visivo di morte
        if (deathEffectController != null)
            deathEffectController.PlayEffect();

        // Nasconde il renderer del modello
        if (Renderer != null)
            Renderer.enabled = false;

        // Aspetta che l'effetto particle finisca
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
            yield return new WaitForSeconds(1.5f);
        }

        // Spawna la gemma
        GemManager.Instance?.SpawnLifeGem(transform.position);

        // Distrugge il GameObject
        Destroy(gameObject);
    }

    // Metodo per riprodurre l'audio custom tramite Animation Event
    public void PlayCustomAudio()
    {
        if (isDead) return; // Opzionale: non riprodurre audio se il nemico è morto
        
        if (customAudioSource != null && customAudioClip != null)
        {
            customAudioSource.PlayOneShot(customAudioClip);
        }
        else
        {
            Debug.LogWarning("CustomAudioSource o CustomAudioClip non sono assegnati nell'Inspector");
        }
    }
}