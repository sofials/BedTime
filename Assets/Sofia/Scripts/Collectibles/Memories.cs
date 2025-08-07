using UnityEngine;

public class Memories : Collectibles
{
    [Header("Memory Specific Settings")]
    [SerializeField] private bool customFloatSettings = false;
    [SerializeField] private float memoryFloatSpeed = 4f;
    [SerializeField] private float memoryFloatStrength = 1f;
    
    [Header("Memory Effects")]
    [SerializeField] private Color memoryGlowColor = Color.cyan;
    [SerializeField] private GameObject memoryCollectionAura;
    [SerializeField] private AudioClip memoryEchoSound;
    
    private void Awake()
    {
        // Forza il tipo a Memory e configura le impostazioni di base
        SetCollectibleType(CollectibleType.Memory);
        
        // Configura impostazioni specifiche per le memory
        ApplyMemorySettings();
        
        Debug.Log($"[Memories] Configurazione Awake completata per {gameObject.name}");
    }
    
    private void Start()
    {
        // Registrati al SceneManager01 se disponibile
        RegisterWithSceneManager();
        
        // Sottoscrivi all'evento di raccolta per logica specifica delle memory
        OnCollected.AddListener(OnMemoryCollected);
        
        Debug.Log($"[Memories] '{GetName()}' inizializzata");
    }
    
    private void ApplyMemorySettings()
    {
        // Le memory usano sempre billboard e non ruotano
        SetBillboardEnabled(true);
        SetRotationEnabled(false);
        
        if (customFloatSettings)
        {
            SetFloatSettings(memoryFloatSpeed, memoryFloatStrength);
        }
        else
        {
            // Impostazioni default per memory - movimento più ampio e misterioso
            SetFloatSettings(4f, 1f);
        }
        
        // Assicura che fluttuino
        SetFloatEnabled(true);
    }
    
    private void RegisterWithSceneManager()
    {
        if (!IsCollected() && SceneManager01.Instance != null)
        {
            SceneManager01.Instance.RegisterCollectible(this);
            Debug.Log($"[Memories] Registrata con SceneManager01: {GetName()}");
        }
        else
        {
            Debug.LogWarning($"[Memories] SceneManager01 non trovato per {GetName()}");
        }
    }
    
    private void OnMemoryCollected(Collectibles collectible)
    {
        Debug.Log($"[Memories] Memory '{GetName()}' raccolta!");
        
        // La notifica al PlayerCollectibleTracker viene gestita automaticamente 
        // dalla classe base Collectibles nel metodo NotifyManagers()
        
        // Logica specifica per le memory
        HandleMemorySpecificLogic();
    }
    
    private void HandleMemorySpecificLogic()
    {
        // Effetti specifici per memory
        PlayMemoryCollectionEffect();
        
        // Integrazione con sistemi narrativi
        NotifyNarrativeSystems();
        
        Debug.Log($"[Memories] Logica specifica eseguita per {GetName()}");
    }
    
    private void PlayMemoryCollectionEffect()
    {
        // Effetti visivi specifici per memory
        if (memoryCollectionAura != null)
        {
            GameObject aura = Instantiate(memoryCollectionAura, transform.position, Quaternion.identity);
            Destroy(aura, 5f); // Effetto più lungo per le memory
        }
        
        // Audio specifico per memory - più etereo e misterioso
        if (memoryEchoSound != null)
        {
            AudioSource.PlayClipAtPoint(memoryEchoSound, transform.position, 0.5f);
        }
        
        Debug.Log($"[Memories] Effetti memory riprodotti per {GetName()}");
    }
    
    private void NotifyNarrativeSystems()
    {
        // Notifica sistemi narrativi specifici per memory
        
        // Esempio: Sistema narrative/dialoghi
        // if (NarrativeManager.Instance != null)
        // {
        //     NarrativeManager.Instance.UnlockMemory(GetName());
        // }
        
        // Esempio: Sistema cutscene
        // if (CutsceneManager.Instance != null)
        // {
        //     CutsceneManager.Instance.TriggerMemoryScene(GetValue());
        // }
        
        // Esempio: Sistema journal/lore
        // if (JournalManager.Instance != null)
        // {
        //     JournalManager.Instance.AddMemoryEntry(GetName(), GetValue());
        // }
    }
    
    // Configurazioni preset per diversi tipi di memory
    public void ConfigureAsStoryMemory()
    {
        SetName("Story Fragment");
        SetValue(1);
        SetFloatSettings(3f, 0.8f);
        Debug.Log("[Memories] Configurata come memoria narrativa");
    }
    
    public void ConfigureAsLoreMemory()
    {
        SetName("Ancient Knowledge");
        SetValue(5);
        SetFloatSettings(5f, 1.2f);
        Debug.Log("[Memories] Configurata come memoria del lore");
    }
    
    public void ConfigureAsSecretMemory()
    {
        SetName("Hidden Truth");
        SetValue(10);
        SetFloatSettings(6f, 1.5f);
        Debug.Log("[Memories] Configurata come memoria segreta");
    }
    
    // Metodo per impostare proprietà memory-specific
    public void SetMemoryProperties(string newName, int importance, float mysticalLevel = 1f)
    {
        SetName(newName);
        SetValue(importance);
        SetFloatSettings(3f + mysticalLevel, 0.5f + (mysticalLevel * 0.3f));
    }
    
    private void OnDestroy()
    {
        // Cleanup dell'evento
        OnCollected.RemoveListener(OnMemoryCollected);
        Debug.Log($"[Memories] Cleanup eventi per {GetName()}");
    }
    
    // Debug e utility
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private void OnDrawGizmos()
    {
        // Gizmo per area di raccolta
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 1.2f);
        
        // Indicatore Memory con effetto mistico
        Gizmos.color = memoryGlowColor;
        Gizmos.matrix = Matrix4x4.TRS(transform.position + Vector3.up * 2.5f, 
                                     Quaternion.Euler(45, 0, 45), 
                                     Vector3.one * 0.4f);
        Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
        Gizmos.matrix = Matrix4x4.identity;
    }
    
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private void OnDrawGizmosSelected()
    {
        // Info dettagliate quando selezionato
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, 2f);
        
        // Visualizza range di floating più ampio per memory
        if (Application.isPlaying)
        {
            Vector3 currentPos = transform.position;
            Vector3 maxFloatPos = currentPos;
            maxFloatPos.y += memoryFloatStrength;
            Vector3 minFloatPos = currentPos;
            minFloatPos.y -= memoryFloatStrength;
            
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(minFloatPos, maxFloatPos);
            
            // Indicatori billboard
            Gizmos.color = Color.yellow;
            if (Camera.main != null)
            {
                Vector3 toCam = (Camera.main.transform.position - transform.position).normalized;
                Gizmos.DrawRay(transform.position, toCam * 1.5f);
            }
        }
    }
}