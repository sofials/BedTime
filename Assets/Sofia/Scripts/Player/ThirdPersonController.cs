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

    private void Awake()
    {
        controls = new PlayerControls();

        controls.Gameplay.Move.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        controls.Gameplay.Move.canceled += ctx => moveInput = Vector2.zero;

        controls.Gameplay.Sprint.performed += ctx => isSprinting = true;
        controls.Gameplay.Sprint.canceled += ctx => isSprinting = false;

        controls.Gameplay.Jump.started += ctx => { jumpInput = true; isHoldingJump = true; };
        controls.Gameplay.Jump.canceled += ctx => { isHoldingJump = false; };
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
        HandleJump();

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
        if (IsMovementLocked)
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

        // ✅ Proietta il movimento sulla pendenza
        moveDir = Vector3.ProjectOnPlane(moveDir, GetGroundNormal());

        float targetSpeed = isSprinting ? sprintSpeed : (smoothInputMagnitude < 0.5f ? walkSpeed : runSpeed);
        playerVelocity = moveDir.normalized * targetSpeed;

        float speedNormalized = Mathf.Clamp01(playerVelocity.magnitude / sprintSpeed);
        _animator.SetFloat("Speed", speedNormalized, 0.1f, Time.deltaTime);
    }

    private void HandleJump()
    {
        bool grounded = controller.isGrounded;

        if (jumpInput && jumpCount < maxJumps && !IsMovementLocked)
        {
            if (jumpCount == 0)
                _animator.SetBool("Jump", true);
            else
                _animator.SetBool("DoubleJump", true);

            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            jumpCount++;
            jumpInput = false;

            currentPlatform = null;
        }

        // Se siamo in aria e stiamo iniziando a cadere, disattiva double jump
        if (velocity.y < 0 && _animator.GetBool("DoubleJump"))
        {
            _animator.SetBool("DoubleJump", false);
        }

        if (velocity.y < 0) velocity.y += gravity * 2.5f * Time.deltaTime;
        else if (velocity.y > 0 && !isHoldingJump) velocity.y += gravity * 2f * Time.deltaTime;
        else velocity.y += gravity * Time.deltaTime;

        if (grounded && velocity.y < 0) velocity.y = -2f;
    }

   private void HandleFalling()
{
    bool grounded = controller.isGrounded;

    // Se non sei grounded, stai scendendo, e sei lontano dal terreno → sei davvero in caduta
    bool isTrulyFalling = !grounded && velocity.y < -5f && !IsNearGroundBelow();

    _animator.SetBool("isFalling", isTrulyFalling);
}
private bool IsNearGroundBelow()
{
    RaycastHit hit;
    float checkDistance = 0.3f; // aumenta se vuoi tolleranza maggiore
    Vector3 origin = transform.position + Vector3.up * 0.1f;
    return Physics.Raycast(origin, Vector3.down, out hit, checkDistance);
}


    private void HandleAirControl()
    {
        if (controller.isGrounded || IsMovementLocked) return;

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
        _animator.SetBool("Jump", false);
        _animator.SetBool("DoubleJump", false);

        jumpCount = 0;

        // Aggiungi questa riga:
        wasGroundedLastFrame = true;

        if (sprintFX) sprintFX.StopEffect();
        sprintFXActive = false;

        IsMovementLocked = false;
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
        if (currentHealth <= 0) return; // evita danni se già morto

        float oldHealth = currentHealth;
        currentHealth -= amount;
        currentHealth = Mathf.Max(0, currentHealth);
        playerUI.UpdateHealth(currentHealth);
        attackEffectUI?.PulseIcon();

        if (currentHealth <= 0 && oldHealth > 0)
        {
            // Blocca movimento subito
            IsMovementLocked = true;
            _animator.SetFloat("Speed", 0f);

            // Controlla se possiamo giocare animazione HitReal o solo Hit
            if (ShouldPlayHitReal())
            {
                _animator.SetTrigger("HitReal");
            }
            else
            {
                _animator.SetTrigger("Hit");
                // Respawn rapido senza animazione se sta cadendo/jumpando
                StartCoroutine(QuickRespawn());
            }
        }
        else if (currentHealth > 0)
        {
            // Se danneggiato ma non morto, trigger animazione hit se non attacca
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
        yield return new WaitForSeconds(0.1f); // piccola pausa o zero
        Respawn();
        currentHealth = maxHealth;
        playerUI.UpdateHealth(currentHealth);
        IsMovementLocked = false;
    }

    public bool ShouldPlayHitReal()
    {
        bool isFalling = !_animator.GetBool("isGrounded") && velocity.y < -2f;
        bool isJumping = _animator.GetBool("Jump") || _animator.GetBool("DoubleJump");
        return !isFalling && !isJumping;
    }

    // Animation Event callback: chiamata a fine animazione HitReal
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