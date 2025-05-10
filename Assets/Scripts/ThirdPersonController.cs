using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class ThirdPersonController : MonoBehaviour
{
    // MOVIMENTO
    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float rotationSmoothTime = 0.1f;

    // MOVIMENTO VARIABILI
    private Vector3 velocity;
    private float rotationVelocity;

    // SALTO 
    [Header("Jump Settings")]
    public float jumpHeight = 4f;
    public float gravity = -9.81f;
    public int maxJumps = 2; // Numero massimo di salti
    private int jumpCount = 0; // Contatore dei salti effettuati

    // RIFERIMENTI AL PLAYER
    private CharacterController controller;
    private Animator _animator;

    [Header("References")]
    public Transform cameraTransform;

    private bool wasGroundedLastFrame; // Variabile per sapere se era a terra nel frame precedente
    private bool Jump; // Flag per verificare se è in salto

    private bool isLanding = false; // Variabile per gestire il landing

    public float landingDistance = 0.5f; // Distanza dalla quale consideriamo che il personaggio è vicino al terreno per il landing

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

        // Controllo del raycast per il terreno
        CheckLanding();

        // Gestione della gravità e reset del contatore dei salti
        if (controller.isGrounded)
        {
            if (!wasGroundedLastFrame)
            {
                // Reset del salto quando tocca terra
                jumpCount = 0;
                _animator.SetBool("Jump", false);
                _animator.SetBool("DoubleJump", false);
            }
            velocity.y = -2f; // Piccola gravità per rimanere ancorati al suolo
        }
        else
        {
            velocity.y += gravity * Time.deltaTime; // Applicazione della gravità
        }

        wasGroundedLastFrame = controller.isGrounded;

        // ✅ Gestione caduta nel vuoto (FreeFall)
        bool isFreeFalling = !controller.isGrounded && velocity.y < 0f && !isLanding && !_animator.GetBool("Jump");
        _animator.SetBool("FreeFall", isFreeFalling);


        // Gestione attacco con Trigger
        if (Input.GetKeyDown(KeyCode.K))
        {
           _animator.SetTrigger("Attack");
        }

    }

    // Gestisce il movimento orizzontale
    private void HandleMovement()
    {
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        Vector3 inputDirection = new Vector3(horizontal, 0f, vertical).normalized;

        if (inputDirection.magnitude < 0.1f)
        {
            _animator.SetFloat("Speed", 0f); // Anima "Idle"
            return;
        }

        _animator.SetFloat("Speed", 1f); // Anima "Run"

        float targetAngle = Mathf.Atan2(inputDirection.x, inputDirection.z) * Mathf.Rad2Deg + cameraTransform.eulerAngles.y;
        float smoothedAngle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref rotationVelocity, rotationSmoothTime);

        transform.rotation = Quaternion.Euler(0f, smoothedAngle, 0f);

        Vector3 moveDirection = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
        controller.Move(moveDirection.normalized * moveSpeed * Time.deltaTime);
    }

    // Gestisce il salto e il doppio salto
    private void HandleJump()
    {
        if (Input.GetButtonDown("Jump") && jumpCount < maxJumps)
        {
            _animator.SetBool("Jump", true);
            if (jumpCount == 1)
            {
                _animator.SetBool("DoubleJump", true);
            }

            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            jumpCount++;
        }

        controller.Move(velocity * Time.deltaTime); // Movimento del personaggio, inclusa la gravità e il salto
    }

    // Controlla la distanza dal terreno e imposta "Landing" su true quando il personaggio è vicino
    private void CheckLanding()
    {
        RaycastHit hit;
        if (Physics.Raycast(transform.position, Vector3.down, out hit, landingDistance))
        {
            // Se il raycast colpisce il terreno e non è già in atterraggio, settiamo 'Landing' a true
            if (!isLanding)
            {
                isLanding = true;
                _animator.SetBool("Landing", true); // Impostiamo Landing su true
            }
        }
        else
        {
            // Se non c'è terreno sotto, non siamo in atterraggio
            if (isLanding)
            {
                isLanding = false;
                _animator.SetBool("Landing", false); // Impostiamo Landing su false
            }
        }
    }
}
