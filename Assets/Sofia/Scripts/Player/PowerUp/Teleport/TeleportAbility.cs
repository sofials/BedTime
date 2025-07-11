using UnityEngine;
using UnityEngine.InputSystem;
using CartoonFX;
using System.Collections;

public class TeleportAbility : AbilityBase
{
    [Header("Teletrasporto")]
    public GameObject telePointerPrefab;
    public LayerMask teleportableLayers;
    public SkinnedMeshRenderer[] meshesToHide;

    [Header("Controller")]
    public GameObject controllerGameObject;

    [Header("Effect FX")]
    public CFXR_EffectController teleportEffectController;

    [Header("Audio")]
    public AudioClip teleportConfirmSound;  // Audio specifico per conferma teletrasporto
    private AudioSource teleportConfirmAudioSource;

    public override int powerCost => 50;
    protected override bool HasFixedDuration => false;

    private GameObject currentPointer;
    private PlayerControls controls;
    private bool confirmPressed;

    private Vector3 teleportPosition;
    private bool canUpdatePointer = false;

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
    }

    protected override void Update()
    {
        base.Update();

        if (!IsActive || !canUpdatePointer) return;

        UpdatePointerPosition();

        if (confirmPressed)
        {
            confirmPressed = false;

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
    if (IsActive)
    {
        Deactivate();
    }
    else if (CanActivate())
    {
        base.TryActivate();  // Questo attiva il suono corretto
    }
    else
    {
        Debug.Log("Impossibile attivare il teletrasporto.");

        // AUDIO FALLIMENTO
        if (failureSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(failureSound);
        }
    }
}

    public override void Activate()
    {
        Vector3 playerPos = controllerGameObject.transform.position;
        Quaternion playerRot = controllerGameObject.transform.rotation;

        currentPointer = Instantiate(telePointerPrefab, playerPos, playerRot);

        SetVisible(false);

        if (teleportEffectController != null)
        {
            teleportEffectController.gameObject.SetActive(true);
            teleportEffectController.PlayEffect();
        }

        var controller = controllerGameObject.GetComponent<ThirdPersonController>();
        if (controller != null) controller.IsMovementLocked = true;

        canUpdatePointer = false;
        StartCoroutine(EnablePointerUpdateNextFrame());
    }

    private IEnumerator EnablePointerUpdateNextFrame()
    {
        yield return null; // aspetta un frame
        canUpdatePointer = true;
    }

    public override void Deactivate()
    {
        if (currentPointer)
        {
            Destroy(currentPointer);
            currentPointer = null;
        }

        SetVisible(true);

        if (teleportEffectController != null)
        {
            teleportEffectController.StopEffect();
            teleportEffectController.gameObject.SetActive(false);
        }

        var controller = controllerGameObject.GetComponent<ThirdPersonController>();
        if (controller != null) controller.IsMovementLocked = false;

        IsActive = false;
        canUpdatePointer = false;
    }

    private void UpdatePointerPosition()
    {
        Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());

        if (Physics.Raycast(ray, out var hit, 500f, teleportableLayers))
        {
            if (currentPointer != null)
            {
                currentPointer.SetActive(true);
                teleportPosition = hit.point;

                Vector3 forward = Vector3.ProjectOnPlane(Camera.main.transform.forward, Vector3.up);
                currentPointer.transform.SetPositionAndRotation(teleportPosition, Quaternion.LookRotation(forward));
            }
        }
        else
        {
            if (currentPointer != null)
                currentPointer.SetActive(false);
        }
    }

    private IEnumerator ConfirmTeleportRoutine()
    {
        if (currentPointer == null || !currentPointer.activeSelf)
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

        if (currentPointer)
        {
            Destroy(currentPointer);
            currentPointer = null;
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
