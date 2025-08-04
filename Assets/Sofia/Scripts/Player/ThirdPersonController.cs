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
    public Vector3 velocity;

    [Header("Ledge Grab System")]
    public float ledgeDetectionDistance = 1.5f;
    public float ledgeHangOffset = 0.5f; // Distanza sotto il bordo
    public float ledgeForwardOffset = 0.3f; // Distanza dal muro
    public float climbUpDuration = 1.0f;
    public float climbUpHeight = 1.5f;
    public string[] climbableTags = {"Platform", "MovingPlatform", "RotatingPlatform"}; // Tag climbabili
    
    [Header("Debug Ledge System")]
    public bool showLedgeRaycast = true;
    
    [Header("Ledge Grab Trigger (Recommended)")]
    public bool useTriggersForLedgeGrab = true; // Usa trigger invece di raycast
    
    [HideInInspector] public bool isHanging = false;
    [HideInInspector] public bool isClimbingUp = false;
    private Vector3 hangingPosition;
    private Vector3 hangingNormal;
    private Transform hangingPlatform;

    [Header("Advanced Jump Timing")]
    public float coyoteTime = 0.15f;
    public float jumpBufferTime = 0.2f;
    private float coyoteTimeCounter = 0f;
    private float jumpBufferCounter = 0f;
    private bool jumpInputPressed = false;

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

    // Property per compatibilità con LedgeGrabDetector
    public bool IsGrounded() => controller.isGrounded;
    [System.Obsolete("Use isHanging instead")]
    public bool isGrabbingLedge => isHanging;

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
            jumpInputPressed = true;
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
        
        if (jumpInputPressed)
        {
            HandleImmediateJump();
            jumpInputPressed = false;
        }
        
        HandleJump();
        HandleLedgeDetection();
        HandleHangingInput();

        if (currentPlatform != null && controller.enabled)
        {
            controller.Move(platformDeltaPos);
            if (platformDeltaRot != Quaternion.identity)
                transform.rotation = platformDeltaRot * transform.rotation;

            if (controller.isGrounded)
                velocity.y = Mathf.Max(platformDeltaPos.y, velocity.y);
        }

        // Non muovere il controller se è disabilitato (durante hanging/climbing)
        if (controller.enabled)
        {
            Vector3 totalMove = playerVelocity + externalPush;
            totalMove.y = velocity.y;
            controller.Move(totalMove * Time.deltaTime);
        }

        externalPush = Vector3.Lerp(externalPush, Vector3.zero, Time.deltaTime * pushRecoverySpeed);

        UpdateGroundedState();
        HandleFalling();
        HandleAirControl();
        HandleSprintFX();
    }

    // Sistema unificato per rilevamento ledge
    private void HandleLedgeDetection()
    {
        // Se usi trigger system, non fare raycast
        if (useTriggersForLedgeGrab) return;
        
        // Non rilevare se già appeso, arrampicandosi, o a terra
        if (isHanging || isClimbingUp || controller.isGrounded) return;
        
        // Solo se stai cadendo abbastanza velocemente
        if (velocity.y > -1f) return;
        
        if (showLedgeRaycast)
            Debug.Log($"Checking ledge detection - Velocity Y: {velocity.y}, Grounded: {controller.isGrounded}");
        
        Vector3 hangPos;
        Vector3 hangNormal;
        Transform hangPlatform;
        
        if (DetectLedgeOpportunity(out hangPos, out hangNormal, out hangPlatform))
        {
            StartHanging(hangPos, hangNormal, hangPlatform);
        }
    }

    private bool DetectLedgeOpportunity(out Vector3 hangPosition, out Vector3 normal, out Transform platform)
    {
        hangPosition = Vector3.zero;
        normal = Vector3.zero;
        platform = null;

        // Raycast in avanti per trovare una parete
        Vector3 forward = transform.forward;
        Vector3 origin = transform.position + Vector3.up * 1.0f;
        
        RaycastHit wallHit;
        if (!Physics.Raycast(origin, forward, out wallHit, ledgeDetectionDistance))
            return false;

        // Verifica che l'oggetto colpito abbia un tag climbabile
        if (!IsClimbableTag(wallHit.collider.tag))
        {
            if (showLedgeRaycast)
                Debug.Log($"Hit {wallHit.collider.name} but tag '{wallHit.collider.tag}' is not climbable");
            return false;
        }

        // Debug raycast
        if (showLedgeRaycast)
        {
            Debug.DrawRay(origin, forward * ledgeDetectionDistance, Color.red, 0.1f);
            Debug.Log($"Wall hit: {wallHit.collider.name} with climbable tag '{wallHit.collider.tag}' at {wallHit.point}");
        }

        // Verifica che sia una parete verticale
        if (Vector3.Dot(wallHit.normal, Vector3.up) > 0.3f)
        {
            if (showLedgeRaycast)
                Debug.Log("Not a vertical wall");
            return false;
        }

        // Raycast verso l'alto dal punto di impatto per trovare il bordo
        Vector3 wallPoint = wallHit.point;
        Vector3 upRayOrigin = wallPoint + Vector3.up * 0.1f - wallHit.normal * 0.1f;
        
        RaycastHit edgeHit;
        if (!Physics.Raycast(upRayOrigin, Vector3.up, out edgeHit, 3.0f))
        {
            if (showLedgeRaycast)
                Debug.Log("No edge found above");
            return false;
        }

        // Verifica che anche il bordo abbia un tag climbabile
        if (!IsClimbableTag(edgeHit.collider.tag))
        {
            if (showLedgeRaycast)
                Debug.Log($"Edge has non-climbable tag: {edgeHit.collider.tag}");
            return false;
        }

        // Debug edge raycast
        if (showLedgeRaycast)
        {
            Debug.DrawRay(upRayOrigin, Vector3.up * 3.0f, Color.blue, 0.1f);
            Debug.Log($"Edge hit: {edgeHit.collider.name} with tag '{edgeHit.collider.tag}' at {edgeHit.point}");
        }

        // Verifica che il bordo sia orizzontale
        if (Vector3.Dot(edgeHit.normal, Vector3.up) < 0.8f)
        {
            if (showLedgeRaycast)
                Debug.Log("Edge not horizontal enough");
            return false;
        }

        // Calcola la posizione di hanging
        Vector3 edgePosition = edgeHit.point;
        hangPosition = edgePosition - wallHit.normal * ledgeForwardOffset + Vector3.down * ledgeHangOffset;
        normal = wallHit.normal;
        platform = edgeHit.collider.transform;

        if (showLedgeRaycast)
        {
            Debug.Log($"Ledge grab opportunity found! Hang position: {hangPosition}");
            Debug.DrawLine(transform.position, hangPosition, Color.green, 1f);
        }

        return true;
    }

    // Verifica se un tag è climbabile
    private bool IsClimbableTag(string tag)
    {
        for (int i = 0; i < climbableTags.Length; i++)
        {
            if (climbableTags[i] == tag)
                return true;
        }
        return false;
    }

    // Metodo pubblico per trigger system (RACCOMANDATO)
    public void StartHangingFromTrigger(Vector3 hangPosition, Vector3 hangNormal, Transform platform)
    {
        if (isHanging || isClimbingUp || controller.isGrounded) 
        {
            if (showLedgeRaycast)
                Debug.Log($"Cannot start hanging - isHanging: {isHanging}, isClimbingUp: {isClimbingUp}, isGrounded: {controller.isGrounded}");
            return;
        }
        
        if (showLedgeRaycast)
        {
            Debug.Log($"StartHangingFromTrigger called - Position: {hangPosition}, Velocity: {velocity.y}");
            Debug.Log($"Current animator states - Jump: {_animator.GetBool("Jump")}, DoubleJump: {_animator.GetBool("DoubleJump")}, isFalling: {_animator.GetBool("isFalling")}");
        }
            
        StartHanging(hangPosition, hangNormal, platform);
    }

    // Metodo pubblico per compatibilità con LedgeGrabDetector vecchio
    public void TryGrabLedge(Collider platformCollider)
    {
        if (isHanging || isClimbingUp || controller.isGrounded) return;
        
        // Calcola posizione di hang basata sul collider
        Vector3 platformTop = new Vector3(transform.position.x, platformCollider.bounds.max.y, transform.position.z);
        Vector3 hangPos = platformTop + Vector3.down * ledgeHangOffset + transform.forward * -ledgeForwardOffset;
        Vector3 hangNormal = -transform.forward;
        
        StartHanging(hangPos, hangNormal, platformCollider.transform);
    }

    private void StartHanging(Vector3 position, Vector3 normal, Transform platform)
    {
        isHanging = true;
        hangingPosition = position;
        hangingNormal = normal;
        hangingPlatform = platform;

        if (showLedgeRaycast)
            Debug.Log($"StartHanging called - Grab position: {position}, Platform: {platform.name}");

        // Ferma il movimento
        velocity = Vector3.zero;
        playerVelocity = Vector3.zero;

        // MIGLIORATO: Posiziona il player considerando la sua altezza/offset
        if (controller.enabled)
        {
            controller.enabled = false;
            
            // Calcola posizione finale considerando l'offset del controller
            Vector3 finalPosition = hangingPosition;
            
            // Aggiungi offset per compensare la differenza tra centro del controller e punto di grab
            finalPosition.y -= controller.height * 0.3f; // Regola questo valore se serve
            
            transform.position = finalPosition;
            transform.rotation = Quaternion.LookRotation(-hangingNormal);
            
            if (showLedgeRaycast)
                Debug.Log($"Player positioned at: {transform.position} (adjusted from {hangingPosition})");
            
            controller.enabled = true;
        }

        // Aggiorna animator - FORZA il reset di tutti gli stati di salto
        _animator.SetBool("Jump", false);
        _animator.SetBool("DoubleJump", false);
        _animator.SetBool("isFalling", false);
        _animator.SetBool("isGrounded", false); // Non è a terra quando appeso
        
        // Aspetta un frame prima di impostare hanging per assicurare transizione pulita
        StartCoroutine(SetHangingAfterFrame());

        // Reset jump count
        jumpCount = 0;
        coyoteTimeCounter = 0f;
        jumpBufferCounter = 0f;
    }

    private System.Collections.IEnumerator SetHangingAfterFrame()
    {
        yield return null; // Aspetta un frame
        _animator.SetBool("isHanging", true);
        
        if (showLedgeRaycast)
            Debug.Log("Set isHanging = true after one frame");
    }

    private void HandleHangingInput()
    {
        if (!isHanging || isClimbingUp) return;
        
        // Input per arrampicarsi (Su o Spazio)
        if (moveInput.y > 0.1f || jumpInputPressed)
        {
            StartClimbUp();
            jumpInputPressed = false;
        }
        // Input per lasciarsi cadere (Giù)
        else if (moveInput.y < -0.1f)
        {
            DropFromHanging();
        }
        
        // Movimento laterale durante hanging
        if (Mathf.Abs(moveInput.x) > 0.1f)
        {
            HandleHangingMovement();
        }
    }

    private void StartClimbUp()
    {
        isClimbingUp = true;
        _animator.SetTrigger("ClimbUp");
        _animator.SetBool("isHanging", false);
        
        StartCoroutine(ClimbUpCoroutine());
    }

    private System.Collections.IEnumerator ClimbUpCoroutine()
    {
        float timer = 0f;
        Vector3 startPos = transform.position;
        Vector3 targetPos = hangingPosition + Vector3.up * climbUpHeight - hangingNormal * 0.5f;
        
        IsMovementLocked = true;
        controller.enabled = false;
        
        while (timer < climbUpDuration)
        {
            timer += Time.deltaTime;
            float t = timer / climbUpDuration;
            float smoothT = Mathf.SmoothStep(0f, 1f, t);
            
            transform.position = Vector3.Lerp(startPos, targetPos, smoothT);
            yield return null;
        }
        
        // Fine arrampicata
        controller.enabled = true;
        isHanging = false;
        isClimbingUp = false;
        IsMovementLocked = false;
        
        velocity = Vector3.zero;
        playerVelocity = Vector3.zero;
        jumpCount = 0;
    }

    private void DropFromHanging()
    {
        isHanging = false;
        _animator.SetBool("isHanging", false);
        
        // Spinta indietro
        velocity.y = -2f;
        externalPush = hangingNormal * 2f;
        
        // Reset contatori
        jumpCount = 0;
    }

    private void HandleHangingMovement()
    {
        if (hangingPlatform == null || !controller.enabled) return;
        
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
        
        _animator.SetBool("Jump", false);
        _animator.SetBool("DoubleJump", false);
        _animator.SetBool("isFalling", false);
    }

    private void HandleImmediateJump()
    {
        if (IsMovementLocked || isHanging) return;
        
        bool grounded = controller.isGrounded;
        
        if (grounded && jumpCount == 0)
        {
            PerformJump();
        }
        else if (!grounded && jumpCount > 0 && jumpCount < maxJumps)
        {
            PerformJump();
        }
    }

    private void UpdateJumpTimers()
    {
        if (controller.isGrounded)
            coyoteTimeCounter = coyoteTime;
        else
            coyoteTimeCounter -= Time.deltaTime;

        jumpBufferCounter -= Time.deltaTime;
    }

    private void HandleJump()
    {
        if (isHanging || isClimbingUp) return;
        
        bool grounded = controller.isGrounded;

        if (grounded && velocity.y < 0)
            velocity.y = -2f;

        if (jumpBufferCounter > 0 && !IsMovementLocked)
        {
            bool canJump = false;

            if (jumpCount == 0 && coyoteTimeCounter > 0)
            {
                canJump = true;
            }
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
        if (velocity.y < -1f && _animator.GetBool("DoubleJump"))
        {
            _animator.SetBool("DoubleJump", false);
        }
        
        _animator.SetFloat("VerticalVelocity", velocity.y);
    }

    private void PerformJump()
    {
        if (velocity.y < 0) velocity.y = 0f;

        velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);

        if (jumpCount == 0)
        {
            _animator.SetBool("Jump", true);
        }
        else
        {
            _animator.SetBool("Jump", false);
            _animator.SetBool("DoubleJump", true);
        }

        jumpCount++;
        jumpBufferCounter = 0;
        
        if (jumpCount == 1)
            coyoteTimeCounter = 0;

        currentPlatform = null;
        fallingTimer = 0f;
    }

    private void ApplyGravity()
    {
        if (isHanging || isClimbingUp) return;
        
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
        moveDir = Vector3.ProjectOnPlane(moveDir, GetGroundNormal());

        float targetSpeed = isSprinting ? sprintSpeed : (smoothInputMagnitude < 0.5f ? walkSpeed : runSpeed);
        playerVelocity = moveDir.normalized * targetSpeed;

        float speedNormalized = Mathf.Clamp01(playerVelocity.magnitude / sprintSpeed);
        _animator.SetFloat("Speed", speedNormalized, 0.1f, Time.deltaTime);
    }

    private void HandleFalling()
    {
        if (isHanging || isClimbingUp) return;
        
        bool grounded = controller.isGrounded;
        bool isDescending = velocity.y < -3f;

        if (grounded || velocity.y > -1f)
        {
            fallingTimer = 0f;
            if (_animator.GetBool("isFalling"))
                _animator.SetBool("isFalling", false);
            return;
        }

        if (isDescending)
        {
            fallingTimer += Time.deltaTime;

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

        // Reset solo i trigger e bool che esistono nel tuo Animator
        _animator.ResetTrigger("ClimbUp");
        _animator.SetBool("Jump", false);
        _animator.SetBool("DoubleJump", false);
        _animator.SetBool("isFalling", false);
        _animator.SetBool("isHanging", false);
        _animator.SetBool("isGrounded", true);

        // Reset stati
        jumpCount = 0;
        coyoteTimeCounter = 0f;
        jumpBufferCounter = 0f;
        fallingTimer = 0f;
        jumpInputPressed = false;
        isHanging = false;
        isClimbingUp = false;
        wasGroundedLastFrame = true;

        StopAllCoroutines();

        if (sprintFX) sprintFX.StopEffect();
        sprintFXActive = false;

        IsMovementLocked = false;
    }

    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (hit.collider.CompareTag("MovingPlatform") || hit.collider.CompareTag("RotatingPlatform") || hit.collider.CompareTag("RaftPlatform"))
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
        if (currentHealth <= 0 || isHanging || isClimbingUp) return;

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