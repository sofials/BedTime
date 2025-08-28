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
    
    [Header("Terminal Control")]
    [Range(1f, 10f)] public float minimumExitTime = 2f;
    [Range(0f, 5f)] public float terminalGracePeriod = 1f;
    
    [Header("Activation Timing")]
    [Range(0f, 2f)] public float quickStartTime = 0.0f;
    [Range(0f, 10f)] public float terminalWaitTime = 3f;
    
    [Header("False Start Detection")]
    [Range(1f, 8f)] public float falseStartDetectionTime = 2f;
    [Range(5f, 30f)] public float falseStartReturnRadius = 15f;
    public bool enableAccidentalDetection = true;
    [Range(1f, 10f)] public float accidentalActivationCheckTime = 3f;
    
    [Header("Player Movement Checks")]
    public bool requirePlayerVelocityCheck = true;
    [Range(0.1f, 5f)] public float minimumPlayerSpeed = 0.5f;
    public bool requirePlayerGrounded = false;
    
    [Header("Jump vs Respawn Detection")]
    [Range(1f, 10f)] public float respawnDetectionTime = 5f;
    
    [Header("Respawn")]
    [Range(10f, 200f)] public float returnToTerminalRadius = 50f;
    
    [Header("Performance")]
    [Range(50, 500)] public int sampleResolution = 100;
    public bool enablePredictiveMovement = true;
    [Range(0.1f, 2f)] public float playerCacheDuration = 1f;
    
    [Header("Legacy - Deprecated")]
    [Range(0.1f, 10f)] public float minimumTriggerTime = 0f;
    [Range(0f, 2f)] public float accidentalPreventionTime = 0.1f;
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
    
    // FIX: Better terminal state tracking
    private bool justArrivedAtTerminal = false;
    private float terminalArrivalTime = 0f;

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
    
    private bool isMoving => _currentState == RaftState.Moving;
    private bool isWaitingAtEnd => _currentState == RaftState.WaitingAtTerminal;
    private bool isReturningToTerminal => _currentState == RaftState.ReturningToTerminal;
    
    private float speedMultiplier = 1f;
    private float _currentSpeedRatio = 0f;
    private float _targetSpeedRatio = 0f;

    // Terminal positions
    private float startDistance = 0f;
    private float endDistance = 0f;

    // FIX: Better activation system tracking
    [Header("Activation Settings")]
    [SerializeField] private float minimumTriggerTime = 0f;
    [SerializeField] private bool debugActivationSystem = true;
    private float playerEnterTime = 0f;
    private bool playerCanActivateRaft = false; // NEW: Explicit activation permission

    // Respawn system
    [Header("Respawn Settings")]
    [SerializeField] private float returnToTerminalRadius = 50f;
    [SerializeField] private bool debugRespawnSystem = true;
    [SerializeField] private RespawnMode respawnMode = RespawnMode.NearestToPlayer;
    
    private Vector3 lastKnownPlayerPosition;
    private bool playerWasOnBoard = false;
    private float lastMovementStartTime = 0f;

    // Checkpoint integration
    [Header("Checkpoint Integration")]
    [SerializeField] private bool useCheckpointSystem = true;
    [SerializeField] private CheckpointManager checkpointManager;
    
    private Vector3 lastKnownCheckpointPosition;
    private bool hasCheckpointPosition = false;

    // FIX: Better exit tracking
    [Header("Jump Detection")]
    [SerializeField] private float jumpIgnoreTime = 5f;
    private float playerExitTime = -1f;
    private bool playerJustExited = false;
    private int lastDirectionWithPlayer = 1;
    private bool wasMovingBeforeExit = false;
    private bool wasAtTerminalBeforeExit = false; // NEW: Track if we were at terminal before exit

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
        justArrivedAtTerminal = false; // Start clean
        
        if (debugRespawnSystem)
        {
            Debug.Log($"[RaftPlatform] {gameObject.name} inizializzato correttamente");
        }
    }

    void Update()
    {
        if (Time.time - _lastValidationTime > VALIDATION_INTERVAL)
        {
            _lastValidationTime = Time.time;
            if (!ValidateRuntimeState())
                return;
        }

        HandleStateMachine();
        CheckForPlayerRespawn(); // FIX: This needs to be more aggressive
        HandleRespawnDetection();
        CheckForAccidentalActivation();
        CollectTelemetryData();
    }

    #region Configuration & Validation
    
    private void SyncSettingsFromSerializedFields()
    {
        if (minimumTriggerTime > 0) 
        {
            settings.quickStartTime = minimumTriggerTime;
        }
        if (returnToTerminalRadius > 0) settings.returnToTerminalRadius = returnToTerminalRadius;
        if (jumpIgnoreTime > 0) settings.respawnDetectionTime = jumpIgnoreTime;
        
        minimumTriggerTime = settings.quickStartTime;
        returnToTerminalRadius = settings.returnToTerminalRadius;
        jumpIgnoreTime = settings.respawnDetectionTime;
        
        if (debugActivationSystem)
        {
            Debug.Log($"[RaftPlatform] {gameObject.name}: Settings sincronizzate - quickStartTime: {settings.quickStartTime}s, falseStartDetectionTime: {settings.falseStartDetectionTime}s");
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

    #region State Machine - FIXED

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
                break;
        }
    }

private void HandleWaitingAtTerminal()
{
    deltaMovement = Vector3.zero;
    _targetSpeedRatio = 0f;
    
    // FIX: Solo se il player è ANCORA sulla piattaforma e ha il permesso
    if (playerController != null && playerCanActivateRaft && !playerJustExited)
    {
        if (ShouldStartMovement())
        {
            StartMovement();
        }
    }
    
    // FIX: Se player è uscito al terminal, NON partire automaticamente
    if (playerJustExited && IsAtTerminal())
    {
        // Player uscito normalmente al terminal - resta fermo
        if (debugActivationSystem)
        {
            Debug.Log($"[RaftPlatform] {gameObject.name}: Player uscito al terminal - resto fermo");
        }
        return;
    }
}
    private void HandleCountingActivation()
    {
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

        // FIX: Check for false start IMMEDIATELY
        if (Time.time - lastMovementStartTime < settings.falseStartDetectionTime)
        {
            if (playerController == null)
            {
                if (debugActivationSystem)
                {
                    Debug.Log($"[RaftPlatform] {gameObject.name}: FALSA PARTENZA rilevata! Player sceso dopo {Time.time - lastMovementStartTime:F1}s");
                }

                HandleFalseStart();
                return;
            }
        }

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

    private void HandleFalseStart()
    {
        Vector3 playerPosition;
        bool foundPlayer = GetPlayerRespawnPosition(out playerPosition);
        
        if (foundPlayer)
        {
            float distanceToPlayer = Vector3.Distance(transform.position, playerPosition);
            
            if (distanceToPlayer <= settings.falseStartReturnRadius)
            {
                if (debugActivationSystem)
                {
                    Debug.Log($"[RaftPlatform] {gameObject.name}: Player vicino ({distanceToPlayer:F1}m), torno da lui");
                }
                StartReturningBasedOnMode(playerPosition);
            }
            else
            {
                if (debugActivationSystem)
                {
                    Debug.Log($"[RaftPlatform] {gameObject.name}: Player lontano ({distanceToPlayer:F1}m), torno al terminal più vicino");
                }
                StartReturningToTerminalNearZattera();
            }
        }
        else
        {
            StartReturningToTerminalNearZattera();
        }
        
        playerJustExited = true;
        playerExitTime = Time.time;
        playerCanActivateRaft = false; // FIX: Prevent immediate reactivation
    }

    private void HandleReturningToTerminal()
    {
        UpdateMovementSmooth();
        
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
        
        if (from == RaftState.Moving)
        {
            OnRaftStop.Invoke();
        }
    }

    #endregion

    #region Anti-Accidental Logic - FIXED

    private bool ShouldStartMovement()
    {
        if (playerController == null || playerJustExited || !playerCanActivateRaft)
            return false;
        
        float timeOnPlatform = Time.time - playerEnterTime;
        float requiredWaitTime;
        
        // FIX: Better terminal wait logic
        if (justArrivedAtTerminal)
        {
            requiredWaitTime = settings.terminalWaitTime;
            
            if (timeOnPlatform >= requiredWaitTime)
            {
                justArrivedAtTerminal = false;
            }
        }
        else
        {
            requiredWaitTime = settings.quickStartTime;
        }
        
        if (timeOnPlatform < requiredWaitTime)
        {
            if (debugActivationSystem)
            {
                string waitType = justArrivedAtTerminal ? "terminal" : "quick start";
                Debug.Log($"[RaftPlatform] {gameObject.name}: Aspetto {waitType} ({timeOnPlatform:F2}/{requiredWaitTime:F2}s)");
            }
            return false;
        }
        
        // Player movement checks
        if (settings.requirePlayerVelocityCheck)
        {
            Vector3 playerVelocity = playerController.velocity;
            float horizontalSpeed = new Vector3(playerVelocity.x, 0, playerVelocity.z).magnitude;
            
            if (horizontalSpeed < settings.minimumPlayerSpeed)
            {
                if (debugActivationSystem)
                {
                    Debug.Log($"[RaftPlatform] {gameObject.name}: Player troppo lento, attendo movimento intenzionale");
                }
                return false;
            }
        }
        
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

    // FIX: Migliore gestione arrivo al terminal
    justArrivedAtTerminal = true;
    terminalArrivalTime = Time.time;
    
    // FIX: Se arriviamo al terminal, fermiamo sempre
    ChangeState(RaftState.WaitingAtTerminal);
    
    // FIX: Se player è ancora a bordo al terminal, deve scendere o aspettare per ripartire
    if (playerController != null)
    {
        // Player ancora a bordo al terminal
        playerCanActivateRaft = false; // Deve aspettare il tempo di grazia del terminal
        
        if (debugActivationSystem)
        {
            Debug.Log($"[RaftPlatform] {gameObject.name}: Arrivato al terminal con player a bordo - attesa obbligatoria");
        }
    }
    else
    {
        // Arrivato al terminal senza player
        ResetActivationTimer();
    }

    wasMovingBeforeExit = false;
}

    private void CheckForAccidentalActivation()
    {
        if (!settings.enableAccidentalDetection) return;
        
        if (_currentState != RaftState.Moving) return;
        
        if (Time.time - lastMovementStartTime > settings.accidentalActivationCheckTime)
        {
            if (playerController == null)
            {
                if (debugActivationSystem)
                {
                    Debug.Log($"[RaftPlatform] {gameObject.name}: Attivazione accidentale rilevata - player non più a bordo, ritorno al terminal");
                }
                
                Vector3 playerPos;
                if (GetPlayerRespawnPosition(out playerPos))
                {
                    StartReturningBasedOnMode(playerPos);
                }
                else
                {
                    StartReturningToTerminalNearZattera();
                }
            }
            else
            {
                settings.enableAccidentalDetection = false;
            }
        }
    }

    private void StartMovement()
    {
        DetermineMovementDirection();
        
        lastMovementStartTime = Time.time;
        ChangeState(RaftState.Moving);
        
        if (playerEnterTime > 0)
        {
            float activationTime = Time.time - playerEnterTime;
            _activationTimes.Add(activationTime);
            if (_activationTimes.Count > 50)
                _activationTimes.RemoveAt(0);
        }
        
        ResetActivationTimer();
    }

    private void DetermineMovementDirection()
    {
        if (Mathf.Approximately(currentDistance, startDistance))
        {
            direction = 1;
        }
        else if (Mathf.Approximately(currentDistance, endDistance))
        {
            direction = -1;
        }
        else
        {
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
        if (!settings.requirePlayerGrounded) return true;
        return playerController.isGrounded;
    }

    private Vector3 GetPredictedPlayerPosition()
    {
        if (!settings.enablePredictiveMovement || playerController?.velocity == null)
            return playerController?.transform.position ?? Vector3.zero;
        
        return playerController.transform.position + playerController.velocity * Time.fixedDeltaTime * 2f;
    }

    private void HandleRespawnDetection()
    {
        if (playerJustExited && (Time.time - playerExitTime) >= settings.respawnDetectionTime)
        {
            Transform currentPlayerTransform = GetCachedPlayerTransform();
            bool playerReallyGone = false;
            
            if (currentPlayerTransform == null)
            {
                playerReallyGone = true;
            }
            else
            {
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
        }
    }

    private void ResetPlayerState()
    {
        playerController = null;
        playerJustExited = false;
        wasMovingBeforeExit = false;
        wasAtTerminalBeforeExit = false;
        playerCanActivateRaft = false; // FIX: Reset activation permission
        ResetActivationTimer();
        
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

    #region Activation Timer System - FIXED

    private void ResetActivationTimer()
    {
        playerEnterTime = 0f;
        playerCanActivateRaft = false; // FIX: Always reset activation permission
        
        if (debugActivationSystem)
        {
            Debug.Log($"[RaftPlatform] {gameObject.name}: Timer attivazione resettato");
        }
    }

    public float GetActivationProgress()
    {
        if (playerEnterTime == 0f) return 0f;
        
        float timeOnPlatform = Time.time - playerEnterTime;
        float requiredTime = justArrivedAtTerminal ? settings.terminalWaitTime : settings.quickStartTime;
        
        return Mathf.Clamp01(timeOnPlatform / requiredTime);
    }

    public bool IsCountingActivation 
    {
        get
        {
            if (playerEnterTime == 0f) return false;
            float timeOnPlatform = Time.time - playerEnterTime;
            float requiredTime = justArrivedAtTerminal ? settings.terminalWaitTime : settings.quickStartTime;
            return timeOnPlatform < requiredTime;
        }
    }

    public float RemainingActivationTime 
    {
        get
        {
            if (playerEnterTime == 0f) return 0f;
            float timeOnPlatform = Time.time - playerEnterTime;
            float requiredTime = justArrivedAtTerminal ? settings.terminalWaitTime : settings.quickStartTime;
            return Mathf.Max(0f, requiredTime - timeOnPlatform);
        }
    }

    #endregion

    #region Respawn System - FIXED

    private void CheckForPlayerRespawn()
    {
        // FIX: Be more aggressive about detecting respawns and returning to terminals
        if (playerController == null && playerWasOnBoard)
        {
            Vector3 playerRespawnPosition;
            bool foundPlayerPosition = GetPlayerRespawnPosition(out playerRespawnPosition);
            
            if (foundPlayerPosition)
            {
                float distanceToPlayer = Vector3.Distance(transform.position, playerRespawnPosition);
                
                // FIX: Check more frequently and with lower threshold
                if (distanceToPlayer > settings.returnToTerminalRadius * 0.5f) // Lower threshold
                {
                    if (_currentState != RaftState.ReturningToTerminal)
                    {
                        if (debugRespawnSystem)
                        {
                            Debug.Log($"[RaftPlatform] {gameObject.name}: Player lontano ({distanceToPlayer:F1}m), avvio ritorno automatico");
                        }
                        
                        StartReturningBasedOnMode(playerRespawnPosition);
                        playerWasOnBoard = false;
                    }
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
        if (_currentState == RaftState.Moving)
        {
            float currentSpeed = settings.speed * speedMultiplier * _currentSpeedRatio;
            _speedSamples.Add(currentSpeed);
            
            if (_speedSamples.Count > 300)
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

    #region Trigger Events - FIXED
private void OnTriggerEnter(Collider other)
{
    if (!other.CompareTag("Player")) return;
    
    CharacterController controller;
    if (!_controllerCache.TryGetValue(other, out controller))
    {
        controller = other.GetComponent<CharacterController>();
        _controllerCache[other] = controller;
    }
    
    // FIX: Handle re-entry dopo uscita al terminal
    if (playerJustExited)
    {
        float timeAway = Time.time - playerExitTime;
        
        if (debugActivationSystem)
        {
            Debug.Log($"[RaftPlatform] {gameObject.name}: Player tornato dopo {timeAway:F2}s, era al terminal: {wasAtTerminalBeforeExit}");
        }
        
        playerJustExited = false;
        playerController = controller;
        
        // FIX: Gestione speciale per rientro dopo uscita al terminal
        if (wasAtTerminalBeforeExit)
        {
            // Player aveva lasciato la piattaforma al terminal e ora torna
            playerEnterTime = Time.time;
            
            if (timeAway < settings.terminalGracePeriod)
            {
                // Rientro rapido - applica attesa terminal
                justArrivedAtTerminal = true; // Forza attesa terminal
                playerCanActivateRaft = false; // Deve aspettare
                
                if (debugActivationSystem)
                {
                    Debug.Log($"[RaftPlatform] {gameObject.name}: Rientro rapido da terminal - attesa terminal obbligatoria ({settings.terminalWaitTime}s)");
                }
            }
            else
            {
                // Rientro dopo più tempo - tratta come nuovo boarding
                justArrivedAtTerminal = false;
                playerCanActivateRaft = true;
                
                if (debugActivationSystem)
                {
                    Debug.Log($"[RaftPlatform] {gameObject.name}: Rientro tardivo da terminal - attivazione rapida consentita");
                }
            }
        }
        else
        {
            // Logica esistente per rientri lontano dal terminal
            if (wasMovingBeforeExit && _currentState != RaftState.Moving && timeAway < 2f && !IsAtTerminal())
            {
                // Resume movement if was moving and returns quickly (not at terminal)
                direction = lastDirectionWithPlayer;
                ChangeState(RaftState.Moving);
                playerCanActivateRaft = true;
                
                if (debugActivationSystem)
                {
                    Debug.Log($"[RaftPlatform] {gameObject.name}: Continuo movimento (ritorno da salto)");
                }
            }
            else if (timeAway >= settings.respawnDetectionTime)
            {
                // Likely respawn - reset everything
                ResetActivationTimer();
                playerEnterTime = Time.time;
                playerCanActivateRaft = true;
                justArrivedAtTerminal = false;
                
                if (debugActivationSystem)
                {
                    Debug.Log($"[RaftPlatform] {gameObject.name}: Probabile respawn - attivazione rapida consentita");
                }
            }
            else
            {
                // Normal return
                playerEnterTime = Time.time;
                playerCanActivateRaft = true;
                justArrivedAtTerminal = false;
                
                if (debugActivationSystem)
                {
                    Debug.Log($"[RaftPlatform] {gameObject.name}: Ritorno normale");
                }
            }
        }
        
        return;
    }
    
    // FIX: Prima salita - comportamento normale
    playerController = controller;
    playerWasOnBoard = true;
    playerJustExited = false;
    wasMovingBeforeExit = false;
    wasAtTerminalBeforeExit = false;
    _boardEvents++;
    
    playerEnterTime = Time.time;
    
    // FIX: Se saliamo per la prima volta al terminal, permetti attivazione immediata
    // Se saliamo durante il movimento o lontano dal terminal, permetti attivazione immediata
    playerCanActivateRaft = true;
    justArrivedAtTerminal = false; // Prima salita non richiede attesa terminal
    
    lastKnownPlayerPosition = other.transform.position;
    OnPlayerBoard.Invoke();
    
    if (debugActivationSystem)
    {
        Debug.Log($"[RaftPlatform] {gameObject.name}: Prima salita del player - attivazione immediata consentita");
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
        wasAtTerminalBeforeExit = IsAtTerminal(); // FIX: Track if we were at terminal
        _exitEvents++;
        
        // FIX: Logica specifica per uscita al terminal
        if (IsAtTerminal())
        {
            // Player è uscito al terminal - comportamento normale
            playerController = null; // Disconnetti il player immediatamente
            playerCanActivateRaft = false;
            playerWasOnBoard = false; // FIX: Reset questo flag per evitare falsi respawn
            ResetActivationTimer();
            
            if (debugActivationSystem)
            {
                Debug.Log($"[RaftPlatform] {gameObject.name}: Player uscito normalmente al terminal - piattaforma resta ferma");
            }
        }
        else
        {
            // Player è uscito mentre eravamo in movimento o lontano dal terminal
            playerCanActivateRaft = false; // Disable activation until re-entry is processed
            
            if (debugActivationSystem)
            {
                Debug.Log($"[RaftPlatform] {gameObject.name}: Player uscito lontano dal terminal - Era in movimento: {wasMovingBeforeExit}");
            }
        }
        
        OnPlayerExit.Invoke();
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
        
        _controllerCache.Clear();
    }

    #endregion

    #region Editor Utilities

    #if UNITY_EDITOR
    private void OnValidate()
    {
        if (settings != null)
        {
            settings.speed = Mathf.Max(0.1f, settings.speed);
            settings.accidentalPreventionTime = Mathf.Max(0f, settings.accidentalPreventionTime);
            settings.respawnDetectionTime = Mathf.Max(1f, settings.respawnDetectionTime);
            settings.returnToTerminalRadius = Mathf.Max(1f, settings.returnToTerminalRadius);
            settings.sampleResolution = Mathf.Max(10, settings.sampleResolution);
            
            speed = settings.speed;
            minimumTriggerTime = settings.quickStartTime;
            returnToTerminalRadius = settings.returnToTerminalRadius;
            jumpIgnoreTime = settings.respawnDetectionTime;
            sampleResolution = settings.sampleResolution;
        }
        
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
        
        // State indicator with activation permission
        Gizmos.color = GetStateColor(_currentState);
        string stateText = $"State: {_currentState}";
        if (_currentState == RaftState.Moving || _currentState == RaftState.ReturningToTerminal)
        {
            stateText += $" (Dir: {(direction == 1 ? "→" : "←")})";
        }
        stateText += $"\nCan Activate: {playerCanActivateRaft}";
        stateText += $"\nJust Arrived: {justArrivedAtTerminal}";
        
        UnityEditor.Handles.Label(transform.position + Vector3.up * 9, stateText);
        
        // Activation timer visualization
        if (playerEnterTime > 0f && IsCountingActivation)
        {   
            float requiredTime = justArrivedAtTerminal ? settings.terminalWaitTime : settings.quickStartTime;
            float progress = (Time.time - playerEnterTime) / requiredTime;
            float remaining = requiredTime - (Time.time - playerEnterTime);
            string timerText = $"Timer: {remaining:F1}s ({progress:P0})";
            
            Gizmos.color = Color.Lerp(Color.red, Color.green, progress);
            UnityEditor.Handles.Label(transform.position + Vector3.up * 12, timerText);
        }
    }

    private Color GetStateColor(RaftState state)
    {
        switch (state)
        {
            case RaftState.WaitingAtTerminal: return playerCanActivateRaft ? Color.white : Color.gray;
            case RaftState.CountingActivation: return Color.yellow;
            case RaftState.Moving: return Color.green;
            case RaftState.ReturningToTerminal: return Color.red;
            case RaftState.Disabled: return Color.black;
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