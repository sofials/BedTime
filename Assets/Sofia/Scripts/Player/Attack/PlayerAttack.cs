using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class PlayerAttack : MonoBehaviour
{
    public GameObject attackHitbox;
    public Collider attackHitboxCollider;

    private Animator animator;
    private PlayerControls controls;
    private bool attackInput;
    private int ignoreFrames = 0;

    // NUOVO: Protezione iniziale contro input fantasma
    private float sceneStartTime;
    private const float INITIAL_PROTECTION_TIME = 0.5f; // Mezzo secondo di protezione

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

    // SISTEMA DI AUTO-TARGETING
    [Header("Auto-Targeting System")]
    [SerializeField] private bool enableAutoTargeting = true;
    [SerializeField] private float targetDetectionRange = 5f; // Raggio di rilevamento nemici
    [SerializeField] private float targetDetectionAngle = 120f; // Angolo di rilevamento (gradi)
    [SerializeField] private LayerMask enemyLayerMask = 1 << 6; // Layer dei nemici (esempio: layer 6 per Enemy)
    [SerializeField] private string[] enemyTags = {"Enemy", "EnemyHurtbox"}; // Tag dei nemici
    [SerializeField] private float rotationSpeed = 720f; // Velocità di rotazione verso il target (gradi/sec)
    [SerializeField] private bool instantRotation = false; // Rotazione istantanea vs smooth
    [SerializeField] private bool debugTargeting = false; // Debug per visualizzare targeting
    
    // Variabili per il targeting
    private Transform currentTarget;
    private bool isRotatingToTarget = false;
    private Quaternion targetRotation;
    private float rotationTimer = 0f;
    private const float MAX_ROTATION_TIME = 0.2f; // Tempo massimo per completare rotazione

    // RIFERIMENTO AL TELEPORT ABILITY
    [Header("Teleport Integration")]
    [Tooltip("Riferimento al TeleportAbility per disabilitare l'attacco durante il teletrasporto")]
    public TeleportAbility teleportAbility;

    // Cache per ottimizzazione
    private Collider[] enemyColliders = new Collider[20]; // Cache per evitare allocazioni

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
        // Registra il tempo di avvio della scena per la protezione iniziale
        sceneStartTime = Time.time;
        
        animator = GetComponentInChildren<Animator>();
        attackInput = false;
        isAttacking = false;
        attackTimer = 0f;
        hitConfirmedThisSwing = false;
        
        // Auto-trova TeleportAbility se non assegnato
        if (teleportAbility == null)
        {
            teleportAbility = GetComponent<TeleportAbility>();
            if (teleportAbility == null)
            {
                teleportAbility = GetComponentInChildren<TeleportAbility>();
            }

            if (teleportAbility != null)
            {
                Debug.Log($"TeleportAbility trovato automaticamente per PlayerAttack: {teleportAbility.name}");
            }
            else
            {
                Debug.LogWarning("TeleportAbility non trovato. L'attacco non sarà disabilitato durante il teletrasporto.");
            }
        }
        
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

        // Valida la configurazione del targeting
        ValidateTargetingSettings();
        
        // Debug iniziale della configurazione
        if (enableAutoTargeting && debugTargeting)
        {
            Debug.Log($"[PlayerAttack] Auto-targeting configurato - Range: {targetDetectionRange}, Angle: {targetDetectionAngle}, LayerMask: {enemyLayerMask.value}");
            Debug.Log($"[PlayerAttack] Enemy tags: [{string.Join(", ", enemyTags)}]");
        }
    }

    private void Update()
    {
        // Protezione iniziale contro input fantasma durante il caricamento della scena
        if (Time.time < sceneStartTime + INITIAL_PROTECTION_TIME)
        {
            attackInput = false; // Reset eventuali input bufferizzati
            return;
        }

        if (ignoreFrames > 0)
        {
            ignoreFrames--;
            return;
        }

        if (Time.timeScale == 0f)
            return;

        // Controlla se il teletrasporto è attivo e blocca l'attacco
        if (teleportAbility != null && teleportAbility.IsActive)
        {
            // Resetta l'input di attacco per evitare attacchi in coda
            attackInput = false;
            
            // Se stava già attaccando, ferma tutto
            if (isAttacking)
            {
                StopCurrentAttack();
            }
            
            return; // Esce dall'Update senza processare attacchi
        }

        // Gestisci la rotazione verso il target (solo dopo aver premuto attacco)
        HandleTargetRotation();

        if (attackInput)
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                attackInput = false;
                return;
            }

            if (Time.time >= lastAttackTime + attackCooldown)
            {
                // Trova e orienta verso il nemico più vicino SOLO quando si attacca
                if (enableAutoTargeting)
                {
                    FindAndTargetNearestEnemy();
                }

                animator.SetTrigger("Attack");

                // Controllo per evitare errore coroutine con oggetti inattivi
                if (currentEffectIcon != null && 
                    currentEffectIcon.gameObject.activeInHierarchy && 
                    currentEffectIcon.enabled)
                {
                    currentEffectIcon.PulseIcon();
                }

                isAttacking = true;
                attackTimer = attackDuration;
                lastAttackTime = Time.time;

                // Reset completo per nuovo attacco
                attackId++;
                hitConfirmedThisSwing = false;
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

    // SISTEMA DI AUTO-TARGETING
    private void FindAndTargetNearestEnemy()
    {
        if (debugTargeting)
            Debug.Log("[PlayerAttack] Cercando nemico più vicino...");
        
        Transform nearestEnemy = FindNearestEnemyInRange();
        
        if (nearestEnemy != null)
        {
            currentTarget = nearestEnemy;
            
            // Calcola la direzione verso il nemico
            Vector3 directionToEnemy = (nearestEnemy.position - transform.position).normalized;
            directionToEnemy.y = 0f; // Mantieni solo la rotazione orizzontale
            
            // Calcola la rotazione target
            targetRotation = Quaternion.LookRotation(directionToEnemy);
            
            if (debugTargeting)
            {
                Debug.Log($"[PlayerAttack] Target trovato: {nearestEnemy.name}");
                Debug.Log($"[PlayerAttack] Rotazione corrente: {transform.rotation.eulerAngles}");
                Debug.Log($"[PlayerAttack] Rotazione target: {targetRotation.eulerAngles}");
                Debug.Log($"[PlayerAttack] Instant rotation: {instantRotation}");
            }
            
            if (instantRotation)
            {
                // Rotazione istantanea
                transform.rotation = targetRotation;
                isRotatingToTarget = false;
                
                if (debugTargeting)
                    Debug.Log($"[PlayerAttack] Rotazione istantanea applicata verso: {nearestEnemy.name}");
            }
            else
            {
                // Rotazione smooth
                isRotatingToTarget = true;
                rotationTimer = 0f;
                
                if (debugTargeting)
                    Debug.Log($"[PlayerAttack] Iniziando rotazione smooth verso: {nearestEnemy.name}");
            }
        }
        else if (debugTargeting)
        {
            Debug.Log("[PlayerAttack] Nessun nemico trovato nel raggio di targeting");
        }
    }

    private Transform FindNearestEnemyInRange()
    {
        Vector3 playerPosition = transform.position;
        Vector3 playerForward = transform.forward;
        
        if (debugTargeting)
        {
            Debug.Log($"[PlayerAttack] Scansionando da posizione: {playerPosition}");
            Debug.Log($"[PlayerAttack] LayerMask: {enemyLayerMask.value}, Range: {targetDetectionRange}");
        }
        
        // Usa OverlapSphere per trovare tutti i collider nemici nel raggio
        int hitCount = Physics.OverlapSphereNonAlloc(
            playerPosition, 
            targetDetectionRange, 
            enemyColliders, 
            enemyLayerMask
        );
        
        if (debugTargeting)
            Debug.Log($"[PlayerAttack] Trovati {hitCount} collider nel raggio");
        
        Transform nearestEnemy = null;
        float nearestDistance = float.MaxValue;
        
        for (int i = 0; i < hitCount; i++)
        {
            Collider enemyCollider = enemyColliders[i];
            
            if (debugTargeting)
                Debug.Log($"[PlayerAttack] Controllando collider {i}: {enemyCollider.name}, Tag: {enemyCollider.tag}");
            
            // Verifica se ha un tag nemico
            if (!HasEnemyTag(enemyCollider.tag))
            {
                if (debugTargeting)
                    Debug.Log($"[PlayerAttack] Tag '{enemyCollider.tag}' non è un tag nemico, saltando");
                continue;
            }
            
            Vector3 enemyPosition = enemyCollider.transform.position;
            Vector3 directionToEnemy = (enemyPosition - playerPosition).normalized;
            
            // Verifica se il nemico è nell'angolo di rilevamento
            float angleToEnemy = Vector3.Angle(playerForward, directionToEnemy);
            if (angleToEnemy > targetDetectionAngle * 0.5f)
            {
                if (debugTargeting)
                    Debug.Log($"[PlayerAttack] Nemico {enemyCollider.name} fuori dall'angolo: {angleToEnemy:F1}° > {targetDetectionAngle * 0.5f:F1}°");
                continue;
            }
            
            // Calcola la distanza
            float distance = Vector3.Distance(playerPosition, enemyPosition);
            
            if (debugTargeting)
                Debug.Log($"[PlayerAttack] Nemico {enemyCollider.name} valido - distanza: {distance:F2}, angolo: {angleToEnemy:F1}°");
            
            // Controlla se è il più vicino finora
            if (distance < nearestDistance)
            {
                // Opzionale: Raycast per verificare che non ci siano ostacoli
                if (HasClearLineOfSight(playerPosition, enemyPosition))
                {
                    nearestDistance = distance;
                    nearestEnemy = enemyCollider.transform;
                    
                    if (debugTargeting)
                        Debug.Log($"[PlayerAttack] Nuovo nemico più vicino: {nearestEnemy.name} a distanza {nearestDistance:F2}");
                }
                else if (debugTargeting)
                {
                    Debug.Log($"[PlayerAttack] Nemico {enemyCollider.name} bloccato da ostacoli");
                }
            }
        }
        
        if (debugTargeting)
        {
            if (nearestEnemy != null)
                Debug.Log($"[PlayerAttack] Nemico finale selezionato: {nearestEnemy.name} a distanza {nearestDistance:F2}");
            else
                Debug.Log("[PlayerAttack] Nessun nemico valido trovato dopo tutti i controlli");
        }
        
        return nearestEnemy;
    }

    private bool HasEnemyTag(string tag)
    {
        for (int i = 0; i < enemyTags.Length; i++)
        {
            if (tag == enemyTags[i])
                return true;
        }
        return false;
    }

    private bool HasClearLineOfSight(Vector3 from, Vector3 to)
    {
        // Raggio verso il nemico per verificare ostacoli
        Vector3 direction = to - from;
        float distance = direction.magnitude;
        
        // Usa un layer mask che escluda i nemici e il player per rilevare solo ostacoli
        LayerMask obstacleLayerMask = ~(enemyLayerMask | LayerMask.GetMask("Player"));
        
        return !Physics.Raycast(from + Vector3.up * 0.5f, direction.normalized, distance - 0.5f, obstacleLayerMask);
    }

    private void HandleTargetRotation()
    {
        if (!isRotatingToTarget) return;
        
        rotationTimer += Time.deltaTime;
        
        // Calcola il progresso della rotazione
        float rotationProgress;
        
        if (rotationSpeed > 0)
        {
            // Basato sulla velocità di rotazione
            float maxRotationThisFrame = rotationSpeed * Time.deltaTime;
            float currentAngleDifference = Quaternion.Angle(transform.rotation, targetRotation);
            
            if (currentAngleDifference <= maxRotationThisFrame || rotationTimer >= MAX_ROTATION_TIME)
            {
                // Completata la rotazione
                transform.rotation = targetRotation;
                isRotatingToTarget = false;
                
                if (debugTargeting)
                    Debug.Log("[PlayerAttack] Rotazione verso target completata");
                
                return;
            }
            
            rotationProgress = maxRotationThisFrame / currentAngleDifference;
        }
        else
        {
            // Fallback basato su tempo
            rotationProgress = rotationTimer / MAX_ROTATION_TIME;
            
            if (rotationProgress >= 1f)
            {
                transform.rotation = targetRotation;
                isRotatingToTarget = false;
                return;
            }
        }
        
        // Applica la rotazione smooth
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationProgress);
    }

    private void ValidateTargetingSettings()
    {
        if (enableAutoTargeting)
        {
            if (enemyTags.Length == 0)
            {
                Debug.LogWarning("[PlayerAttack] Auto-targeting abilitato ma nessun enemyTag specificato!");
            }
            
            if (targetDetectionRange <= 0f)
            {
                Debug.LogWarning("[PlayerAttack] targetDetectionRange deve essere maggiore di 0!");
                targetDetectionRange = 5f;
            }
            
            if (targetDetectionAngle <= 0f || targetDetectionAngle > 360f)
            {
                Debug.LogWarning("[PlayerAttack] targetDetectionAngle deve essere tra 0 e 360 gradi!");
                targetDetectionAngle = 120f;
            }
        }
    }

    // Metodo per fermare l'attacco in corso
    private void StopCurrentAttack()
    {
        isAttacking = false;
        attackTimer = 0f;
        isRotatingToTarget = false;
        currentTarget = null;
        
        // Ferma effetti audio e visivi
        if (punchEffect != null && punchEffect.isPlaying)
        {
            punchEffect.Stop();
        }
        
        if (punchAudioSource != null && punchAudioSource.isPlaying)
        {
            punchAudioSource.Stop();
        }
        
        StopImpactEffects();
        
        Debug.Log("Attacco fermato a causa del teletrasporto attivo");
    }

    public void IgnoreNextClick()
    {
        ignoreFrames = 5;
    }

    // ANIMATION EVENT - Chiamato dall'animazione per attivare l'effetto generale del pugno
    public void EnablePunchEffect()
    {
        // Controlla se il teletrasporto è attivo
        if (teleportAbility != null && teleportAbility.IsActive)
        {
            Debug.Log("EnablePunchEffect ignorato: teletrasporto attivo");
            return;
        }

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
        // Controlla se il teletrasporto è attivo
        if (teleportAbility != null && teleportAbility.IsActive)
        {
            Debug.Log("RegisterSuccessfulHit ignorato: teletrasporto attivo");
            return;
        }

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

    // METODI PUBBLICI PER CONTROLLO TARGETING
    public bool IsAutoTargetingEnabled()
    {
        return enableAutoTargeting;
    }

    public void SetAutoTargeting(bool enabled)
    {
        enableAutoTargeting = enabled;
        if (!enabled)
        {
            isRotatingToTarget = false;
            currentTarget = null;
        }
    }

    public void SetTargetDetectionRange(float range)
    {
        targetDetectionRange = Mathf.Max(0f, range);
    }

    public void SetTargetDetectionAngle(float angle)
    {
        targetDetectionAngle = Mathf.Clamp(angle, 0f, 360f);
    }

    public Transform GetCurrentTarget()
    {
        return currentTarget;
    }

    // Metodo pubblico per controllare se il teletrasporto è attivo
    public bool IsTeleportActive()
    {
        return teleportAbility != null && teleportAbility.IsActive;
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

    // Debug Gizmos per visualizzare il targeting
    private void OnDrawGizmosSelected()
    {
        if (!enableAutoTargeting) return;

        // Disegna il raggio di rilevamento
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, targetDetectionRange);

        // Disegna l'angolo di rilevamento
        Vector3 forward = transform.forward;
        float halfAngle = targetDetectionAngle * 0.5f;
        
        Vector3 leftBoundary = Quaternion.AngleAxis(-halfAngle, Vector3.up) * forward * targetDetectionRange;
        Vector3 rightBoundary = Quaternion.AngleAxis(halfAngle, Vector3.up) * forward * targetDetectionRange;
        
        Gizmos.color = Color.green;
        Gizmos.DrawLine(transform.position, transform.position + leftBoundary);
        Gizmos.DrawLine(transform.position, transform.position + rightBoundary);

        // Disegna una linea verso il target corrente
        if (currentTarget != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, currentTarget.position);
            Gizmos.DrawWireSphere(currentTarget.position, 0.5f);
        }
    }
}