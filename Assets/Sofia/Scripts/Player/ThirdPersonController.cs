using UnityEngine;
using System.Collections;
using UnityEngine.InputSystem;


[RequireComponent(typeof(CharacterController))]
public class ThirdPersonController : MonoBehaviour
{
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

    private CharacterController controller;
    private Animator _animator;

    [Header("References")]
    public Transform cameraTransform;

    private bool wasGroundedLastFrame;
    private Transform currentPlatform = null;
    private Vector3 lastPlatformPosition = Vector3.zero;
    private Vector3 platformVelocity = Vector3.zero;

    private Vector3 playerVelocity;
    private Vector3 externalPush = Vector3.zero;
    [SerializeField] private float pushRecoverySpeed = 1f;

    //Per usare Input Actions
    private PlayerControls controls;
    private Vector2 moveInput;
    private bool jumpInput;
    private bool isSprinting;
    private bool isHoldingJump;


    void Start()
    {
        controls = new PlayerControls();
        _animator = GetComponentInChildren<Animator>();
        controller = GetComponent<CharacterController>();
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;
    }

    //Input Actions
    private void Awake()
    {
        controls = new PlayerControls();

        controls.Gameplay.Move.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        controls.Gameplay.Move.canceled += ctx => moveInput = Vector2.zero;

        controls.Gameplay.Sprint.performed += ctx => isSprinting = true;
        controls.Gameplay.Sprint.canceled += ctx => isSprinting = false;

        controls.Gameplay.Jump.started += ctx =>
        {
            jumpInput = true;
            isHoldingJump = true;
        };

        controls.Gameplay.Jump.canceled += ctx =>
        {
            isHoldingJump = false;
        };

    }

    private void OnEnable()
    {
        controls.Gameplay.Enable();
    }

    private void OnDisable()
    {
        controls.Gameplay.Disable();
    }


    void Update()
    {
        UpdatePlatformVelocity();
        HandleMovement();
        HandleJump();

        Vector3 totalMove = playerVelocity + externalPush + platformVelocity;
        totalMove.y = velocity.y;
        controller.Move(totalMove * Time.deltaTime);

        // Decadimento progressivo della spinta
        externalPush = Vector3.Lerp(externalPush, Vector3.zero, Time.deltaTime * pushRecoverySpeed);


        bool isGrounded = controller.isGrounded;
        _animator.SetBool("isGrounded", isGrounded);

        if (isGrounded && !wasGroundedLastFrame)
        {
            jumpCount = 0;
            _animator.SetBool("Jump", false);
            _animator.SetBool("DoubleJump", false);
        }

        wasGroundedLastFrame = isGrounded;
        bool isFalling = false;

        if (!isGrounded && velocity.y < -3f && !_animator.GetBool("Jump") && !_animator.GetBool("DoubleJump"))
        {
            RaycastHit hit;
            if (Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, out hit, 1.5f))
            {
                float groundAngle = Vector3.Angle(hit.normal, Vector3.up);
                if (groundAngle > controller.slopeLimit + 5f)
                {
                    isFalling = true;
                }
            }
            else
            {
                isFalling = true;
            }
        }

        _animator.SetBool("isFalling", isFalling);

        if (!isGrounded)
        {
            float horizontal = moveInput.x;
            float vertical = moveInput.y;

            Vector3 inputDirection = new Vector3(horizontal, 0f, vertical).normalized;

            if (inputDirection.magnitude >= 0.1f)
            {
                float targetAngle = Mathf.Atan2(inputDirection.x, inputDirection.z) * Mathf.Rad2Deg + cameraTransform.eulerAngles.y;
                float smoothedAngle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref rotationVelocity, airRotationSmoothTime);
                transform.rotation = Quaternion.Euler(0f, smoothedAngle, 0f);

                Vector3 moveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
                Vector3 airMove = moveDir.normalized * airControlSpeed;
                playerVelocity += new Vector3(airMove.x, 0f, airMove.z);
            }
        }
    }

    private void UpdatePlatformVelocity()
    {
        if (currentPlatform != null)
        {
            platformVelocity = (currentPlatform.position - lastPlatformPosition) / Time.deltaTime;
            lastPlatformPosition = currentPlatform.position;
        }
        else
        {
            platformVelocity = Vector3.zero;
        }
    }

    private void HandleMovement()
    {
        float horizontal = moveInput.x;
        float vertical = moveInput.y;

        Vector3 inputDirection = new Vector3(horizontal, 0f, vertical).normalized;

        float inputMagnitude = inputDirection.magnitude;
        smoothInputMagnitude = Mathf.Lerp(smoothInputMagnitude, inputMagnitude, Time.deltaTime * 5f);

        if (inputMagnitude < 0.1f)
        {
            playerVelocity = Vector3.zero;
            _animator.SetFloat("Speed", 0f, 0.1f, Time.deltaTime);
            return;
        }

        float targetAngle = Mathf.Atan2(inputDirection.x, inputDirection.z) * Mathf.Rad2Deg + cameraTransform.eulerAngles.y;
        float smoothedAngle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref rotationVelocity, rotationSmoothTime);
        transform.rotation = Quaternion.Euler(0f, smoothedAngle, 0f);

        Vector3 moveDirection = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;

        // ✅ Usa il valore dallo sprint del controller
        float targetSpeed = isSprinting ? sprintSpeed :
                            (smoothInputMagnitude < 0.5f ? walkSpeed : runSpeed);

        playerVelocity = moveDirection.normalized * targetSpeed;

        float maxSpeed = sprintSpeed;
        float speedNormalized = Mathf.Clamp01(playerVelocity.magnitude / maxSpeed);

        _animator.SetFloat("Speed", speedNormalized, 0.1f, Time.deltaTime);
    }

    private void HandleJump()
    {
        bool isGrounded = controller.isGrounded;

        if (jumpInput && jumpCount < maxJumps)
        {
            if (jumpCount == 0)
            {
                _animator.SetBool("Jump", true);
                _animator.SetBool("DoubleJump", false);
            }
            else if (jumpCount == 1)
            {
                _animator.SetBool("DoubleJump", true);
                _animator.SetBool("Jump", false);
            }

            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            jumpCount++;

            //Input Actions: questo resetta l'input dopo il salto
            jumpInput = false;
        }

        if (velocity.y < 0)
        {
            // Discesa più veloce
            velocity.y += gravity * 2.5f * Time.deltaTime;
        }
        else if (velocity.y > 0 && !isHoldingJump)
        {
            // Se hai rilasciato il tasto durante la salita: scendi prima (salto corto)
            velocity.y += gravity * 2f * Time.deltaTime;
        }
        else
        {
            // Salita normale
            velocity.y += gravity * Time.deltaTime;
        }


        if (controller.isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }
    }

    public void Respawn()
    {
        controller.enabled = false;
        if (GameManager.Instance.currentCheckpoint != null)
        {
            transform.position = GameManager.Instance.currentCheckpoint.position;
        }
        else
        {
            Debug.LogWarning("Nessun checkpoint impostato! Respawn nella posizione iniziale.");
        }

        velocity = Vector3.zero;
        controller.enabled = true;

        _animator.SetBool("Jump", false);
        _animator.SetBool("DoubleJump", false);
        jumpCount = 0;
    }

    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (hit.collider.CompareTag("MovingPlatform"))
        {
            if (currentPlatform != hit.collider.transform)
            {
                currentPlatform = hit.collider.transform;
                lastPlatformPosition = currentPlatform.position;
            }
        }
        else
        {
            if (currentPlatform != null && hit.collider.transform != currentPlatform)
            {
                currentPlatform = null;
                platformVelocity = Vector3.zero;
            }
        }
    }

    public void ApplyKnockback(Vector3 direction, float force, float duration)
    {
        // Direzione del knockback in orizzontale (y = 0)
        direction.y = 0;
        direction.Normalize();

        // Avvia una coroutine per gestire il knockback temporaneo
        StartCoroutine(KnockbackCoroutine(direction, force, duration));
    }

    private IEnumerator KnockbackCoroutine(Vector3 direction, float force, float duration)
    {
        float timer = 0f;

        while (timer < duration)
        {
            // Muove il player in direzione opposta per il knockback
            controller.Move(direction * force * Time.deltaTime);

            timer += Time.deltaTime;
            yield return null;
        }
    }
    public void ApplyExternalPush(Vector3 push)
    {
        externalPush = push;
    }


}
