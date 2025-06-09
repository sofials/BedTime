using UnityEngine;

public class PlatformAbility : AbilityBase
{
    [Header("Platform Prefabs")]
    public GameObject validPlatformPrefab;
    public GameObject ghostPlatformPrefab;
    public GameObject invalidPlatformPrefab;

    [Header("Placement Settings")]
    public float placementDistanceFromEdge = 0.5f;  // distanza dal bordo piattaforma
    public LayerMask platformLayerMask; // layer per rilevare piattaforme

    [HideInInspector]
    public Transform playerPlatform;  // aggiornato dal PlatformDetector

    private GameObject currentGhostPlatform;

    protected override bool HasFixedDuration => true; // durata gestita manualmente

    void Update()
    {
        if (!IsActive) return;

        if (playerPlatform == null)
        {
            Debug.Log("Nessuna piattaforma sotto il player");
            return;
        }

        // Calcolo posizione mouse nel mondo, proiettata sulla piattaforma sotto il player
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        Plane platformPlane = new Plane(Vector3.up, playerPlatform.position);

        if (platformPlane.Raycast(ray, out float enter))
        {
            Vector3 hitPoint = ray.GetPoint(enter);

            // Calcolo la posizione clamped sul bordo più vicino della piattaforma
            Vector3 platformCenter = playerPlatform.position;
            Vector3 localPoint = playerPlatform.InverseTransformPoint(hitPoint);

            Vector3 platformScale = playerPlatform.localScale;

            // Calcola bordo in X e Z
            float halfX = platformScale.x * 0.5f;
            float halfZ = platformScale.z * 0.5f;

            // Clamp posizione rispetto ai bordi della piattaforma
            float clampedX = Mathf.Clamp(localPoint.x, -halfX, halfX);
            float clampedZ = Mathf.Clamp(localPoint.z, -halfZ, halfZ);

            // Determina verso quale lato si è più vicini (asse X o Z)
            float distToXEdge = Mathf.Min(Mathf.Abs(clampedX - (-halfX)), Mathf.Abs(clampedX - halfX));
            float distToZEdge = Mathf.Min(Mathf.Abs(clampedZ - (-halfZ)), Mathf.Abs(clampedZ - halfZ));

            Vector3 finalLocalPos = new Vector3(clampedX, localPoint.y, clampedZ);

            // Posiziona piattaforma ghost vicino al bordo più vicino spostata di placementDistanceFromEdge
            if (distToXEdge < distToZEdge)
            {
                // Bordo X più vicino
                if (Mathf.Abs(clampedX - halfX) < Mathf.Abs(clampedX + halfX))
                    finalLocalPos.x = halfX + placementDistanceFromEdge;
                else
                    finalLocalPos.x = -halfX - placementDistanceFromEdge;
            }
            else
            {
                // Bordo Z più vicino
                if (Mathf.Abs(clampedZ - halfZ) < Mathf.Abs(clampedZ + halfZ))
                    finalLocalPos.z = halfZ + placementDistanceFromEdge;
                else
                    finalLocalPos.z = -halfZ - placementDistanceFromEdge;
            }

            Vector3 worldPos = playerPlatform.TransformPoint(finalLocalPos);

            // Controlla se lo spazio è libero (usa un boxcast con dimensioni piattaforma ghost)
            Vector3 ghostScale = ghostPlatformPrefab.transform.localScale;
            bool spaceOccupied = Physics.CheckBox(worldPos, ghostScale * 0.5f, Quaternion.identity, platformLayerMask);

            // Gestione ghost platform
            if (currentGhostPlatform == null)
            {
                currentGhostPlatform = Instantiate(spaceOccupied ? invalidPlatformPrefab : ghostPlatformPrefab, worldPos, Quaternion.identity);
            }
            else
            {
                currentGhostPlatform.transform.position = worldPos;

                // Cambia modello ghost / invalid se necessario
                bool isCurrentlyInvalid = currentGhostPlatform.name.Contains(invalidPlatformPrefab.name);
                if (spaceOccupied && !isCurrentlyInvalid)
                {
                    Destroy(currentGhostPlatform);
                    currentGhostPlatform = Instantiate(invalidPlatformPrefab, worldPos, Quaternion.identity);
                }
                else if (!spaceOccupied && isCurrentlyInvalid)
                {
                    Destroy(currentGhostPlatform);
                    currentGhostPlatform = Instantiate(ghostPlatformPrefab, worldPos, Quaternion.identity);
                }
            }

            // Se clicchi con il tasto destro e lo spazio è libero piazza la piattaforma valida
            if (Input.GetMouseButtonDown(1) && !spaceOccupied)
            {
                PlacePlatform(worldPos);
            }
        }
    }

    void PlacePlatform(Vector3 position)
    {
        Instantiate(validPlatformPrefab, position, Quaternion.identity);

        // Scala il power cost in base a qualche logica, es:
        int actualCost = powerCost; // potresti modificare in base a posizione o altro
        if (powerUpScript != null)
        {
            powerUpScript.SpendPower(powerCost);
        }

        // Disattiva l'abilità dopo piazzamento
        Deactivate();

        // Distruggi ghost platform
        if (currentGhostPlatform != null)
        {
            Destroy(currentGhostPlatform);
            currentGhostPlatform = null;
        }
    }

    public override void Activate()
    {
        IsActive = true;
    }

    public override void Deactivate()
    {
        IsActive = false;

        // Distruggi ghost platform se presente
        if (currentGhostPlatform != null)
        {
            Destroy(currentGhostPlatform);
            currentGhostPlatform = null;
        }
    }

    public override bool CanActivate()
    {
        return !IsActive && powerUpScript != null && powerUpScript.HasEnoughPower(powerCost);
    }
}
