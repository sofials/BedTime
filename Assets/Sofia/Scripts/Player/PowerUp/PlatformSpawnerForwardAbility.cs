using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class PlatformSpawnerForwardAbility : AbilityBase
{
    [Header("Platform Spawner")]
    public GameObject platformPrefab;
    public GameObject ghostPrefab;
    public float forwardDistance  = 2f;
    public float verticalOffset   = 0f;
    public float checkRadius      = 0.4f;
    public LayerMask obstacleMask;

    public override int powerCost => 75;

    [Header("References")]
    public Transform footTarget;
    [SerializeField] private Transform cameraTransform; // Make this assignable in inspector

    private GameObject currentGhost;
    private bool       placing = false;

    private Vector3    lastForwardDirection;
    private Vector3    currentGhostVelocity;
    private Quaternion targetRotation;
    private const float rotationSmoothSpeed   = 15f;
    private const float updateAngleThreshold  = 10f;

    private PlayerControls controls;
    private bool confirmPressed;

    private float activationClipLength = 0.5f; // durata suono attivazione (modifica se serve)

    protected override void Awake()
    {
        base.Awake(); // importante per AudioSource
        controls = new PlayerControls();
        controls.Gameplay.Confirm.performed += _ => confirmPressed = true;
        controls.Enable();

        effectIconIndex = 3; // slot icona dedicato
        
        // Try to find camera in Awake if not assigned
        if (cameraTransform == null)
        {
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                cameraTransform = mainCamera.transform;
            }
            else
            {
                // Fallback: find any camera
                Camera anyCamera = Object.FindFirstObjectByType<Camera>();
                if (anyCamera != null)
                {
                    cameraTransform = anyCamera.transform;
                    Debug.LogWarning($"MainCamera not found, using {anyCamera.name} instead.");
                }
            }
        }
    }

    private void Start()
    {
        // Final attempt to find camera if still null
        if (cameraTransform == null)
        {
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                cameraTransform = mainCamera.transform;
            }
            else
            {
                Debug.LogError("No camera found! Please assign a camera transform in the inspector or ensure there's a MainCamera in the scene.");
                enabled = false; // Disable this component to prevent further errors
                return;
            }
        }
    }

    protected override void Update()
    {
        base.Update();

        if (!placing || currentGhost == null || cameraTransform == null) return;

        Vector3 camForward = GetCameraForwardFlat();
        float angle = Vector3.Angle(lastForwardDirection, camForward);

        if (angle > updateAngleThreshold)
        {
            lastForwardDirection = camForward;
            targetRotation       = Quaternion.LookRotation(camForward);
        }

        Vector3 targetPos = GetSpawnPosition(lastForwardDirection);

        currentGhost.transform.position = Vector3.SmoothDamp(
            currentGhost.transform.position, targetPos,
            ref currentGhostVelocity, 0.1f);

        currentGhost.transform.rotation = Quaternion.Slerp(
            currentGhost.transform.rotation, targetRotation,
            Time.deltaTime * rotationSmoothSpeed);

        if (!confirmPressed) return;
        confirmPressed = false;

        if (!powerUpScript.HasEnoughPower(powerCost))
        {
            Debug.Log("Energia insufficiente!");
            return;
        }

        if (!CanPlacePlatform(targetPos))
        {
            Debug.Log("Spazio occupato!");
            return;
        }

        Destroy(currentGhost);
        currentGhost = null;

        Instantiate(platformPrefab, targetPos, targetRotation);

        powerUpScript.SpendPower(powerCost);
        Deactivate();
    }

    public override void TryActivate()
    {
        if (IsActive)
        {
            Deactivate();
        }
        else if (CanActivate())
        {
            base.TryActivate(); // questo fa partire il suono di attivazione
        }
        else
        {
            Debug.Log("Impossibile attivare lo spawn piattaforma.");
        }
    }

    public override void Activate()
    {
        if (placing || currentGhost || cameraTransform == null) return;

        placing = true;
        IsActive = true;

        lastForwardDirection = GetCameraForwardFlat();
        Vector3 spawnPos     = GetSpawnPosition(lastForwardDirection);

        currentGhost = Instantiate(ghostPrefab, spawnPos, Quaternion.identity);
        targetRotation = Quaternion.LookRotation(lastForwardDirection);
        currentGhost.transform.rotation = targetRotation;

        DrawRevealEffect drawEffect = currentGhost.GetComponent<DrawRevealEffect>();
        if (drawEffect != null)
        {
            drawEffect.ResetDraw();

            // Fa partire l'audio loop dopo il suono di attivazione
            StartCoroutine(StartDrawAudioAfterDelay(drawEffect));
        }
    }

    private IEnumerator StartDrawAudioAfterDelay(DrawRevealEffect drawEffect)
    {
        yield return new WaitForSeconds(activationClipLength);
        drawEffect.PlayLoopAudio();
    }

    public override void Deactivate()
    {
        if (currentGhost) Destroy(currentGhost);

        placing = false;
        IsActive = false;
    }

    private Vector3 GetCameraForwardFlat()
    {
        if (cameraTransform == null) return transform.forward; // Fallback to object's forward
        
        Vector3 f = cameraTransform.forward;
        f.y = 0f;
        return f.normalized;
    }

    private Vector3 GetSpawnPosition(Vector3 dir)
    {
        Vector3 basePos = footTarget ? footTarget.position : transform.position;
        basePos.y += verticalOffset;
        return basePos + dir * forwardDistance;
    }

    private bool CanPlacePlatform(Vector3 pos)
    {
        return !Physics.CheckSphere(pos, checkRadius, obstacleMask);
    }

    private void OnDestroy()
    {
        if (controls != null)
        {
            controls.Disable();
            controls.Dispose();
        }
    }
}