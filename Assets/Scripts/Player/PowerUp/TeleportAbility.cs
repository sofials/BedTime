using UnityEngine;

public class TeleportAbility : MonoBehaviour
{
    [Header("Teletrasporto")]
    public GameObject telePointerPrefab;
    public LayerMask teleportableLayers;
    public SkinnedMeshRenderer[] meshesToHide;
    public int teleportCost = 40;

    private PlayerPowerUp playerPowerUp;
    private GameObject currentPointer;
    private bool isAiming = false;

    private void Start()
    {
        playerPowerUp = GetComponent<PlayerPowerUp>();
        if (playerPowerUp == null)
        {
            Debug.LogError("TeleportAbility richiede lo script PlayerPowerUp sullo stesso oggetto.");
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.E))
        {
            if (!isAiming && playerPowerUp.HasEnoughPower(teleportCost))
            {
                StartAiming();
            }
            else if (isAiming)
            {
                CancelAiming();
            }
        }

        if (isAiming)
        {
            UpdatePointerPosition();

            if (Input.GetMouseButtonDown(1)) // click destro
            {
                if (playerPowerUp.HasEnoughPower(teleportCost))
                {
                    TeleportToPointer();
                    playerPowerUp.SpendPower(teleportCost);
                }
                else
                {
                    Debug.Log("Non hai abbastanza potere per il teletrasporto.");
                    CancelAiming();
                }
            }
        }
    }

    void StartAiming()
    {
        isAiming = true;
        currentPointer = Instantiate(telePointerPrefab);
        SetVisible(false);
    }

    void CancelAiming()
    {
        isAiming = false;
        if (currentPointer) Destroy(currentPointer);
        SetVisible(true);
    }

    void UpdatePointerPosition()
    {
         Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
         if (Physics.Raycast(ray, out RaycastHit hit, 500f, teleportableLayers))
             {
                currentPointer.SetActive(true);
                currentPointer.transform.position = hit.point;
                currentPointer.transform.rotation = Quaternion.LookRotation(hit.normal);
             }
         else
         {
                // Nessun punto valido: nascondi la freccia
                currentPointer.SetActive(false);
         }
    }

    void TeleportToPointer()
    {   
        
        if (!currentPointer.activeSelf)
        {
              Debug.Log("Punto di teletrasporto non valido.");
              return;
        }

        // Ottieni posizione bersaglio
        Vector3 targetPosition = currentPointer.transform.position;

        // Compensa l'altezza del CharacterController (per portare la base a terra)
        CharacterController controller = GetComponent<CharacterController>();
        if (controller != null)
        {
           targetPosition.y += controller.height / 2f;
        }

        // Applica il teletrasporto
        controller.enabled = false; // disabilita momentaneamente per evitare problemi
        transform.position = targetPosition;
        controller.enabled = true;

        CancelAiming();
    }

    void SetVisible(bool visible)
    {
        foreach (var mesh in meshesToHide)
        {
            mesh.enabled = visible;
        }
    }
}
