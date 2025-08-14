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

    public ParticleSystem punchEffect;            // Effetto generale del pugno (da inspector)
    public CFXR_EffectController punchImpactFX;   // Effetto specifico per impatto con nemici
    private bool hitConfirmedThisSwing = false;  // reset ad ogni swing

    [Header("Audio")]
    public AudioSource punchAudioSource;          // Audio per l'effetto generale del pugno
    public AudioSource punchImpactAudioSource;   // Audio per l'impatto con nemici

    public bool isAttacking = false;
    [SerializeField] private float attackDuration = 0.3f;
    private float attackTimer = 0f;
    [SerializeField] private float attackCooldown = 1f;
    private float lastAttackTime = -999f;

    // Nuova variabile: attackId incrementato ogni attacco
    private int attackId = 0;
    public int AttackId => attackId;

    [Header("Attack Movement")]
    [SerializeField] private float attackAdvanceDistance = 1f; // Distanza da percorrere durante l'attacco
    [SerializeField] private AnimationCurve attackAdvanceCurve = AnimationCurve.EaseInOut(0, 0, 1, 1); // Curva per il movimento
    [SerializeField] private float attackAdvanceDuration = 0.4f; // Durata del movimento di avanzamento
    
    // Riferimenti per il movimento
    private ThirdPersonController playerController;
    private bool isAdvancing = false;
    private float advanceTimer = 0f;
    private Vector3 advanceDirection;
    private Vector3 startPosition;

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

    private void OnEnable() => controls.Gameplay.Enable();
    private void OnDisable() => controls.Gameplay.Disable();

    private void Start()
    {
        animator = GetComponentInChildren<Animator>();
        playerController = GetComponent<ThirdPersonController>();
        
        // Assicurati che gli effetti siano spenti all'inizio
        if (punchEffect != null)
        {
            punchEffect.Stop();
        }
        if (punchImpactFX != null)
        {
            punchImpactFX.StopEffect();
        }

        // Inizializza gli audio sources
        if (punchAudioSource != null)
        {
            punchAudioSource.playOnAwake = false;
        }
        if (punchImpactAudioSource != null)
        {
            punchImpactAudioSource.playOnAwake = false;
        }

        // Crea una curva di default se non è stata impostata
        if (attackAdvanceCurve == null || attackAdvanceCurve.keys.Length == 0)
        {
            attackAdvanceCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        }
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

                // Reset completo per nuovo attacco
                attackId++;
                hitConfirmedThisSwing = false;

                // Inizia il movimento di avanzamento
                StartAttackAdvance();
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

        // Gestisci il movimento di avanzamento
        HandleAttackAdvance();
    }

    private void StartAttackAdvance()
    {
        if (playerController == null) return;

        // Calcola la direzione di avanzamento basata sulla rotazione del player
        advanceDirection = transform.forward;
        startPosition = transform.position;
        
        isAdvancing = true;
        advanceTimer = 0f;

        Debug.Log($"Iniziato avanzamento attacco: direzione {advanceDirection}, distanza {attackAdvanceDistance}");
    }

    private void HandleAttackAdvance()
    {
        if (!isAdvancing || playerController == null) return;

        advanceTimer += Time.deltaTime;
        float normalizedTime = Mathf.Clamp01(advanceTimer / attackAdvanceDuration);

        if (normalizedTime >= 1f)
        {
            // Movimento completato
            isAdvancing = false;
            return;
        }

        // Calcola la velocità di avanzamento basata sulla curva
        float curveValue = attackAdvanceCurve.Evaluate(normalizedTime);
        float nextCurveValue = attackAdvanceCurve.Evaluate(Mathf.Clamp01((advanceTimer + Time.deltaTime) / attackAdvanceDuration));
        float speedMultiplier = (nextCurveValue - curveValue) / Time.deltaTime;
        
        // Calcola la velocità di avanzamento per questo frame
        Vector3 advanceVelocity = advanceDirection * (attackAdvanceDistance * speedMultiplier);
        
        // Applica direttamente la velocità di avanzamento al player
        // Manteniamo solo la componente orizzontale per non interferire con gravità/salti
        advanceVelocity.y = 0f;
        
        // Aggiungi la velocità di avanzamento alla playerVelocity esistente
        playerController.AddAttackVelocity(advanceVelocity);
    }

    public void IgnoreNextClick()
    {
        ignoreFrames = 5;
    }

    // ANIMATION EVENT - Chiamato dall'animazione per attivare l'effetto generale del pugno
    public void EnablePunchEffect()
    {
        if (punchEffect != null)
        {
            punchEffect.Play();
            Debug.Log("PunchEffect generale attivato");
        }

        // Riproduce l'audio contemporaneamente all'effetto visivo
        if (punchAudioSource != null)
        {
            punchAudioSource.Play();
            Debug.Log("PunchAudio generale riprodotto");
        }
    }

    // ANIMATION EVENT - Chiamato dall'animazione per disattivare l'effetto generale del pugno
    public void DisablePunchEffect()
    {
        if (punchEffect != null)
        {
            punchEffect.Stop();
            Debug.Log("PunchEffect generale disattivato");
        }

        // Ferma l'audio se sta ancora riproducendo
        if (punchAudioSource != null && punchAudioSource.isPlaying)
        {
            punchAudioSource.Stop();
            Debug.Log("PunchAudio generale fermato");
        }
    }

    // COLLISION DETECTION - Chiamato quando il pugno colpisce effettivamente un nemico
    public void RegisterSuccessfulHit()
    {
        // ===== PROTEZIONE CONTRO HIT MULTIPLI =====
        if (hitConfirmedThisSwing)
        {
            return; // Evita effetti multipli nello stesso swing
        }

        hitConfirmedThisSwing = true;
        Debug.Log($"Hit confermato per attackId: {attackId}");

        // Riproduce immediatamente l'effetto di impatto
        if (punchImpactFX != null)
        {
            punchImpactFX.PlayEffect();
            Debug.Log("PunchImpactFX attivato su collision");
        }

        // Riproduce immediatamente l'audio di impatto
        if (punchImpactAudioSource != null)
        {
            punchImpactAudioSource.Play();
            Debug.Log("PunchImpactAudio riprodotto su collision");
        }
    }

    // Metodo di utilità per fermare manualmente gli effetti di impatto se necessario
    public void StopImpactEffects()
    {
        if (punchImpactFX != null)
        {
            punchImpactFX.StopEffect();
            Debug.Log("PunchImpactFX fermato manualmente");
        }

        if (punchImpactAudioSource != null && punchImpactAudioSource.isPlaying)
        {
            punchImpactAudioSource.Stop();
            Debug.Log("PunchImpactAudio fermato manualmente");
        }
    }

    // Metodo pubblico per fermare manualmente l'avanzamento (se necessario)
    public void StopAttackAdvance()
    {
        isAdvancing = false;
        advanceTimer = 0f;
    }

    // Metodi per impostare i parametri dell'avanzamento da altri script se necessario
    public void SetAttackAdvanceDistance(float distance)
    {
        attackAdvanceDistance = distance;
    }

    public void SetAttackAdvanceDuration(float duration)
    {
        attackAdvanceDuration = duration;
    }

    // Metodi aggiuntivi per controllo audio (opzionali)
    public void SetPunchAudioVolume(float volume)
    {
        if (punchAudioSource != null)
            punchAudioSource.volume = volume;
    }

    public void SetPunchImpactAudioVolume(float volume)
    {
        if (punchImpactAudioSource != null)
            punchImpactAudioSource.volume = volume;
    }
}