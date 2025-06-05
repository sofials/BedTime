using UnityEngine;

public class PlatformAbility : MonoBehaviour
{
    [Header("Power Settings")]
    public int powerCost = 40;
    public PlayerPowerUp powerUpScript;

    [Header("Placement")]
    public GameObject platformPrefab;
    public GameObject ghostPrefab;
    private GameObject ghostInstance;

    [Header("Layer Mask")]
    public LayerMask obstacleLayers;

    [Header("Offset Settings")]
    public float extraOffset = 8f;

    private Transform mainCamera;
    private bool placing = false;
    private GameObject currentGround;

    private void Start()
    {
        mainCamera = Camera.main.transform;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F) && !placing && powerUpScript.HasEnoughPower(powerCost))
        {
            TryStartPlacement();
        }

        if (placing)
        {
            bool valid = GetValidGhostPosition(out Vector3 validPosition);
            if (valid)
            {
                ghostInstance.transform.position = validPosition;
                ghostInstance.transform.rotation = Quaternion.identity;
                if (!ghostInstance.activeSelf) ghostInstance.SetActive(true);
            }
            else
            {
                if (ghostInstance.activeSelf) ghostInstance.SetActive(false);
            }

            if (Input.GetMouseButtonDown(1))
            {
                PlacePlatform();
            }
        }
    }

    void TryStartPlacement()
    {
        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 3f, obstacleLayers))
        {
            currentGround = hit.collider.gameObject;

            if (ghostInstance == null)
                ghostInstance = Instantiate(ghostPrefab);

            bool valid = GetValidGhostPosition(out Vector3 validPosition);
            if (valid)
            {
                ghostInstance.transform.position = validPosition;
                ghostInstance.transform.rotation = Quaternion.identity;
                ghostInstance.SetActive(true);
                placing = true;
            }
            else
            {
                ghostInstance.SetActive(false);
                placing = false;
            }
        }
    }

    void PlacePlatform()
    {
        if (!ghostInstance || !ghostInstance.activeSelf)
            return;

        Collider ghostCollider = ghostInstance.GetComponent<Collider>();
        Vector3 ghostSize = ghostCollider.bounds.size;

        Collider[] overlaps = Physics.OverlapBox(ghostInstance.transform.position, ghostSize / 2f, Quaternion.identity, obstacleLayers);
        if (overlaps.Length == 0)
        {
            Instantiate(platformPrefab, ghostInstance.transform.position, Quaternion.identity);
            powerUpScript.SpendPower(powerCost);
        }
        else
        {
            Debug.Log("Non puoi piazzare qui, spazio occupato.");
        }

        placing = false;
        ghostInstance.SetActive(false);
    }

    bool GetValidGhostPosition(out Vector3 position)
    {
        position = Vector3.zero;
        if (!currentGround || !ghostInstance)
            return false;

        Vector3 camForward = mainCamera.forward;
        camForward.y = 0;
        camForward.Normalize();

        // Direzione più vicina alla camera
        Vector3[] directions = {
            currentGround.transform.forward,
            -currentGround.transform.forward,
            currentGround.transform.right,
            -currentGround.transform.right
        };

        float maxDot = -Mathf.Infinity;
        Vector3 bestDir = Vector3.forward;

        foreach (var dir in directions)
        {
            float dot = Vector3.Dot(camForward, dir);
            if (dot > maxDot)
            {
                maxDot = dot;
                bestDir = dir;
            }
        }

        Collider groundCollider = currentGround.GetComponent<Collider>();
        Collider ghostCollider = ghostInstance.GetComponent<Collider>();
        if (!groundCollider || !ghostCollider)
            return false;

        Bounds groundBounds = groundCollider.bounds;
        Bounds ghostBounds = ghostCollider.bounds;

        // Offset calcolato correttamente in base all’asse migliore
        Vector3 offset = bestDir.normalized * (
            Vector3.Project(groundBounds.extents, bestDir).magnitude +
            Vector3.Project(ghostBounds.extents, bestDir).magnitude +
            extraOffset
        );

        Vector3 potentialPos = groundBounds.center + offset;

        Collider[] overlaps = Physics.OverlapBox(potentialPos, ghostBounds.extents, Quaternion.identity, obstacleLayers);
        if (overlaps.Length == 0)
        {
            position = potentialPos;
            return true;
        }

        return false;
    }
}
