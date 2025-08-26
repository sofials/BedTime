using UnityEngine;
using UnityEngine.InputSystem;
using CartoonFX;
using System.Collections;

public class TeleportAbility : AbilityBase
{
    [Header("Teletrasporto")]
    public LayerMask teleportableLayers;
    public SkinnedMeshRenderer[] meshesToHide;
    public GameObject teleportPointer; // Effetto particellare che indica dove punta il mouse
    public Camera playerCamera; // Camera da assegnare dall'inspector
    
    private ParticleSystem[] pointerParticleSystems; // Cache dei particle systems

    [Header("Controller")]
    public GameObject controllerGameObject;

    [Header("Effect FX")]
    public CFXR_EffectController teleportEffectController;

    [Header("Audio")]
    public AudioClip teleportConfirmSound;  // Audio specifico per conferma teletrasporto
    private AudioSource teleportConfirmAudioSource;

    [Header("Direct Teleport")]
    public KeyCode directTeleportKey = KeyCode.E; // Tasto per teletrasporto diretto

    public override int powerCost => 50;
    protected override bool HasFixedDuration => false;

    private PlayerControls controls;
    private bool confirmPressed;

    private Vector3 teleportPosition;
    private bool validTeleportTarget = false;
    
    // Tracciamento hover per TeleportBase
    private TeleportBase currentMouseHoveredBase = null;

    protected override void Awake()
    {
        base.Awake();

        controls = new PlayerControls();
        controls.Gameplay.Confirm.performed += _ => confirmPressed = true;
        controls.Enable();

        effectIconIndex = 2;

        teleportConfirmAudioSource = gameObject.AddComponent<AudioSource>();
        teleportConfirmAudioSource.playOnAwake = false;
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

        // Setup del pointer e cache dei particle systems
        if (teleportPointer != null)
        {
            Debug.Log($"TeleportPointer trovato: {teleportPointer.name}");
            
            // Cache tutti i particle systems nel pointer
            pointerParticleSystems = teleportPointer.GetComponentsInChildren<ParticleSystem>();
            Debug.Log($"Particle Systems trovati nel pointer: {pointerParticleSystems.Length}");
            
            // Assicurati che il pointer sia inizialmente disattivo
            teleportPointer.SetActive(false);
            
            // Ferma tutti i particle systems
            foreach (var ps in pointerParticleSystems)
            {
                ps.Stop();
            }
        }
        else
        {
            Debug.LogError("TeleportPointer non assegnato nell'inspector!");
        }
    }

    protected override void Update()
    {
        base.Update();

        // Controllo costante per TeleportBase sotto il mirino (centro schermo)
        CheckCrosshairHover();
        
        // Controllo hover del mouse invisibile sulle TeleportBase
        CheckMouseHoverOnTeleportBases();

        // Controllo per teletrasporto diretto su TeleportBase
        if (Input.GetKeyDown(directTeleportKey))
        {
            TryDirectTeleport();
        }

        if (!IsActive) 
        {
            return;
        }

        UpdateTeleportTarget();

        if (confirmPressed)
        {
            confirmPressed = false;

            if (!validTeleportTarget)
            {
                Debug.Log("Target di teletrasporto non valido.");
                return;
            }

            if (!powerUpScript.HasEnoughPower(powerCost))
            {
                Debug.Log("Non hai abbastanza potere per il teletrasporto.");
                return;
            }

            powerUpScript.SpendPower(powerCost);

            // Esegui audio conferma
            if (teleportConfirmSound != null)
            {
                teleportConfirmAudioSource.PlayOneShot(teleportConfirmSound);
            }

            StartCoroutine(ConfirmTeleportRoutine());
        }
    }

    /// <summary>
    /// Controlla hover del mouse invisibile sulle TeleportBase per feedback visivo
    /// </summary>
    private void CheckMouseHoverOnTeleportBases()
    {
        Camera cameraToUse = playerCamera != null ? playerCamera : Camera.main;
        
        if (cameraToUse == null)
        {
            return;
        }

        // Raycast dalla posizione del mouse (anche se invisibile)
        Ray mouseRay = cameraToUse.ScreenPointToRay(Input.mousePosition);
        TeleportBase newHoveredBase = null;

        // Controlla se il mouse (invisibile) colpisce una TeleportBase
        if (Physics.Raycast(mouseRay, out RaycastHit hit, 100f))
        {
            TeleportBase teleportBase = hit.collider.GetComponent<TeleportBase>();
            if (teleportBase != null)
            {
                newHoveredBase = teleportBase;
            }
        }

        // Gestisci il cambio di hover del mouse
        if (newHoveredBase != currentMouseHoveredBase)
        {
            // Esci dal precedente hover
            if (currentMouseHoveredBase != null)
            {
                currentMouseHoveredBase.OnCursorExit();
            }

            // Entra nel nuovo hover
            currentMouseHoveredBase = newHoveredBase;
            if (newHoveredBase != null)
            {
                newHoveredBase.OnCursorEnter();
            }
        }
    }

    /// <summary>
    /// Controlla se c'è una TeleportBase sotto il mirino (centro schermo)
    /// </summary>
    private void CheckCrosshairHover()
    {
        Camera cameraToUse = playerCamera != null ? playerCamera : Camera.main;
        
        if (cameraToUse == null)
        {
            return;
        }

        // Raycast dal centro dello schermo
        Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        Ray ray = cameraToUse.ScreenPointToRay(screenCenter);
        
        TeleportBase previousHoveredBase = TeleportBase.currentHoveredBase;
        TeleportBase newHoveredBase = null;

        // Controlla se colpisce una TeleportBase
        if (Physics.Raycast(ray, out RaycastHit hit, 100f))
        {
            TeleportBase teleportBase = hit.collider.GetComponent<TeleportBase>();
            if (teleportBase != null)
            {
                newHoveredBase = teleportBase;
            }
        }

        // Gestisci il cambio di hover del crosshair
        if (newHoveredBase != previousHoveredBase)
        {
            // Esci dal precedente hover
            if (previousHoveredBase != null)
            {
                previousHoveredBase.OnCrosshairExit();
            }

            // Entra nel nuovo hover
            TeleportBase.currentHoveredBase = newHoveredBase;
            if (newHoveredBase != null)
            {
                newHoveredBase.OnCrosshairEnter();
            }
        }
    }

    /// <summary>
    /// Tenta il teletrasporto diretto su una TeleportBase sotto hover
    /// </summary>
    private void TryDirectTeleport()
    {
        // Prima priorità: TeleportBase sotto il crosshair (centro schermo)
        TeleportBase targetBase = TeleportBase.currentHoveredBase;
        
        // Se non c'è nulla sotto il crosshair, usa quella sotto il mouse
        if (targetBase == null)
        {
            targetBase = currentMouseHoveredBase;
        }
        
        if (targetBase == null)
        {
            Debug.Log("Nessuna TeleportBase sotto hover per il teletrasporto diretto.");
            
            // AUDIO FALLIMENTO - non stiamo facendo hover su una base
            if (failureSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(failureSound);
            }
            return;
        }

        Debug.Log($"Tentativo teletrasporto diretto su: {targetBase.gameObject.name}");

        // Controlla se hai abbastanza potere
        if (!powerUpScript.HasEnoughPower(powerCost))
        {
            Debug.Log("Non hai abbastanza potere per il teletrasporto diretto.");
            
            // AUDIO FALLIMENTO - non hai abbastanza potere
            if (failureSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(failureSound);
            }
            return;
        }

        // Nascondi immediatamente le mesh quando premiamo E
        SetVisible(false);

        // Spendi il potere
        powerUpScript.SpendPower(powerCost);

        // Esegui audio conferma
        if (teleportConfirmSound != null)
        {
            teleportConfirmAudioSource.PlayOneShot(teleportConfirmSound);
        }

        // Esegui il teletrasporto diretto
        Vector3 targetPosition = targetBase.GetTeleportPosition();
        StartCoroutine(DirectTeleportRoutine(targetPosition, targetBase));
    }

    /// <summary>
    /// Routine per il teletrasporto diretto
    /// </summary>
    private IEnumerator DirectTeleportRoutine(Vector3 targetPosition, TeleportBase targetBase)
    {
        // Disattiva il movimento del player
        var controller = controllerGameObject.GetComponent<ThirdPersonController>();
        if (controller != null) controller.IsMovementLocked = true;

        // Nascondi il player
        SetVisible(false);

        // Attiva effetti di teletrasporto
        if (teleportEffectController != null)
        {
            teleportEffectController.gameObject.SetActive(true);
            teleportEffectController.PlayEffect();
        }

        // Aspetta un momento per l'effetto
        yield return new WaitForSeconds(0.2f);

        // Esegui il teletrasporto
        if (controllerGameObject && controllerGameObject.TryGetComponent(out CharacterController cc))
        {
            Vector3 target = targetPosition;
            target.y += cc.height * 0.5f; // Alza leggermente per evitare che spawni nel terreno

            cc.enabled = false;
            controllerGameObject.transform.position = target;
            cc.enabled = true;

            Debug.Log($"Player teletrasportato a: {target}");
        }
        else
        {
            Debug.LogWarning("CharacterController non trovato per il teletrasporto diretto.");
        }

        // Mostra il player
        SetVisible(true);

        // Aspetta un momento
        yield return new WaitForSeconds(0.3f);

        // Ferma gli effetti
        if (teleportEffectController != null)
        {
            teleportEffectController.StopEffect();
            teleportEffectController.gameObject.SetActive(false);
        }

        // Riattiva il movimento
        if (controller != null) controller.IsMovementLocked = false;

        // Chiama l'evento sulla TeleportBase
        targetBase.OnPlayerTeleported();
    }

    public override void TryActivate()
    {
        Debug.Log($"TryActivate chiamato - IsActive: {IsActive}");
        
        if (IsActive)
        {
            Debug.Log("Disattivando teletrasporto...");
            Deactivate();
        }
        else if (CanActivate())
        {
            Debug.Log("Attivando teletrasporto...");
            base.TryActivate();  // Questo attiva il suono corretto
        }
        else
        {
            Debug.Log("Impossibile attivare il teletrasporto - CanActivate() = false");

            // AUDIO FALLIMENTO
            if (failureSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(failureSound);
            }
        }
    }

    public override void Activate()
    {
        Debug.Log("🚀 Activate() chiamato - Attivando teletrasporto!");
        
        SetVisible(false);

        if (teleportEffectController != null)
        {
            teleportEffectController.gameObject.SetActive(true);
            teleportEffectController.PlayEffect();
        }

        // IMPORTANTE: Il cursore rimane nascosto ma libero di muoversi
        // Non cambiamo le impostazioni del cursore qui!

        // Attiva l'effetto particellare pointer
        if (teleportPointer != null)
        {
            Debug.Log("🎆 Attivando teleportPointer...");
            teleportPointer.SetActive(true);
            
            // Avvia esplicitamente tutti i particle systems
            foreach (var ps in pointerParticleSystems)
            {
                ps.Play();
                Debug.Log($"Avviato particle system: {ps.name}");
            }
        }
        else
        {
            Debug.LogError("❌ teleportPointer è NULL!");
        }

        var controller = controllerGameObject.GetComponent<ThirdPersonController>();
        if (controller != null) controller.IsMovementLocked = true;
        
        // IMPORTANTE: Impostare IsActive = true ESPLICITAMENTE
        IsActive = true;
        
        Debug.Log($"✅ IsActive impostato a: {IsActive}");
    }

    public override void Deactivate()
    {
        Debug.Log("🛑 Deactivate() chiamato");
        
        SetVisible(true);

        // Non tocchiamo le impostazioni del cursore - rimane come impostato dal CursorController

        if (teleportEffectController != null)
        {
            teleportEffectController.StopEffect();
            teleportEffectController.gameObject.SetActive(false);
        }

        // Disattiva l'effetto particellare pointer
        if (teleportPointer != null)
        {
            Debug.Log("🛑 Disattivando teleportPointer...");
            
            // Ferma esplicitamente tutti i particle systems
            foreach (var ps in pointerParticleSystems)
            {
                ps.Stop();
            }
            
            teleportPointer.SetActive(false);
        }

        var controller = controllerGameObject.GetComponent<ThirdPersonController>();
        if (controller != null) controller.IsMovementLocked = false;

        IsActive = false;
        validTeleportTarget = false;
        
        Debug.Log($"✅ IsActive impostato a: {IsActive}");
    }

    private void UpdateTeleportTarget()
    {
        Debug.Log("🔍 UpdateTeleportTarget() ESEGUITO!");
        
        // Controllo sicurezza per camera
        Camera cameraToUse = playerCamera != null ? playerCamera : Camera.main;
        
        if (cameraToUse == null)
        {
            Debug.LogError("Nessuna camera disponibile! Assegna playerCamera nell'inspector o aggiungi tag MainCamera alla camera.");
            validTeleportTarget = false;
            if (teleportPointer != null)
            {
                teleportPointer.SetActive(false);
            }
            return;
        }

        Vector2 mousePosition;
        
        // Usa la posizione del mouse (anche se invisibile!)
        if (Mouse.current != null)
        {
            mousePosition = Mouse.current.position.ReadValue();
            Debug.Log($"Mouse position (invisibile): {mousePosition}");
        }
        else
        {
            // Fallback al centro schermo se mouse non disponibile
            mousePosition = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            Debug.Log("Mouse.current è null, uso centro schermo");
        }

        Ray ray = cameraToUse.ScreenPointToRay(mousePosition);
        
        // Debug del raycast - mostra dove sta puntando
        Debug.DrawRay(ray.origin, ray.direction * 900f, Color.red, 0.1f);
        Debug.Log($"Ray - Origin: {ray.origin}, Direction: {ray.direction}");
        
        // Controlliamo se colpisce qualcosa
        if (Physics.Raycast(ray, out var anyHit, 900f))
        {
            Debug.Log($"🎯 RAYCAST COLPISCE: {anyHit.collider.name} - Layer: {LayerMask.LayerToName(anyHit.collider.gameObject.layer)} ({anyHit.collider.gameObject.layer}) - Distanza: {anyHit.distance:F2}");
            
            // Controlliamo se è nel layer corretto
            if (((1 << anyHit.collider.gameObject.layer) & teleportableLayers) != 0)
            {
                Debug.Log("✅ OGGETTO NEL LAYER CORRETTO!");
                teleportPosition = anyHit.point;
                validTeleportTarget = true;

                // Posiziona l'effetto particellare pointer
                if (teleportPointer != null)
                {
                    teleportPointer.SetActive(true);
                    teleportPointer.transform.position = anyHit.point;
                    
                    // Avvia particle systems se non già attivi
                    foreach (var ps in pointerParticleSystems)
                    {
                        if (!ps.isPlaying)
                        {
                            ps.Play();
                        }
                    }
                    
                    Debug.Log($"📍 Pointer posizionato a: {anyHit.point}");
                    Debug.Log($"📍 Pointer attivo: {teleportPointer.activeInHierarchy}");
                }
            }
            else
            {
                Debug.Log($"❌ OGGETTO NON NEL LAYER CORRETTO. Layer mask value: {teleportableLayers.value}");
                validTeleportTarget = false;
                
                // Ferma le particelle ma mantieni attivo per debug
                if (teleportPointer != null)
                {
                    foreach (var ps in pointerParticleSystems)
                    {
                        ps.Stop();
                    }
                }
            }
        }
        else
        {
            Debug.Log("❌ RAYCAST NON COLPISCE NIENTE");
            validTeleportTarget = false;
            
            if (teleportPointer != null)
            {
                foreach (var ps in pointerParticleSystems)
                {
                    ps.Stop();
                }
            }
        }
    }

    private IEnumerator ConfirmTeleportRoutine()
    {
        if (!validTeleportTarget)
        {
            Debug.Log("Punto di teletrasporto non valido.");
            Deactivate();
            yield break;
        }

        if (controllerGameObject && controllerGameObject.TryGetComponent(out CharacterController cc))
        {
            Vector3 target = teleportPosition;
            target.y += cc.height * 0.5f;

            cc.enabled = false;
            controllerGameObject.transform.position = target;
            cc.enabled = true;
        }
        else
        {
            Debug.LogWarning("CharacterController non trovato.");
        }

        if (teleportEffectController != null)
        {
            teleportEffectController.gameObject.SetActive(true);
            teleportEffectController.PlayEffect();
        }

        yield return new WaitForSeconds(0.5f);

        SetVisible(true);

        yield return new WaitForSeconds(0.3f);

        if (teleportEffectController != null)
        {
            teleportEffectController.StopEffect();
            teleportEffectController.gameObject.SetActive(false);
        }

        Deactivate();
    }

    private void SetVisible(bool visible)
    {
        foreach (var smr in meshesToHide)
        {
            if (smr != null)
                smr.enabled = visible;
        }
    }
}