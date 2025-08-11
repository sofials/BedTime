using UnityEngine;
using UnityEngine.InputSystem;
using CartoonFX;
using System.Collections;

[RequireComponent(typeof(CharacterController))]
public class ThirdPersonController : MonoBehaviour
{
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
    public float maxHealth = 100f;
    public float currentHealth;
    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;

    // NUOVO: SISTEMA AUDIO PASSI
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

    // NUOVE VARIABILI PER ATTACK VELOCITY
    private Vector3 attackVelocity = Vector3.zero;
    [SerializeField] private float attackVelocityDecay = 8f; // Velocità di decadimento della attack velocity

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
                attackVelocity = Vector3.zero; // Reset anche attack velocity
                _animator.SetFloat(SpeedHash, 0f);
                
                // NUOVO: Stop audio passi quando movimento è bloccato
                StopFootstepAudio();
            }
        }
    }

    // OTTIMIZZAZIONE: Cache per raycast
    private RaycastHit[] raycastHits = new RaycastHit[4];

    // OTTIMIZZAZIONE: Riduzione allocazioni temporanee
    private Vector3 tempVector3;

    public bool IsGrounded() => controller.isGrounded;

    // NUOVO METODO: Aggiungi velocità di attacco
    public void AddAttackVelocity(Vector3 velocity)
    {
        attackVelocity += velocity;
        Debug.Log($"Attack velocity aggiunta: {velocity}, totale: {attackVelocity}");
    }

    private void Awake()
    {
        controls = new PlayerControls();
        
        // OTTIMIZZAZIONE: Cache del cameraTransform
        if (cameraTransform == null && Camera.main) 
            cameraTransform = Camera.main.transform;

        controls.Gameplay.Move.performed += OnMovePerformed;
        controls.Gameplay.Move.canceled += OnMoveCanceled;
        controls.Gameplay.Sprint.performed += OnSprintPerformed;
        controls.Gameplay.Sprint.canceled += OnSprintCanceled;
        controls.Gameplay.Jump.started += OnJumpStarted;
        controls.Gameplay.Jump.canceled += OnJumpCanceled;
    }

    // OTTIMIZZAZIONE: Metodi callback separati invece di lambda inline
    private void OnMovePerformed(InputAction.CallbackContext ctx) => moveInput = ctx.ReadValue<Vector2>();
    private void OnMoveCanceled(InputAction.CallbackContext ctx) => moveInput = Vector2.zero;
    private void OnSprintPerformed(InputAction.CallbackContext ctx) => isSprinting = true;
    private void OnSprintCanceled(InputAction.CallbackContext ctx) => isSprinting = false;
    
    // SISTEMA INPUT IMMEDIATO
    private void OnJumpStarted(InputAction.CallbackContext ctx)
    {
        jumpBufferCounter = jumpBufferTime;
        isHoldingJump = true;
        
        // NON provare a saltare qui, lascia che sia Update() a gestirlo
        Debug.Log($"INPUT SALTO RICEVUTO - Buffer: {jumpBufferCounter}");
    }
    private void OnJumpCanceled(InputAction.CallbackContext ctx)
    {
        isHoldingJump = false;
    }

    private void Start()
    {
        _animator = GetComponentInChildren<Animator>();
        controller = GetComponent<CharacterController>();

        currentHealth = maxHealth;
        playerUI.UpdateHealth(currentHealth);

        if (sprintFX) sprintFX.StopEffect();
        
        // NUOVO: Setup AudioSource se non assegnato
        SetupFootstepAudio();
    }

    // NUOVO: Setup del sistema audio passi
    private void SetupFootstepAudio()
    {
        if (footstepAudioSource == null)
        {
            // Cerca un AudioSource esistente o creane uno nuovo
            footstepAudioSource = GetComponent<AudioSource>();
            if (footstepAudioSource == null)
            {
                GameObject audioGO = new GameObject("FootstepAudio");
                audioGO.transform.SetParent(transform);
                audioGO.transform.localPosition = Vector3.zero;
                footstepAudioSource = audioGO.AddComponent<AudioSource>();
            }
        }
        
        // Configura l'AudioSource per i passi
        footstepAudioSource.playOnAwake = false;
        footstepAudioSource.loop = false;
        footstepAudioSource.spatialBlend = 0.7f; // Audio 3D parziale
        footstepAudioSource.rolloffMode = AudioRolloffMode.Linear;
        footstepAudioSource.maxDistance = 15f;
    }

    private void OnEnable() => controls.Gameplay.Enable();
    private void OnDisable()
    {
        controls.Gameplay.Disable();
        if (sprintFX) sprintFX.StopEffect();
        sprintFXActive = false;
        
        // NUOVO: Stop audio quando disabilitato
        StopFootstepAudio();
    }

    private void Update()
    {
        UpdatePlatformVelocity();
        HandleMovement();
        UpdateJumpTimers();
        
        // GESTISCI SALTO PRIMA DELLA FISICA - NUOVO ORDINE
        HandleJumpInput();
        HandleJump();

        // GESTISCI ATTACK VELOCITY DECAY
        HandleAttackVelocity();
        
        // NUOVO: Gestisci audio passi
        HandleFootstepAudio();

        if (currentPlatform != null && controller.enabled)
        {
            controller.Move(platformDeltaPos);
            if (platformDeltaRot != Quaternion.identity)
                transform.rotation = platformDeltaRot * transform.rotation;

            if (controller.isGrounded)
                velocity.y = Mathf.Max(platformDeltaPos.y, velocity.y);
        }

        if (controller.enabled)
        {
            // APPLICA ANCHE ATTACK VELOCITY AL MOVIMENTO FINALE
            tempVector3.Set(playerVelocity.x + externalPush.x + attackVelocity.x, 
                           velocity.y, 
                           playerVelocity.z + externalPush.z + attackVelocity.z);
            controller.Move(tempVector3 * Time.deltaTime);
        }

        externalPush = Vector3.Lerp(externalPush, Vector3.zero, Time.deltaTime * pushRecoverySpeed);

        UpdateGroundedState();
        HandleFalling();
        HandleAirControl();
        HandleSprintFX();
    }

    // NUOVO: Sistema audio passi
    private void HandleFootstepAudio()
    {
        bool isMoving = playerVelocity.sqrMagnitude > 0.1f;
        bool isGrounded = controller.isGrounded;
        bool canPlayFootsteps = isMoving && isGrounded && !IsMovementLocked;
        
        if (canPlayFootsteps)
        {
            // Aggiorna timer
            footstepTimer += Time.deltaTime;
            
            // Determina intervallo e clip basati sulla velocità
            float currentInterval = GetCurrentStepInterval();
            
            // Riproduci passo se è il momento
            if (footstepTimer >= currentInterval)
            {
                PlayFootstepSound();
                footstepTimer = 0f;
            }
            
            wasMovingLastFrame = true;
        }
        else
        {
            // Reset timer quando ci fermiamo
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
        
        // Scegli clip e volume basati sulla velocità
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
        
        // Riproduci clip casuale se disponibile
        if (currentClips != null && currentClips.Length > 0)
        {
            AudioClip clipToPlay = currentClips[Random.Range(0, currentClips.Length)];
            
            // Applica variazione di pitch
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

    // NUOVO METODO: Gestisce il decadimento della attack velocity
    private void HandleAttackVelocity()
    {
        if (attackVelocity.magnitude > 0.01f)
        {
            attackVelocity = Vector3.Lerp(attackVelocity, Vector3.zero, Time.deltaTime * attackVelocityDecay);
            
            // Azzera se è molto piccola per evitare floating point precision issues
            if (attackVelocity.magnitude < 0.01f)
                attackVelocity = Vector3.zero;
        }
    }

    private void HandleJumpInput()
    {
        if (jumpBufferCounter > 0 && !IsMovementLocked)
        {
            if (TryJump())
            {
                jumpBufferCounter = 0; // Consuma buffer solo se il salto è andato a buon fine
            }
        }
    }

    // OTTIMIZZAZIONE: Sistema di grounding migliorato
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

    // LANDING SEMPLIFICATO
    private void OnLanding()
    {
        jumpCount = 0;
        fallingTimer = 0f;
        
        // Reset animazioni
        _animator.SetBool(JumpHash, false);
        _animator.SetBool(DoubleJumpHash, false);
        _animator.SetBool(IsFallingHash, false);
        
        Debug.Log("LANDING - Count resettato a 0");
    }

    private void UpdateJumpTimers()
    {
        // Coyote time
        if (controller.isGrounded)
            coyoteTimeCounter = coyoteTime;
        else
            coyoteTimeCounter -= Time.deltaTime;

        // Jump buffer
        if (jumpBufferCounter > 0)
            jumpBufferCounter -= Time.deltaTime;
    }

    // METODO UNIFICATO PER TENTARE IL SALTO
    private bool TryJump()
    {
        bool grounded = IsGroundedAccurate(); // Usa controllo più accurato
        bool canJump = false;
        bool isFirstJump = false;

        // PRIMO SALTO: da terra o coyote time
        if (jumpCount == 0 && (grounded || coyoteTimeCounter > 0))
        {
            canJump = true;
            isFirstJump = true;
        }
        // SALTI MULTIPLI: in aria
        else if (jumpCount > 0 && jumpCount < maxJumps && !grounded)
        {
            canJump = true;
            isFirstJump = false;
        }

        if (canJump)
        {
            ExecuteJump(isFirstJump);
            Debug.Log($"SALTO ESEGUITO! Primo: {isFirstJump}, Count: {jumpCount}");
            return true;
        }
        
        return false;
    }

    // SISTEMA DI SALTO RIDOTTO (solo gravità)
    private void HandleJump()
    {
        bool grounded = controller.isGrounded;
        if (grounded && velocity.y < 0)
            velocity.y = -2f;

        ApplyGravity();
        UpdateJumpAnimations();
    }

    // ESECUZIONE DEL SALTO
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

        // Reset timers
        coyoteTimeCounter = 0;
        currentPlatform = null;
        fallingTimer = 0f;
        
        // NUOVO: Stop audio passi durante il salto
        StopFootstepAudio();
    }

    private bool IsGroundedAccurate()
    {
        // Combina il controllo del CharacterController con un raycast
        if (controller.isGrounded) return true;
        
        // Raycast aggiuntivo per casi edge
        tempVector3.Set(transform.position.x, transform.position.y + 0.05f, transform.position.z);
        int hitCount = Physics.RaycastNonAlloc(tempVector3, Vector3.down, raycastHits, 0.15f);
        
        return hitCount > 0;
    }

    private void UpdateJumpAnimations()
    {
        // Mantieni solo l'aggiornamento della velocità verticale
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

        bool isMoving = playerVelocity.sqrMagnitude > 0.01f; // OTTIMIZZAZIONE: usa sqrMagnitude
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

    private void UpdatePlatformVelocity()
    {
        if (currentPlatform)
        {
            platformDeltaPos = currentPlatform.position - lastPlatformPos;
            platformDeltaRot = currentPlatform.rotation * Quaternion.Inverse(lastPlatformRot);
            lastPlatformPos = currentPlatform.position;
            lastPlatformRot = currentPlatform.rotation;
        }
        else platformDeltaPos = Vector3.zero;
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

        // OTTIMIZZAZIONE: Riusa variabile temporanea
        tempVector3.Set(h, 0f, v);
        float inputMag = tempVector3.magnitude;
        
        if (inputMag > 1f) // OTTIMIZZAZIONE: Normalizza solo se necessario
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

        // CONSIDERA ANCHE ATTACK VELOCITY NEL CALCOLO DELLA VELOCITÀ PER L'ANIMATORE
        Vector3 totalVelocity = playerVelocity + attackVelocity;
        float speedNormalized = Mathf.Clamp01(totalVelocity.magnitude / sprintSpeed);
        _animator.SetFloat(SpeedHash, speedNormalized, 0.1f, Time.deltaTime);
    }

    // OTTIMIZZAZIONE: Riduzione frequenza controlli falling
    private float fallingCheckTimer = 0f;
    private const float FALLING_CHECK_INTERVAL = 0.1f;

    private void HandleFalling()
    {
        // OTTIMIZZAZIONE: Controlla falling meno frequentemente
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
            fallingTimer += FALLING_CHECK_INTERVAL; // Usa interval invece di deltaTime

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
        // OTTIMIZZAZIONE: Usa NonAlloc per il raycast
        tempVector3.Set(transform.position.x, transform.position.y + 0.1f, transform.position.z);
        int hitCount = Physics.RaycastNonAlloc(tempVector3, Vector3.down, raycastHits, 0.3f);
        return hitCount > 0;
    }

    private void HandleAirControl()
    {
        if (controller.isGrounded || IsMovementLocked) return;

        // OTTIMIZZAZIONE: Riusa variabile temporanea
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
        
        // OTTIMIZZAZIONE: Evita allocazione con new Vector3
        tempVector3.Set(moveDir.x * airControlSpeed, 0f, moveDir.z * airControlSpeed);
        playerVelocity += tempVector3;
    }

    // ========== RESPAWN AGGIORNATO PER SCENEMANAGER ==========
    
    public void Respawn()
    {
        controller.enabled = false;

        // NUOVO: Ottieni spawn point tramite GameManager (che ora gestisce i checkpoint per scena)
        Transform spawnPoint = GetRespawnPoint();

        transform.position = spawnPoint.position;
        transform.rotation = Quaternion.Euler(0f, 0f, 0f);

        velocity = Vector3.zero;
        attackVelocity = Vector3.zero; // Reset attack velocity al respawn
        controller.enabled = true;

        // OTTIMIZZAZIONE: Usa hash precalcolati
        _animator.SetBool(JumpHash, false);
        _animator.SetBool(DoubleJumpHash, false);
        _animator.SetBool(IsFallingHash, false);
        _animator.SetBool(IsGroundedHash, true);

        // Reset stati
        jumpCount = 0;
        coyoteTimeCounter = 0f;
        jumpBufferCounter = 0f;
        fallingTimer = 0f;
        
        wasGroundedLastFrame = true;

        StopAllCoroutines();

        if (sprintFX) sprintFX.StopEffect();
        sprintFXActive = false;
        
        // NUOVO: Stop audio passi al respawn
        StopFootstepAudio();

        IsMovementLocked = false;
        
        Debug.Log($"[ThirdPersonController] Respawn completato alla posizione: {spawnPoint.position}");
    }
    
    /// <summary>
    /// Ottieni il punto di spawn appropriato basato sulla scena corrente
    /// </summary>
    private Transform GetRespawnPoint()
    {
        if (GameManager.Instance == null)
        {
            Debug.LogWarning("[ThirdPersonController] GameManager non trovato, uso transform corrente");
            return transform;
        }

        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        
        // Controlla se c'è un checkpoint salvato per questa scena
        if (GameManager.Instance.HasSceneCheckpoint(currentScene))
        {
            string checkpointName = GameManager.Instance.GetSceneCheckpoint(currentScene);
            
            // Cerca il checkpoint nella scena
            GameObject checkpointObj = GameObject.Find(checkpointName);
            if (checkpointObj != null)
            {
                Debug.Log($"[ThirdPersonController] Respawn al checkpoint: {checkpointName}");
                return checkpointObj.transform;
            }
            else
            {
                Debug.LogWarning($"[ThirdPersonController] Checkpoint '{checkpointName}' non trovato nella scena, uso spawn di default");
            }
        }
        
        // Fallback al punto di spawn di default
        if (GameManager.Instance.levelStartPoint != null)
        {
            Debug.Log($"[ThirdPersonController] Respawn al punto di partenza del livello");
            return GameManager.Instance.levelStartPoint;
        }
        
        Debug.LogWarning("[ThirdPersonController] Nessun punto di spawn trovato, uso posizione corrente");
        return transform;
    }

    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        // OTTIMIZZAZIONE: Cache dei tag per evitare string comparisons ripetute
        string hitTag = hit.collider.tag;
        
        if (hitTag == "MovingPlatform" || hitTag == "RotatingPlatform" || hitTag == "RaftPlatform")
        {
            if (currentPlatform != hit.collider.transform)
            {
                currentPlatform = hit.collider.transform;
                lastPlatformPos = currentPlatform.position;
                lastPlatformRot = currentPlatform.rotation;
            }
        }
        else if (currentPlatform && hit.collider.transform != currentPlatform)
        {
            currentPlatform = null;
        }
    }

    public void ApplyExternalPush(Vector3 force) => externalPush += force;

    public void Heal(float amount)
    {
        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
        playerUI.UpdateHealth(currentHealth);
    }

    // OTTIMIZZAZIONE: Sistema di danno ottimizzato
    private float lastDamageTime = 0f;
    private const float DAMAGE_COOLDOWN = 0.1f; // Previene spam di danni

    public void TakeDamage(float amount)
    {
        // OTTIMIZZAZIONE: Cooldown per evitare spam di danni
        if (Time.time - lastDamageTime < DAMAGE_COOLDOWN) return;
        lastDamageTime = Time.time;
        
        if (currentHealth <= 0) return;

        float oldHealth = currentHealth;
        currentHealth -= amount;
        currentHealth = Mathf.Max(0, currentHealth);
        playerUI.UpdateHealth(currentHealth);
        attackEffectUI?.PulseIcon();

        if (currentHealth <= 0 && oldHealth > 0)
        {
            IsMovementLocked = true;
            _animator.SetFloat(SpeedHash, 0f);

            if (ShouldPlayHitReal())
            {
                _animator.SetTrigger(HitRealHash);
            }
            else
            {
                _animator.SetTrigger(HitHash);
                StartCoroutine(QuickRespawn());
            }
        }
        else if (currentHealth > 0)
        {
            // OTTIMIZZAZIONE: Cache del componente PlayerAttack
            PlayerAttack playerAttack = GetComponentInChildren<PlayerAttack>();
            bool isSwinging = playerAttack != null && playerAttack.isAttacking;
            if (!isSwinging)
            {
                _animator.SetTrigger(HitHash);
            }
        }
    }

    private IEnumerator QuickRespawn()
    {
        yield return new WaitForSeconds(0.1f);
        Respawn();
        currentHealth = maxHealth;
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
        currentHealth = maxHealth;
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
            // OTTIMIZZAZIONE: Aggiorna normal del terreno solo periodicamente
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

    // OTTIMIZZAZIONE: Cleanup per ridurre GC
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