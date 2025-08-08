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

    [Header("UI Effect")]
    public UIEffectHandler attackIcon; // Modificato per usare una sola icona
    public int effectIconIndex = 0; // Indice dell'icona nell'array di PlayerUI

    public ParticleSystem punchEffect;            // Effetto generale del pugno (da inspector)
    public CFXR_EffectController punchImpactFX;   // Effetto specifico per impatto con nemici
    private bool hitConfirmedThisSwing = false;  // reset ad ogni swing
    private bool effectCurrentlyPlaying = false; // tracking dello stato dell'effetto

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
            attackInput = true;  // setta sempre, controlla in Update()
        };
    }

    private void OnEnable()
    {
        controls.Gameplay.Enable();
        // Sottoscrivi all'evento di cambio input
        InputSystem.onActionChange += OnInputActionChange;
    }

    private void OnDisable()
    {
        controls.Gameplay.Disable();
        // Rimuovi la sottoscrizione
        InputSystem.onActionChange -= OnInputActionChange;
    }

    private void OnInputActionChange(object obj, InputActionChange change)
    {
        if (change == InputActionChange.ActionPerformed)
        {
            bool wasGamepad = useGamepad;
            useGamepad = Gamepad.current != null && Gamepad.current.wasUpdatedThisFrame;

            // Aggiorna il testo dell'icona se è cambiato il tipo di input
            if (wasGamepad != useGamepad && attackIcon != null)
            {
                attackIcon.UpdateInputText(useGamepad);
            }
        }
    }

    private void Start()
    {
        animator = GetComponentInChildren<Animator>();
        
        // Assicurati che gli effetti siano spenti all'inizio
        if (punchEffect != null)
        {
            punchEffect.Stop();
        }
        if (punchImpactFX != null)
        {
            punchImpactFX.StopEffect();
            effectCurrentlyPlaying = false;
        }
    }

    private bool useGamepad = false; // Nuova variabile per tenere traccia del tipo di input

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

                // Usa l'indice per aggiornare l'icona tramite PlayerUI
                if (PlayerUI.Instance != null && effectIconIndex >= 0)
                {
                    PlayerUI.Instance.PulseIconAt(effectIconIndex);
                }

                isAttacking = true;
                attackTimer = attackDuration;
                lastAttackTime = Time.time;

                attackId++; // Incrementa ID attacco per segnalare nuovo swing
                hitConfirmedThisSwing = false; // Reset hit confirmation per nuovo attacco
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

    // Chiamato dall'animazione per attivare l'effetto generale del pugno
    public void EnablePunchEffect()
    {
        if (punchEffect != null)
        {
            punchEffect.Play();
            Debug.Log("PunchEffect generale attivato");
        }
    }

    // Chiamato dall'animazione per disattivare l'effetto generale del pugno
    public void DisablePunchEffect()
    {
        if (punchEffect != null)
        {
            punchEffect.Stop();
            Debug.Log("PunchEffect generale disattivato");
        }
    }

    // Chiamato quando il pugno va a segno contro un nemico
    public void EnablePunchFX()
    {
        if (punchImpactFX != null && hitConfirmedThisSwing && !effectCurrentlyPlaying)
        {
            punchImpactFX.PlayEffect();
            effectCurrentlyPlaying = true;
            Debug.Log("PunchFX attivato su impatto nemico");
        }
    }

    // Chiamato per fermare l'effetto impatto
    public void DisablePunchFX()
    {
        if (punchImpactFX != null && effectCurrentlyPlaying)
        {
            punchImpactFX.StopEffect();
            effectCurrentlyPlaying = false;
            Debug.Log("PunchFX disattivato");
        }
    }

    // Chiamato quando il colpo colpisce effettivamente un nemico
    public void RegisterSuccessfulHit()
    {
        hitConfirmedThisSwing = true;
        Debug.Log("Hit confermato per questo swing");
    }

    // Metodo di sicurezza per forzare lo stop dell'effetto
    public void ForceStopPunchEffect()
    {
        if (punchImpactFX != null)
        {
            punchImpactFX.StopEffect();
            effectCurrentlyPlaying = false;
            Debug.Log("PunchEffect forzatamente fermato");
        }
    }
}