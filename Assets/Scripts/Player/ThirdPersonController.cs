using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class ThirdPersonController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float rotationSmoothTime = 0.1f;

    private Vector3 velocity;
    private float rotationVelocity;

    [Header("Jump Settings")]
    public float jumpHeight = 4f;
    public float gravity = -9.81f;
    public int maxJumps = 2;
    private int jumpCount = 0;

    private CharacterController controller;
    private Animator _animator;

    [Header("References")]
    public Transform cameraTransform;

    private bool wasGroundedLastFrame;
    private bool isLanding = false;
    public float landingDistance = 0.5f;

    [Header("Attack")]
    public GameObject attackHitbox; // assegna da Inspector


    void Start()
    {
        _animator = GetComponent<Animator>();
        controller = GetComponent<CharacterController>();
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;
    }

    void Update()
    {
        HandleMovement();
        HandleJump();
        CheckLanding();

        if (controller.isGrounded)
        {
            if (!wasGroundedLastFrame)
            {
                jumpCount = 0;
                _animator.SetBool("Jump", false);
                _animator.SetBool("DoubleJump", false);
            }
            velocity.y = -2f;
        }
        else
        {
            velocity.y += gravity * Time.deltaTime;
        }

        wasGroundedLastFrame = controller.isGrounded;

        bool isFreeFalling = !controller.isGrounded && velocity.y < 0f && !isLanding && !_animator.GetBool("Jump");
        _animator.SetBool("FreeFall", isFreeFalling);

        if (Input.GetMouseButtonDown(0))
        {
            _animator.SetTrigger("Attack");
        }
    }

    private void HandleMovement()
    {
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        Vector3 inputDirection = new Vector3(horizontal, 0f, vertical).normalized;

        if (inputDirection.magnitude < 0.1f)
        {
            _animator.SetFloat("Speed", 0f);
            return;
        }

        _animator.SetFloat("Speed", 1f);

        float targetAngle = Mathf.Atan2(inputDirection.x, inputDirection.z) * Mathf.Rad2Deg + cameraTransform.eulerAngles.y;
        float smoothedAngle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref rotationVelocity, rotationSmoothTime);

        transform.rotation = Quaternion.Euler(0f, smoothedAngle, 0f);

        Vector3 moveDirection = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
        controller.Move(moveDirection.normalized * moveSpeed * Time.deltaTime);
    }

    private void HandleJump()
    {
        if (Input.GetButtonDown("Jump") && jumpCount < maxJumps)
        {
            _animator.SetBool("Jump", true);
            if (jumpCount == 1)
                _animator.SetBool("DoubleJump", true);

            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            jumpCount++;
        }

        controller.Move(velocity * Time.deltaTime);
    }

    private void CheckLanding()
    {
        RaycastHit hit;
        if (Physics.Raycast(transform.position, Vector3.down, out hit, landingDistance))
        {
            if (!isLanding)
            {
                isLanding = true;
                _animator.SetBool("Landing", true);
            }
        }
        else
        {
            if (isLanding)
            {
                isLanding = false;
                _animator.SetBool("Landing", false);
            }
        }
    }

    // Metodo pubblico chiamato dalla KillZone per il respawn
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
        _animator.SetBool("Landing", false);
        _animator.SetBool("FreeFall", false);

        jumpCount = 0;
    }

    public void ActivateHitbox()
    {
       attackHitbox.SetActive(true);
    }

    public void DeactivateHitbox()
    {
       attackHitbox.SetActive(false);
    }
}
