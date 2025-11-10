using UnityEngine;
using UnityEngine.InputSystem;
using CartoonFX;
using System.Collections;
using Unity.Cinemachine;
using UnityEngine.Events;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;

#endif
[System.Serializable]
public class CameraMovementSettings
{
    [Header("Camera Reference")]
    public CinemachineCamera cinemachineCamera;
    public Camera unityCamera;
    
    [Header("Movement Settings")]
    public bool invertForwardBackward = false;
    public bool invertLeftRight = false;
    
    [Header("Identification")]
    public string cameraName = "";
    
    public CameraMovementSettings()
    {
        invertForwardBackward = false;
        invertLeftRight = false;
        cameraName = "New Camera";
    }
}
[RequireComponent(typeof(CharacterController))]
public class ThirdPersonController : MonoBehaviour
{
    [Header("Camera Reset")]
    public CinemachineCamera playerCamera;
  
    [Header("UI Effect")]
    public PlayerUI playerUI;
    public UIEffectHandler attackEffectUI;
    private float lastCollisionLogTime = 0f;
private const float COLLISION_LOG_THROTTLE = 0.1f; // Only log every 0.1 seconds

    [Header("Movement Settings")]
    public float walkSpeed = 2f;
    public float runSpeed = 5f;
    public float sprintSpeed = 8f;
    public float rotationSmoothTime = 0.1f;
    private float rotationVelocity;
    private float smoothInputMagnitude;
    private int unstableFrames = 0;
private bool lastGroundCheckResult = false;
    [Header("Ground Stability")]
[SerializeField] private int groundStabilityFrames = 3; // Numero di frame da mantenere grounded
[SerializeField] private bool debugGroundStability = false;
    [Header("Platform Smoothing")]
[SerializeField] private float platformVerticalSmoothing = 2f; // Regolabile nell'inspector
[SerializeField] private float platformVerticalThreshold = 0.05f; // Soglia minima per movimento verticale
// ✅ AGGIUNGI QUESTI NUOVI PARAMETRI PER CONTROLLO FINE
[Header("Platform Vertical Control")]
[SerializeField] private float platformVerticalMultiplier = 1f; // ← NUOVO: Riduci da 0.3 a 0.15 (solo 15% del movimento)
[SerializeField] private float platformVerticalMaxSpeed = 25f; // ← NUOVO: Velocità massima consentita
[SerializeField] private bool useVerticalDeadZone = true; // ← NUOVO: Abilita zona morta
    [SerializeField] private float verticalDeadZone = 0.005f; // ← NUOVO: Zona morta per movimenti piccoli
[SerializeField] private bool disableVerticalFollowing = false; // NUOVO: Disabilita completamente

    // ✅ NUOVO SISTEMA DI GESTIONE CAMERA DINAMICA CON INTEGRAZIONE CAMERAMANAGER
    [Header("Camera Management")]
    [SerializeField] private bool autoDetectActiveCamera = true;
    [SerializeField] private float cameraCheckInterval = 0.1f;
    [SerializeField] private bool useCameraManagerIntegration = true;
    [SerializeField] private bool debugCameraChanges = false;
    private float cameraCheckTimer = 0f;
    private Camera currentActiveCamera;
    [Header("Camera Movement Inversion")]
[SerializeField] private bool enableMovementInversion = true;
[SerializeField] private List<CameraMovementSettings> cameraSettings = new List<CameraMovementSettings>();
[SerializeField] private bool debugMovementInversion = false;
private CameraMovementSettings currentCameraSettings = null;
private bool movementInverted = false;
    
    // ✅ EVENT SYSTEM PER NOTIFICHE DI CAMBIO CAMERA
    public System.Action<Camera, Camera> OnCameraChanged;

    [Header("Jump Settings")]
    public float jumpHeight = 4f;
    public float gravity = -9.81f;
    public int maxJumps = 10;
    private int jumpCount = 0;
    private Vector3 velocity;
    private bool isJumpEnabled = true; 
    [Header("Advanced Jump Timing")]
public float jumpBufferTime = 0.3f;
public float coyoteTime = 0.2f;
    [Header("Ledge Grab Settings")]
public bool ledgeGrabEnabled = true;
public float ledgeDetectionDistance = 1f;
public LayerMask ledgeLayerMask = 1; // Assegna il layer "Ground" o crea uno specifico per i ledge
    public float hangDelayAfterJump = 0.25f;
[SerializeField] private float ledgeGrabCooldown = 0.5f; // Cooldown per prevenire grab ripetuti
private float lastLedgeGrabTime = 0f; // Ultimo tempo di rilascio dal ledge

[Header("Ledge Grab Position")]
[SerializeField] private float hangHeightOffset = -0.4f; // ← REGOLA QUI L'ALTEZZA! Negativo = più in basso
[SerializeField] private float hangDistanceFromWall = 0.15f; // Distanza dal muro
[SerializeField] private float hangStabilizationForce = 15f; // Forza per mantenere posizione
[SerializeField] private float hangPositionTolerance = 0.1f; // Tolleranza per considerare "in posizione"

[Header("Ledge Grab Debug")]
public bool debugLedgeGrab = false;
    
    private float coyoteTimeCounter = 0f;
    private float jumpBufferCounter = 0f;

    [Header("Falling Settings")]
    public float fallingTimeThreshold = 1.0f;
    private float fallingTimer = 0f;
// AGGIUNGI QUESTE NUOVE VARIABILI:
[Header("Jump Debug")]
[SerializeField] private bool debugJumpInBuild = false;
private float lastJumpAttemptTime = 0f;
private int jumpAttemptCount = 0;

    [Header("Air Control Settings")]
    public float airControlStrength = 0.5f;
    public float airControlSpeed = 2f;
    public float airRotationSmoothTime = 0.3f;
   [Header("Climb Movement")]
[SerializeField] private float climbHeightBoost = 0.2f; // Altezza da salire durante climbing
[SerializeField] private float climbBoostSpeed = 2f; // Velocità della salita
[SerializeField] private AnimationCurve climbHeightCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f); // Curva di salita
private Coroutine climbHeightCoroutine;

    [Header("Player Stats")]
    public float maxHealth = 300f;
    public float currentHealth;
    [Header("Health Protection")]
    [SerializeField] private bool protectMaxHealth = true;
    [SerializeField] private float designatedMaxHealth = 300f;

    [Header("Footstep Audio")]
    [SerializeField] private AudioSource footstepAudioSource;
    // ✅ NUOVO: Configurazione Terrain Paint Texture Detection
[Header("Terrain Texture Detection")]
[SerializeField] private string terrainLayerName = "SquareVillage"; // Layer del terrain da controllare
[SerializeField] private int[] grassTextureIndices = { 0 }; // ← Indici delle Paint Texture erba (es: 0, 2, 4)
[SerializeField] private int[] groundTextureIndices = { 1 }; // ← Indici delle Paint Texture terreno (es: 1, 3, 5)
[SerializeField] private bool debugTerrainTexture = false; // ← Debug per vedere indici in console
 // Set audio per ERBA (Layer: Grass) - 3 suoni casuali
[Header("Grass Footsteps")]
[SerializeField] private AudioClip[] grassFootsteps; // ← SINGOLO ARRAY

// Set audio per TERRENO (Layer: Ground) - 3 suoni casuali
[Header("Ground Footsteps")]
[SerializeField] private AudioClip[] groundFootsteps; // ← SINGOLO ARRAY
    [SerializeField] private float walkStepInterval = 0.5f;
    [SerializeField] private float runStepInterval = 0.35f;
    [SerializeField] private float sprintStepInterval = 0.25f;
    [SerializeField] private float footstepVolumeWalk = 0.5f;
    [SerializeField] private float footstepVolumeRun = 0.5f;
    [SerializeField] private float footstepVolumeSprint = 0.5f;
    [SerializeField] private float pitchVariation = 0.1f;
    private float platformGripTimer = 0f;
private const float PLATFORM_GRIP_TIME = 0.15f; // Mantieni attacco per 150ms dopo perdita rilevamento
    [Header("Jump Audio")]
[SerializeField] private AudioSource jumpAudioSource;
[SerializeField] private AudioClip[] jumpSounds;
[SerializeField] private float jumpVolume = 0.3f;
[SerializeField] private float jumpPitchVariation = 0f;
    [SerializeField] private bool useRandomJumpSound = true;
[Header("Hit Audio")]
[SerializeField] private AudioSource hitAudioSource;
[SerializeField] private AudioClip[] hitSounds;
[SerializeField] private float hitVolume = 0.8f;
[SerializeField] private float hitPitchVariation = 0.15f;
[SerializeField] private bool useRandomHitSound = true;
    
    private float footstepTimer = 0f;
    private bool wasMovingLastFrame = false;

    private CharacterController controller;
    private Animator _animator;

    [Header("References")]
    public Transform cameraTransform;
    [Header("Respawn Events")]
    public UnityEvent<Vector3, Quaternion> OnPlayerRespawned; // ✅ AGGIUNTO QUATERNION MANCANTE
private Transform _cachedPlayerTransform;
private float _lastPlayerCacheTime;
private Vector3 lastKnownPlayerPosition;
private float lastPlayerPositionUpdateTime;
private bool debugRespawnSystem = false; // ✅ AGGIUNGI QUESTO FLAG

    // OTTIMIZZAZIONE: Cache per evitare GetComponent ripetuti
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int JumpHash = Animator.StringToHash("Jump");
    private static readonly int DoubleJumpHash = Animator.StringToHash("DoubleJump");
    private static readonly int IsFallingHash = Animator.StringToHash("isFalling");
    private static readonly int IsGroundedHash = Animator.StringToHash("isGrounded");
    private static readonly int HitHash = Animator.StringToHash("Hit");
    private static readonly int HitRealHash = Animator.StringToHash("HitReal");
    private static readonly int VerticalVelocityHash = Animator.StringToHash("VerticalVelocity");
    private static readonly int HangingHash = Animator.StringToHash("Hanging");
private static readonly int ClimbHash = Animator.StringToHash("Climb");

    private bool wasGroundedLastFrame;

    // ✅ SISTEMA PIATTAFORME OTTIMIZZATO
    [Header("Platform Movement System")]
    [SerializeField] private LayerMask platformLayers = -1;
    [SerializeField] private float platformDetectionRadius = 0.6f;
    [SerializeField] private float maxPlatformHeight = 2f;
    [SerializeField] private bool debugPlatformMovement = false;
    private Vector3 lastFramePlatformPosition = Vector3.zero;
private bool platformMovementStabilized = false;
    
    private Transform currentPlatform = null;
    private Vector3 lastPlatformPosition = Vector3.zero;
    private Quaternion lastPlatformRotation = Quaternion.identity;
    private Vector3 platformDeltaPosition = Vector3.zero;
    private Quaternion platformDeltaRotation = Quaternion.identity;
    private Vector3 localPositionOnPlatform = Vector3.zero;
    
    // Cache per il tipo di piattaforma
    private MovingPlatform currentMovingPlatform = null;
    private RaftPlatform currentRaftPlatform = null;
    private RotatingObject currentRotatingObject = null;
    private bool isOnObstaclePlatform = false;

    private Vector3 playerVelocity;
    private Vector3 externalPush = Vector3.zero;
    [SerializeField] private float pushRecoverySpeed = 0.2f;

    private Vector3 attackVelocity = Vector3.zero;
    [SerializeField] private float attackVelocityDecay = 8f;

    private PlayerControls controls;
    private Vector2 moveInput;
    private bool jumpInput;
    private bool isSprinting;
    private bool isHoldingJump;
    private bool hanging = false;

    private bool canMoveAfterHang = true;
private float lastJumpTime = 0f;
private Vector3 hangPosition;
private Vector3 hangForward;
    private Vector3 lastValidHangPosition; // Backup dell'ultima posizione valida
private float hangStabilityTimer = 0f; // Timer per stabilizzazione
private const float HANG_STABILITY_TIME = 0.1f; // Tempo minimo prima di validare hang
    private bool isHangPositionStable = false;
private bool isClimbing = false;



    [Header("Sprint Effect (assign CFXR_EffectController)")]
    public CFXR_EffectController sprintFX;
    private bool sprintFXActive = false;

    private bool isMovementLocked = false;
    public bool IsMovementLocked
    {
        get => isMovementLocked;
        set
        {
            isMovementLocked = value;
            if (isMovementLocked)
            {
                moveInput = Vector2.zero;
                playerVelocity = Vector3.zero;
                attackVelocity = Vector3.zero;
                _animator.SetFloat(SpeedHash, 0f);
                StopFootstepAudio();
            }
        }
    }
    
    public float MaxHealth 
    { 
        get => protectMaxHealth ? designatedMaxHealth : maxHealth;
        set 
        {
            if (protectMaxHealth)
            {
                return;
            }
            maxHealth = value;
            designatedMaxHealth = value;
        }
    }

    public float CurrentHealth 
    { 
        get => currentHealth; 
        set => currentHealth = value;
    }

    // OTTIMIZZAZIONE: Cache per raycast
    private RaycastHit[] raycastHits = new RaycastHit[8];
    private Vector3 tempVector3;

    public bool IsGrounded() => controller.isGrounded;

    public void AddAttackVelocity(Vector3 velocity)
    {
        attackVelocity += velocity;
    }

    // ✅ NUOVI METODI PER GESTIONE CAMERA DINAMICA CON INTEGRAZIONE CAMERAMANAGER
    
    /// <summary>
    /// Forza l'uso di una camera specifica per il movimento
    /// </summary>
    /// <param name="camera">La camera da utilizzare (null per auto-detect)</param>
    public void SetActiveCamera(Camera camera)
{
    if (camera != null)
    {
        currentActiveCamera = camera;
        cameraTransform = camera.transform;
        autoDetectActiveCamera = false;
        
        // ✅ FIX: Aggiorna camera settings quando si imposta manualmente la camera
        UpdateCurrentCameraSettings();
        
        if (debugCameraChanges)
        {
            Debug.Log($"[ThirdPersonController] 🎮 Camera manualmente impostata: {camera.name}");
            if (currentCameraSettings != null)
            {
                Debug.Log($"[ThirdPersonController] Settings applicate: {currentCameraSettings.cameraName}");
            }
        }
    }
    else
    {
        autoDetectActiveCamera = true;
        if (debugCameraChanges)
            Debug.Log("[ThirdPersonController] 🔄 Ripristinato auto-detect camera");
    }
}
    private void UpdatePlatformGrip()
{
    if (currentPlatform != null)
    {
        // Reset timer se la piattaforma è ancora valida
        if (IsPlatformValidLoose())
        {
            platformGripTimer = 0f;
        }
        else
        {
            // Inizia il countdown del grip
            platformGripTimer += Time.deltaTime;
            
            if (platformGripTimer >= PLATFORM_GRIP_TIME)
            {
                if (debugPlatformMovement)
                    Debug.Log($"[Platform] Grip scaduto per {currentPlatform.name} dopo {platformGripTimer:F2}s");
                DetachFromCurrentPlatform();
                platformGripTimer = 0f;
            }
            else if (debugPlatformMovement && Time.frameCount % 30 == 0)
            {
                Debug.Log($"[Platform] Grip attivo: {(PLATFORM_GRIP_TIME - platformGripTimer):F2}s rimanenti");
            }
        }
    }
    else
    {
        platformGripTimer = 0f;
    }
}

   
public void SetActiveCinemachineCamera(CinemachineCamera cinemachineCamera)
{
    if (cinemachineCamera != null && useCameraManagerIntegration)
    {
        CameraManager cameraManager = FindFirstObjectByType<CameraManager>();
        if (cameraManager != null)
        {
            cameraManager.SwitchCamera(cinemachineCamera);
        }
        
        if (autoDetectActiveCamera)
        {
            DetectActiveCamera();
            // ✅ FIX: Aggiorna camera settings dopo cambio camera
            UpdateCurrentCameraSettings();
        }
        
        if (debugCameraChanges)
        {
            Debug.Log($"[ThirdPersonController] CinemachineCamera attivata: {cinemachineCamera.name}");
            if (currentCameraSettings != null)
            {
                Debug.Log($"[ThirdPersonController] Settings applicate: {currentCameraSettings.cameraName}");
            }
        }
    }
}
public void SetActiveCinemachineCameraByName(string cameraName)
{
    if (useCameraManagerIntegration)
    {
        CameraManager cameraManager = FindFirstObjectByType<CameraManager>();
        if (cameraManager != null)
        {
            cameraManager.SwitchCameraByName(cameraName);
        }
        
        if (autoDetectActiveCamera)
        {
            DetectActiveCamera();
            // ✅ FIX: Aggiorna camera settings dopo cambio camera
            UpdateCurrentCameraSettings();
        }
        
        if (debugCameraChanges)
        {
            Debug.Log($"[ThirdPersonController] CinemachineCamera attivata via nome: {cameraName}");
            if (currentCameraSettings != null)
            {
                Debug.Log($"[ThirdPersonController] Settings applicate: {currentCameraSettings.cameraName}");
            }
        }
    }
}
    /// <summary>
    /// Ottieni la camera attualmente utilizzata per il movimento
    /// </summary>
    /// <returns>La camera attiva</returns>
    public Camera GetActiveCamera()
    {
        return currentActiveCamera;
    }

   public CinemachineCamera GetActiveCinemachineCamera()
{
    if (useCameraManagerIntegration)
    {
        CameraManager cameraManager = FindFirstObjectByType<CameraManager>();
        return cameraManager != null ? cameraManager.GetActiveCamera() : null;
    }
    return null;
}

    /// <summary>
    /// Abilita/disabilita il rilevamento automatico della camera
    /// </summary>
    /// <param name="enabled">True per abilitare l'auto-detect</param>
    public void SetAutoDetectCamera(bool enabled)
    {
        autoDetectActiveCamera = enabled;
        if (debugCameraChanges)
        {
            Debug.Log($"[ThirdPersonController] Auto-detect camera {(enabled ? "abilitato" : "disabilitato")}");
        }
    }

    /// <summary>
    /// Abilita/disabilita l'integrazione con CameraManager
    /// </summary>
    /// <param name="enabled">True per abilitare l'integrazione</param>
    public void SetCameraManagerIntegration(bool enabled)
    {
        useCameraManagerIntegration = enabled;
        if (debugCameraChanges)
        {
            Debug.Log($"[ThirdPersonController] CameraManager integration {(enabled ? "abilitata" : "disabilitata")}");
        }
    }

public string GetActiveCameraInfo()
{
    if (currentActiveCamera == null) return "Nessuna camera attiva";
    
    string info = $"Camera: {currentActiveCamera.name}";
    
    CameraManager cameraManager = FindFirstObjectByType<CameraManager>();
    if (useCameraManagerIntegration && cameraManager != null && cameraManager.GetActiveCamera() != null)
    {
        info += $"\nCinemachine: {cameraManager.GetActiveCamera().name}";
        info += $"\nPriorità: {cameraManager.GetActiveCamera().Priority}";
    }
    
    info += $"\nAuto-detect: {autoDetectActiveCamera}";
    info += $"\nSimpleCameraManager: {useCameraManagerIntegration}";
    
    return info;
}

private void DetectActiveCamera()
{
    Camera newActiveCamera = null;
    string detectionMethod = "";
    
    // 1. PRIORITÀ ASSOLUTA: CameraManager (se disponibile e configurato)
    if (useCameraManagerIntegration)
    {
        CameraManager cameraManager = FindFirstObjectByType<CameraManager>();
        if (cameraManager != null && cameraManager.IsCameraSystemReady() && cameraManager.GetActiveCamera() != null)
        {
            // Cerca il CinemachineBrain nella scena corrente
            CinemachineBrain brain = null;
            CinemachineBrain[] allBrains = FindObjectsByType<CinemachineBrain>(FindObjectsSortMode.None);
            
            // Trova il brain nella scena corrente e che sia attivo
            foreach (var b in allBrains)
            {
                if (b.isActiveAndEnabled && b.OutputCamera != null && b.OutputCamera.isActiveAndEnabled)
                {
                    brain = b;
                    break;
                }
            }
            
            if (brain != null && brain.OutputCamera != null)
            {
                newActiveCamera = brain.OutputCamera;
                detectionMethod = $"CameraManager→{cameraManager.GetActiveCamera().name}→Brain→{newActiveCamera.name}";
            }
        }
    }
    
    // 2. FALLBACK: Camera.main (solo se è valida e attiva)
    if (newActiveCamera == null)
    {
        if (Camera.main != null && Camera.main.isActiveAndEnabled)
        {
            newActiveCamera = Camera.main;
            detectionMethod = "Camera.main";
        }
    }
    
    // 3. FALLBACK: Prima CinemachineBrain attiva
    if (newActiveCamera == null)
    {
        CinemachineBrain[] brains = FindObjectsByType<CinemachineBrain>(FindObjectsSortMode.None);
        foreach (var brain in brains)
        {
            if (brain.isActiveAndEnabled && brain.OutputCamera != null && brain.OutputCamera.isActiveAndEnabled)
            {
                newActiveCamera = brain.OutputCamera;
                detectionMethod = $"FirstActiveBrain→{newActiveCamera.name}";
                break;
            }
        }
    }
    
    // 4. FALLBACK FINALE: Prima camera attiva trovata
    if (newActiveCamera == null)
    {
        Camera[] allCameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
        foreach (var cam in allCameras)
        {
            if (cam.isActiveAndEnabled)
            {
                newActiveCamera = cam;
                detectionMethod = $"FirstActiveCamera→{cam.name}";
                break;
            }
        }
    }
    
    // 5. AGGIORNA SOLO SE CAMERA È DIVERSA E VALIDA
   if (newActiveCamera != currentActiveCamera)
{
    if (newActiveCamera != null)
    {
        Camera previousCamera = currentActiveCamera;
        
        currentActiveCamera = newActiveCamera;
                cameraTransform = newActiveCamera.transform;
             rotationVelocity = 0f;
        
        if (debugCameraChanges)
        {
            Debug.Log($"[ThirdPersonController] 🎮 Camera cambiata: {(previousCamera ? previousCamera.name : "null")} → {newActiveCamera.name} via {detectionMethod}");
            Debug.Log($"[ThirdPersonController] ✅ cameraTransform aggiornato: {cameraTransform.name}");
        }
        
        // ✅ FIX: Invoca evento PRIMA di aggiornare settings
        OnCameraChanged?.Invoke(previousCamera, newActiveCamera);

                // ✅ FIX: IMPORTANTE - Aggiorna settings DOPO aver cambiato camera
                // Questo garantisce che le settings siano sincronizzate con la nuova camera
                UpdateCurrentCameraSettings();
             ForceRecalculateMovementDirection();
        
        if (debugCameraChanges && currentCameraSettings != null)
        {
            Debug.Log($"[ThirdPersonController] 🎯 Camera settings aggiornate automaticamente: {currentCameraSettings.cameraName}");
        }
    }
    else if (debugCameraChanges)
    {
        Debug.LogError("[ThirdPersonController] ❌ NESSUNA CAMERA VALIDA TROVATA! Movimento bloccato.");
    }
}
}
/// <summary>
/// Forza il ricalcolo immediato della direzione di movimento al cambio camera
/// </summary>
private void ForceRecalculateMovementDirection()
{
    if (moveInput.magnitude < 0.1f) return;
    
    // Ricalcola immediatamente l'angolo target con la nuova camera
    Vector2 processedInput = GetProcessedMoveInput();
    Vector3 inputVector = new Vector3(processedInput.x, 0f, processedInput.y);
    
    if (cameraTransform != null)
    {
        float targetAngle = Mathf.Atan2(inputVector.x, inputVector.z) * Mathf.Rad2Deg + cameraTransform.eulerAngles.y;
        
        // ✅ APPLICA IMMEDIATAMENTE la rotazione per evitare transizioni strane
        transform.rotation = Quaternion.Euler(0f, targetAngle, 0f);
        
        if (debugCameraChanges)
        {
            Debug.Log($"[Movement] Direzione ricalcolata: {targetAngle:F1}° (Camera Y: {cameraTransform.eulerAngles.y:F1}°)");
        }
    }
}
public void ForceUpdateActiveCamera()
{
    if (autoDetectActiveCamera)
    {
        DetectActiveCamera();
        
        // ✅ FIX: Aggiorna anche le camera settings per inversione movimento
        UpdateCurrentCameraSettings();
        
        if (debugCameraChanges)
        {
            Debug.Log($"[ThirdPersonController] ForceUpdate: Camera attiva ora è {(currentActiveCamera ? currentActiveCamera.name : "null")}");
            
            // ✅ FIX: Debug info sulle camera settings
            if (currentCameraSettings != null)
            {
                Debug.Log($"[ThirdPersonController] Camera Settings: {currentCameraSettings.cameraName} " +
                         $"(FB Invert: {currentCameraSettings.invertForwardBackward}, " +
                         $"LR Invert: {currentCameraSettings.invertLeftRight})");
            }
            else
            {
                Debug.Log("[ThirdPersonController] Nessuna camera settings trovata per questa camera");
            }
        }
    }
    else if (debugCameraChanges)
    {
        Debug.Log("[ThirdPersonController] ForceUpdate richiesto ma auto-detect è disabilitato");
    }
}

    /// <summary>
    /// Callback statico per notificare tutti i controller del cambio camera
    /// Da chiamare quando CameraManager cambia camera
    /// </summary>
    public static void NotifyAllControllersOfCameraChange()
    {
        ThirdPersonController[] controllers = FindObjectsByType<ThirdPersonController>(FindObjectsSortMode.None);
        foreach (var controller in controllers)
        {
            if (controller.autoDetectActiveCamera)
            {
                controller.DetectActiveCamera();
            }
        }
    }

    private void Awake()
    {
        controls = new PlayerControls();
        
        // ✅ INIZIALIZZAZIONE CAMERA MIGLIORATA
        InitializeCamera();

        controls.Gameplay.Move.performed += OnMovePerformed;
        controls.Gameplay.Move.canceled += OnMoveCanceled;
        controls.Gameplay.Sprint.performed += OnSprintPerformed;
        controls.Gameplay.Sprint.canceled += OnSprintCanceled;
        controls.Gameplay.Jump.started += OnJumpStarted;
        controls.Gameplay.Jump.canceled += OnJumpCanceled;
    }

    /// <summary>
    /// Inizializza il sistema di camera con integrazione CameraManager
    /// </summary>
    private void InitializeCamera()
    {
        // Se cameraTransform è già assegnato manualmente, usalo
        if (cameraTransform != null)
        {
            currentActiveCamera = cameraTransform.GetComponent<Camera>();
            autoDetectActiveCamera = false;
            
            if (debugCameraChanges)
                Debug.Log($"[ThirdPersonController] 📹 Camera preassegnata: {cameraTransform.name}");
            return;
        }
        
        // ✅ INTEGRAZIONE CAMERAMANAGER
        if (useCameraManagerIntegration)
        {
            // Registra per gli eventi di cambio camera (se CameraManager lo supporta)
            // Al momento CameraManager non ha eventi, ma potremmo aggiungere questa funzionalità
            
            if (debugCameraChanges)
                Debug.Log("[ThirdPersonController] 🎬 CameraManager integration abilitata");
        }
        
        // Altrimenti, attiva l'auto-detect
        autoDetectActiveCamera = true;
        DetectActiveCamera();
    }

    private void OnMovePerformed(InputAction.CallbackContext ctx) => moveInput = ctx.ReadValue<Vector2>();
    private void OnMoveCanceled(InputAction.CallbackContext ctx) => moveInput = Vector2.zero;
    private void OnSprintPerformed(InputAction.CallbackContext ctx) => isSprinting = true;
    private void OnSprintCanceled(InputAction.CallbackContext ctx) => isSprinting = false;

    private void OnJumpStarted(InputAction.CallbackContext ctx)
{
    if (!isJumpEnabled || IsMovementLocked) return;

    // Debug per build problematiche
    if (debugJumpInBuild)
    {
        jumpAttemptCount++;
        lastJumpAttemptTime = Time.time;
        string deviceInfo = $"Device: {SystemInfo.deviceModel} | FPS: {(1f/Time.deltaTime):F1} | " +
                           $"Ground: {controller.isGrounded} | Count: {jumpCount}/{maxJumps}";
        Debug.Log($"[Jump #{jumpAttemptCount}] {deviceInfo}");
    }

    isHoldingJump = true;

    // LOGICA UNIFICATA E SEMPLIFICATA
    if (TryExecuteJumpImmediate())
    {
        if (debugJumpInBuild)
            Debug.Log("[Jump] Eseguito IMMEDIATAMENTE");
    }
    else
    {
        // Imposta buffer solo se il salto non è stato eseguito
        jumpBufferCounter = jumpBufferTime;
        if (debugJumpInBuild)
            Debug.Log($"[Jump] Impostato buffer: {jumpBufferCounter}s");
    }
}
private bool TryExecuteJumpImmediate()
{
    if (!isJumpEnabled || IsMovementLocked) return false;
    
    // HANGING - priorità assoluta
    if (hanging)
    {
        ExecuteJumpFromHang();
        return true;
    }
    
    // PRIMO SALTO - condizioni semplificate
    if (jumpCount == 0)
    {
        bool canFirstJump = controller.isGrounded || coyoteTimeCounter > 0f;
        
        if (canFirstJump)
        {
            ExecuteJump(true);
            return true;
        }
    }
    
    // MULTI JUMP - logica diretta
    else if (jumpCount > 0 && jumpCount < maxJumps)
    {
        // Deve essere in aria per il multi-jump
        bool inAir = !controller.isGrounded;
        
        if (inAir)
        {
            ExecuteJump(false);
            return true;
        }
    }
    
    return false;
}

    private void OnJumpCanceled(InputAction.CallbackContext ctx)
    {
        isHoldingJump = false;
    }

    private void Start()
    {
        _animator = GetComponentInChildren<Animator>();
        controller = GetComponent<CharacterController>();
        
        if (protectMaxHealth && maxHealth != designatedMaxHealth)
            maxHealth = designatedMaxHealth;
        else if (!protectMaxHealth)
            designatedMaxHealth = maxHealth;
        
        currentHealth = MaxHealth;
        
        if (playerUI != null)
            playerUI.UpdateHealth(currentHealth);
        
        if (sprintFX) sprintFX.StopEffect();
        
        SetupFootstepAudio();

        StartCoroutine(InitializeCameraWithRetryImproved());
    }
  private IEnumerator InitializeCameraWithRetryImproved()
{
    int attempts = 0;
    const int maxAttempts = 20; // Aumentato per maggiore sicurezza
    
    while (attempts < maxAttempts && cameraTransform == null)
    {
        if (debugCameraChanges)
        {
            Debug.Log($"[ThirdPersonController] Tentativo inizializzazione camera {attempts + 1}/{maxAttempts}");
        }
        
        // Aspetta che il CameraManager sia pronto (se utilizzato)
        if (useCameraManagerIntegration)
        {
            CameraManager cameraManager = FindFirstObjectByType<CameraManager>();
            if (cameraManager != null && !cameraManager.IsCameraSystemReady())
            {
                if (debugCameraChanges)
                {
                    Debug.Log($"[ThirdPersonController] Aspettando CameraManager...");
                }
                attempts++;
                yield return new WaitForSeconds(0.1f);
                continue;
            }
        }
        
        // Prova il rilevamento
        DetectActiveCamera();
        
        if (cameraTransform != null)
        {
            if (debugCameraChanges)
            {
                Debug.Log($"[ThirdPersonController] ✅ Camera inizializzata con successo al tentativo {attempts + 1}: {cameraTransform.name}");
            }
            break;
        }
        
        attempts++;
        yield return new WaitForSeconds(0.1f);
    }
    
    if (cameraTransform == null)
    {
        Debug.LogError($"[ThirdPersonController] ❌ FALLIMENTO COMPLETO inizializzazione camera dopo {maxAttempts} tentativi!");
        Debug.LogError("Il movimento del player sarà compromesso. Verifica la configurazione delle camere.");
        
        // Ultima disperata ricerca
        Camera fallbackCamera = Camera.main ?? FindFirstObjectByType<Camera>();
        if (fallbackCamera != null)
        {
            Debug.LogWarning($"[ThirdPersonController] Usando camera di emergenza: {fallbackCamera.name}");
            currentActiveCamera = fallbackCamera;
            cameraTransform = fallbackCamera.transform;
        }
    }
}

    private void SetupFootstepAudio()
    {
        // Setup footstep audio (codice esistente)
        if (footstepAudioSource == null)
        {
            footstepAudioSource = GetComponent<AudioSource>();
            if (footstepAudioSource == null)
            {
                GameObject audioGO = new GameObject("FootstepAudio");
                audioGO.transform.SetParent(transform);
                audioGO.transform.localPosition = Vector3.zero;
                footstepAudioSource = audioGO.AddComponent<AudioSource>();
            }
        }

        footstepAudioSource.playOnAwake = false;
        footstepAudioSource.loop = false;
        footstepAudioSource.spatialBlend = 0.7f;
        footstepAudioSource.rolloffMode = AudioRolloffMode.Linear;
        footstepAudioSource.maxDistance = 15f;

        // ✅ SETUP JUMP AUDIO SOURCE
        SetupJumpAudio();

        // ✅ SETUP HIT AUDIO SOURCE
        SetupHitAudio();
    }
    // ✅ NUOVO METODO PER SETUP DELL'AUDIO DEL SALTO
    private void SetupJumpAudio()
    {
        // Configura l'AudioSource per il salto (se è separato)
        if (jumpAudioSource != footstepAudioSource)
        {
            jumpAudioSource.playOnAwake = false;
            jumpAudioSource.loop = false;
            jumpAudioSource.spatialBlend = 0.7f;
            jumpAudioSource.rolloffMode = AudioRolloffMode.Linear;
            jumpAudioSource.maxDistance = 20f; // Leggermente più lontano dei footsteps
        }
    }
    private void SetupHitAudio()
{

    // Configura l'AudioSource per i colpi (se è separato)
    if (hitAudioSource != footstepAudioSource)
    {
        hitAudioSource.playOnAwake = false;
        hitAudioSource.loop = false;
        hitAudioSource.spatialBlend = 0.8f; // Più spaziale dei footsteps
        hitAudioSource.rolloffMode = AudioRolloffMode.Linear;
        hitAudioSource.maxDistance = 25f; // Più lontano dei footsteps
        hitAudioSource.priority = 128; // Priorità normale
    }
}
public void PlayHitSound()
{
    if (hitAudioSource == null || hitSounds == null || hitSounds.Length == 0) 
    {
        Debug.LogWarning("[ThirdPersonController] Hit audio non configurato correttamente!");
        return;
    }
    
    AudioClip clipToPlay;
    
    if (useRandomHitSound && hitSounds.Length > 1)
    {
        // Scegli un suono casuale
        clipToPlay = hitSounds[Random.Range(0, hitSounds.Length)];
    }
    else
    {
        // Usa sempre il primo suono
        clipToPlay = hitSounds[0];
    }
    
    // Configura e riproduci il suono
    float randomPitch = 1f + Random.Range(-hitPitchVariation, hitPitchVariation);
    
    // Se usi lo stesso AudioSource di altri suoni, usa PlayOneShot per non interrompere
    if (hitAudioSource == footstepAudioSource || hitAudioSource == jumpAudioSource)
    {
        hitAudioSource.pitch = randomPitch;
        hitAudioSource.PlayOneShot(clipToPlay, hitVolume);
    }
    else
    {
        hitAudioSource.pitch = randomPitch;
        hitAudioSource.volume = hitVolume;
        hitAudioSource.clip = clipToPlay;
        hitAudioSource.Play();
    }
    
    // Debug per verificare che funzioni
    Debug.Log($"[ThirdPersonController] 💥 Riprodotto suono colpo: {clipToPlay.name}");
}
// ✅ NUOVO METODO PER RIPRODURRE IL SUONO DEL SALTO
    private void PlayJumpSound()
    {
        if (jumpAudioSource == null || jumpSounds == null || jumpSounds.Length == 0)
        {
            Debug.LogWarning("[ThirdPersonController] Jump audio non configurato correttamente!");
            return;
        }

        AudioClip clipToPlay;

        if (useRandomJumpSound && jumpSounds.Length > 1)
        {
            // Scegli un suono casuale
            clipToPlay = jumpSounds[Random.Range(0, jumpSounds.Length)];
        }
        else
        {
            // Usa sempre il primo suono
            clipToPlay = jumpSounds[0];
        }

        // Configura e riproduci il suono
        jumpAudioSource.pitch = 1f + Random.Range(-jumpPitchVariation, jumpPitchVariation);
        jumpAudioSource.volume = jumpVolume;

        // Se usi lo stesso AudioSource dei footsteps, usa PlayOneShot per non interrompere i footsteps
        if (jumpAudioSource == footstepAudioSource)
        {
            jumpAudioSource.PlayOneShot(clipToPlay, jumpVolume);
        }
        else
        {
            jumpAudioSource.clip = clipToPlay;
            jumpAudioSource.Play();
        }

        // Debug per verificare che funzioni
        Debug.Log($"[ThirdPersonController] 🔊 Riprodotto suono salto: {clipToPlay.name}");
    }


    private void OnEnable() => controls.Gameplay.Enable();
    private void OnDisable()
    {
        controls.Gameplay.Disable();
        if (sprintFX) sprintFX.StopEffect();
        sprintFXActive = false;
        StopFootstepAudio();
    }

    // ✅ UPDATE OTTIMIZZATO CON GESTIONE CAMERA DINAMICA
    private void Update()
    {
        // SOLO INPUT E ANIMAZIONI qui
        UpdateActiveCamera();
        UpdateCurrentCameraSettings();
        HandleFootstepAudio();
        HandleSprintFX();
        CheckAndFixStuckJumpAnimation();

        // Salva l'input per usarlo in FixedUpdate
        cachedMoveInput = moveInput;
        cachedJumpInput = jumpInput;
    }

private Vector2 cachedMoveInput;
private bool cachedJumpInput;
    private void CheckAndFixStuckJumpAnimation()
    {
        bool isJumpAnimActive = _animator.GetBool(JumpHash) || _animator.GetBool(DoubleJumpHash);
        bool isGroundedNow = controller.isGrounded;
        bool hasLowVerticalVelocity = Mathf.Abs(velocity.y) < 1f;

        // If jump animation is active but we're clearly grounded and not moving vertically
        if (isJumpAnimActive && isGroundedNow && hasLowVerticalVelocity)
        {
            // Force landing state
            Debug.Log("[ThirdPersonController] 🔧 Fixing stuck jump animation");
            OnLanding();
        }

        // Additional safety check for very long jump animations
        if (isJumpAnimActive && Time.time - lastJumpTime > 3f)
        {
            Debug.Log("[ThirdPersonController] 🔧 Force resetting jump animation after timeout");
            OnLanding();
        }
    }

private void CheckForLedgeRelease()
    {
        if (!hanging || !isHangPositionStable) return;

        // ✅ ARRAMPICATA CON MOVIMENTO VERTICALE
        if (moveInput.y > 0.3f) // Soglia per arrampicata
        {
            if (debugLedgeGrab)
                Debug.Log("[LedgeGrab] 🧗 Iniziando arrampicata con movimento verticale");

            // ✅ AVVIA STATO ARRAMPICATA PRIMA DI TERMINARE HANGING
            isClimbing = true;

            // ✅ AVVIA IL MOVIMENTO DI SALITA
            if (climbHeightCoroutine != null)
                StopCoroutine(climbHeightCoroutine);
            climbHeightCoroutine = StartCoroutine(ExecuteClimbMovement());

            // ✅ TRIGGER ANIMAZIONE
            _animator.SetTrigger(ClimbHash);

            // ✅ TERMINA HANGING
            hanging = false;
            isHangPositionStable = false;
            hangStabilityTimer = 0f;
            _animator.SetBool(HangingHash, false);

            // ✅ SALVA TEMPO DI RILASCIO PER COOLDOWN
            lastLedgeGrabTime = Time.time;

            // ✅ RESET VELOCITÀ (ma non fermare il movimento di climbing)
            velocity = Vector3.zero;
            playerVelocity = Vector3.zero;
            attackVelocity = Vector3.zero;
            externalPush = Vector3.zero;

            // ✅ DELAY PER EVITARE RIATTACCO
            StartCoroutine(EnableMovementAfterClimb());

            if (debugLedgeGrab)
            {
                Debug.Log($"[LedgeGrab] ✅ Arrampicata iniziata - salita di {climbHeightBoost}m in corso!");
            }

            return;
        }

        // ✅ RILASCIA con input verso il basso/indietro
        else if (moveInput.y < -0.3f) // Soglia per rilascio manuale
        {
            if (debugLedgeGrab)
                Debug.Log("[LedgeGrab] 🎮 Rilasciato manualmente con input indietro");

            ReleaseLedgeGrab();
            StartCoroutine(EnableMovementAfterHangJump());
        }

        // ✅ RILASCIA con movimento laterale forte (scendere di lato)
        else if (Mathf.Abs(moveInput.x) > 0.8f && moveInput.y < 0.1f)
        {
            if (debugLedgeGrab)
                Debug.Log("[LedgeGrab] 🎮 Rilasciato con movimento laterale forte");

            ReleaseLedgeGrab();
            StartCoroutine(EnableMovementAfterHangJump());
        }

        // ✅ DEBUG: Mostra controlli disponibili
        else if (debugLedgeGrab && Time.frameCount % 180 == 0) // Ogni 3 secondi
        {
            Debug.Log("[LedgeGrab] 🎮 CONTROLLI: ↑ per arrampicare, ↓ per rilasciare, Spazio per saltare, ← → forte per scendere di lato");
        }
    }
public void OnClimbAnimationEnd()
{
    isClimbing = false;
    
    // ✅ FERMA LA COROUTINE SE ANCORA ATTIVA
    if (climbHeightCoroutine != null)
    {
        StopCoroutine(climbHeightCoroutine);
        climbHeightCoroutine = null;
        
        if (debugLedgeGrab)
            Debug.Log("[LedgeGrab] 🛑 Movimento di salita interrotto dall'Animation Event");
    }
    
    if (debugLedgeGrab)
        Debug.Log("[LedgeGrab] ✅ Animation Event: Arrampicata completata - gravità riabilitata");
}
    // ✅ AGGIUNGI QUESTO NUOVO METODO (dopo EnableMovementAfterHangJump, circa linea 750)
    /// <summary>
    /// Riabilita il movimento dopo l'arrampicata
    /// </summary>

    private void HandleLedgeGrab()
    {
        if (!ledgeGrabEnabled) return;

        // ✅ GESTIONE HANGING - SOLO STABILIZZAZIONE, NESSUN RILASCIO AUTOMATICO
        if (hanging)
        {
            // Mantieni posizione stabile usando CharacterController.Move
            StabilizeHangPosition();

            // ✅ RIMOSSO: Non controllare più la validità del ledge automaticamente
            // Il player rimane appeso fino a input manuale o salto

            return;
        }

        // Controlla solo se stiamo cadendo e non siamo già appesi
        if (velocity.y < -2f && !controller.isGrounded && canMoveAfterHang)
        {
            TryLedgeGrab();
        }
    }
    private IEnumerator ExecuteClimbMovement()
{
    if (debugLedgeGrab)
        Debug.Log("[LedgeGrab] 🏃 Iniziando movimento di salita durante climbing");
    
    Vector3 startPosition = transform.position;
    Vector3 targetPosition = startPosition + Vector3.up * climbHeightBoost;
    
    float elapsedTime = 0f;
    float duration = 1f / climbBoostSpeed; // Calcola durata basata sulla velocità
    
    while (elapsedTime < duration && isClimbing)
    {
        elapsedTime += Time.deltaTime;
        float progress = Mathf.Clamp01(elapsedTime / duration);
        
        // ✅ USA LA CURVA PER UN MOVIMENTO FLUIDO
        float curveValue = climbHeightCurve.Evaluate(progress);
        Vector3 currentTargetPos = Vector3.Lerp(startPosition, targetPosition, curveValue);
        
        // ✅ CALCOLA IL MOVIMENTO DA APPLICARE QUESTO FRAME
        Vector3 moveThisFrame = currentTargetPos - transform.position;
        
        // ✅ APPLICA IL MOVIMENTO USANDO CHARACTERCONTROLLER
        if (controller.enabled && moveThisFrame.magnitude > 0.001f)
        {
            controller.Move(moveThisFrame);
        }
        
        // Debug opzionale
        if (debugLedgeGrab && Time.frameCount % 10 == 0)
        {
            Debug.Log($"[LedgeGrab] 📈 Climbing progress: {progress:P0} - Altezza: {(transform.position.y - startPosition.y):F3}m");
            Debug.DrawLine(startPosition, targetPosition, Color.green, 0.1f);
            Debug.DrawLine(transform.position, transform.position + Vector3.up * 0.5f, Color.yellow, 0.1f);
        }
        
        yield return null; // Aspetta il prossimo frame
    }
    
    // ✅ ASSICURATI DI RAGGIUNGERE LA POSIZIONE FINALE
    if (isClimbing)
    {
        Vector3 finalMove = targetPosition - transform.position;
        if (controller.enabled && finalMove.magnitude > 0.001f)
        {
            controller.Move(finalMove);
        }
        
        if (debugLedgeGrab)
        {
            float totalHeight = transform.position.y - startPosition.y;
            Debug.Log($"[LedgeGrab] ✅ Movimento di salita completato! Altezza totale: {totalHeight:F3}m");
        }
    }
    
    climbHeightCoroutine = null;
}
private IEnumerator EnableMovementAfterClimb()
    {
        canMoveAfterHang = false;

        // ✅ BREVE DELAY SOLO PER EVITARE RIATTACCO IMMEDIATO AL LEDGE
        yield return new WaitForSeconds(0.3f);

        canMoveAfterHang = true;

        if (debugLedgeGrab)
            Debug.Log("[LedgeGrab] ✅ Movimento riabilitato dopo arrampicata");
    }
private void StabilizeHangPosition()
{
    if (!hanging) return;
    
    // Incrementa timer di stabilità
    hangStabilityTimer += Time.deltaTime;
    if (!isHangPositionStable && hangStabilityTimer >= HANG_STABILITY_TIME)
    {
        isHangPositionStable = true;
        lastValidHangPosition = transform.position; // ✅ SALVA SOLO UNA VOLTA QUANDO STABILE
        
        if (debugLedgeGrab)
            Debug.Log("[LedgeGrab] ✅ Posizione stabilizzata e salvata");
    }
    
    // ✅ CALCOLA DIFFERENZA E APPLICA CORREZIONE GRADUALE
    Vector3 currentPos = transform.position;
    Vector3 targetPos = hangPosition;
    Vector3 positionDifference = targetPos - currentPos;
    
    // Solo se la differenza è significativa
    if (positionDifference.magnitude > hangPositionTolerance)
    {
        // ✅ USA CHARACTERCONTROLLER.MOVE PER CORREZIONI GRADUALI
        Vector3 correctionMove = positionDifference * hangStabilizationForce * Time.deltaTime;
        
        // Limita la correzione per evitare overshooting
        if (correctionMove.magnitude > positionDifference.magnitude)
        {
            correctionMove = positionDifference;
        }
        
        controller.Move(correctionMove);
        
        if (debugLedgeGrab && isHangPositionStable && Time.frameCount % 10 == 0) // ✅ RIDOTTO SPAM
        {
            Debug.DrawLine(currentPos, targetPos, Color.red, 0.1f);
            Debug.Log($"[LedgeGrab] Correzione posizione: {correctionMove.magnitude:F3}");
        }
    }
    
    // ✅ FORZA COMPLETAMENTE LA VELOCITÀ A ZERO DURANTE HANGING
    velocity = Vector3.zero;
    playerVelocity = Vector3.zero;
    attackVelocity = Vector3.zero;
    externalPush = Vector3.zero;
}
private void TryLedgeGrab()
{
    if (!CanGrabLedge()) return;
    
    // ✅ MIGLIORATO: Usa la direzione FORWARD del player per il rilevamento
    Vector3 playerForward = transform.forward;
    
    // Ray verso il basso per trovare il top del ledge
    RaycastHit downHit;
    Vector3 lineDownStart = (transform.position + Vector3.up * 1.5f) + playerForward * ledgeDetectionDistance;
    Vector3 lineDownEnd = (transform.position + Vector3.up * 0.7f) + playerForward * ledgeDetectionDistance;
    
    if (debugLedgeGrab)
        Debug.DrawLine(lineDownStart, lineDownEnd, Color.red, 0.5f);
    
    if (Physics.Linecast(lineDownStart, lineDownEnd, out downHit, ledgeLayerMask))
    {
        // ✅ MIGLIORATO: Ray in avanti dalla posizione del ledge trovato
        RaycastHit fwdHit;
        Vector3 lineFwdStart = new Vector3(transform.position.x, downHit.point.y - 0.1f, transform.position.z);
        Vector3 lineFwdEnd = lineFwdStart + playerForward * (ledgeDetectionDistance + 0.3f);
        
        if (debugLedgeGrab)
            Debug.DrawLine(lineFwdStart, lineFwdEnd, Color.green, 0.5f);
        
        if (Physics.Linecast(lineFwdStart, lineFwdEnd, out fwdHit, ledgeLayerMask))
        {
            if (debugLedgeGrab)
            {
                Debug.DrawLine(fwdHit.point, fwdHit.point + fwdHit.normal * 2f, Color.blue, 0.5f);
                Debug.Log($"[LedgeGrab] Muro trovato! Normale: {fwdHit.normal}, Angolo: {Vector3.Angle(fwdHit.normal, Vector3.up)}°");
            }
            
            if (IsValidLedgeGrab(downHit, fwdHit))
            {
                ExecuteLedgeGrab(downHit, fwdHit);
            }
        }
        else if (debugLedgeGrab)
        {
            Debug.Log("[LedgeGrab] ❌ Nessuna parete trovata per il ledge");
        }
    }
    else if (debugLedgeGrab)
    {
        Debug.DrawLine(lineDownStart, lineDownEnd, Color.white, 0.5f);
    }
}


private bool CanGrabLedge()
{
    // ✅ COOLDOWN PER PREVENIRE GRAB RIPETUTI
    if (Time.time - lastLedgeGrabTime < ledgeGrabCooldown)
    {
        if (debugLedgeGrab && Time.frameCount % 30 == 0) // Ridotto spam debug
        {
            Debug.Log($"[LedgeGrab] Cooldown attivo: {(ledgeGrabCooldown - (Time.time - lastLedgeGrabTime)):F2}s rimanenti");
        }
        return false;
    }
    
    // Non afferrare se siamo troppo vicini al suolo
    if (Physics.Raycast(transform.position, Vector3.down, 0.8f, ledgeLayerMask))
    {
        if (debugLedgeGrab)
            Debug.Log("[LedgeGrab] Troppo vicino al suolo");
        return false;
    }
    
    // ✅ AGGIUSTATO per gravity -40: velocità più realistiche
    if (velocity.y < -35f) // Era -75f, troppo restrittivo
    {
        if (debugLedgeGrab)
            Debug.Log($"[LedgeGrab] Velocità di caduta troppo alta: {velocity.y:F1}");
        return false;
    }
    
    // ✅ NUOVO: Verifica che stiamo effettivamente cadendo
    if (velocity.y > -2f)
    {
        if (debugLedgeGrab)
            Debug.Log($"[LedgeGrab] Non abbastanza in caduta: {velocity.y:F1}");
        return false;
    }
    
    return true;
}
private bool IsValidLedgeGrab(RaycastHit downHit, RaycastHit fwdHit)
{
    // Verifica che l'angolo del muro sia appropriato
    float wallAngle = Vector3.Angle(fwdHit.normal, Vector3.up);
    if (wallAngle < 70f)
    {
        if (debugLedgeGrab)
            Debug.Log($"[LedgeGrab] ❌ Muro troppo inclinato: {wallAngle:F1}° (minimo: 70°)");
        return false;
    }
    
    // ✅ CALCOLO POSIZIONE DI HANG PRECISO - CORRETTO
    Vector3 proposedHangPos = new Vector3(fwdHit.point.x, downHit.point.y + hangHeightOffset, fwdHit.point.z);
    Vector3 wallOffset = fwdHit.normal * hangDistanceFromWall; // ✅ NORMALE verso l'esterno
    proposedHangPos += wallOffset;
    
    // ✅ CONTROLLO SPAZIO OTTIMIZZATO
    float checkRadius = 1.2f;
    float checkHeight = 4f;
    
    LayerMask solidLayers = LayerMask.GetMask("Default", "Ground");
    
    Vector3 capsuleTop = proposedHangPos + Vector3.up * (checkHeight * 0.5f);
    Vector3 capsuleBottom = proposedHangPos - Vector3.up * (checkHeight * 0.5f);
    
    bool spaceOccupied = Physics.CapsuleCast(
        capsuleTop, 
        capsuleBottom, 
        checkRadius, 
        Vector3.up, 
        0.01f, 
        solidLayers
    );
    
    if (spaceOccupied)
    {
        if (debugLedgeGrab)
        {
            Debug.Log("[LedgeGrab] ❌ Spazio di hang occupato");
            Debug.DrawLine(capsuleTop, capsuleBottom, Color.red, 1f);
        }
        return false;
    }
    
    if (debugLedgeGrab)
    {
        Debug.Log($"[LedgeGrab] ✅ Ledge valido! Ang.muro: {wallAngle:F1}°, Pos: {proposedHangPos}");
        Debug.DrawLine(capsuleTop, capsuleBottom, Color.green, 2f);
        Debug.DrawLine(proposedHangPos, proposedHangPos + fwdHit.normal * 1f, Color.yellow, 2f);
    }
    
    return true;
}


private void ExecuteLedgeGrab(RaycastHit downHit, RaycastHit fwdHit)
{
    if (debugLedgeGrab)
        Debug.Log($"[LedgeGrab] 🎯 Iniziando ledge grab... Normal muro: {fwdHit.normal}");
    
    // ✅ RESET COMPLETO DELLE VELOCITÀ PRIMA DI TUTTO
    velocity = Vector3.zero;
    playerVelocity = Vector3.zero;
    attackVelocity = Vector3.zero;
    externalPush = Vector3.zero;
    
    // ✅ CALCOLA POSIZIONE TARGET PRECISA
    hangPosition = new Vector3(fwdHit.point.x, downHit.point.y + hangHeightOffset, fwdHit.point.z);
    Vector3 wallOffset = fwdHit.normal * hangDistanceFromWall; // ✅ CORRETTO: normale verso l'esterno
    hangPosition += wallOffset;
    
    // ✅ CORRETTO: SALVA DIREZIONE VERSO IL MURO (per il salto)
    hangForward = -fwdHit.normal; // Direzione dal player verso il muro
    
    // ✅ MOVIMENTO IMMEDIATO VERSO LA POSIZIONE DI HANG
    Vector3 moveToHang = hangPosition - transform.position;
    controller.Move(moveToHang);
    
    // ✅ CORRETTO: Orienta il player guardando VERSO IL MURO (non lontano!)
    // Il player deve guardare il muro per sembrare che si stia aggrappando
    Vector3 lookDirection = hangForward; // Guarda VERSO il muro
    transform.rotation = Quaternion.LookRotation(lookDirection);
    
    // ✅ IMPOSTA STATO HANGING CON RESET TIMER
    hanging = true;
    hangStabilityTimer = 0f;
    isHangPositionStable = false;
    _animator.SetBool(HangingHash, true);
    
    // Reset vari
        StopFootstepAudio();
    jumpBufferCounter = 0f;
    coyoteTimeCounter = 0f;
    jumpCount = 0;
    
    if (debugLedgeGrab)
    {
        Debug.Log($"[LedgeGrab] ✅ Appeso al ledge! Posizione: {hangPosition}");
        Debug.Log($"[LedgeGrab] 🧭 Player guarda VERSO il muro: {lookDirection}");
        Debug.Log($"[LedgeGrab] 📍 Offset dalla parete: {hangDistanceFromWall}m");
    }
}
private bool IsLedgeStillValid()
{
    if (!hanging) return true;
    
    // ✅ NON CONTROLLARE SE NON SIAMO ANCORA STABILIZZATI (evita rilasci prematuri)
    if (!isHangPositionStable) 
    {
        if (debugLedgeGrab && Time.frameCount % 30 == 0)
            Debug.Log("[LedgeGrab] ⏳ Aspettando stabilizzazione...");
        return true;
    }
    
    // ✅ SOLO DEBUG - NON RILASCIA MAI AUTOMATICAMENTE
    if (debugLedgeGrab && Time.frameCount % 120 == 0) // Ogni 2 secondi circa
    {
        Vector3 rayStart = transform.position + Vector3.up * 0.5f;
        Vector3 rayDirection = hangForward;
        float rayDistance = hangDistanceFromWall + 0.3f;
        
        bool wallStillThere = Physics.Raycast(rayStart, rayDirection, rayDistance, ledgeLayerMask);
        
        Color rayColor = wallStillThere ? Color.green : Color.yellow;
        Debug.DrawLine(rayStart, rayStart + rayDirection * rayDistance, rayColor, 2f);
        
        Debug.Log($"[LedgeGrab] 🔒 APPESO PERMANENTE - Muro: {(wallStillThere ? "✅ PRESENTE" : "⚠️ NON RILEVATO (ma rimango appeso)")}");
        Debug.Log($"[LedgeGrab] 💡 Per rilasciare: muovi stick giù o salta");
    }
    
    // ✅ RITORNA SEMPRE TRUE - Non rilascia mai automaticamente
    return true;
}

  private void UpdateActiveCamera()
{
    if (!autoDetectActiveCamera) return;
    
    // Controlla molto più frequentemente se non abbiamo una camera valida
    float checkInterval = (currentActiveCamera == null || cameraTransform == null) ? 0.02f : 0.1f; // Molto più frequente
    
    cameraCheckTimer += Time.deltaTime;
    
    if (cameraCheckTimer >= checkInterval)
    {
        cameraCheckTimer = 0f;
        DetectActiveCamera();
        
        // NUOVO: Verifica immediata dopo rilevamento
        if (cameraTransform == null && debugCameraChanges)
        {
            Debug.LogWarning("[ThirdPersonController] ⚠️ Camera ancora null dopo DetectActiveCamera!");
        }
    }
}

    // [Il resto dei metodi rimane identico al codice originale...]
    // ✅ NUOVO SISTEMA DI RILEVAMENTO PIATTAFORME INTELLIGENTE
   private void DetectAndUpdatePlatform()
{
    // Se siamo hanging o climbing, mantieni la piattaforma corrente ma non aggiornare
    if (hanging || isClimbing)
    {
        return;
    }
    
    if (!controller.isGrounded)
    {
        // NUOVO: Sganciamento più conservativo
        if (currentPlatform != null && velocity.y > 5f) // Aumentato da 2f a 5f
        {
            if (debugPlatformMovement)
                Debug.Log($"[Platform] Sganciato da {currentPlatform.name} durante salto alto (velocità: {velocity.y:F2})");
            DetachFromCurrentPlatform();
        }
        // Controllo più permissivo della validità
        else if (currentPlatform != null && !IsPlatformValidLoose())
        {
            if (debugPlatformMovement)
                Debug.Log($"[Platform] Piattaforma non più valida: {currentPlatform.name}");
            DetachFromCurrentPlatform();
        }
        
        UpdatePlatformMovement();
        return;
    }

    // Trova la piattaforma più vicina sotto di noi
    Transform detectedPlatform = FindPlatformBelow();
    
    if (detectedPlatform != currentPlatform)
    {
        if (currentPlatform != null)
        {
            if (debugPlatformMovement)
                Debug.Log($"[Platform] Cambiando da {currentPlatform.name} a {(detectedPlatform ? detectedPlatform.name : "nessuna")}");
            DetachFromCurrentPlatform();
        }
        
        if (detectedPlatform != null)
        {
            AttachToPlatform(detectedPlatform);
        }
    }
    
    UpdatePlatformMovement();
}
private bool IsPlatformValidLoose()
{
    if (currentPlatform == null) return false;

    // Distanze più permissive basate sullo stato del player
    float maxDistance;
    if (velocity.y > 0) // Saltando
    {
        maxDistance = 15f; // Molto permissivo durante salti
    }
    else if (!controller.isGrounded)
    {
        maxDistance = 10f; // Permissivo in aria
    }
    else
    {
        maxDistance = 6f; // Normale quando a terra
    }
    
    float distance = Vector3.Distance(transform.position, currentPlatform.position);
    
    // Usa i bounds della piattaforma per calcoli più precisi
    Collider platformCollider = currentPlatform.GetComponent<Collider>();
    if (platformCollider != null)
    {
        // Calcola distanza dai bounds invece che dal centro
        Vector3 closestPoint = platformCollider.ClosestPoint(transform.position);
        float boundsDistance = Vector3.Distance(transform.position, closestPoint);
        
        // Usa la distanza più piccola tra centro e bounds
        distance = Mathf.Min(distance, boundsDistance);
        
        // Aumenta la tolleranza basata sulla dimensione della piattaforma
        float platformSize = Mathf.Max(platformCollider.bounds.size.x, platformCollider.bounds.size.z);
        maxDistance = Mathf.Max(maxDistance, platformSize * 1.5f);
    }
    
    bool isValid = distance <= maxDistance;
    
    if (debugPlatformMovement && !isValid && Time.frameCount % 90 == 0) // Ridotto spam
        Debug.Log($"[Platform] Piattaforma NON valida: distanza {distance:F2} > max {maxDistance:F2} (bounds check)");
    
    return isValid;
}

    // ✅ TROVA LA PIATTAFORMA SOTTO IL PLAYER
private Transform FindPlatformBelow()
{
    Vector3 rayStart = transform.position + Vector3.up * 0.1f;
    
    // Prima prova con il metodo principale
    int hitCount = Physics.RaycastNonAlloc(rayStart, Vector3.down, raycastHits, 1.8f, platformLayers);
    
    Transform bestPlatform = null;
    float closestDistance = float.MaxValue;
    
    // Analizza tutti i hits per trovare la piattaforma migliore
    for (int i = 0; i < hitCount; i++)
    {
        RaycastHit hit = raycastHits[i];
        Transform hitTransform = hit.collider.transform;
        
        if (IsPlatformTag(hit.collider.tag) && hit.distance < closestDistance)
        {
            Vector3 hitPoint = hit.point;
            float heightDifference = transform.position.y - hitPoint.y;
            
            if (heightDifference > -0.8f && heightDifference < maxPlatformHeight)
            {
                // Verifica speciale per RotatingObject
                if (hit.collider.tag == "RotatingPlatform")
                {
                    RotatingObject rotObj = hit.collider.GetComponent<RotatingObject>();
                    if (rotObj != null && rotObj.GetPlatformType() == PlatformType.ObstaclePlatform && 
                        rotObj.GetDetachPlayerOnHit())
                    {
                        continue;
                    }
                }
                
                bestPlatform = hitTransform;
                closestDistance = hit.distance;
            }
        }
    }
    
    // Se non trova nulla, prova con un rilevamento multi-punto più ampio
    if (bestPlatform == null)
    {
        Vector3[] checkPoints = {
            transform.position,
            transform.position + transform.forward * 0.3f,
            transform.position - transform.forward * 0.3f,
            transform.position + transform.right * 0.3f,
            transform.position - transform.right * 0.3f
        };
        
        foreach (Vector3 point in checkPoints)
        {
            Vector3 checkStart = point + Vector3.up * 0.05f;
            if (Physics.Raycast(checkStart, Vector3.down, out RaycastHit multiHit, 1.2f, platformLayers))
            {
                if (IsPlatformTag(multiHit.collider.tag))
                {
                    bestPlatform = multiHit.collider.transform;
                    break;
                }
            }
        }
    }
    
    return bestPlatform;
}

    // ✅ VERIFICA SE È UN TAG DI PIATTAFORMA
    private bool IsPlatformTag(string tag)
    {
        return tag == "MovingPlatform" || tag == "RotatingPlatform" || tag == "RaftPlatform" || tag == "FallBlock";
    }

    // ✅ ATTACCA IL PLAYER ALLA PIATTAFORMA
private void AttachToPlatform(Transform platform)
{
    currentPlatform = platform;
    lastPlatformPosition = platform.position;
    lastPlatformRotation = platform.rotation;
    
    // Calcola posizione locale del player sulla piattaforma
    localPositionOnPlatform = platform.InverseTransformPoint(transform.position);
    
    // Cache dei componenti della piattaforma
    currentMovingPlatform = platform.GetComponent<MovingPlatform>();
    currentRaftPlatform = platform.GetComponent<RaftPlatform>();
    currentRotatingObject = platform.GetComponent<RotatingObject>();
    
    // Determina se è una piattaforma ostacolo
    isOnObstaclePlatform = currentRotatingObject != null && 
                          currentRotatingObject.GetPlatformType() == PlatformType.ObstaclePlatform;
    
    if (debugPlatformMovement)
    {
        string platformType = currentMovingPlatform ? "Moving" : 
                             currentRaftPlatform ? "Raft" : 
                             currentRotatingObject ? "Rotating" : "Unknown";
        
        // ✅ NUOVO: Info sul tipo di collider
        Collider platformCollider = platform.GetComponent<Collider>();
        string colliderType = "Nessun Collider";
        string colliderInfo = "";
        
        if (platformCollider != null)
        {
            colliderType = platformCollider.GetType().Name;
            
            MeshCollider meshCol = platformCollider as MeshCollider;
            if (meshCol != null)
            {
                colliderInfo = meshCol.convex ? " (Convex)" : " (Non-Convex)";
            }
        }
        
        Debug.Log($"[Platform] Attaccato a {platform.name} (Tipo: {platformType}, Obstacle: {isOnObstaclePlatform}, Collider: {colliderType}{colliderInfo})");
    }
}

    // ✅ VERIFICA SE LA PIATTAFORMA È ANCORA VALIDA
    private bool IsPlatformValid()
{
    if (currentPlatform == null) return false;

    // Durante il salto, sii più permissivo sulla distanza
    float maxDistance = velocity.y > 0 ? 10f : 5f;
    float distance = Vector3.Distance(transform.position, currentPlatform.position);
    
    // Usa i bounds per determinare se siamo ancora vicini alla piattaforma
    Collider platformCollider = currentPlatform.GetComponent<Collider>();
    if (platformCollider != null)
    {
        Bounds platformBounds = platformCollider.bounds;
        float allowedDistance = Mathf.Max(platformBounds.size.magnitude * 1.2f, maxDistance);
        
        bool isValid = distance <= allowedDistance;
        
        if (debugPlatformMovement && !isValid && Time.frameCount % 60 == 0)
            Debug.Log($"[Platform] Piattaforma NON valida: distanza {distance:F2} > max {allowedDistance:F2}");
        
        return isValid;
    }
    else
    {
        // Fallback se non c'è collider
        bool isValid = distance <= maxDistance;
        
        if (debugPlatformMovement && !isValid && Time.frameCount % 60 == 0)
            Debug.Log($"[Platform] Piattaforma senza collider - distanza {distance:F2} > max {maxDistance:F2}");
        
        return isValid;
    }
}

    // ✅ SGANCIA IL PLAYER DALLA PIATTAFORMA
    private void DetachFromCurrentPlatform()
    {
        if (debugPlatformMovement && currentPlatform != null)
            Debug.Log($"[Platform] Sganciato da {currentPlatform.name}");
        
        currentPlatform = null;
        currentMovingPlatform = null;
        currentRaftPlatform = null;
        currentRotatingObject = null;
        isOnObstaclePlatform = false;
        
        platformDeltaPosition = Vector3.zero;
        platformDeltaRotation = Quaternion.identity;
    }

    // ✅ AGGIORNA IL MOVIMENTO DELLA PIATTAFORMA
  private void UpdatePlatformMovement()
{
    if (currentPlatform == null)
    {
        platformDeltaPosition = Vector3.zero;
        platformDeltaRotation = Quaternion.identity;
        platformMovementStabilized = false;
        return;
    }

    Vector3 currentPlatformPos = currentPlatform.position;
    Quaternion currentPlatformRot = currentPlatform.rotation;
    
    platformDeltaPosition = currentPlatformPos - lastPlatformPosition;
    platformDeltaRotation = currentPlatformRot * Quaternion.Inverse(lastPlatformRotation);
    
    // STABILIZZAZIONE MENO AGGRESSIVA
    if (platformDeltaPosition.magnitude < 0.001f) // Ridotto da 0.003f
    {
        platformDeltaPosition = Vector3.zero;
    }
    else
    {
        // Damping meno aggressivo
        platformDeltaPosition *= 0.95f; // Era 0.9f
        
        // Damping verticale meno aggressivo
        platformDeltaPosition.y *= 0.8f; // Era 0.5f
    }
    
    if (Quaternion.Angle(platformDeltaRotation, Quaternion.identity) < 0.1f) // Ridotto da 0.2f
    {
        platformDeltaRotation = Quaternion.identity;
    }
    
    lastPlatformPosition = currentPlatformPos;
    lastPlatformRotation = currentPlatformRot;
    
    if (debugPlatformMovement && platformDeltaPosition.magnitude > 0.0005f) // Soglia ridotta
    {
        Debug.Log($"[Platform] Delta meno aggressivo - Pos: {platformDeltaPosition} Mag: {platformDeltaPosition.magnitude:F4}");
    }
}


    // ✅ SISTEMA DI APPLICAZIONE MOVIMENTO COMPLETO E OTTIMIZZATO
private void ApplyAllMovement()
{
    // Se siamo hanging, usa solo la stabilizzazione
    if (hanging)
    {
        return;
    }
    if (isClimbing)
    {
        return;
    }
    
    if (!controller.enabled) return;
    
    Vector3 totalMovement = Vector3.zero;
    
    // Verifica grip della piattaforma
    UpdatePlatformGrip();
    
    // Soglia di salto più alta per mantenere movimento piattaforma più a lungo
    bool isHighJump = velocity.y > 8f; // Aumentato da 1f
    
    // 1. Movimento piattaforma (più permissivo)
    if (currentPlatform != null && !isOnObstaclePlatform && !isHighJump)
    {
        Vector3 platformMovement = ApplyPlatformMovement();
        totalMovement += platformMovement;
        
        if (debugPlatformMovement && platformMovement.magnitude > 0.001f)
            Debug.Log($"[Platform] Applicando movimento: {platformMovement.magnitude:F3} (Jump: {isHighJump}, VelY: {velocity.y:F1})");
    }
    else if (isHighJump && debugPlatformMovement && Time.frameCount % 30 == 0)
    {
        Debug.Log($"[Platform] Movimento IGNORATO durante salto alto (velocità Y: {velocity.y:F2})");
    }
    
    // 2. Movimento player (invariato)
    Vector3 playerMovement = Vector3.zero;
    playerMovement.x = (playerVelocity.x + externalPush.x + attackVelocity.x) * Time.fixedDeltaTime;
    playerMovement.z = (playerVelocity.z + externalPush.z + attackVelocity.z) * Time.fixedDeltaTime;
    playerMovement.y = velocity.y * Time.fixedDeltaTime;
    
    totalMovement += playerMovement;
    
    // 3. Applica tutto il movimento in una sola chiamata
    controller.Move(totalMovement);
}

private void ExecuteJump(bool isFirstJump)
{
    // Reset velocità verticale se negativa
    if (velocity.y < 0) velocity.y = 0f;
    
    // Calcola velocità di salto
    velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
    
    // Track del tempo di salto
    lastJumpTime = Time.time;

    // Sgancia da piattaforma se necessario
    if (currentPlatform != null)
    {
        if (debugPlatformMovement)
            Debug.Log($"[Platform] Sganciato durante salto da {currentPlatform.name}");
        DetachFromCurrentPlatform();
    }

    // Gestione contatore e animazioni
    if (isFirstJump)
    {
        jumpCount = 1;
        _animator.SetBool(JumpHash, true);
        _animator.SetBool(DoubleJumpHash, false);
        
        if (debugJumpInBuild)
            Debug.Log("[Jump] PRIMO SALTO eseguito");
    }
    else
    {
        jumpCount++;
        _animator.SetBool(JumpHash, false);
        _animator.SetBool(DoubleJumpHash, true);
        
        if (debugJumpInBuild)
            Debug.Log($"[Jump] MULTI-SALTO #{jumpCount} eseguito");
    }

    // Reset timers
    coyoteTimeCounter = 0f;
    jumpBufferCounter = 0f;
    fallingTimer = 0f;
    
    if (isFirstJump) 
    {
        PlayJumpSound();
    }
    StopFootstepAudio();
}

private Vector3 ApplyPlatformMovement()
{
    Vector3 totalPlatformMovement = Vector3.zero;
    
    // A) MOVIMENTO ORIZZONTALE - Sempre al 100%
    Vector3 horizontalMovement = platformDeltaPosition;
    horizontalMovement.y = 0f;
    totalPlatformMovement += horizontalMovement;
    
    // B) MOVIMENTO VERTICALE OTTIMIZZATO PER VELOCITÀ ALTE
    if (!disableVerticalFollowing)
    {
        float verticalDelta = platformDeltaPosition.y;
        
        // Soglia molto più bassa per velocità alte (20 unità/sec = 0.33 per frame a 60fps)
        if (useVerticalDeadZone && Mathf.Abs(verticalDelta) <= verticalDeadZone)
        {
            verticalDelta = 0f;
        }
        
        // Smoothing condizionale: NON applicare per movimenti grandi
        if (platformVerticalSmoothing > 0f && Mathf.Abs(verticalDelta) < 0.5f) // Solo per movimenti piccoli
        {
            verticalDelta = Mathf.Lerp(0f, verticalDelta, Time.deltaTime * platformVerticalSmoothing);
        }
        // Per movimenti grandi (piattaforme veloci), applica direttamente senza smoothing
        
        // Soglia molto più bassa per piattaforme veloci
        if (currentRaftPlatform != null || Mathf.Abs(verticalDelta) >= platformVerticalThreshold)
        {
            // Multiplier al 100% per seguire completamente la piattaforma
            verticalDelta *= platformVerticalMultiplier;
            
            // Max speed deve essere maggiore della velocità massima della piattaforma
            verticalDelta = Mathf.Clamp(verticalDelta, -platformVerticalMaxSpeed, platformVerticalMaxSpeed);
            
            // Applica sempre se siamo su una piattaforma valida
            if (controller.isGrounded || IsPlatformValidLoose())
            {
                totalPlatformMovement.y = verticalDelta;
            }
        }
    }
    
    // C) ROTAZIONE - Invariato
    if (platformDeltaRotation != Quaternion.identity)
    {
        Vector3 eulerAngles = platformDeltaRotation.eulerAngles;
        Quaternion yOnlyRotation = Quaternion.Euler(0f, eulerAngles.y, 0f);
        transform.rotation = yOnlyRotation * transform.rotation;
        
        Vector3 relativePosition = transform.position - currentPlatform.position;
        relativePosition.y = 0f;
        Vector3 rotatedRelativePosition = yOnlyRotation * relativePosition;
        Vector3 rotationMovement = rotatedRelativePosition - relativePosition;
        
        totalPlatformMovement += rotationMovement;
    }
    
    if (debugPlatformMovement && totalPlatformMovement.magnitude > 0.001f)
    {
        float platformSpeed = totalPlatformMovement.magnitude / Time.deltaTime;
        Debug.Log($"[Platform] Movimento veloce: V{totalPlatformMovement.y:F3} Speed{platformSpeed:F1}u/s Raw{platformDeltaPosition.y:F4}");
    }
    
    return totalPlatformMovement;
}

    private void HandleFootstepAudio()
    {
        bool isMoving = playerVelocity.sqrMagnitude > 0.1f;
        bool isGrounded = controller.isGrounded;
        bool canPlayFootsteps = isMoving && isGrounded && !IsMovementLocked;
        
        if (canPlayFootsteps)
        {
            footstepTimer += Time.deltaTime;
            float currentInterval = GetCurrentStepInterval();
            
            if (footstepTimer >= currentInterval)
            {
                PlayFootstepSound();
                footstepTimer = 0f;
            }
            
            wasMovingLastFrame = true;
        }
        else
        {
            if (wasMovingLastFrame)
            {
                footstepTimer = 0f;
                wasMovingLastFrame = false;
            }
        }
    }
    
    private float GetCurrentStepInterval()
    {
        if (isSprinting)
            return sprintStepInterval;
        else if (smoothInputMagnitude > 0.5f)
            return runStepInterval;
        else
            return walkStepInterval;
    }

   private void PlayFootstepSound()
{
    if (footstepAudioSource == null) return;

    // ✅ RILEVA IL LAYER SOTTO IL PLAYER
    string currentGroundLayer = GetGroundLayerName();

    // ✅ SELEZIONA IL SET AUDIO BASATO SUL LAYER
    AudioClip[] currentClips = (currentGroundLayer == "Grass") ? grassFootsteps : groundFootsteps;
    
    // ✅ VOLUME BASATO SULLA VELOCITÀ (walk/run/sprint)
    float currentVolume;
    if (isSprinting)
        currentVolume = footstepVolumeSprint;
    else if (smoothInputMagnitude > 0.5f)
        currentVolume = footstepVolumeRun;
    else
        currentVolume = footstepVolumeWalk;

    // ✅ RIPRODUCI UN SUONO CASUALE DAI 3 DISPONIBILI
    if (currentClips != null && currentClips.Length > 0)
    {
        AudioClip clipToPlay = currentClips[Random.Range(0, currentClips.Length)];

        footstepAudioSource.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
        footstepAudioSource.volume = currentVolume;
        footstepAudioSource.clip = clipToPlay;
        footstepAudioSource.Play();
    }
}
    /// <summary>
    /// Rileva il nome del layer sotto il player usando raycast
    /// </summary>
    /// <summary>
    /// Rileva il tipo di superficie sotto il player
    /// - Per layer "SquareVillage": controlla Paint Texture del Terrain
    /// - Per altri layer: usa il nome del layer normale (Grass/Ground)
    /// </summary>
    private string GetGroundLayerName()
    {
        Vector3 rayStart = transform.position + Vector3.up * 0.1f;

        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 1.5f))
        {
            GameObject hitObject = hit.collider.gameObject;
            string layerName = LayerMask.LayerToName(hitObject.layer);

            // ✅ CASO SPECIALE: Layer SquareVillage -> Controlla Paint Texture del Terrain
            if (layerName == terrainLayerName)
            {
                Terrain terrain = hitObject.GetComponent<Terrain>();
                if (terrain != null && terrain.terrainData != null)
                {
                    string textureType = GetDominantTerrainTextureType(terrain, hit.point);
                    return textureType; // Ritorna "Grass" o "Ground" in base alla texture
                }
            }

            // ✅ CASO NORMALE: Altri layer -> Usa il nome del layer direttamente
            return layerName; // Ritorna "Grass" o "Ground" dal layer
        }

        // Fallback: controlla currentPlatform
        if (currentPlatform != null)
        {
            string platformLayer = LayerMask.LayerToName(currentPlatform.gameObject.layer);

            // Controlla anche qui se è un terrain speciale
            if (platformLayer == terrainLayerName)
            {
                Terrain terrain = currentPlatform.GetComponent<Terrain>();
                if (terrain != null && terrain.terrainData != null)
                {
                    return GetDominantTerrainTextureType(terrain, transform.position);
                }
            }

            return platformLayer;
        }

        return "Ground"; // Default assoluto
    }
/// <summary>
/// Analizza quale Paint Texture del Terrain è dominante nella posizione specificata
/// e ritorna "Grass" o "Ground" in base agli INDICI configurati
/// </summary>
private string GetDominantTerrainTextureType(Terrain terrain, Vector3 worldPosition)
{
    TerrainData terrainData = terrain.terrainData;
    Vector3 terrainPosition = terrain.transform.position;
    
    // ✅ Converti posizione world in coordinate della alphamap
    int mapX = (int)(((worldPosition.x - terrainPosition.x) / terrainData.size.x) * terrainData.alphamapWidth);
    int mapZ = (int)(((worldPosition.z - terrainPosition.z) / terrainData.size.z) * terrainData.alphamapHeight);
    
    // Clamp per sicurezza
    mapX = Mathf.Clamp(mapX, 0, terrainData.alphamapWidth - 1);
    mapZ = Mathf.Clamp(mapZ, 0, terrainData.alphamapHeight - 1);
    
    // ✅ Ottieni i pesi delle texture in questa posizione (1x1 pixel)
    float[,,] splatmapData = terrainData.GetAlphamaps(mapX, mapZ, 1, 1);
    
    // ✅ Trova l'indice della texture con il peso maggiore
    int dominantIndex = 0;
    float maxWeight = 0f;
    
    for (int i = 0; i < splatmapData.GetLength(2); i++)
    {
        if (splatmapData[0, 0, i] > maxWeight)
        {
            maxWeight = splatmapData[0, 0, i];
            dominantIndex = i;
        }
    }
    
    // ✅ DEBUG: Mostra quale texture è dominante
    if (debugTerrainTexture && Time.frameCount % 60 == 0)
    {
        string textureName = dominantIndex < terrainData.terrainLayers.Length 
            ? terrainData.terrainLayers[dominantIndex].name 
            : "Unknown";
        Debug.Log($"[Footsteps] 🎨 Texture dominante: Index={dominantIndex}, Name='{textureName}', Weight={maxWeight:F2}");
    }
    
    // ✅ Controlla se l'indice è in GRASS
    if (System.Array.IndexOf(grassTextureIndices, dominantIndex) >= 0)
    {
        if (debugTerrainTexture && Time.frameCount % 60 == 0)
            Debug.Log($"[Footsteps] ✅ Index {dominantIndex} = GRASS");
        return "Grass";
    }
    
    // ✅ Controlla se l'indice è in GROUND
    if (System.Array.IndexOf(groundTextureIndices, dominantIndex) >= 0)
    {
        if (debugTerrainTexture && Time.frameCount % 60 == 0)
            Debug.Log($"[Footsteps] ✅ Index {dominantIndex} = GROUND");
        return "Ground";
    }
    
    // ✅ Fallback: texture non configurata
    if (debugTerrainTexture && Time.frameCount % 60 == 0)
    {
        Debug.LogWarning($"[Footsteps] ⚠️ Texture Index {dominantIndex} NON configurato - usando Ground di default");
    }
    
    return "Ground"; // Default
}
    
    private void StopFootstepAudio()
    {
        if (footstepAudioSource != null && footstepAudioSource.isPlaying)
        {
            footstepAudioSource.Stop();
        }
        footstepTimer = 0f;
    }

    private void HandleAttackVelocity()
    {
        if (attackVelocity.magnitude > 0.01f)
        {
            attackVelocity = Vector3.Lerp(attackVelocity, Vector3.zero, Time.deltaTime * attackVelocityDecay);
            
            if (attackVelocity.magnitude < 0.01f)
                attackVelocity = Vector3.zero;
        }
    }

  private void HandleJumpInput()
{
    if (!isJumpEnabled || IsMovementLocked)
    {
        jumpBufferCounter = 0f;
        return;
    }
    
    // Processa il buffer solo se è attivo
    if (jumpBufferCounter > 0f)
    {
        if (debugJumpInBuild && Time.frameCount % 10 == 0)
        {
            Debug.Log($"[Jump] Buffer attivo: {jumpBufferCounter:F3}s");
        }
        
        // Riprova il salto
        if (TryExecuteJumpImmediate())
        {
            jumpBufferCounter = 0f; // Reset buffer dopo successo
            if (debugJumpInBuild)
                Debug.Log("[Jump] Eseguito da BUFFER");
        }
    }
}

   private void UpdateGroundedState()
{
    // ✅ Se siamo hanging, non aggiornare lo stato grounded
    if (hanging)
    {
        return;
    }
    
    bool grounded = controller.isGrounded;

    // Enhanced ground detection
    if (!grounded)
    {
        tempVector3.Set(transform.position.x, transform.position.y + 0.05f, transform.position.z);
        int hitCount = Physics.RaycastNonAlloc(tempVector3, Vector3.down, raycastHits, 0.15f); // Increased range

        if (hitCount > 0)
        {
            grounded = true;
        }
    }

    _animator.SetBool(IsGroundedHash, grounded);

    // ✅ IMPROVED: More reliable landing detection
    if (grounded && !wasGroundedLastFrame)
    {
        // Additional check: only call OnLanding if we were actually falling/jumping
        if (velocity.y <= 0.5f || _animator.GetBool(JumpHash) || _animator.GetBool(DoubleJumpHash))
        {
            OnLanding();
        }
    }
    // ✅ NEW: Check if animations should be reset while grounded
    else if (grounded && (_animator.GetBool(JumpHash) || _animator.GetBool(DoubleJumpHash)))
    {
        // If we're grounded but jump animations are still active, and we're not jumping upward
        if (velocity.y < 1f)
        {
            OnLanding();
        }
    }

    wasGroundedLastFrame = grounded;
}

    private void ReleaseLedgeGrab()
{
    if (!hanging) return;

    if (debugLedgeGrab)
        Debug.Log("[LedgeGrab] 🔽 Rilasciato dal ledge - SOLO tramite input manuale");

    hanging = false;
    isHangPositionStable = false;
    hangStabilityTimer = 0f;
    _animator.SetBool(HangingHash, false);
    
    
    // ✅ SALVA TEMPO DI RILASCIO PER COOLDOWN
        lastLedgeGrabTime = Time.time;

    // Inizia a cadere dolcemente
    velocity.y = -2f;
}
    private void OnLanding()
{
    jumpCount = 0;
    fallingTimer = 0f;
    lastJumpTime = 0f; // Reset jump timer

    // ✅ Force all jump-related animations to false
    _animator.SetBool(JumpHash, false);
    _animator.SetBool(DoubleJumpHash, false);
    _animator.SetBool(IsFallingHash, false);
    
    // ✅ Additional safety - set vertical velocity animation
    _animator.SetFloat(VerticalVelocityHash, 0f);
    
    Debug.Log("[ThirdPersonController] 🛬 Landing completed - all jump animations reset");
}

private void UpdateJumpTimers()
{
    // ✅ USA SEMPRE fixedDeltaTime per i timer fisici
    float deltaTime = Time.fixedDeltaTime;
    
    if (controller.isGrounded)
    {
        coyoteTimeCounter = coyoteTime;
    }
    else if (coyoteTimeCounter > 0f)
    {
        coyoteTimeCounter -= deltaTime;
        coyoteTimeCounter = Mathf.Max(0f, coyoteTimeCounter);
    }

    if (jumpBufferCounter > 0f)
    {
        jumpBufferCounter -= deltaTime;
        jumpBufferCounter = Mathf.Max(0f, jumpBufferCounter);
    }
}   private bool TryJump()
{
    if (!isJumpEnabled) return false;
    
    if (hanging)
    {
        if (jumpBufferCounter > 0f)
        {
            ExecuteJumpFromHang();
            return true;
        }
        return false;
    }
    
    // GROUND CHECK PIÙ DIRETTO
    bool grounded = controller.isGrounded;
    bool canJump = false;
    bool isFirstJump = false;

    // PRIMO SALTO - CONDIZIONI SEMPLIFICATE
    if (jumpCount == 0)
    {
        if (grounded || coyoteTimeCounter > 0)
        {
            canJump = true;
            isFirstJump = true;
        }
    }
    // MULTI JUMP
    else if (jumpCount < maxJumps && !grounded)
    {
        canJump = true;
        isFirstJump = false;
    }

    if (canJump)
    {
        ExecuteJump(isFirstJump);
        return true;
    }
    
    return false;
}

private void HandleMovement()
{
    // ⭐ CONTROLLO PRIORITARIO: Verifica camera PRIMA di tutto
    if (cameraTransform == null)
    {
        if (autoDetectActiveCamera)
        {
            // Forzatura immediata del rilevamento camera
            DetectActiveCamera();
        }
        
        // Se ancora null dopo il rilevamento, blocca completamente il movimento
        if (cameraTransform == null)
        {
            if (debugCameraChanges && Time.frameCount % 30 == 0) // Log ogni mezzo secondo circa
            {
                Debug.LogError("[ThirdPersonController] ❌ MOVIMENTO BLOCCATO: cameraTransform è null!");
            }
            
            playerVelocity = Vector3.zero;
            _animator.SetFloat(SpeedHash, 0f, 0.1f, Time.deltaTime);
            return;
        }
        else if (debugCameraChanges)
        {
            Debug.Log($"[ThirdPersonController] ✅ Camera recuperata durante HandleMovement: {cameraTransform.name}");
        }
    }
    
    // Blocca movimento durante stati speciali
    if (hanging || isClimbing || IsMovementLocked || !canMoveAfterHang)
    {
        playerVelocity = Vector3.zero;
        _animator.SetFloat(SpeedHash, 0f, 0.1f, Time.deltaTime);
        return;
    }

    Vector2 processedInput = GetProcessedMoveInput();
float h = processedInput.x;
float v = processedInput.y;

    Vector3 inputVector = new Vector3(h, 0f, v);
    float inputMagnitude = inputVector.magnitude;
    
    if (inputMagnitude > 1f)
    {
        inputVector.Normalize();
        inputMagnitude = 1f;
    }
    
    smoothInputMagnitude = Mathf.Lerp(smoothInputMagnitude, inputMagnitude, Time.fixedDeltaTime * 5f);

    if (inputMagnitude < 0.1f)
    {
        playerVelocity = Vector3.zero;
        _animator.SetFloat(SpeedHash, 0f, 0.1f, Time.fixedDeltaTime);
        return;
    }

    // ⭐ CALCOLO MOVIMENTO RELATIVO ALLA CAMERA - CON VERIFICA AGGIUNTIVA
    if (cameraTransform == null)
    {
        Debug.LogError("[ThirdPersonController] ❌ ERRORE CRITICO: cameraTransform è diventato null durante il calcolo!");
        playerVelocity = Vector3.zero;
        return;
    }
    
      // ✅ CALCOLO ANGOLO TARGET BASATO SULLA CAMERA
    // Questo garantisce che il movimento sia SEMPRE relativo alla camera
    float cameraYaw = cameraTransform.eulerAngles.y;
    float inputAngle = Mathf.Atan2(inputVector.x, inputVector.z) * Mathf.Rad2Deg;
    float targetAngle = inputAngle + cameraYaw;
    
    // ✅ SMOOTH ROTATION verso l'angolo target
    float smoothedAngle = Mathf.SmoothDampAngle(
        transform.eulerAngles.y, 
        targetAngle, 
        ref rotationVelocity, 
        rotationSmoothTime
    );
    transform.rotation = Quaternion.Euler(0f, smoothedAngle, 0f);

    // ✅ DIREZIONE MOVIMENTO = Quaternion basato sull'angolo target
    Vector3 moveDirection = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
    
    // Proietta sul piano del terreno
    moveDirection = Vector3.ProjectOnPlane(moveDirection, GetGroundNormal());
    moveDirection.Normalize();

    // ✅ CALCOLO VELOCITÀ TARGET
    float targetSpeed = isSprinting ? sprintSpeed : (smoothInputMagnitude < 0.5f ? walkSpeed : runSpeed);
    playerVelocity = moveDirection * targetSpeed;

    // ✅ AGGIORNA ANIMATORE
    Vector3 totalVelocity = playerVelocity + attackVelocity;
    float speedNormalized = Mathf.Clamp01(totalVelocity.magnitude / sprintSpeed);
    _animator.SetFloat(SpeedHash, speedNormalized, 0.1f, Time.fixedDeltaTime);
    
    // ⭐ DEBUG MIGLIORATO: Mostra le informazioni più importanti
    if (debugCameraChanges && inputMagnitude > 0.1f && Time.frameCount % 30 == 0)
    {
        Debug.Log($"[Movement] Camera: {cameraTransform.name} | TargetAngle: {targetAngle:F1}° | Speed: {targetSpeed:F1} | Input: {inputVector}");
        Debug.DrawLine(transform.position, transform.position + moveDirection * 2f, Color.green, 0.5f);
        Debug.DrawLine(cameraTransform.position, cameraTransform.position + cameraTransform.forward * 3f, Color.blue, 0.5f);
    }
}


/// <summary>
/// Esegue il salto da ledge grab
/// </summary>
private void ExecuteJumpFromHang()
{   
    if (debugLedgeGrab)
        Debug.Log("[LedgeGrab] 🚀 Saltando dal ledge...");
    
    // Termina hanging
    hanging = false;
    isHangPositionStable = false;
    hangStabilityTimer = 0f;
    _animator.SetBool(HangingHash, false);
    
    // ✅ SALVA TEMPO DI RILASCIO PER COOLDOWN
        lastLedgeGrabTime = Time.time;
    
    // ✅ MOVIMENTO VERSO L'ESTERNO DAL MURO
    // hangForward punta verso il muro, quindi -hangForward ci allontana
    Vector3 jumpOffset = -hangForward * 0.4f + Vector3.up * 0.1f;
    controller.Move(jumpOffset);
    
    // Salto normale verso l'alto
    velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
    
    // ✅ IMPULSO IN DIREZIONE OPPOSTA AL MURO (allontanandosi)
    Vector3 jumpDirection = -hangForward * 3f;
    playerVelocity = new Vector3(jumpDirection.x, 0f, jumpDirection.z);
    
    // Reset jump count e animazioni
    jumpCount = 1;
    _animator.SetBool(JumpHash, true);
    _animator.SetBool(DoubleJumpHash, false);
    
    // Disabilita movimento temporaneamente
    StartCoroutine(EnableMovementAfterHangJump());
    
    // Riproduci suono del salto
    PlayJumpSound();
    
    if (debugLedgeGrab)
    {
        Debug.Log($"[LedgeGrab] ✅ Salto completato! Direzione: {-hangForward}");
        Debug.DrawLine(transform.position, transform.position + jumpDirection, Color.cyan, 2f);
    }
}

/// <summary>
/// Riabilita il movimento dopo un breve delay dal salto da hang
/// </summary>
private IEnumerator EnableMovementAfterHangJump()
{
    canMoveAfterHang = false;
    yield return new WaitForSeconds(hangDelayAfterJump);
    canMoveAfterHang = true;
}


private void HandleJump()
{
    bool grounded = controller.isGrounded;
    
    if (hanging || isClimbing)
    {
        velocity = Vector3.zero;
        return;
    }
    
    // VALORE FISSO PER GROUNDED
    if (grounded && velocity.y < 0)
    {
        velocity.y = -2f; // VALORE FISSO INVECE DI VARIABILE
    }

    ApplyGravity();
    UpdateJumpAnimations();
}


  private bool IsGroundedAccurate()
{
    // 1. Controlla prima il built-in isGrounded
    if (controller.isGrounded) 
    {
        if (debugGroundStability && Time.frameCount % 60 == 0)
            Debug.Log("[Ground] CharacterController.isGrounded = TRUE");
        return true;
    }
    
    // 2. Multi-point ground detection per piattaforme mobili
    Vector3[] checkPoints = {
        transform.position,
        transform.position + transform.forward * 0.2f,
        transform.position - transform.forward * 0.2f,
        transform.position + transform.right * 0.2f,
        transform.position - transform.right * 0.2f
    };
    
    float groundCheckDistance = 0.15f; // Ridotto da 0.2f
    int groundHits = 0;
    
    foreach (Vector3 point in checkPoints)
    {
        Vector3 rayStart = point + Vector3.up * 0.05f; // Partenza più bassa
        
        if (Physics.Raycast(rayStart, Vector3.down, groundCheckDistance, platformLayers))
        {
            groundHits++;
            
            // Se almeno 2 punti toccano terra, consideraci grounded
            if (groundHits >= 2)
            {
                if (debugGroundStability && Time.frameCount % 60 == 0)
                    Debug.Log($"[Ground] Multi-point detection: {groundHits} hit points");
                return true;
            }
        }
    }
    
    // 3. Check speciale per piattaforme in movimento
    if (currentPlatform != null)
    {
        // Verifica distanza dalla piattaforma
        Collider platformCollider = currentPlatform.GetComponent<Collider>();
        if (platformCollider != null)
        {
            Vector3 closestPoint = platformCollider.ClosestPoint(transform.position);
            float distanceToSurface = Vector3.Distance(transform.position, closestPoint);
            
            // Se siamo molto vicini alla superficie della piattaforma
            if (distanceToSurface <= 0.5f) // Character Controller radius + tolleranza
            {
                // Verifica che siamo sopra la piattaforma (non sotto o di lato)
                if (transform.position.y >= closestPoint.y - 0.2f)
                {
                    if (debugGroundStability && Time.frameCount % 60 == 0)
                        Debug.Log($"[Ground] Platform proximity check: distanza {distanceToSurface:F3}");
                    return true;
                }
            }
        }
    }
    
    // 4. Fallback: Sphere cast per superfici irregolari
    if (Physics.SphereCast(
        transform.position + Vector3.up * 0.1f, 
        0.35f, // Raggio leggermente più piccolo del Character Controller
        Vector3.down, 
        out RaycastHit sphereHit, 
        0.2f, 
        platformLayers))
    {
        if (debugGroundStability && Time.frameCount % 60 == 0)
            Debug.Log("[Ground] Sphere cast detection");
        return true;
    }
    
    if (debugGroundStability && Time.frameCount % 60 == 0)
        Debug.Log("[Ground] Tutti i check falliti - NOT GROUNDED");
    
    return false;
}
private void UpdateGroundedStateForPlatforms()
{
    bool wasGrounded = _animator.GetBool(IsGroundedHash);
    bool isGroundedNow = IsGroundedAccurate();
    
    // ✅ STABILIZZAZIONE DELLO STATO GROUNDED SU PIATTAFORME MOBILI
    if (currentPlatform != null && !isGroundedNow && wasGrounded)
    {
        // Incrementa il contatore di frame instabili
        unstableFrames++;
        
        // Mantieni grounded per alcuni frame extra per evitare flickering
        if (unstableFrames < groundStabilityFrames)
        {
            isGroundedNow = true;
            
            if (debugGroundStability)
            {
                Debug.Log($"[Ground] Frame instabile {unstableFrames}/{groundStabilityFrames} - mantengo grounded");
            }
        }
        else
        {
            // Dopo troppi frame instabili, accetta la perdita di ground
            unstableFrames = 0;
            if (debugGroundStability)
            {
                Debug.Log("[Ground] Troppi frame instabili - accetto perdita di ground");
            }
        }
    }
    else if (isGroundedNow || currentPlatform == null)
    {
        // Reset del contatore se siamo effettivamente grounded o non su piattaforma
        unstableFrames = 0;
    }
    
    // ✅ PREVENZIONE OSCILLAZIONE RAPIDA
    if (isGroundedNow != lastGroundCheckResult)
    {
        if (debugGroundStability)
        {
            Debug.Log($"[Ground] Cambio stato: {lastGroundCheckResult} → {isGroundedNow} (Platform: {(currentPlatform ? currentPlatform.name : "none")})");
        }
    }
    
    lastGroundCheckResult = isGroundedNow;
    _animator.SetBool(IsGroundedHash, isGroundedNow);
    
    // Landing detection più accurata
    if (isGroundedNow && !wasGrounded)
    {
        if (velocity.y <= 0.5f || _animator.GetBool(JumpHash) || _animator.GetBool(DoubleJumpHash))
        {
            OnLanding();
            
            if (debugGroundStability)
            {
                Debug.Log("[Ground] Landing rilevato");
            }
        }
    }
}    private void UpdateJumpAnimations()
    {
        _animator.SetFloat(VerticalVelocityHash, velocity.y);
    }

    private void ApplyGravity()
{
    // ✅ USA fixedDeltaTime invece di deltaTime
    float dt = Time.fixedDeltaTime;
    
    if (velocity.y < 0)
    {
        velocity.y += gravity * 2.5f * dt;
    }
    else if (velocity.y > 0 && !isHoldingJump)
    {
        velocity.y += gravity * 2f * dt;
    }
    else
    {
        velocity.y += gravity * dt;
    }
}

    private void HandleSprintFX()
    {
        if (sprintFX == null) return;

        bool isMoving = playerVelocity.sqrMagnitude > 0.01f;
        bool shouldShow = isSprinting && controller.isGrounded && isMoving;

        if (shouldShow && !sprintFXActive)
        {
            sprintFX.PlayEffect();
            sprintFXActive = true;
        }
        else if (!shouldShow && sprintFXActive)
        {
            sprintFX.StopEffect();
            sprintFXActive = false;
        }
    }
// ✅ AGGIUNGI NUOVO METODO PER GESTIRE MEGLIO LE PIATTAFORME IN MOVIMENTO
/// <summary>
/// Verifica se dovremmo sganciare dalla piattaforma basandoci sulla velocità
/// </summary>
private bool ShouldDetachFromPlatform()
{
    if (currentPlatform == null) return false;
    
    // Sgancia se stiamo saltando verso l'alto con velocità significativa
    if (velocity.y > 2f) return true;
    
    // Sgancia se siamo troppo lontani dalla piattaforma
    if (!IsPlatformValid()) return true;
    
    // Sgancia se è una piattaforma ostacolo che vuole detachare
    if (isOnObstaclePlatform && currentRotatingObject != null && 
        currentRotatingObject.GetDetachPlayerOnHit()) return true;
    
    return false;
}
    private float fallingCheckTimer = 0f;
    private const float FALLING_CHECK_INTERVAL = 0.1f;

    private void HandleFalling()
    {
        fallingCheckTimer += Time.deltaTime;
        if (fallingCheckTimer < FALLING_CHECK_INTERVAL) return;
        fallingCheckTimer = 0f;
        
        bool grounded = controller.isGrounded;
        bool isDescending = velocity.y < -3f;

        if (grounded || velocity.y > -1f)
        {
            fallingTimer = 0f;
            if (_animator.GetBool(IsFallingHash))
                _animator.SetBool(IsFallingHash, false);
            return;
        }

        if (isDescending)
        {
            fallingTimer += FALLING_CHECK_INTERVAL;

            bool shouldFall = fallingTimer >= fallingTimeThreshold && 
                             !IsNearGroundBelow() && 
                             !_animator.GetBool(JumpHash) && 
                             !_animator.GetBool(DoubleJumpHash);

            if (shouldFall && !_animator.GetBool(IsFallingHash))
            {
                _animator.SetBool(IsFallingHash, true);
            }
        }
    }

    private bool IsNearGroundBelow()
    {
        tempVector3.Set(transform.position.x, transform.position.y + 0.1f, transform.position.z);
        int hitCount = Physics.RaycastNonAlloc(tempVector3, Vector3.down, raycastHits, 0.3f);
        return hitCount > 0;
    }
private void HandleAirControl()
{
    // ✅ NON APPLICARE AIR CONTROL DURANTE HANGING
    if (controller.isGrounded || IsMovementLocked || hanging || !canMoveAfterHang) return;

    // ✅ VERIFICA CHE LA CAMERA SIA VALIDA ANCHE PER AIR CONTROL
    if (cameraTransform == null) return;

    Vector3 tempVector3 = new Vector3(moveInput.x, 0f, moveInput.y);
    float inputMag = tempVector3.magnitude;
    
    if (inputMag < 0.1f) return;
    
    if (inputMag > 1f)
    {
        tempVector3.Normalize();
    }

    float targetAngle = Mathf.Atan2(tempVector3.x, tempVector3.z) * Mathf.Rad2Deg + cameraTransform.eulerAngles.y;
    float smoothedAngle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref rotationVelocity, airRotationSmoothTime);
    transform.rotation = Quaternion.Euler(0f, smoothedAngle, 0f);

    Vector3 moveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
    
    tempVector3.Set(moveDir.x * airControlSpeed, 0f, moveDir.z * airControlSpeed);
    playerVelocity += tempVector3;
}
    public void Respawn()
{
    // ✅ ASSICURATI CHE IL CHARACTERCONTROLLER SIA ABILITATO
    if (!controller.enabled)
    {
        controller.enabled = true;
    }

    controller.enabled = false;

    Transform spawnPoint = GetRespawnPoint();
    Vector3 oldPosition = transform.position; // ✅ SALVA POSIZIONE PRECEDENTE

    transform.position = spawnPoint.position;
    transform.rotation = Quaternion.Euler(0f, 0f, 0f);

    velocity = Vector3.zero;
    attackVelocity = Vector3.zero;

    // ✅ RESET LEDGE GRAB STATE
    hanging = false;
    canMoveAfterHang = true;

    // Sgancia dalla piattaforma durante il respawn
    DetachFromCurrentPlatform();

    if (playerCamera != null)
    {
        CinemachineCore.ResetCameraState();
    }
    else
    {
        Debug.LogWarning("[ThirdPersonController] playerCamera non assegnata - impossibile resettare camera");
    }

    controller.enabled = true;

    _animator.SetBool(JumpHash, false);
    _animator.SetBool(DoubleJumpHash, false);
    _animator.SetBool(IsFallingHash, false);
    _animator.SetBool(IsGroundedHash, true);

    jumpCount = 0;
    coyoteTimeCounter = 0f;
    jumpBufferCounter = 0f;
    fallingTimer = 0f;
    wasGroundedLastFrame = true;

    StopAllCoroutines();
    if (sprintFX) sprintFX.StopEffect();
    sprintFXActive = false;
    StopFootstepAudio();

    IsMovementLocked = false;

    Debug.Log($"[ThirdPersonController] Respawn completato alla posizione: {spawnPoint.position}");

    // ✅ NUOVO: NOTIFICA TUTTI I SISTEMI DEL RESPAWN
    OnPlayerRespawned?.Invoke(spawnPoint.position, spawnPoint.rotation);

    // ✅ NUOVO: NOTIFICA ANCHE CHECKPOINTMANAGER SE PRESENTE
    CheckpointManager checkpointManager = CheckpointManager.Instance;
    if (checkpointManager != null)
    {
        // Forza la notifica alle zattere attraverso il CheckpointManager
        checkpointManager.OnPlayerCheckpointChanged?.Invoke(spawnPoint.position);
    }

    // ✅ NUOVO: NOTIFICA TUTTE LE RAFT PLATFORMS DIRETTAMENTE
    NotifyAllRaftPlatformsOfRespawn(oldPosition, spawnPoint.position);
}


 private void NotifyAllRaftPlatformsOfRespawn(Vector3 oldPosition, Vector3 newPosition)
{
    RaftPlatform[] rafts = FindObjectsByType<RaftPlatform>(FindObjectsSortMode.None);

    foreach (var raft in rafts)
    {
        // Calcola distanza per determinare se la zattera dovrebbe reagire
        float distanceFromOld = Vector3.Distance(raft.transform.position, oldPosition);
        float distanceFromNew = Vector3.Distance(raft.transform.position, newPosition);

        // Se la zattera era vicina al player prima del respawn, o è vicina ora
        if (distanceFromOld <= 100f || distanceFromNew <= 100f)
        {
            // Chiama il metodo pubblico della zattera per notificare il respawn
            raft.UpdatePlayerCheckpointPosition(newPosition);

            // Se la zattera è lontana dal nuovo spawn point, forzala a tornare
            if (distanceFromNew > 50f)
            {
                raft.ForceReturnToNearestTerminal();
            }
        }
    }

    if (debugRespawnSystem)
    {
        Debug.Log($"[ThirdPersonController] Notificate {rafts.Length} zattere del respawn: {oldPosition} → {newPosition}");
    }
}
public void TeleportTo(Vector3 position, Quaternion rotation)
{
    Vector3 oldPosition = transform.position;
    
    controller.enabled = false;
    transform.position = position;
    transform.rotation = rotation;
    controller.enabled = true;
    
    // Reset velocità
    velocity = Vector3.zero;
    attackVelocity = Vector3.zero;
    
    // Sgancia dalle piattaforme
    DetachFromCurrentPlatform();
    
    // Notifica eventi
    OnPlayerRespawned?.Invoke(position, rotation);
    
    // Notifica zattere
    NotifyAllRaftPlatformsOfRespawn(oldPosition, position);
    
    Debug.Log($"[ThirdPersonController] Teleport: {oldPosition} → {position}");
}


/// <summary>
/// Abilita/disabilita il sistema di ledge grab
/// </summary>
public void SetLedgeGrabEnabled(bool enabled)
{
    ledgeGrabEnabled = enabled;
    
    if (!enabled && hanging)
    {
        // Se disabilitiamo durante hanging, forza il rilascio
        hanging = false;
        canMoveAfterHang = true;
    }
    
    Debug.Log($"[LedgeGrab] Sistema {(enabled ? "abilitato" : "disabilitato")}");
}
/// <summary>
/// Verifica se il player è attualmente appeso
/// </summary>
public bool IsHanging()
{
    return hanging;
}

/// <summary>
/// Forza il rilascio dal ledge (per eventi speciali)
/// </summary>
public void ForceReleaseLedge()
{
    if (hanging)
    {
        ReleaseLedgeGrab();
        canMoveAfterHang = true;
        
        Debug.Log("[LedgeGrab] Rilascio forzato dal ledge");
    }
}
/// <summary>
/// Ottieni informazioni sullo stato del ledge grab
/// </summary>
public string GetLedgeGrabInfo()
{
    if (!ledgeGrabEnabled) return "Ledge Grab disabilitato";
    if (hanging) return $"Appeso al ledge - Posizione: {hangPosition}";
    if (!canMoveAfterHang) return "Delay dopo salto da ledge";
    return "Ledge Grab pronto";
}

    private Transform GetRespawnPoint()
    {
        CheckpointManager checkpointManager = CheckpointManager.Instance;
        if (checkpointManager == null)
        {
            checkpointManager = FindFirstObjectByType<CheckpointManager>();
        }
        
        if (checkpointManager != null)
        {
            Vector3 spawnPos = checkpointManager.GetCurrentSpawnPosition();
            Quaternion spawnRot = checkpointManager.GetCurrentSpawnRotation();
            
            GameObject tempSpawn = new GameObject("TempRespawnPoint");
            tempSpawn.transform.position = spawnPos;
            tempSpawn.transform.rotation = spawnRot;
            
            if (checkpointManager.HasActiveCheckpoint())
            {
                Debug.Log($"[ThirdPersonController] ✅ Respawn al checkpoint: '{checkpointManager.GetCurrentCheckpoint()}' - {spawnPos}");
            }
            else
            {
                Debug.Log($"[ThirdPersonController] ✅ Respawn al default spawn point: {spawnPos}");
            }
            
            return tempSpawn.transform;
        }
        
        Debug.LogError("[ThirdPersonController] ❌ CHECKPOINTMANAGER NON TROVATO!");
        Debug.LogError("Devi aggiungere un CheckpointManager alla scena per il sistema di respawn!");
        Debug.LogWarning("Usando posizione corrente come fallback...");
        
        return transform;
    }

    // ✅ GESTIONE COLLISIONI OTTIMIZZATA - ORA DEPRECATA (usiamo il nuovo sistema di rilevamento)
   void OnControllerColliderHit(ControllerColliderHit hit)
{
    // Throttle collision processing
    if (Time.time - lastCollisionLogTime < COLLISION_LOG_THROTTLE) return;
    lastCollisionLogTime = Time.time;
}

    public void ApplyExternalPush(Vector3 force) => externalPush += force;

    public void Heal(float amount)
    {
        currentHealth = Mathf.Min(currentHealth + amount, MaxHealth);
        UpdateHealthUI();
    }

    private void UpdateHealthUI()
    {
        if (playerUI == null)
        {
            playerUI = PlayerUI.Instance ?? FindFirstObjectByType<PlayerUI>();
            if (playerUI == null) return;
        }

        playerUI.UpdateHealth(currentHealth);
    }

    private float lastDamageTime = 0f;
    private const float DAMAGE_COOLDOWN = 0.1f;

    public void TakeDamage(float amount)
    {
        if (Time.time - lastDamageTime < DAMAGE_COOLDOWN) return;
        lastDamageTime = Time.time;
        
        if (currentHealth <= 0) return;

        float oldHealth = currentHealth;
        currentHealth = Mathf.Max(0, currentHealth - amount);
        
        UpdateHealthUI();

        if (currentHealth <= 0 && oldHealth > 0)
        {
            IsMovementLocked = true;
            _animator.SetFloat(SpeedHash, 0f);

            if (ShouldPlayHitReal())
                _animator.SetTrigger(HitRealHash);
            else
            {
                _animator.SetTrigger(HitHash);
                StartCoroutine(QuickRespawn());
            }
        }
        else if (currentHealth > 0)
        {
            PlayerAttack playerAttack = GetComponentInChildren<PlayerAttack>();
            bool isSwinging = playerAttack != null && playerAttack.isAttacking;
            if (!isSwinging)
                _animator.SetTrigger(HitHash);
        }
    }

    private IEnumerator QuickRespawn()
    {
        yield return new WaitForSeconds(0.1f);
        Respawn();
        currentHealth = MaxHealth;
        playerUI.UpdateHealth(currentHealth);
        IsMovementLocked = false;
    }

    public bool ShouldPlayHitReal()
    {
        bool isFalling = !_animator.GetBool(IsGroundedHash) && velocity.y < -2f;
        bool isJumping = _animator.GetBool(JumpHash) || _animator.GetBool(DoubleJumpHash);
        return !isFalling && !isJumping;
    }

    public void OnHitRealEnd()
    {
        Respawn();
        currentHealth = MaxHealth;
        playerUI.UpdateHealth(currentHealth);
        IsMovementLocked = false;
    }

    // OTTIMIZZAZIONE: Cache per normal del terreno
    private Vector3 cachedGroundNormal = Vector3.up;
    private float lastGroundNormalCheck = 0f;
    private const float GROUND_NORMAL_CHECK_INTERVAL = 0.2f;

    private Vector3 GetGroundNormal()
    {
        if (controller.isGrounded)
        {
            if (Time.time - lastGroundNormalCheck > GROUND_NORMAL_CHECK_INTERVAL)
            {
                lastGroundNormalCheck = Time.time;
                
                tempVector3.Set(transform.position.x, transform.position.y + 0.1f, transform.position.z);
                int hitCount = Physics.RaycastNonAlloc(tempVector3, Vector3.down, raycastHits, 1.5f);
                
                if (hitCount > 0)
                {
                    cachedGroundNormal = raycastHits[0].normal;
                }
                else
                {
                    cachedGroundNormal = Vector3.up;
                }
            }
        }
        return cachedGroundNormal;
    }

    // ✅ METODI PUBBLICI PER GESTIONE PIATTAFORME
    
    /// <summary>
    /// Sgancia il player solo se è su una piattaforma specifica
    /// </summary>
    /// <param name="platform">La piattaforma da cui sganciare</param>
    public void DetachFromPlatform(Transform platform)
    {
        if (currentPlatform == platform)
        {
            DetachFromCurrentPlatform();
        }
    }
    /// <summary>
/// Gestisce le collisioni provenienti dai child colliders
/// </summary>
/// <param name="collision">Dati della collisione</param>
/// <param name="childTransform">Transform del figlio che ha generato la collisione</param>
public void HandleChildCollision(Collision collision, Transform childTransform)
{
    Debug.Log($"[ThirdPersonController] Collisione ricevuta dal child {childTransform.name}");
    
    // Verifica se la collisione viene da un RotatingObject
    RotatingObject rotatingObject = childTransform.GetComponentInParent<RotatingObject>();
    if (rotatingObject != null)
    {
        // Lascia che il RotatingObject gestisca la collisione
        rotatingObject.HandleChildCollision(collision, childTransform);
    }
}
/// <summary>
/// Gestisce i trigger provenienti dai child colliders
/// </summary>
/// <param name="other">Collider che ha attivato il trigger</param>
/// <param name="childTransform">Transform del figlio che ha generato il trigger</param>
public void HandleChildTrigger(Collider other, Transform childTransform)
{
    Debug.Log($"[ThirdPersonController] Trigger ricevuto dal child {childTransform.name}");
    
    // Verifica se il trigger viene da un RotatingObject
    RotatingObject rotatingObject = childTransform.GetComponentInParent<RotatingObject>();
    if (rotatingObject != null)
    {
        // Lascia che il RotatingObject gestisca il trigger
        rotatingObject.HandleChildTrigger(other, childTransform);
    }
}

    /// <summary>
    /// Forza l'attacco a una piattaforma specifica (per casi speciali)
    /// </summary>
    /// <param name="platform">La piattaforma a cui attaccarsi</param>
    public void ForceAttachToPlatform(Transform platform)
    {
        if (platform != null && IsPlatformTag(platform.tag))
        {
            DetachFromCurrentPlatform();
            AttachToPlatform(platform);
        }
    }

    /// <summary>
    /// Verifica se il player è attualmente su una piattaforma
    /// </summary>
    /// <returns>True se è su una piattaforma</returns>
    public bool IsOnPlatform()
    {
        return currentPlatform != null && !isOnObstaclePlatform;
    }

    /// <summary>
    /// Ottieni la piattaforma corrente
    /// </summary>
    /// <returns>Transform della piattaforma corrente o null</returns>
    public Transform GetCurrentPlatform()
    {
        return currentPlatform;
    }

    /// <summary>
    /// Verifica se il player è su una piattaforma ostacolo
    /// </summary>
    /// <returns>True se è su una piattaforma ostacolo</returns>
    public bool IsOnObstaclePlatform()
    {
        return isOnObstaclePlatform;
    }
    // AGGIUNGI QUESTO GETTER PUBBLICO (dopo gli altri getter pubblici)
public bool GetAutoDetectCamera()
{
    return autoDetectActiveCamera;
}

    /// <summary>
    /// Ottieni informazioni sulla piattaforma corrente
    /// </summary>
    /// <returns>Stringa con informazioni sulla piattaforma</returns>
    public string GetPlatformInfo()
    {
        if (currentPlatform == null) return "Nessuna piattaforma";

        string platformType = currentMovingPlatform ? "Moving" :
                             currentRaftPlatform ? "Raft" :
                             currentRotatingObject ? "Rotating" : "Unknown";

        return $"{currentPlatform.name} (Tipo: {platformType}, Obstacle: {isOnObstaclePlatform})";
    }
private void UpdateCurrentCameraSettings()
{
    if (!enableMovementInversion) 
    {
        currentCameraSettings = null;
        movementInverted = false;
        return;
    }
    
    CameraMovementSettings newSettings = null;
    
    // Prima prova con CinemachineCamera
    if (useCameraManagerIntegration)
    {
        CameraManager cameraManager = FindFirstObjectByType<CameraManager>();
        if (cameraManager != null && cameraManager.GetActiveCamera() != null)
        {
            CinemachineCamera activeCinemachine = cameraManager.GetActiveCamera();
            newSettings = cameraSettings.Find(cs => cs.cinemachineCamera == activeCinemachine);
        }
    }
    
    // Se non trovato, prova con Unity Camera
    if (newSettings == null && currentActiveCamera != null)
    {
        newSettings = cameraSettings.Find(cs => cs.unityCamera == currentActiveCamera);
    }
    
    // Se non trovato, prova per nome
    if (newSettings == null && currentActiveCamera != null)
    {
        string cameraName = currentActiveCamera.name;
        newSettings = cameraSettings.Find(cs => cs.cameraName.Equals(cameraName, System.StringComparison.OrdinalIgnoreCase));
    }
    
    if (newSettings != currentCameraSettings)
    {
        currentCameraSettings = newSettings;
        movementInverted = newSettings != null && (newSettings.invertForwardBackward || newSettings.invertLeftRight);
        
        if (debugMovementInversion)
        {
            if (newSettings != null)
            {
                Debug.Log($"[Movement] Camera: {newSettings.cameraName} - FB: {newSettings.invertForwardBackward}, LR: {newSettings.invertLeftRight}");
            }
            else
            {
                Debug.Log("[Movement] Nessuna inversione per questa camera");
            }
        }
    }
}

    private Vector2 GetProcessedMoveInput()
    {
        Vector2 processedInput = moveInput;

        if (enableMovementInversion && currentCameraSettings != null)
        {
            if (currentCameraSettings.invertForwardBackward)
            {
                processedInput.y = -processedInput.y;
            }

            if (currentCameraSettings.invertLeftRight)
            {
                processedInput.x = -processedInput.x;
            }

            if (debugMovementInversion && processedInput != moveInput && Time.frameCount % 30 == 0)
            {
                Debug.Log($"[Movement] Input: {moveInput} → {processedInput}");
            }
        }

        return processedInput;
    }
private void FixedUpdate()
{
    // TUTTA LA FISICA QUI
    DetectAndUpdatePlatform();
    UpdateJumpTimers();
    HandleJumpInput();
    HandleLedgeGrab();
    HandleJump();
    HandleAttackVelocity();
    HandleMovement();
    ApplyAllMovement();
    HandleFalling();
    CheckForLedgeRelease();
    HandleAirControl();
    UpdateGroundedStateForPlatforms();
    
    // Decay dei push esterni
    externalPush = Vector3.Lerp(externalPush, Vector3.zero, Time.fixedDeltaTime * pushRecoverySpeed);
}

[ContextMenu("Auto Setup Current Cameras")]
public void AutoSetupCurrentCameras()
{
    cameraSettings.Clear();
    
    CinemachineCamera[] cinemachineCameras = FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None);
    foreach (var cam in cinemachineCameras)
    {
        var newSettings = new CameraMovementSettings();
        newSettings.cinemachineCamera = cam;
        newSettings.cameraName = cam.name;
        cameraSettings.Add(newSettings);
    }
    
    Camera[] unityCameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
    foreach (var cam in unityCameras)
    {
        if (!cameraSettings.Exists(cs => cs.unityCamera == cam))
        {
            var newSettings = new CameraMovementSettings();
            newSettings.unityCamera = cam;
            newSettings.cameraName = cam.name;
            cameraSettings.Add(newSettings);
        }
    }
    
    Debug.Log($"[Movement] Setup completato: {cameraSettings.Count} camere");
}
    private void OnDestroy()
{
    // ✅ FERMA LA COROUTINE DI CLIMBING
    if (climbHeightCoroutine != null)
    {
        StopCoroutine(climbHeightCoroutine);
        climbHeightCoroutine = null;
    }
    
    // Cleanup esistente
    if (controls != null)
    {
        controls.Gameplay.Move.performed -= OnMovePerformed;
        controls.Gameplay.Move.canceled -= OnMoveCanceled;
        controls.Gameplay.Sprint.performed -= OnSprintPerformed;
        controls.Gameplay.Sprint.canceled -= OnSprintCanceled;
        controls.Gameplay.Jump.started -= OnJumpStarted;
        controls.Gameplay.Jump.canceled -= OnJumpCanceled;
        controls.Dispose();
    }
}
// Replace the OnDrawGizmosSelected method (around line 3020-3070):

#if UNITY_EDITOR || DEVELOPMENT_BUILD
private void OnDrawGizmosSelected()
{
    if (!debugPlatformMovement || !Application.isPlaying) return;
    
    // Disegna il raggio di rilevamento piattaforme
    Gizmos.color = Color.cyan;
    Gizmos.DrawWireSphere(transform.position, platformDetectionRadius);
    
    // Disegna la piattaforma corrente
    if (currentPlatform != null)
    {
        Gizmos.color = isOnObstaclePlatform ? Color.red : Color.green;
        Gizmos.DrawLine(transform.position, currentPlatform.position);
        Gizmos.DrawWireCube(currentPlatform.position, Vector3.one * 2f);
        
        if (debugPlatformMovement && Time.frameCount % 60 == 0)
            Debug.Log($"[Platform Debug] {GetPlatformInfo()}");
    }
    
    // Disegna i delta di movimento
    if (platformDeltaPosition.magnitude > 0.001f)
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, transform.position + platformDeltaPosition * 10f);
    }

    // ✅ DISEGNA INFO CAMERA ATTIVA E CAMERAMANAGER - FIXED SCOPE
    if (currentActiveCamera != null)
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(currentActiveCamera.transform.position, 1f);
        Gizmos.DrawLine(transform.position, currentActiveCamera.transform.position);
        
        string cameraInfo = $"Camera Attiva: {currentActiveCamera.name}\nAuto-detect: {autoDetectActiveCamera}";
        
        
        CameraManager cameraManager = FindFirstObjectByType<CameraManager>();
if (useCameraManagerIntegration && cameraManager != null && cameraManager.GetActiveCamera() != null)
{
    cameraInfo += $"\nCinemachine: {cameraManager.GetActiveCamera().name}\nPriorità: {cameraManager.GetActiveCamera().Priority}";
    
    // Disegna anche la CinemachineCamera
    Gizmos.color = Color.cyan;
    Gizmos.DrawWireCube(cameraManager.GetActiveCamera().transform.position, Vector3.one * 0.5f);
}
        
        if (debugCameraChanges && Time.frameCount % 60 == 0)
            Debug.Log($"[Camera Debug] {cameraInfo}");
    }
    
    // Ledge grab debug
    if (debugLedgeGrab && Application.isPlaying)
    {
        Vector3 playerForward = transform.forward;
        
        // Disegna i raggi di rilevamento ledge con direzione corretta
        if (!hanging && velocity.y < -2f)
        {
            // Ray verso il basso
            Vector3 lineDownStart = (transform.position + Vector3.up * 1.5f) + playerForward * ledgeDetectionDistance;
            Vector3 lineDownEnd = (transform.position + Vector3.up * 0.7f) + playerForward * ledgeDetectionDistance;
            Gizmos.color = Color.red;
            Gizmos.DrawLine(lineDownStart, lineDownEnd);
            
            // Ray in avanti
            Vector3 lineFwdStart = new Vector3(transform.position.x, transform.position.y, transform.position.z);
            Vector3 lineFwdEnd = lineFwdStart + playerForward * (ledgeDetectionDistance + 0.3f);
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(lineFwdStart, lineFwdEnd);
            
            // Mostra direzione forward del player
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(transform.position, transform.position + playerForward * 2f);
        }
        
        // Disegna la posizione di hang e orientamento
        if (hanging)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(hangPosition, 0.3f);
            
            // Direzione verso il muro (hangForward)
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, transform.position + hangForward * 1.5f);
            
            // Direzione del player (verso dove guarda)
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 1f);
        }
    }
}
#endif

}