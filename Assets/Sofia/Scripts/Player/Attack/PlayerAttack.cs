using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

public class PlayerAttack : MonoBehaviour
{
    public GameObject attackHitbox;
    private Animator animator;

    private PlayerControls controls;
    private bool attackInput;
    private int ignoreFrames = 0;

    [Header("UI Effect per l'attacco")]
    public UIEffectHandler attackIconKeyboard;
    public UIEffectHandler attackIconController;

    private UIEffectHandler currentEffectIcon =>
        Gamepad.current != null && Gamepad.current.wasUpdatedThisFrame
            ? attackIconController
            : attackIconKeyboard;

    private void Awake()
    {
        controls = new PlayerControls();

        controls.Gameplay.Attack.performed += ctx =>
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            attackInput = true;
        };
    }

    private void OnEnable() => controls.Gameplay.Enable();
    private void OnDisable() => controls.Gameplay.Disable();

    private void Start()
    {
        animator = GetComponentInChildren<Animator>();
    }

    private void Update()
    {
        if (ignoreFrames > 0)
        {
            ignoreFrames--;
            return;
        }

        if (Time.timeScale == 0f)
            return;

        if (attackInput)
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                attackInput = false;
                return;
            }

            animator.SetTrigger("Attack");

            if (currentEffectIcon != null)
                currentEffectIcon.PulseIcon();

            attackInput = false;
        }
    }
    public void IgnoreNextClick()
    {
        ignoreFrames = 2;
    }
    public void EnableHitbox()
{
    if (attackHitbox != null)
    {
        var collider = attackHitbox.GetComponent<Collider>();
        if (collider != null)
        {
            collider.enabled = true;
            Debug.Log("Hitbox abilitata");
        }
    }
}

public void DisableHitbox()
{
    if (attackHitbox != null)
    {
        var collider = attackHitbox.GetComponent<Collider>();
        if (collider != null)
        {
            collider.enabled = false;
            Debug.Log("Hitbox disabilitata");
        }
    }
}
}
