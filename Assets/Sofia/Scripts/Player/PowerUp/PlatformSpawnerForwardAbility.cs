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

    [Header("Audio - Confirmation")]
    [Tooltip("Suono riprodotto quando la piattaforma viene piazzata con successo (secondo F)")]
    public AudioClip confirmationSound;

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

    // ⭐ NUOVO: Flag per prevenire input multipli durante lo stesso frame
    private bool isProcessingInput = false;

    // ⭐ NUOVO: Cooldown dopo CancelPlacement per prevenire ri-attivazione durante respawn
    private float lastCancelTime = 0f;
    private const float CANCEL_COOLDOWN = 0.5f; // Aumentato per sicurezza durante respawn

    protected override void Awake()
    {
        base.Awake();
        controls = new PlayerControls();
        controls.Gameplay.Confirm.performed += _ => cancelPressed = true; // Click destro = annulla
        controls.Gameplay.Create.performed += _ =>
        {
            // ⭐ Previeni input multipli
            if (isProcessingInput) return;
            
            // ⭐ FIX: Resetta ignoreNextConfirm SOLO se siamo in placing mode
            // Altrimenti i press di F quando non abbiamo energia "consumano" il flag
            if (!ignoreNextConfirm)
            {
                confirmPressed = true;
            }
            else if (placing) // Solo se siamo effettivamente in placing mode
            {
                ignoreNextConfirm = false;
            }
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
                Debug.LogError("No camera found! Please assign a camera transform in the inspector or ensure there's a MainCamera in the scene.");
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
        Debug.Log("[PlatformSpawnerForwardAbility] ❌ CancelPlacement chiamato - Reset completo");

        // ⭐ NUOVO: Imposta cooldown per prevenire ri-attivazione durante respawn
        lastCancelTime = Time.time;

        // Ferma eventuali coroutine audio in corso PRIMA di tutto
        StopAllCoroutines();

        // ⭐ Distruggi il ghost IMMEDIATAMENTE se esiste
        if (currentGhost != null)
        {
            Debug.Log($"[PlatformSpawnerForwardAbility] 💀 Distruggendo ghost: {currentGhost.name}");

            // ⭐ STEP 1: Nascondi SUBITO il ghost (effetto visivo immediato)
            currentGhost.SetActive(false);

            // ⭐ STEP 2: Ferma l'audio
            DrawRevealEffect drawEffect = currentGhost.GetComponent<DrawRevealEffect>();
            if (drawEffect != null)
            {
                AudioSource ghostAudio = currentGhost.GetComponent<AudioSource>();
                if (ghostAudio != null && ghostAudio.isPlaying)
                {
                    ghostAudio.Stop();
                }
            }

            // ⭐ STEP 3: Distruggi (Destroy è più sicuro di DestroyImmediate a runtime)
            Destroy(currentGhost);
            currentGhost = null;

            Debug.Log("[PlatformSpawnerForwardAbility] ✅ Ghost distrutto con successo");
        }
        else
        {
            Debug.Log("[PlatformSpawnerForwardAbility] ⚠️ CancelPlacement chiamato ma currentGhost era già null!");
        }

        // Reset COMPLETO di tutti gli stati
        placing = false;
        IsActive = false;
        cancelPressed = false;
        confirmPressed = false;
        ignoreNextConfirm = false;
        wasValidLastFrame = true;
        stateTimer = 0f;
        ghostRenderer = null;
        isProcessingInput = false;
        
        Debug.Log("[PlatformSpawnerForwardAbility] ✅ Reset completo eseguito");
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

        // ⭐ Previeni elaborazione multipla
        if (isProcessingInput) return;
        isProcessingInput = true;

        if (!powerUpScript.HasEnoughPower(powerCost))
        {
            Debug.Log("Energia insufficiente!");
            PlayFailureSound();
            isProcessingInput = false;
            return;
        }

        if (!CanPlacePlatform(targetPos))
        {
            Debug.Log("Spazio occupato!");
            PlayFailureSound();
            isProcessingInput = false;
            return;
        }

        // ⭐ PIAZZAMENTO CONFERMATO - Distruggi ghost e crea piattaforma
        Destroy(currentGhost);
        currentGhost = null;

        currentPlatform = Instantiate(platformPrefab, targetPos, targetRotation);

        // Nascondi il tutorial la prima volta che viene piazzata una piattaforma
        if (PlatformTutorial.Instance != null)
        {
            PlatformTutorial.Instance.Hide();
        }

        // Scala i soldi solo qui, quando confermiamo la piattaforma
        powerUpScript.SpendPower(powerCost);

        // ⭐ NUOVO: Riproduci suono di conferma (secondo F)
        PlayConfirmationSound();

        Deactivate();
        
        // Reset del flag dopo un breve delay per sicurezza
        StartCoroutine(ResetInputProcessingFlag());
    }

    /// <summary>
    /// Riproduce il suono di conferma quando la piattaforma viene piazzata
    /// </summary>
    private void PlayConfirmationSound()
    {
        if (confirmationSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(confirmationSound);
            Debug.Log("[PlatformSpawnerForwardAbility] 🔊 Suono conferma piazzamento riprodotto");
        }
        else if (confirmationSound == null)
        {
            Debug.LogWarning("[PlatformSpawnerForwardAbility] ⚠️ confirmationSound non assegnato!");
        }
    }

    /// <summary>
    /// Riproduce il suono di fallimento (posizione invalida, energia insufficiente, ecc.)
    /// </summary>
    private void PlayFailureSound()
    {
        if (failureSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(failureSound);
            Debug.Log("[PlatformSpawnerForwardAbility] 🔊 Suono fallimento riprodotto");
        }
    }

    /// <summary>
    /// Reset del flag di elaborazione input dopo un breve delay
    /// </summary>
    private IEnumerator ResetInputProcessingFlag()
    {
        yield return new WaitForSeconds(0.1f);
        isProcessingInput = false;
    }

    public override void TryActivate()
    {
        Debug.Log("\n=== [PlatformSpawnerForwardAbility] TryActivate() DEBUG START ===");

        // ⭐ NUOVO: Previeni attivazione subito dopo CancelPlacement (durante respawn)
        if (Time.time - lastCancelTime < CANCEL_COOLDOWN)
        {
            Debug.Log($"[PlatformSpawnerForwardAbility] ⏳ Cooldown attivo dopo cancel - BLOCCO ATTIVAZIONE");
            return;
        }

        // ⭐ Previeni attivazione se stiamo già elaborando input
        if (isProcessingInput)
        {
            Debug.Log("[PlatformSpawnerForwardAbility] Input già in elaborazione - ignoro");
            return;
        }

        if (!IsEnabled)
        {
            string reason = GetDisableReason();
            Debug.LogWarning($"[PlatformSpawnerForwardAbility] Abilità disabilitata: {reason}");

            if (reason.Contains("non permessa in questo livello"))
            {
                Debug.Log("[PlatformSpawnerForwardAbility] Abilità non permessa nel livello - nessun feedback");
                return;
            }

            if (failureSound != null && audioSource != null)
                audioSource.PlayOneShot(failureSound);

            if (PlayerUI.Instance != null)
                PlayerUI.Instance.PulseIconAt(effectIconIndex);

            return;
        }

        if (IsActive)
        {
            Debug.Log("[PlatformSpawnerForwardAbility] Già attiva - disattivazione");
            Deactivate();
            return;
        }

        // ⭐ Controlla energia PRIMA di mostrare il ghost
        if (powerUpScript == null || !powerUpScript.HasEnoughPower(powerCost))
        {
            Debug.Log($"[PlatformSpawnerForwardAbility] Energia insufficiente ({powerUpScript?.CurrentPower ?? 0}/{powerCost})");
            
            if (failureSound != null && audioSource != null)
                audioSource.PlayOneShot(failureSound);

            if (PlayerUI.Instance != null)
                PlayerUI.Instance.PulseIconAt(effectIconIndex);

            return;
        }

        if (cameraTransform == null)
        {
            Debug.LogWarning("[PlatformSpawnerForwardAbility] Camera non disponibile");

            if (failureSound != null && audioSource != null)
                audioSource.PlayOneShot(failureSound);

            if (PlayerUI.Instance != null)
                PlayerUI.Instance.PulseIconAt(effectIconIndex);

            return;
        }

        Debug.Log("[PlatformSpawnerForwardAbility] Attivazione abilità");
        DestroyCurrentPlatform();

        Activate();
        // ⭐ Suono matita gestito da DrawRevealEffect sul ghost prefab (PlayLoopAudio)

        if (PlayerUI.Instance != null)
            PlayerUI.Instance.PulseIconAt(effectIconIndex);
    }

    public override void Activate()
    {
        // ⭐ NUOVO: Blocco assoluto durante cooldown dopo cancel/respawn
        if (Time.time - lastCancelTime < CANCEL_COOLDOWN)
        {
            Debug.Log("[PlatformSpawnerForwardAbility] ⛔ Activate() BLOCCATO - cooldown dopo cancel attivo");
            return;
        }

        if (placing || currentGhost || cameraTransform == null) return;

        placing = true;
        IsActive = true;
        ignoreNextConfirm = true; // ⭐ Ignora il primo F che ha attivato il ghost
        isProcessingInput = false; // ⭐ Reset del flag
        confirmPressed = false; // ⭐ FIX: Reset per evitare che un press precedente (senza energia) attivi subito la conferma

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
        
        // ⭐ Verifica che siamo ancora in fase di placing prima di avviare l'audio
        if (placing && currentGhost != null)
        {
            drawEffect.PlayLoopAudio();
        }
    }

    public override void Deactivate()
    {
        // ⭐ Ferma coroutine prima di distruggere
        StopAllCoroutines();
        
        if (currentGhost) Destroy(currentGhost);
        currentGhost = null;

        placing = false;
        IsActive = false;
        isProcessingInput = false;
        confirmPressed = false;
        cancelPressed = false;
        ignoreNextConfirm = false;
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
        // ⭐ NUOVO: Previeni attivazione subito dopo CancelPlacement (durante respawn)
        if (Time.time - lastCancelTime < CANCEL_COOLDOWN)
        {
            Debug.Log($"[PlatformSpawnerForwardAbility] ⏳ Cooldown attivo dopo cancel: {CANCEL_COOLDOWN - (Time.time - lastCancelTime):F2}s rimanenti");
            return false;
        }

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