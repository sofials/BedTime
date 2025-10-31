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
    [SerializeField] private float maxDetectionAngle = 45f;
    [SerializeField] private float screenDetectionRadius = 200f;
    [SerializeField] private int raycastSamples = 9;
    [SerializeField] private bool useMultipleRaycasts = true;
    [SerializeField] private bool useScreenAreaDetection = true;
    
    [SerializeField] private bool usePredictiveDetection = true; // NUOVO
[SerializeField] private float predictionTime = 0.1f; // NUOVO - tempo di predizione in secondi
    [SerializeField] private int detectionFrameBuffer = 3; // NUOVO - mantieni detection per N frame
private TeleportBase lastDetectedBase;
private int detectionFrameCount = 0;
private Vector3 lastCameraPosition;
private Vector3 lastCameraForward;

    private AudioSource teleportConfirmAudioSource;
    private AudioSource teleportFailureAudioSource;
    private bool isTeleporting = false;

    public override int powerCost => 50;
    protected override bool HasFixedDuration => false;
    [Header("Distance Settings")]
[SerializeField] private float minTeleportDistance = 3f; // Distanza minima per il teletrasporto

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
         if (lastCameraPosition == Vector3.zero && targetCamera != null)
    {
        lastCameraPosition = targetCamera.transform.position;
        lastCameraForward = targetCamera.transform.forward;
    }
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
    
    // Nuovo controllo: verifica che non siamo già sopra la base target
    bool notOnTargetBase = true;
    if (TeleportBase.currentHoveredBase != null && controllerGameObject != null)
    {
        float distanceToTarget = Vector3.Distance(
            controllerGameObject.transform.position, 
            TeleportBase.currentHoveredBase.transform.position
        );
        notOnTargetBase = distanceToTarget > minTeleportDistance;
    }
    
    return baseCanActivate && notTeleporting && hasValidTarget && notOnTargetBase;
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

    // Prima verifica: l'abilità deve essere abilitata
    if (!IsEnabled)
    {
        Debug.Log("FAILURE - Abilità non abilitata");
        return;
    }

    // Seconda verifica: deve esserci una TeleportBase inquadrata
    if (TeleportBase.currentHoveredBase == null)
    {
        Debug.Log("FAILURE - Nessuna TeleportBase inquadrata dalla camera");
        return;
    }

    // Terza verifica: controlla se può essere attivata (energia, cooldown, ecc.)
    if (!CanActivate())
    {
        Debug.Log($"FAILURE - {GetDisableReason()}");
        PlayTeleportFailureSound(); // Solo qui riproduci il suono di fallimento
        return;
    }

    // Se arriviamo qui, tutto è OK - attiva l'abilità
    base.TryActivate();
}

  public new string GetDisableReason()
{
    string baseReason = base.GetDisableReason();
    if (baseReason != "motivo sconosciuto") return baseReason;
    
    if (isTeleporting) return "teletrasporto in corso";
    if (TeleportBase.currentHoveredBase == null) return "nessun bersaglio inquadrato";
    
    // Nuovo controllo distanza
    if (TeleportBase.currentHoveredBase != null && controllerGameObject != null)
    {
        float distanceToTarget = Vector3.Distance(
            controllerGameObject.transform.position, 
            TeleportBase.currentHoveredBase.transform.position
        );
        if (distanceToTarget <= minTeleportDistance)
            return "già sopra la base di teletrasporto";
    }
    
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

        // 1. Prova detection normale
        if (useMultipleRaycasts && newHoveredBase == null)
        {
            newHoveredBase = CheckMultipleScreenRaycasts(cameraToUse);
        }

        if (newHoveredBase == null)
        {
            newHoveredBase = CheckSingleRaycast(cameraToUse);
        }

        if (newHoveredBase == null)
        {
            newHoveredBase = CheckOverlapSphere(cameraToUse);
        }

        if (useScreenAreaDetection && newHoveredBase == null)
        {
            newHoveredBase = CheckScreenAreaDetection(cameraToUse);
        }

        // 2. Se non trovato nulla, prova detection predittiva
        if (usePredictiveDetection && newHoveredBase == null)
        {
            newHoveredBase = CheckPredictiveDetection(cameraToUse);
        }

        // 3. Sistema di buffer per detection stabile
        if (newHoveredBase != null)
        {
            lastDetectedBase = newHoveredBase;
            detectionFrameCount = detectionFrameBuffer;
        }
        else if (detectionFrameCount > 0)
        {
            // Mantieni l'ultima base rilevata per alcuni frame
            newHoveredBase = lastDetectedBase;
            detectionFrameCount--;
        }

        // 4. Aggiorna le hover states
        UpdateHoverStates(newHoveredBase);

        // 5. Salva posizione per il frame successivo
        lastCameraPosition = cameraToUse.transform.position;
        lastCameraForward = cameraToUse.transform.forward;
    }
    private TeleportBase CheckOverlapSphere(Camera camera)
    {
        Vector3 searchCenter = camera.transform.position + camera.transform.forward * (playerSkipDistance + 5f);
        Collider[] nearbyColliders = Physics.OverlapSphere(searchCenter, 12f, teleportLayerMask);

        float closestScore = float.MaxValue;
        TeleportBase bestBase = null;

        foreach (var col in nearbyColliders)
        {
            TeleportBase teleportBase = col.GetComponent<TeleportBase>();
            if (teleportBase != null)
            {
                Vector3 directionToBase = (teleportBase.transform.position - camera.transform.position).normalized;
                float angle = Vector3.Angle(camera.transform.forward, directionToBase);

                if (angle < maxDetectionAngle)
                {
                    float distance = Vector3.Distance(camera.transform.position, teleportBase.transform.position);
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
            Debug.Log($"METODO OVERLAP SUCCESS: Found '{bestBase.name}' with score {closestScore:F2}");
        }

        return bestBase;
    }
private void UpdateHoverStates(TeleportBase newHoveredBase)
{
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

private TeleportBase CheckSingleRaycast(Camera camera)
    {
        Vector3 rayOrigin = camera.transform.position + camera.transform.forward * playerSkipDistance;
        Vector3 rayDirection = camera.transform.forward;
        Ray cameraRay = new Ray(rayOrigin, rayDirection);

        Debug.DrawRay(rayOrigin, rayDirection * maxTeleportRange, Color.red, 0.1f);

        if (Physics.Raycast(cameraRay, out RaycastHit hit, maxTeleportRange, teleportLayerMask))
        {
            TeleportBase teleportBase = hit.collider.GetComponent<TeleportBase>();
            if (teleportBase != null)
            {
                Debug.Log($"METODO CAMERA SUCCESS: Raycast found '{teleportBase.name}' at {hit.distance:F1}m");
                return teleportBase;
            }
        }

        return null;
    }
    private TeleportBase CheckPredictiveDetection(Camera camera)
    {
        // Calcola la velocità della camera
        Vector3 cameraVelocity = Vector3.zero;
        if (lastCameraPosition != Vector3.zero)
        {
            cameraVelocity = (camera.transform.position - lastCameraPosition) / Time.deltaTime;
        }

        // Se la camera si muove lentamente, salta la predizione
        if (cameraVelocity.magnitude < 1f)
            return null;

        // Predici dove sarà la camera nel prossimo frame
        Vector3 predictedPosition = camera.transform.position + cameraVelocity * predictionTime;
        Vector3 predictedForward = camera.transform.forward; // Assumiamo che la direzione non cambi drasticamente

        // Esegui raycast dalla posizione predetta
        Vector3 rayOrigin = predictedPosition + predictedForward * playerSkipDistance;
        Ray predictiveRay = new Ray(rayOrigin, predictedForward);

        Debug.DrawRay(rayOrigin, predictedForward * maxTeleportRange, Color.yellow, 0.1f);

        if (Physics.Raycast(predictiveRay, out RaycastHit hit, maxTeleportRange, teleportLayerMask))
        {
            TeleportBase teleportBase = hit.collider.GetComponent<TeleportBase>();
            if (teleportBase != null)
            {
                Debug.Log($"METODO PREDITTIVO SUCCESS: Found '{teleportBase.name}' at predicted position");
                return teleportBase;
            }
        }

        // Prova anche con multiple raycasts predittivi
        return CheckMultiplePredictiveRaycasts(predictedPosition, predictedForward);
    }
private TeleportBase CheckMultiplePredictiveRaycasts(Vector3 predictedPosition, Vector3 predictedForward)
{
    TeleportBase bestBase = null;
    float closestDistance = float.MaxValue;
    
    // Crea una griglia di raycasts attorno alla posizione predetta
    int samples = 5; // Meno samples per performance
    float spreadAngle = 10f; // Angolo di spread in gradi
    
    for (int i = 0; i < samples; i++)
    {
        float angle = (i - samples/2) * (spreadAngle / samples) * Mathf.Deg2Rad;
        Vector3 direction = Quaternion.AngleAxis(angle * Mathf.Rad2Deg, Vector3.up) * predictedForward;
        
        Vector3 rayOrigin = predictedPosition + direction * playerSkipDistance;
        Ray ray = new Ray(rayOrigin, direction);
        
        Debug.DrawRay(rayOrigin, direction * maxTeleportRange * 0.3f, Color.magenta, 0.1f);
        
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
    
    if (bestBase != null)
    {
        Debug.Log($"METODO MULTI-PREDITTIVO SUCCESS: Found '{bestBase.name}' at {closestDistance:F1}m");
    }
    
    return bestBase;
}
    private TeleportBase CheckMultipleScreenRaycasts(Camera camera)
    {
        Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        TeleportBase bestBase = null;
        float closestDistance = float.MaxValue;
        
        int gridSize = Mathf.RoundToInt(Mathf.Sqrt(raycastSamples));
        float step = screenDetectionRadius / gridSize;
        
        for (int x = -gridSize/2; x <= gridSize/2; x++)
        {
            for (int y = -gridSize/2; y <= gridSize/2; y++)
            {
                Vector2 screenPoint = screenCenter + new Vector2(x * step, y * step);
                
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
        TeleportBase[] allBases = FindObjectsByType<TeleportBase>(FindObjectsSortMode.None);
        TeleportBase bestBase = null;
        float bestScore = float.MaxValue;

        Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

        foreach (var teleportBase in allBases)
        {
            Vector3 screenPos = camera.WorldToScreenPoint(teleportBase.transform.position);

            if (screenPos.z <= 0) continue;

            Vector2 screenPos2D = new Vector2(screenPos.x, screenPos.y);
            float screenDistance = Vector2.Distance(screenCenter, screenPos2D);

            if (screenDistance > screenDetectionRadius) continue;

            Vector3 directionToBase = (teleportBase.transform.position - camera.transform.position).normalized;
            Ray losRay = new Ray(camera.transform.position + camera.transform.forward * playerSkipDistance, directionToBase);

            if (Physics.Raycast(losRay, out RaycastHit hit, maxTeleportRange))
            {
                if (hit.collider.GetComponent<TeleportBase>() == teleportBase)
                {
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