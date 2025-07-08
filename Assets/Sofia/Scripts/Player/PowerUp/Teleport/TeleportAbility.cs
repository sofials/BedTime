using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using CartoonFX;

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

    public override int powerCost => 50;
    protected override bool HasFixedDuration => false;

    private GameObject currentPointer;
    private PlayerControls controls;
    private bool confirmPressed;
    private Coroutine currentRoutine;

    private void Awake()
    {
        controls = new PlayerControls();
        controls.Gameplay.Confirm.performed += _ => confirmPressed = true;
        controls.Enable();
        effectIconIndex = 2;
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

        if (!IsActive) return;

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

            if (currentRoutine != null)
                StopCoroutine(currentRoutine);

            currentRoutine = StartCoroutine(TeleportRoutine(0.5f));
        }
    }

    public override void TryActivate()
    {
        if (IsActive)
        {
            if (currentRoutine != null)
            {
                StopCoroutine(currentRoutine);
                currentRoutine = null;
            }
            Deactivate();

            // Ripristina alpha e surface opaque per sicurezza
            var mats = GetAllMaterials();
            foreach (var mat in mats)
            {
                if (mat.HasProperty("_Color"))
                {
                    Color c = mat.color;
                    c.a = 1f;
                    mat.color = c;
                    FadeHelper.SetMaterialSurfaceType(mat, false);
                }
            }
            SetVisible(true);
        }
        else if (CanActivate())
        {
            Activate();
            IsActive = true;

            if (PlayerUI.Instance != null)
                PlayerUI.Instance.PulseIconAt(effectIconIndex);

            if (HasFixedDuration)
                Invoke(nameof(Deactivate), duration);
        }
        else
        {
            Debug.Log("Impossibile attivare il teletrasporto.");
        }
    }

    public override void Activate()
    {
        currentPointer = Instantiate(telePointerPrefab);
        var mats = GetAllMaterials();
        currentRoutine = StartCoroutine(FadeOutAndPlayEffect(mats, 0.5f));
    }

    public override void Deactivate()
    {
        if (currentRoutine != null)
        {
            StopCoroutine(currentRoutine);
            currentRoutine = null;
        }

        if (teleportEffectController != null)
        {
            teleportEffectController.StopEffect();
            teleportEffectController.gameObject.SetActive(false);
        }

        if (currentPointer)
        {
            Destroy(currentPointer);
            currentPointer = null;
        }

        SetVisible(true);
        var mats = GetAllMaterials();
        foreach (var mat in mats)
        {
            if (mat.HasProperty("_Color"))
            {
                Color c = mat.color;
                c.a = 1f;
                mat.color = c;
            }
            FadeHelper.SetMaterialSurfaceType(mat, false);
        }

        IsActive = false;
    }

    private void UpdatePointerPosition()
    {
        Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());

        if (Physics.Raycast(ray, out var hit, 500f, teleportableLayers))
        {
            if (currentPointer != null)
            {
                currentPointer.SetActive(true);
                Vector3 p = hit.point; p.y += 0.1f;
                currentPointer.transform.SetPositionAndRotation(p, Quaternion.LookRotation(hit.normal));
            }
        }
        else
        {
            if (currentPointer != null)
                currentPointer.SetActive(false);
        }
    }

    private IEnumerator FadeOutAndPlayEffect(Material[] mats, float duration)
    {
        // Usa la versione aggiornata con forzatura refresh renderers
        yield return StartCoroutine(FadeHelper.FadeMaterialsAlpha(mats, 1f, 0f, duration, meshesToHide));
        SetVisible(false);

        if (teleportEffectController != null)
        {
            teleportEffectController.gameObject.SetActive(true);
            teleportEffectController.PlayEffect();
        }
    }

    private IEnumerator TeleportRoutine(float fadeDuration)
    {
        if (currentPointer == null || !currentPointer.activeSelf)
        {
            Debug.Log("Punto di teletrasporto non valido.");
            Deactivate();
            yield break;
        }

        var mats = GetAllMaterials();

        if (teleportEffectController != null)
        {
            teleportEffectController.StopEffect();
            teleportEffectController.gameObject.SetActive(false);
        }

        if (controllerGameObject && controllerGameObject.TryGetComponent(out CharacterController cc))
        {
            Vector3 target = currentPointer.transform.position;
            target.y += cc.height * 0.5f;

            cc.enabled = false;
            controllerGameObject.transform.position = target;
            cc.enabled = true;
        }
        else
        {
            Debug.LogWarning("CharacterController non trovato.");
        }

        // Prepara materiali per il fade in (alpha = 0 e surface trasparente)
        PrepareFadeIn(mats);

        // Mostra mesh trasparente subito (ma senza fade ancora)
        SetVisible(true);

        if (teleportEffectController != null)
        {
            teleportEffectController.gameObject.SetActive(true);
            teleportEffectController.PlayEffect();
        }

        yield return new WaitForSeconds(0.5f);

        // Ora esegui il fade in del player con versione sincronizzata
        yield return StartCoroutine(FadeHelper.FadeMaterialsAlpha(mats, 0f, 1f, fadeDuration, meshesToHide));

        yield return new WaitForSeconds(0.3f);

        if (teleportEffectController != null)
        {
            teleportEffectController.StopEffect();
            teleportEffectController.gameObject.SetActive(false);
        }

        Deactivate();
    }

    private void PrepareFadeIn(Material[] mats)
    {
        foreach (var mat in mats)
        {
            FadeHelper.SetMaterialSurfaceType(mat, true);
            if (mat.HasProperty("_Color"))
            {
                Color c = mat.color;
                c.a = 0f;
                mat.color = c;
            }
        }
    }

    private Material[] GetAllMaterials()
    {
        var mats = new System.Collections.Generic.List<Material>();
        foreach (var smr in meshesToHide)
        {
            if (smr != null)
                mats.AddRange(smr.materials);
        }
        return mats.ToArray();
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
