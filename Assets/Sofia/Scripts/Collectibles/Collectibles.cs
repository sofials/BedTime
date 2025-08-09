using UnityEngine;

// Enum per i tipi di collectible
public enum CollectibleType
{
    Memory,
    Present,
    Checkpoint
}

/// <summary>
/// Classe base FLESSIBILE per tutti gli oggetti collezionabili.
/// Gestisce automaticamente la gerarchia: Mesh_Container, Effects_Container
/// AUDIO: USA SOLO AudioSource assegnati dall'Inspector (rispetta la loro configurazione)
/// NON modifica mai i parametri degli AudioSource - usa quello che è configurato nell'Inspector
/// </summary>
public class Collectibles : MonoBehaviour
{
    [Header("Collectible Settings")]
    [SerializeField] protected string collectibleName;
    [SerializeField] protected CollectibleType collectibleType;
    [SerializeField] protected bool isCollected = false;
    [SerializeField] protected int collectibleValue = 1;
    
    [Header("Hierarchy Auto-Detection")]
    [SerializeField] protected bool useHierarchyAutoDetection = true;
    [SerializeField] protected Transform meshContainer; // Riferimento a Mesh_Container
    [SerializeField] protected Transform effectsContainer; // Riferimento a Effects_Container
    
    [Header("Manual Component References (se non auto-detect)")]
    [SerializeField] protected Renderer[] meshRenderers; // Mesh da nascondere SUBITO alla raccolta
    [SerializeField] protected Collider mainCollider; // Collider principale per la raccolta
    
    [Header("Effects Management")]
    [SerializeField] protected ParticleSystem[] preCollectionEffects; // Effetti da FERMARE alla raccolta
    [SerializeField] protected ParticleSystem[] postCollectionEffects; // Effetti da AVVIARE alla raccolta
    
    [Header("Base Visual Feedback")]
    [SerializeField] protected GameObject collectEffect; // Legacy effect
    [SerializeField] protected float effectDuration = 1f;
    
    [Header("Collection Behavior Configuration")]
    [SerializeField] protected bool hideMeshImmediately = true;
    [SerializeField] protected bool disableGameObjectAfterCollection = true;
    [SerializeField] protected float delayBeforeHiding = 0f;
    
    [Header("🔊 AUDIO SYSTEM - Pure AudioSource")]
    [SerializeField] protected AudioSource preAudioSource; // AudioSource con clip + configurazione completa
    [SerializeField] protected AudioSource postAudioSource; // AudioSource con clip + configurazione completa
    [SerializeField] protected bool enablePreAudioLoop = false; // Abilita il loop del pre-audio
    
    [Header("Base UI Feedback")]
    [SerializeField] protected string displayMessage = "";
    
    [Header("Base Animation")]
    [SerializeField] protected bool enableFloating = false;
    [SerializeField] protected float floatSpeed = 2f;
    [SerializeField] protected float floatStrength = 0.3f;
    
    [Header("Rotation Animation")]
    [SerializeField] protected bool enableRotation = true;
    [SerializeField] protected float rotationSpeed = 50f;
    [SerializeField] protected Vector3 rotationAxis = Vector3.up;
    
    [Header("Base Interaction")]
    [SerializeField] protected bool canBeClickedToCollect = true;
    [SerializeField] protected bool canBeTriggerToCollect = true;
    
    // Proprietà comuni
    protected Vector3 startPosition;
    protected Quaternion startRotation;
    protected bool collectibleInitialized = false;
    protected bool preAudioPlaying = false;
    
    protected virtual void Awake()
    {
        startPosition = transform.position;
        startRotation = transform.rotation;
        
        if (string.IsNullOrEmpty(collectibleName))
        {
            collectibleName = gameObject.name;
        }
        
        if (string.IsNullOrEmpty(displayMessage))
        {
            displayMessage = $"{collectibleType} raccolto!";
        }
        
        // AUTO-DETECTION della gerarchia
        if (useHierarchyAutoDetection)
        {
            AutoDetectHierarchy();
        }
        
        ConfigureDefaultsForType();
        
        Debug.Log($"[Collectibles] Awake completato per {collectibleName} ({collectibleType})");
    }
    
    /// <summary>
    /// Auto-rileva la gerarchia standard: Mesh_Container, Effects_Container
    /// Cerca anche AudioSource se non assegnati manualmente
    /// </summary>
    protected virtual void AutoDetectHierarchy()
    {
        Debug.Log($"[Collectibles] === AUTO-DETECTION GERARCHIA per {collectibleName} ===");
        
        // Trova Mesh_Container
        meshContainer = transform.Find("Mesh_Container");
        if (meshContainer != null)
        {
            var allRenderers = meshContainer.GetComponentsInChildren<Renderer>();
            var validRenderers = System.Array.FindAll(allRenderers, r => !(r is ParticleSystemRenderer));
            meshRenderers = validRenderers;
            Debug.Log($"[Collectibles] ✅ Mesh_Container trovato: {validRenderers.Length} renderers");
        }
        else
        {
            Debug.LogWarning($"[Collectibles] ⚠️ Mesh_Container non trovato per {collectibleName}");
        }
        
        // Trova Effects_Container
        effectsContainer = transform.Find("Effects_Container");
        if (effectsContainer != null)
        {
            var allEffects = effectsContainer.GetComponentsInChildren<ParticleSystem>();
            
            var preEffects = System.Array.FindAll(allEffects, p => 
                p.name.ToLower().Contains("pre") || 
                p.name.ToLower().Contains("idle") || 
                p.name.ToLower().Contains("loop") ||
                p.name.ToLower().Contains("ambient"));
            
            var postEffects = System.Array.FindAll(allEffects, p => 
                p.name.ToLower().Contains("post") || 
                p.name.ToLower().Contains("collect") || 
                p.name.ToLower().Contains("burst") ||
                p.name.ToLower().Contains("explosion") ||
                p.name.ToLower().Contains("sparkle"));
            
            preCollectionEffects = preEffects;
            postCollectionEffects = postEffects;
            
            Debug.Log($"[Collectibles] ✅ Effects_Container trovato: {preEffects.Length} pre-effects, {postEffects.Length} post-effects");
        }
        else
        {
            Debug.LogWarning($"[Collectibles] ⚠️ Effects_Container non trovato per {collectibleName}");
        }
        
        // Auto-detection AudioSource se non assegnati manualmente
        if (preAudioSource == null || postAudioSource == null)
        {
            AutoDetectAudioSources();
        }
        
        // Auto-assegna il collider principale
        if (mainCollider == null)
        {
            mainCollider = GetComponent<Collider>();
            if (mainCollider != null)
            {
                Debug.Log($"[Collectibles] ✅ Collider principale auto-assegnato: {mainCollider.GetType().Name}");
            }
        }
    }
    
    /// <summary>
    /// Auto-rileva AudioSource nella gerarchia se non assegnati manualmente
    /// </summary>
    protected virtual void AutoDetectAudioSources()
    {
        Debug.Log($"[Collectibles] 🔍 Auto-detection AudioSource per {collectibleName}...");
        
        // Cerca Audio_Container
        Transform audioContainer = transform.Find("Audio_Container");
        if (audioContainer != null)
        {
            // Cerca AudioSource specifici per nome
            if (preAudioSource == null)
            {
                Transform preAudioTransform = audioContainer.Find("PreAudio_Source");
                if (preAudioTransform != null)
                {
                    preAudioSource = preAudioTransform.GetComponent<AudioSource>();
                    if (preAudioSource != null)
                        Debug.Log($"[Collectibles] ✅ Pre AudioSource auto-rilevato: {preAudioSource.name}");
                }
            }
            
            if (postAudioSource == null)
            {
                Transform postAudioTransform = audioContainer.Find("PostAudio_Source");
                if (postAudioTransform != null)
                {
                    postAudioSource = postAudioTransform.GetComponent<AudioSource>();
                    if (postAudioSource != null)
                        Debug.Log($"[Collectibles] ✅ Post AudioSource auto-rilevato: {postAudioSource.name}");
                }
            }
        }
        else
        {
            // Fallback: cerca AudioSource direttamente sui child
            AudioSource[] allAudioSources = GetComponentsInChildren<AudioSource>();
            if (allAudioSources.Length > 0)
            {
                if (preAudioSource == null && allAudioSources.Length >= 1)
                    preAudioSource = allAudioSources[0];
                if (postAudioSource == null && allAudioSources.Length >= 2)
                    postAudioSource = allAudioSources[1];
                    
                Debug.Log($"[Collectibles] ✅ AudioSource auto-assegnati: {allAudioSources.Length} trovati");
            }
        }
        
        if (preAudioSource == null)
            Debug.LogWarning($"[Collectibles] ⚠️ Pre AudioSource non trovato per {collectibleName} - assegnare manualmente nell'Inspector");
        if (postAudioSource == null)
            Debug.LogWarning($"[Collectibles] ⚠️ Post AudioSource non trovato per {collectibleName} - assegnare manualmente nell'Inspector");
    }
    
    protected virtual void ConfigureDefaultsForType()
    {
        switch (collectibleType)
        {
            case CollectibleType.Memory:
                hideMeshImmediately = true;
                disableGameObjectAfterCollection = true;
                delayBeforeHiding = 0f;
                enableRotation = true;
                enablePreAudioLoop = false; // Memory non ha loop
                break;
                
            case CollectibleType.Present:
                hideMeshImmediately = true;
                disableGameObjectAfterCollection = false; // GameObject resta ATTIVO per effetti
                delayBeforeHiding = 3f;
                enablePreAudioLoop = true; // ⭐ IMPORTANTE: Abilita il loop per Present
                canBeClickedToCollect = false; // Solo trigger
                enableRotation = true;
                break;
                
            case CollectibleType.Checkpoint:
                hideMeshImmediately = false;
                disableGameObjectAfterCollection = false;
                delayBeforeHiding = 0f;
                enableRotation = false;
                enablePreAudioLoop = false; // Checkpoint non ha loop
                break;
        }
        
        Debug.Log($"[Collectibles] 🔊 Default configurati per {collectibleType}: preLoop={enablePreAudioLoop}");
    }
    
    protected virtual void Start()
    {
        InitializeCollectible();
        RegisterWithSceneManager();
        
        // ⭐ SOLO aggiorna il loop sui pre-audio (se necessario) ⭐
        // Tutto il resto (volume, distanze, 3D, etc.) è configurato direttamente sugli AudioSource
        ApplyPreAudioLoopSetting();
        
        // Avvia audio e effetti
        StartPreAudio();
        StartPreCollectionEffects();
        
        Debug.Log($"[Collectibles] '{collectibleName}' inizializzato come {collectibleType}");
    }
    
    /// <summary>
    /// Applica SOLO l'impostazione del loop al pre-audio (se necessario)
    /// Tutto il resto rimane come configurato nell'Inspector dell'AudioSource
    /// </summary>
    protected virtual void ApplyPreAudioLoopSetting()
    {
        if (preAudioSource != null)
        {
            // Aggiorna SOLO il loop se necessario
            if (preAudioSource.loop != enablePreAudioLoop)
            {
                preAudioSource.loop = enablePreAudioLoop;
                Debug.Log($"[Collectibles] ✅ Pre-audio loop aggiornato a {enablePreAudioLoop} per {collectibleName}");
            }
            
            // Assicura che playOnAwake sia sempre false
            if (preAudioSource.playOnAwake)
            {
                preAudioSource.playOnAwake = false;
                Debug.Log($"[Collectibles] ✅ Pre-audio playOnAwake disabilitato per {collectibleName}");
            }
            
            Debug.Log($"[Collectibles] 🔊 Pre AudioSource '{preAudioSource.name}': " +
                      $"Volume={preAudioSource.volume}, " +
                      $"3D={preAudioSource.spatialBlend}, " +
                      $"MaxDist={preAudioSource.maxDistance}, " +
                      $"Loop={preAudioSource.loop}, " +
                      $"Clip={preAudioSource.clip?.name ?? "NULL"}");
        }
        
        if (postAudioSource != null)
        {
            // Post-audio non deve mai essere in loop
            if (postAudioSource.loop)
            {
                postAudioSource.loop = false;
                Debug.Log($"[Collectibles] ✅ Post-audio loop disabilitato per {collectibleName}");
            }
            
            // Assicura che playOnAwake sia sempre false
            if (postAudioSource.playOnAwake)
            {
                postAudioSource.playOnAwake = false;
                Debug.Log($"[Collectibles] ✅ Post-audio playOnAwake disabilitato per {collectibleName}");
            }
            
            Debug.Log($"[Collectibles] 🔊 Post AudioSource '{postAudioSource.name}': " +
                      $"Volume={postAudioSource.volume}, " +
                      $"3D={postAudioSource.spatialBlend}, " +
                      $"MaxDist={postAudioSource.maxDistance}, " +
                      $"Loop={postAudioSource.loop}, " +
                      $"Clip={postAudioSource.clip?.name ?? "NULL"}");
        }
    }
    
    protected virtual void Update()
    {
        if (!isCollected)
        {
            if (enableFloating)
            {
                FloatAnimation();
            }
            
            if (enableRotation)
            {
                RotationAnimation();
            }
        }
    }
    
    protected virtual void FloatAnimation()
    {
        float newY = startPosition.y + Mathf.Sin(Time.time * floatSpeed) * floatStrength;
        transform.position = new Vector3(startPosition.x, newY, startPosition.z);
    }
    
    protected virtual void RotationAnimation()
    {
        transform.Rotate(rotationAxis.normalized * rotationSpeed * Time.deltaTime, Space.Self);
    }
    
    protected virtual void StartPreAudio()
    {
        if (enablePreAudioLoop && preAudioSource != null && !isCollected)
        {
            if (preAudioSource.clip != null && !preAudioSource.isPlaying)
            {
                preAudioSource.time = 0f;
                preAudioSource.Play();
                preAudioPlaying = true;
                Debug.Log($"[Collectibles] 🔊 Pre-audio AVVIATO: {preAudioSource.clip.name} (Vol: {preAudioSource.volume}, Loop: {preAudioSource.loop})");
            }
            else if (preAudioSource.clip == null)
            {
                Debug.LogError($"[Collectibles] ❌ Pre AudioSource '{preAudioSource.name}' non ha clip assegnato! Assegnare il clip direttamente sull'AudioSource nell'Inspector.");
            }
        }
        else
        {
            if (!enablePreAudioLoop)
                Debug.Log($"[Collectibles] Pre-audio loop disabilitato per {collectibleName}");
            if (preAudioSource == null)
                Debug.LogWarning($"[Collectibles] ⚠️ Pre AudioSource non assegnato per {collectibleName}");
        }
    }
    
    protected virtual void StopPreAudio()
    {
        if (preAudioSource != null && preAudioPlaying)
        {
            preAudioSource.Stop();
            preAudioPlaying = false;
            Debug.Log($"[Collectibles] ✅ Pre-audio fermato per {collectibleName}");
        }
    }
    
    protected virtual void PlayPostAudio()
    {
        if (postAudioSource != null && postAudioSource.clip != null)
        {
            postAudioSource.time = 0f;
            postAudioSource.Play();
            Debug.Log($"[Collectibles] 🔊 Post-audio RIPRODOTTO: {postAudioSource.clip.name} (Vol: {postAudioSource.volume})");
        }
        else
        {
            if (postAudioSource == null)
                Debug.LogWarning($"[Collectibles] ⚠️ Post AudioSource non assegnato per {collectibleName}");
            else if (postAudioSource.clip == null)
                Debug.LogError($"[Collectibles] ❌ Post AudioSource '{postAudioSource.name}' non ha clip assegnato! Assegnare il clip direttamente sull'AudioSource nell'Inspector.");
        }
    }
    
    protected virtual void InitializeCollectible()
    {
        if (collectibleInitialized) return;
        
        collectibleInitialized = true;
        gameObject.SetActive(true);
        
        if (Vector3.Distance(transform.position, startPosition) > 0.1f)
        {
            transform.position = startPosition;
        }
        
        if (mainCollider != null && !mainCollider.isTrigger)
        {
            mainCollider.isTrigger = true;
        }
        
        Debug.Log($"[Collectibles] Inizializzazione base completata per {collectibleName}");
    }
    
    /// <summary>
    /// Ferma tutti gli effetti pre-raccolta
    /// </summary>
    protected virtual void StopPreCollectionEffects()
    {
        if (preCollectionEffects != null && preCollectionEffects.Length > 0)
        {
            foreach (var effect in preCollectionEffects)
            {
                if (effect != null && effect.isPlaying)
                {
                    effect.Stop();
                    Debug.Log($"[Collectibles] ✅ Pre-effect fermato: {effect.name}");
                }
            }
            Debug.Log($"[Collectibles] ✅ {preCollectionEffects.Length} pre-effetti fermati per {collectibleName}");
        }
    }
    
    /// <summary>
    /// Avvia tutti gli effetti post-raccolta
    /// </summary>
    protected virtual void StartPostCollectionEffects()
    {
        if (postCollectionEffects != null && postCollectionEffects.Length > 0)
        {
            foreach (var effect in postCollectionEffects)
            {
                if (effect != null)
                {
                    if (!effect.gameObject.activeInHierarchy)
                    {
                        effect.gameObject.SetActive(true);
                        Debug.Log($"[Collectibles] ✅ Attivato GameObject per effetto: {effect.name}");
                    }
                    
                    effect.Clear();
                    effect.Play();
                    Debug.Log($"[Collectibles] ✅ Post-effect avviato: {effect.name} (isPlaying: {effect.isPlaying})");
                }
            }
            Debug.Log($"[Collectibles] ✅ {postCollectionEffects.Length} post-effetti avviati per {collectibleName}");
        }
        else
        {
            Debug.LogWarning($"[Collectibles] ⚠️ Nessun post-effect configurato per {collectibleName}");
        }
    }
    
    /// <summary>
    /// Avvia gli effetti pre-raccolta (chiamato in Start)
    /// </summary>
    protected virtual void StartPreCollectionEffects()
    {
        if (preCollectionEffects != null && preCollectionEffects.Length > 0)
        {
            foreach (var effect in preCollectionEffects)
            {
                if (effect != null)
                {
                    if (!effect.isPlaying)
                    {
                        effect.Clear();
                        effect.Play();
                        Debug.Log($"[Collectibles] ✅ Pre-effect avviato: {effect.name}");
                    }
                }
            }
        }
    }
    
    protected virtual void RegisterWithSceneManager()
    {
        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        
        if (currentScene == "01 - Party in Lukelandia" && SceneManager01.Instance != null)
        {
            SceneManager01.Instance.RegisterCollectible(this);
            Debug.Log($"[Collectibles] Registrato con SceneManager01: {collectibleName}");
        }
    }
    
    // ========== INTERAZIONE BASE ==========

    protected virtual void OnTriggerEnter(Collider other)
    {
        if (canBeTriggerToCollect && other.CompareTag("Player") && !isCollected)
        {
            CollectItem();
        }
    }
    
    protected virtual void OnMouseDown()
    {
        if (canBeClickedToCollect && !isCollected)
        {
            CollectItem();
        }
    }
    
    /// <summary>
    /// Metodo principale per raccogliere l'oggetto
    /// </summary>
    public virtual void CollectItem()
    {
        if (isCollected)
        {
            Debug.LogWarning($"Collectible {collectibleName} già raccolto!");
            return;
        }
        
        Debug.Log($"[Collectibles] === INIZIO RACCOLTA {collectibleType} {collectibleName} ===");
        
        // Fase 1: Marca come raccolto
        isCollected = true;
        
        // Fase 2: Ferma audio pre-raccolta
        StopPreAudio();
        
        // Fase 3: Ferma effetti pre-raccolta
        StopPreCollectionEffects();
        
        // Fase 4: Nascondimento mesh
        if (hideMeshImmediately)
        {
            HideMeshContainer();
            Debug.Log($"[Collectibles] ✅ Mesh_Container nascosto per {collectibleName}");
        }
        
        // Fase 5: Disabilita collider
        if (mainCollider != null)
        {
            mainCollider.enabled = false;
            Debug.Log($"[Collectibles] ✅ Collider disabilitato per {collectibleName}");
        }
        
        // Fase 6: Ferma animazioni
        enableFloating = false;
        enableRotation = false;
        
        // Fase 7: Avvia effetti post-raccolta
        StartPostCollectionEffects();
        
        // Fase 8: Feedback di raccolta
        PlayCollectionFeedback();
        
        // Fase 9: Notifica manager
        NotifySceneManager();
        
        // Fase 10: Hook per classi derivate
        OnItemCollected();
        
        // Fase 11: Gestione post-raccolta
        if (disableGameObjectAfterCollection)
        {
            Debug.Log($"[Collectibles] Avvio HideAfterEffect per {collectibleName}");
            StartCoroutine(HideAfterEffect());
        }
        else if (delayBeforeHiding > 0f)
        {
            Debug.Log($"[Collectibles] Delay di {delayBeforeHiding}s per {collectibleName} - GameObject resta ATTIVO");
            StartCoroutine(HandleDelayedHiding());
        }
        else
        {
            Debug.Log($"[Collectibles] {collectibleName} completato - GameObject mantiene stato ATTIVO per effetti");
        }
        
        Debug.Log($"[Collectibles] === FINE RACCOLTA {collectibleType} {collectibleName} ===");
    }
    
    /// <summary>
    /// Nasconde SOLO il Mesh_Container, lasciando Effects_Container visibile
    /// </summary>
    protected virtual void HideMeshContainer()
    {
        if (meshContainer != null)
        {
            meshContainer.gameObject.SetActive(false);
            Debug.Log($"[Collectibles] ✅ Mesh_Container disattivato (effetti rimangono attivi)");
        }
        else if (meshRenderers != null && meshRenderers.Length > 0)
        {
            foreach (var renderer in meshRenderers)
            {
                if (renderer != null)
                {
                    renderer.enabled = false;
                }
            }
            Debug.Log($"[Collectibles] ✅ {meshRenderers.Length} mesh renderer disabilitati manualmente");
        }
    }
    
    /// <summary>
    /// Riattiva il Mesh_Container (per reset)
    /// </summary>
    protected virtual void ShowMeshContainer()
    {
        if (meshContainer != null)
        {
            meshContainer.gameObject.SetActive(true);
            Debug.Log($"[Collectibles] ✅ Mesh_Container riattivato");
        }
        else if (meshRenderers != null && meshRenderers.Length > 0)
        {
            foreach (var renderer in meshRenderers)
            {
                if (renderer != null)
                {
                    renderer.enabled = true;
                }
            }
            Debug.Log($"[Collectibles] ✅ {meshRenderers.Length} mesh renderer riabilitati manualmente");
        }
    }
    
    /// <summary>
    /// Present con delay - GameObject resta SEMPRE ATTIVO
    /// </summary>
    protected virtual System.Collections.IEnumerator HandleDelayedHiding()
    {
        Debug.Log($"[Collectibles] ⏱️ Inizio delay di {delayBeforeHiding}s per {collectibleName}");
        
        yield return new WaitForSeconds(delayBeforeHiding);
        
        Debug.Log($"[Collectibles] ✅ Delay completato per {collectibleName} - GameObject resta ATTIVO");
    }
    
    protected virtual void PlayCollectionFeedback()
    {
        Debug.Log($"[Collectibles] === FEEDBACK BASE per {collectibleName} ===");
        
        if (collectEffect != null)
        {
            GameObject effect = Instantiate(collectEffect, transform.position, Quaternion.identity);
            Destroy(effect, effectDuration);
            Debug.Log($"[Collectibles] ✅ Effetto visivo creato");
        }
        
        PlayPostAudio();
        
        if (!string.IsNullOrEmpty(displayMessage))
        {
            Debug.Log($"[Collectible] {displayMessage}");
        }
    }
    
    protected virtual void OnItemCollected()
    {
        // Implementazione base vuota - le classi derivate possono sovrascrivere
    }
    
    protected virtual void NotifySceneManager()
    {
        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        
        if (currentScene == "01 - Party in Lukelandia" && SceneManager01.Instance != null)
        {
            SceneManager01.Instance.OnCollectibleCollected(collectibleName, collectibleType);
            
            switch (collectibleType)
            {
                case CollectibleType.Memory:
                    SceneManager01.Instance.NotifyMemoryCollected(collectibleName);
                    break;
                case CollectibleType.Present:
                    SceneManager01.Instance.NotifyPresentCollected(collectibleName);
                    break;
            }
        }
        else
        {
            if (GameManager.Instance != null)
            {
                switch (collectibleType)
                {
                    case CollectibleType.Memory:
                        GameManager.Instance.OnSceneMemoryCollected(currentScene, collectibleName);
                        break;
                    case CollectibleType.Present:
                        GameManager.Instance.OnScene01PresentCollected(collectibleName);
                        break;
                }
            }
        }
        
        Debug.Log($"[Collectibles] Notifica inviata per {collectibleType} '{collectibleName}'");
    }
    
    /// <summary>
    /// Solo per Memory e altri che devono essere disattivati
    /// </summary>
    protected virtual System.Collections.IEnumerator HideAfterEffect()
    {
        float waitTime = effectDuration;
        
        if (postAudioSource != null && postAudioSource.isPlaying && postAudioSource.clip != null)
        {
            float audioLength = postAudioSource.clip.length;
            waitTime = Mathf.Max(waitTime, audioLength);
        }
        
        Debug.Log($"[Collectibles] HideAfterEffect: attendo {waitTime}s prima di disattivare {collectibleName}");
        yield return new WaitForSeconds(waitTime);
        
        Debug.Log($"[Collectibles] Disattivazione {collectibleName} dopo effetti completati");
        gameObject.SetActive(false);
    }
    
    // ========== GESTIONE AUDIO/EFFETTI ==========
    
    protected virtual void OnDestroy()
    {
        StopAllAudio();
    }
    
    protected virtual void OnDisable()
    {
        StopAllAudio();
    }
    
    protected virtual void StopAllAudio()
    {
        StopPreAudio();
        
        if (postAudioSource != null && postAudioSource.isPlaying)
        {
            postAudioSource.Stop();
        }
        
        StopAllEffects();
    }
    
    protected virtual void StopAllEffects()
    {
        if (preCollectionEffects != null)
        {
            foreach (var effect in preCollectionEffects)
            {
                if (effect != null && effect.isPlaying)
                {
                    effect.Stop();
                }
            }
        }
        
        if (postCollectionEffects != null)
        {
            foreach (var effect in postCollectionEffects)
            {
                if (effect != null && effect.isPlaying)
                {
                    effect.Stop();
                }
            }
        }
    }
    
    // ========== GETTERS PUBBLICI ==========
    
    public string GetName() => collectibleName;
    public CollectibleType GetCollectibleType() => collectibleType;
    public bool IsCollected() => isCollected;
    public string GetDisplayMessage() => displayMessage;
    public int GetCollectibleValue() => collectibleValue;
    public Vector3 GetStartPosition() => startPosition;
    public Quaternion GetStartRotation() => startRotation;
    public bool WillDisableGameObjectAfterCollection() => disableGameObjectAfterCollection;
    public bool WillHideMeshImmediately() => hideMeshImmediately;
    public float GetDelayBeforeHiding() => delayBeforeHiding;
    
    public bool IsRotationEnabled() => enableRotation;
    public float GetRotationSpeed() => rotationSpeed;
    public Vector3 GetRotationAxis() => rotationAxis;
    
    // Audio getters - Ottiene i valori DIRETTAMENTE dagli AudioSource
    public AudioSource GetPreAudioSource() => preAudioSource;
    public AudioSource GetPostAudioSource() => postAudioSource;
    public bool IsPreAudioPlaying() => preAudioPlaying;
    public AudioClip GetPreAudioClip() => preAudioSource?.clip;
    public AudioClip GetPostAudioClip() => postAudioSource?.clip;
    public float GetAudioMaxDistance() => preAudioSource?.maxDistance ?? 25f; // Usa il valore dell'AudioSource
    public float GetAudioMinDistance() => preAudioSource?.minDistance ?? 0.5f; // Usa il valore dell'AudioSource
    public bool IsPreAudioLoopEnabled() => enablePreAudioLoop;
    public float GetPreAudioVolume() => preAudioSource?.volume ?? 1f; // Usa il valore dell'AudioSource
    public float GetPostAudioVolume() => postAudioSource?.volume ?? 1f; // Usa il valore dell'AudioSource
    public bool IsUse3DAudio() => preAudioSource?.spatialBlend == 1f; // Usa il valore dell'AudioSource
    
    public Renderer[] GetMeshRenderers() => meshRenderers;
    public Collider GetMainCollider() => mainCollider;
    public ParticleSystem[] GetPreCollectionEffects() => preCollectionEffects;
    public ParticleSystem[] GetPostCollectionEffects() => postCollectionEffects;
    public Transform GetMeshContainer() => meshContainer;
    public Transform GetEffectsContainer() => effectsContainer;
    
    // ========== SETTERS PUBBLICI LIMITATI ==========
    
    public void SetAudioSources(AudioSource preSource, AudioSource postSource)
    {
        preAudioSource = preSource;
        postAudioSource = postSource;
        
        // Applica solo le impostazioni di loop
        ApplyPreAudioLoopSetting();
        
        Debug.Log($"[Collectibles] AudioSource assegnati per {collectibleName}: Pre={preSource?.name}, Post={postSource?.name}");
    }
    
    public void SetEnablePreAudioLoop(bool enableLoop)
    {
        enablePreAudioLoop = enableLoop;
        
        if (preAudioSource != null)
        {
            preAudioSource.loop = enableLoop;
        }
        
        Debug.Log($"[Collectibles] Pre Audio Loop aggiornato per {collectibleName}: {enableLoop}");
    }
    
    // ⚠️ RIMOSSI tutti i setters per volume, distanze, 3D audio - si configurano direttamente sugli AudioSource
    
    // ========== RESET E CONTROLLO ==========
    
    public virtual void ForceCollect()
    {
        if (!isCollected)
        {
            CollectItem();
        }
    }
    
    public virtual void ResetCollected()
    {
        isCollected = false;
        transform.position = startPosition;
        transform.rotation = startRotation;
        gameObject.SetActive(true);
        
        // Ripristina visibilità mesh
        ShowMeshContainer();
        
        // Ripristina collider
        if (mainCollider != null)
        {
            mainCollider.enabled = true;
        }
        
        // Ripristina animazioni
        enableFloating = (collectibleType != CollectibleType.Checkpoint);
        enableRotation = (collectibleType != CollectibleType.Checkpoint);
        
        // Reset audio
        StopAllAudio();
        StartPreAudio();
        
        // Riavvia effetti pre-raccolta
        StartPreCollectionEffects();
        
        Debug.Log($"[Collectible] {collectibleName} resetato");
    }
    
    // ========== METODI DI VALIDAZIONE ==========
    
    /// <summary>
    /// Valida che tutti i componenti siano correttamente assegnati
    /// </summary>
    public virtual bool ValidateComponents()
    {
        bool isValid = true;
        
        Debug.Log($"🔍 === VALIDAZIONE COMPONENTI per {collectibleName} ===");
        
        // Valida Mesh System
        if (meshContainer == null && (meshRenderers == null || meshRenderers.Length == 0))
        {
            Debug.LogWarning($"⚠️ Nessun sistema mesh configurato per {collectibleName}");
            isValid = false;
        }
        else
        {
            Debug.Log($"✅ Mesh System: OK");
        }
        
        // Valida Effects System
        if (preCollectionEffects == null || preCollectionEffects.Length == 0)
        {
            Debug.LogWarning($"⚠️ Nessun pre-effect configurato per {collectibleName}");
        }
        else
        {
            Debug.Log($"✅ Pre Effects: {preCollectionEffects.Length} configurati");
        }
        
        if (postCollectionEffects == null || postCollectionEffects.Length == 0)
        {
            Debug.LogWarning($"⚠️ Nessun post-effect configurato per {collectibleName}");
        }
        else
        {
            Debug.Log($"✅ Post Effects: {postCollectionEffects.Length} configurati");
        }
        
        // ⭐ VALIDAZIONE AUDIO PURA - SOLO AudioSource ⭐
        if (preAudioSource == null)
        {
            Debug.LogWarning($"⚠️ Pre AudioSource non assegnato per {collectibleName}");
            isValid = false;
        }
        else
        {
            Debug.Log($"✅ Pre AudioSource: {preAudioSource.name}");
            if (preAudioSource.clip == null)
            {
                Debug.LogError($"❌ Pre AudioSource '{preAudioSource.name}' non ha AudioClip assegnato!");
                isValid = false;
            }
            else
            {
                Debug.Log($"✅ Pre AudioClip: {preAudioSource.clip.name}");
                Debug.Log($"   - Volume: {preAudioSource.volume}");
                Debug.Log($"   - 3D: {preAudioSource.spatialBlend}");
                Debug.Log($"   - Max Distance: {preAudioSource.maxDistance}");
                Debug.Log($"   - Loop: {preAudioSource.loop}");
            }
        }
        
        if (postAudioSource == null)
        {
            Debug.LogWarning($"⚠️ Post AudioSource non assegnato per {collectibleName}");
            isValid = false;
        }
        else
        {
            Debug.Log($"✅ Post AudioSource: {postAudioSource.name}");
            if (postAudioSource.clip == null)
            {
                Debug.LogError($"❌ Post AudioSource '{postAudioSource.name}' non ha AudioClip assegnato!");
                isValid = false;
            }
            else
            {
                Debug.Log($"✅ Post AudioClip: {postAudioSource.clip.name}");
                Debug.Log($"   - Volume: {postAudioSource.volume}");
                Debug.Log($"   - 3D: {postAudioSource.spatialBlend}");
                Debug.Log($"   - Max Distance: {postAudioSource.maxDistance}");
                Debug.Log($"   - Loop: {postAudioSource.loop}");
            }
        }
        
        // Valida Collider
        if (mainCollider == null)
        {
            Debug.LogWarning($"⚠️ Main Collider non assegnato per {collectibleName}");
            isValid = false;
        }
        else
        {
            Debug.Log($"✅ Main Collider: {mainCollider.GetType().Name} (isTrigger: {mainCollider.isTrigger})");
        }
        
        Debug.Log($"🔍 Validazione completata: {(isValid ? "✅ TUTTO OK" : "⚠️ PROBLEMI TROVATI")}");
        return isValid;
    }
    
    // ========== DEBUG METHODS AGGIORNATI ==========
    
    [ContextMenu("🔍 Validate Components")]
    public void DebugValidateComponents() => ValidateComponents();
    
    [ContextMenu("🔄 Force Collect")]
    public void DebugForceCollect() => ForceCollect();
    
    [ContextMenu("↩️ Reset Collected")]
    public void DebugResetCollected() => ResetCollected();
    
    [ContextMenu("🔊 Force Play Pre Audio")]
    public void DebugForcePlayPreAudio()
    {
        if (preAudioSource != null && preAudioSource.clip != null)
        {
            preAudioSource.Stop();
            preAudioSource.time = 0f;
            preAudioSource.Play();
            preAudioPlaying = true;
            Debug.Log($"[Collectibles] 🔊 FORCE Pre-audio avviato: {preAudioSource.clip.name}");
        }
        else
        {
            Debug.LogError($"[Collectibles] ❌ Pre AudioSource o clip non disponibili per {collectibleName}");
        }
    }
    
    [ContextMenu("🔊 Force Play Post Audio")]
    public void DebugForcePlayPostAudio()
    {
        if (postAudioSource != null && postAudioSource.clip != null)
        {
            postAudioSource.Stop();
            postAudioSource.time = 0f;
            postAudioSource.Play();
            Debug.Log($"[Collectibles] 🔊 FORCE Post-audio avviato: {postAudioSource.clip.name}");
        }
        else
        {
            Debug.LogError($"[Collectibles] ❌ Post AudioSource o clip non disponibili per {collectibleName}");
        }
    }
    
    [ContextMenu("📍 Test Audio Distance")]
    public void DebugTestAudioDistance()
    {
        if (Camera.main != null && preAudioSource != null)
        {
            float distance = Vector3.Distance(transform.position, Camera.main.transform.position);
            float maxDistance = preAudioSource.maxDistance;
            bool shouldBeAudible = distance <= maxDistance;
            
            Debug.Log($"📍 === AUDIO DISTANCE TEST per {collectibleName} ===\n" +
                      $"🎯 Distanza da Camera: {distance:F2} unità\n" +
                      $"📏 Audio Max Distance (AudioSource): {maxDistance}\n" +
                      $"🔊 Audio dovrebbe essere: {(shouldBeAudible ? "✅ UDIBILE" : "❌ NON UDIBILE")}\n" +
                      $"🎵 Pre AudioSource: {(preAudioSource != null ? "✅ Assegnato" : "❌ NULL")}\n" +
                      $"🎼 Pre Audio Clip: {(preAudioSource?.clip != null ? preAudioSource.clip.name : "❌ NULL")}\n" +
                      $"▶️ Pre Audio Playing: {(preAudioSource?.isPlaying ?? false)}\n" +
                      $"🔄 Pre Audio Loop: {enablePreAudioLoop}");
        }
        else
        {
            Debug.LogWarning("❌ Camera.main non trovata o preAudioSource non assegnato!");
        }
    }
    
    [ContextMenu("📊 Debug Audio Settings")]
    public void DebugAudioSettings()
    {
        Debug.Log($"🔊 === AUDIO SETTINGS per {collectibleName} ===\n" +
                  $"Enable Pre Audio Loop: {enablePreAudioLoop}\n" +
                  $"⚠️ TUTTO IL RESTO È CONFIGURATO DIRETTAMENTE SUGLI AUDIOSOURCE ⚠️");
        
        if (preAudioSource != null)
        {
            Debug.Log($"🎵 PRE AUDIO SOURCE: {preAudioSource.name}\n" +
                      $"  - Clip: {(preAudioSource.clip != null ? preAudioSource.clip.name : "❌ NULL - Assegnare nell'Inspector!")}\n" +
                      $"  - Volume: {preAudioSource.volume} (configurato sull'AudioSource)\n" +
                      $"  - Loop: {preAudioSource.loop}\n" +
                      $"  - Is Playing: {preAudioSource.isPlaying}\n" +
                      $"  - Spatial Blend: {preAudioSource.spatialBlend} (configurato sull'AudioSource)\n" +
                      $"  - Min Distance: {preAudioSource.minDistance} (configurato sull'AudioSource)\n" +
                      $"  - Max Distance: {preAudioSource.maxDistance} (configurato sull'AudioSource)\n" +
                      $"  - Rolloff Mode: {preAudioSource.rolloffMode} (configurato sull'AudioSource)");
        }
        else
        {
            Debug.LogError("❌ PRE AUDIO SOURCE: NULL - Assegnare nell'Inspector!");
        }
        
        if (postAudioSource != null)
        {
            Debug.Log($"🎵 POST AUDIO SOURCE: {postAudioSource.name}\n" +
                      $"  - Clip: {(postAudioSource.clip != null ? postAudioSource.clip.name : "❌ NULL - Assegnare nell'Inspector!")}\n" +
                      $"  - Volume: {postAudioSource.volume} (configurato sull'AudioSource)\n" +
                      $"  - Loop: {postAudioSource.loop}\n" +
                      $"  - Is Playing: {postAudioSource.isPlaying}\n" +
                      $"  - Spatial Blend: {postAudioSource.spatialBlend} (configurato sull'AudioSource)\n" +
                      $"  - Min Distance: {postAudioSource.minDistance} (configurato sull'AudioSource)\n" +
                      $"  - Max Distance: {postAudioSource.maxDistance} (configurato sull'AudioSource)\n" +
                      $"  - Rolloff Mode: {postAudioSource.rolloffMode} (configurato sull'AudioSource)");
        }
        else
        {
            Debug.LogError("❌ POST AUDIO SOURCE: NULL - Assegnare nell'Inspector!");
        }
    }
    
    [ContextMenu("📐 Auto-Detect Hierarchy")]
    public void DebugAutoDetectHierarchy()
    {
        AutoDetectHierarchy();
        Debug.Log("Auto-detection gerarchia completata - controlla i log per i dettagli");
    }
    
    [ContextMenu("ℹ️ Debug State")]
    public void DebugState()
    {
        Debug.Log($"ℹ️ === DEBUG STATE per {collectibleName} ===\n" +
                  $"GameObject Active: {gameObject.activeInHierarchy}\n" +
                  $"Is Collected: {isCollected}\n" +
                  $"Type: {collectibleType}\n" +
                  $"Hide Mesh Immediately: {hideMeshImmediately}\n" +
                  $"Disable GameObject After Collection: {disableGameObjectAfterCollection}\n" +
                  $"Delay Before Hiding: {delayBeforeHiding}\n" +
                  $"Pre Audio Playing: {preAudioPlaying}\n" +
                  $"Enable Pre Audio Loop: {enablePreAudioLoop}\n" +
                  $"Pre AudioSource Assigned: {preAudioSource != null}\n" +
                  $"Post AudioSource Assigned: {postAudioSource != null}\n" +
                  $"Pre AudioClip Assigned: {(preAudioSource?.clip != null)}\n" +
                  $"Post AudioClip Assigned: {(postAudioSource?.clip != null)}");
    }
    
    [ContextMenu("🎯 Test Collection Behavior")]
    public void DebugTestCollection()
    {
        if (!isCollected)
        {
            Debug.Log($"🎯 === TEST RACCOLTA {collectibleName} ===");
            ForceCollect();
        }
        else
        {
            Debug.Log($"🔄 === RESET {collectibleName} ===");
            ResetCollected();
        }
    }
    
    [ContextMenu("📋 Show Pure Audio Setup Instructions")]
    public void DebugShowAudioInstructions()
    {
        Debug.Log($"📋 === CONFIGURAZIONE AUDIO PURA per {collectibleName} ===\n" +
                  $"🎯 PRINCIPIO: Configura TUTTO direttamente sugli AudioSource!\n\n" +
                  $"🔧 SETUP:\n" +
                  $"1. Crea Audio_Container sotto il prefab\n" +
                  $"2. Crea PreAudio_Source + aggiungi AudioSource component\n" +
                  $"3. Crea PostAudio_Source + aggiungi AudioSource component\n" +
                  $"4. CONFIGURA COMPLETAMENTE ogni AudioSource nell'Inspector:\n" +
                  $"   ✅ Audio Clip\n" +
                  $"   ✅ Volume (es. 0.8)\n" +
                  $"   ✅ Spatial Blend (1.0 per 3D)\n" +
                  $"   ✅ Min/Max Distance (es. 0.5/25)\n" +
                  $"   ✅ Rolloff Mode (Linear/Logarithmic)\n" +
                  $"   ✅ Loop (per pre-audio dei Present)\n" +
                  $"   ✅ Play On Awake = FALSE\n" +
                  $"5. Assegna i due AudioSource nei campi Collectibles\n" +
                  $"6. Imposta 'Enable Pre Audio Loop' = {(collectibleType == CollectibleType.Present ? "TRUE" : "FALSE")} per {collectibleType}\n\n" +
                  $"✅ VANTAGGI:\n" +
                  $"- Controllo totale su ogni AudioSource\n" +
                  $"- Configurazione visiva diretta\n" +
                  $"- Nessuna sovrascrittura automatica\n" +
                  $"- Sistema più semplice e prevedibile");
    }
    
    [ContextMenu("🔄 Toggle Pre Audio Loop")]
    public void DebugTogglePreAudioLoop()
    {
        SetEnablePreAudioLoop(!enablePreAudioLoop);
        Debug.Log($"[Collectibles] 🔄 Pre Audio Loop toggled a {enablePreAudioLoop} per {collectibleName}");
    }
}