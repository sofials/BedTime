using UnityEngine;

public class PlatformSpawnAbility : AbilityBase
{
    public GameObject platformPrefab;
    public GameObject ghostPrefab;
    public float offsetDistance = 1.5f;
    public float platformCheckRadius = 0.5f;
    public LayerMask obstacleMask;

    private GameObject currentGhost;
    private Transform cameraTransform;

    private bool placing = false;

    private void Start()
    {
        cameraTransform = Camera.main.transform;
    }

    public override void Activate()
    {
        if (placing || currentGhost != null) return;

        Vector3 spawnPos;
        if (!TryGetSpawnPosition(out spawnPos))
        {
            Debug.Log("Nessuna piattaforma sotto la camera.");
            return;
        }

        currentGhost = Instantiate(ghostPrefab, spawnPos, Quaternion.identity);
        AlignGhostToCamera();
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

        // Aggiorna la posizione e rotazione del ghost in tempo reale
        Vector3 spawnPos;
        if (TryGetSpawnPosition(out spawnPos))
        {
            currentGhost.transform.position = spawnPos;
            AlignGhostToCamera();
        }
        else
        {
            Debug.Log("Nessuna piattaforma sotto la camera (update).");
        }

        // Clic sinistro per piazzare la piattaforma
        if (Input.GetMouseButtonDown(0))
        {
            if (CanPlacePlatform(currentGhost.transform.position))
            {
                Instantiate(platformPrefab, currentGhost.transform.position, currentGhost.transform.rotation);
                powerUpScript.SpendPower(powerCost);
                Deactivate();
            }
            else
            {
                Debug.Log("Spazio occupato! Impossibile piazzare.");
            }
        }
    }

    private bool TryGetSpawnPosition(out Vector3 spawnPos)
    {
        Vector3 origin = cameraTransform.position + Vector3.up * 0.1f;
        float maxDistance = 3f;

        Debug.DrawRay(origin, Vector3.down * maxDistance, Color.red);

        if (Physics.SphereCast(origin, 0.3f, Vector3.down, out RaycastHit hit, maxDistance, obstacleMask))
        {
            Vector3 forward = cameraTransform.forward;
            forward.y = 0;
            forward.Normalize();

            spawnPos = hit.point + forward * offsetDistance;
            spawnPos.y = hit.point.y; // allinea altezza piattaforma
            return true;
        }

        spawnPos = Vector3.zero;
        return false;
    }

    private void AlignGhostToCamera()
    {
        Vector3 forward = cameraTransform.forward;
        forward.y = 0;
        if (forward != Vector3.zero)
            currentGhost.transform.rotation = Quaternion.LookRotation(forward);
    }

    private bool CanPlacePlatform(Vector3 position)
    {
        return !Physics.CheckSphere(position, platformCheckRadius, obstacleMask);
    }
}
