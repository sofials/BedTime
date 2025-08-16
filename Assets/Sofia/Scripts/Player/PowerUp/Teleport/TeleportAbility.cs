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

    public override int powerCost => 50;
    protected override bool HasFixedDuration => false;

    private PlayerControls controls;
    private bool confirmPressed;

    private Vector3 teleportPosition;
    private bool validTeleportTarget = false;

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

        // Sblocca il cursor per il teletrasporto
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

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

        // Ripristina il cursor come era prima
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

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
        
        // Usa la posizione del mouse anche se nascosto
        if (Mouse.current != null)
        {
            mousePosition = Mouse.current.position.ReadValue();
            Debug.Log($"Mouse position: {mousePosition}");
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
        
        // TEST: Prima controlliamo se colpisce QUALSIASI cosa
        if (Physics.Raycast(ray, out var anyHit, 900f))
        {
            Debug.Log($"🎯 RAYCAST COLPISCE: {anyHit.collider.name} - Layer: {LayerMask.LayerToName(anyHit.collider.gameObject.layer)} ({anyHit.collider.gameObject.layer}) - Distanza: {anyHit.distance:F2}");
            
            // Ora controlliamo se è nel layer corretto
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