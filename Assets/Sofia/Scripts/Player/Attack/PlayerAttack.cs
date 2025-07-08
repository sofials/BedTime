using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

public class PlayerAttack : MonoBehaviour
{
    public GameObject attackHitbox;
    public Collider attackHitboxCollider;

    private Animator animator;
    private PlayerControls controls;
    private bool attackInput;
    private int ignoreFrames = 0;

    public UIEffectHandler attackIconKeyboard;
    public UIEffectHandler attackIconController;

    private UIEffectHandler currentEffectIcon =>
        Gamepad.current != null && Gamepad.current.wasUpdatedThisFrame
            ? attackIconController
            : attackIconKeyboard;

    public TrailRenderer punchTrail;
    // AGGIUNGI
    public CFXR_EffectController punchImpactFX;   // prefab con il ParticleSystem
    private bool hitConfirmedThisSwing = false;  // reset ad ogni swing


    public bool isAttacking = false;
    [SerializeField] private float attackDuration = 0.3f;
    private float attackTimer = 0f;
    [SerializeField] private float attackCooldown = 1f;
    private float lastAttackTime = -999f;

    // Nuova variabile: attackId incrementato ogni attacco
    private int attackId = 0;
    public int AttackId => attackId;

    private void Awake()
    {
        controls = new PlayerControls();

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

        if (attackInput)
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                attackInput = false;
                return;
            }

            if (Time.time >= lastAttackTime + attackCooldown)
            {
                animator.SetTrigger("Attack");

                if (currentEffectIcon != null)
                    currentEffectIcon.PulseIcon();

                isAttacking = true;
                attackTimer = attackDuration;
                lastAttackTime = Time.time;

                attackId++; // Incrementa ID attacco per segnalare nuovo swing
            }
            attackInput = false;
        }

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
    public void RegisterSuccessfulHit()
    {
       hitConfirmedThisSwing = true;
    }
// 1) Evento all’inizio del frame di contatto
   public void EnablePunchFX()
   {
       if (punchImpactFX != null && hitConfirmedThisSwing)
       {
           punchImpactFX.PlayEffect();
           hitConfirmedThisSwing = false;       // consumato per questo swing
       }
   }

   // 2) Evento di fine colpo (stesso frame in cui disabiliti il punchTrail)
   public void DisablePunchFX()
   {
       if (punchImpactFX != null)
          punchImpactFX.StopEffect();
   }

}
