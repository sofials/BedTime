using UnityEngine;
using UnityEngine.InputSystem;
using CartoonFX;
using System.Collections;
using Unity.Cinemachine;

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

    [Header("Fade Settings")]
    [SerializeField] private float fadeOutDuration = 0.3f;
    [SerializeField] private float fadeInDuration = 0.3f;
    [SerializeField] private float holdTime = 0.1f;
    
    [Header("Camera Settings")]
    [SerializeField] private Camera targetCamera;
    
    [Header("Layer Settings")]
    [SerializeField] private LayerMask teleportLayerMask = -1;

    private AudioSource teleportConfirmAudioSource;
    private AudioSource teleportFailureAudioSource;
    private bool isTeleporting = false;
    private TeleportBase[] allTeleportBases;
    private ScreenFader screenFader;

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
                Debug.LogError("[TeleportAbility] CFXR_EffectController non trovato!");
        }

        if (teleportEffectController != null)
        {
            teleportEffectController.StopEffect();
            teleportEffectController.gameObject.SetActive(false);
        }

        screenFader = FindFirstObjectByType<ScreenFader>();
        if (screenFader == null)
        {
            Debug.LogWarning("[TeleportAbility] ScreenFader non trovato! Il fade non funzionerà.");
        }

        RefreshTeleportBases();
    }

    public void RefreshTeleportBases()
    {
        allTeleportBases = FindObjectsByType<TeleportBase>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Debug.Log($"[TeleportAbility] Trovate {allTeleportBases.Length} basi di teletrasporto");
    }

    protected override void Update()
    {
        base.Update();
        UpdateHoverState();

        if (Input.GetKeyDown(directTeleportKey))
        {
            TryActivate();
        }
    }

    private TeleportBase GetClosestTeleportableBase()
    {
        if (controllerGameObject == null || allTeleportBases == null) return null;

        Vector3 playerPos = controllerGameObject.transform.position;
        TeleportBase closestBase = null;
        float closestDistance = float.MaxValue;

        foreach (var baseObj in allTeleportBases)
        {
            if (baseObj == null || !baseObj.IsObjectActive) continue;
            if (baseObj.linkedBase == null) continue;

            float distance = Vector3.Distance(playerPos, baseObj.transform.position);
            if (distance <= baseObj.TeleportActivationRadius && distance < closestDistance)
            {
                closestDistance = distance;
                closestBase = baseObj;
            }
        }

        return closestBase;
    }

    private void UpdateHoverState()
    {
        if (controllerGameObject == null || allTeleportBases == null) return;

        Vector3 playerPos = controllerGameObject.transform.position;
        TeleportBase closestBase = null;
        float closestDistance = float.MaxValue;

        foreach (var baseObj in allTeleportBases)
        {
            if (baseObj == null || !baseObj.IsObjectActive) continue;

            if (baseObj.IsPlayerInHoverRange(playerPos))
            {
                float distance = Vector3.Distance(playerPos, baseObj.transform.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestBase = baseObj;
                }
            }
        }

        if (closestBase != TeleportBase.currentHoveredBase)
        {
            if (TeleportBase.currentHoveredBase != null)
            {
                TeleportBase.currentHoveredBase.OnCursorExit();
            }

            if (closestBase != null)
            {
                closestBase.OnCursorEnter();
            }
        }
    }
    
    public override bool CanActivate()
    {
        bool baseCanActivate = base.CanActivate();
        bool notTeleporting = !isTeleporting;
        TeleportBase closestBase = GetClosestTeleportableBase();
        bool canTeleportFromBase = closestBase != null;

        return baseCanActivate && notTeleporting && canTeleportFromBase;
    }

    public override void Activate()
    {
        TeleportBase sourceBase = GetClosestTeleportableBase();

        if (sourceBase == null || sourceBase.linkedBase == null)
        {
            Debug.LogWarning("[TeleportAbility] Nessuna coppia di basi valida!");
            return;
        }

        TeleportBase targetBase = sourceBase.linkedBase;
        Debug.Log($"[TeleportAbility] Teletrasporto: {sourceBase.name} → {targetBase.name}");

        powerUpScript.SpendPower(powerCost);
        PlayTeleportConfirmSound();

        Vector3 targetPosition = targetBase.GetTeleportPosition();
        StartCoroutine(ExecuteTeleportRoutine(targetPosition, targetBase));
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
            
            if (screenFader != null)
            {
                screenFader.SetClear();
            }
            
            Debug.Log("[TeleportAbility] Teletrasporto interrotto");
        }
    }

    public override void TryActivate()
    {
        if (!CanActivate())
        {
            string reason = GetDisableReason();
            Debug.Log($"[TeleportAbility] Fallito: {reason}");
            
            if (!reason.Contains("non permessa") && 
                !reason.Contains("nessuna base") &&
                !reason.Contains("devi essere sopra"))
            {
                PlayTeleportFailureSound();
            }
            return;
        }

        Activate();
        IsActive = true;

        if (activationSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(activationSound);
        }

        if (PlayerUI.Instance != null)
            PlayerUI.Instance.PulseIconAt(effectIconIndex);
    }

    public new string GetDisableReason()
    {
        string baseReason = base.GetDisableReason();
        if (baseReason != "motivo sconosciuto") return baseReason;

        if (isTeleporting) return "teletrasporto in corso";

        TeleportBase closestBase = GetClosestTeleportableBase();
        if (closestBase == null)
        {
            return "nessuna base di teletrasporto nel range";
        }

        return "motivo sconosciuto";
    }

    private IEnumerator ExecuteTeleportRoutine(Vector3 targetPosition, TeleportBase targetBase)
    {
        isTeleporting = true;
        LockPlayerMovement();
        SetPlayerVisible(false);

        // FASE 1: FADE OUT
        if (screenFader != null && fadeOutDuration > 0f)
        {
            yield return screenFader.FadeOut(fadeOutDuration);
        }

        // FASE 2: A SCHERMO NERO - Effetti e teletrasporto
        if (teleportEffectController != null)
        {
            teleportEffectController.gameObject.SetActive(true);
            teleportEffectController.PlayEffect();
        }

        // Attesa a schermo nero
        if (holdTime > 0f)
        {
            yield return new WaitForSeconds(holdTime);
        }

        // Teletrasporto fisico
        PerformPhysicalTeleport(targetPosition);

        // FASE 3: FADE IN
        SetPlayerVisible(true);

        if (screenFader != null && fadeInDuration > 0f)
        {
            yield return screenFader.FadeIn(fadeInDuration);
        }

        // Cleanup
        if (teleportEffectController != null)
        {
            teleportEffectController.StopEffect();
            teleportEffectController.gameObject.SetActive(false);
        }

        UnlockPlayerMovement();
        targetBase.OnPlayerTeleported();

        IsActive = false;
        isTeleporting = false;

        Debug.Log("[TeleportAbility] Teletrasporto completato!");
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

            // Forza la camera a saltare immediatamente
            CinemachineCore.ResetCameraState();

            Debug.Log($"[TeleportAbility] Player teletrasportato a: {finalTarget}");
        }
        else
        {
            Debug.LogWarning("[TeleportAbility] CharacterController non trovato!");
        }
    }

    private void LockPlayerMovement()
    {
        var controller = controllerGameObject.GetComponent<ThirdPersonController>();
        if (controller != null) 
        {
            controller.IsMovementLocked = true;
        }
    }

    private void UnlockPlayerMovement()
    {
        var controller = controllerGameObject.GetComponent<ThirdPersonController>();
        if (controller != null) 
        {
            controller.IsMovementLocked = false;
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
        }
    }

    private void PlayTeleportFailureSound()
    {
        if (teleportFailureSound != null && teleportFailureAudioSource != null)
        {
            teleportFailureAudioSource.PlayOneShot(teleportFailureSound);
        }
    }

    public bool ForceTeleportToPosition(Vector3 targetPosition)
    {
        if (!IsEnabled || isTeleporting)
        {
            Debug.Log($"[TeleportAbility] Impossibile forzare: {GetDisableReason()}");
            return false;
        }

        Debug.Log($"[TeleportAbility] Teletrasporto forzato a: {targetPosition}");
        
        IsActive = true;
        PlayTeleportConfirmSound();
        
        GameObject tempBase = new GameObject("TempTeleportBase");
        tempBase.transform.position = targetPosition;
        TeleportBase tempTeleportBase = tempBase.AddComponent<TeleportBase>();
        
        StartCoroutine(ExecuteTeleportRoutine(targetPosition, tempTeleportBase));
        StartCoroutine(DestroyTempBase(tempBase));
        
        return true;
    }

    private IEnumerator DestroyTempBase(GameObject tempBase)
    {
        yield return new WaitForSeconds(2f);
        if (tempBase != null)
            Destroy(tempBase);
    }
}