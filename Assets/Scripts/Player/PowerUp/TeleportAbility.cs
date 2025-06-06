using UnityEngine;

public class TeleportAbility : AbilityBase
{
    [Header("Teletrasporto")]
    public GameObject telePointerPrefab;
    public LayerMask teleportableLayers;
    public SkinnedMeshRenderer[] meshesToHide;

    [Header("Controller")]
    [Tooltip("Il GameObject che contiene il CharacterController da usare per il teletrasporto.")]
    public GameObject controllerGameObject;

    private GameObject currentPointer;

    void Update()
    {
        if (!IsActive) return;

        UpdatePointerPosition();

        if (Input.GetMouseButtonDown(1)) // click destro per confermare il teletrasporto
        {
            if (powerUpScript.HasEnoughPower(powerCost))
            {
                TeleportToPointer();
                powerUpScript.SpendPower(powerCost);
            }
            else
            {
                Debug.Log("Non hai abbastanza potere per il teletrasporto.");
            }

            Deactivate();
        }
    }

    public override void Activate()
    {
        IsActive = true;
        currentPointer = Instantiate(telePointerPrefab);
        SetVisible(false);
    }

    public override void Deactivate()
    {
        if (!IsActive) return;

        IsActive = false;

        if (currentPointer)
            Destroy(currentPointer);

        SetVisible(true);
        powerUpScript.UpdatePowerBar();

        Debug.Log("TeleportAbility disattivata.");
    }

    private void UpdatePointerPosition()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 500f, teleportableLayers))
        {
            currentPointer.SetActive(true);

            Vector3 adjustedPoint = hit.point;
            adjustedPoint.y += 0.1f;

            currentPointer.transform.position = adjustedPoint;
            currentPointer.transform.rotation = Quaternion.LookRotation(hit.normal);

            Debug.DrawRay(ray.origin, ray.direction * 500f, Color.green);
            Debug.DrawRay(hit.point, hit.normal, Color.red);
        }
        else
        {
            currentPointer.SetActive(false);
        }
    }

    private void TeleportToPointer()
    {
        if (currentPointer == null || !currentPointer.activeSelf)
        {
            Debug.Log("Punto di teletrasporto non valido.");
            return;
        }

        Vector3 targetPosition = currentPointer.transform.position;

        if (controllerGameObject != null)
        {
            CharacterController controller = controllerGameObject.GetComponent<CharacterController>();
            if (controller != null)
            {
                targetPosition.y += controller.height / 2f;

                controller.enabled = false;
                controllerGameObject.transform.position = targetPosition;
                controller.enabled = true;
            }
            else
            {
                Debug.LogWarning("Nessun CharacterController trovato nel GameObject assegnato.");
            }
        }
        else
        {
            Debug.LogWarning("Nessun GameObject controller assegnato.");
        }
    }

    private void SetVisible(bool visible)
    {
        foreach (var mesh in meshesToHide)
        {
            if (mesh != null)
                mesh.enabled = visible;
        }
    }
}
