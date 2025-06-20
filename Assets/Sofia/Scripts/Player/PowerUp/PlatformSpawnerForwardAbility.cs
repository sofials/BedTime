using UnityEngine;
using UnityEngine.InputSystem;

public class PlatformSpawnerForwardAbility : AbilityBase
{
    public GameObject platformPrefab;
    public GameObject ghostPrefab;
    public float forwardDistance = 2f;
    public float verticalOffset = 0f;
    public float checkRadius = 0.4f;
    public LayerMask obstacleMask;
    public override int powerCost => 10;

    public Transform footTarget;

    private GameObject currentGhost;
    private Transform cameraTransform;
    private bool placing = false;

    private Vector3 lastForwardDirection;
    private Vector3 currentGhostVelocity;
    private Quaternion targetRotation;
    private float rotationSmoothSpeed = 15f;
    private float updateAngleThreshold = 10f;

    private PlayerControls controls;
    private bool confirmPressed;

    private void Start()
    {
        cameraTransform = Camera.main.transform;
    }

    private void Awake()
    {
        controls = new PlayerControls();
        controls.Gameplay.Confirm.performed += ctx => confirmPressed = true;
        controls.Enable(); // da disattivare eventualmente se vuoi OnEnable/OnDisable
    }

    public override void Activate()
    {
        if (placing || currentGhost != null) return;

        placing = true;
        IsActive = true;

        lastForwardDirection = GetCameraForwardFlat();

        Vector3 spawnPos = GetSpawnPosition(lastForwardDirection);
        currentGhost = Instantiate(ghostPrefab, spawnPos, Quaternion.identity);
        targetRotation = Quaternion.LookRotation(lastForwardDirection);
        currentGhost.transform.rotation = targetRotation;
    }

    public override void Deactivate()
    {
        if (currentGhost != null)
            Destroy(currentGhost);

        placing = false;
        IsActive = false;
    }

    private void Update()
    {
        if (!placing || currentGhost == null) return;

        Vector3 currentCamForward = GetCameraForwardFlat();
        float angleDifference = Vector3.Angle(lastForwardDirection, currentCamForward);

        if (angleDifference > updateAngleThreshold)
        {
            lastForwardDirection = currentCamForward;
            targetRotation = Quaternion.LookRotation(currentCamForward);
        }

        Vector3 targetPos = GetSpawnPosition(lastForwardDirection);
        currentGhost.transform.position = Vector3.SmoothDamp(currentGhost.transform.position, targetPos, ref currentGhostVelocity, 0.1f);
        currentGhost.transform.rotation = Quaternion.Slerp(currentGhost.transform.rotation, targetRotation, Time.deltaTime * rotationSmoothSpeed);

        if (confirmPressed)
        {
            confirmPressed = false;
            if (!powerUpScript.HasEnoughPower(powerCost))
            {
                Debug.Log("Energia insufficiente!");
                return;
            }

            if (CanPlacePlatform(targetPos))
            {
                Instantiate(platformPrefab, targetPos, currentGhost.transform.rotation);
                powerUpScript.SpendPower(powerCost);
                Deactivate();
            }
            else
            {
                Debug.Log("Spazio occupato!");
            }
        }
    }

    private Vector3 GetCameraForwardFlat()
    {
        Vector3 forward = cameraTransform.forward;
        forward.y = 0f;
        return forward.normalized;
    }

    private Vector3 GetSpawnPosition(Vector3 direction)
    {
        Vector3 basePos = footTarget != null ? footTarget.position : transform.position;
        basePos.y += verticalOffset;
        return basePos + direction * forwardDistance;
    }

    private bool CanPlacePlatform(Vector3 position)
    {
        return !Physics.CheckSphere(position, checkRadius, obstacleMask);
    }
}
