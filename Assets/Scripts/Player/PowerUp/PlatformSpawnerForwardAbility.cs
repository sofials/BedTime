using UnityEngine;

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

    private int directionIndex = 0; // 0 = davanti, 1 = destra, 2 = dietro, 3 = sinistra
    private float mouseSensitivity = 5f;
    private float mouseXAccum = 0f;

    private Vector3 currentGhostVelocity; // per SmoothDamp posizione
    private float rotationSmoothSpeed = 15f;

    private Quaternion targetRotation;  // memorizza rotazione target stabile

    private void Start()
    {
        cameraTransform = Camera.main.transform;
    }

    public override void Activate()
    {
        if (placing || currentGhost != null) return;

        directionIndex = 0;
        mouseXAccum = 0f;

        Vector3 spawnPos = GetSpawnPosition();
        currentGhost = Instantiate(ghostPrefab, spawnPos, Quaternion.identity);

        targetRotation = CalculateTargetRotation(directionIndex);
        currentGhost.transform.rotation = targetRotation;

        placing = true;
        IsActive = true;
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

        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        mouseXAccum += mouseX;

        bool directionChanged = false;

        if (mouseXAccum > 1f)
        {
            directionIndex = (directionIndex + 1) % 4; // ruota a destra
            mouseXAccum = 0f;
            directionChanged = true;
        }
        else if (mouseXAccum < -1f)
        {
            directionIndex = (directionIndex + 3) % 4; // ruota a sinistra
            mouseXAccum = 0f;
            directionChanged = true;
        }

        if (directionChanged)
        {
            targetRotation = CalculateTargetRotation(directionIndex);
        }

        Vector3 targetPos = GetSpawnPosition();
        currentGhost.transform.position = Vector3.SmoothDamp(currentGhost.transform.position, targetPos, ref currentGhostVelocity, 0.1f);

        // Solo se la direzione è significativa ruota il ghost, altrimenti no
        if (targetRotation != Quaternion.identity)
        {
            currentGhost.transform.rotation = Quaternion.Slerp(currentGhost.transform.rotation, targetRotation, Time.deltaTime * rotationSmoothSpeed);
        }

        if (Input.GetMouseButtonDown(0))
        {
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

    private Vector3 GetSpawnPosition()
    {
        Vector3 camForward = cameraTransform.forward;
        camForward.y = 0f;
        camForward.Normalize();

        Vector3 camRight = cameraTransform.right;
        camRight.y = 0f;
        camRight.Normalize();

        Vector3 direction = Vector3.zero;

        switch (directionIndex)
        {
            case 0: direction = camForward; break;
            case 1: direction = camRight; break;
            case 2: direction = -camForward; break;
            case 3: direction = -camRight; break;
        }

        Vector3 basePosition = footTarget != null ? footTarget.position : transform.position;
        basePosition.y += verticalOffset;

        return basePosition + direction * forwardDistance;
    }

    private Quaternion CalculateTargetRotation(int dirIndex)
    {
        Vector3 camForward = cameraTransform.forward;
        camForward.y = 0f;
        camForward.Normalize();

        Vector3 camRight = cameraTransform.right;
        camRight.y = 0f;
        camRight.Normalize();

        Vector3 direction = Vector3.zero;

        switch (dirIndex)
        {
            case 0: direction = camForward; break;
            case 1: direction = camRight; break;
            case 2: direction = -camForward; break;
            case 3: direction = -camRight; break;
        }

        if (direction.sqrMagnitude < 0.01f)
            return Quaternion.identity;

        return Quaternion.LookRotation(direction);
    }

    private bool CanPlacePlatform(Vector3 position)
    {
        return !Physics.CheckSphere(position, checkRadius, obstacleMask);
    }
}
