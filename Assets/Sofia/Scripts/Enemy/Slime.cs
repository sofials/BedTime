using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

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
    [Tooltip("Time before warning can be triggered again (should be ~20s)")]
    public float warningResetTime = 20f;
    public float visionPersistenceTime = 3f;
    private float playerLastSeenTime = 0f;

    [Header("Waypoint Management")]
    [Tooltip("Maximum enemies allowed near a waypoint")]
    public int maxEnemiesPerWaypoint = 2;
    [Tooltip("Distance to check for other enemies around waypoint")]
    public float waypointOccupancyRadius = 3f;
    [Tooltip("LayerMask for other enemies")]
    public LayerMask enemyMask = -1;

    [Header("🔍 WARNING DEBUG")]
    [Tooltip("Enable detailed warning system debugging")]
    public bool enableWarningDebug = false;
    [Tooltip("Show debug logs for vision changes")]
    public bool debugVisionChanges = false;

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

    // 🎵 WARNING SYSTEM: Traccia lifetime del warning
    private bool hasEverSeenPlayer = false;
    private bool hasPlayedWarningEver = false;

    // Warning session variables
    private float lastWarningTime = -999f;
    private float playerFirstSeenTime = 0f;
    private bool isCurrentlyInWarningCooldown = false;
    private Coroutine currentWarningCoroutine = null;

    private Animator animator;

    // 🔍 DEBUG: Traccia chiamate al warning system
    private int warningCallCount = 0;
    private float lastWarningAttemptTime = 0f;
    private string lastWarningBlockReason = "";

    private void Awake()
    {
        ForceResetAllWarningVariables();
    }

    private void OnEnable()
    {
        // Reinizializza il NavMeshAgent quando il nemico viene riattivato
        // Questo è necessario per il respawn dopo DreamWave o altri sistemi che disattivano/riattivano i nemici
        StartCoroutine(ReinitializeAgentOnEnable());
    }

    /// <summary>
    /// Reinizializza il NavMeshAgent quando il nemico viene riattivato
    /// </summary>
    private IEnumerator ReinitializeAgentOnEnable()
    {
        // Attendi un frame per permettere alla posizione di stabilizzarsi
        yield return null;

        if (agent == null)
            agent = GetComponent<NavMeshAgent>();

        if (agent == null) yield break;

        // Se l'agent non è sulla NavMesh, prova a warparci
        if (!agent.isOnNavMesh)
        {
            // Disabilita e riabilita l'agent per forzare il reset
            agent.enabled = false;
            yield return null;
            agent.enabled = true;
            yield return null;

            // Prova a warpare sulla NavMesh
            UnityEngine.AI.NavMeshHit hit;
            if (UnityEngine.AI.NavMesh.SamplePosition(transform.position, out hit, 10f, UnityEngine.AI.NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
            }
        }

        yield return null;

        // Imposta la destinazione al waypoint più vicino
        if (agent.isOnNavMesh && waypoints != null && waypoints.Length > 0)
        {
            Transform closestWaypoint = FindClosestWaypointTransform();
            if (closestWaypoint != null)
            {
                agent.SetDestination(closestWaypoint.position);
            }
        }

        // Reset dello stato per tornare in patrol
        isPatrolling = true;
        caughtPlayer = false;
        isAttacking = false;
        playerVisible = false;
        player = null;
    }

    /// <summary>
    /// Trova il waypoint più vicino alla posizione attuale
    /// </summary>
    private Transform FindClosestWaypointTransform()
    {
        if (waypoints == null || waypoints.Length == 0) return null;

        Transform closest = null;
        float minDistance = float.MaxValue;

        foreach (Transform wp in waypoints)
        {
            if (wp == null) continue;
            float dist = Vector3.Distance(transform.position, wp.position);
            if (dist < minDistance)
            {
                minDistance = dist;
                closest = wp;
            }
        }

        return closest;
    }

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
        {
            int bestWaypoint = FindBestAvailableWaypoint();
            currentWaypoint = bestWaypoint;

            // ✅ Verifica che l'agent sia sulla NavMesh prima di impostare la destinazione
            if (agent.isOnNavMesh)
            {
                agent.SetDestination(waypoints[currentWaypoint].position);
            }
            else
            {
                // Se non è sulla NavMesh, ritenta nel prossimo frame
                StartCoroutine(SetDestinationWhenReady());
            }
        }

        if (deathEffectController != null)
            deathEffectController.StopEffect();

        if (enemyChildObjectToActivate == null)
        {
            Transform chaseIndicator = transform.Find("ChaseIndicator");
            if (chaseIndicator != null)
                enemyChildObjectToActivate = chaseIndicator.gameObject;
        }

        if (enemyChildObjectToActivate != null)
            enemyChildObjectToActivate.SetActive(false);

        if (deathAudioSource == null && chaseAudioSource != null)
            deathAudioSource = chaseAudioSource;

        // 🔍 DEBUG: Initial state log
        if (enableWarningDebug)
        {
            WarningDebugLog($"🔧 INITIALIZED - hasPlayedWarningEver: {hasPlayedWarningEver}, hasEverSeenPlayer: {hasEverSeenPlayer}");
        }
    }

    /// <summary>
    /// Attende che il NavMeshAgent sia sulla NavMesh prima di impostare la destinazione
    /// </summary>
    private IEnumerator SetDestinationWhenReady()
    {
        // Attendi fino a quando l'agent è sulla NavMesh
        while (!agent.isOnNavMesh)
        {
            yield return null; // Aspetta il prossimo frame
        }

        // Ora che l'agent è sulla NavMesh, imposta la destinazione
        if (waypoints != null && waypoints.Length > 0 && currentWaypoint >= 0 && currentWaypoint < waypoints.Length)
        {
            agent.SetDestination(waypoints[currentWaypoint].position);
        }
    }

    private void ForceResetAllWarningVariables()
    {
        lastWarningTime = -999f;
        playerFirstSeenTime = 0f;
        isCurrentlyInWarningCooldown = false;

        // Stop any running warning coroutine
        if (currentWarningCoroutine != null)
        {
            StopCoroutine(currentWarningCoroutine);
            currentWarningCoroutine = null;
        }

        // Reset state variables
        player = null;
        playerVisible = false;
        isPatrolling = true;
        caughtPlayer = false;
        isAttacking = false;

        if (enableWarningDebug)
        {
            WarningDebugLog("🔄 RESET - Warning variables reset (keeping lifetime flags)");
        }
    }

    private void Update()
    {
        if (isDead)
        {
            StopAgentSafely();
            return;
        }

        if (player != null)
        {
            float distanceToPlayer = Vector3.Distance(transform.position, player.position);
            if (distanceToPlayer > viewRadius * 2f)
            {
                if (enableWarningDebug)
                {
                    WarningDebugLog($"🔄 FORCE RESET: Player too far {distanceToPlayer:F2}m > {viewRadius * 2f}m");
                }
                player = null;
                playerVisible = false;
                ResetToPatrol();
                return;
            }
        }

        UpdatePlayerVisibility();

        // Check cooldown
        if (isCurrentlyInWarningCooldown && Time.time - lastWarningTime >= warningResetTime)
        {
            isCurrentlyInWarningCooldown = false;
            if (enableWarningDebug)
            {
                WarningDebugLog("⏰ COOLDOWN ENDED - Ready for next warning");
            }
        }

        if (isDizzy) return;

        // Main behavior logic
        if (playerVisible && !caughtPlayer)
        {
            float distanceToPlayer = Vector3.Distance(transform.position, player.position);

            if (isPatrolling)
            {
                isPatrolling = false;
                playerFirstSeenTime = Time.time;

                // 🎵 WARNING: Controllo per first-time warning
                if (CanTriggerWarningFirstTime(distanceToPlayer))
                {
                    TriggerWarningSequence();
                }
                else if (!hasEverSeenPlayer)
                {
                    hasEverSeenPlayer = true;
                    if (enableWarningDebug)
                    {
                        WarningDebugLog($"👁️ FIRST SIGHT - No audio trigger (reason: {lastWarningBlockReason})");
                    }
                }
            }
            ChasePlayer();
        }
        else
        {
            if (!isPatrolling && !IsPlayerRecentlyLost())
            {
                ResetToPatrol();
            }
            else if (!isPatrolling && IsPlayerRecentlyLost())
            {
                ChaseLastKnownPosition();
            }
            else
            {
                Patrol();
            }
        }
    }

    // 🎵 WARNING: Controllo specifico per il PRIMO warning con debug dettagliato
    private bool CanTriggerWarningFirstTime(float playerDistance = -1f)
    {
        warningCallCount++;
        lastWarningAttemptTime = Time.time;

        if (enableWarningDebug)
        {
            WarningDebugLog($"🔍 WARNING CHECK #{warningCallCount} - Starting validation...");
        }

        // Check 1: Enemy alive
        if (isDead)
        {
            lastWarningBlockReason = "Enemy is dead";
            if (enableWarningDebug) WarningDebugLog($"❌ BLOCKED: {lastWarningBlockReason}");
            return false;
        }

        // Check 2: Never played warning before (MAIN CHECK)
        if (hasPlayedWarningEver)
        {
            lastWarningBlockReason = "Warning already played in lifetime";
            if (enableWarningDebug) WarningDebugLog($"❌ BLOCKED: {lastWarningBlockReason}");
            return false;
        }

        // Check 3: Player visibility
        if (!playerVisible || player == null)
        {
            lastWarningBlockReason = "Player not visible";
            if (enableWarningDebug) WarningDebugLog($"❌ BLOCKED: {lastWarningBlockReason}");
            return false;
        }

        // Check 4: Distance validation
        if (playerDistance < 0f)
        {
            playerDistance = Vector3.Distance(transform.position, player.position);
        }
        if (playerDistance > viewRadius)
        {
            lastWarningBlockReason = $"Distance {playerDistance:F2}m > viewRadius {viewRadius}m";
            if (enableWarningDebug) WarningDebugLog($"❌ BLOCKED: {lastWarningBlockReason}");
            return false;
        }

        // Check 5: Audio components
        if (chaseAudioSource == null || chaseAudioClip == null)
        {
            lastWarningBlockReason = "Missing audio components";
            if (enableWarningDebug)
            {
                WarningDebugLog($"❌ BLOCKED: {lastWarningBlockReason} (source: {chaseAudioSource != null}, clip: {chaseAudioClip != null})");
            }
            return false;
        }

        // ✅ All checks passed
        if (enableWarningDebug)
        {
            WarningDebugLog($"✅ VALIDATION PASSED - Distance: {playerDistance:F2}m, All components ready");
        }

        lastWarningBlockReason = "All checks passed";
        return true;
    }

    private void UpdatePlayerVisibility()
    {
        if (isDead)
        {
            playerVisible = false;
            player = null;
            playerLastSeenTime = 0f;
            return;
        }

        bool previouslyVisible = playerVisible;
        playerVisible = false;
        Transform detectedPlayer = null;

        Collider[] hits = Physics.OverlapSphere(transform.position, viewRadius, playerMask);

        foreach (var hit in hits)
        {
            Vector3 dirToPlayer = (hit.transform.position - transform.position);
            float distance = dirToPlayer.magnitude;

            if (distance > viewRadius)
            {
                continue;
            }

            Vector3 dirNormalized = dirToPlayer.normalized;
            float angle = Vector3.Angle(transform.forward, dirNormalized);

            if (angle <= viewAngle / 2)
            {
                if (!Physics.Raycast(transform.position + Vector3.up * 0.5f, dirNormalized, distance, obstacleMask))
                {
                    playerVisible = true;
                    detectedPlayer = hit.transform;

                    // 🎵 First sight tracking
                    if (!hasEverSeenPlayer)
                    {
                        hasEverSeenPlayer = true;
                        if (enableWarningDebug)
                        {
                            WarningDebugLog($"👁️ FIRST SIGHT EVER - Player detected at {distance:F2}m");
                        }
                    }

                    // 🔍 Debug vision changes
                    if (debugVisionChanges && !previouslyVisible)
                    {
                        WarningDebugLog($"👁️ VISION ACQUIRED - Player at {distance:F2}m, angle: {angle:F1}°");
                    }
                    break;
                }
            }
        }

        // Update player reference
        if (playerVisible && detectedPlayer != null)
        {
            player = detectedPlayer;
            playerLastSeenTime = Time.time;
        }

        // Handle vision loss
        if (!playerVisible && player != null)
        {
            float timeSinceLastSeen = Time.time - playerLastSeenTime;
            if (timeSinceLastSeen >= visionPersistenceTime)
            {
                if (debugVisionChanges)
                {
                    WarningDebugLog($"👁️ VISION LOST - Player lost for {timeSinceLastSeen:F1}s");
                }
                player = null;
                playerFirstSeenTime = 0f;
            }
        }
    }

    private void ChasePlayer()
    {
        if (isDead || player == null) return;

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        if (distanceToPlayer > viewRadius * 1.2f)
        {
            player = null;
            ResetToPatrol();
            return;
        }

        // Attack logic
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

        // Navigation
        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.speed = runSpeed;

            if (!agent.hasPath || Vector3.Distance(agent.destination, player.position) > 2f)
            {
                agent.SetDestination(player.position);
            }
        }

        // Handle reaching destination
        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            if (!playerVisible)
            {
                if (waitTimer <= 0f)
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
        else
        {
            waitTimer = waitTimeAtPoint;
        }
    }

    // 🎵 WARNING: Trigger del warning con debug completo
    private void TriggerWarningSequence()
    {
        // Final safety checks
        if (isDead)
        {
            if (enableWarningDebug) WarningDebugLog("🚨 TRIGGER ABORTED: Enemy is dead");
            return;
        }

        if (player == null)
        {
            if (enableWarningDebug) WarningDebugLog("🚨 TRIGGER ABORTED: No player reference");
            return;
        }

        float finalDistance = Vector3.Distance(transform.position, player.position);
        if (finalDistance > viewRadius)
        {
            if (enableWarningDebug)
            {
                WarningDebugLog($"🚨 TRIGGER ABORTED: Final distance check failed {finalDistance:F2}m > {viewRadius}m");
            }

            playerVisible = false;
            player = null;
            ResetToPatrol();
            return;
        }

        // 🎵 MAIN ACTION: Mark warning as played forever
        hasPlayedWarningEver = true;
        hasEverSeenPlayer = true;

        // 🔍 CRITICAL DEBUG: Log the actual trigger
        WarningDebugLog($"🔊 WARNING TRIGGERED! 🔊 Distance: {finalDistance:F2}m, Time: {Time.time:F2}s");

        lastWarningTime = Time.time;
        isCurrentlyInWarningCooldown = true;

        // Visual indicator
        if (enemyChildObjectToActivate != null)
            enemyChildObjectToActivate.SetActive(true);

        // 🔍 DEBUG: Audio playback
        if (chaseAudioSource != null && chaseAudioClip != null)
        {
            chaseAudioSource.Stop();
            chaseAudioSource.PlayOneShot(chaseAudioClip);

            if (enableWarningDebug)
            {
                WarningDebugLog($"🎧 AUDIO PLAYED - Clip: '{chaseAudioClip.name}', Length: {chaseAudioClip.length:F2}s");
            }

            // Start coroutine sequence
            if (currentWarningCoroutine != null)
                StopCoroutine(currentWarningCoroutine);

            currentWarningCoroutine = StartCoroutine(WarningSequenceCoroutine());
        }
        else
        {
            if (enableWarningDebug)
            {
                WarningDebugLog("🚨 AUDIO FAILED - Missing components!");
            }
        }
    }

    private IEnumerator WarningSequenceCoroutine()
    {
        float waitTime = chaseAudioClip != null ? chaseAudioClip.length : 2f;

        if (enableWarningDebug)
        {
            WarningDebugLog($"⏳ WARNING SEQUENCE - Waiting {waitTime:F2}s for audio to complete...");
        }

        yield return new WaitForSeconds(waitTime);

        if (playerVisible || IsPlayerRecentlyLost())
        {
            PlaySecondAudio();
        }

        if (enemyChildObjectToActivate != null)
            enemyChildObjectToActivate.SetActive(false);

        if (enableWarningDebug)
        {
            WarningDebugLog("✅ WARNING SEQUENCE COMPLETE");
        }

        currentWarningCoroutine = null;
    }

    private void PlaySecondAudio()
    {
        if (secondAudioSource != null && secondAudioClip != null)
        {
            secondAudioSource.Stop();
            secondAudioSource.PlayOneShot(secondAudioClip);

            if (enableWarningDebug)
            {
                WarningDebugLog($"🎧 SECOND AUDIO PLAYED - '{secondAudioClip.name}'");
            }
        }
        else if (secondAudioClip != null && chaseAudioSource != null)
        {
            chaseAudioSource.Stop();
            chaseAudioSource.PlayOneShot(secondAudioClip);

            if (enableWarningDebug)
            {
                WarningDebugLog($"🎧 SECOND AUDIO PLAYED (via chase source) - '{secondAudioClip.name}'");
            }
        }
    }

    private bool IsPlayerRecentlyLost()
    {
        if (player == null) return false;

        float timeSinceLastSeen = Time.time - playerLastSeenTime;
        return !playerVisible && timeSinceLastSeen < visionPersistenceTime;
    }

    private void ChaseLastKnownPosition()
    {
        if (player != null && agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.speed = runSpeed;
            agent.SetDestination(player.position);
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

        int nextWaypoint = FindBestAvailableWaypoint();

        if (nextWaypoint != currentWaypoint)
        {
            currentWaypoint = nextWaypoint;
            if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                agent.SetDestination(waypoints[currentWaypoint].position);
            }
        }
        else
        {
            currentWaypoint = (currentWaypoint + 1) % waypoints.Length;
            if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                agent.SetDestination(waypoints[currentWaypoint].position);
            }
        }
    }

    private int FindBestAvailableWaypoint()
    {
        if (waypoints == null || waypoints.Length == 0) return 0;

        var waypointScores = new List<(int index, float score)>();

        for (int i = 0; i < waypoints.Length; i++)
        {
            if (waypoints[i] == null) continue;

            float distance = Vector3.Distance(transform.position, waypoints[i].position);
            int enemyCount = CountEnemiesNearWaypoint(waypoints[i].position);

            float occupationPenalty = enemyCount >= maxEnemiesPerWaypoint ? 1000f : enemyCount * 10f;
            float score = distance + occupationPenalty;

            waypointScores.Add((i, score));
        }

        waypointScores.Sort((a, b) => a.score.CompareTo(b.score));
        int bestWaypoint = waypointScores.Count > 0 ? waypointScores[0].index : 0;
        return bestWaypoint;
    }

    private int CountEnemiesNearWaypoint(Vector3 waypointPosition)
    {
        Collider[] enemiesNear = Physics.OverlapSphere(waypointPosition, waypointOccupancyRadius, enemyMask);

        int count = 0;
        foreach (var enemy in enemiesNear)
        {
            if (enemy.transform != this.transform)
            {
                count++;
            }
        }

        return count;
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

        CleanupWarningSequence();

        int bestWaypoint = FindBestAvailableWaypoint();
        currentWaypoint = bestWaypoint;

        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.speed = walkSpeed;
            agent.SetDestination(waypoints[currentWaypoint].position);
        }
    }

    private void CleanupWarningSequence()
    {
        // Stop visual indicator
        if (enemyChildObjectToActivate != null && enemyChildObjectToActivate.activeSelf)
        {
            enemyChildObjectToActivate.SetActive(false);
        }

        // Stop coroutine
        if (currentWarningCoroutine != null)
        {
            StopCoroutine(currentWarningCoroutine);
            currentWarningCoroutine = null;
        }

        // Stop all audio
        if (chaseAudioSource != null && chaseAudioSource.isPlaying)
        {
            chaseAudioSource.Stop();
        }
        if (secondAudioSource != null && secondAudioSource.isPlaying)
        {
            secondAudioSource.Stop();
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
            Die();
            return;
        }

        animator?.SetTrigger("Dizzy");
        StartDizzy();
    }

    public void DestroyAfterDeath()
    {
        if (!isDead) return;
        StartCoroutine(DestroyAfterDeathSequence());
    }

    private IEnumerator DestroyAfterDeathSequence()
    {
        yield return new WaitForSeconds(2f);

        if (deathAudioSource != null && deathAudioClip != null)
        {
            deathAudioSource.PlayOneShot(deathAudioClip);
        }

        if (deathEffectController != null)
        {
            deathEffectController.PlayEffect();
        }

        if (Renderer != null)
        {
            Renderer.enabled = false;
        }

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

        GemManager.Instance?.SpawnLifeGem(transform.position);
        Destroy(gameObject);
    }

    private void Die()
    {
        if (isDead) return;

        isDead = true;

        // Reset completo dello stato
        playerVisible = false;
        player = null;
        isPatrolling = false;
        caughtPlayer = false;
        isAttacking = false;
        isDizzy = false;

        // Cleanup warning system
        CleanupWarningSequence();
        ForceResetAllWarningVariables();

        // Stop agent
        StopAgentSafely();

        // Disable agent
        if (agent != null)
        {
            agent.enabled = false;
        }

        // Trigger death animation
        animator?.SetTrigger("Die");

        if (enableWarningDebug)
        {
            WarningDebugLog("💀 ENEMY DIED - All systems stopped");
        }
    }
    public void PlayCustomAudio()
    {
        if (isDead) return;

        if (customAudioSource != null && customAudioClip != null)
        {
            customAudioSource.PlayOneShot(customAudioClip);
        }
    }

    // 🔍 UTILITY: Centralized warning debug logging
    private void WarningDebugLog(string message)
    {
        if (enableWarningDebug)
        {
            Debug.Log($"🎵 <color=yellow>[{name}]</color> {message} <color=gray>(T:{Time.time:F2})</color>");
        }
    }

    // 🔍 DEBUG METHODS: Simplified and focused on warning system
    [ContextMenu("🔍 Debug Warning State")]
    public void DebugWarningState()
    {
        float playerDist = player != null ? Vector3.Distance(transform.position, player.position) : -1f;

        string statusIcon = isDead ? "💀" : (hasPlayedWarningEver ? "🔇" : "🔊");

        Debug.Log($"🎵 <color=white>[WARNING STATE DEBUG]</color> {name} {statusIcon}\n" +
                 $"🎯 LIFETIME STATUS:\n" +
                 $"  ├─ hasPlayedWarningEver: {hasPlayedWarningEver}\n" +
                 $"  ├─ hasEverSeenPlayer: {hasEverSeenPlayer}\n" +
                 $"  └─ Can play warning: {(!hasPlayedWarningEver && !isDead ? "✅ YES" : "❌ NO")}\n" +
                 $"👁️ CURRENT DETECTION:\n" +
                 $"  ├─ playerVisible: {playerVisible}\n" +
                 $"  ├─ Player distance: {(playerDist >= 0 ? $"{playerDist:F2}m" : "N/A")}\n" +
                 $"  ├─ Within view radius ({viewRadius}m): {(playerDist >= 0 && playerDist <= viewRadius ? "✅" : "❌")}\n" +
                 $"  └─ isDead: {isDead}\n" +
                 $"🎧 AUDIO COMPONENTS:\n" +
                 $"  ├─ chaseAudioSource: {(chaseAudioSource != null ? "✅" : "❌")}\n" +
                 $"  ├─ chaseAudioClip: {(chaseAudioClip != null ? $"✅ '{chaseAudioClip.name}'" : "❌")}\n" +
                 $"  └─ Last block reason: {lastWarningBlockReason}\n" +
                 $"📊 STATISTICS:\n" +
                 $"  ├─ Warning calls: {warningCallCount}\n" +
                 $"  ├─ Last attempt: {(lastWarningAttemptTime > 0 ? $"{Time.time - lastWarningAttemptTime:F2}s ago" : "Never")}\n" +
                 $"  └─ Cooldown active: {isCurrentlyInWarningCooldown}");
    }

    [ContextMenu("🔊 Test Warning (Force)")]
    public void TestWarningForce()
    {
        if (isDead)
        {
            Debug.LogWarning("🎵 Cannot test warning: Enemy is dead");
            return;
        }

        Debug.Log($"🎵 <color=magenta>[FORCE TEST]</color> Attempting to trigger warning...");

        // Setup test conditions
        if (player == null)
        {
            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
            {
                player = playerObj.transform;
                playerVisible = true;
                Debug.Log($"🎵 Found player for test: {player.name}");
            }
            else
            {
                Debug.LogError("🎵 No player found for test!");
                return;
            }
        }

        // Show current state before test
        DebugWarningState();

        // Attempt trigger
        if (CanTriggerWarningFirstTime())
        {
            TriggerWarningSequence();
        }
        else
        {
            Debug.LogWarning($"🎵 <color=red>[FORCE TEST FAILED]</color> Reason: {lastWarningBlockReason}");
        }
    }

    [ContextMenu("🔄 Reset Warning System")]
    public void ResetWarningSystemForDebug()
    {
        Debug.Log($"🔄 <color=red>[MANUAL RESET]</color> {name} - Resetting warning system for testing...");

        // Reset lifetime flags for testing
        hasEverSeenPlayer = false;
        hasPlayedWarningEver = false;

        // Reset counters
        warningCallCount = 0;
        lastWarningAttemptTime = 0f;
        lastWarningBlockReason = "";

        // Reset other variables
        ForceResetAllWarningVariables();

        Debug.Log($"🔄 Warning system completely reset. hasPlayedWarningEver: {hasPlayedWarningEver}");
    }

    [ContextMenu("📊 Show Debug Statistics")]
    public void ShowDebugStatistics()
    {
        Debug.Log($"📊 <color=cyan>[DEBUG STATS]</color> {name}:\n" +
                 $"⏱️ TIMING:\n" +
                 $"  ├─ Current time: {Time.time:F2}s\n" +
                 $"  ├─ Last warning: {(lastWarningTime > -999 ? $"{lastWarningTime:F2}s ({Time.time - lastWarningTime:F2}s ago)" : "Never")}\n" +
                 $"  ├─ Last attempt: {(lastWarningAttemptTime > 0 ? $"{lastWarningAttemptTime:F2}s ({Time.time - lastWarningAttemptTime:F2}s ago)" : "Never")}\n" +
                 $"  └─ Player first seen: {(playerFirstSeenTime > 0 ? $"{playerFirstSeenTime:F2}s ({Time.time - playerFirstSeenTime:F2}s ago)" : "Never")}\n" +
                 $"📈 COUNTERS:\n" +
                 $"  ├─ Total warning checks: {warningCallCount}\n" +
                 $"  ├─ Currently in cooldown: {isCurrentlyInWarningCooldown}\n" +
                 $"  └─ Cooldown time remaining: {(isCurrentlyInWarningCooldown ? Mathf.Max(0, warningResetTime - (Time.time - lastWarningTime)) : 0):F1}s\n" +
                 $"🎮 GAME STATE:\n" +
                 $"  ├─ IsPatrolling: {isPatrolling}\n" +
                 $"  ├─ IsAttacking: {isAttacking}\n" +
                 $"  ├─ IsDizzy: {isDizzy}\n" +
                 $"  └─ IsDead: {isDead}");
    }

    // 🔍 GIZMOS: Simplified visual debug
    private void OnDrawGizmosSelected()
    {
        // View radius
        if (viewRadius > 0)
        {
            Gizmos.color = playerVisible ? Color.red : Color.yellow;
            Gizmos.DrawWireSphere(transform.position, viewRadius);

            // View angle
            Vector3 leftBoundary = Quaternion.AngleAxis(-viewAngle / 2, Vector3.up) * transform.forward * viewRadius;
            Vector3 rightBoundary = Quaternion.AngleAxis(viewAngle / 2, Vector3.up) * transform.forward * viewRadius;

            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, transform.position + leftBoundary);
            Gizmos.DrawLine(transform.position, transform.position + rightBoundary);
        }

        // Attack range
        if (attackRange > 0)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRange);
        }

        // Warning system status indicator
        if (hasPlayedWarningEver)
        {
            // Purple cube = warning already played
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireCube(transform.position + Vector3.up * 2f, Vector3.one * 0.5f);
        }
        else if (isDead)
        {
            // Red X = dead
            Gizmos.color = Color.red;
            Vector3 pos = transform.position + Vector3.up * 2f;
            Gizmos.DrawLine(pos + Vector3.left * 0.3f, pos + Vector3.right * 0.3f);
            Gizmos.DrawLine(pos + Vector3.forward * 0.3f, pos + Vector3.back * 0.3f);
        }
        else
        {
            // Green sphere = ready for warning
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 2f, 0.3f);
        }

        // Current waypoint
        if (waypoints != null && waypoints.Length > 0 && currentWaypoint < waypoints.Length && waypoints[currentWaypoint] != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(transform.position, waypoints[currentWaypoint].position);
            Gizmos.DrawWireSphere(waypoints[currentWaypoint].position, 0.5f);
        }

        // Player line of sight
        if (player != null)
        {
            Gizmos.color = playerVisible ? Color.green : Color.red;
            Gizmos.DrawLine(transform.position + Vector3.up * 0.5f, player.position + Vector3.up * 0.5f);
        }
    }
}