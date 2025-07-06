using UnityEngine;
using UnityEngine.InputSystem;

public class PlatformSpawnerForwardAbility : AbilityBase
{
    [Header("Platform Spawner")]
    public GameObject platformPrefab;
    public GameObject ghostPrefab;
    public float forwardDistance  = 2f;
    public float verticalOffset   = 0f;
    public float checkRadius      = 0.4f;
    public LayerMask obstacleMask;

    public override int powerCost => 35;

    [Header("References")]
    public Transform footTarget;

    private GameObject currentGhost;
    private Transform  cameraTransform;
    private bool       placing = false;

    private Vector3    lastForwardDirection;
    private Vector3    currentGhostVelocity;
    private Quaternion targetRotation;
    private const float rotationSmoothSpeed   = 15f;
    private const float updateAngleThreshold  = 10f;

    private PlayerControls controls;
    private bool confirmPressed;

    /* ---------- INITIALISATION ---------- */

    private void Awake()
    {
        controls = new PlayerControls();
        controls.Gameplay.Confirm.performed += _ => confirmPressed = true;
        controls.Enable();

        effectIconIndex = 1;      // slot icona dedicato
    }

    private void Start()
    {
        cameraTransform = Camera.main.transform;
    }

    /* ---------- MAIN LOOP ---------- */

    protected override void Update()        // ← override, non più private!
    {
        base.Update();                      // ← mantiene il feedback grigio/bianco

        if (!placing || currentGhost == null) return;

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

        Instantiate(platformPrefab, targetPos, currentGhost.transform.rotation);
        powerUpScript.SpendPower(powerCost);
        Deactivate();
    }

    /* ---------- PUBLIC API ---------- */

    public override void Activate()
    {
        if (placing || currentGhost) return;

        placing = true; IsActive = true;

        lastForwardDirection = GetCameraForwardFlat();
        Vector3 spawnPos     = GetSpawnPosition(lastForwardDirection);

        currentGhost   = Instantiate(ghostPrefab, spawnPos, Quaternion.identity);
        targetRotation = Quaternion.LookRotation(lastForwardDirection);
        currentGhost.transform.rotation = targetRotation;
    }

    public override void Deactivate()
    {
        if (currentGhost) Destroy(currentGhost);

        placing = false;
        IsActive = false;
    }

    /* ---------- INTERNAL ---------- */

    private Vector3 GetCameraForwardFlat()
    {
        Vector3 f = cameraTransform.forward; f.y = 0f;
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
}
