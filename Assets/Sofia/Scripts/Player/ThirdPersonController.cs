using UnityEngine;
using UnityEngine.InputSystem;
using CartoonFX;

[RequireComponent(typeof(CharacterController))]
public class ThirdPersonController : MonoBehaviour
{
    /*────────────────────  UI  ────────────────────*/
    [Header("UI Effect")]
    public PlayerUI playerUI;
    public UIEffectHandler attackEffectUI;

    /*─────────────────  Movement  ─────────────────*/
    [Header("Movement Settings")]
    public float walkSpeed   = 2f;
    public float runSpeed    = 5f;
    public float sprintSpeed = 8f;
    public float rotationSmoothTime = 0.1f;
    private float rotationVelocity;
    private float smoothInputMagnitude;

    /*────────────────────  Jump  ───────────────────*/
    [Header("Jump Settings")]
    public float jumpHeight = 4f;
    public float gravity    = -9.81f;
    public int   maxJumps   = 2;
    private int   jumpCount = 0;
    private Vector3 velocity;

    /*───────────────  Air‑control  ───────────────*/
    [Header("Air Control Settings")]
    public float airControlStrength = 0.5f;
    public float airControlSpeed    = 2f;
    public float airRotationSmoothTime = 0.3f;

    /*─────────────────  Stats  ───────────────────*/
    [Header("Player Stats")]
    public float maxHealth = 100f;
    public float currentHealth;
    public float CurrentHealth => currentHealth;
    public float MaxHealth    => maxHealth;

    /*─────────────────  Internals  ───────────────*/
    private CharacterController controller;
    private Animator _animator;

    [Header("References")]
    public Transform cameraTransform;

    private bool wasGroundedLastFrame;
    private Transform currentPlatform  = null;
    private Vector3   lastPlatformPos  = Vector3.zero;
    private Quaternion lastPlatformRot = Quaternion.identity;
    private Vector3 platformDeltaPos   = Vector3.zero;
    private Quaternion platformDeltaRot = Quaternion.identity;

    private Vector3 playerVelocity;
    private Vector3 externalPush = Vector3.zero;

    [SerializeField] private float pushRecoverySpeed = 0.2f;

    private PlayerControls controls;
    private Vector2 moveInput;
    private bool jumpInput;
    private bool isSprinting;
    private bool isHoldingJump;

    /*───────────────  Sprint FX  ───────────────*/
    [Header("Sprint Effect (assign CFXR_EffectController)")]
    public CFXR_EffectController sprintFX;
    private bool sprintFXActive = false;

    /*─────────────────  Awake  ───────────────────*/
    private void Awake()
    {
        controls = new PlayerControls();

        controls.Gameplay.Move.performed +=  ctx => moveInput = ctx.ReadValue<Vector2>();
        controls.Gameplay.Move.canceled  +=  ctx => moveInput = Vector2.zero;

        controls.Gameplay.Sprint.performed += ctx => isSprinting = true;
        controls.Gameplay.Sprint.canceled  += ctx => isSprinting = false;

        controls.Gameplay.Jump.started  += ctx => { jumpInput = true;  isHoldingJump = true;  };
        controls.Gameplay.Jump.canceled += ctx => { isHoldingJump = false;                     };
    }

    /*─────────────────  Start  ───────────────────*/
    private void Start()
    {
        _animator  = GetComponentInChildren<Animator>();
        controller = GetComponent<CharacterController>();

        if (cameraTransform == null && Camera.main) cameraTransform = Camera.main.transform;

        currentHealth = maxHealth;
        playerUI.UpdateHealth(currentHealth);

        /* Assicuriamoci che il VFX sia spento all'avvio */
        if (sprintFX) sprintFX.StopEffect();
    }

    private void OnEnable()  => controls.Gameplay.Enable();
    private void OnDisable()
    {
        controls.Gameplay.Disable();
        if (sprintFX) sprintFX.StopEffect();
        sprintFXActive = false;
    }

    /*──────────────────  Update  ──────────────────*/
    private void Update()
    {
        UpdatePlatformVelocity();
        HandleMovement();
        HandleJump();

        // movimento relativo a eventuale piattaforma
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

        bool grounded = controller.isGrounded;
        _animator.SetBool("isGrounded", grounded);

        if (grounded && !wasGroundedLastFrame)
        {
            jumpCount = 0;
            _animator.SetBool("Jump", false);
            _animator.SetBool("DoubleJump", false);
        }
        wasGroundedLastFrame = grounded;

        HandleFalling();
        HandleAirControl();
        HandleSprintFX();
    }

    /*──────────────  Sprint VFX  ──────────────*/
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

    /*─────────────  Movement helpers  ───────────*/
    private void UpdatePlatformVelocity()
    {
        if (currentPlatform)
        {
            platformDeltaPos = currentPlatform.position - lastPlatformPos;
            platformDeltaRot = currentPlatform.rotation * Quaternion.Inverse(lastPlatformRot);
            lastPlatformPos  = currentPlatform.position;
            lastPlatformRot  = currentPlatform.rotation;
        }
        else platformDeltaPos = Vector3.zero;
    }

    private void HandleMovement()
    {
        float h = moveInput.x;
        float v = moveInput.y;

        Vector3 inputDir = new Vector3(h, 0f, v).normalized;
        float inputMag   = inputDir.magnitude;
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
        float targetSpeed = isSprinting ? sprintSpeed : (smoothInputMagnitude < 0.5f ? walkSpeed : runSpeed);

        playerVelocity = moveDir.normalized * targetSpeed;
        float speedNormalized = Mathf.Clamp01(playerVelocity.magnitude / sprintSpeed);
        _animator.SetFloat("Speed", speedNormalized, 0.1f, Time.deltaTime);
    }

    /*──────────────  Jump & fall  ──────────────*/
    private void HandleJump()
    {
        bool grounded = controller.isGrounded;

        if (jumpInput && jumpCount < maxJumps)
        {
            if (jumpCount == 0)   _animator.SetBool("Jump", true);
            else                  _animator.SetBool("DoubleJump", true);

            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            jumpCount++;
            jumpInput = false;

            currentPlatform = null;
        }

        if (velocity.y < 0)                                  velocity.y += gravity * 2.5f * Time.deltaTime;
        else if (velocity.y > 0 && !isHoldingJump)           velocity.y += gravity * 2f   * Time.deltaTime;
        else                                                 velocity.y += gravity         * Time.deltaTime;

        if (grounded && velocity.y < 0) velocity.y = -2f;
    }

    private void HandleFalling()
    {
        bool grounded = controller.isGrounded;
        bool falling = !grounded && velocity.y < -3f &&
                       !_animator.GetBool("Jump") && !_animator.GetBool("DoubleJump");

        _animator.SetBool("isFalling", falling);
    }

    private void HandleAirControl()
    {
        if (controller.isGrounded) return;

        Vector3 inputDir = new Vector3(moveInput.x, 0f, moveInput.y).normalized;
        if (inputDir.magnitude < 0.1f) return;

        float targetAngle = Mathf.Atan2(inputDir.x, inputDir.z) * Mathf.Rad2Deg + cameraTransform.eulerAngles.y;
        float smoothedAngle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref rotationVelocity, airRotationSmoothTime);
        transform.rotation = Quaternion.Euler(0f, smoothedAngle, 0f);

        Vector3 moveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
        playerVelocity += new Vector3(moveDir.x, 0f, moveDir.z) * airControlSpeed;
    }

    /*──────────────  Misc  ──────────────*/
    public void Respawn()
    {
        controller.enabled = false;
        transform.position = GameManager.Instance.currentCheckpoint ?
                             GameManager.Instance.currentCheckpoint.position :
                             transform.position;

        velocity = Vector3.zero;
        controller.enabled = true;

        _animator.ResetTrigger("Jump");
        _animator.ResetTrigger("DoubleJump");
        _animator.SetBool("Jump", false);
        _animator.SetBool("DoubleJump", false);
        jumpCount = 0;

        if (sprintFX) sprintFX.StopEffect();
        sprintFXActive = false;
    }

    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (hit.collider.CompareTag("MovingPlatform") || hit.collider.CompareTag("RotatingPlatform"))
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
        currentHealth -= amount;
        playerUI.UpdateHealth(currentHealth);
        attackEffectUI?.PulseIcon();
        if (currentHealth <= 0) currentHealth = 0;
    }
}
