using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Classe per i Present che eredita da Collectibles.
/// Usa il sistema audio unificato della classe base per evitare conflitti.
/// </summary>
public class Presents : Collectibles
{
    [Header("Present Specific Animation")]
    [SerializeField] private bool enableRotation = true;
    [SerializeField] private float rotationSpeed = 45f;
    
    [Header("Present Particle Effects")]
    [SerializeField] private ParticleSystem preCollectionEffect; // Effetto pre-raccolta (sempre attivo)
    [SerializeField] private ParticleSystem postCollectionEffect1; // Primo effetto post-raccolta
    [SerializeField] private ParticleSystem postCollectionEffect2; // Secondo effetto post-raccolta
    [SerializeField] private float postEffectDuration = 3f; // Durata effetti post-raccolta
    
    [Header("Legacy Effects (Fallback)")]
    [SerializeField] private GameObject presentCollectionParticles; // Legacy - GameObject con particelle
    [SerializeField] private AudioClip presentJingleSound; // Legacy - suono jingle (ora usa il sistema base)
    
    [Header("Present Events")]
    public UnityEvent<Presents> OnPresentCollected;
    
    [Header("Present Components")]
    [SerializeField] private Transform visualContainer; // Container per le mesh/LOD
    [SerializeField] private Transform effectsContainer; // Container per effetti
    [SerializeField] private Collider presentCollider; // Reference al collider
    [SerializeField] private LODGroup lodGroup; // Reference al LOD Group
    
    // Cache dei renderer per gestione visibilità
    private Renderer[] childRenderers;
    
    protected override void Awake()
    {
        // Imposta il tipo come Present prima di chiamare il base Awake
        collectibleType = CollectibleType.Present;
        
        // Imposta default specifici per i Present
        if (collectibleValue == 1) // Se è ancora il valore di default
        {
            collectibleValue = 10; // I Present valgono di più di default
        }
        
        // Abilita floating per default nei Present
        enableFloating = true;
        floatSpeed = 2f;
        floatStrength = 0.3f;
        
        // CONFIGURA AUDIO TRAMITE SISTEMA BASE (evita conflitti!)
        enableBackgroundLoop = true; // Abilita il loop di background per i Present
        use3DAudio = true; // Audio 3D per i Present
        
        // Se hai audio legacy, mappali al sistema base
        if (presentJingleSound != null && collectSound == null)
        {
            collectSound = presentJingleSound; // Usa il jingle come suono di raccolta
        }
        
        // Chiama il base Awake che configurerà il sistema audio
        base.Awake();
        
        // Setup specifico dei Present
        CacheChildComponents();
        
        Debug.Log($"[Presents] Present Awake completato per {collectibleName}");
    }
    
    protected override void Start()
    {
        base.Start();
        
        // Avvia effetti pre-raccolta specifici dei Present
        StartPreCollectionEffects();
        
        Debug.Log($"[Presents] Present '{collectibleName}' inizializzato");
    }
    
    protected override void Update()
    {
        base.Update();
        
        if (!isCollected)
        {
            // Animazione rotazione - applica al visual container se presente, altrimenti al parent
            if (enableRotation)
            {
                Transform targetTransform = visualContainer != null ? visualContainer : transform;
                targetTransform.Rotate(Vector3.up * rotationSpeed * Time.deltaTime);
            }
        }
    }
    
    private void CacheChildComponents()
    {
        // Auto-trova i componenti se non assegnati manualmente
        if (presentCollider == null)
        {
            presentCollider = GetComponent<Collider>();
            if (presentCollider == null)
            {
                presentCollider = GetComponentInChildren<Collider>();
            }
        }
        
        if (lodGroup == null)
        {
            lodGroup = GetComponent<LODGroup>();
            if (lodGroup == null)
            {
                lodGroup = GetComponentInChildren<LODGroup>();
            }
        }
        
        // Auto-trova i container
        if (visualContainer == null)
        {
            Transform found = transform.Find("Visual_Container");
            if (found == null) found = transform.Find("Visuals");
            if (found == null) found = transform.Find("Mesh");
            visualContainer = found;
        }
        
        if (effectsContainer == null)
        {
            Transform found = transform.Find("Effects_Container");
            if (found == null) found = transform.Find("Effects");
            effectsContainer = found;
        }
        
        // Cache tutti i renderer per gestione visibilità (escludendo i ParticleSystemRenderer)
        Renderer[] allRenderers = GetComponentsInChildren<Renderer>(true);
        System.Collections.Generic.List<Renderer> validRenderers = new System.Collections.Generic.List<Renderer>();
        
        foreach (var renderer in allRenderers)
        {
            // Esclude i ParticleSystemRenderer dalla cache per evitare di disabilitarli involontariamente
            if (!(renderer is ParticleSystemRenderer))
            {
                validRenderers.Add(renderer);
            }
        }
        
        childRenderers = validRenderers.ToArray();
        
        Debug.Log($"[Presents] Componenti cachati: Collider={presentCollider != null}, LOD={lodGroup != null}, " +
                  $"Renderers validi={childRenderers.Length}/{allRenderers.Length} (esclusi ParticleSystemRenderer)");
    }
    
    private void StartPreCollectionEffects()
    {
        Debug.Log($"[Presents] === AVVIO EFFETTI PRE-RACCOLTA PRESENT per {collectibleName} ===");
        
        // Avvia effetto particellare pre-raccolta
        if (preCollectionEffect != null)
        {
            if (preCollectionEffect.gameObject.activeInHierarchy)
            {
                if (!preCollectionEffect.isPlaying)
                {
                    preCollectionEffect.Clear();
                    preCollectionEffect.Play();
                    Debug.Log($"[Presents] ✅ Effetto pre-raccolta AVVIATO per {collectibleName}");
                }
                else
                {
                    Debug.Log($"[Presents] ⚠️ Effetto pre-raccolta già in riproduzione per {collectibleName}");
                }
            }
            else
            {
                Debug.LogError($"[Presents] ❌ GameObject dell'effetto pre-raccolta NON ATTIVO per {collectibleName}! " +
                              $"Path: {GetParticleSystemPath(preCollectionEffect)}");
            }
        }
        else
        {
            Debug.LogWarning($"[Presents] ❌ Effetto pre-raccolta NON ASSEGNATO per {collectibleName}");
        }
        
        // L'audio di background è ora gestito automaticamente dalla classe base!
        Debug.Log($"[Presents] Audio di background gestito dalla classe base Collectibles");
        
        Debug.Log($"[Presents] === FINE AVVIO EFFETTI PRE-RACCOLTA PRESENT ===");
    }
    
    private void StopPreCollectionEffects()
    {
        Debug.Log($"[Presents] === STOP EFFETTI PRE-RACCOLTA PRESENT per {collectibleName} ===");
        
        if (preCollectionEffect != null)
        {
            if (preCollectionEffect.isPlaying)
            {
                preCollectionEffect.Stop();
                Debug.Log($"[Presents] ✅ Effetto pre-raccolta FERMATO per {collectibleName}");
            }
        }
        
        // L'audio di background è ora gestito automaticamente dalla classe base!
        Debug.Log($"[Presents] Audio di background fermato dalla classe base Collectibles");
    }
    
    // Helper per debug path dei ParticleSystem
    private string GetParticleSystemPath(ParticleSystem ps)
    {
        if (ps == null) return "NULL";
        
        string path = ps.name;
        Transform current = ps.transform.parent;
        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }
        return path;
    }
    
    // ========== OVERRIDE METODI BASE ==========
    
    /// <summary>
    /// Override del metodo virtuale chiamato quando l'item viene raccolto
    /// </summary>
    protected override void OnItemCollected()
    {
        // Evento specifico Present
        OnPresentCollected?.Invoke(this);
    }
    
    /// <summary>
    /// Override del feedback di raccolta per aggiungere effetti specifici dei Present
    /// </summary>
    protected override void PlayCollectionFeedback()
    {
        Debug.Log($"[Presents] === FEEDBACK PRESENT SPECIFICO per {collectibleName} ===");
        
        // Ferma effetti pre-raccolta specifici dei Present
        StopPreCollectionEffects();
        
        // Posizione per gli effetti - preferisce il centro visuale
        Vector3 effectPosition = visualContainer != null ? visualContainer.position : transform.position;
        
        // ========== NUOVI EFFETTI PARTICELLARI PRESENT ==========
        
        // Avvia primo effetto post-raccolta
        if (postCollectionEffect1 != null)
        {
            if (postCollectionEffect1.gameObject.activeInHierarchy)
            {
                postCollectionEffect1.Clear();
                postCollectionEffect1.Play();
                
                if (postCollectionEffect1.isPlaying)
                {
                    Debug.Log($"[Presents] ✅ Primo effetto post-raccolta CONFERMATO ATTIVO per {collectibleName}");
                }
                else
                {
                    Debug.LogError($"[Presents] ❌ Primo effetto post-raccolta NON SI AVVIA per {collectibleName}!");
                }
                
                StartCoroutine(StopParticleEffectAfterDelay(postCollectionEffect1, postEffectDuration, "Primo"));
            }
            else
            {
                Debug.LogError($"[Presents] ❌ GameObject del primo effetto NON ATTIVO! Path: {GetParticleSystemPath(postCollectionEffect1)}");
            }
        }
        else
        {
            Debug.LogWarning($"[Presents] ❌ Primo effetto post-raccolta NON ASSEGNATO per {collectibleName}");
        }
        
        // Avvia secondo effetto post-raccolta
        if (postCollectionEffect2 != null)
        {
            if (postCollectionEffect2.gameObject.activeInHierarchy)
            {
                postCollectionEffect2.Clear();
                postCollectionEffect2.Play();
                
                if (postCollectionEffect2.isPlaying)
                {
                    Debug.Log($"[Presents] ✅ Secondo effetto post-raccolta CONFERMATO ATTIVO per {collectibleName}");
                }
                else
                {
                    Debug.LogError($"[Presents] ❌ Secondo effetto post-raccolta NON SI AVVIA per {collectibleName}!");
                }
                
                StartCoroutine(StopParticleEffectAfterDelay(postCollectionEffect2, postEffectDuration, "Secondo"));
            }
            else
            {
                Debug.LogError($"[Presents] ❌ GameObject del secondo effetto NON ATTIVO! Path: {GetParticleSystemPath(postCollectionEffect2)}");
            }
        }
        else
        {
            Debug.LogWarning($"[Presents] ❌ Secondo effetto post-raccolta NON ASSEGNATO per {collectibleName}");
        }
        
        // ========== EFFETTI LEGACY COME FALLBACK ==========
        
        bool needsLegacyEffects = (postCollectionEffect1 == null && postCollectionEffect2 == null);
        
        if (needsLegacyEffects)
        {
            Debug.Log($"[Presents] Usando effetti LEGACY per {collectibleName}");
            
            if (presentCollectionParticles != null)
            {
                GameObject particles = Instantiate(presentCollectionParticles, effectPosition, Quaternion.identity);
                Destroy(particles, 3f);
                Debug.Log($"[Presents] ✅ Effetto legacy particelle creato");
            }
        }
        
        // ========== CHIAMA IL FEEDBACK BASE ==========
        // Questo gestirà automaticamente:
        // - L'effetto visivo base (se presente)
        // - L'audio di raccolta tramite il sistema unificato
        // - Il messaggio UI
        base.PlayCollectionFeedback();
        
        Debug.Log($"[Presents] === FINE FEEDBACK PRESENT SPECIFICO ===");
    }
    
    /// <summary>
    /// Override del metodo HideAfterEffect per gestire meglio i Present
    /// </summary>
    protected override System.Collections.IEnumerator HideAfterEffect()
    {
        // Disabilita IMMEDIATAMENTE solo i renderer per feedback visivo immediato
        SetChildVisibility(false);
        
        // Disabilita anche il collider per evitare multiple collezioni
        if (presentCollider != null)
        {
            presentCollider.enabled = false;
        }
        
        Debug.Log($"[Presents] Renderer disabilitati per {collectibleName}, oggetto rimane attivo per effetti");
        
        // Calcola tempo massimo di attesa
        float maxEffectTime = Mathf.Max(effectDuration * 0.5f, postEffectDuration);
        
        // Considera anche l'audio di raccolta gestito dalla classe base
        if (GetCollectionAudioSource() != null && GetCollectionAudioSource().isPlaying && GetCollectionAudioSource().clip != null)
        {
            float audioLength = GetCollectionAudioSource().clip.length;
            maxEffectTime = Mathf.Max(maxEffectTime, audioLength);
        }
        
        Debug.Log($"[Presents] Aspettando {maxEffectTime} secondi per completare tutti gli effetti");
        
        yield return new WaitForSeconds(maxEffectTime + 0.5f); // +0.5f di buffer
        
        // Ora disattiva completamente l'oggetto
        gameObject.SetActive(false);
        
        Debug.Log($"[Presents] Oggetto {collectibleName} completamente disabilitato dopo effetti");
    }
    
    private System.Collections.IEnumerator StopParticleEffectAfterDelay(ParticleSystem particleSystem, float delay, string effectName = "Unknown")
    {
        Debug.Log($"[Presents] Aspettando {delay} secondi prima di fermare {effectName} effetto per {collectibleName}");
        
        yield return new WaitForSeconds(delay);
        
        if (particleSystem != null && particleSystem.isPlaying)
        {
            particleSystem.Stop();
            Debug.Log($"[Presents] {effectName} effetto fermato per {collectibleName}");
        }
    }
    
    private void SetChildVisibility(bool visible)
    {
        if (childRenderers != null)
        {
            foreach (var renderer in childRenderers)
            {
                if (renderer != null)
                {
                    renderer.enabled = visible;
                }
            }
        }
        
        if (lodGroup != null)
        {
            lodGroup.enabled = visible;
        }
        
        Debug.Log($"[Presents] Visibilità renderer impostata a {visible} per {collectibleName}");
    }
    
    // ========== GESTIONE EFFETTI ON DESTROY/DISABLE ==========
    
    protected override void OnDestroy()
    {
        StopAllPresentEffects();
        base.OnDestroy(); // Chiama anche il metodo della classe base
    }
    
    protected override void OnDisable()
    {
        StopAllPresentEffects();
        base.OnDisable(); // Chiama anche il metodo della classe base
    }
    
    private void StopAllPresentEffects()
    {
        StopPreCollectionEffects();
        
        if (postCollectionEffect1 != null && postCollectionEffect1.isPlaying)
        {
            postCollectionEffect1.Stop();
        }
        
        if (postCollectionEffect2 != null && postCollectionEffect2.isPlaying)
        {
            postCollectionEffect2.Stop();
        }
        
        // L'audio è ora gestito automaticamente dalla classe base
        Debug.Log($"[Presents] Effetti particellari Present fermati per {collectibleName}");
    }
    
    // ========== OVERRIDE RESET ==========
    
    public override void ResetCollected()
    {
        base.ResetCollected(); // Questo resetterà anche l'audio automaticamente
        
        // Reset specifico dei Present
        SetChildVisibility(true);
        
        if (presentCollider != null)
        {
            presentCollider.enabled = true;
        }
        
        // Reset rotazione del visual container se presente
        if (visualContainer != null)
        {
            visualContainer.rotation = Quaternion.identity;
        }
        
        // Ferma tutti gli effetti Present prima di riavviare quelli pre-raccolta
        StopAllPresentEffects();
        
        // Riavvia effetti pre-raccolta Present
        StartPreCollectionEffects();
        
        Debug.Log($"[Presents] Present {collectibleName} resetato completamente");
    }
    
    // ========== GETTERS E SETTERS SPECIFICI ==========
    
    public Collider GetCollider() => presentCollider;
    public LODGroup GetLODGroup() => lodGroup;
    public Transform GetVisualContainer() => visualContainer;
    public Transform GetEffectsContainer() => effectsContainer;
    
    public void SetRotationSpeed(float speed)
    {
        rotationSpeed = speed;
    }
    
    public void SetAnimationEnabled(bool rotation, bool floating)
    {
        enableRotation = rotation;
        SetFloatingEnabled(floating);
    }
    
    /// <summary>
    /// Usa il sistema audio unificato della classe base
    /// </summary>
    public void SetPresentAudioVolumes(float backgroundVol, float collectionVol)
    {
        SetAudioVolumes(backgroundVol, collectionVol);
    }
    
    public void SetPostEffectDuration(float duration)
    {
        postEffectDuration = duration;
    }
    
    // ========== CONFIGURAZIONI PRESET ==========
    
    public void ConfigureAsChristmasPresent()
    {
        SetCollectibleName("Christmas Gift");
        SetCollectibleValue(10);
        SetRotationSpeed(30f);
        SetFloatSettings(1.5f, 0.25f);
        SetAudioVolumes(0.2f, 0.6f);
        SetDisplayMessage("Regalo di Natale!");
        Debug.Log("[Presents] Configurato come regalo di Natale");
    }
    
    public void ConfigureAsBirthdayPresent()
    {
        SetCollectibleName("Birthday Gift");
        SetCollectibleValue(15);
        SetRotationSpeed(45f);
        SetFloatSettings(2.5f, 0.4f);
        SetAudioVolumes(0.3f, 0.7f);
        SetDisplayMessage("Regalo di compleanno!");
        Debug.Log("[Presents] Configurato come regalo di compleanno");
    }
    
    public void ConfigureAsSpecialPresent()
    {
        SetCollectibleName("Special Gift");
        SetCollectibleValue(25);
        SetRotationSpeed(60f);
        SetFloatSettings(3f, 0.5f);
        SetAudioVolumes(0.4f, 0.8f);
        SetDisplayMessage("Regalo speciale!");
        Debug.Log("[Presents] Configurato come regalo speciale");
    }
    
    // ========== LOD MANAGEMENT ==========
    
    public void ForceLODLevel(int lodLevel)
    {
        if (lodGroup != null)
        {
            lodGroup.ForceLOD(lodLevel);
        }
    }
    
    public void EnableAutoLOD()
    {
        if (lodGroup != null)
        {
            lodGroup.ForceLOD(-1); // -1 = automatic
        }
    }
    
    // ========== METODI PER COMPATIBILITÀ CON VECCHIO CODICE ==========
    
    /// <summary>
    /// Compatibilità con il vecchio metodo CollectPresent
    /// </summary>
    public void CollectPresent()
    {
        CollectItem();
    }
    
    /// <summary>
    /// Compatibilità con il vecchio metodo GetPresentName
    /// </summary>
    public string GetPresentName() => GetName();
    
    /// <summary>
    /// Compatibilità con il vecchio metodo GetPresentValue
    /// </summary>
    public int GetPresentValue() => GetCollectibleValue();
    
    /// <summary>
    /// Compatibilità con il vecchio metodo SetPresentName
    /// </summary>
    public void SetPresentName(string name)
    {
        SetCollectibleName(name);
    }
    
    /// <summary>
    /// Compatibilità con il vecchio metodo SetPresentValue
    /// </summary>
    public void SetPresentValue(int value)
    {
        SetCollectibleValue(value);
    }
    
    /// <summary>
    /// Compatibilità - alias per ResetCollected
    /// </summary>
    public void ResetPresent()
    {
        ResetCollected();
    }
    
    /// <summary>
    /// Metodo per compatibilità con sistemi esterni che chiamano OnPresentCollected
    /// </summary>
    public void TriggerPresentCollection()
    {
        CollectItem();
    }
    
    // ========== CONTROLLO EFFETTI MANUALI ==========
    
    [ContextMenu("Test Pre-Collection Effects")]
    public void TestPreCollectionEffects()
    {
        StartPreCollectionEffects();
    }
    
    [ContextMenu("Test Post-Collection Effects")]
    public void TestPostCollectionEffects()
    {
        PlayCollectionFeedback();
    }
    
    [ContextMenu("Stop Present Effects")]
    public void ManualStopPresentEffects()
    {
        StopAllPresentEffects();
    }
    
    // ========== DEBUG PRESENT-SPECIFIC ==========
    
    [ContextMenu("Debug Present State")]
    public void DebugPresentState()
    {
        Debug.Log($"=== STATO PRESENT {collectibleName} ===\n" +
                  $"Is Collected: {isCollected}\n" +
                  $"GameObject Active: {gameObject.activeInHierarchy}\n" +
                  $"Pre Effect: {(preCollectionEffect != null ? (preCollectionEffect.gameObject.activeInHierarchy ? "Active" : "Inactive GameObject") : "NULL")}\n" +
                  $"Pre Effect Playing: {(preCollectionEffect != null ? preCollectionEffect.isPlaying : false)}\n" +
                  $"Post Effect 1: {(postCollectionEffect1 != null ? (postCollectionEffect1.gameObject.activeInHierarchy ? "Active" : "Inactive GameObject") : "NULL")}\n" +
                  $"Post Effect 2: {(postCollectionEffect2 != null ? (postCollectionEffect2.gameObject.activeInHierarchy ? "Active" : "Inactive GameObject") : "NULL")}\n" +
                  $"Background Audio (base): {IsBackgroundAudioPlaying()}\n" +
                  $"Background Clip (base): {(GetBackgroundLoopSound() != null ? GetBackgroundLoopSound().name : "NULL")}\n" +
                  $"Collection Clip (base): {(GetCollectSound() != null ? GetCollectSound().name : "NULL")}");
    }
    
    [ContextMenu("Configure as Christmas Present")]
    public void DebugConfigureChristmas()
    {
        ConfigureAsChristmasPresent();
    }
    
    [ContextMenu("Configure as Birthday Present")]
    public void DebugConfigureBirthday()
    {
        ConfigureAsBirthdayPresent();
    }
    
    [ContextMenu("Configure as Special Present")]
    public void DebugConfigureSpecial()
    {
        ConfigureAsSpecialPresent();
    }
    
    [ContextMenu("Cache Components")]
    public void DebugCacheComponents()
    {
        CacheChildComponents();
    }
    
    [ContextMenu("Force LOD 0")]
    public void DebugForceLOD0()
    {
        ForceLODLevel(0);
    }
    
    [ContextMenu("Enable Auto LOD")]
    public void DebugEnableAutoLOD()
    {
        EnableAutoLOD();
    }
    
    public override void DebugInfo()
    {
        base.DebugInfo();
        Debug.Log($"=== Present Specific Info ===\n" +
                  $"Rotation Enabled: {enableRotation}\n" +
                  $"Rotation Speed: {rotationSpeed}\n" +
                  $"Has Pre Effect: {preCollectionEffect != null}\n" +
                  $"Has Post Effect 1: {postCollectionEffect1 != null}\n" +
                  $"Has Post Effect 2: {postCollectionEffect2 != null}\n" +
                  $"Post Effect Duration: {postEffectDuration}\n" +
                  $"Visual Container: {visualContainer != null}\n" +
                  $"Effects Container: {effectsContainer != null}\n" +
                  $"LOD Group: {lodGroup != null}\n" +
                  $"Cached Renderers: {(childRenderers != null ? childRenderers.Length : 0)}\n" +
                  $"Legacy Particles: {presentCollectionParticles != null}\n" +
                  $"Legacy Jingle: {presentJingleSound != null}");
    }
    
    // ========== GIZMOS PRESENT-SPECIFIC ==========
    
    protected override void OnDrawGizmos()
    {
        base.OnDrawGizmos();
        
        // Indicatore Present specifico
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position + Vector3.up * 2f, Vector3.one * 0.3f);
        
        // Indicatori per containers
        if (visualContainer != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(visualContainer.position, Vector3.one * 0.1f);
        }
        
        if (effectsContainer != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireCube(effectsContainer.position, Vector3.one * 0.1f);
        }
        
        // Indicatori per effetti particellari
        if (preCollectionEffect != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(preCollectionEffect.transform.position, 0.3f);
        }
        
        if (postCollectionEffect1 != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(postCollectionEffect1.transform.position, 0.2f);
        }
        
        if (postCollectionEffect2 != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(postCollectionEffect2.transform.position, 0.2f);
        }
    }
    
    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();
        
        // Area di raccolta dettagliata per Present
        Gizmos.color = Color.red;
        if (presentCollider != null)
        {
            if (presentCollider is SphereCollider sphereCol)
            {
                Gizmos.DrawWireSphere(transform.position + sphereCol.center, sphereCol.radius * 1.2f);
            }
            else if (presentCollider is BoxCollider boxCol)
            {
                Gizmos.matrix = Matrix4x4.TRS(transform.position + boxCol.center, transform.rotation, boxCol.size * 1.2f);
                Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
                Gizmos.matrix = Matrix4x4.identity;
            }
        }
        else
        {
            Gizmos.DrawWireSphere(transform.position, 1.5f);
        }
        
        // Visualizza rotazione se abilitata
        if (enableRotation)
        {
            Transform rotatingTransform = visualContainer != null ? visualContainer : transform;
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(rotatingTransform.position, 0.5f);
            
            // Frecce per indicare la rotazione
            Vector3 right = rotatingTransform.right * 0.8f;
            Vector3 forward = rotatingTransform.forward * 0.8f;
            Gizmos.DrawRay(rotatingTransform.position, right);
            Gizmos.DrawRay(rotatingTransform.position, forward);
        }
        
        // Connessioni tra parent e container
        if (visualContainer != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, visualContainer.position);
        }
        
        if (effectsContainer != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(transform.position, effectsContainer.position);
        }
        
        // Connessioni agli effetti particellari
        if (preCollectionEffect != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, preCollectionEffect.transform.position);
        }
        
        if (postCollectionEffect1 != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(transform.position, postCollectionEffect1.transform.position);
        }
        
        if (postCollectionEffect2 != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, postCollectionEffect2.transform.position);
        }
    }
}