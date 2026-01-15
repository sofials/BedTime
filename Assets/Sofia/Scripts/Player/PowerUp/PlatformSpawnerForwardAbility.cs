using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class PlatformSpawnerForwardAbility : AbilityBase
{
    [Header("Platform Spawner")]
    public GameObject platformPrefab;
    public GameObject ghostPrefab;
    public Material ghostValidMat;     // Materiale ghost normale
    public Material ghostInvalidMat;   // Materiale ghost rosso
    public float forwardDistance  = 50f;
    public float verticalOffset   = 25f;
    public float checkRadius      = 0.4f;
    public LayerMask obstacleMask;
    

    [Header("Anti-Flickering")]
    [Tooltip("Raggio ridotto per tornare valido (evita flickering) - deve essere MOLTO più piccolo")]
    public float checkRadiusValid = 0.2f;
    [Tooltip("Ritardo temporale prima di cambiare stato (debounce)")]
    public float stateChangeDelay = 0.08f;
    
    [Header("Vertical Placement")]
    [Tooltip("Quanto si alza/abbassa la piattaforma guardando su/giù")]
    public float verticalSensitivity = 80f;
    [Tooltip("Altezza massima sopra il giocatore")]
    public float maxHeightOffset = 500f;
    [Tooltip("Altezza minima sotto il giocatore")]
    public float minHeightOffset = -50f;

    public override int powerCost => 75;

    [Header("References")]
    public Transform footTarget;
    [SerializeField] private Transform cameraTransform;

    private GameObject currentGhost;
    private GameObject currentPlatform;
    private bool       placing = false;
    private bool       wasValidLastFrame = true;    // Per evitare flickering nel cambio colore

    private Vector3    lastForwardDirection;
    private Vector3    currentGhostVelocity;
    private Quaternion targetRotation;
    private float      fixedBaseHeight; // ✅ Altezza fissa salvata all'attivazione
    private const float rotationSmoothSpeed   = 15f;
    private const float updateAngleThreshold  = 10f;

    private PlayerControls controls;
    private bool cancelPressed;
    private bool confirmPressed;
    private bool ignoreNextConfirm; // Ignora il primo F dell'attivazione


    private Renderer ghostRenderer; // Renderer per cambiare materiale
    private float stateTimer = 0f;  // Timer per debounce

    private float activationClipLength = 0.5f;

    protected override void Awake()
    {
        base.Awake();
        controls = new PlayerControls();
        controls.Gameplay.Confirm.performed += _ => cancelPressed = true; // Click destro = annulla
        controls.Gameplay.Create.performed += _ =>
        {
            if (!ignoreNextConfirm)
                confirmPressed = true;
            else
                ignoreNextConfirm = false; // Resetta dopo averlo ignorato
        };
        controls.Enable();

        effectIconIndex = 3;
        
        if (cameraTransform == null)
        {
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                cameraTransform = mainCamera.transform;
            }
            else
            {
                Camera anyCamera = Object.FindFirstObjectByType<Camera>();
                if (anyCamera != null)
                {
                    cameraTransform = anyCamera.transform;
                    Debug.LogWarning($"MainCamera not found, using {anyCamera.name} instead.");
                }
            }
        }
    }

    private void Start()
    {
        if (cameraTransform == null)
        {
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                cameraTransform = mainCamera.transform;
            }
            else
            {
                Debug.LogError("No camera found! Please assW  a camera transform in the inspector or ensure there's a MainCamera in the scene.");
                enabled = false;
                return;
            }
        }
    }
/// <summary>
/// Cancella il processo di piazzamento in corso (chiamato al respawn/morte)
/// </summary>
public void CancelPlacement()
{
    if (placing || currentGhost != null)
    {
        Debug.Log("[PlatformSpawnerForwardAbility] ❌ Piazzamento annullato (morte/respawn)");

        if (currentGhost != null)
        {
            Destroy(currentGhost);
            currentGhost = null;
        }

        placing = false;
        IsActive = false;
        cancelPressed = false;
    }
}
    protected override void Update()
    {
        base.Update();

       if (!placing || currentGhost == null || cameraTransform == null) return;

       if (!IsEnabled)
    {
        Deactivate();
        return;
    }

        // Gestione annullamento con click destro
        if (cancelPressed)
        {
            cancelPressed = false;
            Debug.Log("[PlatformSpawnerForwardAbility] Piazzamento annullato dall'utente (click destro)");
            Deactivate();
            return;
        }

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

        // ⭐ Gestione stato visivo del ghost (valido/invalido)
        bool isValidPosition = IsPositionValidForVisual(targetPos);

        // Debounce: cambia stato solo dopo un ritardo per evitare oscillazioni rapide
        if (isValidPosition != wasValidLastFrame)
        {
            stateTimer += Time.deltaTime;
            if (stateTimer >= stateChangeDelay)
            {
                Debug.Log($"[PlatformSpawner] Stato cambiato - Valid: {wasValidLastFrame}->{isValidPosition}");
                UpdateGhostVisualState(isValidPosition);
                wasValidLastFrame = isValidPosition;
                stateTimer = 0f;
            }
        }
        else
        {
            stateTimer = 0f; // Reset timer se lo stato rimane uguale
        }

        // Conferma piazzamento con F (secondo press)
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

        Destroy(currentGhost);
        currentGhost = null;

        currentPlatform = Instantiate(platformPrefab, targetPos, targetRotation);

        // Scala i soldi solo qui, quando confermiamo la piattaforma
        powerUpScript.SpendPower(powerCost);
        Deactivate();
    }

    public override void TryActivate()
    {
        Debug.Log("\n=== [PlatformSpawnerForwardAbility] TryActivate() DEBUG START ===");
        Debug.Log($"IsEnabled: {IsEnabled}");
        Debug.Log($"GetDisableReason(): {GetDisableReason()}");
        Debug.Log($"IsActive: {IsActive}");
        Debug.Log($"powerUpScript null: {powerUpScript == null}");
        if (powerUpScript != null)
            Debug.Log($"HasEnoughPower({powerCost}): {powerUpScript.HasEnoughPower(powerCost)}");

        if (!IsEnabled)
        {
            string reason = GetDisableReason();
            Debug.LogWarning($"[PlatformSpawnerForwardAbility] Abilità disabilitata: {reason}");

            if (reason.Contains("non permessa in questo livello"))
            {
                Debug.Log("[PlatformSpawnerForwardAbility] Abilità non permessa nel livello - nessun feedback, nessuna UI");
                Debug.Log("=== [PlatformSpawnerForwardAbility] TryActivate() DEBUG END (silent exit) ===\n");
                return;
            }
            Debug.Log("[PlatformSpawnerForwardAbility] Altri tipi di disabilitazione - riproduce failure sound");

            if (failureSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(failureSound);
                Debug.Log("[PlatformSpawnerForwardAbility] Audio di fallimento per abilità disabilitata");
            }

            if (PlayerUI.Instance != null)
            {
                PlayerUI.Instance.PulseIconAt(effectIconIndex);
            }

            return;
        }

        if (IsActive)
        {
            Debug.Log("[PlatformSpawnerForwardAbility] Già attiva - disattivazione");
            Deactivate();
            Debug.Log("=== [PlatformSpawnerForwardAbility] TryActivate() DEBUG END (deactivated) ===\n");
            return;
        }

        // NON controllo energia qui - i soldi vengono scalati solo alla conferma finale (secondo F)
        // Controllo solo che la camera sia disponibile
        if (cameraTransform == null)
        {
            Debug.LogWarning("[PlatformSpawnerForwardAbility] Camera non disponibile");

            if (failureSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(failureSound);
            }

            if (PlayerUI.Instance != null)
            {
                PlayerUI.Instance.PulseIconAt(effectIconIndex);
            }

            Debug.Log("=== [PlatformSpawnerForwardAbility] TryActivate() DEBUG END (no camera) ===\n");
            return;
        }

        Debug.Log("[PlatformSpawnerForwardAbility] Attivazione abilità - distruzione piattaforma precedente se esiste");

        // Distruggi la piattaforma precedente se esiste (primo F)
        DestroyCurrentPlatform();

        Activate();

        if (activationSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(activationSound);
            Debug.Log("[PlatformSpawnerForwardAbility] Audio di attivazione riprodotto");
        }

        if (PlayerUI.Instance != null)
        {
            PlayerUI.Instance.PulseIconAt(effectIconIndex);
        }

        Debug.Log("=== [PlatformSpawnerForwardAbility] TryActivate() DEBUG END (activated) ===\n");
    }

    public override void Activate()
    {
        if (placing || currentGhost || cameraTransform == null) return;

        placing = true;
        IsActive = true;
        ignoreNextConfirm = true; // ⭐ Ignora il primo F che ha attivato il ghost

        // ✅ Salva l'altezza base al momento dell'attivazione (non cambia se il player salta)
        Vector3 basePos = footTarget ? footTarget.position : transform.position;
        fixedBaseHeight = basePos.y + verticalOffset;

        lastForwardDirection = GetCameraForwardFlat();
        Vector3 spawnPos     = GetSpawnPosition(lastForwardDirection);

        currentGhost = Instantiate(ghostPrefab, spawnPos, Quaternion.identity);
        targetRotation = Quaternion.LookRotation(lastForwardDirection);
        currentGhost.transform.rotation = targetRotation;

        // Ottieni il renderer e imposta materiale valido inizialmente
        ghostRenderer = currentGhost.GetComponentInChildren<Renderer>();
        if (ghostRenderer != null && ghostValidMat != null)
        {
            ghostRenderer.material = ghostValidMat;
        }

        DrawRevealEffect drawEffect = currentGhost.GetComponent<DrawRevealEffect>();
        if (drawEffect != null)
        {
            drawEffect.ResetDraw();
            StartCoroutine(StartDrawAudioAfterDelay(drawEffect));
        }
    }


    private IEnumerator StartDrawAudioAfterDelay(DrawRevealEffect drawEffect)
    {
        yield return new WaitForSeconds(activationClipLength);
        drawEffect.PlayLoopAudio();
    }

    public override void Deactivate()
    {
        if (currentGhost) Destroy(currentGhost);

        placing = false;
        IsActive = false;
    }

    private void DestroyCurrentPlatform()
    {
        if (currentPlatform != null)
        {
            Debug.Log("[PlatformSpawnerForwardAbility] Distruggendo piattaforma precedente");
            Destroy(currentPlatform);
            currentPlatform = null;
        }
    }

    private Vector3 GetCameraForwardFlat()
    {
        if (cameraTransform == null) return transform.forward;
        
        Vector3 f = cameraTransform.forward;
        f.y = 0f;
        return f.normalized;
    }

    /// <summary>
    /// Calcola l'offset verticale basato sull'angolo di pitch della camera
    /// </summary>
    private float GetVerticalOffsetFromCamera()
    {
        if (cameraTransform == null) return 0f;
        
        // Usa l'angolo di rotazione X della camera (pitch)
        // Quando guardi in alto, l'angolo è negativo (es. -30)
        // Quando guardi in basso, l'angolo è positivo (es. +30)
        float pitch = cameraTransform.eulerAngles.x;
        
        // Converti da 0-360 a -180/+180
        if (pitch > 180f)
            pitch -= 360f;
        
        
        // Inverti: pitch positivo (guardi giù) = offset negativo (piattaforma scende)
        //          pitch negativo (guardi su) = offset positivo (piattaforma sale)
        // Dividi per un valore più piccolo per maggiore sensibilità
        float normalizedPitch = -pitch / 45f; // Era 90, ora 45 per più sensibilità
        
        // Moltiplica per la sensibilità
        float heightOffset = normalizedPitch * verticalSensitivity;
        
        // Clamp tra min e max
        heightOffset = Mathf.Clamp(heightOffset, minHeightOffset, maxHeightOffset);
        
        return heightOffset;
    }

    private Vector3 GetSpawnPosition(Vector3 dir)
    {
        Vector3 basePos = footTarget ? footTarget.position : transform.position;
        
        // Posizione orizzontale segue il player
        Vector3 spawnPos = basePos + dir * forwardDistance;
        
        // ✅ L'altezza usa la base fissa (salvata all'attivazione) + offset dalla camera
        // NON segue la Y del player quando salta
        spawnPos.y = fixedBaseHeight + GetVerticalOffsetFromCamera();
        
        return spawnPos;
    }

    private bool CanPlacePlatform(Vector3 pos)
{
    bool blocked = Physics.CheckSphere(pos, checkRadius, obstacleMask);

    if (blocked)
    {
        // Trova cosa sta bloccando
        Collider[] hits = Physics.OverlapSphere(pos, checkRadius, obstacleMask);
        foreach (var hit in hits)
        {
            Debug.LogWarning($"[Platform BLOCKED] Oggetto: {hit.gameObject.name} | Layer: {LayerMask.LayerToName(hit.gameObject.layer)} | Pos: {hit.transform.position}");
        }
    }

    return !blocked;
}

    /// <summary>
    /// Controlla se la posizione è valida per il visual feedback (con isteresi CORRETTA per evitare flickering)
    /// </summary>
    private bool IsPositionValidForVisual(Vector3 pos)
    {
        // ✅ ISTERESI CORRETTA:
        // Se ero VALIDO → uso raggio PICCOLO per diventare invalido (difficile perdere validità)
        // Se ero INVALIDO → uso raggio GRANDE per tornare valido (facile riacquistare validità)
        float radiusToUse = wasValidLastFrame ? checkRadiusValid : checkRadius;

        bool blocked = Physics.CheckSphere(pos, radiusToUse, obstacleMask);
        return !blocked;
    }

    /// <summary>
    /// Aggiorna lo stato visivo del ghost in base a validità
    /// </summary>
    private void UpdateGhostVisualState(bool isValid)
    {
        if (ghostRenderer == null) return;

        Material targetMat = isValid ? ghostValidMat : ghostInvalidMat;

        if (targetMat != null)
        {
            ghostRenderer.material = targetMat;
        }
    }

    public override bool CanActivate()
    {
        bool baseCanActivate = base.CanActivate();
        bool cameraAvailable = cameraTransform != null;
        
        return baseCanActivate && cameraAvailable;
    }

    private void OnDestroy()
    {
        DestroyCurrentPlatform();
        
        if (controls != null)
        {
            controls.Disable();
            controls.Dispose();
        }
    }
    private void OnDrawGizmos()
{
    if (!placing || currentGhost == null) return;
    
    Vector3 checkPos = currentGhost.transform.position;
    
    // Verde = libero, Rosso = bloccato
    bool blocked = Physics.CheckSphere(checkPos, checkRadius, obstacleMask);
    Gizmos.color = blocked ? Color.red : Color.green;
    Gizmos.DrawWireSphere(checkPos, checkRadius);
}
}