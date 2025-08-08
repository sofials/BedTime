using UnityEngine;
using UnityEngine.Events;

public class Memories : MonoBehaviour
{
    [Header("Memory Specific Settings")]
    [SerializeField] private string memoryName;
    [SerializeField] private bool isCollected = false;
    [SerializeField] private int memoryValue = 1;
    
    [Header("Memory Floating Animation")]
    [SerializeField] private bool enableFloating = true;
    [SerializeField] private float floatSpeed = 2f;
    [SerializeField] private float floatStrength = 0.5f;
    
    [Header("Memory Effects")]
    [SerializeField] private Color memoryGlowColor = Color.cyan;
    [SerializeField] private GameObject memoryCollectionAura;
    [SerializeField] private AudioClip memoryEchoSound;
    [SerializeField] private GameObject collectEffect;
    
    [Header("Memory Events")]
    public UnityEvent<Memories> OnMemoryCollected;
    
    private Vector3 startPosition;
    private bool memoryInitialized = false;
    
    private void Awake()
    {
        // Salva la posizione iniziale
        startPosition = transform.position;
        
        // Auto-assign name se non impostato
        if (string.IsNullOrEmpty(memoryName))
        {
            memoryName = gameObject.name;
        }
        
        Debug.Log($"[Memories] Awake completato per {memoryName} alla posizione {startPosition}");
    }
    
    private void Start()
    {
        InitializeMemory();
        RegisterWithSceneManager();
        
        Debug.Log($"[Memories] '{memoryName}' inizializzata come Memory");
    }
    
    private void Update()
    {
        // Animazione floating se abilitata
        if (enableFloating && !isCollected)
        {
            FloatAnimation();
        }
    }
    
    private void InitializeMemory()
    {
        if (memoryInitialized) return;
        
        memoryInitialized = true;
        
        // Assicurati che l'oggetto sia attivo
        gameObject.SetActive(true);
        
        // Verifica che la posizione sia corretta
        if (Vector3.Distance(transform.position, startPosition) > 0.1f)
        {
            transform.position = startPosition;
            Debug.Log($"[Memories] Posizione corretta a {startPosition}");
        }
        
        Debug.Log($"[Memories] Inizializzazione specifica completata per {memoryName}");
    }
    
    private void FloatAnimation()
    {
        // Animazione floating - solo sull'asse Y
        float newY = startPosition.y + Mathf.Sin(Time.time * floatSpeed) * floatStrength;
        transform.position = new Vector3(startPosition.x, newY, startPosition.z);
    }
    
    private void RegisterWithSceneManager()
    {
        // Registra con lo SceneManager appropriato
        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        
        if (currentScene == "01 - Party in Lukelandia" && SceneManager01.Instance != null)
        {
            // Per la scena 01, usa il sistema Collectibles
            Collectibles collectible = gameObject.GetComponent<Collectibles>();
            if (collectible == null)
            {
                // Aggiungi componente Collectibles se non presente
                collectible = gameObject.AddComponent<Collectibles>();
                collectible.SetCollectibleName(memoryName);
                collectible.SetCollectibleType(CollectibleType.Memory);
            }
            
            SceneManager01.Instance.RegisterCollectible(collectible);
            Debug.Log($"[Memories] Registrata con SceneManager01: {memoryName}");
        }
        else
        {
            Debug.LogWarning($"[Memories] SceneManager non trovato per la scena '{currentScene}'");
        }
    }
    
    // ========== INTERAZIONE MEMORY ==========
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !isCollected)
        {
            CollectMemory();
        }
    }
    
    private void OnMouseDown()
    {
        if (!isCollected)
        {
            CollectMemory();
        }
    }
    
    public void CollectMemory()
    {
        if (isCollected)
        {
            Debug.LogWarning($"Memory {memoryName} già raccolta!");
            return;
        }
        
        isCollected = true;
        
        // Feedback visivo/audio
        PlayMemoryFeedback();
        
        // Notifica allo SceneManager
        NotifySceneManager();
        
        // Eventi
        OnMemoryCollected?.Invoke(this);
        
        // Nascondi dopo un breve delay
        StartCoroutine(HideAfterEffect());
        
        Debug.Log($"[Memories] Memory '{memoryName}' raccolta!");
    }
    
    private void PlayMemoryFeedback()
    {
        // Effetto visivo specifico per le memorie
        if (memoryCollectionAura != null)
        {
            GameObject aura = Instantiate(memoryCollectionAura, transform.position, Quaternion.identity);
            Destroy(aura, 5f);
        }
        else if (collectEffect != null)
        {
            GameObject effect = Instantiate(collectEffect, transform.position, Quaternion.identity);
            Destroy(effect, 2f);
        }
        
        // Audio specifico per memorie
        if (memoryEchoSound != null)
        {
            AudioSource.PlayClipAtPoint(memoryEchoSound, transform.position, 0.5f);
        }
        
        Debug.Log($"[Memories] Effetti memoria riprodotti per {memoryName}");
    }
    
    private void NotifySceneManager()
    {
        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        
        if (currentScene == "01 - Party in Lukelandia" && SceneManager01.Instance != null)
        {
            SceneManager01.Instance.NotifyMemoryCollected(memoryName);
        }
        else
        {
            // Fallback diretto al GameManager
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnSceneMemoryCollected(currentScene, memoryName);
            }
        }
        
        Debug.Log($"[Memories] Notifica inviata per memory '{memoryName}'");
    }
    
    private System.Collections.IEnumerator HideAfterEffect()
    {
        yield return new WaitForSeconds(1f);
        gameObject.SetActive(false);
    }
    
    // ========== GETTERS E SETTERS ==========
    
    public string GetMemoryName() => memoryName;
    public bool IsCollected() => isCollected;
    public int GetMemoryValue() => memoryValue;
    
    public void SetMemoryName(string name)
    {
        memoryName = name;
    }
    
    public void SetMemoryValue(int value)
    {
        memoryValue = value;
    }
    
    public void SetFloatSettings(float speed, float strength)
    {
        floatSpeed = speed;
        floatStrength = strength;
    }
    
    // ========== CONFIGURAZIONI PRESET ==========
    
    public void ConfigureAsStoryMemory()
    {
        SetMemoryName("Story Fragment");
        SetMemoryValue(1);
        SetFloatSettings(3f, 0.8f);
        Debug.Log("[Memories] Configurata come memory narrativa");
    }
    
    public void ConfigureAsLoreMemory()
    {
        SetMemoryName("Ancient Knowledge");
        SetMemoryValue(5);
        SetFloatSettings(5f, 1.2f);
        Debug.Log("[Memories] Configurata come memory lore");
    }
    
    public void ConfigureAsSecretMemory()
    {
        SetMemoryName("Hidden Truth");
        SetMemoryValue(10);
        SetFloatSettings(6f, 1.5f);
        Debug.Log("[Memories] Configurata come memory segreta");
    }
    
    // ========== UTILITY METHODS ==========
    
    public void ResetMemory()
    {
        isCollected = false;
        transform.position = startPosition;
        gameObject.SetActive(true);
        Debug.Log($"[Memories] Memory {memoryName} resetata");
    }
    
    public void ForceCollect()
    {
        if (!isCollected)
        {
            CollectMemory();
        }
    }
    
    // ========== DEBUG ==========
    
    [ContextMenu("Force Collect")]
    public void DebugForceCollect()
    {
        ForceCollect();
    }
    
    [ContextMenu("Reset Memory")]
    public void DebugResetMemory()
    {
        ResetMemory();
    }
    
    [ContextMenu("Reset to Start Position")]
    public void ResetToStartPosition()
    {
        transform.position = startPosition;
        Debug.Log($"[Memories] Posizione reset a {startPosition} per {memoryName}");
    }
    
    [ContextMenu("Update Start Position to Current")]
    public void UpdateStartPositionToCurrent()
    {
        startPosition = transform.position;
        Debug.Log($"[Memories] Start position aggiornata a {startPosition} per {memoryName}");
    }
    
    // ========== GIZMOS ==========
    
    private void OnDrawGizmos()
    {
        // Area di raccolta
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 1.2f);
        
        // Posizione iniziale
        if (Application.isPlaying)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(startPosition, Vector3.one * 0.2f);
        }
        
        // Indicatore Memory
        Gizmos.color = memoryGlowColor;
        Gizmos.matrix = Matrix4x4.TRS(transform.position + Vector3.up * 2.5f, 
                                     Quaternion.Euler(45, 0, 45), 
                                     Vector3.one * 0.4f);
        Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
        Gizmos.matrix = Matrix4x4.identity;
        
        // Warning se invisibile
        if (!gameObject.activeInHierarchy)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, 2f);
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        // Info dettagliate quando selezionata
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, 2f);
        
        // Mostra range floating
        if (Application.isPlaying && enableFloating)
        {
            Vector3 maxFloatPos = startPosition;
            maxFloatPos.y += floatStrength;
            Vector3 minFloatPos = startPosition;
            minFloatPos.y -= floatStrength;
            
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(minFloatPos, maxFloatPos);
        }
    }
}