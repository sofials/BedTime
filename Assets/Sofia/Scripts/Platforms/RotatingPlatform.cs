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
    public Transform pinwheelPivot;
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
public float customSlowdownDuration = 15f;

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
        originalRotationSpeed = rotationSpeed;
        currentRotationSpeed = rotationSpeed;
        
        InitializeMaterials();
        
        if (slowdownEffect != null)
        {
            slowdownEffect.gameObject.SetActive(false);
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

    // ✅ ROTAZIONE SEMPLIFICATA
    private void UpdateRotation()
    {
        float rotationThisFrame = currentRotationSpeed * Time.deltaTime;

        if (usePinwheelMode && visualToRotate != null)
        {
            visualToRotate.Rotate(0f, 0f, rotationSpeed * Time.deltaTime, Space.Self);
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
            
            Debug.Log($"[RotatingObject] {gameObject.name} - SLOWDOWN ATTIVATO: {originalRotationSpeed} → {currentRotationSpeed}");
        }
        else
        {
            isSlowdownActive = false;
            currentRotationSpeed = originalRotationSpeed;
            Debug.Log($"[RotatingObject] {gameObject.name} - SLOWDOWN DISATTIVATO: velocità ripristinata a {currentRotationSpeed}");
        }
    }

    public void RestoreOriginalSpeed()
    {
        isSlowdownActive = false;
        currentRotationSpeed = originalRotationSpeed;
        Debug.Log($"[RotatingObject] {gameObject.name} - velocità ripristinata a {originalRotationSpeed}");
    }

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

    private void DetachPlayerFromPlatform(ThirdPersonController player)
    {
        player.DetachFromPlatform(this.transform);
        Debug.Log($"[RotatingObject] Player sganciato dalla piattaforma {gameObject.name}");
    }

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