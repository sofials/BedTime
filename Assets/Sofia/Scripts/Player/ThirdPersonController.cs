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
        HandleFalling();
        HandleAirControl();
        HandleSprintFX();
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
            // Se non siamo a terra, mantieni la piattaforma attuale se è valida
            if (currentPlatform != null && !IsPlatformValid())
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
        return tag == "MovingPlatform" || tag == "RotatingPlatform" || tag == "RaftPlatform";
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
        
        float distance = Vector3.Distance(transform.position, currentPlatform.position);
        Bounds platformBounds = currentPlatform.GetComponent<Collider>().bounds;
        
        return distance <= platformBounds.size.magnitude * 1.5f;
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
        if (!controller.enabled) return;
        
        Vector3 totalMovement = Vector3.zero;
        
        // 1. ✅ MOVIMENTO PIATTAFORMA (se presente e valida)
        if (currentPlatform != null && !isOnObstaclePlatform)
        {
            Vector3 platformMovement = ApplyPlatformMovement();
            totalMovement += platformMovement;
        }
        
        // 2. MOVIMENTO PLAYER (orizzontale + verticale)
        Vector3 playerMovement = Vector3.zero;
        playerMovement.x = (playerVelocity.x + externalPush.x + attackVelocity.x) * Time.deltaTime;
        playerMovement.z = (playerVelocity.z + externalPush.z + attackVelocity.z) * Time.deltaTime;
        playerMovement.y = velocity.y * Time.deltaTime;
        
        totalMovement += playerMovement;
        
        // 3. ✅ APPLICA TUTTO IL MOVIMENTO IN UNA SOLA CHIAMATA
        controller.Move(totalMovement);
    }

    // ✅ APPLICA IL MOVIMENTO SPECIFICO DELLA PIATTAFORMA
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
        bool grounded = controller.isGrounded;

        if (!grounded)
        {
            tempVector3.Set(transform.position.x, transform.position.y + 0.05f, transform.position.z);
            int hitCount = Physics.RaycastNonAlloc(tempVector3, Vector3.down, raycastHits, 0.1f);

            if (hitCount > 0)
            {
                grounded = true;
            }
        }

        _animator.SetBool(IsGroundedHash, grounded);

        if (grounded && !wasGroundedLastFrame)
        {
            OnLanding();
        }

        wasGroundedLastFrame = grounded;
    }

    public bool IsJumpEnabled
    {
        get => isJumpEnabled;
        set => isJumpEnabled = value;
    }

    public void SetJumpEnabled(bool enabled)
    {
        isJumpEnabled = enabled;
        
        if (!enabled)
        {
            jumpBufferCounter = 0f;
            isHoldingJump = false;
        }
        
        Debug.Log($"[ThirdPersonController] Salto {(enabled ? "abilitato" : "disabilitato")}");
    }

    private void OnLanding()
    {
        jumpCount = 0;
        fallingTimer = 0f;

        _animator.SetBool(JumpHash, false);
        _animator.SetBool(DoubleJumpHash, false);
        _animator.SetBool(IsFallingHash, false);
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

    private void HandleJump()
    {
        bool grounded = controller.isGrounded;
        if (grounded && velocity.y < 0)
            velocity.y = -2f;

        ApplyGravity();
        UpdateJumpAnimations();
    }

    private void ExecuteJump(bool isFirstJump)
    {
        if (velocity.y < 0) velocity.y = 0f;
        velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);

        if (isFirstJump)
        {
            _animator.SetBool(JumpHash, true);
            _animator.SetBool(DoubleJumpHash, false);
            jumpCount = 1;
        }
        else
        {
            _animator.SetBool(JumpHash, false);
            _animator.SetBool(DoubleJumpHash, true);
            jumpCount++;
        }

        coyoteTimeCounter = 0;
        // ✅ NON sganciare automaticamente dalla piattaforma al salto
        // Lascia che il sistema di rilevamento gestisca naturalmente il distacco
        fallingTimer = 0f;
        
        StopFootstepAudio();
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

    // ✅ MOVIMENTO MIGLIORATO CON GESTIONE CAMERA DINAMICA
    private void HandleMovement()
    {
        if (IsMovementLocked)
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

        float h = moveInput.x;
        float v = moveInput.y;

        tempVector3.Set(h, 0f, v);
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

        // ✅ CALCOLO MOVIMENTO RELATIVO ALLA CAMERA ATTIVA (cameraTransform)
        // Questo è il punto chiave: usa cameraTransform.eulerAngles.y per calcolare la direzione
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
        if (controller.isGrounded || IsMovementLocked) return;

        // ✅ VERIFICA CHE LA CAMERA SIA VALIDA ANCHE PER AIR CONTROL
        if (cameraTransform == null) return;

        tempVector3.Set(moveInput.x, 0f, moveInput.y);
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
        controller.enabled = false;

        Transform spawnPoint = GetRespawnPoint();
        transform.position = spawnPoint.position;
        transform.rotation = Quaternion.Euler(0f, 0f, 0f);

        velocity = Vector3.zero;
        attackVelocity = Vector3.zero;
        
        // ✅ Sgancia dalla piattaforma durante il respawn
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
    }
    #endif
}