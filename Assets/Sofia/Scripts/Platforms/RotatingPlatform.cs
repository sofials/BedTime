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
    public Vector3 rotationAxis = Vector3.up;
    public float rotationSpeed = 360f;
    private float speedMultiplier = 1f;
    private float originalRotationSpeed;

    [Header("Overlay Emission")]
    [SerializeField] private Color overlayColor = Color.red;
    [SerializeField] private float overlayIntensity = 2f; // Aumentato per più luminosità
    [Tooltip("Moltiplicatore aggiuntivo per HDR emission (valori alti = più luce)")]
    [SerializeField] private float hdrMultiplier = 3f;
    [Tooltip("Se true, mantiene anche il tint del Base Color oltre all'emission")]
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
    [Tooltip("Support Platform: il player può appoggiarsi sopra. Obstacle Platform: colpisce e respinge il player")]
    [SerializeField] private bool detachPlayerOnHit = false;
    [Tooltip("Se true, sgancia il player dalla piattaforma quando viene colpito (solo per Obstacle Platform)")]
    
    [Header("Damage Settings")]
    [SerializeField] private bool canDamagePlayer = true;
    [Tooltip("Se disabilitato, la piattaforma non farà danno al player")]
    [SerializeField] public float damageAmount = 10f;
    [Tooltip("Se true, fa solo trigger Hit senza danno quando canDamagePlayer è false")]
    [SerializeField] public bool triggerHitWhenNoDamage = false;

    // CAMBIATO: Array di MeshRenderer invece di uno singolo
    private MeshRenderer[] meshRenderers;
    private bool patinaActive = false;

    // Per salvare i colori originali dei materiali - ora per ogni renderer
    private Dictionary<MeshRenderer, Dictionary<Material, Material>> rendererMaterialInstances = new Dictionary<MeshRenderer, Dictionary<Material, Material>>();
    private Dictionary<MeshRenderer, Dictionary<Material, Color>> rendererOriginalBaseColors = new Dictionary<MeshRenderer, Dictionary<Material, Color>>();
    private Dictionary<MeshRenderer, Dictionary<Material, Color>> rendererOriginalEmissionColors = new Dictionary<MeshRenderer, Dictionary<Material, Color>>();
    
    // NUOVO: Salva i materiali originali per ogni renderer
    private Dictionary<MeshRenderer, Material[]> rendererOriginalMaterials = new Dictionary<MeshRenderer, Material[]>();
    private bool materialsInitialized = false;

    // Variabili private per il pendolo RELATIVO
    private float pendulumTimer = 0f;
    private Vector3 pendulumInitialPosition;
    private Quaternion pendulumInitialRotation;
    private Vector3 pendulumAnchorPosition;
    private float pendulumDistance;

    void Awake()
    {
        // CAMBIATO: Ottieni TUTTI i MeshRenderer (oggetto corrente + figli)
        meshRenderers = GetComponentsInChildren<MeshRenderer>();
        
        if (meshRenderers.Length == 0)
        {
            Debug.LogWarning($"[RotatingObject] Nessun MeshRenderer trovato su {gameObject.name} o sui suoi figli");
        }
        else
        {
            Debug.Log($"[RotatingObject] Trovati {meshRenderers.Length} MeshRenderer su {gameObject.name}");
            
            // Inizializza le strutture dati per ogni renderer
            foreach (MeshRenderer renderer in meshRenderers)
            {
                rendererMaterialInstances[renderer] = new Dictionary<Material, Material>();
                rendererOriginalBaseColors[renderer] = new Dictionary<Material, Color>();
                rendererOriginalEmissionColors[renderer] = new Dictionary<Material, Color>();
                
                // Salva i colori originali dei materiali SHARED per questo renderer
                foreach (Material mat in renderer.sharedMaterials)
                {
                    if (mat != null)
                    {
                        if (mat.HasProperty("_BaseColor"))
                            rendererOriginalBaseColors[renderer][mat] = mat.GetColor("_BaseColor");
                        if (mat.HasProperty("_EmissionColor"))
                            rendererOriginalEmissionColors[renderer][mat] = mat.GetColor("_EmissionColor");
                    }
                }
            }
        }

        if (slowdownEffect != null)
        {
            slowdownEffect.gameObject.SetActive(false);
        }

        if ((rotateAroundObject || usePinwheelMode) && targetObject == null && !usePendulumMode)
        {
            Debug.LogWarning($"[RotatingObject] Modalità attivata ma targetObject non assegnato su {gameObject.name}");
        }

        if (usePinwheelMode && visualToRotate == null)
        {
            Debug.LogWarning($"[RotatingObject] usePinwheelMode attivo ma nessun visualToRotate assegnato su {gameObject.name}");
        }

        // Inizializzazione pendolo
        if (usePendulumMode)
        {
            InitializePendulum();
        }
    }

    void Update()
    {
        if (usePendulumMode && pendulumAnchor != null)
        {
            UpdatePendulumMovement();
        }
        else
        {
            // Movimento rotazionale normale esistente
            float rotationThisFrame = rotationSpeed * speedMultiplier * Time.deltaTime;

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
    }

    private void InitializePendulum()
    {
        if (pendulumAnchor == null)
        {
            pendulumAnchor = transform.parent;
        }
        
        if (pendulumAnchor == null)
        {
            Debug.LogWarning($"[RotatingObject] usePendulumMode attivo ma nessun anchor trovato su {gameObject.name}");
            return;
        }
        
        // SALVA stato iniziale completo
        pendulumAnchorPosition = pendulumAnchor.position;
        pendulumInitialPosition = transform.position;
        pendulumInitialRotation = transform.rotation;
        pendulumDistance = Vector3.Distance(pendulumAnchorPosition, pendulumInitialPosition);
        
        Debug.Log($"[RotatingObject] Pendolo RELATIVO inizializzato - distanza: {pendulumDistance}, angolo max: {pendulumMaxAngle}°, asse: {rotationAxis}");
    }

    private void UpdatePendulumMovement()
    {
        // Incrementa il timer in base alla velocità e al moltiplicatore
        pendulumTimer += Time.deltaTime * pendulumSpeed * speedMultiplier;
        
        // Calcola l'angolo usando una sinusoide per oscillazione limitata (non 360°)
        float currentAngle = Mathf.Sin(pendulumTimer) * pendulumMaxAngle;
        
        // USA LO STESSO SISTEMA di RotateAround ma con angolo limitato
        // Calcola la rotazione dal centro (anchor) verso l'oggetto
        Vector3 directionFromAnchor = (pendulumInitialPosition - pendulumAnchorPosition).normalized;
        float originalDistance = Vector3.Distance(pendulumAnchorPosition, pendulumInitialPosition);
        
        // Applica la rotazione limitata attorno al punto di anchor
        Quaternion oscillationRotation = Quaternion.AngleAxis(currentAngle, rotationAxis.normalized);
        Vector3 rotatedDirection = oscillationRotation * directionFromAnchor;
        
        // Calcola la nuova posizione mantenendo la distanza originale
        transform.position = pendulumAnchorPosition + (rotatedDirection * originalDistance);
        
        // ROTAZIONE: mantieni orientamento se richiesto
        if (maintainOrientation)
        {
            // Ruota l'oggetto per seguire naturalmente l'oscillazione
            Quaternion naturalRotation = Quaternion.AngleAxis(currentAngle, rotationAxis.normalized);
            transform.rotation = pendulumInitialRotation * naturalRotation;
        }
        else
        {
            // Mantieni solo la rotazione iniziale
            transform.rotation = pendulumInitialRotation;
        }
    }

    // NUOVO: Metodi pubblici per controllare il tipo di piattaforma
    public void SetPlatformType(PlatformType type)
    {
        platformType = type;
        Debug.Log($"[RotatingObject] {gameObject.name} - tipo piattaforma: {type}");
    }

    public PlatformType GetPlatformType() => platformType;

    // NUOVO: Metodi per controllare lo sganciamento
    public void SetDetachPlayerOnHit(bool detach)
    {
        detachPlayerOnHit = detach;
        Debug.Log($"[RotatingObject] {gameObject.name} - sganciamento player: {(detach ? "ABILITATO" : "DISABILITATO")}");
    }

    public bool GetDetachPlayerOnHit() => detachPlayerOnHit;

    // NUOVO: Metodi pubblici per controllare il danno
    public void SetCanDamagePlayer(bool canDamage)
    {
        canDamagePlayer = canDamage;
        Debug.Log($"[RotatingObject] {gameObject.name} - danno al player: {(canDamage ? "ABILITATO" : "DISABILITATO")}");
    }

    public void ToggleDamage()
    {
        SetCanDamagePlayer(!canDamagePlayer);
    }

    public bool CanDamagePlayer => canDamagePlayer;

    // Metodi pubblici per controllare il pendolo
    public void SetPendulumMode(bool enabled)
    {
        usePendulumMode = enabled;
        if (enabled)
        {
            InitializePendulum();
        }
        Debug.Log($"[RotatingObject] {gameObject.name} - modalità pendolo: {(enabled ? "ATTIVATA" : "DISATTIVATA")}");
    }

    public void SetPendulumAngle(float angle)
    {
        pendulumMaxAngle = Mathf.Clamp(angle, 5f, 90f);
        Debug.Log($"[RotatingObject] {gameObject.name} - angolo pendolo: {pendulumMaxAngle}°");
    }

    public void SetPendulumSpeed(float speed)
    {
        pendulumSpeed = speed;
        Debug.Log($"[RotatingObject] {gameObject.name} - velocità pendolo: {pendulumSpeed}");
    }

    public void SetSpeedMultiplier(float multiplier)
    {
        // NOTA: Questo metodo ora è usato solo per altri scopi, 
        // SlowdownAbility modifica direttamente rotationSpeed
        
        if (useCustomSlowdown)
        {
            // Salva la velocità originale solo se non già salvata
            if (originalRotationSpeed == 0f)
            {
                originalRotationSpeed = rotationSpeed;
            }
            
            // Applica la velocità custom direttamente
            rotationSpeed = customSlowdownFactor;
            Debug.Log($"[RotatingObject] {gameObject.name} - velocità custom impostata a {rotationSpeed} (originale: {originalRotationSpeed})");
        }
        else
        {
            speedMultiplier = multiplier;
            Debug.Log($"[RotatingObject] {gameObject.name} - speed multiplier impostato a {speedMultiplier}");
        }
    }

    public void RestoreOriginalSpeed()
    {
        if (useCustomSlowdown)
        {
            if (originalRotationSpeed != 0f)
            {
                rotationSpeed = originalRotationSpeed;
                Debug.Log($"[RotatingObject] {gameObject.name} - velocità ripristinata a {rotationSpeed}");
            }
            else
            {
                Debug.LogWarning($"[RotatingObject] {gameObject.name} - originalRotationSpeed non salvata!");
            }
        }
        else
        {
            speedMultiplier = 1f;
            Debug.Log($"[RotatingObject] {gameObject.name} - speed multiplier ripristinato a 1.0");
        }
    }

    public void PlaySlowdownEffect(float duration = 1f)
    {
        if (slowdownEffect != null)
        {
            StartCoroutine(SlowdownWithFxRoutine(duration));
        }
        else
        {
            StartCoroutine(SlowdownRoutine(duration));
        }
    }

    private IEnumerator SlowdownRoutine(float duration)
    {
        SetSpeedMultiplier(1f);
        yield return new WaitForSeconds(duration);
        RestoreOriginalSpeed();
    }

    private IEnumerator SlowdownWithFxRoutine(float duration)
    {
        slowdownEffect.gameObject.SetActive(true);
        slowdownEffect.PlayEffect();

        SetSpeedMultiplier(1f);
        yield return new WaitForSeconds(duration);

        RestoreOriginalSpeed();
        slowdownEffect.StopEffect();
        slowdownEffect.gameObject.SetActive(false);
    }

    // OVERLAY EMISSIVO LUMINOSO per URP Simple Lit - ORA PER TUTTI I RENDERER
    public void SetOverlayActive(bool active)
    {
        Debug.Log($"[RotatingObject] *** SetOverlayActive({active}) chiamato su {gameObject.name} ***");
        
        if (meshRenderers == null || meshRenderers.Length == 0) 
        {
            Debug.LogWarning($"[RotatingObject] Nessun MeshRenderer disponibile su {gameObject.name}");
            return;
        }
        
        Debug.Log($"[RotatingObject] Applicando overlay a {meshRenderers.Length} MeshRenderer");
        
        // CAMBIATO: Applica l'overlay a TUTTI i renderer
        foreach (MeshRenderer renderer in meshRenderers)
        {
            SetEmissiveOverlay(renderer, active);
        }
    }

    private void SetEmissiveOverlay(MeshRenderer meshRenderer, bool active)
    {
        Debug.Log($"[RotatingObject] SetEmissiveOverlay({active}) su renderer {meshRenderer.gameObject.name}");
        
        // INIZIALIZZA i materiali originali solo la prima volta per questo renderer
        if (!rendererOriginalMaterials.ContainsKey(meshRenderer))
        {
            rendererOriginalMaterials[meshRenderer] = meshRenderer.sharedMaterials;
            Debug.Log($"[RotatingObject] Materiali originali salvati per {meshRenderer.gameObject.name}: {rendererOriginalMaterials[meshRenderer].Length}");
        }
        
        Material[] originalMaterials = rendererOriginalMaterials[meshRenderer];
        Material[] currentMaterials = meshRenderer.materials;
        bool materialsChanged = false;

        Debug.Log($"[RotatingObject] Materiali da processare: {currentMaterials.Length}");

        for (int i = 0; i < originalMaterials.Length; i++)
        {
            Material originalMat = originalMaterials[i];
            if (originalMat == null) continue;

            Debug.Log($"[RotatingObject] Processando materiale {i}: {originalMat.name}");

            Material instanceMat;

            // Crea istanza del materiale SOLO se non esiste ancora per questo renderer
            if (!rendererMaterialInstances[meshRenderer].ContainsKey(originalMat))
            {
                Material newInstance = new Material(originalMat);
                rendererMaterialInstances[meshRenderer][originalMat] = newInstance;
                currentMaterials[i] = newInstance;
                materialsChanged = true;
                instanceMat = newInstance;
                Debug.Log($"[RotatingObject] Creata PRIMA istanza per materiale {originalMat.name} su {meshRenderer.gameObject.name}");
            }
            else
            {
                // Usa l'istanza esistente
                instanceMat = rendererMaterialInstances[meshRenderer][originalMat];
                if (currentMaterials[i] != instanceMat)
                {
                    currentMaterials[i] = instanceMat;
                    materialsChanged = true;
                }
                Debug.Log($"[RotatingObject] Usando istanza ESISTENTE per materiale {originalMat.name} su {meshRenderer.gameObject.name}");
            }

            if (active)
            {
                Debug.Log($"[RotatingObject] ATTIVANDO overlay per materiale {instanceMat.name}");
                
                // EMISSION LUMINOSO (principale)
                if (instanceMat.HasProperty("_EmissionColor"))
                {
                    // Calcola colore emission HDR per massima luminosità
                    Color hdrEmission = overlayColor * overlayIntensity * hdrMultiplier;
                    instanceMat.SetColor("_EmissionColor", hdrEmission);
                    
                    // Abilita emission
                    instanceMat.EnableKeyword("_EMISSION");
                    
                    // Forza il material a essere emission-enabled
                    instanceMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                    
                    Debug.Log($"[RotatingObject] Emission attivata con colore {hdrEmission}");
                }
                else
                {
                    Debug.LogWarning($"[RotatingObject] Materiale {instanceMat.name} non ha _EmissionColor");
                }

                // BASE COLOR TINT (opzionale, per colorare anche la texture)
                if (applyColorTint && instanceMat.HasProperty("_BaseColor"))
                {
                    if (rendererOriginalBaseColors[meshRenderer].ContainsKey(originalMat))
                    {
                        Color originalColor = rendererOriginalBaseColors[meshRenderer][originalMat];
                        // Mescola il colore originale con l'overlay
                        Color tintedColor = Color.Lerp(originalColor, originalColor * overlayColor, 0.3f);
                        tintedColor.a = originalColor.a;
                        instanceMat.SetColor("_BaseColor", tintedColor);
                        Debug.Log($"[RotatingObject] BaseColor tint applicato");
                    }
                }
            }
            else
            {
                Debug.Log($"[RotatingObject] DISATTIVANDO overlay per materiale {instanceMat.name}");
                
                // Ripristina colori originali
                if (instanceMat.HasProperty("_EmissionColor") && rendererOriginalEmissionColors[meshRenderer].ContainsKey(originalMat))
                {
                    Color originalEmission = rendererOriginalEmissionColors[meshRenderer][originalMat];
                    instanceMat.SetColor("_EmissionColor", originalEmission);
                    
                    // Se l'originale non aveva emission, disabilitalo
                    if (originalEmission == Color.black || originalEmission.maxColorComponent <= 0.01f)
                    {
                        instanceMat.DisableKeyword("_EMISSION");
                        instanceMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                    }
                    
                    Debug.Log($"[RotatingObject] Emission disattivata, ripristinato colore originale {originalEmission}");
                }

                if (instanceMat.HasProperty("_BaseColor") && rendererOriginalBaseColors[meshRenderer].ContainsKey(originalMat))
                {
                    instanceMat.SetColor("_BaseColor", rendererOriginalBaseColors[meshRenderer][originalMat]);
                    Debug.Log($"[RotatingObject] BaseColor ripristinato");
                }
            }
        }

        if (materialsChanged)
        {
            meshRenderer.materials = currentMaterials;
            Debug.Log($"[RotatingObject] Materiali aggiornati nel renderer {meshRenderer.gameObject.name}");
        }
        
        patinaActive = active;
        Debug.Log($"[RotatingObject] SetEmissiveOverlay completato per {meshRenderer.gameObject.name} - patinaActive = {patinaActive}");
    }

    /// <summary>
    /// Sgancia immediatamente il player dalla piattaforma per evitare che giri insieme
    /// </summary>
    private void DetachPlayerFromPlatform(ThirdPersonController player)
    {
        // Versione semplice che usa il metodo pubblico del ThirdPersonController
        player.DetachFromPlatform(this.transform);
        Debug.Log($"[RotatingObject] Player sganciato dalla piattaforma {gameObject.name}");
    }
}