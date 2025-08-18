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
    private Transform currentPlatform = null;
    private Vector3 lastPlatformPos = Vector3.zero;
    private Quaternion lastPlatformRot = Quaternion.identity;
    private Vector3 platformDeltaPos = Vector3.zero;
    private Quaternion platformDeltaRot = Quaternion.identity;

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
    private RaycastHit[] raycastHits = new RaycastHit[4];
    private Vector3 tempVector3;

    public bool IsGrounded() => controller.isGrounded;

    public void AddAttackVelocity(Vector3 velocity)
    {
        attackVelocity += velocity;
    }

    private void Awake()
    {
        controls = new PlayerControls();
        
        if (cameraTransform == null && Camera.main) 
            cameraTransform = Camera.main.transform;

        controls.Gameplay.Move.performed += OnMovePerformed;
        controls.Gameplay.Move.canceled += OnMoveCanceled;
        controls.Gameplay.Sprint.performed += OnSprintPerformed;
        controls.Gameplay.Sprint.canceled += OnSprintCanceled;
        controls.Gameplay.Jump.started += OnJumpStarted;
        controls.Gameplay.Jump.canceled += OnJumpCanceled;
    }

    private void OnMovePerformed(InputAction.CallbackContext ctx) => moveInput = ctx.ReadValue<Vector2>();
    private void OnMoveCanceled(InputAction.CallbackContext ctx) => moveInput = Vector2.zero;
    private void OnSprintPerformed(InputAction.CallbackContext ctx) => isSprinting = true;
    private void OnSprintCanceled(InputAction.CallbackContext ctx) => isSprinting = false;
    
    private void OnJumpStarted(InputAction.CallbackContext ctx)
{
    // ✅ AGGIUNGI QUESTO CONTROLLO:
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

    // ✅ UPDATE CORRETTO CON MOVIMENTO SOLIDALE
    private void Update()
    {
        // 1. PRIMA: Aggiorna movimento della piattaforma
        UpdatePlatformMovement();
        
        // 2. Gestisci input e logica
        UpdateJumpTimers();
        HandleJumpInput();
        HandleJump();
        HandleAttackVelocity();
        HandleFootstepAudio();
        
        // 3. Calcola movimento del player
        HandleMovement();
        
        // 4. ✅ APPLICA TUTTO IL MOVIMENTO INSIEME (UNA SOLA CHIAMATA)
        ApplyAllMovement();
        
        // 5. Decay dei push esterni
        externalPush = Vector3.Lerp(externalPush, Vector3.zero, Time.deltaTime * pushRecoverySpeed);
        
        // 6. Update stati finali
        UpdateGroundedState();
        HandleFalling();
        HandleAirControl();
        HandleSprintFX();
    }

    // ✅ VERSIONE COMPLETA CHE GESTISCE TUTTI I CASI
    private void ApplyAllMovement()
    {
        if (!controller.enabled) return;
        
        Vector3 totalMovement = Vector3.zero;
        
        // 1. MOVIMENTO PIATTAFORMA (se presente)
        if (currentPlatform != null)
        {
            Vector3 platformMovement = Vector3.zero;
            Vector3 rotationMovement = Vector3.zero;
            
            // A) MOVIMENTO LINEARE della piattaforma (orizzontale + verticale)
            platformMovement = platformDeltaPos;
            
            // B) MOVIMENTO DOVUTO ALLA ROTAZIONE
            if (platformDeltaRot != Quaternion.identity)
            {
                // Calcola la posizione del player relativa al centro della piattaforma
                Vector3 relativePosition = transform.position - currentPlatform.position;
                
                // Applica la rotazione al player stesso
                transform.rotation = platformDeltaRot * transform.rotation;
                
                // Calcola dove si sposta il player a causa della rotazione
                Vector3 rotatedRelativePosition = platformDeltaRot * relativePosition;
                rotationMovement = rotatedRelativePosition - relativePosition;
            }
            
            // C) MOVIMENTO TOTALE DELLA PIATTAFORMA
            Vector3 totalPlatformMovement = platformMovement + rotationMovement;
            totalMovement += totalPlatformMovement;
            
            // D) ✅ COMPENSAZIONE VELOCITÀ VERTICALE INTELLIGENTE
            if (controller.isGrounded)
            {
                // Separa i componenti verticali
                float linearVerticalSpeed = platformMovement.y / Time.deltaTime;
                float rotationVerticalSpeed = rotationMovement.y / Time.deltaTime;
                float totalVerticalSpeed = totalPlatformMovement.y / Time.deltaTime;
                
                // Debug dettagliato
                if (Mathf.Abs(totalVerticalSpeed) > 0.01f)
                {
                  
                }
                
                // STRATEGIA DI COMPENSAZIONE BASATA SUL TIPO DI MOVIMENTO
                if (Mathf.Abs(totalVerticalSpeed) > 0.01f)
                {
                    // CASO 1: Movimento verticale molto rapido (ascensori veloci, etc.)
                    if (Mathf.Abs(totalVerticalSpeed) > 5f)
                    {
                        velocity.y = totalVerticalSpeed;
                       
                    }
                    // CASO 2: Movimento verticale rapido
                    else if (Mathf.Abs(totalVerticalSpeed) > 2f)
                    {
                        // Compensazione immediata ma con leggero smoothing
                        velocity.y = Mathf.Lerp(velocity.y, totalVerticalSpeed, Time.deltaTime * 25f);
                    }
                    // CASO 3: Movimento verticale moderato
                    else if (Mathf.Abs(totalVerticalSpeed) > 0.5f)
                    {
                        // Solo se la piattaforma sale o il player non sta cadendo velocemente
                        if (totalVerticalSpeed > 0 || velocity.y > -5f)
                        {
                            float targetVelocity = Mathf.Max(totalVerticalSpeed, velocity.y);
                            velocity.y = Mathf.Lerp(velocity.y, targetVelocity, Time.deltaTime * 15f);
                            Debug.Log($"Moderate platform: Lerping velocity.y to {targetVelocity:F3}");
                        }
                    }
                    // CASO 4: Movimento verticale lento (ondulazioni, etc.)
                    else
                    {
                        // Compensazione delicata solo se necessario
                        if (totalVerticalSpeed > 0.1f || (totalVerticalSpeed < -0.1f && velocity.y > -2f))
                        {
                            velocity.y = Mathf.Lerp(velocity.y, totalVerticalSpeed, Time.deltaTime * 8f);
                            Debug.Log($"Slow platform: Gentle lerping velocity.y to {totalVerticalSpeed:F3}");
                        }
                    }
                }
            }
            
            // E) ✅ GESTIONE SPECIALE PER MOVIMENTO ORIZZONTALE CON ROTAZIONE
            // Se c'è rotazione significativa, assicurati che il movimento orizzontale sia fluido
            if (platformDeltaRot != Quaternion.identity)
            {
                float rotationAngle = Quaternion.Angle(Quaternion.identity, platformDeltaRot);
                if (rotationAngle > 0.1f) // Rotazione significativa
                {
                    // Compensa eventuali jitter orizzontali dovuti alla rotazione
                    Vector3 horizontalPlatformMovement = new Vector3(totalPlatformMovement.x, 0f, totalPlatformMovement.z);
                    if (horizontalPlatformMovement.magnitude > 0.001f)
                    {
                        Debug.Log($"Compensating horizontal movement during rotation: {horizontalPlatformMovement}");
                    }
                }
            }
        }
        
        // 2. MOVIMENTO PLAYER + ATTACK + PUSH
        Vector3 playerMovement = Vector3.zero;
        playerMovement.x = (playerVelocity.x + externalPush.x + attackVelocity.x) * Time.deltaTime;
        playerMovement.z = (playerVelocity.z + externalPush.z + attackVelocity.z) * Time.deltaTime;
        playerMovement.y = velocity.y * Time.deltaTime;
        
        totalMovement += playerMovement;
        
        // 3. ✅ UNA SOLA CHIAMATA A MOVE() CON TUTTO
        controller.Move(totalMovement);
        
    }

    // ✅ RINOMINATO DA UpdatePlatformVelocity A UpdatePlatformMovement
    private void UpdatePlatformMovement()
    {
        if (currentPlatform)
        {
            platformDeltaPos = currentPlatform.position - lastPlatformPos;
            platformDeltaRot = currentPlatform.rotation * Quaternion.Inverse(lastPlatformRot);
            lastPlatformPos = currentPlatform.position;
            lastPlatformRot = currentPlatform.rotation;
        }
        else 
        {
            platformDeltaPos = Vector3.zero;
            platformDeltaRot = Quaternion.identity;
        }
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
    // ✅ AGGIUNGI QUESTO CONTROLLO ALL'INIZIO DEL METODO:
    if (!isJumpEnabled)
    {
        // Reset dei contatori quando il salto è disabilitato
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
/// <summary>
/// Abilita o disabilita il salto del player
/// </summary>
/// <param name="enabled">True per abilitare, false per disabilitare</param>
public void SetJumpEnabled(bool enabled)
{
    isJumpEnabled = enabled;
    
    if (!enabled)
    {
        // Resetta anche il buffer del salto quando disabilitato
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
    // ✅ AGGIUNGI QUESTO CONTROLLO ALL'INIZIO DEL METODO:
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
        currentPlatform = null;
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

    private void HandleMovement()
    {
        if (IsMovementLocked)
        {
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

    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        string hitTag = hit.collider.tag;
        
        if (hitTag == "MovingPlatform" || hitTag == "RotatingPlatform" || hitTag == "RaftPlatform")
        {
            // ✅ Controlla se siamo sopra la piattaforma (non di lato)
            Vector3 hitPoint = hit.point;
            Vector3 platformTop = hit.collider.bounds.max;
            float heightDifference = transform.position.y - hitPoint.y;
            
            // Solo se siamo effettivamente sopra la piattaforma
            if (heightDifference > -0.5f && heightDifference < 2f)
            {
                // ✅ NUOVO: Controlla se è una piattaforma rotante con sganciamento
                if (hitTag == "RotatingPlatform")
                {
                    RotatingObject rotatingObj = hit.collider.GetComponent<RotatingObject>();
                    if (rotatingObj != null)
                    {
                        // Se è un obstacle platform con sganciamento abilitato, NON attaccare il player
                        if (rotatingObj.GetPlatformType() == PlatformType.ObstaclePlatform && 
                            rotatingObj.GetDetachPlayerOnHit())
                        {
                            Debug.Log($"[ThirdPersonController] Evitato attaccamento a piattaforma rotante con sganciamento: {hit.collider.name}");
                            return; // Non attaccare il player a questa piattaforma
                        }
                    }
                }
                
                // Comportamento normale per tutte le altre piattaforme
                if (currentPlatform != hit.collider.transform)
                {
                    currentPlatform = hit.collider.transform;
                    lastPlatformPos = currentPlatform.position;
                    lastPlatformRot = currentPlatform.rotation;
                }
            }
        }
        else if (currentPlatform && hit.collider.transform != currentPlatform)
        {
            // ✅ Verifica che non siamo più sulla piattaforma
            float distanceFromPlatform = Vector3.Distance(transform.position, currentPlatform.position);
            Bounds platformBounds = currentPlatform.GetComponent<Collider>().bounds;
            
            if (distanceFromPlatform > platformBounds.size.magnitude)
            {
                currentPlatform = null;
            }
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
/// <summary>
/// Sgancia immediatamente il player dalla piattaforma corrente
/// Utile quando il player viene colpito da una piattaforma rotante
/// </summary>
public void DetachFromCurrentPlatform()
{
    if (currentPlatform != null)
    {
        currentPlatform = null;
        
        // Reset anche i delta di movimento della piattaforma
        platformDeltaPos = Vector3.zero;
        platformDeltaRot = Quaternion.identity;
    }
}

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
}