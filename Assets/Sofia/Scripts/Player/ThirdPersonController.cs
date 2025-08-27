using UnityEngine;
using UnityEngine.InputSystem;
using CartoonFX;
using System.Collections;
using Unity.Cinemachine;

[RequireComponent(typeof(CharacterController))]
public class ThirdPersonController : MonoBehaviour
{
    [Header("Camera Reset")]
    public CinemachineCamera playerCamera;
  
    [Header("UI Effect")]
    public PlayerUI playerUI;
    public UIEffectHandler attackEffectUI;

    [Header("Movement Settings")]
    public float walkSpeed = 2f;
    public float runSpeed = 5f;
    public float sprintSpeed = 8f;
    public float rotationSmoothTime = 0.1f;
    private float rotationVelocity;
    private float smoothInputMagnitude;

    // ✅ NUOVO SISTEMA DI GESTIONE CAMERA DINAMICA CON INTEGRAZIONE CAMERAMANAGER
    [Header("Camera Management")]
    [SerializeField] private bool autoDetectActiveCamera = true;
    [SerializeField] private float cameraCheckInterval = 0.1f;
    [SerializeField] private bool useCameraManagerIntegration = true;
    [SerializeField] private bool debugCameraChanges = false;
    private float cameraCheckTimer = 0f;
    private Camera currentActiveCamera;
    
    // ✅ EVENT SYSTEM PER NOTIFICHE DI CAMBIO CAMERA
    public System.Action<Camera, Camera> OnCameraChanged;

    [Header("Jump Settings")]
    public float jumpHeight = 4f;
    public float gravity = -9.81f;
    public int maxJumps = 10;
    private int jumpCount = 0;
    private Vector3 velocity;
    private bool isJumpEnabled = true; 
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

    [Header("Advanced Jump Timing")]
    public float coyoteTime = 0.15f;
    public float jumpBufferTime = 0.2f;
    
    private float coyoteTimeCounter = 0f;
    private float jumpBufferCounter = 0f;

    [Header("Falling Settings")]
    public float fallingTimeThreshold = 1.0f;
    private float fallingTimer = 0f;

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
    [SerializeField] private AudioClip[] walkFootsteps;
    [SerializeField] private AudioClip[] runFootsteps;
    [SerializeField] private AudioClip[] sprintFootsteps;
    [SerializeField] private float walkStepInterval = 0.5f;
    [SerializeField] private float runStepInterval = 0.35f;
    [SerializeField] private float sprintStepInterval = 0.25f;
    [SerializeField] private float footstepVolumeWalk = 0.5f;
    [SerializeField] private float footstepVolumeRun = 0.5f;
    [SerializeField] private float footstepVolumeSprint = 0.5f;
    [SerializeField] private float pitchVariation = 0.1f;
    [Header("Jump Audio")]
[SerializeField] private AudioSource jumpAudioSource;
[SerializeField] private AudioClip[] jumpSounds;
[SerializeField] private float jumpVolume = 0.7f;
[SerializeField] private float jumpPitchVariation = 0.1f;
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
    [SerializeField] private float platformDetectionRadius = 0.8f;
    [SerializeField] private float maxPlatformHeight = 2f;
    [SerializeField] private bool debugPlatformMovement = false;
    
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
            
            if (debugCameraChanges)
                Debug.Log($"[ThirdPersonController] 🎮 Camera manualmente impostata: {camera.name}");
        }
        else
        {
            autoDetectActiveCamera = true;
            if (debugCameraChanges)
                Debug.Log("[ThirdPersonController] 🔄 Ripristinato auto-detect camera");
        }
    }

    /// <summary>
    /// Forza l'uso di una CinemachineCamera specifica tramite CameraManager
    /// </summary>
    /// <param name="cinemachineCamera">La CinemachineCamera da attivare</param>
    public void SetActiveCinemachineCamera(CinemachineCamera cinemachineCamera)
    {
        if (cinemachineCamera != null && useCameraManagerIntegration)
        {
            // Usa CameraManager per switchare
            CameraManager.SwitchCamera(cinemachineCamera);
            
            // Forza un aggiornamento immediato
            if (autoDetectActiveCamera)
            {
                DetectActiveCamera();
            }
            
            if (debugCameraChanges)
                Debug.Log($"[ThirdPersonController] 🎬 CinemachineCamera attivata via CameraManager: {cinemachineCamera.name}");
        }
        else if (!useCameraManagerIntegration)
        {
            Debug.LogWarning("[ThirdPersonController] CameraManager integration è disabilitata!");
        }
    }

    /// <summary>
    /// Forza l'uso di una CinemachineCamera specifica tramite nome
    /// </summary>
    /// <param name="cameraName">Nome della CinemachineCamera da attivare</param>
    public void SetActiveCinemachineCameraByName(string cameraName)
    {
        if (useCameraManagerIntegration)
        {
            CameraManager.SwitchCameraByName(cameraName);
            
            // Forza un aggiornamento immediato
            if (autoDetectActiveCamera)
            {
                DetectActiveCamera();
            }
            
            if (debugCameraChanges)
                Debug.Log($"[ThirdPersonController] 🎬 CinemachineCamera attivata via nome: {cameraName}");
        }
        else
        {
            Debug.LogWarning("[ThirdPersonController] CameraManager integration è disabilitata!");
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

    /// <summary>
    /// Ottieni la CinemachineCamera attualmente attiva dal CameraManager
    /// </summary>
    /// <returns>La CinemachineCamera attiva o null</returns>
    public CinemachineCamera GetActiveCinemachineCamera()
    {
        return useCameraManagerIntegration ? CameraManager.ActiveCamera : null;
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

    /// <summary>
    /// Ottieni informazioni dettagliate sulla camera attiva
    /// </summary>
    /// <returns>Stringa con informazioni sulla camera</returns>
    public string GetActiveCameraInfo()
    {
        if (currentActiveCamera == null) return "Nessuna camera attiva";
        
        string info = $"Camera: {currentActiveCamera.name}";
        
        if (useCameraManagerIntegration && CameraManager.ActiveCamera != null)
        {
            info += $"\nCinemachine: {CameraManager.ActiveCamera.name}";
            info += $"\nPriorità: {CameraManager.ActiveCamera.Priority}";
        }
        
        info += $"\nAuto-detect: {autoDetectActiveCamera}";
        info += $"\nCameraManager: {useCameraManagerIntegration}";
        
        return info;
    }

    /// <summary>
    /// Rileva automaticamente la camera attiva - Integrato con CameraManager
    /// </summary>
    private void DetectActiveCamera()
    {
        Camera newActiveCamera = null;
        
        // 1. ✅ PRIORITÀ: Usa CameraManager se disponibile
        if (CameraManager.ActiveCamera != null)
        {
            // Ottieni la camera Unity dal CinemachineBrain
            CinemachineBrain brain = FindFirstObjectByType<CinemachineBrain>();
            if (brain != null && brain.OutputCamera != null)
            {
                newActiveCamera = brain.OutputCamera;
                
                if (debugCameraChanges)
                {
                    Debug.Log($"[ThirdPersonController] 🎥 Usando CameraManager - Camera attiva: {CameraManager.ActiveCamera.name}");
                }
            }
        }
        
        // 2. FALLBACK: Trova CinemachineCamera con priorità più alta
        if (newActiveCamera == null)
        {
            CinemachineCamera[] cinemachineCameras = FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None);
            CinemachineCamera highestPriorityCamera = null;
            int highestPriority = -1;
            
            foreach (var cmCamera in cinemachineCameras)
            {
                if (cmCamera.isActiveAndEnabled && cmCamera.Priority > highestPriority)
                {
                    highestPriorityCamera = cmCamera;
                    highestPriority = cmCamera.Priority;
                }
            }
            
            if (highestPriorityCamera != null)
            {
                CinemachineBrain brain = FindFirstObjectByType<CinemachineBrain>();
                if (brain != null && brain.OutputCamera != null)
                {
                    newActiveCamera = brain.OutputCamera;
                }
            }
        }
        
        // 3. FALLBACK: Camera.main
        if (newActiveCamera == null)
        {
            newActiveCamera = Camera.main;
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
                    break;
                }
            }
        }
        
        // 5. ✅ AGGIORNA SOLO SE LA CAMERA È CAMBIATA
        if (newActiveCamera != currentActiveCamera && newActiveCamera != null)
        {
            Camera previousCamera = currentActiveCamera;
            
            // ✅ AGGIORNA I RIFERIMENTI PER IL MOVIMENTO
            currentActiveCamera = newActiveCamera;
            cameraTransform = newActiveCamera.transform; // ← QUESTO È IL PUNTO CHIAVE!
            
            // Log più dettagliato con info CameraManager
            string cameraManagerInfo = CameraManager.ActiveCamera != null ? 
                $" (CameraManager: {CameraManager.ActiveCamera.name})" : " (No CameraManager)";
            
            if (debugCameraChanges)
            {
                Debug.Log($"[ThirdPersonController] ✅ Camera cambiata: {(previousCamera ? previousCamera.name : "nessuna")} → {newActiveCamera.name}{cameraManagerInfo}");
                Debug.Log($"[ThirdPersonController] 🎮 cameraTransform aggiornato per il movimento: {cameraTransform.name}");
            }
            
            // ✅ NOTIFICA EVENT (se necessario per altri sistemi)
            OnCameraChanged?.Invoke(previousCamera, newActiveCamera);
        }
    }

    /// <summary>
    /// Forza un aggiornamento immediato della camera attiva - OTTIMIZZATO
    /// </summary>
    public void ForceUpdateActiveCamera()
    {
        if (autoDetectActiveCamera)
        {
            DetectActiveCamera();
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
        if (!isJumpEnabled) return;
        
        jumpBufferCounter = jumpBufferTime;
        isHoldingJump = true;
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

        // ✅ ASSICURATI CHE LA CAMERA SIA CONFIGURATA
        if (autoDetectActiveCamera && currentActiveCamera == null)
        {
            DetectActiveCamera();
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
        // 0. ✅ AGGIORNA CAMERA ATTIVA (se auto-detect è abilitato)
        UpdateActiveCamera();
        
        // 1. RILEVAMENTO E AGGIORNAMENTO PIATTAFORME
        DetectAndUpdatePlatform();
        
        // 2. Gestisci input e logica
        UpdateJumpTimers();
        HandleJumpInput();
        HandleLedgeGrab();
        HandleJump();
        HandleAttackVelocity();
        HandleFootstepAudio();
        
        // 3. Calcola movimento del player
        HandleMovement();
        
        // 4. ✅ APPLICA TUTTO IL MOVIMENTO INSIEME
        ApplyAllMovement();
        
        // 5. Decay dei push esterni
        externalPush = Vector3.Lerp(externalPush, Vector3.zero, Time.deltaTime * pushRecoverySpeed);
        
        // 6. Update stati finali
        UpdateGroundedState();
        CheckAndFixStuckJumpAnimation();
    
        HandleFalling();
        CheckForLedgeRelease(); 
        HandleAirControl();
        HandleSprintFX();
    }
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

    /// <summary>
    /// Aggiorna la camera attiva se auto-detect è abilitato
    /// </summary>
    private void UpdateActiveCamera()
    {
        if (!autoDetectActiveCamera) return;
        
        cameraCheckTimer += Time.deltaTime;
        if (cameraCheckTimer >= cameraCheckInterval)
        {
            cameraCheckTimer = 0f;
            DetectActiveCamera();
        }
    }

    // [Il resto dei metodi rimane identico al codice originale...]
    // ✅ NUOVO SISTEMA DI RILEVAMENTO PIATTAFORME INTELLIGENTE
    private void DetectAndUpdatePlatform()
{
    if (!controller.isGrounded)
    {
        // ✅ NUOVO: Sgancia automaticamente quando saltiamo
        if (currentPlatform != null && velocity.y > 2f) // Se stiamo saltando verso l'alto
        {
            if (debugPlatformMovement)
                Debug.Log($"[Platform] Sganciato da {currentPlatform.name} durante salto (velocità: {velocity.y:F2})");
            DetachFromCurrentPlatform();
        }
        // Se non siamo a terra ma non stiamo saltando, mantieni la piattaforma solo se è valida
        else if (currentPlatform != null && !IsPlatformValid())
        {
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

    // ✅ TROVA LA PIATTAFORMA SOTTO IL PLAYER
    private Transform FindPlatformBelow()
    {
        Vector3 rayStart = transform.position + Vector3.up * 0.1f;
        int hitCount = Physics.RaycastNonAlloc(rayStart, Vector3.down, raycastHits, 1.5f, platformLayers);
        
        Transform bestPlatform = null;
        float closestDistance = float.MaxValue;
        
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = raycastHits[i];
            Transform hitTransform = hit.collider.transform;
            
            // Verifica se è una piattaforma valida
            if (IsPlatformTag(hit.collider.tag) && hit.distance < closestDistance)
            {
                // Verifica se siamo effettivamente sopra la piattaforma
                Vector3 hitPoint = hit.point;
                float heightDifference = transform.position.y - hitPoint.y;
                
                if (heightDifference > -0.5f && heightDifference < maxPlatformHeight)
                {
                    // Verifica speciale per RotatingObject
                    if (hit.collider.tag == "RotatingPlatform")
                    {
                        RotatingObject rotObj = hit.collider.GetComponent<RotatingObject>();
                        if (rotObj != null)
                        {
                            // Se è un obstacle platform con detach, lo ignoriamo
                            if (rotObj.GetPlatformType() == PlatformType.ObstaclePlatform && 
                                rotObj.GetDetachPlayerOnHit())
                            {
                                continue;
                            }
                        }
                    }
                    
                    bestPlatform = hitTransform;
                    closestDistance = hit.distance;
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
            Debug.Log($"[Platform] Attaccato a {platform.name} (Tipo: {platformType}, Obstacle: {isOnObstaclePlatform})");
        }
    }

    // ✅ VERIFICA SE LA PIATTAFORMA È ANCORA VALIDA
    private bool IsPlatformValid()
{
    if (currentPlatform == null) return false;
    
    // Durante il salto, sii più permissivo sulla distanza
    float maxDistance = velocity.y > 0 ? 10f : 5f;
    
    float distance = Vector3.Distance(transform.position, currentPlatform.position);
    Bounds platformBounds = currentPlatform.GetComponent<Collider>().bounds;
    
    float allowedDistance = Mathf.Max(platformBounds.size.magnitude * 1.5f, maxDistance);
    
    bool isValid = distance <= allowedDistance;
    
    if (debugPlatformMovement && !isValid)
        Debug.Log($"[Platform] Piattaforma NON valida: distanza {distance:F2} > max {allowedDistance:F2}");
    
    return isValid;
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
            return;
        }

        // Calcola i delta di movimento e rotazione
        platformDeltaPosition = currentPlatform.position - lastPlatformPosition;
        platformDeltaRotation = currentPlatform.rotation * Quaternion.Inverse(lastPlatformRotation);
        
        // Aggiorna le posizioni per il prossimo frame
        lastPlatformPosition = currentPlatform.position;
        lastPlatformRotation = currentPlatform.rotation;

        if (debugPlatformMovement && (platformDeltaPosition.magnitude > 0.001f || platformDeltaRotation != Quaternion.identity))
        {
            Debug.Log($"[Platform] Delta - Pos: {platformDeltaPosition}, Rot: {platformDeltaRotation.eulerAngles}");
        }
    }

    // ✅ SISTEMA DI APPLICAZIONE MOVIMENTO COMPLETO E OTTIMIZZATO
private void ApplyAllMovement()
{
    // ✅ SE SIAMO HANGING, USA SOLO LA STABILIZZAZIONE
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
    
    // ✅ NUOVO: Non applicare movimento piattaforma se stiamo saltando
    bool isJumping = velocity.y > 1f; // Soglia per considerare un salto attivo
    
    // 1. Movimento piattaforma (solo se non stiamo saltando e se presente e valida)
    if (currentPlatform != null && !isOnObstaclePlatform && !isJumping)
    {
        Vector3 platformMovement = ApplyPlatformMovement();
        totalMovement += platformMovement;
        
        if (debugPlatformMovement && platformMovement.magnitude > 0.001f)
            Debug.Log($"[Platform] Applicando movimento piattaforma: {platformMovement} (Jump: {isJumping})");
    }
    else if (isJumping && debugPlatformMovement)
    {
        Debug.Log($"[Platform] Movimento piattaforma IGNORATO durante salto (velocità Y: {velocity.y:F2})");
    }
    
    // 2. Movimento player (orizzontale + verticale)
    Vector3 playerMovement = Vector3.zero;
    playerMovement.x = (playerVelocity.x + externalPush.x + attackVelocity.x) * Time.deltaTime;
    playerMovement.z = (playerVelocity.z + externalPush.z + attackVelocity.z) * Time.deltaTime;
    playerMovement.y = velocity.y * Time.deltaTime;
    
    totalMovement += playerMovement;
    
    // 3. Applica tutto il movimento in una sola chiamata
    controller.Move(totalMovement);
}


private void ExecuteJump(bool isFirstJump)
{
    if (velocity.y < 0) velocity.y = 0f;
    velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
    
    // ✅ Track when jump started
    lastJumpTime = Time.time;

    // ✅ NUOVO: Sgancia dalla piattaforma quando saltiamo
    if (currentPlatform != null)
    {
        if (debugPlatformMovement)
            Debug.Log($"[Platform] Sganciato da {currentPlatform.name} durante ExecuteJump");
        DetachFromCurrentPlatform();
    }

    if (isFirstJump)
    {
        _animator.SetBool(JumpHash, true);
        _animator.SetBool(DoubleJumpHash, false);
        jumpCount = 1;
        
        PlayJumpSound();
    }
    else
    {
        _animator.SetBool(JumpHash, false);
        _animator.SetBool(DoubleJumpHash, true);
        jumpCount++;
    }

    coyoteTimeCounter = 0;
    fallingTimer = 0f;
    
    StopFootstepAudio();
}

private Vector3 ApplyPlatformMovement()
{
    Vector3 totalPlatformMovement = Vector3.zero;
    
    // A) MOVIMENTO LINEARE della piattaforma
    totalPlatformMovement += platformDeltaPosition;
    
    // B) MOVIMENTO DOVUTO ALLA ROTAZIONE
    if (platformDeltaRotation != Quaternion.identity)
    {
        // Applica rotazione al player
        transform.rotation = platformDeltaRotation * transform.rotation;
        
        // Calcola spostamento dovuto alla rotazione
        Vector3 relativePosition = transform.position - currentPlatform.position;
        Vector3 rotatedRelativePosition = platformDeltaRotation * relativePosition;
        Vector3 rotationMovement = rotatedRelativePosition - relativePosition;
        
        totalPlatformMovement += rotationMovement;
    }
    
    // C) ✅ COMPENSAZIONE VELOCITÀ VERTICALE INTELLIGENTE
    if (controller.isGrounded && totalPlatformMovement.y != 0f)
    {
        float platformVerticalSpeed = totalPlatformMovement.y / Time.deltaTime;
        
        // Strategia di compensazione basata sulla velocità
        if (Mathf.Abs(platformVerticalSpeed) > 5f)
        {
            // Movimento verticale molto rapido (ascensori veloci)
            velocity.y = platformVerticalSpeed;
        }
        else if (Mathf.Abs(platformVerticalSpeed) > 2f)
        {
            // Movimento verticale rapido con smoothing
            velocity.y = Mathf.Lerp(velocity.y, platformVerticalSpeed, Time.deltaTime * 20f);
        }
        else if (Mathf.Abs(platformVerticalSpeed) > 0.5f)
        {
            // Movimento verticale moderato
            if (platformVerticalSpeed > 0 || velocity.y > -5f)
            {
                float targetVelocity = Mathf.Max(platformVerticalSpeed, velocity.y);
                velocity.y = Mathf.Lerp(velocity.y, targetVelocity, Time.deltaTime * 12f);
            }
        }
        else if (Mathf.Abs(platformVerticalSpeed) > 0.1f)
        {
            // Movimento verticale lento (ondulazioni)
            if (platformVerticalSpeed > 0.1f || (platformVerticalSpeed < -0.1f && velocity.y > -2f))
            {
                velocity.y = Mathf.Lerp(velocity.y, platformVerticalSpeed, Time.deltaTime * 6f);
            }
        }
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
        
        AudioClip[] currentClips;
        float currentVolume;
        
        if (isSprinting)
        {
            currentClips = sprintFootsteps;
            currentVolume = footstepVolumeSprint;
        }
        else if (smoothInputMagnitude > 0.5f)
        {
            currentClips = runFootsteps;
            currentVolume = footstepVolumeRun;
        }
        else
        {
            currentClips = walkFootsteps;
            currentVolume = footstepVolumeWalk;
        }
        
        if (currentClips != null && currentClips.Length > 0)
        {
            AudioClip clipToPlay = currentClips[Random.Range(0, currentClips.Length)];
            
            footstepAudioSource.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
            footstepAudioSource.volume = currentVolume;
            footstepAudioSource.clip = clipToPlay;
            footstepAudioSource.Play();
        }
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
        if (!isJumpEnabled)
        {
            jumpBufferCounter = 0f;
            return;
        }
        
        if (jumpBufferCounter > 0 && !IsMovementLocked)
        {
            if (TryJump())
            {
                jumpBufferCounter = 0;
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
        if (controller.isGrounded)
            coyoteTimeCounter = coyoteTime;
        else
            coyoteTimeCounter -= Time.deltaTime;

        if (jumpBufferCounter > 0)
            jumpBufferCounter -= Time.deltaTime;
    }

    private bool TryJump()
{
    if (!isJumpEnabled) return false;
    
    // ✅ GESTIONE SALTO DA HANGING
    if (hanging)
    {
       if (jumpBufferCounter > 0f) // Solo se è stato premuto il pulsante salto
    {
        ExecuteJumpFromHang();
        return true;
    }
    // Se siamo hanging ma non c'è input di salto, non fare nulla
    return false;
    }
    
    // Resto della logica di salto normale rimane uguale...
    bool grounded = IsGroundedAccurate();
    bool canJump = false;
    bool isFirstJump = false;

    if (jumpCount == 0 && (grounded || coyoteTimeCounter > 0))
    {
        canJump = true;
        isFirstJump = true;
    }
    else if (jumpCount > 0 && jumpCount < maxJumps && !grounded)
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
    // ✅ BLOCCA MOVIMENTO DURANTE HANGING (prima di tutto)
    if (hanging ||isClimbing || IsMovementLocked)
    {
        playerVelocity = Vector3.zero;
        _animator.SetFloat(SpeedHash, 0f, 0.1f, Time.deltaTime);
        return;
    }

    // ✅ VERIFICA CHE LA CAMERA SIA VALIDA PER IL MOVIMENTO
    if (cameraTransform == null)
    {
        if (debugCameraChanges)
            Debug.LogWarning("[ThirdPersonController] ⚠️ Nessuna cameraTransform disponibile per calcolare il movimento!");
        
        playerVelocity = Vector3.zero;
        _animator.SetFloat(SpeedHash, 0f, 0.1f, Time.deltaTime);
        return;
    }

    // ✅ CONTROLLO AGGIUNTIVO PER canMoveAfterHang
    if (!canMoveAfterHang)
    {
        playerVelocity = Vector3.zero;
        _animator.SetFloat(SpeedHash, 0f, 0.1f, Time.deltaTime);
        return;
    }

    float h = moveInput.x;
    float v = moveInput.y;

    Vector3 tempVector3 = new Vector3(h, 0f, v);
    float inputMag = tempVector3.magnitude;
    
    if (inputMag > 1f)
    {
        tempVector3.Normalize();
        inputMag = 1f;
    }
    
    smoothInputMagnitude = Mathf.Lerp(smoothInputMagnitude, inputMag, Time.deltaTime * 5f);

    if (inputMag < 0.1f)
    {
        playerVelocity = Vector3.zero;
        _animator.SetFloat(SpeedHash, 0f, 0.1f, Time.deltaTime);
        return;
    }

    // ✅ CALCOLO MOVIMENTO RELATIVO ALLA CAMERA ATTIVA
    float targetAngle = Mathf.Atan2(tempVector3.x, tempVector3.z) * Mathf.Rad2Deg + cameraTransform.eulerAngles.y;
    float smoothedAngle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref rotationVelocity, rotationSmoothTime);
    transform.rotation = Quaternion.Euler(0f, smoothedAngle, 0f);

    Vector3 moveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
    moveDir = Vector3.ProjectOnPlane(moveDir, GetGroundNormal());

    float targetSpeed = isSprinting ? sprintSpeed : (smoothInputMagnitude < 0.5f ? walkSpeed : runSpeed);
    playerVelocity = moveDir * targetSpeed;

    Vector3 totalVelocity = playerVelocity + attackVelocity;
    float speedNormalized = Mathf.Clamp01(totalVelocity.magnitude / sprintSpeed);
    _animator.SetFloat(SpeedHash, speedNormalized, 0.1f, Time.deltaTime);
    
    // ✅ DEBUG: Mostra quale camera sta usando per il movimento
    if (debugCameraChanges && inputMag > 0.1f)
    {
        Debug.DrawLine(transform.position, transform.position + moveDir * 2f, Color.green, 0.1f);
        Debug.DrawLine(cameraTransform.position, cameraTransform.position + cameraTransform.forward * 3f, Color.blue, 0.1f);
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
    
    // ✅ SE SIAMO HANGING O IN ARRAMPICATA, NON APPLICARE GRAVITÀ NÉ MOVIMENTO VERTICALE
    if (hanging || isClimbing)
    {
        // Durante hanging o climbing, forza tutto a zero
        velocity = Vector3.zero;
        
        if (debugLedgeGrab && isClimbing && Time.frameCount % 30 == 0)
            Debug.Log("[LedgeGrab] 🧗 Climbing attivo - gravità disabilitata (Animation Event controllerà la fine)");
        
        return;
    }
    
    if (grounded && velocity.y < 0)
        velocity.y = -2f;

    ApplyGravity();
    UpdateJumpAnimations();
}

    private bool IsGroundedAccurate()
    {
        if (controller.isGrounded) return true;
        
        tempVector3.Set(transform.position.x, transform.position.y + 0.05f, transform.position.z);
        int hitCount = Physics.RaycastNonAlloc(tempVector3, Vector3.down, raycastHits, 0.15f);
        
        return hitCount > 0;
    }

    private void UpdateJumpAnimations()
    {
        _animator.SetFloat(VerticalVelocityHash, velocity.y);
    }

    private void ApplyGravity()
    {
        if (velocity.y < 0)
        {
            velocity.y += gravity * 2.5f * Time.deltaTime;
        }
        else if (velocity.y > 0 && !isHoldingJump)
        {
            velocity.y += gravity * 2f * Time.deltaTime;
        }
        else
        {
            velocity.y += gravity * Time.deltaTime;
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
        // Questo metodo è ora principalmente per compatibilità e casi edge
        // Il nuovo sistema di rilevamento in DetectAndUpdatePlatform() è più robusto
        
        if (debugPlatformMovement)
        {
            Debug.Log($"[Platform] OnControllerColliderHit: {hit.collider.name} (tag: {hit.collider.tag})");
        }
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
        
        if (attackEffectUI != null)
            attackEffectUI.PulseIcon();

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

    // ✅ METODI DI DEBUG (solo in build di sviluppo)
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
            
            // Etichetta
            UnityEditor.Handles.Label(currentPlatform.position + Vector3.up * 3, GetPlatformInfo());
        }
        
        // Disegna i delta di movimento
        if (platformDeltaPosition.magnitude > 0.001f)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, transform.position + platformDeltaPosition * 10f);
        }

        // ✅ DISEGNA INFO CAMERA ATTIVA E CAMERAMANAGER
        if (currentActiveCamera != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(currentActiveCamera.transform.position, 1f);
            Gizmos.DrawLine(transform.position, currentActiveCamera.transform.position);
            
            string cameraInfo = $"Camera Attiva: {currentActiveCamera.name}\nAuto-detect: {autoDetectActiveCamera}";
            if (useCameraManagerIntegration && CameraManager.ActiveCamera != null)
            {
                cameraInfo += $"\nCinemachine: {CameraManager.ActiveCamera.name}\nPriorità: {CameraManager.ActiveCamera.Priority}";
                
                // Disegna anche la CinemachineCamera
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireCube(CameraManager.ActiveCamera.transform.position, Vector3.one * 0.5f);
            }
            
            UnityEditor.Handles.Label(currentActiveCamera.transform.position + Vector3.up * 2, cameraInfo);
        }
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
            
            UnityEditor.Handles.Label(transform.position + Vector3.up * 2, 
                $"HANGING\nVel: {velocity.y:F1}\nStabile: {isHangPositionStable}");
        }
    }
    }
    #endif
}