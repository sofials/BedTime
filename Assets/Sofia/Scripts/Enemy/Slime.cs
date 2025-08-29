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
    public GameObject enemyChildObjectToActivate;
    
    [Header("Second Audio After Chase")]
    public AudioSource secondAudioSource;
    public AudioClip secondAudioClip;

    [Header("Death Audio")]
    public AudioSource deathAudioSource;
    public AudioClip deathAudioClip;

    [Header("Warning System")]
    public float warningResetTime = 15f; // Tempo prima che il warning possa essere riattivato

    [Header("Debug")]
    public bool enableDebugLogs = true;
    public bool showDistanceInConsole = true;

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
    
    // FIXED: Sistema warning corretto con timeout
    private bool isPlayingWarningSequence = false;
    private bool hasTriggeredWarningThisChase = false;
    private float lastPlayerLostTime = 0f; // Quando il player è uscito dal view radius
    private bool playerWasVisible = false; // Per tracciare quando il player esce
    private Coroutine currentWarningCoroutine = null;
    
    private Animator animator;

    // Debug variables
    private float lastPlayerDistance = -1f;
    private float debugUpdateTimer = 0f;
    private const float DEBUG_UPDATE_INTERVAL = 0.1f;

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
            
        // Inizializzazione sistema warning
        ResetWarningState();

        // DEBUG: Info iniziali
        if (enableDebugLogs)
        {
            Debug.Log($"<color=yellow>[Slime Debug]</color> {name} initialized - ViewRadius: {viewRadius}, AttackRange: {attackRange}, ViewAngle: {viewAngle}, WarningReset: {warningResetTime}s");
        }
    }

    private void Update()
    {
        UpdatePlayerVisibility();
        UpdateDebugInfo();

        if (isDead)
        {
            StopAgentSafely();
            return;
        }

        if (isDizzy) return;

        // FIXED: Logica chase con sistema warning corretto
        if (playerVisible && !caughtPlayer)
        {
            if (isPatrolling)
            {
                isPatrolling = false;
                DebugLog($"<color=orange>SWITCHING TO CHASE MODE</color> - Distance: {GetPlayerDistance():F2}");
                
                // FIXED: Check se può triggare warning basato su timeout
                if (CanTriggerWarning() && !isPlayingWarningSequence)
                {
                    StartWarningSequence();
                }
            }
            ChasePlayer();
        }
        else
        {
            if (!isPatrolling)
            {
                DebugLog($"<color=green>SWITCHING TO PATROL MODE</color> - Player lost at distance: {GetPlayerDistance():F2}");
                ResetToPatrol();
            }

            Patrol();
        }
    }

    private void UpdateDebugInfo()
    {
        if (!enableDebugLogs || !showDistanceInConsole) return;

        debugUpdateTimer += Time.deltaTime;
        
        if (debugUpdateTimer >= DEBUG_UPDATE_INTERVAL)
        {
            debugUpdateTimer = 0f;
            
            float currentDistance = GetPlayerDistance();
            if (currentDistance != lastPlayerDistance && currentDistance >= 0)
            {
                lastPlayerDistance = currentDistance;
                
                string status = "";
                if (isDead) status = "DEAD";
                else if (isDizzy) status = "DIZZY";
                else if (isAttacking) status = "ATTACKING";
                else if (!isPatrolling) status = "CHASING";
                else status = "PATROLLING";
                
                string visibilityStatus = playerVisible ? "VISIBLE" : "NOT VISIBLE";
                string warningStatus = hasTriggeredWarningThisChase ? "TRIGGERED" : "NOT TRIGGERED";
                string playingStatus = isPlayingWarningSequence ? "PLAYING" : "STOPPED";
                
                // FIXED: Mostra anche il tempo rimanente per il reset warning
                float timeSinceLost = lastPlayerLostTime > 0 ? Time.time - lastPlayerLostTime : 0f;
                string resetStatus = CanTriggerWarning() ? "READY" : $"COOLDOWN ({warningResetTime - timeSinceLost:F1}s)";
                
                Debug.Log($"<color=cyan>[Slime Debug]</color> {name} | Distance: {currentDistance:F2} | Status: {status} | Player: {visibilityStatus} | Warning: {warningStatus} | Audio: {playingStatus} | Reset: {resetStatus}");
            }
        }
    }

    private float GetPlayerDistance()
    {
        if (player == null) return -1f;
        return Vector3.Distance(transform.position, player.position);
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"<color=yellow>[Slime Debug]</color> {name}: {message}");
        }
    }

    // FIXED: Controlla se può triggerare warning basato su timeout
    private bool CanTriggerWarning()
    {
        // Se non ha mai triggerato warning, può farlo
        if (!hasTriggeredWarningThisChase) return true;
        
        // Se il player non è mai uscito dal view radius, non può retriggare
        if (lastPlayerLostTime <= 0f) return false;
        
        // Controlla se è passato abbastanza tempo dal momento in cui il player è uscito
        float timeSinceLost = Time.time - lastPlayerLostTime;
        bool canTrigger = timeSinceLost >= warningResetTime;
        
        if (canTrigger)
        {
            DebugLog($"<color=green>WARNING CAN BE TRIGGERED AGAIN!</color> Time since lost: {timeSinceLost:F1}s (required: {warningResetTime}s)");
        }
        
        return canTrigger;
    }

    private void StartWarningSequence()
    {
        if (isPlayingWarningSequence) return;
        
        hasTriggeredWarningThisChase = true;
        isPlayingWarningSequence = true;
        lastPlayerLostTime = 0f; // Reset timer perché ora il player è visibile

        float warningDistance = GetPlayerDistance();
        DebugLog($"<color=orange>WARNING SEQUENCE STARTED!</color> Distance: {warningDistance:F2}, ViewRadius: {viewRadius}");

        // Attiva l'oggetto figlio del nemico
        if (enemyChildObjectToActivate != null)
        {
            enemyChildObjectToActivate.SetActive(true);
            DebugLog("Chase indicator activated");
        }

        // Riproduci l'audio warning
        if (chaseAudioSource != null && chaseAudioClip != null)
        {
            chaseAudioSource.PlayOneShot(chaseAudioClip);
            DebugLog($"Playing chase audio clip (length: {chaseAudioClip.length:F2}s)");
            currentWarningCoroutine = StartCoroutine(WarningSequenceCoroutine());
        }
        else
        {
            DebugLog("No chase audio found, using default timing");
            currentWarningCoroutine = StartCoroutine(WarningSequenceCoroutine());
        }
    }

    private IEnumerator WarningSequenceCoroutine()
    {
        // Aspetta che finisca l'audio warning
        if (chaseAudioClip != null)
        {
            DebugLog($"Waiting for chase audio to finish ({chaseAudioClip.length:F2}s)...");
            yield return new WaitForSeconds(chaseAudioClip.length);
        }
        else
        {
            DebugLog("Waiting for default warning duration (2s)...");
            yield return new WaitForSeconds(2f);
        }

        // Riproduci il secondo audio
        PlaySecondAudio();

        // Disattiva l'oggetto figlio del nemico
        if (enemyChildObjectToActivate != null)
        {
            enemyChildObjectToActivate.SetActive(false);
            DebugLog("Chase indicator deactivated");
        }

        // Reset flag
        isPlayingWarningSequence = false;
        currentWarningCoroutine = null;
        
        DebugLog($"<color=green>WARNING SEQUENCE COMPLETED!</color> Distance: {GetPlayerDistance():F2}");
    }

    private void PlaySecondAudio()
    {
        if (secondAudioSource != null && secondAudioClip != null)
        {
            secondAudioSource.PlayOneShot(secondAudioClip);
            DebugLog($"Playing second audio clip (length: {secondAudioClip.length:F2}s)");
        }
        else if (secondAudioClip != null && chaseAudioSource != null)
        {
            chaseAudioSource.PlayOneShot(secondAudioClip);
            DebugLog($"Playing second audio on chase source (length: {secondAudioClip.length:F2}s)");
        }
        else
        {
            DebugLog("No second audio clip to play");
        }
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
            DebugLog("Attack interrupted");
            if (animator != null)
                animator.SetBool("isAttacking", false);
        }
    }

    public void StartDizzy()
    {
        if (isDizzy) return;

        isDizzy = true;
        DebugLog($"<color=purple>DIZZY STARTED</color> - Duration: {dizzyDuration}s");
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
        DebugLog("<color=purple>DIZZY ENDED</color>");

        if (stunParticles != null)
            stunParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
            agent.isStopped = false;

        if (player != null)
        {
            float distanceToPlayer = Vector3.Distance(transform.position, player.position);
            DebugLog($"After dizzy - Distance to player: {distanceToPlayer:F2}");

            if (distanceToPlayer <= attackRange)
            {
                isAttacking = true;
                DebugLog("Starting attack after dizzy");
                if (animator != null)
                    animator.SetBool("isAttacking", true);
                if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
                    agent.isStopped = true;
                return;
            }

            if (distanceToPlayer <= viewRadius)
            {
                isPatrolling = false;
                DebugLog("Continuing chase after dizzy");
                return;
            }
        }

        ResetToPatrol();
    }

    // FIXED: Gestione visibilità player con tracking quando esce
    private void UpdatePlayerVisibility()
    {
        if (isDead)
        {
            playerVisible = false;
            player = null;
            return;
        }

        bool wasPlayerVisible = playerVisible;
        playerVisible = false;
        Transform detectedPlayer = null;

        Collider[] hits = Physics.OverlapSphere(transform.position, viewRadius, playerMask);
        foreach (var hit in hits)
        {
            Vector3 dir = (hit.transform.position - transform.position).normalized;
            float angle = Vector3.Angle(transform.forward, dir);
            float distance = Vector3.Distance(transform.position, hit.transform.position);

            if (distance > viewRadius)
            {
                if (enableDebugLogs && wasPlayerVisible)
                {
                    DebugLog($"Player BEYOND view radius - Distance: {distance:F2}, ViewRadius: {viewRadius}");
                }
                continue;
            }

            if (angle < viewAngle / 2)
            {
                if (!Physics.Raycast(transform.position, dir, distance, obstacleMask))
                {
                    playerVisible = true;
                    detectedPlayer = hit.transform;
                    
                    if (!wasPlayerVisible)
                    {
                        DebugLog($"<color=lime>PLAYER DETECTED!</color> Distance: {distance:F2}, Angle: {angle:F1}°, ViewRadius: {viewRadius}");
                    }
                    break;
                }
                else
                {
                    if (enableDebugLogs && wasPlayerVisible)
                    {
                        DebugLog($"Player blocked by obstacle - Distance: {distance:F2}, Angle: {angle:F1}°");
                    }
                }
            }
            else
            {
                if (enableDebugLogs && distance <= viewRadius)
                {
                    DebugLog($"Player outside view angle - Distance: {distance:F2}, Angle: {angle:F1}°, MaxAngle: {viewAngle/2:F1}°");
                }
            }
        }

        // FIXED: Gestione corretta del timing quando player esce
        if (playerVisible)
        {
            player = detectedPlayer;
            playerWasVisible = true;
            // Se il player è tornato visibile, resetta il timer di uscita
            if (lastPlayerLostTime > 0f)
            {
                DebugLog($"Player returned to view, reset lost timer. Was lost for: {Time.time - lastPlayerLostTime:F2}s");
                lastPlayerLostTime = 0f;
            }
        }
        else
        {
            // FIXED: Marca il momento in cui il player esce SOLO la prima volta
            if (wasPlayerVisible && playerWasVisible)
            {
                float lastDistance = player != null ? Vector3.Distance(transform.position, player.position) : -1f;
                lastPlayerLostTime = Time.time;
                playerWasVisible = false;
                DebugLog($"<color=red>PLAYER LOST!</color> Last distance: {lastDistance:F2}, starting timer for warning reset ({warningResetTime}s)");
            }
            
            // Mantieni player reference per un breve periodo per evitare flickering
            if (player != null && Vector3.Distance(transform.position, player.position) > viewRadius + 2f)
            {
                DebugLog("Player reference cleared due to distance");
                player = null;
            }
        }

        // Debug visualization
        if (playerVisible && player != null)
            Debug.DrawLine(transform.position, player.position, Color.red);
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
                DebugLog($"<color=red>STARTING ATTACK!</color> Distance: {distanceToPlayer:F2}");
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
                DebugLog($"Chase timeout - Distance: {distanceToPlayer:F2}, switching to patrol");
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
        {
            agent.SetDestination(waypoints[currentWaypoint].position);
            DebugLog($"Moving to waypoint {currentWaypoint}");
        }
    }

    private void ResetToPatrol()
    {
        isPatrolling = true;
        DebugLog("<color=green>RESET TO PATROL</color>");

        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
            agent.isStopped = false;

        waitTimer = waitTimeAtPoint;
        rotateTimer = rotateTime;
        caughtPlayer = false;
        isAttacking = false;

        if (animator != null)
            animator.SetBool("isAttacking", false);

        // Cleanup warning sequence se in corso
        CleanupWarningSequence();

        FindClosestWaypoint();

        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
            agent.speed = walkSpeed;
    }

    private void CleanupWarningSequence()
    {
        if (enemyChildObjectToActivate != null && enemyChildObjectToActivate.activeSelf)
        {
            enemyChildObjectToActivate.SetActive(false);
            DebugLog("Chase indicator deactivated during cleanup");
        }
        
        if (currentWarningCoroutine != null)
        {
            StopCoroutine(currentWarningCoroutine);
            currentWarningCoroutine = null;
            DebugLog("Warning coroutine stopped during cleanup");
        }
        
        isPlayingWarningSequence = false;
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
        {
            agent.SetDestination(waypoints[currentWaypoint].position);
            DebugLog($"Found closest waypoint: {closest} at distance {Vector3.Distance(transform.position, waypoints[closest].position):F2}");
        }
    }

    public void EnemyAttackHitbox()
    {
        if (isDead) return;

        DebugLog("<color=red>ATTACK HITBOX TRIGGERED!</color>");

        Collider[] hits = Physics.OverlapBox(
            transform.position + transform.forward * (attackRange * 0.5f),
            new Vector3(1f, 1f, 1f),
            transform.rotation,
            LayerMask.GetMask("PlayerHurtbox")
        );

        DebugLog($"Attack hitbox detected {hits.Length} colliders");

        foreach (var hit in hits)
        {
            if (hit.gameObject.CompareTag("PlayerHurtbox"))
            {
                var hurtbox = hit.GetComponent<HurtBox>();
                if (hurtbox != null)
                {
                    Vector3 pushDir = (hurtbox.transform.position - transform.position).normalized;
                    hurtbox.OnHit(pushDir, pushForce, damage);
                    DebugLog($"<color=red>HIT PLAYER!</color> Damage: {damage}, Push: {pushForce}");
                }
            }
        }
    }

    public void TakeDamage(float amount)
    {
        if (isDead) return;

        currentHealth -= amount;
        DebugLog($"<color=yellow>TOOK DAMAGE!</color> Amount: {amount}, Health: {currentHealth}/{maxHealth}");
        InterruptAttack();

        animator?.SetTrigger("GetHit");

        if (currentHealth <= 0f)
        {
            currentHealth = 0f;
            isDead = true;
            DebugLog("<color=red>SLIME DIED!</color>");

            playerVisible = false;
            player = null;
            isPatrolling = false;
            caughtPlayer = false;
            isAttacking = false;

            // Cleanup completo alla morte
            CleanupWarningSequence();
            ResetWarningState();

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

            return;
        }

        animator?.SetTrigger("Dizzy");
        StartDizzy();
    }

    // FIXED: Reset warning con opzione forzata
    public void ResetWarningAudio(bool forceReset = false)
    {
        DebugLog("Manual warning reset requested" + (forceReset ? " (FORCED)" : ""));
        if (forceReset)
        {
            // Force reset ignora il timeout
            ResetWarningState();
            lastPlayerLostTime = 0f;
            playerWasVisible = false;
        }
        else
        {
            // Reset normale segue le regole del timeout
            if (CanTriggerWarning())
            {
                ResetWarningState();
            }
        }
        CleanupWarningSequence();
    }
    
    private void ResetWarningState()
    {
        hasTriggeredWarningThisChase = false;
        isPlayingWarningSequence = false;
        DebugLog("Warning state reset");
    }

    // Metodo chiamato tramite Animation Event alla fine animazione morte
    public void DestroyAfterDeath()
    {
        if (!isDead) return;
        DebugLog("Starting death sequence...");
        StartCoroutine(DestroyAfterDeathSequence());
    }

    private IEnumerator DestroyAfterDeathSequence()
    {
        yield return new WaitForSeconds(2f);

        // Riproduci il suono di morte
        if (deathAudioSource != null && deathAudioClip != null)
        {
            deathAudioSource.PlayOneShot(deathAudioClip);
            DebugLog("Playing death audio");
        }

        // Avvia l'effetto visivo di morte
        if (deathEffectController != null)
        {
            deathEffectController.PlayEffect();
            DebugLog("Playing death effect");
        }

        // Nasconde il renderer del modello
        if (Renderer != null)
        {
            Renderer.enabled = false;
            DebugLog("Model renderer disabled");
        }

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
        DebugLog("Life gem spawned");

        // Distrugge il GameObject
        DebugLog("Destroying slime GameObject");
        Destroy(gameObject);
    }

    // Metodo per riprodurre l'audio custom tramite Animation Event
    public void PlayCustomAudio()
    {
        if (isDead) return;

        if (customAudioSource != null && customAudioClip != null)
        {
            customAudioSource.PlayOneShot(customAudioClip);
            DebugLog("Playing custom audio");
        }
        else
        {
            DebugLog("CustomAudioSource or CustomAudioClip not assigned!");
        }
    }

    private void OnDrawGizmosSelected()
    {
        // VIEW RADIUS
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, viewRadius);

        // ATTACK RANGE
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        // VIEW ANGLE
        Vector3 leftBoundary = Quaternion.Euler(0, -viewAngle / 2, 0) * transform.forward;
        Vector3 rightBoundary = Quaternion.Euler(0, viewAngle / 2, 0) * transform.forward;

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, transform.position + leftBoundary * viewRadius);
        Gizmos.DrawLine(transform.position, transform.position + rightBoundary * viewRadius);

        // Show distance to player if visible
        if (Application.isPlaying && playerVisible && player != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(transform.position, player.position);
            
            // Draw distance text in Scene view
            #if UNITY_EDITOR
            UnityEditor.Handles.Label(
                Vector3.Lerp(transform.position, player.position, 0.5f), 
                $"Distance: {Vector3.Distance(transform.position, player.position):F2}"
            );
            #endif
        }

        // Show warning reset timer in Scene view
        #if UNITY_EDITOR
        if (Application.isPlaying && hasTriggeredWarningThisChase && lastPlayerLostTime > 0f)
        {
            float timeSinceLost = Time.time - lastPlayerLostTime;
            float timeRemaining = warningResetTime - timeSinceLost;
            
            if (timeRemaining > 0f)
            {
                UnityEditor.Handles.Label(
                    transform.position + Vector3.up * 3f,
                    $"Warning Reset: {timeRemaining:F1}s",
                    new GUIStyle() { normal = new GUIStyleState() { textColor = Color.yellow } }
                );
            }
            else
            {
                UnityEditor.Handles.Label(
                    transform.position + Vector3.up * 3f,
                    "Warning Ready!",
                    new GUIStyle() { normal = new GUIStyleState() { textColor = Color.green } }
                );
            }
        }
        #endif
    }
}