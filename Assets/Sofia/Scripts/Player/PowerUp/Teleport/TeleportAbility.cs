using UnityEngine;
using UnityEngine.InputSystem;
using CartoonFX;
using System.Collections;

public class TeleportAbility : AbilityBase
{
    [Header("Teletrasporto Diretto")]
    public KeyCode directTeleportKey = KeyCode.E;
    public SkinnedMeshRenderer[] meshesToHide;
    
    [Header("Controller")]
    public GameObject controllerGameObject;

    [Header("Effect FX")]
    public CFXR_EffectController teleportEffectController;

    [Header("Audio")]
    public AudioClip teleportConfirmSound;
    public AudioClip teleportFailureSound;
    
    [Header("Camera Settings")]
    [SerializeField] private Camera targetCamera;
    
    [Header("Layer Settings")]
    [SerializeField] private LayerMask teleportLayerMask = -1;
    
    [Header("Raycast Settings")]
    [SerializeField] private float maxTeleportRange = 100f;
    [SerializeField] private float playerSkipDistance = 3f;
    
    [Header("Detection Settings")]
    [SerializeField] private float maxDetectionAngle = 45f; // Angolo massimo dal centro (gradi)
    [SerializeField] private float screenDetectionRadius = 200f; // Raggio in pixel dal centro schermo
    [SerializeField] private int raycastSamples = 9; // Numero di raggi da lanciare (3x3 grid)
    [SerializeField] private bool useMultipleRaycasts = true; // Usa raggi multipli
    [SerializeField] private bool useScreenAreaDetection = true; // Usa rilevamento area schermo
    
    private AudioSource teleportConfirmAudioSource;
    private AudioSource teleportFailureAudioSource;
    private bool isTeleporting = false;

    public override int powerCost => 50;
    protected override bool HasFixedDuration => false;

    protected override void Awake()
    {
        base.Awake();

        teleportConfirmAudioSource = gameObject.AddComponent<AudioSource>();
        teleportConfirmAudioSource.playOnAwake = false;
        
        teleportFailureAudioSource = gameObject.AddComponent<AudioSource>();
        teleportFailureAudioSource.playOnAwake = false;
        
        if (teleportLayerMask == -1)
        {
            teleportLayerMask = LayerMask.GetMask("Teleport");
        }
    }

    private void Start()
    {
        if (teleportEffectController == null)
        {
            teleportEffectController = GetComponentInChildren<CFXR_EffectController>(true);
            if (teleportEffectController == null)
                Debug.LogError("CFXR_EffectController non trovato tra i figli del player!");
        }

        if (teleportEffectController != null)
        {
            teleportEffectController.StopEffect();
            teleportEffectController.gameObject.SetActive(false);
        }
    }

    protected override void Update()
    {
        base.Update();
        CheckScreenCenterForTeleportBases();

        if (Input.GetKeyDown(directTeleportKey))
        {
            TryActivate();
        }
    }

    public override bool CanActivate()
    {
        bool baseCanActivate = base.CanActivate();
        bool notTeleporting = !isTeleporting;
        bool hasValidTarget = TeleportBase.currentHoveredBase != null;
        
        return baseCanActivate && notTeleporting && hasValidTarget;
    }

    public override void Activate()
    {
        if (TeleportBase.currentHoveredBase == null)
        {
            Debug.LogWarning("Activate() chiamato senza una TeleportBase valida!");
            return;
        }

        Debug.Log($"ATTIVAZIONE - Teletrasporto su: {TeleportBase.currentHoveredBase.gameObject.name}");

        powerUpScript.SpendPower(powerCost);
        PlayTeleportConfirmSound();
        SetPlayerVisible(false);

        Vector3 targetPosition = TeleportBase.currentHoveredBase.GetTeleportPosition();
        StartCoroutine(ExecuteTeleportRoutine(targetPosition, TeleportBase.currentHoveredBase));
    }

    public override void Deactivate()
    {
        if (isTeleporting)
        {
            StopAllCoroutines();
            isTeleporting = false;
            IsActive = false;
            
            SetPlayerVisible(true);
            UnlockPlayerMovement();
            
            if (teleportEffectController != null)
            {
                teleportEffectController.StopEffect();
                teleportEffectController.gameObject.SetActive(false);
            }
            
            Debug.Log("Teletrasporto forzatamente interrotto");
        }
    }

    public override void TryActivate()
    {
        Debug.Log("Tentativo teletrasporto diretto...");

        if (TeleportBase.currentHoveredBase == null)
        {
            Debug.Log("FAILURE - Nessuna TeleportBase inquadrata dalla camera");
            PlayTeleportFailureSound();
            return;
        }

        base.TryActivate();
    }

    public new string GetDisableReason()
    {
        string baseReason = base.GetDisableReason();
        if (baseReason != "motivo sconosciuto") return baseReason;
        
        if (isTeleporting) return "teletrasporto in corso";
        if (TeleportBase.currentHoveredBase == null) return "nessun bersaglio inquadrato";
        
        return "motivo sconosciuto";
    }

    private void CheckScreenCenterForTeleportBases()
    {
        Camera cameraToUse = targetCamera != null ? targetCamera : Camera.main;
        if (cameraToUse == null) 
        {
            Debug.LogError("Nessuna camera disponibile!");
            return;
        }

        TeleportBase newHoveredBase = null;
        
        // METODO 1: Raycast multipli in griglia dal centro schermo
        if (useMultipleRaycasts && newHoveredBase == null)
        {
            newHoveredBase = CheckMultipleScreenRaycasts(cameraToUse);
        }
        
        // METODO 2: Raycast dalla camera con skip del player (metodo originale)
        if (newHoveredBase == null)
        {
            Vector3 rayOrigin = cameraToUse.transform.position + cameraToUse.transform.forward * playerSkipDistance;
            Vector3 rayDirection = cameraToUse.transform.forward;
            Ray cameraRay = new Ray(rayOrigin, rayDirection);
            
            Debug.DrawRay(rayOrigin, rayDirection * maxTeleportRange, Color.red, 0.1f);

            if (Physics.Raycast(cameraRay, out RaycastHit hit, maxTeleportRange, teleportLayerMask))
            {
                TeleportBase teleportBase = hit.collider.GetComponent<TeleportBase>();
                if (teleportBase != null)
                {
                    newHoveredBase = teleportBase;
                    Debug.Log($"METODO CAMERA SUCCESS: Raycast found '{teleportBase.name}' at {hit.distance:F1}m");
                }
            }
        }
        
        // METODO 3: OverlapSphere con angolo di rilevamento ampliato
        if (newHoveredBase == null)
        {
            Vector3 searchCenter = cameraToUse.transform.position + cameraToUse.transform.forward * (playerSkipDistance + 5f);
            Collider[] nearbyColliders = Physics.OverlapSphere(searchCenter, 12f, teleportLayerMask); // Aumentato raggio
            
            float closestScore = float.MaxValue; // Combina distanza e angolo
            TeleportBase bestBase = null;
            
            foreach (var col in nearbyColliders)
            {
                TeleportBase teleportBase = col.GetComponent<TeleportBase>();
                if (teleportBase != null)
                {
                    Vector3 directionToBase = (teleportBase.transform.position - cameraToUse.transform.position).normalized;
                    float angle = Vector3.Angle(cameraToUse.transform.forward, directionToBase);
                    
                    if (angle < maxDetectionAngle) // Usa il parametro configurabile
                    {
                        float distance = Vector3.Distance(cameraToUse.transform.position, teleportBase.transform.position);
                        // Score che favorisce angoli piccoli e distanze corte
                        float score = (angle / maxDetectionAngle) * 0.7f + (distance / maxTeleportRange) * 0.3f;
                        
                        if (score < closestScore)
                        {
                            closestScore = score;
                            bestBase = teleportBase;
                        }
                    }
                }
            }
            
            if (bestBase != null)
            {
                newHoveredBase = bestBase;
                Debug.Log($"METODO OVERLAP SUCCESS: Found '{bestBase.name}' with score {closestScore:F2}");
            }
        }
        
        // METODO 4: Rilevamento area schermo con proiezione
        if (useScreenAreaDetection && newHoveredBase == null)
        {
            newHoveredBase = CheckScreenAreaDetection(cameraToUse);
        }

        // Gestisci cambio target
        if (newHoveredBase != TeleportBase.currentHoveredBase)
        {
            if (TeleportBase.currentHoveredBase != null)
            {
                TeleportBase.currentHoveredBase.OnCursorExit();
            }

            if (newHoveredBase != null)
            {
                newHoveredBase.OnCursorEnter();
            }
        }
    }
    
    private TeleportBase CheckMultipleScreenRaycasts(Camera camera)
    {
        Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        TeleportBase bestBase = null;
        float closestDistance = float.MaxValue;
        
        // Crea una griglia 3x3 di punti intorno al centro
        int gridSize = Mathf.RoundToInt(Mathf.Sqrt(raycastSamples));
        float step = screenDetectionRadius / gridSize;
        
        for (int x = -gridSize/2; x <= gridSize/2; x++)
        {
            for (int y = -gridSize/2; y <= gridSize/2; y++)
            {
                Vector2 screenPoint = screenCenter + new Vector2(x * step, y * step);
                
                // Assicurati che il punto sia dentro i limiti dello schermo
                if (screenPoint.x < 0 || screenPoint.x > Screen.width || 
                    screenPoint.y < 0 || screenPoint.y > Screen.height)
                    continue;
                
                Ray ray = camera.ScreenPointToRay(screenPoint);
                ray.origin = ray.origin + ray.direction * playerSkipDistance;
                
                Debug.DrawRay(ray.origin, ray.direction * maxTeleportRange * 0.5f, Color.cyan, 0.1f);
                
                if (Physics.Raycast(ray, out RaycastHit hit, maxTeleportRange, teleportLayerMask))
                {
                    TeleportBase teleportBase = hit.collider.GetComponent<TeleportBase>();
                    if (teleportBase != null && hit.distance < closestDistance)
                    {
                        closestDistance = hit.distance;
                        bestBase = teleportBase;
                    }
                }
            }
        }
        
        if (bestBase != null)
        {
            Debug.Log($"METODO MULTI-RAYCAST SUCCESS: Found '{bestBase.name}' at {closestDistance:F1}m");
        }
        
        return bestBase;
    }
    
    private TeleportBase CheckScreenAreaDetection(Camera camera)
{
    // Trova tutti i TeleportBase nella scena
    TeleportBase[] allBases = FindObjectsByType<TeleportBase>(FindObjectsSortMode.None);
    TeleportBase bestBase = null;
    float bestScore = float.MaxValue;

    Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

    foreach (var teleportBase in allBases)
    {
        // Controlla se la base è visibile dalla camera
        Vector3 screenPos = camera.WorldToScreenPoint(teleportBase.transform.position);

        // Se è dietro la camera, salta
        if (screenPos.z <= 0) continue;

        // Calcola la distanza dal centro schermo in pixel
        Vector2 screenPos2D = new Vector2(screenPos.x, screenPos.y);
        float screenDistance = Vector2.Distance(screenCenter, screenPos2D);

        // Se è troppo lontano dal centro, salta
        if (screenDistance > screenDetectionRadius) continue;

        // Controlla se c'è line of sight
        Vector3 directionToBase = (teleportBase.transform.position - camera.transform.position).normalized;
        Ray losRay = new Ray(camera.transform.position + camera.transform.forward * playerSkipDistance, directionToBase);

        if (Physics.Raycast(losRay, out RaycastHit hit, maxTeleportRange))
        {
            if (hit.collider.GetComponent<TeleportBase>() == teleportBase)
            {
                // Score basato su distanza schermo e distanza 3D
                float worldDistance = Vector3.Distance(camera.transform.position, teleportBase.transform.position);
                float score = (screenDistance / screenDetectionRadius) * 0.6f + (worldDistance / maxTeleportRange) * 0.4f;

                if (score < bestScore)
                {
                    bestScore = score;
                    bestBase = teleportBase;
                }
            }
        }
    }

    if (bestBase != null)
    {
        Debug.Log($"METODO SCREEN-AREA SUCCESS: Found '{bestBase.name}' with score {bestScore:F2}");
    }

    return bestBase;
}


    private IEnumerator ExecuteTeleportRoutine(Vector3 targetPosition, TeleportBase targetBase)
    {
        isTeleporting = true;
        Debug.Log("Inizio routine teletrasporto...");

        LockPlayerMovement();

        if (teleportEffectController != null)
        {
            teleportEffectController.gameObject.SetActive(true);
            teleportEffectController.PlayEffect();
            Debug.Log("Effetti teletrasporto attivati");
        }

        yield return new WaitForSeconds(0.2f);

        PerformPhysicalTeleport(targetPosition);

        SetPlayerVisible(true);
        Debug.Log("Player mostrato");

        yield return new WaitForSeconds(0.3f);

        if (teleportEffectController != null)
        {
            teleportEffectController.StopEffect();
            teleportEffectController.gameObject.SetActive(false);
            Debug.Log("Effetti teletrasporto fermati");
        }

        UnlockPlayerMovement();
        targetBase.OnPlayerTeleported();

        IsActive = false;
        isTeleporting = false;

        Debug.Log("Teletrasporto completato!");
    }

    private void PerformPhysicalTeleport(Vector3 targetPosition)
    {
        if (controllerGameObject && controllerGameObject.TryGetComponent(out CharacterController cc))
        {
            Vector3 finalTarget = targetPosition;
            finalTarget.y += cc.height * 0.5f;

            cc.enabled = false;
            controllerGameObject.transform.position = finalTarget;
            cc.enabled = true;

            Debug.Log($"Player teletrasportato a: {finalTarget}");
        }
        else
        {
            Debug.LogWarning("CharacterController non trovato!");
        }
    }

    private void LockPlayerMovement()
    {
        var controller = controllerGameObject.GetComponent<ThirdPersonController>();
        if (controller != null) 
        {
            controller.IsMovementLocked = true;
            Debug.Log("Movimento player bloccato");
        }
    }

    private void UnlockPlayerMovement()
    {
        var controller = controllerGameObject.GetComponent<ThirdPersonController>();
        if (controller != null) 
        {
            controller.IsMovementLocked = false;
            Debug.Log("Movimento player sbloccato");
        }
    }

    private void SetPlayerVisible(bool visible)
    {
        foreach (var mesh in meshesToHide)
        {
            if (mesh != null)
            {
                mesh.enabled = visible;
            }
        }
    }

    private void PlayTeleportConfirmSound()
    {
        if (teleportConfirmSound != null && teleportConfirmAudioSource != null)
        {
            teleportConfirmAudioSource.PlayOneShot(teleportConfirmSound);
            Debug.Log("Audio conferma teletrasporto riprodotto");
        }
    }

    private void PlayTeleportFailureSound()
    {
        if (teleportFailureSound != null && teleportFailureAudioSource != null)
        {
            teleportFailureAudioSource.PlayOneShot(teleportFailureSound);
            Debug.Log("Audio fallimento teletrasporto riprodotto");
        }
    }

    public bool ForceTeleportToPosition(Vector3 targetPosition)
    {
        if (!IsEnabled || isTeleporting)
        {
            Debug.Log($"Impossibile forzare il teletrasporto: {GetDisableReason()}");
            return false;
        }

        Debug.Log($"Teletrasporto forzato alla posizione: {targetPosition}");
        
        IsActive = true;
        PlayTeleportConfirmSound();
        SetPlayerVisible(false);
        
        GameObject tempBase = new GameObject("TempTeleportBase");
        tempBase.transform.position = targetPosition;
        TeleportBase tempTeleportBase = tempBase.AddComponent<TeleportBase>();
        
        StartCoroutine(ExecuteTeleportRoutine(targetPosition, tempTeleportBase));
        StartCoroutine(DestroyTempBase(tempBase));
        
        return true;
    }

    private IEnumerator DestroyTempBase(GameObject tempBase)
    {
        yield return null;
        if (tempBase != null)
            DestroyImmediate(tempBase);
    }
}