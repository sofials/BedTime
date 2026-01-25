using UnityEngine;
using UnityEngine.Splines;
using UnityEngine.Events;
using System.Collections.Generic;
using System.Linq;
using System.Collections;

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
    [Range(1f, 8f)] public float falseStartDetectionTime = 0.5f;
    [Range(5f, 30f)] public float falseStartReturnRadius = 15f;
    public bool enableAccidentalDetection = true;
    [Range(1f, 10f)] public float accidentalActivationCheckTime = 3f;
    [Range(0.5f, 10f)] public float falseStartDistanceThreshold = 2f; // Distanza minima per considerare un viaggio "reale"
    
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
    private bool playerJustReturnedToTerminal = false;
    
    // FIX: Better terminal state tracking
    private bool justArrivedAtTerminal = false;
    private float terminalArrivalTime = 0f;
    // Aggiungi dopo le altre variabili private
private Rigidbody rb;

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
    private float movementStartDistance = 0f; // Distanza sulla spline quando è partita

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

    // NEW: Track if player must re-board to restart at terminal
    private bool playerMustReboardAtTerminal = false;

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

    // ✅ NUOVO: Setup Rigidbody per movimento fluido
    SetupRigidbody();

    SampleSpline();
    InitializePositions();
    SetupCheckpointIntegration();

    _currentState = RaftState.WaitingAtTerminal;
    justArrivedAtTerminal = false;

    if (debugRespawnSystem)
    {
        Debug.Log($"[RaftPlatform] {gameObject.name} inizializzato correttamente");
    }
    
    // Registra per notifiche respawn
    ThirdPersonController playerController = FindFirstObjectByType<ThirdPersonController>();
    if (playerController != null)
    {
        playerController.OnPlayerRespawned.AddListener((pos, rot) => OnPlayerRespawnedNotification(lastKnownPlayerPosition, pos));
    }
}

// ✅ RIMOSSO: Non usiamo più Rigidbody - movimento diretto come MovingPlatform
private void SetupRigidbody()
{
    // Rimuovi eventuali Rigidbody esistenti per evitare conflitti
    rb = GetComponent<Rigidbody>();
    if (rb != null)
    {
        Debug.Log($"[RaftPlatform] {gameObject.name}: Rigidbody rimosso - usando movimento diretto come MovingPlatform");
        Destroy(rb);
        rb = null;
    }
}
void Update()
{
    // SOLO controlli non-fisici qui
    if (Time.time - _lastValidationTime > VALIDATION_INTERVAL)
    {
        _lastValidationTime = Time.time;
        if (!ValidateRuntimeState())
            return;
    }

    // Logica di stato e rilevamento (non movimento)
    CheckForPlayerRespawn();
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

    // ✅ SEMPRE inizia all'inizio della spline (distanza 0)
    currentDistance = startDistance;
    
    // ✅ Posiziona fisicamente la raft all'inizio della spline
    Vector3 startPosition = GetPositionAtDistance(startDistance);
    transform.position = startPosition;
    
    lastPosition = startPosition;
    lastKnownPlayerPosition = startPosition;

    if (debugRespawnSystem)
    {
        Debug.Log($"[RaftPlatform] {gameObject.name}: Posizionata automaticamente all'inizio della spline (distanza: {currentDistance:F2}m)");
    }
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

    // FIX: Se player è uscito al terminal, NON partire automaticamente
    if (playerJustExited && IsAtTerminal())
    {
        return;
    }

    // ✅ FIX: Chiama ShouldStartMovement() anche se playerCanActivateRaft è false
    // perché ShouldStartMovement() gestisce internamente l'attesa e riabilita playerCanActivateRaft
    if (playerController != null && !playerJustExited)
    {
        if (ShouldStartMovement())
        {
            StartMovement();
        }
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

    // FIX: Se il player è uscito durante il movimento (salto), aspetta che ritorni
    // NON fermare la raft immediatamente - continua il movimento
    if (playerController == null && wasMovingBeforeExit)
    {
        float timeSinceExit = Time.time - playerExitTime;

        // Se il player non ritorna entro jumpIgnoreTime, considera che è caduto/morto
        if (timeSinceExit > jumpIgnoreTime)
        {
            if (debugActivationSystem)
            {
                Debug.Log($"[RaftPlatform] {gameObject.name}: Player non tornato dopo {timeSinceExit:F2}s - considerato caduto, torno al terminal");
            }
            wasMovingBeforeExit = false;
            HandleEarlyExit();
            return;
        }

        // Altrimenti continua il movimento - il player potrebbe essere in aria (salto)
    }
    // FIX: Check per false start (uscita immediata dopo partenza, NON durante movimento normale)
    else if (playerController == null && !wasMovingBeforeExit && !playerJustReturnedToTerminal)
    {
        float timeSinceStart = Time.time - lastMovementStartTime;

        if (timeSinceStart < settings.falseStartDetectionTime)
        {
            if (debugActivationSystem)
            {
                Debug.Log($"[RaftPlatform] {gameObject.name}: FALSA PARTENZA rilevata!");
            }
            HandleFalseStart();
            return;
        }
        else if (timeSinceStart < settings.accidentalActivationCheckTime)
        {
            if (debugActivationSystem)
            {
                Debug.Log($"[RaftPlatform] {gameObject.name}: Player uscito durante movimento dopo {timeSinceStart:F2}s, torno al terminal");
            }
            HandleEarlyExit();
            return;
        }
    }

    if (playerController != null)
    {
        lastDirectionWithPlayer = direction;
        // NON settare wasMovingBeforeExit = true qui - viene settato in OnTriggerExit
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
private void HandleEarlyExit()
{
    Vector3 playerPosition;
    bool foundPlayer = GetPlayerRespawnPosition(out playerPosition);
    
    if (foundPlayer)
    {
        // Sempre vai al terminal più vicino al PLAYER per uscite precoci
        Vector3 startPos = GetPositionAtDistance(startDistance);
        Vector3 endPos = GetPositionAtDistance(endDistance);
        
        float distancePlayerToStart = Vector3.Distance(playerPosition, startPos);
        float distancePlayerToEnd = Vector3.Distance(playerPosition, endPos);
        
        direction = (distancePlayerToStart < distancePlayerToEnd) ? -1 : 1;
        
        // FIX: Settiamo playerJustExited dopo aver deciso
        playerJustExited = true;
        playerExitTime = Time.time;
        playerCanActivateRaft = false;
        
        ChangeState(RaftState.ReturningToTerminal);
        
        if (debugActivationSystem)
        {
            string targetTerminal = (direction == -1) ? "START" : "END";
            Debug.Log($"[RaftPlatform] {gameObject.name}: USCITA PRECOCE - vado al terminal {targetTerminal} (distanze: start={distancePlayerToStart:F1}m, end={distancePlayerToEnd:F1}m)");
        }
    }
    else
    {
        // Fallback: va al terminal più vicino alla zattera
        playerJustExited = true;
        playerExitTime = Time.time;
        playerCanActivateRaft = false;
        
        StartReturningToTerminalNearZattera();
        
        if (debugActivationSystem)
        {
            Debug.Log($"[RaftPlatform] {gameObject.name}: USCITA PRECOCE - player non trovato, vado al terminal più vicino alla zattera");
        }
    }
}

private void HandleFalseStart()
{
    Vector3 playerPosition;
    bool foundPlayer = GetPlayerRespawnPosition(out playerPosition);
    
    if (foundPlayer)
    {
        // Per false start, sempre vai al terminal più vicino al PLAYER
        Vector3 startPos = GetPositionAtDistance(startDistance);
        Vector3 endPos = GetPositionAtDistance(endDistance);
        
        float distancePlayerToStart = Vector3.Distance(playerPosition, startPos);
        float distancePlayerToEnd = Vector3.Distance(playerPosition, endPos);
        
        direction = (distancePlayerToStart < distancePlayerToEnd) ? -1 : 1;
        
        // FIX: Ora settiamo playerJustExited dopo aver deciso la direzione
        playerJustExited = true;
        playerExitTime = Time.time;
        playerCanActivateRaft = false;
        
        ChangeState(RaftState.ReturningToTerminal);
        
        if (debugActivationSystem)
        {
            string targetTerminal = (direction == -1) ? "START" : "END";
            Debug.Log($"[RaftPlatform] {gameObject.name}: FALSA PARTENZA - vado al terminal {targetTerminal} (distanze: start={distancePlayerToStart:F1}m, end={distancePlayerToEnd:F1}m)");
        }
    }
    else
    {
        // Fallback: se il player non è trovato, vai al terminal più vicino alla zattera
        if (debugActivationSystem)
        {
            Debug.Log($"[RaftPlatform] {gameObject.name}: FALSA PARTENZA - player non trovato, vado al terminal più vicino alla zattera");
        }
        
        playerJustExited = true;
        playerExitTime = Time.time;
        playerCanActivateRaft = false;
        
        StartReturningToTerminalNearZattera();
    }
}

    private void HandleReturningToTerminal()
    {
        UpdateMovementSmooth();

        // NUOVO: Controlla SEMPRE la posizione del player durante il ritorno (ogni 0.5 secondi)
        _redirectCheckTimer += Time.fixedDeltaTime;
        if (_redirectCheckTimer >= 0.5f)
        {
            _redirectCheckTimer = 0f;
            CheckAndRedirectToPlayerTerminal();
        }

        if (direction == 1 && currentDistance >= endDistance)
        {
            ReachTerminal(endDistance);
        }
        else if (direction == -1 && currentDistance <= startDistance)
        {
            ReachTerminal(startDistance);
        }
    }

    // Timer per controllare periodicamente la posizione del player
    private float _redirectCheckTimer = 0f;
    private Vector3 _lastKnownPlayerPosForRedirect = Vector3.zero;

    private void CheckAndRedirectToPlayerTerminal()
    {
        // Ottieni posizione ATTUALE del player (non cache vecchia)
        Transform playerTransform = GetCachedPlayerTransform();
        Vector3 playerPos;

        if (playerTransform != null)
        {
            playerPos = playerTransform.position;
        }
        else if (hasCheckpointPosition)
        {
            playerPos = lastKnownCheckpointPosition;
        }
        else
        {
            return; // Non possiamo determinare dove sia il player
        }

        // Se la posizione del player è cambiata significativamente, ricalcola
        if (Vector3.Distance(playerPos, _lastKnownPlayerPosForRedirect) < 1f)
        {
            return; // Player non si è mosso abbastanza
        }

        _lastKnownPlayerPosForRedirect = playerPos;

        Vector3 startPos = GetPositionAtDistance(startDistance);
        Vector3 endPos = GetPositionAtDistance(endDistance);

        float distPlayerToStart = Vector3.Distance(playerPos, startPos);
        float distPlayerToEnd = Vector3.Distance(playerPos, endPos);

        int bestDirection = (distPlayerToStart < distPlayerToEnd) ? -1 : 1;

        // Se stiamo andando al terminal sbagliato, cambia direzione
        if (bestDirection != direction)
        {
            direction = bestDirection;

            if (debugRespawnSystem)
            {
                string newTarget = (direction == -1) ? "START" : "END";
                Debug.Log($"[RaftPlatform] {gameObject.name}: Player rilevato più vicino all'altro terminal - cambio direzione verso {newTarget} (distanze: start={distPlayerToStart:F1}m, end={distPlayerToEnd:F1}m)");
            }
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
                _redirectCheckTimer = 0f; // Reset timer per il prossimo ritorno
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
        if (playerController == null || playerJustExited)
            return false;

        // NEW: Se player deve scendere e risalire al terminal, non può partire
        if (playerMustReboardAtTerminal)
        {
            if (debugActivationSystem)
            {
                Debug.Log($"[RaftPlatform] {gameObject.name}: Player deve scendere e risalire per ripartire");
            }
            return false;
        }

        float timeOnPlatform = Time.time - playerEnterTime;
        float requiredWaitTime;

        // FIX: Better terminal wait logic
        if (justArrivedAtTerminal)
        {
            requiredWaitTime = settings.terminalWaitTime;

            if (timeOnPlatform >= requiredWaitTime)
            {
                justArrivedAtTerminal = false;
                // NON riabilitare automaticamente - player deve scendere e risalire
            }
        }
        else
        {
            requiredWaitTime = settings.quickStartTime;
        }

        // ✅ FIX: Controlla permesso DOPO aver potenzialmente riabilitato
        if (!playerCanActivateRaft)
            return false;

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
        accelerationRate * Time.fixedDeltaTime);
    
    float smoothedSpeed = settings.speed * speedMultiplier * 
        accelerationCurve.Evaluate(_currentSpeedRatio);
    
    currentDistance += smoothedSpeed * direction * Time.fixedDeltaTime;
    
    Vector3 newPosition = GetPositionAtDistance(currentDistance);

    // ✅ FIX: Calcola delta usando lastPosition (come MovingPlatform)
    // Questo evita lo sfasamento causato dall'interpolazione del Rigidbody
    deltaMovement = newPosition - lastPosition;

    if (rb != null)
    {
        rb.MovePosition(newPosition);
    }
    else
    {
        transform.position = newPosition;
    }

    lastPosition = newPosition;
}

private void ReachTerminal(float terminalDistance)
{
    currentDistance = terminalDistance;
    _completedTrips++;

    justArrivedAtTerminal = true;
    terminalArrivalTime = Time.time;

    ChangeState(RaftState.WaitingAtTerminal);

    // FIX: Controlla se player ERA a bordo (non solo se è ancora a bordo ora)
    if (playerController != null || playerWasOnBoard)
    {
        playerCanActivateRaft = false;
        playerMustReboardAtTerminal = true;

        if (debugActivationSystem)
        {
            Debug.Log($"[RaftPlatform] {gameObject.name}: Arrivato al terminal - player deve scendere/risalire");
        }
    }
    else
    {
        ResetActivationTimer();
        playerMustReboardAtTerminal = false;
    }

    wasMovingBeforeExit = false;
    playerWasOnBoard = false; // Reset per prossimo viaggio
}

  private void CheckForAccidentalActivation()
{
    if (!settings.enableAccidentalDetection) return;
    
    if (_currentState != RaftState.Moving) return;
    
    // FIX: Questa logica è ora gestita direttamente in HandleMoving()
    // Manteniamo solo per casi tardivi (dopo accidentalActivationCheckTime)
    float timeSinceStart = Time.time - lastMovementStartTime;
    
    if (timeSinceStart > settings.accidentalActivationCheckTime && timeSinceStart < 10f) // Max 10s di controllo
    {
        if (playerController == null)
        {
            if (debugActivationSystem)
            {
                Debug.Log($"[RaftPlatform] {gameObject.name}: Controllo attivazione accidentale tardiva - torno al terminal");
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
            
            // Disabilita ulteriori controlli per questo viaggio
            settings.enableAccidentalDetection = false;
        }
    }
    else if (timeSinceStart > 10f)
    {
        // Dopo 10 secondi di movimento normale, disabilita il controllo
        settings.enableAccidentalDetection = false;
    }
}
void FixedUpdate()
{
    // TUTTA LA FISICA E IL MOVIMENTO QUI
    HandleStateMachine();
}
    private void StartMovement()
    {
        DetermineMovementDirection();

        lastMovementStartTime = Time.time;
        movementStartDistance = currentDistance; // Salva dove siamo partiti
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
        playerMustReboardAtTerminal = false;
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
    // Controllo più aggressivo per respawn/teleport
    if (playerController == null && playerWasOnBoard)
    {
        Transform currentPlayerTransform = GetCachedPlayerTransform();
        
        if (currentPlayerTransform != null)
        {
            float distanceToPlayer = Vector3.Distance(transform.position, currentPlayerTransform.position);
            float distanceFromLastKnown = Vector3.Distance(currentPlayerTransform.position, lastKnownPlayerPosition);
            
            // Se il player è molto lontano OPPURE si è spostato di colpo dalla sua ultima posizione
            bool playerTeleported = distanceFromLastKnown > 20f; // Teleport/respawn detection
            bool playerVeryFar = distanceToPlayer > settings.returnToTerminalRadius * 0.5f;
            
            if (playerTeleported || playerVeryFar)
            {
                if (_currentState != RaftState.ReturningToTerminal)
                {
                    if (debugRespawnSystem)
                    {
                        string reason = playerTeleported ? "teleport/respawn rilevato" : "player molto lontano";
                        Debug.Log($"[RaftPlatform] {gameObject.name}: {reason} ({distanceToPlayer:F1}m), avvio ritorno automatico");
                    }
                    
                    StartReturningBasedOnMode(currentPlayerTransform.position);
                    playerWasOnBoard = false;
                }
                
                // Aggiorna ultima posizione conosciuta
                lastKnownPlayerPosition = currentPlayerTransform.position;
            }
        }
        else
        {
            // Player transform non trovato - probabile morte/respawn
            if (_currentState != RaftState.ReturningToTerminal && playerWasOnBoard)
            {
                if (debugRespawnSystem)
                {
                    Debug.Log($"[RaftPlatform] {gameObject.name}: Player scomparso, avvio ritorno automatico");
                }
                
                StartReturningToTerminalNearZattera();
                playerWasOnBoard = false;
            }
        }
    }
}
public void OnPlayerRespawnedNotification(Vector3 oldPosition, Vector3 newPosition)
{
    if (debugRespawnSystem)
    {
        Debug.Log($"[RaftPlatform] {gameObject.name}: 💀 RESPAWN RILEVATO - {oldPosition} → {newPosition}");
    }

    // Aggiorna posizione checkpoint IMMEDIATAMENTE
    lastKnownPlayerPosition = newPosition;
    lastKnownCheckpointPosition = newPosition;
    hasCheckpointPosition = true;
    _lastKnownPlayerPosForRedirect = newPosition; // Aggiorna anche per il redirect check

    // Reset stato player
    playerController = null;
    playerWasOnBoard = false;
    playerJustExited = false;
    playerCanActivateRaft = false;
    wasMovingBeforeExit = false;
    ResetActivationTimer();

    // NUOVO: Se stiamo già tornando al terminal, forza un check immediato della direzione
    if (_currentState == RaftState.ReturningToTerminal)
    {
        Vector3 startPos = GetPositionAtDistance(startDistance);
        Vector3 endPos = GetPositionAtDistance(endDistance);

        float distPlayerToStart = Vector3.Distance(newPosition, startPos);
        float distPlayerToEnd = Vector3.Distance(newPosition, endPos);

        int bestDirection = (distPlayerToStart < distPlayerToEnd) ? -1 : 1;

        if (bestDirection != direction)
        {
            direction = bestDirection;
            if (debugRespawnSystem)
            {
                string newTarget = (direction == -1) ? "START" : "END";
                Debug.Log($"[RaftPlatform] {gameObject.name}: 🔄 Player respawnato - cambio direzione verso {newTarget}");
            }
        }
        return; // Già in ritorno, non serve cambiare stato
    }

    // Se stavamo muovendo normalmente, inizia a tornare al terminal più vicino al player
    if (_currentState == RaftState.Moving || _currentState == RaftState.CountingActivation)
    {
        if (debugRespawnSystem)
        {
            Debug.Log($"[RaftPlatform] {gameObject.name}: Player morto durante viaggio, torno al terminal vicino a lui");
        }

        StartReturningBasedOnMode(newPosition);
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
    // Per false start, ignora respawnMode e vai sempre vicino al player
    if (Time.time - lastMovementStartTime < settings.falseStartDetectionTime)
    {
        StartReturningToTerminalNearPlayer(referencePosition);
        return;
    }
    
    // Comportamento normale per altri casi
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
        _redirectCheckTimer = 0f; // Reset timer
        _lastKnownPlayerPosForRedirect = playerPosition;

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
        _redirectCheckTimer = 0f; // Reset timer

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
        _redirectCheckTimer = 0f; // Reset timer
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
        Vector3 oldPosition = lastKnownPlayerPosition;
        lastKnownCheckpointPosition = checkpointPosition;
        lastKnownPlayerPosition = checkpointPosition;
        hasCheckpointPosition = true;

        if (debugRespawnSystem)
        {
            Debug.Log($"[RaftPlatform] {gameObject.name}: Posizione player aggiornata: {oldPosition} → {checkpointPosition}");
        }

        // Se la zattera è in movimento e il player è molto lontano, considera ritorno
        float distanceToPlayer = Vector3.Distance(transform.position, checkpointPosition);
        if (distanceToPlayer > settings.returnToTerminalRadius && _currentState == RaftState.Moving)
        {
            if (debugRespawnSystem)
            {
                Debug.Log($"[RaftPlatform] {gameObject.name}: Player aggiornato lontano durante movimento, valuto ritorno");
            }

            // Aspetta un frame per non interrompere movimento valido
            StartCoroutine(DelayedReturnCheck(checkpointPosition));
        }
    }
private IEnumerator DelayedReturnCheck(Vector3 playerPosition)
{
    yield return new WaitForSeconds(0.5f);
    
    if (playerController == null && 
        _currentState == RaftState.Moving && 
        !playerJustExited)
    {
        // ✅ FIX: Rinomina per evitare conflitto con il campo 'currentDistance'
        float distanceToPlayer = Vector3.Distance(transform.position, playerPosition);
        if (distanceToPlayer > settings.returnToTerminalRadius)
        {
            if (debugRespawnSystem)
            {
                Debug.Log($"[RaftPlatform] {gameObject.name}: Confermato player lontano ({distanceToPlayer:F1}m), avvio ritorno");
            }
            
            StartReturningBasedOnMode(playerPosition);
        }
        else if (debugRespawnSystem)
        {
            Debug.Log($"[RaftPlatform] {gameObject.name}: Player ora abbastanza vicino ({distanceToPlayer:F1}m), continuo movimento normale");
        }
    }
    else if (debugRespawnSystem && playerController != null)
    {
        Debug.Log($"[RaftPlatform] {gameObject.name}: Player è tornato sulla zattera, annullo controllo ritorno");
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
    
    // ✅ FIX: USA MovePosition se Rigidbody disponibile
    if (rb != null)
    {
        rb.MovePosition(startPosition);
    }
    else
    {
        transform.position = startPosition;
    }
    
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
       ThirdPersonController tpc = other.GetComponent<ThirdPersonController>();
    if (tpc != null)
    {
         StartCoroutine(ForceResetJumpAfterDelay(tpc, 0.05f));
    }

    // FIX: Se la raft sta già muovendo e il player ritorna (da un salto), riconnetti semplicemente
    if (_currentState == RaftState.Moving && wasMovingBeforeExit)
    {
        playerController = controller;
        wasMovingBeforeExit = false;
        lastKnownPlayerPosition = other.transform.position;

        if (debugActivationSystem)
        {
            Debug.Log($"[RaftPlatform] {gameObject.name}: Player ritornato da salto durante movimento - continuo normalmente");
        }

        OnPlayerBoard.Invoke();
        return;
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
        if (wasAtTerminalBeforeExit && IsAtTerminal())
        {
            // Player era sceso al terminal e ora risale - può ripartire!
            playerEnterTime = Time.time;
            justArrivedAtTerminal = false;
            playerCanActivateRaft = true;
            playerMustReboardAtTerminal = false; // Ha fatto il reboard richiesto

            if (debugActivationSystem)
            {
                Debug.Log($"[RaftPlatform] {gameObject.name}: Player risalito al terminal dopo essere sceso - può ripartire!");
            }
        }
        else if (wasMovingBeforeExit && timeAway < 2f && !IsAtTerminal())
        {
            // Ritorno da salto mentre era in movimento (non al terminal)
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
            playerMustReboardAtTerminal = false;

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

        lastKnownPlayerPosition = other.transform.position;
        OnPlayerBoard.Invoke();
        return;
    }

    // FIX: Prima salita - comportamento normale
    playerController = controller;
    playerWasOnBoard = true;
    playerJustExited = false;
    wasMovingBeforeExit = false;
    wasAtTerminalBeforeExit = false;
    playerMustReboardAtTerminal = false;
    _boardEvents++;

    playerEnterTime = Time.time;

    // FIX: Se saliamo per la prima volta al terminal, permetti attivazione immediata
    playerCanActivateRaft = true;
    justArrivedAtTerminal = false; // Prima salita non richiede attesa terminal

    lastKnownPlayerPosition = other.transform.position;
    OnPlayerBoard.Invoke();

    if (debugActivationSystem)
    {
        Debug.Log($"[RaftPlatform] {gameObject.name}: Prima salita del player - attivazione immediata consentita");
    }
}
private void OnTriggerStay(Collider other)
{
    if (!other.CompareTag("Player")) return;
    
    // Reset continuo del jump count mentre il player è sulla raft
    ThirdPersonController tpc = other.GetComponent<ThirdPersonController>();
    if (tpc != null && playerController != null) // Solo se il player è "registrato"
    {
          if (tpc.IsGrounded())
        {
            tpc.ForceResetJumpCount();
        }
    }
}
private IEnumerator ForceResetJumpAfterDelay(ThirdPersonController tpc, float delay)
{
    yield return new WaitForSeconds(delay);
    
    if (tpc != null && playerController != null)
    {
        // Forza reset quando il player è stabilizzato sulla raft
        if (tpc.IsGrounded())
        {
            tpc.ForceResetJumpCount();
            
            if (debugActivationSystem)
            {
                Debug.Log($"[RaftPlatform] {gameObject.name}: Jump count resettato dopo delay");
            }
        }
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
        _exitEvents++;

        // FIX: Se siamo in movimento, controlla se è una falsa partenza o un salto
        if (_currentState == RaftState.Moving)
        {
            float distanceTraveled = Mathf.Abs(currentDistance - movementStartDistance);

            if (distanceTraveled < settings.falseStartDistanceThreshold)
            {
                // FALSA PARTENZA
                wasMovingBeforeExit = false;
                wasAtTerminalBeforeExit = false;
                playerController = null;
                playerWasOnBoard = false; // Reset subito per false start

                if (debugActivationSystem)
                {
                    Debug.Log($"[RaftPlatform] {gameObject.name}: FALSA PARTENZA - {distanceTraveled:F2}m");
                }
            }
            else
            {
                // SALTO durante viaggio - la raft continua
                wasMovingBeforeExit = true;
                wasAtTerminalBeforeExit = false;
                playerController = null; // Player non più a bordo
                // NON resettare playerWasOnBoard - serve per ReachTerminal

                if (debugActivationSystem)
                {
                    Debug.Log($"[RaftPlatform] {gameObject.name}: Player saltato dopo {distanceTraveled:F2}m - raft continua");
                }
            }
        }
        else if (IsAtTerminal())
        {
            // Player sceso al terminal
            playerController = null;
            playerJustExited = true;
            playerCanActivateRaft = false;
            wasAtTerminalBeforeExit = true;
            playerMustReboardAtTerminal = false;
            playerWasOnBoard = false; // Reset - non più in viaggio
            ResetActivationTimer();

            if (debugActivationSystem)
            {
                Debug.Log($"[RaftPlatform] {gameObject.name}: Player sceso al terminal");
            }
        }
        else
        {
            // Altri casi
            playerController = null;
            playerJustExited = true;
            playerCanActivateRaft = false;
            // NON resettare playerWasOnBoard
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
        // Deregistra dalle notifiche respawn
ThirdPersonController playerController = FindFirstObjectByType<ThirdPersonController>();
if (playerController != null)
{
    playerController.OnPlayerRespawned.RemoveAllListeners();
}
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