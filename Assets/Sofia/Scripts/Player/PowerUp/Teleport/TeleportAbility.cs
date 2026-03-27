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

    if (Input.GetKeyDown(directTeleportKey))
    {
        TryActivate();
    }
}
    
public override bool CanActivate()
{
    bool baseCanActivate = base.CanActivate();
    bool notTeleporting = !isTeleporting;
    
    // Verifica che ci sia una base inquadrata, che siamo sopra di essa e che abbia una gemella
    bool canTeleportFromBase = false;
    
    if (TeleportBase.currentHoveredBase != null && controllerGameObject != null)
    {
        float distanceToTarget = Vector3.Distance(
            controllerGameObject.transform.position, 
            TeleportBase.currentHoveredBase.transform.position
        );
        
        // Possiamo teletrasportarci SOLO se siamo sopra la base E ha una gemella
        if (distanceToTarget <= minTeleportDistance && TeleportBase.currentHoveredBase.linkedBase != null)
        {
            canTeleportFromBase = true;
        }
    }
    
    return baseCanActivate && notTeleporting && canTeleportFromBase;
}

    public override void Activate()
{
    if (TeleportBase.currentHoveredBase == null || TeleportBase.currentHoveredBase.linkedBase == null)
    {
        Debug.LogWarning("Activate() chiamato senza una coppia di basi valida!");
        return;
    }

    TeleportBase targetBase = TeleportBase.currentHoveredBase.linkedBase;
    Debug.Log($"ATTIVAZIONE - Teletrasporto dalla base {TeleportBase.currentHoveredBase.name} alla gemella {targetBase.name}");

    powerUpScript.SpendPower(powerCost);
    PlayTeleportConfirmSound();
    SetPlayerVisible(false);

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
            
            Debug.Log("Teletrasporto forzatamente interrotto");
        }
    }

   public override void TryActivate()
{
    Debug.Log("Tentativo teletrasporto...");

    // Prima verifica: l'abilità deve essere abilitata
    if (!IsEnabled)
    {
        Debug.Log("FAILURE - Abilità non abilitata");
        return;
    }

    // Seconda verifica: deve esserci una TeleportBase inquadrata con una gemella
    if (TeleportBase.currentHoveredBase == null || TeleportBase.currentHoveredBase.linkedBase == null)
    {
        Debug.Log("FAILURE - Nessuna base valida con destinazione collegata");
        return;
    }

    // Terza verifica: controlla se può essere attivata (energia, cooldown, distanza, ecc.)
    if (!CanActivate())
    {
        Debug.Log($"FAILURE - {GetDisableReason()}");
        PlayTeleportFailureSound();
        return;
    }

    // Se arriviamo qui, tutto è OK - attiva l'abilità
    base.TryActivate();
}  public new string GetDisableReason()
{
    string baseReason = base.GetDisableReason();
    if (baseReason != "motivo sconosciuto") return baseReason;
    
    if (isTeleporting) return "teletrasporto in corso";
    if (TeleportBase.currentHoveredBase == null) return "nessuna base nelle vicinanze";
    
    if (TeleportBase.currentHoveredBase != null && controllerGameObject != null)
    {
        float distanceToTarget = Vector3.Distance(
            controllerGameObject.transform.position, 
            TeleportBase.currentHoveredBase.transform.position
        );
        
        // Se siamo troppo lontani dalla base
        if (distanceToTarget > minTeleportDistance)
        {
            return "devi essere sopra una base di teletrasporto";
        }
        
        // Se siamo sopra la base ma non ha una gemella
        if (TeleportBase.currentHoveredBase.linkedBase == null)
        {
            return "questa base non ha una destinazione collegata";
        }
    }
    
    return "motivo sconosciuto";
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