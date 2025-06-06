using UnityEngine;

public class PlatformAbility : AbilityBase
{
    [Header("Platform Prefabs")]
    public GameObject platformPrefab;
    public GameObject ghostValidPrefab;
    public GameObject ghostInvalidPrefab;

    [Header("Placement Settings")]
    public LayerMask obstacleLayers;
    public float placementDistance = 3f;
    public float extraOffset = 3f;

    private GameObject ghostValidInstance;
    private GameObject ghostInvalidInstance;

    private bool isPlacing = false;
    private GameObject currentGround;
    private Transform camTransform;

    private bool lastPlacementValid = false;

    public bool IsPlacing => isPlacing;

    void Start()
    {
        camTransform = Camera.main.transform;

        if (ghostValidPrefab != null)
        {
            ghostValidInstance = Instantiate(ghostValidPrefab);
            ghostValidInstance.SetActive(false);
        }

        if (ghostInvalidPrefab != null)
        {
            ghostInvalidInstance = Instantiate(ghostInvalidPrefab);
            ghostInvalidInstance.SetActive(false);
        }

        duration = null; // chiarisco esplicitamente che questa abilità non ha durata automatica
    }

    void Update()
    {
        if (IsActive)
        {
            UpdateGhostPosition();

            if (Input.GetMouseButtonDown(1)) // Click destro per piazzare
            {
                TryPlacePlatform();
            }
        }
    }

    public override void Activate()
    {
        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, placementDistance, obstacleLayers))
        {
            currentGround = hit.collider.gameObject;

            if (ghostValidInstance != null && ghostInvalidInstance != null)
            {
                ghostValidInstance.SetActive(true);
                ghostInvalidInstance.SetActive(false);
                isPlacing = true;
            }
        }
        else
        {
            Debug.Log("Nessun terreno valido trovato per il piazzamento.");
            Deactivate(); // annulla subito se non puoi piazzare
        }
    }

    public override void Deactivate()
    {
        isPlacing = false;
        IsActive = false;

        if (ghostValidInstance != null) ghostValidInstance.SetActive(false);
        if (ghostInvalidInstance != null) ghostInvalidInstance.SetActive(false);
    }

    void UpdateGhostPosition()
    {
        if (currentGround == null || ghostValidInstance == null || ghostInvalidInstance == null) return;

        Vector3 camForward = camTransform.forward;
        camForward.y = 0;
        camForward.Normalize();

        Vector3[] directions = {
            currentGround.transform.forward,
            -currentGround.transform.forward,
            currentGround.transform.right,
            -currentGround.transform.right
        };

        float maxDot = float.NegativeInfinity;
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
        Collider ghostCollider = ghostValidInstance.GetComponent<Collider>();

        if (groundCollider == null || ghostCollider == null) return;

        Bounds groundBounds = groundCollider.bounds;
        Bounds ghostBounds = ghostCollider.bounds;

        Vector3 offset = bestDir.normalized * (
            Vector3.Project(groundBounds.extents, bestDir).magnitude +
            Vector3.Project(ghostBounds.extents, bestDir).magnitude +
            extraOffset
        );

        Vector3 targetPos = groundBounds.center + offset;
        Vector3 boxSize = ghostBounds.extents;

        Collider[] overlaps = Physics.OverlapBox(targetPos, boxSize, Quaternion.identity, obstacleLayers);
        bool isValid = overlaps.Length == 0;

        if (isValid != lastPlacementValid)
        {
            ghostValidInstance.SetActive(isValid);
            ghostInvalidInstance.SetActive(!isValid);
            lastPlacementValid = isValid;
        }

        ghostValidInstance.transform.position = targetPos;
        ghostInvalidInstance.transform.position = targetPos;
        ghostValidInstance.transform.rotation = Quaternion.identity;
        ghostInvalidInstance.transform.rotation = Quaternion.identity;
    }

    void TryPlacePlatform()
    {
        if (!lastPlacementValid || ghostValidInstance == null) return;

        Vector3 position = ghostValidInstance.transform.position;
        Collider ghostCollider = ghostValidInstance.GetComponent<Collider>();
        Vector3 ghostSize = ghostCollider.bounds.size;

        Collider[] overlaps = Physics.OverlapBox(position, ghostSize / 2f, Quaternion.identity, obstacleLayers);
        if (overlaps.Length == 0)
        {
            Instantiate(platformPrefab, position, Quaternion.identity);
            Deactivate();  // disattivo l'abilità dopo il piazzamento
        }
        else
        {
            Debug.Log("Non puoi piazzare qui, spazio occupato.");
        }
    }
}
