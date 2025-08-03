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
    public int maxJumps = 2;
    private int jumpCount = 0;
    private Vector3 velocity;
    
    [Header("Advanced Jump Timing")]
    public float coyoteTime = 0.15f;
    public float jumpBufferTime = 0.2f;
    private float coyoteTimeCounter = 0f;
    private float jumpBufferCounter = 0f;
    private bool jumpInputPressed = false; // Flag per input immediato

    [Header("Falling Settings")]
    public float fallingTimeThreshold = 1.0f; // Ridotto per più responsività
    private float fallingTimer = 0f;

    [Header("Air Control Settings")]
    public float airControlStrength = 0.5f;
    public float airControlSpeed = 2f;
    public float airRotationSmoothTime = 0.3f;

    [Header("Hanging Settings")]
    public float hangingDetectionDistance = 1.2f;
    public float hangingOffsetY = 0.5f; // Quanto sotto il bordo si posiziona
    public float hangingOffsetZ = 0.3f; // Distanza dal bordo
    public float climbUpDuration = 1.0f;
    public LayerMask hangingLayerMask = -1; // Quali layer possono essere appesi

    private bool isHanging = false;
    private bool isClimbingUp = false;
    private Vector3 hangingPosition;
    private Vector3 hangingNormal;
    private Transform hangingPlatform;

    [Header("Player Stats")]
    public float maxHealth = 100f;
    public float currentHealth;
    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;

    private CharacterController controller;
    private Animator _animator;

    [Header("References")]
    public Transform cameraTransform;

    private bool wasGroundedLastFrame;
    private Transform currentPlatform = null;
    private Vector3 lastPlatformPos = Vector3.zero;
    private Quaternion lastPlatformRot = Quaternion.identity;
    private Vector3 platformDeltaPos = Vector3.zero;
    private Quaternion platformDeltaRot = Quaternion.identity;

    private Vector3 playerVelocity;
    private Vector3 externalPush = Vector3.zero;
    [SerializeField] private float pushRecoverySpeed = 0.2f;

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
                _animator.SetFloat("Speed", 0f);
            }
        }
    }

    private void Awake()
    {
        controls = new PlayerControls();

        controls.Gameplay.Move.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        controls.Gameplay.Move.canceled += ctx => moveInput = Vector2.zero;

        controls.Gameplay.Sprint.performed += ctx => isSprinting = true;
        controls.Gameplay.Sprint.canceled += ctx => isSprinting = false;

        controls.Gameplay.Jump.started += ctx => { 
            jumpBufferCounter = jumpBufferTime; 
            isHoldingJump = true;
            jumpInputPressed = true; // Flag per salto immediato
        };
        controls.Gameplay.Jump.canceled += ctx => { 
            isHoldingJump = false; 
            jumpInputPressed = false;
        };
    }

    private void Start()
    {
        _animator = GetComponentInChildren<Animator>();
        controller = GetComponent<CharacterController>();

        if (cameraTransform == null && Camera.main) cameraTransform = Camera.main.transform;

        currentHealth = maxHealth;
        playerUI.UpdateHealth(currentHealth);

        if (sprintFX) sprintFX.StopEffect();
    }

    private void OnEnable() => controls.Gameplay.Enable();
    private void OnDisable()
    {
        controls.Gameplay.Disable();
        if (sprintFX) sprintFX.StopEffect();
        sprintFXActive = false;
    }

    private void Update()
    {
        UpdatePlatformVelocity();
        HandleMovement();
        UpdateJumpTimers();
        
        // Gestione salto ottimizzata - controlla prima l'input diretto
        if (jumpInputPressed)
        {
            HandleImmediateJump();
            jumpInputPressed = false; // Consuma il flag
        }
        
        HandleJump();
        HandleHangingDetection(); // Sistema hanging
        HandleHangingInput(); // Input durante hanging

        if (currentPlatform != null)
        {
            controller.Move(platformDeltaPos);
            if (platformDeltaRot != Quaternion.identity)
                transform.rotation = platformDeltaRot * transform.rotation;

            if (controller.isGrounded)
                velocity.y = Mathf.Max(platformDeltaPos.y, velocity.y);
        }

        Vector3 totalMove = playerVelocity + externalPush;
        totalMove.y = velocity.y;
        controller.Move(totalMove * Time.deltaTime);

        externalPush = Vector3.Lerp(externalPush, Vector3.zero, Time.deltaTime * pushRecoverySpeed);

        UpdateGroundedState();
        HandleFalling();
        HandleAirControl();
        HandleSprintFX();
    }

    // Rilevamento opportunità hanging
    private bool DetectHangingOpportunity(out Vector3 hangPosition, out Vector3 normal, out Transform platform)
    {
        hangPosition = Vector3.zero;
        normal = Vector3.zero;
        platform = null;

        // Controlla solo se stai cadendo ma NON nel vuoto
        if (controller.isGrounded || velocity.y > -1f || isHanging || _animator.GetBool("isFalling")) 
            return false;

        // Raycast in avanti per trovare una parete
        Vector3 forward = transform.forward;
        Vector3 origin = transform.position + Vector3.up * 1.0f;
        
        RaycastHit wallHit;
        if (!Physics.Raycast(origin, forward, out wallHit, hangingDetectionDistance, hangingLayerMask))
            return false;

        // Verifica che sia una parete verticale (normale verso l'alto tra 0.7 e 1.0)
        if (Vector3.Dot(wallHit.normal, Vector3.up) > 0.3f)
            return false;

        // Raycast verso l'alto dal punto di impatto per trovare il bordo
        Vector3 wallPoint = wallHit.point;
        Vector3 upRayOrigin = wallPoint + Vector3.up * 0.1f - wallHit.normal * 0.1f;
        
        RaycastHit edgeHit;
        if (!Physics.Raycast(upRayOrigin, Vector3.up, out edgeHit, 3.0f, hangingLayerMask))
            return false;

        // Verifica che il bordo sia orizzontale
        if (Vector3.Dot(edgeHit.normal, Vector3.up) < 0.8f)
            return false;

        // Calcola la posizione di hanging
        Vector3 edgePosition = edgeHit.point;
        hangPosition = edgePosition - wallHit.normal * hangingOffsetZ + Vector3.down * hangingOffsetY;
        normal = wallHit.normal;
        platform = edgeHit.collider.transform;

        return true;
    }

    // Nuovo metodo separato per il rilevamento hanging
    private void HandleHangingDetection()
    {
        // Se già in hanging o climbing, non fare nulla
        if (isHanging || isClimbingUp) return;
        
        // Rileva opportunità di hanging (solo se NON stai cadendo nel vuoto)
        Vector3 hangPos;
        Vector3 hangNormal;
        Transform hangPlatform;
        
        if (DetectHangingOpportunity(out hangPos, out hangNormal, out hangPlatform))
        {
            StartHanging(hangPos, hangNormal, hangPlatform);
        }
    }

    // Nuovo metodo per iniziare hanging
    private void StartHanging(Vector3 position, Vector3 normal, Transform platform)
    {
        isHanging = true;
        hangingPosition = position;
        hangingNormal = normal;
        hangingPlatform = platform;
        
        // Ferma il movimento
        velocity = Vector3.zero;
        playerVelocity = Vector3.zero;
        
        // Posiziona il personaggio
        controller.enabled = false;
        transform.position = hangingPosition;
        transform.rotation = Quaternion.LookRotation(-hangingNormal);
        controller.enabled = true;
        
        // Aggiorna animator (NON toccare isFalling che gestisce cadute nel vuoto)
        _animator.SetBool("isHanging", true);
        _animator.SetBool("Jump", false);
        _animator.SetBool("DoubleJump", false);
        
        // Reset jump count
        jumpCount = 0;
    }

    // Nuovo metodo per gestire input durante hanging
    private void HandleHangingInput()
    {
        if (!isHanging || isClimbingUp) return;
        
        // Input per arrampicarsi
        if (moveInput.y > 0.1f || jumpInputPressed) // Su o Spazio
        {
            StartClimbUp();
            jumpInputPressed = false;
        }
        // Input per lasciarsi cadere
        else if (moveInput.y < -0.1f) // Giù
        {
            DropFromHanging();
        }
        
        // Movimento laterale durante hanging (opzionale)
        if (Mathf.Abs(moveInput.x) > 0.1f)
        {
            HandleHangingMovement();
        }
    }

    // Metodo per iniziare arrampicata
    private void StartClimbUp()
    {
        isClimbingUp = true;
        _animator.SetTrigger("ClimbUp");
        _animator.SetBool("isHanging", false);
        
        StartCoroutine(ClimbUpCoroutine());
    }

    // Coroutine per gestire l'arrampicata
    private System.Collections.IEnumerator ClimbUpCoroutine()
    {
        float timer = 0f;
        Vector3 startPos = transform.position;
        Vector3 targetPos = hangingPosition + Vector3.up * 1.5f - hangingNormal * 0.5f;
        
        // Disabilita controlli durante arrampicata
        IsMovementLocked = true;
        
        while (timer < climbUpDuration)
        {
            timer += Time.deltaTime;
            float t = timer / climbUpDuration;
            
            // Interpolazione smooth
            float smoothT = Mathf.SmoothStep(0f, 1f, t);
            Vector3 currentPos = Vector3.Lerp(startPos, targetPos, smoothT);
            
            controller.enabled = false;
            transform.position = currentPos;
            controller.enabled = true;
            
            yield return null;
        }
        
        // Fine arrampicata
        isHanging = false;
        isClimbingUp = false;
        IsMovementLocked = false;
        
        // Reset velocità
        velocity = Vector3.zero;
        playerVelocity = Vector3.zero;
    }

    // Metodo per lasciarsi cadere
    private void DropFromHanging()
    {
        isHanging = false;
        _animator.SetBool("isHanging", false);
        // NON impostare isFalling - quello è solo per cadute nel vuoto
        
        // Dai una piccola spinta indietro
        velocity.y = -2f;
        externalPush = hangingNormal * 2f;
    }

    // Movimento laterale durante hanging (opzionale)
    private void HandleHangingMovement()
    {
        if (hangingPlatform == null) return;
        
        Vector3 right = Vector3.Cross(Vector3.up, -hangingNormal);
        Vector3 moveDirection = right * moveInput.x * 2f * Time.deltaTime;
        
        controller.enabled = false;
        transform.position += moveDirection;
        controller.enabled = true;
        
        hangingPosition = transform.position;
    }

    private void UpdateGroundedState()
    {
        bool grounded = controller.isGrounded;
        _animator.SetBool("isGrounded", grounded);

        // Landing logic
        if (grounded && !wasGroundedLastFrame)
        {
            OnLanding();
        }
        wasGroundedLastFrame = grounded;
    }

    private void OnLanding()
    {
        jumpCount = 0;
        fallingTimer = 0f;
        
        // Reset animazioni di salto
        _animator.SetBool("Jump", false);
        _animator.SetBool("DoubleJump", false);
        _animator.SetBool("isFalling", false);
        
    }

    private void HandleImmediateJump()
    {
        if (IsMovementLocked || isHanging) return;
        
        bool grounded = controller.isGrounded;
        
        // Salto immediato se grounded
        if (grounded && jumpCount == 0)
        {
            PerformJump();
        }
        // Doppio salto immediato se in aria
        else if (!grounded && jumpCount > 0 && jumpCount < maxJumps)
        {
            PerformJump();
        }
    }

    private void UpdateJumpTimers()
    {
        // Aggiorna coyote time
        if (controller.isGrounded)
            coyoteTimeCounter = coyoteTime;
        else
            coyoteTimeCounter -= Time.deltaTime;

        // Aggiorna jump buffer
        jumpBufferCounter -= Time.deltaTime;
    }

    private void HandleJump()
    {
        if (isHanging || isClimbingUp) return; // Non saltare durante hanging
        
        bool grounded = controller.isGrounded;

        // Stabilizza velocità Y quando sei a terra
        if (grounded && velocity.y < 0)
            velocity.y = -2f;

        // Jump buffer logic (per input non consumati dal salto immediato)
        if (jumpBufferCounter > 0 && !IsMovementLocked)
        {
            bool canJump = false;

            // Primo salto con coyote time
            if (jumpCount == 0 && coyoteTimeCounter > 0)
            {
                canJump = true;
            }
            // Salti successivi in aria
            else if (jumpCount > 0 && jumpCount < maxJumps && !grounded)
            {
                canJump = true;
            }

            if (canJump)
            {
                PerformJump();
            }
        }

        ApplyGravity();
        UpdateJumpAnimations();
    }

    private void UpdateJumpAnimations()
    {
        // Reset double jump animation quando inizi a cadere
        if (velocity.y < -1f && _animator.GetBool("DoubleJump"))
        {
            _animator.SetBool("DoubleJump", false);
        }
        
        // Set vertical velocity per blend tree (opzionale)
        _animator.SetFloat("VerticalVelocity", velocity.y);
    }

    private void PerformJump()
    {
        // Reset velocità Y per salto pulito
        if (velocity.y < 0) velocity.y = 0f;

        // Calcola velocità del salto
        velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);

        // Gestione animazioni ottimizzata
        if (jumpCount == 0)
        {
            // Primo salto
            _animator.SetBool("Jump", true);
        }
        else
        {
            // Doppio salto
            _animator.SetBool("Jump", false); // Reset primo salto
            _animator.SetBool("DoubleJump", true);
        }

        // Aggiorna contatori
        jumpCount++;
        jumpBufferCounter = 0; // Consuma buffer
        
        // Consuma coyote time solo per il primo salto
        if (jumpCount == 1)
            coyoteTimeCounter = 0;

        // Reset stati
        currentPlatform = null;
        fallingTimer = 0f;
    }

    private void ApplyGravity()
    {
        if (isHanging || isClimbingUp) return; // Nessuna gravità durante hanging
        
        if (velocity.y < 0)
        {
            // Moltiplicatore di caduta per caduta più rapida
            velocity.y += gravity * 2.5f * Time.deltaTime;
        }
        else if (velocity.y > 0 && !isHoldingJump)
        {
            // Moltiplicatore per salto basso quando si rilascia il tasto
            velocity.y += gravity * 2f * Time.deltaTime;
        }
        else
        {
            // Gravità normale
            velocity.y += gravity * Time.deltaTime;
        }
    }

    private void HandleSprintFX()
    {
        if (sprintFX == null) return;

        bool isMoving = playerVelocity.magnitude > 0.1f;
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
        if (IsMovementLocked || isHanging || isClimbingUp)
        {
            playerVelocity = Vector3.zero;
            _animator.SetFloat("Speed", 0f, 0.1f, Time.deltaTime);
            return;
        }

        float h = moveInput.x;
        float v = moveInput.y;

        Vector3 inputDir = new Vector3(h, 0f, v).normalized;
        float inputMag = inputDir.magnitude;
        smoothInputMagnitude = Mathf.Lerp(smoothInputMagnitude, inputMag, Time.deltaTime * 5f);

        if (inputMag < 0.1f)
        {
            playerVelocity = Vector3.zero;
            _animator.SetFloat("Speed", 0f, 0.1f, Time.deltaTime);
            return;
        }

        float targetAngle = Mathf.Atan2(inputDir.x, inputDir.z) * Mathf.Rad2Deg + cameraTransform.eulerAngles.y;
        float smoothedAngle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref rotationVelocity, rotationSmoothTime);
        transform.rotation = Quaternion.Euler(0f, smoothedAngle, 0f);

        Vector3 moveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;

        // Proietta il movimento sulla pendenza
        moveDir = Vector3.ProjectOnPlane(moveDir, GetGroundNormal());

        float targetSpeed = isSprinting ? sprintSpeed : (smoothInputMagnitude < 0.5f ? walkSpeed : runSpeed);
        playerVelocity = moveDir.normalized * targetSpeed;

        float speedNormalized = Mathf.Clamp01(playerVelocity.magnitude / sprintSpeed);
        _animator.SetFloat("Speed", speedNormalized, 0.1f, Time.deltaTime);
    }

    private void HandleFalling()
    {
        if (isHanging || isClimbingUp) return; // Non gestire falling durante hanging
        
        bool grounded = controller.isGrounded;
        bool isDescending = velocity.y < -3f; // Soglia più alta per falling

        // Reset falling se grounded o salendo
        if (grounded || velocity.y > -1f)
        {
            fallingTimer = 0f;
            if (_animator.GetBool("isFalling"))
                _animator.SetBool("isFalling", false);
            return;
        }

        // Accumula tempo di caduta solo se scendi velocemente
        if (isDescending)
        {
            fallingTimer += Time.deltaTime;

            // Attiva falling solo dopo soglia temporale E distanza dal terreno
            bool shouldFall = fallingTimer >= fallingTimeThreshold && 
                             !IsNearGroundBelow() && 
                             !_animator.GetBool("Jump") && 
                             !_animator.GetBool("DoubleJump");

            if (shouldFall && !_animator.GetBool("isFalling"))
            {
                _animator.SetBool("isFalling", true);

            }
        }
    }

    private bool IsNearGroundBelow()
    {
        RaycastHit hit;
        float checkDistance = 0.3f;
        Vector3 origin = transform.position + Vector3.up * 0.1f;
        return Physics.Raycast(origin, Vector3.down, out hit, checkDistance);
    }

    private void HandleAirControl()
    {
        if (controller.isGrounded || IsMovementLocked || isHanging || isClimbingUp) return;

        Vector3 inputDir = new Vector3(moveInput.x, 0f, moveInput.y).normalized;
        if (inputDir.magnitude < 0.1f) return;

        float targetAngle = Mathf.Atan2(inputDir.x, inputDir.z) * Mathf.Rad2Deg + cameraTransform.eulerAngles.y;
        float smoothedAngle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref rotationVelocity, airRotationSmoothTime);
        transform.rotation = Quaternion.Euler(0f, smoothedAngle, 0f);

        Vector3 moveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
        playerVelocity += new Vector3(moveDir.x, 0f, moveDir.z) * airControlSpeed;
    }

    public void Respawn()
    {
        controller.enabled = false;

        Transform spawnPoint = GameManager.Instance.currentCheckpoint != null ?
                               GameManager.Instance.currentCheckpoint :
                               GameManager.Instance.levelStartPoint;

        transform.position = spawnPoint.position;
        transform.rotation = Quaternion.Euler(0f, 0f, 0f);

        velocity = Vector3.zero;
        controller.enabled = true;

        _animator.ResetTrigger("Jump");
        _animator.ResetTrigger("DoubleJump");
        _animator.ResetTrigger("JumpStart");
        _animator.ResetTrigger("DoubleJumpStart");
        _animator.ResetTrigger("StartFalling");
        _animator.ResetTrigger("Land");
        _animator.ResetTrigger("ClimbUp"); // Reset hanging trigger
        _animator.SetBool("Jump", false);
        _animator.SetBool("DoubleJump", false);
        _animator.SetBool("isFalling", false);
        _animator.SetBool("isHanging", false); // Reset hanging state

        jumpCount = 0;
        coyoteTimeCounter = 0f;
        jumpBufferCounter = 0f;
        fallingTimer = 0f;
        jumpInputPressed = false; // Reset flag

        // Reset hanging state
        isHanging = false;
        isClimbingUp = false;
        StopAllCoroutines(); // Ferma eventuale ClimbUpCoroutine

        wasGroundedLastFrame = true;

        if (sprintFX) sprintFX.StopEffect();
        sprintFXActive = false;

        IsMovementLocked = false;
    }

    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (hit.collider.CompareTag("MovingPlatform") || hit.collider.CompareTag("RotatingPlatform")||hit.collider.CompareTag("RaftPlatform"))
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

    public void TakeDamage(float amount)
    {
        if (currentHealth <= 0 || isHanging || isClimbingUp) return; // No damage during hanging

        float oldHealth = currentHealth;
        currentHealth -= amount;
        currentHealth = Mathf.Max(0, currentHealth);
        playerUI.UpdateHealth(currentHealth);
        attackEffectUI?.PulseIcon();

        if (currentHealth <= 0 && oldHealth > 0)
        {
            IsMovementLocked = true;
            _animator.SetFloat("Speed", 0f);

            if (ShouldPlayHitReal())
            {
                _animator.SetTrigger("HitReal");
            }
            else
            {
                _animator.SetTrigger("Hit");
                StartCoroutine(QuickRespawn());
            }
        }
        else if (currentHealth > 0)
        {
            PlayerAttack playerAttack = GetComponentInChildren<PlayerAttack>();
            bool isSwinging = playerAttack != null && playerAttack.isAttacking;
            if (!isSwinging)
            {
                _animator.SetTrigger("Hit");
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
        bool isFalling = !_animator.GetBool("isGrounded") && velocity.y < -2f;
        bool isJumping = _animator.GetBool("Jump") || _animator.GetBool("DoubleJump");
        return !isFalling && !isJumping && !isHanging && !isClimbingUp;
    }

    public void OnHitRealEnd()
    {
        Respawn();
        currentHealth = maxHealth;
        playerUI.UpdateHealth(currentHealth);
        IsMovementLocked = false;
    }

    private Vector3 GetGroundNormal()
    {
        if (controller.isGrounded)
        {
            RaycastHit hit;
            if (Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, out hit, 1.5f))
            {
                return hit.normal;
            }
        }
        return Vector3.up;
    }
}