using UnityEngine;
using UnityEngine.InputSystem;

public class TeleportAbility : AbilityBase
{
    [Header("Teletrasporto")]
    public GameObject telePointerPrefab;
    public LayerMask teleportableLayers;
    public SkinnedMeshRenderer[] meshesToHide;

    [Header("Controller")]
    [Tooltip("Il GameObject che contiene il CharacterController da usare per il teletrasporto.")]
    public GameObject controllerGameObject;

    [Header("VFX")]
    public ParticleSystem teleportStartVFX;

    public override int powerCost => 50;
    protected override bool HasFixedDuration => false;

    private GameObject currentPointer;
    private PlayerControls controls;
    private bool confirmPressed;

    /* --------- INITIALISATION --------- */

    private void Awake()
    {
        controls = new PlayerControls();
        controls.Gameplay.Confirm.performed += _ => confirmPressed = true;
        controls.Enable();

        effectIconIndex = 2;          // slot dell’icona
    }

    /* --------- MAIN LOOP --------- */

    protected override void Update()  // ← ora è override!
    {
        base.Update();                // mantiene l’icona aggiornata

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

            TeleportToPointer();
            powerUpScript.SpendPower(powerCost);
            Deactivate();
        }
    }

    /* --------- PUBLIC API --------- */

    public override void Activate()
    {
        if (IsActive) return;

        IsActive = true;
        currentPointer = Instantiate(telePointerPrefab);
        SetVisible(false);

        if (teleportStartVFX) teleportStartVFX.Play();
    }

    public override void Deactivate()
    {
        if (!IsActive) return;

        IsActive = false;

        if (currentPointer) Destroy(currentPointer);
        SetVisible(true);

        if (teleportStartVFX) teleportStartVFX.Stop(true,
            ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    /* --------- INTERNAL --------- */

    private void UpdatePointerPosition()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out var hit, 500f, teleportableLayers))
        {
            currentPointer.SetActive(true);

            Vector3 p = hit.point; p.y += 0.1f;
            currentPointer.transform.SetPositionAndRotation(
                p, Quaternion.LookRotation(hit.normal));
        }
        else
        {
            currentPointer.SetActive(false);
        }
    }

    private void TeleportToPointer()
    {
        if (currentPointer == null || !currentPointer.activeSelf)
        {
            Debug.Log("Punto di teletrasporto non valido.");
            return;
        }

        Vector3 target = currentPointer.transform.position;

        if (controllerGameObject &&
            controllerGameObject.TryGetComponent(out CharacterController cc))
        {
            target.y += cc.height * 0.5f;

            cc.enabled = false;
            controllerGameObject.transform.position = target;
            cc.enabled = true;
        }
        else
        {
            Debug.LogWarning("CharacterController non trovato.");
        }
    }

    private void SetVisible(bool visible)
    {
        foreach (var m in meshesToHide) if (m) m.enabled = visible;
    }
}
