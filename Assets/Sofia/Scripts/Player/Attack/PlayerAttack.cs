using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

public class PlayerAttack : MonoBehaviour
{
    public GameObject attackHitbox;
    [Header("Collider della hitbox da assegnare nell'Inspector")]
    public Collider attackHitboxCollider;

    private Animator animator;
    private PlayerControls controls;
    private bool attackInput;
    private int ignoreFrames = 0;

    [Header("UI Effect per l'attacco")]
    public UIEffectHandler attackIconEffect;
    [Header("Scia del pugno")]
    public TrailRenderer punchTrail;

    public bool isAttacking = false;
    [SerializeField] private float attackDuration = 0.3f; // Durata in secondi dell'attacco
    private float attackTimer = 0f;

    private void Awake()
    {
        controls = new PlayerControls();

        // Se non assegnato da Inspector, prova a trovarlo tra i figli
        if (attackHitboxCollider == null && attackHitbox != null)
            attackHitboxCollider = attackHitbox.GetComponent<Collider>();

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

        // Gestione attacco
        if (attackInput)
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                attackInput = false;
                return;
            }

            animator.SetTrigger("Attack");
            if (attackIconEffect != null)
                attackIconEffect.PulseIcon();

            isAttacking = true;
            attackTimer = attackDuration;
            attackInput = false;
        }

        // Timer per la durata dell'attacco
        if (isAttacking)
        {
            attackTimer -= Time.deltaTime;
            if (attackTimer <= 0f)
            {
                isAttacking = false;
            }
        }
    }
    public void IgnoreNextClick()
    {
        ignoreFrames = 2;
    }
    public void EnablePunchTrail()
    {
        if (punchTrail != null)
        {
            punchTrail.enabled = true;
            Debug.Log("PunchTrail abilitata");
        }
    }

    public void DisablePunchTrail()
    {
        if (punchTrail != null)
        {
            punchTrail.enabled = false;
            Debug.Log("PunchTrail disabilitata");
        }
    }

}
