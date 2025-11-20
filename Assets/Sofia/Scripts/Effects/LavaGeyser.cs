using UnityEngine;
using CartoonFX;

/// <summary>
/// Getto di lava intermittente con 3 modalità di danno:
/// 1. DIRECT DAMAGE: Usa ThirdPersonController direttamente (più affidabile)
/// 2. HURTBOX SYSTEM: Usa il sistema HurtBox esistente (più modulare)
/// 3. INSTANT DEATH: Usa il sistema di morte da lava del player (più drammatico)
/// 
/// ✅ Il collider è sempre attivo, il danno è sincronizzato esattamente con le particelle
/// </summary>
[RequireComponent(typeof(CFXR_Effect))]
[RequireComponent(typeof(SphereCollider))]
public class LavaGeyser : MonoBehaviour
{
    [Header("Timing")]
    [SerializeField] private float initialDelay = 2f;
    [SerializeField] private float intervalBetweenBursts = 5f;
    [SerializeField] private float burstDuration = 3f;

    [Header("Damage Settings")]
    [SerializeField] private DamageMode damageMode = DamageMode.InstantDeath;
    [SerializeField] private float damage = 50f;
    [SerializeField] private float damageCooldown = 0.5f;

    [Header("Debug")]
    [SerializeField] private bool debugMode = true;

    private CFXR_Effect cfxrEffect;
    private ParticleSystem ps;
    private SphereCollider damageCollider;
    
    private bool firstBurst = true;
    private float burstTimer = 0f;
    private bool isBursting = false;
    private bool isDamageActive = false;

    private float lastDamageTime = 0f;

    public enum DamageMode
    {
        DirectDamage,
        HurtboxSystem,
        InstantDeath
    }

    private void Awake()
    {
        cfxrEffect = GetComponent<CFXR_Effect>();
        ps = GetComponent<ParticleSystem>();
        damageCollider = GetComponent<SphereCollider>();

        // ✅ COLLIDER SEMPRE ATTIVO
        damageCollider.isTrigger = true;
        damageCollider.enabled = true;

        if (cfxrEffect != null)
            cfxrEffect.clearBehavior = CFXR_Effect.ClearBehavior.None;

        if (ps != null)
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);

        if (debugMode)
            Debug.Log($"[LavaGeyser] {gameObject.name} inizializzato - Modalità: {damageMode}");
    }

    private void Start()
    {
        Invoke(nameof(StartBurst), initialDelay);
    }

    private void StartBurst()
    {
        if (debugMode)
            Debug.Log($"[LavaGeyser] 🔥 BURST INIZIATO");

        // ✅ ATTIVA PARTICELLE E DANNO SIMULTANEAMENTE
        if (cfxrEffect != null)
            cfxrEffect.ResetState();

        if (ps != null)
        {
            if (firstBurst)
            {
                ps.Play();
                firstBurst = false;
            }
            else
            {
                ps.Clear();
                ps.Play();
            }
        }

        // ✅ DANNO ATTIVO IMMEDIATAMENTE CON LE PARTICELLE
        isDamageActive = true;

        burstTimer = 0f;
        isBursting = true;

        if (debugMode)
            Debug.DrawLine(transform.position, transform.position + Vector3.up * 5f, Color.red, burstDuration);
    }

    private void Update()
    {
        if (isBursting)
        {
            burstTimer += Time.deltaTime;

            if (burstTimer >= burstDuration)
            {
                EndBurst();
            }
        }
    }

    private void EndBurst()
    {
        if (debugMode)
            Debug.Log($"[LavaGeyser] 🔵 BURST TERMINATO");

        // ✅ DISATTIVA DANNO E PARTICELLE SIMULTANEAMENTE
        isDamageActive = false;

        if (ps != null)
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);

        isBursting = false;

        // Programma prossimo burst
        Invoke(nameof(StartBurst), intervalBetweenBursts);
    }

    private void OnTriggerStay(Collider other)
    {
        // ✅ VERIFICA CHE IL DANNO SIA ATTIVO
        if (!isDamageActive)
            return;

        // Cooldown per evitare spam di danni
        if (Time.time - lastDamageTime < damageCooldown)
            return;

        // ✅ APPLICA DANNO IN BASE ALLA MODALITÀ
        bool damageApplied = false;

        switch (damageMode)
        {
            case DamageMode.DirectDamage:
                damageApplied = ApplyDirectDamage(other);
                break;

            case DamageMode.HurtboxSystem:
                damageApplied = ApplyHurtboxDamage(other);
                break;

            case DamageMode.InstantDeath:
                damageApplied = ApplyInstantDeath(other);
                break;
        }

        if (damageApplied)
        {
            lastDamageTime = Time.time;
            
            if (debugMode)
                Debug.Log($"[LavaGeyser] 💥 DANNO APPLICATO a {other.gameObject.name} ({damageMode})");
        }
    }

    // ===== MODALITÀ 1: DIRECT DAMAGE =====
    private bool ApplyDirectDamage(Collider other)
    {
        ThirdPersonController player = other.GetComponentInParent<ThirdPersonController>();
        if (player == null)
            player = other.GetComponent<ThirdPersonController>();

        if (player != null)
        {
            player.TakeDamage(damage);

            if (debugMode)
                Debug.Log($"[LavaGeyser] Direct damage: {damage} HP");

            return true;
        }

        return false;
    }

    // ===== MODALITÀ 2: HURTBOX SYSTEM =====
    private bool ApplyHurtboxDamage(Collider other)
    {
        HurtBox hurtBox = other.GetComponent<HurtBox>();
        if (hurtBox == null)
            hurtBox = other.GetComponentInParent<HurtBox>();

        if (hurtBox != null)
        {
            hurtBox.OnHit(Vector3.zero, 0f, damage);

            if (debugMode)
                Debug.Log($"[LavaGeyser] HurtBox damage: {damage} HP");

            return true;
        }

        return false;
    }

    // ===== MODALITÀ 3: INSTANT DEATH =====
    private bool ApplyInstantDeath(Collider other)
    {
        ThirdPersonController player = other.GetComponentInParent<ThirdPersonController>();
        if (player == null)
            player = other.GetComponent<ThirdPersonController>();

        if (player != null)
        {
            player.TakeDamage(9999f);

            if (debugMode)
                Debug.Log($"[LavaGeyser] ☠️ INSTANT DEATH!");

            return true;
        }

        return false;
    }

    // ✅ METODI PUBBLICI

    public void ForceBurst()
    {
        CancelInvoke();
        StartBurst();
    }

    public void StopGeyser()
    {
        CancelInvoke();
        if (isBursting)
            EndBurst();
    }

    public void SetDamageMode(DamageMode newMode)
    {
        damageMode = newMode;
        if (debugMode)
            Debug.Log($"[LavaGeyser] Modalità cambiata: {newMode}");
    }

    // ✅ GIZMOS
    private void OnDrawGizmosSelected()
    {
        if (damageCollider == null)
            damageCollider = GetComponent<SphereCollider>();

        if (damageCollider != null)
        {
            // Verde = danno OFF, Rosso = danno ON
            Gizmos.color = isDamageActive ? Color.red : Color.green;
            Gizmos.color = new Color(Gizmos.color.r, Gizmos.color.g, Gizmos.color.b, 0.3f);

            Gizmos.DrawWireSphere(transform.position, damageCollider.radius);
        }
    }

    private void OnDestroy()
    {
        CancelInvoke();
    }
}