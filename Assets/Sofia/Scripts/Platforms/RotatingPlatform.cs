using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public enum PlatformType
{
    SupportPlatform,    // Il player può appoggiarsi sopra
    ObstaclePlatform    // Colpisce e respinge il player
}

public class RotatingObject : MonoBehaviour
{
    [Header("Rotation Settings")]
    public Vector3 rotationAxis = Vector3.up;
    public float rotationSpeed = 360f;

    // ✅ SEMPLIFICATO: Una sola variabile per la velocità
    private float currentRotationSpeed;
    private float originalRotationSpeed;
    private bool isSlowdownActive = false;

    [Header("Overlay Emission")]
    [SerializeField] private Color overlayColor = Color.red;
    [SerializeField] private float overlayIntensity = 2f;
    [SerializeField] private float hdrMultiplier = 3f;
    [SerializeField] private bool applyColorTint = true;

    [Header("Slowdown FX")]
    [SerializeField] private CFXR_EffectController slowdownEffect;

    [Header("Modalità Girandola")]
    public bool usePinwheelMode = false;
    public Transform visualToRotate;

    [Header("Rotazione attorno a oggetto")]
    public bool rotateAroundObject = false;
    public Transform targetObject;
    public bool maintainOrientation = true;

    [Header("Modalità Pendolo")]
    public bool usePendulumMode = false;
    [Tooltip("Punto di ancoraggio del pendolo (se null, usa il parent)")]
    public Transform pendulumAnchor;
    [Tooltip("Angolo massimo di oscillazione in gradi")]
    [Range(5f, 90f)]
    public float pendulumMaxAngle = 45f;
    [Tooltip("Velocità del pendolo (più basso = più lento)")]
    public float pendulumSpeed = 2f;

    [Header("Slowdown Custom Settings")]
    public bool useCustomSlowdown = false;
    [Tooltip("Velocità assoluta temporanea durante lo slowdown.")]
    public float customSlowdownFactor = 90f;

    [Header("Platform Behavior")]
    [SerializeField] private PlatformType platformType = PlatformType.SupportPlatform;
    [SerializeField] private bool detachPlayerOnHit = false;

    [Header("Damage Settings")]
    [SerializeField] private bool canDamagePlayer = true;
    [SerializeField] public float damageAmount = 10f;
    [SerializeField] public bool triggerHitWhenNoDamage = false;

    [Header("Slowdown Custom Duration")]
    [Tooltip("Durata personalizzata per lo slowdown (0 = usa durata default dell'abilità)")]
    public float customSlowdownDuration = 7f;

    [Header("🔄 NUOVA FUNZIONALITÀ: Child Colliders Mode")]
    [SerializeField] private bool useChildColliders = false;
    [Tooltip("Se attivo, l'oggetto padre usa i collider dei figli per le collisioni")]
    private List<ChildColliderHandler> childColliderHandlers = new List<ChildColliderHandler>();

    [Header("🧱 BOX COLLIDER WALL MODE")]
    [Tooltip("Se attivo, il wallCollider viene disattivato solo quando l'oggetto è rallentato")]
    [SerializeField] private bool useBoxColliderWallMode = false;
    [Tooltip("Il BoxCollider che funge da 'muro' - attivo normalmente, disattivato durante slowdown")]
    [SerializeField] private BoxCollider wallCollider;
    [Tooltip("Danno inflitto al player quando tocca il wall collider")]
    [SerializeField] private float wallDamageAmount = 10f;
    [Tooltip("Forza della spinta quando il player tocca il wall")]
    [SerializeField] private float wallPushForce = 8f;

    private WallColliderHandler wallHandler;

    // Overlay materials
    private MeshRenderer[] meshRenderers;
    private Dictionary<MeshRenderer, Material[]> rendererOriginalMaterials = new Dictionary<MeshRenderer, Material[]>();
    private Dictionary<MeshRenderer, Material[]> rendererInstanceMaterials = new Dictionary<MeshRenderer, Material[]>();
    private bool materialsInitialized = false;

    // Pendolo
    private float pendulumTimer = 0f;
    private Vector3 pendulumInitialPosition;
    private Quaternion pendulumInitialRotation;
    private Vector3 pendulumAnchorPosition;

    void Awake()
    {
        // ✅ INIZIALIZZAZIONE PULITA
        originalRotationSpeed = rotationSpeed;
        currentRotationSpeed = rotationSpeed;

        InitializeMaterials();
        InitializeChildColliders();

        if (slowdownEffect != null)
        {
            slowdownEffect.gameObject.SetActive(false);
        }

        // 🧱 BOX COLLIDER WALL MODE: assicura che il muro parta attivo e aggiungi handler per danno
        if (useBoxColliderWallMode && wallCollider != null)
        {
            wallCollider.enabled = true;

            // Aggiungi il handler per gestire le collisioni col player
            wallHandler = wallCollider.GetComponent<WallColliderHandler>();
            if (wallHandler == null)
            {
                wallHandler = wallCollider.gameObject.AddComponent<WallColliderHandler>();
            }
            wallHandler.Initialize(this, wallDamageAmount, wallPushForce);

            Debug.Log($"[RotatingObject] {gameObject.name} - Wall collider inizializzato come ATTIVO con danno {wallDamageAmount}");
        }

        if (usePendulumMode)
        {
            InitializePendulum();
        }

        Debug.Log($"[RotatingObject] {gameObject.name} inizializzato - velocità: {originalRotationSpeed}");
    }

    void Update()
    {
        if (usePendulumMode && pendulumAnchor != null)
        {
            UpdatePendulumMovement();
        }
        else
        {
            UpdateRotation();
        }
    }

    // ✅ NUOVA FUNZIONALITÀ: Inizializzazione Child Colliders
    private void InitializeChildColliders()
    {
        if (!useChildColliders) return;

        // Trova tutti i collider nei figli
        Collider[] childColliders = GetComponentsInChildren<Collider>();
        
        foreach (Collider childCollider in childColliders)
        {
            // Salta il collider dell'oggetto padre (se presente)
            if (childCollider.transform == this.transform) continue;

            // Aggiungi o ottieni il componente ChildColliderHandler
            ChildColliderHandler handler = childCollider.GetComponent<ChildColliderHandler>();
            if (handler == null)
            {
                handler = childCollider.gameObject.AddComponent<ChildColliderHandler>();
            }

            // Configura il handler per riferirsi a questo oggetto padre
            handler.SetParentRotatingObject(this);
            childColliderHandlers.Add(handler);

            Debug.Log($"[RotatingObject] Child collider configurato: {childCollider.gameObject.name} → {gameObject.name}");
        }

        // Disabilita il collider dell'oggetto padre se presente
        Collider parentCollider = GetComponent<Collider>();
        if (parentCollider != null)
        {
            parentCollider.enabled = false;
            Debug.Log($"[RotatingObject] Collider del padre disabilitato per usare child colliders");
        }
    }

    // ✅ METODO PER GESTIRE LE COLLISIONI DAI FIGLI
    public void HandleChildCollision(Collision collision, Transform childTransform)
    {
        Debug.Log($"[RotatingObject] Collisione rilevata dal figlio {childTransform.name} su padre {gameObject.name}");

        // Verifica se è il player
        ThirdPersonController player = collision.gameObject.GetComponent<ThirdPersonController>();
        if (player == null) return;

        // Gestisci la collisione come se fosse avvenuta sull'oggetto padre
        HandlePlayerCollision(player, collision, childTransform);
    }

    public void HandleChildTrigger(Collider other, Transform childTransform)
    {
        Debug.Log($"[RotatingObject] Trigger rilevato dal figlio {childTransform.name} su padre {gameObject.name}");

        // Verifica se è il player
        ThirdPersonController player = other.GetComponent<ThirdPersonController>();
        if (player == null) return;

        // Gestisci il trigger come se fosse avvenuto sull'oggetto padre
        HandlePlayerTrigger(player, childTransform);
    }

    // ✅ GESTIONE COLLISIONI UNIFICATE - MIGLIORATA PER THIRDPERSONCONTROLLER
    private void HandlePlayerCollision(ThirdPersonController player, Collision collision, Transform collisionSource = null)
    {
        string sourceName = collisionSource != null ? collisionSource.name : gameObject.name;
        Debug.Log($"[RotatingObject] Gestione collisione player da {sourceName}");

        // Gestisci in base al tipo di piattaforma
        switch (platformType)
        {
            case PlatformType.SupportPlatform:
                HandleSupportPlatformCollision(player, collision, collisionSource);
                break;
            case PlatformType.ObstaclePlatform:
                HandleObstaclePlatformCollision(player, collision, collisionSource);
                break;
        }
    }

    private void HandlePlayerTrigger(ThirdPersonController player, Transform triggerSource = null)
    {
        string sourceName = triggerSource != null ? triggerSource.name : gameObject.name;
        Debug.Log($"[RotatingObject] Gestione trigger player da {sourceName}");

        // Gestisci sempre come ostacolo per i trigger
        if (CanCauseDamage())
        {
            player.TakeDamage(damageAmount);
            player.PlayHitSound(); // ✅ NUOVO: Riproduci suono di colpo
            Debug.Log($"[RotatingObject] Player ha subito {damageAmount} danni da trigger su {sourceName}");
        }
        else if (ShouldTriggerHitWithoutDamage())
        {
            // ThirdPersonController non ha TriggerHitEffect, usiamo l'animazione Hit
            player.GetComponentInChildren<Animator>()?.SetTrigger("Hit");
            player.PlayHitSound(); // ✅ NUOVO: Riproduci suono anche senza danno
            Debug.Log($"[RotatingObject] Trigger hit senza danni su {sourceName}");
        }

        // Applica spinta usando il sistema del ThirdPersonController
        Vector3 pushDirection = (player.transform.position - (triggerSource ?? transform).position).normalized;
        player.ApplyExternalPush(pushDirection * 8f); // ✅ USA IL SISTEMA INTEGRATO
    }

    private void HandleSupportPlatformCollision(ThirdPersonController player, Collision collision, Transform collisionSource = null)
    {
        // Verifica se il player è sopra la piattaforma usando la normale di contatto
        bool isPlayerAbove = collision.contacts.Length > 0 && collision.contacts[0].normal.y < -0.5f;
        
        if (isPlayerAbove)
        {
            // Il player può stare sulla piattaforma - usa il sistema integrato del controller
            Transform platformToAttach = useChildColliders && collisionSource != null ? collisionSource : this.transform;
            player.ForceAttachToPlatform(platformToAttach);
            Debug.Log($"[RotatingObject] Player attaccato alla piattaforma {platformToAttach.name}");
        }
        else if (CanCauseDamage())
        {
            // Collisione laterale con piattaforma che può fare danno
            player.TakeDamage(damageAmount);
            player.PlayHitSound(); // ✅ NUOVO: Riproduci suono di colpo
            
            if (detachPlayerOnHit)
            {
                DetachPlayerFromPlatform(player);
            }

            // Applica spinta laterale
            Vector3 pushDirection = (player.transform.position - (collisionSource ?? transform).position).normalized;
            pushDirection.y = 0.2f; // Leggera spinta verso l'alto
            player.ApplyExternalPush(pushDirection * 6f);
        }
    }

    private void HandleObstaclePlatformCollision(ThirdPersonController player, Collision collision, Transform collisionSource = null)
    {
        // Gli ostacoli sempre respingono e danneggiano
        if (CanCauseDamage())
        {
            player.TakeDamage(damageAmount);
            player.PlayHitSound(); // ✅ NUOVO: Riproduci suono di colpo
            Debug.Log($"[RotatingObject] Player ha subito {damageAmount} danni da {gameObject.name}");
        }
        else if (ShouldTriggerHitWithoutDamage())
        {
            // Usa l'animazione Hit del controller
            player.GetComponentInChildren<Animator>()?.SetTrigger("Hit");
            player.PlayHitSound(); // ✅ NUOVO: Riproduci suono anche senza danno
        }

        if (detachPlayerOnHit)
        {
            DetachPlayerFromPlatform(player);
        }

        // ✅ SPINTA MIGLIORATA usando il sistema del ThirdPersonController
        Vector3 collisionPoint = collisionSource != null ? collisionSource.position : transform.position;
        Vector3 pushDirection = (player.transform.position - collisionPoint).normalized;
        
        // Calcola forza basata sulla velocità di rotazione
        float pushForce = Mathf.Lerp(8f, 15f, currentRotationSpeed / originalRotationSpeed);
        pushDirection.y = Mathf.Clamp(pushDirection.y + 0.3f, 0.1f, 0.8f); // Spinta verso l'alto
        
        player.ApplyExternalPush(pushDirection * pushForce);
        Debug.Log($"[RotatingObject] Applicata spinta {pushForce:F1} in direzione {pushDirection}");
    }

    // ✅ METODI LEGACY PER COMPATIBILITÀ CON COLLISIONI DIRETTE
    void OnCollisionEnter(Collision collision)
    {
        if (useChildColliders) return; // Se usa child colliders, ignora le collisioni dirette

        ThirdPersonController player = collision.gameObject.GetComponent<ThirdPersonController>();
        if (player != null)
        {
            HandlePlayerCollision(player, collision);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (useChildColliders) return; // Se usa child colliders, ignora i trigger diretti

        ThirdPersonController player = other.GetComponent<ThirdPersonController>();
        if (player != null)
        {
            HandlePlayerTrigger(player);
        }
    }

    // ✅ ROTAZIONE SEMPLIFICATA
    private void UpdateRotation()
    {
        float rotationThisFrame = currentRotationSpeed * Time.deltaTime;

        if (usePinwheelMode && visualToRotate != null)
        {
            visualToRotate.Rotate(rotationAxis.normalized, rotationThisFrame, Space.Self);
        }
        else if (rotateAroundObject && targetObject != null)
        {
            transform.RotateAround(targetObject.position, rotationAxis.normalized, rotationThisFrame);

            if (maintainOrientation)
            {
                transform.rotation = Quaternion.Euler(0, transform.rotation.eulerAngles.y, 0);
            }
        }
        else
        {
            transform.Rotate(rotationAxis.normalized, rotationThisFrame, Space.Self);
        }
    }

    // ✅ PENDOLO CORRETTO
    private void UpdatePendulumMovement()
    {
        pendulumTimer += Time.deltaTime * pendulumSpeed * (currentRotationSpeed / originalRotationSpeed);

        float currentAngle = Mathf.Sin(pendulumTimer) * pendulumMaxAngle;

        Vector3 directionFromAnchor = (pendulumInitialPosition - pendulumAnchorPosition).normalized;
        float originalDistance = Vector3.Distance(pendulumAnchorPosition, pendulumInitialPosition);

        Quaternion oscillationRotation = Quaternion.AngleAxis(currentAngle, rotationAxis.normalized);
        Vector3 rotatedDirection = oscillationRotation * directionFromAnchor;

        transform.position = pendulumAnchorPosition + (rotatedDirection * originalDistance);

        if (maintainOrientation)
        {
            Quaternion naturalRotation = Quaternion.AngleAxis(currentAngle, rotationAxis.normalized);
            transform.rotation = pendulumInitialRotation * naturalRotation;
        }
        else
        {
            transform.rotation = pendulumInitialRotation;
        }
    }

    private void InitializePendulum()
    {
        if (pendulumAnchor == null)
        {
            pendulumAnchor = transform.parent;
        }

        if (pendulumAnchor == null)
        {
            Debug.LogWarning($"[RotatingObject] Pendolo: nessun anchor trovato su {gameObject.name}");
            return;
        }

        pendulumAnchorPosition = pendulumAnchor.position;
        pendulumInitialPosition = transform.position;
        pendulumInitialRotation = transform.rotation;

        Debug.Log($"[RotatingObject] Pendolo inizializzato su {gameObject.name}");
    }

    // ✅ INIZIALIZZAZIONE MATERIALI SEMPLIFICATA
    private void InitializeMaterials()
    {
        meshRenderers = GetComponentsInChildren<MeshRenderer>();

        if (meshRenderers.Length == 0)
        {
            Debug.LogWarning($"[RotatingObject] Nessun MeshRenderer trovato su {gameObject.name}");
            return;
        }

        foreach (MeshRenderer renderer in meshRenderers)
        {
            // Salva i materiali originali
            rendererOriginalMaterials[renderer] = renderer.sharedMaterials;

            // Crea istanze per l'overlay
            Material[] instanceMaterials = new Material[renderer.materials.Length];
            for (int i = 0; i < renderer.materials.Length; i++)
            {
                if (renderer.materials[i] != null)
                {
                    instanceMaterials[i] = new Material(renderer.materials[i]);
                }
            }
            rendererInstanceMaterials[renderer] = instanceMaterials;
        }

        materialsInitialized = true;
        Debug.Log($"[RotatingObject] Materiali inizializzati per {meshRenderers.Length} renderer su {gameObject.name}");
    }

    // ✅ CONTROLLO SLOWDOWN SEMPLIFICATO
    public void SetSlowdownState(bool inSlowdown, float newSpeed = 0f)
    {
        Debug.Log($"[RotatingObject] {gameObject.name} - SetSlowdownState({inSlowdown}, {newSpeed})");

        if (inSlowdown)
        {
            isSlowdownActive = true;

            if (useCustomSlowdown)
            {
                currentRotationSpeed = customSlowdownFactor;
            }
            else if (newSpeed > 0f)
            {
                currentRotationSpeed = newSpeed;
            }
            else
            {
                currentRotationSpeed = originalRotationSpeed * 0.5f; // Fallback
            }

            // 🧱 BOX COLLIDER WALL MODE: disattiva il muro durante slowdown
            if (useBoxColliderWallMode && wallCollider != null)
            {
                wallCollider.enabled = false;
                Debug.Log($"[RotatingObject] {gameObject.name} - Wall collider DISATTIVATO (slowdown)");
            }

            Debug.Log($"[RotatingObject] {gameObject.name} - SLOWDOWN ATTIVATO: {originalRotationSpeed} → {currentRotationSpeed}");
        }
        else
        {
            isSlowdownActive = false;
            currentRotationSpeed = originalRotationSpeed;

            // 🧱 BOX COLLIDER WALL MODE: riattiva il muro quando slowdown finisce
            if (useBoxColliderWallMode && wallCollider != null)
            {
                wallCollider.enabled = true;
                Debug.Log($"[RotatingObject] {gameObject.name} - Wall collider RIATTIVATO");
            }

            Debug.Log($"[RotatingObject] {gameObject.name} - SLOWDOWN DISATTIVATO: velocità ripristinata a {currentRotationSpeed}");
        }
    }

    public void RestoreOriginalSpeed()
    {
        isSlowdownActive = false;
        currentRotationSpeed = originalRotationSpeed;

        // 🧱 BOX COLLIDER WALL MODE: riattiva il muro
        if (useBoxColliderWallMode && wallCollider != null)
        {
            wallCollider.enabled = true;
            Debug.Log($"[RotatingObject] {gameObject.name} - Wall collider RIATTIVATO (restore)");
        }

        Debug.Log($"[RotatingObject] {gameObject.name} - velocità ripristinata a {originalRotationSpeed}");
    }

    // ✅ OVERLAY SEMPLIFICATO
    public void SetOverlayActive(bool active)
    {
        if (!materialsInitialized || meshRenderers == null)
        {
            Debug.LogWarning($"[RotatingObject] Materiali non inizializzati su {gameObject.name}");
            return;
        }

        Debug.Log($"[RotatingObject] SetOverlayActive({active}) su {gameObject.name}");

        foreach (MeshRenderer renderer in meshRenderers)
        {
            ApplyOverlayToRenderer(renderer, active);
        }
    }

    private void ApplyOverlayToRenderer(MeshRenderer renderer, bool active)
    {
        if (!rendererInstanceMaterials.ContainsKey(renderer) || !rendererOriginalMaterials.ContainsKey(renderer))
        {
            Debug.LogWarning($"[RotatingObject] Materiali non trovati per renderer {renderer.gameObject.name}");
            return;
        }

        Material[] materialsToUse;

        if (active)
        {
            // Usa le istanze e applica l'overlay
            materialsToUse = rendererInstanceMaterials[renderer];

            for (int i = 0; i < materialsToUse.Length; i++)
            {
                if (materialsToUse[i] != null)
                {
                    ApplyEmissionOverlay(materialsToUse[i], true);
                }
            }
        }
        else
        {
            // Ripristina i materiali originali
            materialsToUse = rendererOriginalMaterials[renderer];
        }

        renderer.materials = materialsToUse;
    }

    private void ApplyEmissionOverlay(Material material, bool active)
    {
        if (!material.HasProperty("_EmissionColor")) return;

        if (active)
        {
            Color hdrEmission = overlayColor * overlayIntensity * hdrMultiplier;
            material.SetColor("_EmissionColor", hdrEmission);
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

            if (applyColorTint && material.HasProperty("_BaseColor"))
            {
                Color baseColor = material.GetColor("_BaseColor");
                Color tintedColor = Color.Lerp(baseColor, baseColor * overlayColor, 0.3f);
                tintedColor.a = baseColor.a;
                material.SetColor("_BaseColor", tintedColor);
            }
        }
        else
        {
            material.SetColor("_EmissionColor", Color.black);
            material.DisableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        }
    }

    // ✅ EFFETTI AUDIO
    public void PlaySlowdownEffect(float duration = 1f)
    {
        if (slowdownEffect != null)
        {
            StartCoroutine(SlowdownWithFxRoutine(duration));
        }
    }

    private IEnumerator SlowdownWithFxRoutine(float duration)
    {
        if (slowdownEffect != null)
        {
            slowdownEffect.gameObject.SetActive(true);
            slowdownEffect.PlayEffect();
        }

        yield return new WaitForSeconds(duration);

        if (slowdownEffect != null)
        {
            slowdownEffect.StopEffect();
            slowdownEffect.gameObject.SetActive(false);
        }
    }

    // ✅ PROPRIETÀ PUBBLICHE
    public bool IsInSlowdown => isSlowdownActive;
    public PlatformType GetPlatformType() => platformType;
    public bool GetDetachPlayerOnHit() => detachPlayerOnHit;
    public bool CanDamagePlayer => canDamagePlayer && !isSlowdownActive;

    public bool CanCauseDamage()
    {
        bool canDamage = canDamagePlayer && !isSlowdownActive;
        Debug.Log($"[RotatingObject] {gameObject.name} - CanCauseDamage: {canDamage}");
        return canDamage;
    }

    public bool ShouldTriggerHitWithoutDamage()
    {
        bool shouldTrigger = triggerHitWhenNoDamage && !CanCauseDamage();
        Debug.Log($"[RotatingObject] {gameObject.name} - ShouldTriggerHitWithoutDamage: {shouldTrigger}");
        return shouldTrigger;
    }

    // ✅ SETTER PUBBLICI
    public void SetPlatformType(PlatformType type)
    {
        platformType = type;
        Debug.Log($"[RotatingObject] {gameObject.name} - tipo piattaforma: {type}");
    }

    public void SetDetachPlayerOnHit(bool detach)
    {
        detachPlayerOnHit = detach;
    }

    public void SetCanDamagePlayer(bool canDamage)
    {
        canDamagePlayer = canDamage;
    }

    public void SetPendulumMode(bool enabled)
    {
        usePendulumMode = enabled;
        if (enabled)
        {
            InitializePendulum();
        }
    }

    public void SetPendulumAngle(float angle)
    {
        pendulumMaxAngle = Mathf.Clamp(angle, 5f, 90f);
    }

    public void SetPendulumSpeed(float speed)
    {
        pendulumSpeed = speed;
    }

    // 🧱 SETTER PER BOX COLLIDER WALL MODE
    public void SetBoxColliderWallMode(bool enabled, BoxCollider collider = null)
    {
        useBoxColliderWallMode = enabled;

        if (collider != null)
        {
            wallCollider = collider;
        }

        if (useBoxColliderWallMode && wallCollider != null)
        {
            // Se siamo in slowdown, il muro deve essere disattivato
            wallCollider.enabled = !isSlowdownActive;
            Debug.Log($"[RotatingObject] {gameObject.name} - Box Collider Wall Mode: {enabled}, collider: {(wallCollider.enabled ? "ATTIVO" : "DISATTIVATO")}");
        }
    }

    // ✅ NUOVI METODI PER CHILD COLLIDERS
    public void SetUseChildColliders(bool enabled)
    {
        useChildColliders = enabled;
        
        if (enabled)
        {
            InitializeChildColliders();
        }
        else
        {
            // Riabilita il collider del padre se presente
            Collider parentCollider = GetComponent<Collider>();
            if (parentCollider != null)
            {
                parentCollider.enabled = true;
            }

            // Rimuovi i handler dai figli
            foreach (ChildColliderHandler handler in childColliderHandlers)
            {
                if (handler != null)
                {
                    DestroyImmediate(handler);
                }
            }
            childColliderHandlers.Clear();
        }
    }

    private void DetachPlayerFromPlatform(ThirdPersonController player)
    {
        player.DetachFromPlatform(this.transform);
        Debug.Log($"[RotatingObject] Player sganciato dalla piattaforma {gameObject.name}");
    }

    // ✅ METODI LEGACY PER COMPATIBILITÀ (deprecati)
    [System.Obsolete("Usa SetSlowdownState invece")]
    public void SetSpeedMultiplier(float multiplier)
    {
        if (useCustomSlowdown)
        {
            SetSlowdownState(true, customSlowdownFactor);
        }
        else
        {
            SetSlowdownState(multiplier < 1f, originalRotationSpeed * multiplier);
        }
    }
}

// ✅ NUOVO COMPONENTE: ChildColliderHandler
[System.Serializable]
public class ChildColliderHandler : MonoBehaviour
{
    private RotatingObject parentRotatingObject;

    public void SetParentRotatingObject(RotatingObject parent)
    {
        parentRotatingObject = parent;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (parentRotatingObject != null)
        {
            parentRotatingObject.HandleChildCollision(collision, this.transform);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (parentRotatingObject != null)
        {
            parentRotatingObject.HandleChildTrigger(other, this.transform);
        }
    }

    void OnDestroy()
    {
        // Cleanup quando il componente viene distrutto
        parentRotatingObject = null;
    }
}

// 🧱 COMPONENTE: WallColliderHandler - Gestisce danno quando il player tocca il wall collider
public class WallColliderHandler : MonoBehaviour
{
    private RotatingObject parentRotatingObject;
    private float damageAmount;
    private float pushForce;

    public void Initialize(RotatingObject parent, float damage, float push)
    {
        parentRotatingObject = parent;
        damageAmount = damage;
        pushForce = push;
        Debug.Log($"[WallColliderHandler] Inizializzato su {gameObject.name} - Danno: {damage}, Spinta: {push}");
    }

    void OnCollisionEnter(Collision collision)
    {
        HandleCollision(collision.gameObject, collision.contacts[0].point);
    }

    void OnTriggerEnter(Collider other)
    {
        HandleCollision(other.gameObject, other.ClosestPoint(transform.position));
    }

    private void HandleCollision(GameObject obj, Vector3 contactPoint)
    {
        ThirdPersonController player = obj.GetComponent<ThirdPersonController>();
        if (player == null) return;

        // Infliggi danno
        if (damageAmount > 0)
        {
            player.TakeDamage(damageAmount);
            player.PlayHitSound();
            Debug.Log($"[WallColliderHandler] Player ha subito {damageAmount} danni dal wall {gameObject.name}");
        }

        // Applica spinta
        if (pushForce > 0)
        {
            Vector3 pushDirection = (player.transform.position - contactPoint).normalized;
            pushDirection.y = Mathf.Max(pushDirection.y, 0.2f); // Leggera spinta verso l'alto
            player.ApplyExternalPush(pushDirection * pushForce);
            Debug.Log($"[WallColliderHandler] Applicata spinta {pushForce} al player");
        }
    }

    void OnDestroy()
    {
        parentRotatingObject = null;
    }
}