using UnityEngine;
using UnityEngine.Splines;
using System.Collections.Generic;

public class RaftPlatform : MonoBehaviour
{
    public SplineContainer splineContainer;
    public float speed = 10f;
    public int sampleResolution = 100;

    private List<Vector3> sampledPoints = new List<Vector3>();
    private List<float> cumulativeDistances = new List<float>();
    private float currentDistance = 0f;
    private float totalLength = 0f;
    private int direction = 1;

    private Vector3 lastPosition;
    private CharacterController playerController = null;

    private float speedMultiplier = 1f;

    private Vector3 deltaMovement;

    public Vector3 DeltaMovement => deltaMovement;

    private bool isMoving = false;
    private bool isWaitingAtEnd = false;
    private bool wasMovingWithPlayer = false;

    private float startDistance = 0f;
    private float endDistance = 0f;

    // ✅ NUOVO: Sistema tempo minimo per attivazione
    [Header("Activation Settings")]
    [SerializeField] private float minimumTriggerTime = 1.5f; // Tempo minimo sul trigger prima di partire
    [SerializeField] private bool debugActivationSystem = true;
    
    private float playerOnTriggerTime = 0f;
    private bool playerReadyToTravel = false;
    private bool isCountingActivationTime = false;

    // Sistema per gestione respawn
    [Header("Respawn Settings")]
    [SerializeField] private float returnToTerminalRadius = 50f;
    [SerializeField] private bool debugRespawnSystem = true;
    
    private bool isReturningToTerminal = false;
    private Vector3 lastKnownPlayerPosition;
    private bool playerWasOnBoard = false;

    // Integrazione sistema checkpoint
    [Header("Checkpoint Integration")]
    [SerializeField] private bool useCheckpointSystem = true;
    [SerializeField] private CheckpointManager checkpointManager;

    private Vector3 lastKnownCheckpointPosition;
    private bool hasCheckpointPosition = false;

    // Sistema per gestire i salti del player
    [Header("Jump Detection")]
    [SerializeField] private float jumpIgnoreTime = 1.5f;
    private float playerExitTime = -1f;
    private bool playerJustExited = false;
    private int lastDirectionWithPlayer = 1;
    private bool wasMovingBeforeJump = false;

    void Start()
    {
        SampleSpline();
        
        startDistance = 0f;
        endDistance = GetDistanceAtT(1f / (splineContainer.Spline.Count - 1));
        
        currentDistance = startDistance;
        
        Vector3 startPosition = GetPositionAtDistance(startDistance);
        transform.position = startPosition;
        lastPosition = startPosition;

        isWaitingAtEnd = true;
        
        SetupCheckpointIntegration();
        lastKnownPlayerPosition = startPosition;
        
        if (debugRespawnSystem)
        {
            Debug.Log($"[RaftPlatform] Zattera posizionata automaticamente all'inizio della spline: {startPosition}");
            Debug.Log($"[RaftPlatform] currentDistance={currentDistance}, startDistance={startDistance}, endDistance={endDistance}");
        }
    }

    void Update()
    {
        // ✅ NUOVO: Gestione sistema tempo minimo per attivazione
        HandleActivationTimer();
        
        CheckForPlayerRespawn();
        
        if (playerJustExited && (Time.time - playerExitTime) >= jumpIgnoreTime)
        {
            if (debugRespawnSystem)
            {
                Debug.Log("[RaftPlatform] Player sceso definitivamente, fermo la piattaforma.");
            }
            
            ResetPlayerState();
        }

        if (isMoving)
        {
            if (playerController != null)
            {
                lastDirectionWithPlayer = direction;
                wasMovingBeforeJump = true;
            }
            
            currentDistance += speed * speedMultiplier * direction * Time.deltaTime;

            if (direction == 1 && currentDistance >= endDistance)
            {
                currentDistance = endDistance;
                isMoving = false;
                isWaitingAtEnd = true;
                isReturningToTerminal = false;
                wasMovingBeforeJump = false;
            }
            else if (direction == -1 && currentDistance <= startDistance)
            {
                currentDistance = startDistance;
                isMoving = false;
                isWaitingAtEnd = true;
                isReturningToTerminal = false;
                wasMovingBeforeJump = false;
            }

            Vector3 newPosition = GetPositionAtDistance(currentDistance);
            deltaMovement = newPosition - lastPosition;
            transform.position = newPosition;
            lastPosition = newPosition;
        }
        else
        {
            deltaMovement = Vector3.zero;
            lastPosition = transform.position;

            if (wasMovingWithPlayer && playerController == null && !isReturningToTerminal && !playerJustExited)
            {
                StartReturningToTerminalNearZattera();
            }
        }
    }

    // ✅ NUOVO: Gestisce il timer per l'attivazione
    private void HandleActivationTimer()
    {
        // Se il player è sul trigger e la zattera è ferma
        if (playerController != null && !isMoving && !isReturningToTerminal)
        {
            if (!isCountingActivationTime)
            {
                // Inizia il countdown
                isCountingActivationTime = true;
                playerOnTriggerTime = 0f;
                playerReadyToTravel = false;
                
                if (debugActivationSystem)
                {
                    Debug.Log($"[RaftPlatform] Iniziato countdown attivazione ({minimumTriggerTime}s)");
                }
            }
            
            // Incrementa il timer
            playerOnTriggerTime += Time.deltaTime;
            
            // Controlla se ha raggiunto il tempo minimo
            if (playerOnTriggerTime >= minimumTriggerTime && !playerReadyToTravel)
            {
                playerReadyToTravel = true;
                
                if (debugActivationSystem)
                {
                    Debug.Log("[RaftPlatform] Tempo minimo raggiunto, zattera pronta a partire!");
                }
                
                StartMovement();
            }
        }
        else if (isCountingActivationTime)
        {
            // Reset del timer se non ci sono le condizioni
            ResetActivationTimer();
        }
    }

    // ✅ NUOVO: Avvia il movimento della zattera
    private void StartMovement()
    {
        isMoving = true;
        isWaitingAtEnd = false;
        isReturningToTerminal = false;

        // Determina direzione basata sulla posizione attuale
        if (Mathf.Approximately(currentDistance, startDistance))
        {
            direction = 1; // Verso end
        }
        else if (Mathf.Approximately(currentDistance, endDistance))
        {
            direction = -1; // Torna indietro
        }
        else
        {
            // Usa direzione precedente se disponibile
            direction = lastDirectionWithPlayer;
        }

        wasMovingWithPlayer = true;
        
        if (debugActivationSystem)
        {
            Debug.Log($"[RaftPlatform] Movimento avviato in direzione {direction}");
        }
        
        // Reset timer attivazione
        ResetActivationTimer();
    }

    // ✅ NUOVO: Reset del timer di attivazione
    private void ResetActivationTimer()
    {
        isCountingActivationTime = false;
        playerOnTriggerTime = 0f;
        playerReadyToTravel = false;
        
        if (debugActivationSystem && playerOnTriggerTime > 0)
        {
            Debug.Log("[RaftPlatform] Timer attivazione resettato");
        }
    }

    // ✅ NUOVO: Reset completo stato player
    private void ResetPlayerState()
    {
        playerController = null;
        isMoving = false;
        wasMovingWithPlayer = false;
        playerJustExited = false;
        wasMovingBeforeJump = false;
        ResetActivationTimer();
    }

    // ✅ NUOVO: Proprietà pubblica per UI/Debug
    public float GetActivationProgress()
    {
        if (!isCountingActivationTime) return 0f;
        return Mathf.Clamp01(playerOnTriggerTime / minimumTriggerTime);
    }

    public bool IsCountingActivation => isCountingActivationTime;
    public float RemainingActivationTime => Mathf.Max(0f, minimumTriggerTime - playerOnTriggerTime);

    // Setup integrazione con checkpoint system
    private void SetupCheckpointIntegration()
    {
        if (!useCheckpointSystem) return;
        
        if (checkpointManager == null)
        {
            checkpointManager = CheckpointManager.Instance;
            if (checkpointManager == null)
            {
                checkpointManager = FindFirstObjectByType<CheckpointManager>();
            }
        }
        
        if (checkpointManager != null)
        {
            checkpointManager.OnPlayerCheckpointChanged.AddListener(OnPlayerCheckpointUpdated);
            
            if (checkpointManager.HasActiveCheckpoint())
            {
                lastKnownCheckpointPosition = checkpointManager.GetCurrentSpawnPosition();
                hasCheckpointPosition = true;
                
                if (debugRespawnSystem)
                {
                    Debug.Log($"[RaftPlatform] Checkpoint iniziale trovato: {lastKnownCheckpointPosition}");
                }
            }
        }
        else if (debugRespawnSystem)
        {
            Debug.LogWarning("[RaftPlatform] CheckpointManager non trovato, sistema checkpoint disabilitato");
        }
    }

    private void OnPlayerCheckpointUpdated(Vector3 checkpointPosition)
    {
        lastKnownCheckpointPosition = checkpointPosition;
        hasCheckpointPosition = true;
        
        if (debugRespawnSystem)
        {
            Debug.Log($"[RaftPlatform] Nuovo checkpoint player ricevuto: {checkpointPosition}");
        }
        
        if (isReturningToTerminal && !isWaitingAtEnd)
        {
            if (debugRespawnSystem)
            {
                Debug.Log("[RaftPlatform] Player ha raggiunto checkpoint durante ritorno automatico, ricalcolo destinazione");
            }
            StartReturningToTerminalNearPlayer(checkpointPosition);
        }
    }

    private void CheckForPlayerRespawn()
    {
        if (playerController == null && !isReturningToTerminal && playerWasOnBoard)
        {
            Vector3 playerRespawnPosition;
            bool foundPlayerPosition = false;
            
            if (useCheckpointSystem && hasCheckpointPosition)
            {
                playerRespawnPosition = lastKnownCheckpointPosition;
                foundPlayerPosition = true;
                
                if (debugRespawnSystem)
                {
                    Debug.Log($"[RaftPlatform] Usando posizione checkpoint per respawn: {playerRespawnPosition}");
                }
            }
            else
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    playerRespawnPosition = player.transform.position;
                    foundPlayerPosition = true;
                    
                    if (debugRespawnSystem)
                    {
                        Debug.Log($"[RaftPlatform] Player trovato nella scena: {playerRespawnPosition}");
                    }
                }
                else
                {
                    playerRespawnPosition = Vector3.zero;
                }
            }
            
            if (foundPlayerPosition)
            {
                float distanceToPlayer = Vector3.Distance(transform.position, playerRespawnPosition);
                
                if (debugRespawnSystem)
                {
                    Debug.Log($"[RaftPlatform] Distanza zattera-player: {distanceToPlayer:F1}m");
                }

                if (distanceToPlayer > returnToTerminalRadius)
                {
                    if (debugRespawnSystem)
                    {
                        Debug.Log($"[RaftPlatform] Player respawnato lontano ({distanceToPlayer:F1}m > {returnToTerminalRadius}m), torno al capolinea più vicino AL PLAYER");
                    }
                    
                    StartReturningToTerminalNearPlayer(playerRespawnPosition);
                    playerWasOnBoard = false;
                }
            }
        }
    }

    private void StartReturningToTerminalNearPlayer(Vector3 playerPosition)
    {
        isReturningToTerminal = true;
        isMoving = true;
        isWaitingAtEnd = false;
        
        Vector3 startPos = GetPositionAtDistance(startDistance);
        Vector3 endPos = GetPositionAtDistance(endDistance);
        
        float distancePlayerToStart = Vector3.Distance(playerPosition, startPos);
        float distancePlayerToEnd = Vector3.Distance(playerPosition, endPos);
        
        direction = (distancePlayerToStart < distancePlayerToEnd) ? -1 : 1;
        
        if (debugRespawnSystem)
        {
            string targetTerminal = (direction == -1) ? "START" : "END";
            float targetDistance = (direction == -1) ? distancePlayerToStart : distancePlayerToEnd;
            Debug.Log($"[RaftPlatform] Ritorno al capolinea {targetTerminal} più vicino al PLAYER (distanza player-capolinea: {targetDistance:F1})");
            Debug.Log($"[RaftPlatform] Player position: {playerPosition}, Start: {startPos}, End: {endPos}");
        }
        
        wasMovingWithPlayer = false;
        ResetActivationTimer(); // Reset timer quando inizia ritorno automatico
    }

    private void StartReturningToTerminalNearZattera()
    {
        isReturningToTerminal = true;
        isMoving = true;
        isWaitingAtEnd = false;
        
        float distanceToStart = Mathf.Abs(currentDistance - startDistance);
        float distanceToEnd = Mathf.Abs(currentDistance - endDistance);
        
        direction = (distanceToStart < distanceToEnd) ? -1 : 1;
        
        if (debugRespawnSystem)
        {
            string targetTerminal = (direction == -1) ? "START" : "END";
            Debug.Log($"[RaftPlatform] Ritorno al capolinea {targetTerminal} più vicino alla ZATTERA (distanza: {(direction == -1 ? distanceToStart : distanceToEnd):F1})");
        }
        
        wasMovingWithPlayer = false;
        ResetActivationTimer();
    }

    public void ForceReturnToNearestTerminal()
    {
        if (useCheckpointSystem && hasCheckpointPosition)
        {
            if (debugRespawnSystem)
            {
                Debug.Log("[RaftPlatform] Forzato ritorno al capolinea più vicino al CHECKPOINT");
            }
            StartReturningToTerminalNearPlayer(lastKnownCheckpointPosition);
            return;
        }
        
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            if (debugRespawnSystem)
            {
                Debug.Log("[RaftPlatform] Forzato ritorno al capolinea più vicino al PLAYER");
            }
            StartReturningToTerminalNearPlayer(player.transform.position);
        }
        else
        {
            if (debugRespawnSystem)
            {
                Debug.Log("[RaftPlatform] Player non trovato, forzato ritorno al capolinea più vicino alla ZATTERA");
            }
            StartReturningToTerminalNearZattera();
        }
    }

    public void UpdatePlayerCheckpointPosition(Vector3 checkpointPosition)
    {
        lastKnownCheckpointPosition = checkpointPosition;
        hasCheckpointPosition = true;
        
        if (debugRespawnSystem)
        {
            Debug.Log($"[RaftPlatform] Posizione checkpoint aggiornata manualmente: {checkpointPosition}");
        }
    }

    public void DisableCheckpointSystem()
    {
        useCheckpointSystem = false;
        hasCheckpointPosition = false;
        
        if (checkpointManager != null)
        {
            checkpointManager.OnPlayerCheckpointChanged.RemoveListener(OnPlayerCheckpointUpdated);
        }
        
        if (debugRespawnSystem)
        {
            Debug.Log("[RaftPlatform] Sistema checkpoint disabilitato");
        }
    }

    public float GetDistanceToNearestTerminal()
    {
        float distanceToStart = Mathf.Abs(currentDistance - startDistance);
        float distanceToEnd = Mathf.Abs(currentDistance - endDistance);
        return Mathf.Min(distanceToStart, distanceToEnd);
    }

    public float GetPlayerDistanceToNearestTerminal()
    {
        Vector3 playerPos;
        
        if (useCheckpointSystem && hasCheckpointPosition)
        {
            playerPos = lastKnownCheckpointPosition;
        }
        else
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) return float.MaxValue;
            playerPos = player.transform.position;
        }
        
        Vector3 startPos = GetPositionAtDistance(startDistance);
        Vector3 endPos = GetPositionAtDistance(endDistance);
        
        float distanceToStart = Vector3.Distance(playerPos, startPos);
        float distanceToEnd = Vector3.Distance(playerPos, endPos);
        
        return Mathf.Min(distanceToStart, distanceToEnd);
    }

    public bool IsAtTerminal()
    {
        float tolerance = 0.1f;
        return Mathf.Abs(currentDistance - startDistance) < tolerance || 
               Mathf.Abs(currentDistance - endDistance) < tolerance;
    }

    [ContextMenu("Reset to Start Position")]
    public void ResetToStartPosition()
    {
        if (sampledPoints.Count == 0)
        {
            SampleSpline();
        }
        
        currentDistance = startDistance;
        Vector3 startPosition = GetPositionAtDistance(startDistance);
        transform.position = startPosition;
        lastPosition = startPosition;
        
        isMoving = false;
        isWaitingAtEnd = true;
        isReturningToTerminal = false;
        ResetActivationTimer();
        
        if (debugRespawnSystem)
        {
            Debug.Log($"[RaftPlatform] Zattera riposizionata manualmente all'inizio: {startPosition}");
        }
    }

    void SampleSpline()
    {
        sampledPoints.Clear();
        cumulativeDistances.Clear();

        totalLength = 0f;
        Vector3 prevPoint = splineContainer.EvaluatePosition(0f);
        sampledPoints.Add(prevPoint);
        cumulativeDistances.Add(0f);

        for (int i = 1; i <= sampleResolution; i++)
        {
            float t = (float)i / sampleResolution;
            Vector3 point = splineContainer.EvaluatePosition(t);
            float dist = Vector3.Distance(prevPoint, point);
            totalLength += dist;

            sampledPoints.Add(point);
            cumulativeDistances.Add(totalLength);
            prevPoint = point;
        }
    }

    float GetDistanceAtT(float t)
    {
        float targetDistance = t * totalLength;
        for (int i = 1; i < cumulativeDistances.Count; i++)
        {
            if (cumulativeDistances[i] >= targetDistance)
            {
                return cumulativeDistances[i];
            }
        }
        return totalLength;
    }

    Vector3 GetPositionAtDistance(float distance)
    {
        if (distance <= 0f) return sampledPoints[0];
        if (distance >= totalLength) return sampledPoints[sampledPoints.Count - 1];

        for (int i = 1; i < cumulativeDistances.Count; i++)
        {
            if (cumulativeDistances[i] >= distance)
            {
                float prevDist = cumulativeDistances[i - 1];
                float nextDist = cumulativeDistances[i];
                float segmentT = Mathf.InverseLerp(prevDist, nextDist, distance);
                return Vector3.Lerp(sampledPoints[i - 1], sampledPoints[i], segmentT);
            }
        }

        return sampledPoints[sampledPoints.Count - 1];
    }

    public void SetSpeedMultiplier(float multiplier)
    {
        speedMultiplier = multiplier;
        Debug.Log($"[RaftPlatform] {gameObject.name} speed multiplier impostato a {multiplier}");
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            // Se il player è rientrato poco dopo essere uscito, era solo un salto
            if (playerJustExited && (Time.time - playerExitTime) < jumpIgnoreTime)
            {
                if (debugActivationSystem)
                {
                    Debug.Log($"[RaftPlatform] Player atterrato dopo salto ({(Time.time - playerExitTime):F2}s), continuo movimento.");
                }
                
                playerJustExited = false;
                playerController = other.GetComponent<CharacterController>();
                
                if (wasMovingBeforeJump && !isMoving)
                {
                    // ✅ MODIFICATO: Riprendi movimento immediato per salti, senza timer
                    isMoving = true;
                    direction = lastDirectionWithPlayer;
                    
                    if (debugActivationSystem)
                    {
                        Debug.Log($"[RaftPlatform] Riprendo movimento immediato nella direzione {direction} (dopo salto)");
                    }
                }
                
                return;
            }
            
            // ✅ MODIFICATO: Nuovo ingresso - NON parte più immediatamente
            playerController = other.GetComponent<CharacterController>();
            playerWasOnBoard = true;
            playerJustExited = false;
            wasMovingBeforeJump = false;
            
            lastKnownPlayerPosition = other.transform.position;

            if (debugActivationSystem)
            {
                Debug.Log($"[RaftPlatform] Player entrato nel trigger. Timer di attivazione necessario: {minimumTriggerTime}s");
            }
            
            // ✅ Il movimento verrà avviato dal timer in HandleActivationTimer()
            // Non più avvio immediato!
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (playerController == other.GetComponent<CharacterController>())
            {
                lastKnownPlayerPosition = other.transform.position;
                
                playerExitTime = Time.time;
                playerJustExited = true;
                wasMovingBeforeJump = isMoving;
                
                // ✅ NUOVO: Reset timer attivazione quando esce
                ResetActivationTimer();
                
                if (debugActivationSystem)
                {
                    Debug.Log($"[RaftPlatform] Player uscito dal trigger, timer salto avviato. Era in movimento: {wasMovingBeforeJump}");
                }
            }
        }
    }

    private void OnDestroy()
    {
        if (checkpointManager != null)
        {
            checkpointManager.OnPlayerCheckpointChanged.RemoveListener(OnPlayerCheckpointUpdated);
        }
    }

    #if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, returnToTerminalRadius);
        
        Gizmos.color = Color.green;
        Vector3 startPos = GetPositionAtDistance(startDistance);
        Vector3 endPos = GetPositionAtDistance(endDistance);
        
        Gizmos.DrawWireSphere(startPos, 2f);
        Gizmos.DrawWireSphere(endPos, 2f);
        
        UnityEditor.Handles.Label(startPos + Vector3.up * 3, "START");
        UnityEditor.Handles.Label(endPos + Vector3.up * 3, "END");
        
        if (isReturningToTerminal)
        {
            Gizmos.color = Color.red;
            Vector3 targetPos = (direction == -1) ? startPos : endPos;
            Gizmos.DrawLine(transform.position, targetPos);
        }
        
        if (hasCheckpointPosition)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(lastKnownCheckpointPosition, 1.5f);
            UnityEditor.Handles.Label(lastKnownCheckpointPosition + Vector3.up * 4, "CHECKPOINT");
            
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(lastKnownCheckpointPosition, startPos);
            Gizmos.DrawLine(lastKnownCheckpointPosition, endPos);
        }
        
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null && !hasCheckpointPosition)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(player.transform.position, 1f);
            UnityEditor.Handles.Label(player.transform.position + Vector3.up * 4, "PLAYER");
            
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(player.transform.position, startPos);
            Gizmos.DrawLine(player.transform.position, endPos);
        }

        // ✅ NUOVO: Visualizzazione timer attivazione
        if (isCountingActivationTime)
        {
            float progress = GetActivationProgress();
            string timerText = $"Activation: {(minimumTriggerTime - playerOnTriggerTime):F1}s ({progress:P0})";
            
            // Barra di progresso colorata
            Gizmos.color = Color.Lerp(Color.red, Color.green, progress);
            UnityEditor.Handles.Label(transform.position + Vector3.up * 6, timerText);
            
            // Barra visuale
            Vector3 barStart = transform.position + Vector3.up * 5 + Vector3.left * 2;
            Vector3 barEnd = barStart + Vector3.right * 4;
            Vector3 barProgress = Vector3.Lerp(barStart, barEnd, progress);
            
            Gizmos.color = Color.white;
            Gizmos.DrawLine(barStart, barEnd);
            Gizmos.color = Color.green;
            Gizmos.DrawLine(barStart, barProgress);
        }

        if (playerJustExited)
        {
            float timeRemaining = jumpIgnoreTime - (Time.time - playerExitTime);
            UnityEditor.Handles.Label(transform.position + Vector3.up * 7, $"Jump Timer: {timeRemaining:F1}s");
        }
    }
    #endif
}