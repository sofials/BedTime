using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class ThirdPersonController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float walkSpeed = 2f;
    public float runSpeed = 5f;
    public float rotationSmoothTime = 0.1f;

    private Vector3 velocity;
    private float rotationVelocity;
    private float smoothInputMagnitude;
    public float smoothTime = 0.3f; // regola quanto “morbida” è la transizione (0.1–0.2 sono valori tipici)


    [Header("Jump Settings")]
    public float jumpHeight = 4f;
    public float gravity = -9.81f;
    public int maxJumps = 2;
    private int jumpCount = 0;

    private CharacterController controller;
    private Animator _animator;

    [Header("Air Control Settings")]
    public float airControlSpeed = 2f;           // velocità orizzontale in aria
    public float airControlLerpSpeed = 2f;       // quanto velocemente risponde all’input
    public float airRotationSmoothTime = 0.3f;   // rotazione in aria (opzionale)

    [Header("References")]
    public Transform cameraTransform;

    private bool wasGroundedLastFrame;
    private bool isLanding = false;
    public float landingDistance = 0.5f;


    void Start()
    {
        _animator = GetComponentInChildren<Animator>();
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

        if (!controller.isGrounded)
        {
            float horizontal = Input.GetAxis("Horizontal");
            float vertical = Input.GetAxis("Vertical");
            Vector3 inputDirection = new Vector3(horizontal, 0f, vertical).normalized;

            // Se c'è input
            if (inputDirection.magnitude >= 0.1f)
            {
                // Calcola la direzione rispetto alla camera
                float targetAngle = Mathf.Atan2(inputDirection.x, inputDirection.z) * Mathf.Rad2Deg + cameraTransform.eulerAngles.y;
                float smoothedAngle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref rotationVelocity, airRotationSmoothTime);
                transform.rotation = Quaternion.Euler(0f, smoothedAngle, 0f);

                Vector3 moveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;

                // Muoviti in aria!
                Vector3 airMove = moveDir.normalized * airControlSpeed;
                Vector3 horizontalAirVelocity = new Vector3(airMove.x, 0f, airMove.z);

                // Applica “additivamente” solo la parte orizzontale
                controller.Move(horizontalAirVelocity * Time.deltaTime);
            }
        }

    }

    private void HandleMovement()
    {
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        Vector3 inputDirection = new Vector3(horizontal, 0f, vertical).normalized;

        // Qui calcoli la “grandezza” del movimento (0 = fermo, 1 = massimo)
        float inputMagnitude = inputDirection.magnitude;

        // Invece di usare inputMagnitude direttamente,
        // lo “ammorbidisci” con SmoothDamp
        smoothInputMagnitude = Mathf.Lerp(
          smoothInputMagnitude,
          inputMagnitude,
          Time.deltaTime * 3f
         );

        // Animazione: usa il valore smussato!
        _animator.SetFloat("Speed", smoothInputMagnitude, 0.03f, Time.deltaTime);

        // Se non c’è input, fermati
        if (inputMagnitude < 0.1f)
            return;

        // Calcola la rotazione verso la direzione desiderata
        float targetAngle = Mathf.Atan2(inputDirection.x, inputDirection.z) * Mathf.Rad2Deg + cameraTransform.eulerAngles.y;
        float smoothedAngle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref rotationVelocity, rotationSmoothTime);
        transform.rotation = Quaternion.Euler(0f, smoothedAngle, 0f);

        // Movimento fisico
        Vector3 moveDirection = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;

        float targetSpeed = smoothInputMagnitude < 0.5f ? walkSpeed : runSpeed;
        controller.Move(moveDirection.normalized * targetSpeed * Time.deltaTime);
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
}
