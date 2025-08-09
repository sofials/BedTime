using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Classe per le Memory che eredita da Collectibles.
/// Usa il sistema audio unificato della classe base per evitare conflitti.
/// Aggiunge funzionalità specifiche per le memorie come glow, echoes, e categorizzazione.
/// </summary>
public class Memories : Collectibles
{
    [Header("Memory Specific Settings")]
    [SerializeField] private Color memoryGlowColor = Color.cyan;
    [SerializeField] private MemoryCategory memoryCategory = MemoryCategory.Story;
    
    [Header("Memory Specific Effects")]
    [SerializeField] private GameObject memoryCollectionAura;
    [SerializeField] private AudioClip memoryEchoSound; // Ora usa il sistema base
    [SerializeField] private ParticleSystem memoryGlowEffect;
    
    [Header("Memory Events")]
    public UnityEvent<Memories> OnMemoryCollected;
    
    // Enum per categorie di memorie
    public enum MemoryCategory
    {
        Story,      // Memorie narrative principali
        Lore,       // Conoscenza del mondo/lore
        Secret,     // Memorie segrete/nascoste
        Character   // Memorie dei personaggi
    }
    
    private Renderer[] memoryRenderers;
    private Material[] originalMaterials;
    private Material[] glowMaterials;
    
    protected override void Awake()
    {
        // Imposta il tipo come Memory prima di chiamare il base Awake
        collectibleType = CollectibleType.Memory;
        
        // Imposta default specifici per le Memory
        if (collectibleValue == 1) // Se è ancora il valore di default
        {
            collectibleValue = GetDefaultValueForCategory();
        }
        
        // Abilita floating per default nelle Memory
        enableFloating = true;
        floatSpeed = 2f;
        floatStrength = 0.5f;
        
        // CONFIGURA AUDIO TRAMITE SISTEMA BASE (evita conflitti!)
        enableBackgroundLoop = false; // Le Memory di default non hanno loop di background
        use3DAudio = true; // Audio 3D per le Memory
        
        // Mappa l'audio legacy al sistema base
        if (memoryEchoSound != null && collectSound == null)
        {
            collectSound = memoryEchoSound; // Usa l'echo come suono di raccolta
        }
        
        // Chiama il base Awake che configurerà il sistema audio
        base.Awake();
        
        // Setup specifico delle Memory
        SetupMemoryRenderers();
        
        Debug.Log($"[Memories] Memory Awake completato per {collectibleName} - Categoria: {memoryCategory}");
    }
    
    protected override void Start()
    {
        base.Start();
        
        // Avvia effetti specifici delle Memory
        StartMemoryEffects();
        
        Debug.Log($"[Memories] Memory '{collectibleName}' inizializzata come {memoryCategory} Memory");
    }
    
    protected override void Update()
    {
        base.Update();
        
        // Aggiorna effetti specifici delle Memory se necessario
        UpdateMemoryEffects();
    }
    
    private int GetDefaultValueForCategory()
    {
        return memoryCategory switch
        {
            MemoryCategory.Story => 1,
            MemoryCategory.Lore => 5,
            MemoryCategory.Secret => 10,
            MemoryCategory.Character => 3,
            _ => 1
        };
    }
    
    private void SetupMemoryRenderers()
    {
        // Cache dei renderer per effetti glow
        memoryRenderers = GetComponentsInChildren<Renderer>();
        
        if (memoryRenderers.Length > 0)
        {
            originalMaterials = new Material[memoryRenderers.Length];
            glowMaterials = new Material[memoryRenderers.Length];
            
            for (int i = 0; i < memoryRenderers.Length; i++)
            {
                if (memoryRenderers[i] != null && !(memoryRenderers[i] is ParticleSystemRenderer))
                {
                    originalMaterials[i] = memoryRenderers[i].material;
                    // Qui potresti creare materiali glow se necessario
                    glowMaterials[i] = originalMaterials[i]; // Per ora usa lo stesso
                }
            }
        }
        
        Debug.Log($"[Memories] Setup {memoryRenderers.Length} renderer per {collectibleName}");
    }
    
    private void StartMemoryEffects()
    {
        Debug.Log($"[Memories] === AVVIO EFFETTI MEMORY per {collectibleName} ===");
        
        // Avvia effetto glow se presente
        if (memoryGlowEffect != null)
        {
            if (memoryGlowEffect.gameObject.activeInHierarchy)
            {
                if (!memoryGlowEffect.isPlaying)
                {
                    memoryGlowEffect.Clear();
                    memoryGlowEffect.Play();
                    Debug.Log($"[Memories] ✅ Effetto glow AVVIATO per {collectibleName}");
                }
                else
                {
                    Debug.Log($"[Memories] ⚠️ Effetto glow già in riproduzione per {collectibleName}");
                }
            }
            else
            {
                Debug.LogError($"[Memories] ❌ GameObject dell'effetto glow NON ATTIVO per {collectibleName}!");
            }
        }
        else
        {
            Debug.LogWarning($"[Memories] ❌ Effetto glow NON ASSEGNATO per {collectibleName}");
        }
        
        // L'audio di background è ora gestito automaticamente dalla classe base!
        Debug.Log($"[Memories] Audio gestito dalla classe base Collectibles");
        
        Debug.Log($"[Memories] === FINE AVVIO EFFETTI MEMORY ===");
    }
    
    private void UpdateMemoryEffects()
    {
        // Qui puoi aggiungere effetti continui come pulsing, color changing, etc.
        // Per esempio, un glow pulsante basato sul tempo
        if (!isCollected && memoryRenderers != null)
        {
            // Effetto glow pulsante (esempio)
            // float pulse = (Mathf.Sin(Time.time * 2f) + 1f) * 0.5f;
            // ApplyGlowIntensity(pulse);
        }
    }
    
    private void StopMemoryEffects()
    {
        Debug.Log($"[Memories] === STOP EFFETTI MEMORY per {collectibleName} ===");
        
        // Ferma effetto glow se presente
        if (memoryGlowEffect != null)
        {
            if (memoryGlowEffect.isPlaying)
            {
                memoryGlowEffect.Stop();
                Debug.Log($"[Memories] ✅ Effetto glow FERMATO per {collectibleName}");
            }
        }
        
        // L'audio è ora gestito automaticamente dalla classe base!
        Debug.Log($"[Memories] Audio fermato dalla classe base Collectibles");
    }
    
    // ========== OVERRIDE METODI BASE ==========
    
    /// <summary>
    /// Override del metodo virtuale chiamato quando l'item viene raccolto
    /// </summary>
    protected override void OnItemCollected()
    {
        // Evento specifico Memory
        OnMemoryCollected?.Invoke(this);
    }
    
    /// <summary>
    /// Override del feedback di raccolta per aggiungere effetti specifici delle Memory
    /// </summary>
    protected override void PlayCollectionFeedback()
    {
        Debug.Log($"[Memories] === FEEDBACK MEMORY SPECIFICO per {collectibleName} ===");
        
        // Ferma effetti specifici delle Memory
        StopMemoryEffects();
        
        // ========== EFFETTI VISIVI SPECIFICI MEMORY ==========
        
        // Effetto visivo specifico per le memorie
        if (memoryCollectionAura != null)
        {
            GameObject aura = Instantiate(memoryCollectionAura, transform.position, Quaternion.identity);
            Destroy(aura, 5f);
            Debug.Log($"[Memories] ✅ Aura memory creata");
        }
        else
        {
            Debug.LogWarning($"[Memories] ❌ Memory collection aura NON ASSEGNATA per {collectibleName}");
        }
        
        // ========== CHIAMA IL FEEDBACK BASE ==========
        // Questo gestirà automaticamente:
        // - L'effetto visivo base (se presente) come fallback
        // - L'audio di raccolta tramite il sistema unificato (memoryEchoSound mappato in collectSound)
        // - Il messaggio UI specifico delle Memory
        base.PlayCollectionFeedback();
        
        Debug.Log($"[Memories] === FINE FEEDBACK MEMORY SPECIFICO ===");
    }
    
    /// <summary>
    /// Override del metodo HideAfterEffect per gestire meglio le Memory
    /// </summary>
    protected override System.Collections.IEnumerator HideAfterEffect()
    {
        Debug.Log($"[Memories] Aspettando prima di nascondere Memory {collectibleName}");
        
        // Aspetta un po' di più per le Memory per permettere agli effetti di completarsi
        float waitTime = Mathf.Max(effectDuration * 0.5f, 1.5f);
        
        // Considera anche l'audio di raccolta gestito dalla classe base
        if (GetCollectionAudioSource() != null && GetCollectionAudioSource().isPlaying && GetCollectionAudioSource().clip != null)
        {
            float audioLength = GetCollectionAudioSource().clip.length;
            waitTime = Mathf.Max(waitTime, audioLength);
        }
        
        Debug.Log($"[Memories] Aspettando {waitTime} secondi per completare tutti gli effetti");
        
        yield return new WaitForSeconds(waitTime);
        
        gameObject.SetActive(false);
        Debug.Log($"[Memories] Memory {collectibleName} nascosta dopo effetti");
    }
    
    // ========== GESTIONE EFFETTI ON DESTROY/DISABLE ==========
    
    protected override void OnDestroy()
    {
        StopAllMemoryEffects();
        base.OnDestroy(); // Chiama anche il metodo della classe base
    }
    
    protected override void OnDisable()
    {
        StopAllMemoryEffects();
        base.OnDisable(); // Chiama anche il metodo della classe base
    }
    
    private void StopAllMemoryEffects()
    {
        StopMemoryEffects();
        Debug.Log($"[Memories] Tutti gli effetti Memory fermati per {collectibleName}");
    }
    
    // ========== OVERRIDE RESET ==========
    
    public override void ResetCollected()
    {
        base.ResetCollected(); // Questo resetterà anche l'audio automaticamente
        
        // Reset specifico delle Memory
        StartMemoryEffects();
        
        Debug.Log($"[Memories] Memory {collectibleName} resetata con effetti");
    }
    
    // ========== METODI SPECIFICI MEMORY ==========
    
    public MemoryCategory GetMemoryCategory() => memoryCategory;
    
    public void SetMemoryCategory(MemoryCategory category)
    {
        memoryCategory = category;
        
        // Aggiorna il valore se è ancora quello di default
        if (collectibleValue == GetDefaultValueForCategory())
        {
            collectibleValue = GetDefaultValueForCategory();
        }
    }
    
    public void SetMemoryGlowColor(Color color)
    {
        memoryGlowColor = color;
        
        // Applica il colore al glow effect se presente
        if (memoryGlowEffect != null)
        {
            var main = memoryGlowEffect.main;
            main.startColor = color;
        }
    }
    
    public void SetMemoryEchoSound(AudioClip echoClip)
    {
        memoryEchoSound = echoClip;
        // Aggiorna anche il sistema audio base
        SetCollectSound(echoClip);
    }
    
    /// <summary>
    /// Abilita/disabilita un eventuale background loop per Memory speciali
    /// </summary>
    public void SetMemoryBackgroundLoop(AudioClip loopClip, bool enabled = true)
    {
        if (loopClip != null)
        {
            SetBackgroundLoopSound(loopClip);
        }
        SetBackgroundLoopEnabled(enabled);
    }
    
    // ========== CONFIGURAZIONI PRESET ==========
    
    public void ConfigureAsStoryMemory()
    {
        SetMemoryCategory(MemoryCategory.Story);
        SetCollectibleName("Story Fragment");
        SetCollectibleValue(1);
        SetFloatSettings(3f, 0.8f);
        SetMemoryGlowColor(Color.cyan);
        SetDisplayMessage("Storia recuperata!");
        SetAudioVolumes(0.2f, 0.6f); // Volume basso per story memory
        Debug.Log("[Memories] Configurata come memory narrativa");
    }
    
    public void ConfigureAsLoreMemory()
    {
        SetMemoryCategory(MemoryCategory.Lore);
        SetCollectibleName("Ancient Knowledge");
        SetCollectibleValue(5);
        SetFloatSettings(5f, 1.2f);
        SetMemoryGlowColor(Color.magenta);
        SetDisplayMessage("Antica conoscenza acquisita!");
        SetAudioVolumes(0.3f, 0.7f); // Volume medio per lore memory
        Debug.Log("[Memories] Configurata come memory lore");
    }
    
    public void ConfigureAsSecretMemory()
    {
        SetMemoryCategory(MemoryCategory.Secret);
        SetCollectibleName("Hidden Truth");
        SetCollectibleValue(10);
        SetFloatSettings(6f, 1.5f);
        SetMemoryGlowColor(Color.yellow);
        SetDisplayMessage("Verità nascosta rivelata!");
        SetAudioVolumes(0.4f, 0.8f); // Volume alto per secret memory
        Debug.Log("[Memories] Configurata come memory segreta");
    }
    
    public void ConfigureAsCharacterMemory(string characterName = "Unknown")
    {
        SetMemoryCategory(MemoryCategory.Character);
        SetCollectibleName($"{characterName} Memory");
        SetCollectibleValue(3);
        SetFloatSettings(4f, 1.0f);
        SetMemoryGlowColor(Color.green);
        SetDisplayMessage($"Ricordo di {characterName} recuperato!");
        SetAudioVolumes(0.25f, 0.65f); // Volume medio-basso per character memory
        Debug.Log($"[Memories] Configurata come memory di {characterName}");
    }
    
    // ========== METODI PER COMPATIBILITÀ CON VECCHIO CODICE ==========
    
    /// <summary>
    /// Compatibilità con il vecchio metodo CollectMemory
    /// </summary>
    public void CollectMemory()
    {
        CollectItem();
    }
    
    /// <summary>
    /// Compatibilità con il vecchio metodo GetMemoryName
    /// </summary>
    public string GetMemoryName() => GetName();
    
    /// <summary>
    /// Compatibilità con il vecchio metodo GetMemoryValue
    /// </summary>
    public int GetMemoryValue() => GetCollectibleValue();
    
    /// <summary>
    /// Compatibilità con il vecchio metodo SetMemoryName
    /// </summary>
    public void SetMemoryName(string name)
    {
        SetCollectibleName(name);
    }
    
    /// <summary>
    /// Compatibilità con il vecchio metodo SetMemoryValue
    /// </summary>
    public void SetMemoryValue(int value)
    {
        SetCollectibleValue(value);
    }
    
    /// <summary>
    /// Compatibilità - alias per ResetCollected
    /// </summary>
    public void ResetMemory()
    {
        ResetCollected();
    }
    
    /// <summary>
    /// Metodo per compatibilità con sistemi esterni che chiamano OnMemoryCollected
    /// </summary>
    public void TriggerMemoryCollection()
    {
        CollectItem();
    }
    
    // ========== CONTROLLO EFFETTI MANUALI ==========
    
    [ContextMenu("Test Memory Glow")]
    public void TestMemoryGlow()
    {
        StartMemoryEffects();
    }
    
    [ContextMenu("Stop Memory Effects")]
    public void ManualStopMemoryEffects()
    {
        StopAllMemoryEffects();
    }
    
    [ContextMenu("Test Memory Collection")]
    public void TestMemoryCollection()
    {
        PlayCollectionFeedback();
    }
    
    // ========== DEBUG MEMORY-SPECIFIC ==========
    
    [ContextMenu("Debug Memory State")]
    public void DebugMemoryState()
    {
        Debug.Log($"=== STATO MEMORY {collectibleName} ===\n" +
                  $"Is Collected: {isCollected}\n" +
                  $"GameObject Active: {gameObject.activeInHierarchy}\n" +
                  $"Category: {memoryCategory}\n" +
                  $"Glow Color: {memoryGlowColor}\n" +
                  $"Glow Effect: {(memoryGlowEffect != null ? (memoryGlowEffect.gameObject.activeInHierarchy ? "Active" : "Inactive GameObject") : "NULL")}\n" +
                  $"Glow Playing: {(memoryGlowEffect != null ? memoryGlowEffect.isPlaying : false)}\n" +
                  $"Memory Aura: {(memoryCollectionAura != null ? "Assigned" : "NULL")}\n" +
                  $"Echo Sound (base): {(GetCollectSound() != null ? GetCollectSound().name : "NULL")}\n" +
                  $"Background Audio: {IsBackgroundAudioPlaying()}");
    }
    
    [ContextMenu("Configure as Story Memory")]
    public void DebugConfigureStory()
    {
        ConfigureAsStoryMemory();
    }
    
    [ContextMenu("Configure as Lore Memory")]
    public void DebugConfigureLore()
    {
        ConfigureAsLoreMemory();
    }
    
    [ContextMenu("Configure as Secret Memory")]
    public void DebugConfigureSecret()
    {
        ConfigureAsSecretMemory();
    }
    
    [ContextMenu("Configure as Character Memory")]
    public void DebugConfigureCharacter()
    {
        ConfigureAsCharacterMemory("TestCharacter");
    }
    
    public override void DebugInfo()
    {
        base.DebugInfo();
        Debug.Log($"=== Memory Specific Info ===\n" +
                  $"Category: {memoryCategory}\n" +
                  $"Glow Color: {memoryGlowColor}\n" +
                  $"Has Memory Aura: {memoryCollectionAura != null}\n" +
                  $"Has Echo Sound: {memoryEchoSound != null}\n" +
                  $"Has Glow Effect: {memoryGlowEffect != null}\n" +
                  $"Renderers Count: {(memoryRenderers != null ? memoryRenderers.Length : 0)}\n" +
                  $"Original Materials: {(originalMaterials != null ? originalMaterials.Length : 0)}\n" +
                  $"Glow Materials: {(glowMaterials != null ? glowMaterials.Length : 0)}");
    }
    
    // ========== GIZMOS MEMORY-SPECIFIC ==========
    
    protected override void OnDrawGizmos()
    {
        base.OnDrawGizmos();
        
        // Indicatore specifico Memory con colore categoria
        Color categoryColor = memoryCategory switch
        {
            MemoryCategory.Story => Color.cyan,
            MemoryCategory.Lore => Color.magenta,
            MemoryCategory.Secret => Color.yellow,
            MemoryCategory.Character => Color.green,
            _ => Color.white
        };
        
        Gizmos.color = categoryColor;
        Gizmos.matrix = Matrix4x4.TRS(transform.position + Vector3.up * 3f, 
                                     Quaternion.Euler(0, Time.time * 45f, 0), 
                                     Vector3.one * 0.3f);
        Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
        Gizmos.matrix = Matrix4x4.identity;
        
        // Indicatore glow effect
        if (memoryGlowEffect != null)
        {
            Gizmos.color = memoryGlowColor;
            Gizmos.DrawWireSphere(memoryGlowEffect.transform.position, 0.5f);
        }
    }
    
    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();
        
        // Range effetti Memory
        Gizmos.color = memoryGlowColor;
        Gizmos.DrawWireSphere(transform.position, 3f);
        
        // Connessione al glow effect
        if (memoryGlowEffect != null)
        {
            Gizmos.color = memoryGlowColor;
            Gizmos.DrawLine(transform.position, memoryGlowEffect.transform.position);
        }
        
        // Indicatore categoria nella visualizzazione
        Gizmos.color = Color.white;
        Vector3 labelPos = transform.position + Vector3.up * 4f;
        // Qui potresti aggiungere una label se hai un sistema di debug GUI
    }
}