using UnityEngine;
using UnityEngine.Splines;
using UnityEngine.Events;
using System.Collections.Generic;
using System.Linq;

[System.Serializable]
public class RaftSettings
{
    [Header("Movement")]
    public float speed = 10f;
    [Range(0.1f, 5f)] public float accelerationTime = 2f;
    [Range(0.1f, 5f)] public float decelerationTime = 1.5f;
    
    [Header("Anti-Accidental Activation")]
    [Range(0f, 2f)] public float accidentalPreventionTime = 0.3f; // Short delay to prevent accidents
    public bool requirePlayerVelocityCheck = true; // Check if player is moving intentionally
    [Range(0.1f, 5f)] public float minimumPlayerSpeed = 0.5f; // Min speed to consider intentional
    public bool requirePlayerGrounded = false;
    
    [Header("Jump vs Respawn Detection")]
    [Range(1f, 10f)] public float respawnDetectionTime = 5f; // Time to consider player respawned
    
    [Header("Respawn")]
    [Range(10f, 200f)] public float returnToTerminalRadius = 50f;
    
    [Header("Performance")]
    [Range(50, 500)] public int sampleResolution = 100;
    public bool enablePredictiveMovement = true;
    [Range(0.1f, 2f)] public float playerCacheDuration = 1f;
    
    [Header("Legacy - Deprecated")]
    [Range(0.1f, 10f)] public float minimumTriggerTime = 0f; // Deprecated - use accidentalPreventionTime
}

public enum RaftState 
{
    WaitingAtTerminal,
    CountingActivation, 
    Moving,
    ReturningToTerminal,
    Disabled
}

public enum RespawnMode
{
    NearestToPlayer,
    NearestToRaft,
    AlwaysStart,
    AlwaysEnd
}

public class RaftPlatform : MonoBehaviour
{
    [Header("Core Configuration")]
    public SplineContainer splineContainer;
    [SerializeField] private RaftSettings settings = new();
    
    // Backward compatibility - delegano alle settings
    public float speed 
    { 
        get => settings.speed; 
        set => settings.speed = value; 
    }
    public int sampleResolution 
    { 
        get => settings.sampleResolution; 
        set => settings.sampleResolution = value; 
    }
    [SerializeField] private AnimationCurve accelerationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    
    [Header("Events")]
    public UnityEvent OnRaftStart = new();
    public UnityEvent OnRaftStop = new();
    public UnityEvent OnPlayerBoard = new();
    public UnityEvent OnPlayerExit = new();

    // Core movement data
    private List<Vector3> sampledPoints = new List<Vector3>();
    private List<float> cumulativeDistances = new List<float>();
    private float currentDistance = 0f;
    private float totalLength = 0f;
    private int direction = 1;

    // Position tracking
    private Vector3 lastPosition;
    private Vector3 deltaMovement;
    public Vector3 DeltaMovement => deltaMovement;

    // Player tracking - optimized
    private CharacterController playerController = null;
    private Transform _cachedPlayerTransform;
    private float _lastPlayerCacheTime;
    private static readonly Dictionary<Collider, CharacterController> _controllerCache = new();

    // State management
    private RaftState _currentState = RaftState.WaitingAtTerminal;
    public RaftState CurrentState => _currentState;
    
    // Legacy state flags for compatibility
    private bool isMoving => _currentState == RaftState.Moving;
    private bool isWaitingAtEnd => _currentState == RaftState.WaitingAtTerminal;
    private bool isReturningToTerminal => _currentState == RaftState.ReturningToTerminal;
    
    private float speedMultiplier = 1f;
    private float _currentSpeedRatio = 0f;
    private float _targetSpeedRatio = 0f;

    // Terminal positions
    private float startDistance = 0f;
    private float endDistance = 0f;

    // Activation system - MODIFIED
    [Header("Activation Settings")]
    [SerializeField] private float minimumTriggerTime = 0f; // Deprecated - use settings.accidentalPreventionTime
    [SerializeField] private bool debugActivationSystem = true;
    
    private float playerOnTriggerTime = 0f;
    private bool playerReadyToTravel = false;
    private bool isCountingActivationTime = false;
    private bool wasMovingWithPlayer = false;
    private float playerEnterTime = 0f; // NEW: Track when player entered

    // Respawn system
    [Header("Respawn Settings")]
    [SerializeField] private float returnToTerminalRadius = 50f; // Backward compatibility
    [SerializeField] private bool debugRespawnSystem = true;
    [SerializeField] private RespawnMode respawnMode = RespawnMode.NearestToPlayer;
    
    private Vector3 lastKnownPlayerPosition;
    private bool playerWasOnBoard = false;

    // Checkpoint integration
    [Header("Checkpoint Integration")]
    [SerializeField] private bool useCheckpointSystem = true;
    [SerializeField] private CheckpointManager checkpointManager;
    
    private Vector3 lastKnownCheckpointPosition;
    private bool hasCheckpointPosition = false;

    // Jump detection - MODIFIED
    [Header("Jump Detection")]
    [SerializeField] private float jumpIgnoreTime = 5f; // INCREASED - now used for respawn detection
    private float playerExitTime = -1f;
    private bool playerJustExited = false;
    private int lastDirectionWithPlayer = 1;
    private bool wasMovingBeforeExit = false; // RENAMED from wasMovingBeforeJump

    // Performance optimization
    private const float VALIDATION_INTERVAL = 1f;
    private float _lastValidationTime;
    private bool _isValidConfiguration = true;
    
    // Telemetry data
    private List<float> _activationTimes = new List<float>();
    private int _completedTrips = 0;
    private int _boardEvents = 0;
    private int _exitEvents = 0;
    private List<float> _speedSamples = new List<float>(60);

    void Start()
    {
        // Sync settings for backward compatibility
        SyncSettingsFromSerializedFields();
        
        if (!ValidateConfiguration())
        {
            Debug.LogError($"[RaftPlatform] {gameObject.name}: Configurazione non valida, componente disabilitato");
            enabled = false;
            return;
        }

        SampleSpline();
        InitializePositions();
        SetupCheckpointIntegration();
        
        _currentState = RaftState.WaitingAtTerminal;
        
        if (debugRespawnSystem)
        {
            Debug.Log($"[RaftPlatform] {gameObject.name} inizializzato correttamente");
        }
    }

    void Update()
    {
        // Periodic validation
        if (Time.time - _lastValidationTime > VALIDATION_INTERVAL)
        {
            _lastValidationTime = Time.time;
            if (!ValidateRuntimeState())
                return;
        }

        HandleStateMachine();
        CheckForPlayerRespawn();
        HandleRespawnDetection(); // RENAMED from HandleJumpDetection
        CollectTelemetryData();
    }

    #region Configuration & Validation

    private void SyncSettingsFromSerializedFields()
    {
        // Sync dei campi serializzati con le settings per backward compatibility
        if (minimumTriggerTime > 0) 
        {
            settings.accidentalPreventionTime = minimumTriggerTime; // Map old setting to new
        }
        if (returnToTerminalRadius > 0) settings.returnToTerminalRadius = returnToTerminalRadius;
        if (jumpIgnoreTime > 0) settings.respawnDetectionTime = jumpIgnoreTime; // Map old setting to new
        
        // E viceversa - aggiorna i campi serializzati
        minimumTriggerTime = settings.accidentalPreventionTime;
        returnToTerminalRadius = settings.returnToTerminalRadius;
        jumpIgnoreTime = settings.respawnDetectionTime;
        
        if (debugActivationSystem)
        {
            Debug.Log($"[RaftPlatform] {gameObject.name}: Settings sincronizzate - accidentalPreventionTime: {settings.accidentalPreventionTime}s, respawnDetectionTime: {settings.respawnDetectionTime}s");
        }
    }

    private bool ValidateConfiguration()
    {
        if (splineContainer?.Spline == null)
        {
            Debug.LogError($"[RaftPlatform] {gameObject.name}: SplineContainer o Spline mancanti!");
            return false;
        }
        
        if (splineContainer.Spline.Count < 2)
        {
            Debug.LogError($"[RaftPlatform] {gameObject.name}: La spline deve avere almeno 2 punti di controllo!");
            return false;
        }

        settings.speed = Mathf.Max(0.1f, settings.speed);
        settings.accidentalPreventionTime = Mathf.Max(0f, settings.accidentalPreventionTime);
        settings.respawnDetectionTime = Mathf.Max(1f, settings.respawnDetectionTime);
        settings.returnToTerminalRadius = Mathf.Max(1f, settings.returnToTerminalRadius);
        
        return true;
    }

    private bool ValidateRuntimeState()
    {
        if (!_isValidConfiguration) return false;
        
        // Verifica che il spline container non sia stato distrutto
        if (splineContainer == null || splineContainer.Spline == null)
        {
            Debug.LogError($"[RaftPlatform] {gameObject.name}: SplineContainer perso durante runtime!");
            _isValidConfiguration = false;
            return false;
        }
        
        return true;
    }

    private void InitializePositions()
    {
        startDistance = 0f;
        endDistance = GetDistanceAtT(1f / (splineContainer.Spline.Count - 1));
        currentDistance = startDistance;
        
        Vector3 startPosition = GetPositionAtDistance(startDistance);
        transform.position = startPosition;
        lastPosition = startPosition;
        lastKnownPlayerPosition = startPosition;
    }

    #endregion

    #region State Machine - MODIFIED

    private void HandleStateMachine()
    {
        switch (_currentState)
        {
            case RaftState.WaitingAtTerminal:
                HandleWaitingAtTerminal();
                break;
                
            case RaftState.CountingActivation:
                HandleCountingActivation();
                break;
                
            case RaftState.Moving:
                HandleMoving();
                break;
                
            case RaftState.ReturningToTerminal:
                HandleReturningToTerminal();
                break;
                
            case RaftState.Disabled:
                // Do nothing
                break;
        }
    }

    private void HandleWaitingAtTerminal()
    {
        deltaMovement = Vector3.zero;
        _targetSpeedRatio = 0f;
        
        // MODIFIED: Anti-accidental activation system
        if (playerController != null && !playerJustExited)
        {
            if (ShouldStartMovement())
            {
                StartMovement();
            }
        }
        else
        {
            // Reset timer when player leaves
            playerEnterTime = 0f;
        }
    }

    private void HandleCountingActivation()
    {
        // This state is largely deprecated but kept for compatibility
        // Redirect to the new anti-accidental system
        if (playerController != null && !playerJustExited)
        {
            if (ShouldStartMovement())
            {
                StartMovement();
            }
        }
        else
        {
            ChangeState(RaftState.WaitingAtTerminal);
        }
    }

    private void HandleMoving()
    {
        UpdateMovementSmooth();
        
        if (playerController != null)
        {
            lastDirectionWithPlayer = direction;
            wasMovingBeforeExit = true;
        }

        // Check terminal reached
        if (direction == 1 && currentDistance >= endDistance)
        {
            ReachTerminal(endDistance);
        }
        else if (direction == -1 && currentDistance <= startDistance)
        {
            ReachTerminal(startDistance);
        }
    }

    private void HandleReturningToTerminal()
    {
        UpdateMovementSmooth();
        
        // Check terminal reached
        if (direction == 1 && currentDistance >= endDistance)
        {
            ReachTerminal(endDistance);
        }
        else if (direction == -1 && currentDistance <= startDistance)
        {
            ReachTerminal(startDistance);
        }
    }

    private void ChangeState(RaftState newState)
    {
        if (_currentState == newState) return;
        
        RaftState previousState = _currentState;
        _currentState = newState;
        
        OnStateChanged(previousState, newState);
    }

    private void OnStateChanged(RaftState from, RaftState to)
    {
        if (debugActivationSystem)
        {
            Debug.Log($"[RaftPlatform] {gameObject.name}: Stato cambiato da {from} a {to}");
        }
        
        switch (to)
        {
            case RaftState.CountingActivation:
                isCountingActivationTime = true;
                playerOnTriggerTime = 0f;
                playerReadyToTravel = false;
                
                if (debugActivationSystem)
                {
                    Debug.Log($"[RaftPlatform] {gameObject.name}: Iniziato countdown attivazione - {settings.accidentalPreventionTime}s richiesti");
                }
                break;
                
            case RaftState.WaitingAtTerminal:
                ResetActivationTimer();
                _targetSpeedRatio = 0f;
                break;
                
            case RaftState.Moving:
                _targetSpeedRatio = 1f;
                OnRaftStart.Invoke();
                
                if (debugActivationSystem)
                {
                    Debug.Log($"[RaftPlatform] {gameObject.name}: MOVIMENTO AVVIATO! Direzione: {direction}");
                }
                break;
        }
        
        // Audio feedback
        if (from == RaftState.Moving)
        {
            OnRaftStop.Invoke();
        }
    }

    #endregion

    #region Anti-Accidental Logic - NEW

    private bool ShouldStartMovement()
    {
        if (playerController == null || playerJustExited)
            return false;
        
        // Basic time check - brief delay to prevent accidents
        float timeOnPlatform = Time.time - playerEnterTime;
        if (timeOnPlatform < settings.accidentalPreventionTime)
        {
            if (debugActivationSystem)
            {
                Debug.Log($"[RaftPlatform] {gameObject.name}: Aspetto conferma ({timeOnPlatform:F2}/{settings.accidentalPreventionTime:F2}s)");
            }
            return false;
        }
        
        // Optional: Check player velocity/movement intention
        if (settings.requirePlayerVelocityCheck)
        {
            Vector3 playerVelocity = playerController.velocity;
            float horizontalSpeed = new Vector3(playerVelocity.x, 0, playerVelocity.z).magnitude;
            
            // If player is moving too slowly, might be accidental
            if (horizontalSpeed < settings.minimumPlayerSpeed)
            {
                if (debugActivationSystem)
                {
                    Debug.Log($"[RaftPlatform] {gameObject.name}: Player troppo lento ({horizontalSpeed:F2} < {settings.minimumPlayerSpeed}), attendo movimento intenzionale");
                }
                return false;
            }
        }
        
        // Optional: Check if grounded (prevents mid-air activation)
        if (settings.requirePlayerGrounded && !IsPlayerGrounded())
        {
            if (debugActivationSystem)
            {
                Debug.Log($"[RaftPlatform] {gameObject.name}: Player non a terra, attendo atterraggio");
            }
            return false;
        }
        
        return true;
    }

    #endregion

    #region Movement System

    private void UpdateMovementSmooth()
    {
        // Smooth acceleration/deceleration
        float accelerationRate = _targetSpeedRatio > _currentSpeedRatio ? 
            1f / settings.accelerationTime : 1f / settings.decelerationTime;
        
        _currentSpeedRatio = Mathf.MoveTowards(_currentSpeedRatio, _targetSpeedRatio, 
            accelerationRate * Time.deltaTime);
        
        float smoothedSpeed = settings.speed * speedMultiplier * 
            accelerationCurve.Evaluate(_currentSpeedRatio);
        
        currentDistance += smoothedSpeed * direction * Time.deltaTime;
        
        Vector3 newPosition = GetPositionAtDistance(currentDistance);
        deltaMovement = newPosition - lastPosition;
        transform.position = newPosition;
        lastPosition = newPosition;
    }

    private void ReachTerminal(float terminalDistance)
    {
        currentDistance = terminalDistance;
        _completedTrips++;
        
        if (_currentState == RaftState.ReturningToTerminal)
        {
            ChangeState(RaftState.WaitingAtTerminal);
        }
        else
        {
            // FIXED: Reset activation system completely at terminal
            // The player needs to "re-activate" the platform even if they're still on board
            ResetActivationTimer();
            wasMovingWithPlayer = false; // Reset this so it doesn't auto-return
            
            if (playerController != null)
            {
                // Player is still on board but we reset the activation
                // They need to wait the full activation time again
                playerEnterTime = Time.time;
                
                if (debugActivationSystem)
                {
                    Debug.Log($"[RaftPlatform] {gameObject.name}: Arrivato al terminal con player a bordo - deve riattivare la piattaforma ({settings.accidentalPreventionTime}s)");
                }
            }
            
            ChangeState(RaftState.WaitingAtTerminal);
        }
        
        wasMovingBeforeExit = false;
    }

    private void StartMovement()
    {
        // Determina direzione
        DetermineMovementDirection();
        
        wasMovingWithPlayer = true;
        ChangeState(RaftState.Moving);
        
        // Record activation time for telemetry
        if (playerEnterTime > 0)
        {
            float activationTime = Time.time - playerEnterTime;
            _activationTimes.Add(activationTime);
            if (_activationTimes.Count > 50) // Keep last 50 samples
                _activationTimes.RemoveAt(0);
        }
        
        ResetActivationTimer();
    }

    private void DetermineMovementDirection()
    {
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
    }

    #endregion

    #region Player Detection & Management

    private Transform GetCachedPlayerTransform()
    {
        if (Time.time - _lastPlayerCacheTime > settings.playerCacheDuration)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            _cachedPlayerTransform = player?.transform;
            _lastPlayerCacheTime = Time.time;
        }
        
        return _cachedPlayerTransform;
    }

    private bool IsPlayerGrounded()
    {
        if (playerController == null) return false;
        
        // Se il controllo grounded è disabilitato, considera sempre grounded
        if (!settings.requirePlayerGrounded) return true;
        
        // Simple ground check - can be improved with raycast
        return playerController.isGrounded;
    }

    private Vector3 GetPredictedPlayerPosition()
    {
        if (!settings.enablePredictiveMovement || playerController?.velocity == null)
            return playerController?.transform.position ?? Vector3.zero;
        
        return playerController.transform.position + playerController.velocity * Time.fixedDeltaTime * 2f;
    }

    private void HandleRespawnDetection() // RENAMED from HandleJumpDetection
    {
        // IMPROVED: Only consider respawn after longer absence AND make sure we don't accidentally reset during normal jumps
        if (playerJustExited && (Time.time - playerExitTime) >= settings.respawnDetectionTime)
        {
            // Additional check: only reset if the player is really far or if we haven't seen them for a while
            Transform currentPlayerTransform = GetCachedPlayerTransform();
            bool playerReallyGone = false;
            
            if (currentPlayerTransform == null)
            {
                // Player object doesn't exist - definitely respawned
                playerReallyGone = true;
            }
            else
            {
                // Check if player is very far from the platform
                float distanceToPlayer = Vector3.Distance(transform.position, currentPlayerTransform.position);
                if (distanceToPlayer > settings.returnToTerminalRadius)
                {
                    playerReallyGone = true;
                }
            }
            
            if (playerReallyGone)
            {
                if (debugRespawnSystem)
                {
                    Debug.Log($"[RaftPlatform] {gameObject.name}: Player assente da {settings.respawnDetectionTime}s e lontano - considerato respawnato/morto");
                }
                
                ResetPlayerState();
            }
            else
            {
                if (debugRespawnSystem)
                {
                    Debug.Log($"[RaftPlatform] {gameObject.name}: Player assente da tempo ma vicino - probabilmente ancora vivo, continuo ad aspettare");
                }
            }
        }
    }

    private void ResetPlayerState()
    {
        playerController = null;
        wasMovingWithPlayer = false;
        playerJustExited = false;
        wasMovingBeforeExit = false;
        ResetActivationTimer();
        
        // MODIFIED: Stop movement only when we're sure player respawned
        if (_currentState == RaftState.Moving || _currentState == RaftState.CountingActivation)
        {
            if (debugRespawnSystem)
            {
                Debug.Log($"[RaftPlatform] {gameObject.name}: Fermata per respawn/morte player");
            }
            ChangeState(RaftState.WaitingAtTerminal);
        }
    }

    #endregion

    #region Activation Timer System - MODIFIED

    private void ResetActivationTimer()
    {
        isCountingActivationTime = false;
        playerOnTriggerTime = 0f;
        playerReadyToTravel = false;
        playerEnterTime = 0f; // NEW
        
        if (debugActivationSystem && playerOnTriggerTime > 0)
        {
            Debug.Log($"[RaftPlatform] {gameObject.name}: Timer attivazione resettato");
        }
    }

    // Public API for compatibility - MODIFIED
    public float GetActivationProgress()
    {
        if (playerEnterTime == 0f) return 0f;
        float timeOnPlatform = Time.time - playerEnterTime;
        return Mathf.Clamp01(timeOnPlatform / settings.accidentalPreventionTime);
    }

    public bool IsCountingActivation => playerEnterTime > 0f && (Time.time - playerEnterTime) < settings.accidentalPreventionTime;
    public float RemainingActivationTime => playerEnterTime > 0f ? Mathf.Max(0f, settings.accidentalPreventionTime - (Time.time - playerEnterTime)) : 0f;

    #endregion

    #region Respawn System - ENHANCED

    private void CheckForPlayerRespawn()
    {
        // MODIFIED: Only check for respawn after extended absence
        if (playerController == null && playerJustExited && 
            (Time.time - playerExitTime) >= settings.respawnDetectionTime && 
            !(_currentState == RaftState.ReturningToTerminal) && playerWasOnBoard)
        {
            Vector3 playerRespawnPosition;
            bool foundPlayerPosition = GetPlayerRespawnPosition(out playerRespawnPosition);
            
            if (foundPlayerPosition)
            {
                float distanceToPlayer = Vector3.Distance(transform.position, playerRespawnPosition);
                
                if (debugRespawnSystem)
                {
                    Debug.Log($"[RaftPlatform] {gameObject.name}: Rilevato respawn - Distanza: {distanceToPlayer:F1}m");
                }

                if (distanceToPlayer > settings.returnToTerminalRadius)
                {
                    if (debugRespawnSystem)
                    {
                        Debug.Log($"[RaftPlatform] {gameObject.name}: Player respawnato lontano, attivo ritorno automatico");
                    }
                    
                    StartReturningBasedOnMode(playerRespawnPosition);
                    playerWasOnBoard = false;
                }
            }
        }
    }

    private bool GetPlayerRespawnPosition(out Vector3 position)
    {
        if (useCheckpointSystem && hasCheckpointPosition)
        {
            position = lastKnownCheckpointPosition;
            return true;
        }
        
        Transform playerTransform = GetCachedPlayerTransform();
        if (playerTransform != null)
        {
            position = playerTransform.position;
            return true;
        }
        
        position = Vector3.zero;
        return false;
    }

    private void StartReturningBasedOnMode(Vector3 referencePosition)
    {
        switch (respawnMode)
        {
            case RespawnMode.NearestToPlayer:
                StartReturningToTerminalNearPlayer(referencePosition);
                break;
            case RespawnMode.NearestToRaft:
                StartReturningToTerminalNearZattera();
                break;
            case RespawnMode.AlwaysStart:
                StartReturningToSpecificTerminal(-1);
                break;
            case RespawnMode.AlwaysEnd:
                StartReturningToSpecificTerminal(1);
                break;
        }
    }

    private void StartReturningToTerminalNearPlayer(Vector3 playerPosition)
    {
        Vector3 startPos = GetPositionAtDistance(startDistance);
        Vector3 endPos = GetPositionAtDistance(endDistance);
        
        float distancePlayerToStart = Vector3.Distance(playerPosition, startPos);
        float distancePlayerToEnd = Vector3.Distance(playerPosition, endPos);
        
        direction = (distancePlayerToStart < distancePlayerToEnd) ? -1 : 1;
        
        ChangeState(RaftState.ReturningToTerminal);
        wasMovingWithPlayer = false;
        
        if (debugRespawnSystem)
        {
            string targetTerminal = (direction == -1) ? "START" : "END";
            Debug.Log($"[RaftPlatform] {gameObject.name}: Ritorno al capolinea {targetTerminal} più vicino al PLAYER");
        }
    }

    private void StartReturningToTerminalNearZattera()
    {
        float distanceToStart = Mathf.Abs(currentDistance - startDistance);
        float distanceToEnd = Mathf.Abs(currentDistance - endDistance);
        
        direction = (distanceToStart < distanceToEnd) ? -1 : 1;
        
        ChangeState(RaftState.ReturningToTerminal);
        wasMovingWithPlayer = false;
        
        if (debugRespawnSystem)
        {
            string targetTerminal = (direction == -1) ? "START" : "END";
            Debug.Log($"[RaftPlatform] {gameObject.name}: Ritorno al capolinea {targetTerminal} più vicino alla ZATTERA");
        }
    }

    private void StartReturningToSpecificTerminal(int targetDirection)
    {
        direction = targetDirection;
        ChangeState(RaftState.ReturningToTerminal);
        wasMovingWithPlayer = false;
        
        if (debugRespawnSystem)
        {
            string targetTerminal = (direction == -1) ? "START" : "END";
            Debug.Log($"[RaftPlatform] {gameObject.name}: Ritorno forzato al capolinea {targetTerminal}");
        }
    }

    #endregion

    #region Checkpoint Integration

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
                    Debug.Log($"[RaftPlatform] {gameObject.name}: Checkpoint iniziale trovato");
                }
            }
        }
        else if (debugRespawnSystem)
        {
            Debug.LogWarning($"[RaftPlatform] {gameObject.name}: CheckpointManager non trovato, sistema checkpoint disabilitato");
        }
    }

    private void OnPlayerCheckpointUpdated(Vector3 checkpointPosition)
    {
        lastKnownCheckpointPosition = checkpointPosition;
        hasCheckpointPosition = true;
        
        if (debugRespawnSystem)
        {
            Debug.Log($"[RaftPlatform] {gameObject.name}: Nuovo checkpoint ricevuto");
        }
        
        if (_currentState == RaftState.ReturningToTerminal)
        {
            if (debugRespawnSystem)
            {
                Debug.Log($"[RaftPlatform] {gameObject.name}: Ricalcolo destinazione per nuovo checkpoint");
            }
            StartReturningToTerminalNearPlayer(checkpointPosition);
        }
    }

    #endregion

    #region Telemetry and Analytics

    private void CollectTelemetryData()
    {
        // Collect speed samples for analytics
        if (_currentState == RaftState.Moving)
        {
            float currentSpeed = settings.speed * speedMultiplier * _currentSpeedRatio;
            _speedSamples.Add(currentSpeed);
            
            if (_speedSamples.Count > 300) // ~5 seconds at 60fps
                _speedSamples.RemoveAt(0);
        }
    }

    public RaftTelemetryData GetTelemetryData()
    {
        return new RaftTelemetryData
        {
            AverageActivationTime = _activationTimes.Count > 0 ? _activationTimes.Average() : 0f,
            TotalTrips = _completedTrips,
            PlayerExitRate = _boardEvents > 0 ? (float)_exitEvents / _boardEvents : 0f,
            AverageSpeed = _speedSamples.Count > 0 ? _speedSamples.Average() : 0f,
            CurrentState = _currentState,
            IsPlayerOnBoard = playerController != null
        };
    }

    [System.Serializable]
    public struct RaftTelemetryData
    {
        public float AverageActivationTime;
        public int TotalTrips;
        public float PlayerExitRate;
        public float AverageSpeed;
        public RaftState CurrentState;
        public bool IsPlayerOnBoard;
    }

    #endregion

    #region Spline Sampling and Positioning

    void SampleSpline()
    {
        sampledPoints.Clear();
        cumulativeDistances.Clear();

        totalLength = 0f;
        Vector3 prevPoint = splineContainer.EvaluatePosition(0f);
        sampledPoints.Add(prevPoint);
        cumulativeDistances.Add(0f);

        for (int i = 1; i <= settings.sampleResolution; i++)
        {
            float t = (float)i / settings.sampleResolution;
            Vector3 point = splineContainer.EvaluatePosition(t);
            float dist = Vector3.Distance(prevPoint, point);
            totalLength += dist;

            sampledPoints.Add(point);
            cumulativeDistances.Add(totalLength);
            prevPoint = point;
        }

        if (debugRespawnSystem)
        {
            Debug.Log($"[RaftPlatform] {gameObject.name}: Spline campionata - {sampledPoints.Count} punti, lunghezza totale: {totalLength:F2}m");
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

    #endregion

    #region Public API (Backward Compatibility)

    public void SetSpeedMultiplier(float multiplier)
    {
        speedMultiplier = Mathf.Max(0.1f, multiplier);
        
        if (debugActivationSystem)
        {
            Debug.Log($"[RaftPlatform] {gameObject.name}: Speed multiplier impostato a {multiplier}");
        }
    }

    public void ForceReturnToNearestTerminal()
    {
        Vector3 referencePosition;
        
        if (GetPlayerRespawnPosition(out referencePosition))
        {
            if (debugRespawnSystem)
            {
                Debug.Log($"[RaftPlatform] {gameObject.name}: Forzato ritorno al capolinea");
            }
            StartReturningBasedOnMode(referencePosition);
        }
        else
        {
            if (debugRespawnSystem)
            {
                Debug.Log($"[RaftPlatform] {gameObject.name}: Player non trovato, ritorno al più vicino alla zattera");
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
            Debug.Log($"[RaftPlatform] {gameObject.name}: Posizione checkpoint aggiornata manualmente");
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
            Debug.Log($"[RaftPlatform] {gameObject.name}: Sistema checkpoint disabilitato");
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
        
        if (!GetPlayerRespawnPosition(out playerPos))
            return float.MaxValue;
        
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
        
        ChangeState(RaftState.WaitingAtTerminal);
        ResetActivationTimer();
        ResetPlayerState();
        
        if (debugRespawnSystem)
        {
            Debug.Log($"[RaftPlatform] {gameObject.name}: Zattera riposizionata manualmente all'inizio");
        }
    }

    [ContextMenu("Force Validation")]
    public void ForceValidation()
    {
        bool isValid = ValidateConfiguration();
        Debug.Log($"[RaftPlatform] {gameObject.name}: Validazione forzata - Risultato: {(isValid ? "VALIDO" : "NON VALIDO")}");
        
        if (isValid && !enabled)
        {
            enabled = true;
            Debug.Log($"[RaftPlatform] {gameObject.name}: Componente riabilitato");
        }
    }

    [ContextMenu("Print Telemetry")]
    public void PrintTelemetry()
    {
        var telemetry = GetTelemetryData();
        Debug.Log($"[RaftPlatform] {gameObject.name} Telemetria:" +
                 $"\n- Stato attuale: {telemetry.CurrentState}" +
                 $"\n- Viaggi completati: {telemetry.TotalTrips}" +
                 $"\n- Tempo attivazione medio: {telemetry.AverageActivationTime:F2}s" +
                 $"\n- Velocità media: {telemetry.AverageSpeed:F2} u/s" +
                 $"\n- Tasso di uscita player: {telemetry.PlayerExitRate:P1}" +
                 $"\n- Player a bordo: {telemetry.IsPlayerOnBoard}");
    }

    #endregion

    #region Trigger Events - MODIFIED

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        
        // Ottimizzazione: usa cache per CharacterController
        CharacterController controller;
        if (!_controllerCache.TryGetValue(other, out controller))
        {
            controller = other.GetComponent<CharacterController>();
            _controllerCache[other] = controller;
        }
        
        // IMPROVED: Handle re-entry after temporary exit
        if (playerJustExited)
        {
            float timeAway = Time.time - playerExitTime;
            
            if (debugActivationSystem)
            {
                Debug.Log($"[RaftPlatform] {gameObject.name}: Player tornato dopo {timeAway:F2}s - Era in movimento: {wasMovingBeforeExit}");
            }
            
            playerJustExited = false;
            playerController = controller;
            
            // FIXED: Only continue movement if we were moving before AND it was a short exit (jump)
            if (wasMovingBeforeExit && _currentState != RaftState.Moving && timeAway < settings.respawnDetectionTime)
            {
                direction = lastDirectionWithPlayer;
                ChangeState(RaftState.Moving);
                
                if (debugActivationSystem)
                {
                    Debug.Log($"[RaftPlatform] {gameObject.name}: Continuo movimento nella direzione {direction} (ritorno da salto)");
                }
            }
            else if (timeAway >= settings.respawnDetectionTime)
            {
                // This was likely a respawn, reset everything and start fresh
                ResetActivationTimer();
                playerEnterTime = Time.time;
                
                if (debugActivationSystem)
                {
                    Debug.Log($"[RaftPlatform] {gameObject.name}: Probabile respawn - reset completo");
                }
            }
            else
            {
                // Short exit but we weren't moving - restart the anti-accidental timer
                playerEnterTime = Time.time;
                
                if (debugActivationSystem)
                {
                    Debug.Log($"[RaftPlatform] {gameObject.name}: Breve uscita ma non eravamo in movimento - riavvio timer");
                }
            }
            
            return;
        }
        
        // New entry - Start anti-accidental timer
        playerController = controller;
        playerWasOnBoard = true;
        playerJustExited = false;
        wasMovingBeforeExit = false;
        _boardEvents++;
        
        playerEnterTime = Time.time; // NEW: Track entry time for anti-accidental system
        lastKnownPlayerPosition = other.transform.position;
        
        OnPlayerBoard.Invoke();
        
        if (debugActivationSystem)
        {
            Debug.Log($"[RaftPlatform] {gameObject.name}: Player entrato - Timer anti-accidentale avviato ({settings.accidentalPreventionTime}s)");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        
        CharacterController controller;
        if (_controllerCache.TryGetValue(other, out controller) && playerController == controller)
        {
            lastKnownPlayerPosition = other.transform.position;
            
            playerExitTime = Time.time;
            playerJustExited = true;
            wasMovingBeforeExit = _currentState == RaftState.Moving;
            _exitEvents++;
            
            // MODIFIED: Don't immediately reset activation timer - let the system decide if it's a jump or respawn
            OnPlayerExit.Invoke();
            
            if (debugActivationSystem)
            {
                Debug.Log($"[RaftPlatform] {gameObject.name}: Player uscito - Era in movimento: {wasMovingBeforeExit}. Attendo {settings.respawnDetectionTime}s per determinare se è salto o respawn");
            }
            
            // CRITICAL: Don't stop movement here - let the respawn detection system handle it
            // Movement continues during jumps, only stops on confirmed respawn
        }
    }

    #endregion

    #region Cleanup

    private void OnDestroy()
    {
        if (checkpointManager != null)
        {
            checkpointManager.OnPlayerCheckpointChanged.RemoveListener(OnPlayerCheckpointUpdated);
        }
        
        // Clear static cache when object is destroyed
        _controllerCache.Clear();
    }
    #endregion

    #region Editor Utilities

    #if UNITY_EDITOR
    private void OnValidate()
    {
        // Automatic parameter validation in editor
        if (settings != null)
        {
            settings.speed = Mathf.Max(0.1f, settings.speed);
            settings.accidentalPreventionTime = Mathf.Max(0f, settings.accidentalPreventionTime);
            settings.respawnDetectionTime = Mathf.Max(1f, settings.respawnDetectionTime);
            settings.returnToTerminalRadius = Mathf.Max(1f, settings.returnToTerminalRadius);
            settings.sampleResolution = Mathf.Max(10, settings.sampleResolution);
            
            // Sync with legacy fields
            speed = settings.speed;
            minimumTriggerTime = settings.accidentalPreventionTime;
            returnToTerminalRadius = settings.returnToTerminalRadius;
            jumpIgnoreTime = settings.respawnDetectionTime;
            sampleResolution = settings.sampleResolution;
        }
        
        // Validate spline in editor
        if (splineContainer?.Spline != null && splineContainer.Spline.Count < 2)
        {
            Debug.LogWarning($"[RaftPlatform] {gameObject.name}: La spline deve avere almeno 2 punti di controllo!");
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) return;

        // Terminal radius visualization
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, settings.returnToTerminalRadius);
        
        // Terminal positions
        Gizmos.color = Color.green;
        Vector3 startPos = GetPositionAtDistance(startDistance);
        Vector3 endPos = GetPositionAtDistance(endDistance);
        
        Gizmos.DrawWireSphere(startPos, 2f);
        Gizmos.DrawWireSphere(endPos, 2f);
        
        UnityEditor.Handles.Label(startPos + Vector3.up * 3, "START");
        UnityEditor.Handles.Label(endPos + Vector3.up * 3, "END");
        
        // Movement direction indicator
        if (_currentState == RaftState.ReturningToTerminal)
        {
            Gizmos.color = Color.red;
            Vector3 targetPos = (direction == -1) ? startPos : endPos;
            Gizmos.DrawLine(transform.position, targetPos);
            UnityEditor.Handles.Label(transform.position + Vector3.up * 8, "RETURNING TO TERMINAL");
        }
        else if (_currentState == RaftState.Moving)
        {
            Gizmos.color = Color.blue;
            Vector3 targetPos = (direction == -1) ? startPos : endPos;
            Gizmos.DrawLine(transform.position, targetPos);
            UnityEditor.Handles.Label(transform.position + Vector3.up * 8, $"MOVING (Speed: {(_currentSpeedRatio * 100):F0}%)");
        }
        
        // Checkpoint visualization
        if (hasCheckpointPosition)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(lastKnownCheckpointPosition, 1.5f);
            UnityEditor.Handles.Label(lastKnownCheckpointPosition + Vector3.up * 4, "CHECKPOINT");
            
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(lastKnownCheckpointPosition, startPos);
            Gizmos.DrawLine(lastKnownCheckpointPosition, endPos);
        }
        
        // Player position (if no checkpoint)
        Transform playerTransform = GetCachedPlayerTransform();
        if (playerTransform != null && !hasCheckpointPosition)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(playerTransform.position, 1f);
            UnityEditor.Handles.Label(playerTransform.position + Vector3.up * 4, "PLAYER");
            
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(playerTransform.position, startPos);
            Gizmos.DrawLine(playerTransform.position, endPos);
        }

        // Anti-accidental activation timer visualization
        if (playerEnterTime > 0f && (Time.time - playerEnterTime) < settings.accidentalPreventionTime)
        {
            float progress = (Time.time - playerEnterTime) / settings.accidentalPreventionTime;
            float remaining = settings.accidentalPreventionTime - (Time.time - playerEnterTime);
            string timerText = $"Anti-Accidental: {remaining:F1}s ({progress:P0})";
            
            // Colored progress bar
            Gizmos.color = Color.Lerp(Color.red, Color.green, progress);
            UnityEditor.Handles.Label(transform.position + Vector3.up * 6, timerText);
            
            // Visual progress bar
            Vector3 barStart = transform.position + Vector3.up * 5 + Vector3.left * 2;
            Vector3 barEnd = barStart + Vector3.right * 4;
            Vector3 barProgress = Vector3.Lerp(barStart, barEnd, progress);
            
            Gizmos.color = Color.white;
            Gizmos.DrawLine(barStart, barEnd);
            Gizmos.color = Color.green;
            Gizmos.DrawLine(barStart, barProgress);
        }

        // Respawn detection timer visualization
        if (playerJustExited)
        {
            float timeAway = Time.time - playerExitTime;
            float timeRemaining = settings.respawnDetectionTime - timeAway;
            string status = timeAway >= settings.respawnDetectionTime ? "RESPAWNED" : "JUMPING";
            UnityEditor.Handles.Label(transform.position + Vector3.up * 7, $"{status}: {timeRemaining:F1}s");
        }
        
        // State indicator
        Gizmos.color = GetStateColor(_currentState);
        string stateText = $"State: {_currentState}";
        if (_currentState == RaftState.Moving || _currentState == RaftState.ReturningToTerminal)
        {
            stateText += $" (Dir: {(direction == 1 ? "→" : "←")})";
        }
        UnityEditor.Handles.Label(transform.position + Vector3.up * 9, stateText);
        
        // Additional info for debugging
        if (playerController != null && settings.requirePlayerVelocityCheck)
        {
            float horizontalSpeed = new Vector3(playerController.velocity.x, 0, playerController.velocity.z).magnitude;
            string speedInfo = $"Player Speed: {horizontalSpeed:F1} (Min: {settings.minimumPlayerSpeed:F1})";
            Color speedColor = horizontalSpeed >= settings.minimumPlayerSpeed ? Color.green : Color.red;
            Gizmos.color = speedColor;
            UnityEditor.Handles.Label(transform.position + Vector3.up * 11, speedInfo);
        }
        
        // Telemetry info
        if (_completedTrips > 0 || _boardEvents > 0)
        {
            string telemetryText = $"Trips: {_completedTrips} | Boards: {_boardEvents} | Exits: {_exitEvents}";
            UnityEditor.Handles.Label(transform.position + Vector3.up * 12, telemetryText);
        }
    }

    private Color GetStateColor(RaftState state)
    {
        switch (state)
        {
            case RaftState.WaitingAtTerminal: return Color.white;
            case RaftState.CountingActivation: return Color.yellow;
            case RaftState.Moving: return Color.green;
            case RaftState.ReturningToTerminal: return Color.red;
            case RaftState.Disabled: return Color.gray;
            default: return Color.magenta;
        }
    }

    [UnityEditor.MenuItem("Tools/RaftPlatform/Validate All Rafts")]
    private static void ValidateAllRafts()
    {
        RaftPlatform[] rafts = FindObjectsByType<RaftPlatform>(FindObjectsSortMode.None);
        int validCount = 0;
        int invalidCount = 0;
        
        foreach (var raft in rafts)
        {
            if (raft.ValidateConfiguration())
                validCount++;
            else
                invalidCount++;
        }
        
        Debug.Log($"[RaftPlatform] Validazione completata: {validCount} valide, {invalidCount} non valide");
    }

    [UnityEditor.MenuItem("Tools/RaftPlatform/Print All Telemetry")]
    private static void PrintAllTelemetry()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[RaftPlatform] Telemetria disponibile solo durante il play mode");
            return;
        }
        
        RaftPlatform[] rafts = FindObjectsByType<RaftPlatform>(FindObjectsSortMode.None);
        foreach (var raft in rafts)
        {
            raft.PrintTelemetry();
        }
    }
    #endif

    #endregion
}