
using UnityEngine;

public class Presents : Collectibles
{
    [Header("Present Specific Settings")]
    [SerializeField] private bool customRotationSpeed = false;
    [SerializeField] private float presentRotationSpeed = 60f;
    [SerializeField] private bool customFloatSettings = false;
    [SerializeField] private float presentFloatSpeed = 2.5f;
    [SerializeField] private float presentFloatStrength = 0.4f;
    
    [Header("Present Effects")]
    [SerializeField] private GameObject presentCollectionParticles;
    [SerializeField] private AudioClip presentJingleSound;
    
    private void Awake()
    {
        // Forza il tipo a Present e configura le impostazioni di base
        SetCollectibleType(CollectibleType.Present);
        
        // Assicurati che i present non usino mai il billboard
        SetBillboardEnabled(false);
        
        // Configura impostazioni specifiche per i present se personalizzate
        ApplyPresentSettings();
        
        Debug.Log($"[Presents] Configurazione Awake completata per {gameObject.name}");
    }
    
    private void Start()
    {
        // Registrati al SceneManager01 se disponibile
        RegisterWithSceneManager();
        
        // Sottoscrivi all'evento di raccolta per logica specifica dei present
        OnCollected.AddListener(OnPresentCollected);
        
        Debug.Log($"[Presents] '{GetName()}' inizializzato - Valore: {GetValue()}");
    }
    
    private void ApplyPresentSettings()
    {
        if (customRotationSpeed)
        {
            SetRotationSpeed(presentRotationSpeed);
        }
        else
        {
            // Impostazioni default per present
            SetRotationSpeed(45f);
        }
        
        if (customFloatSettings)
        {
            SetFloatSettings(presentFloatSpeed, presentFloatStrength);
        }
        else
        {
            // Impostazioni default per present
            SetFloatSettings(2f, 0.3f);
        }
        
        // Assicura che i present ruotino e fluttuino
        SetRotationEnabled(true);
        SetFloatEnabled(true);
    }
    
    private void RegisterWithSceneManager()
    {
        if (!IsCollected() && SceneManager01.Instance != null)
        {
            SceneManager01.Instance.RegisterCollectible(this);
            Debug.Log($"[Presents] Registrato con SceneManager01: {GetName()}");
        }
        else
        {
            Debug.LogWarning($"[Presents] SceneManager01 non trovato per {GetName()}");
        }
    }
    
    private void OnPresentCollected(Collectibles collectible)
    {
        Debug.Log($"[Presents] Present '{GetName()}' raccolto! Valore: {GetValue()}");
        
        // La notifica al PlayerCollectibleTracker viene gestita automaticamente 
        // dalla classe base Collectibles nel metodo NotifyManagers()
        
        // Logica specifica per i present
        HandlePresentSpecificLogic();
    }
    
    private void HandlePresentSpecificLogic()
    {
        // Effetti specifici per present
        PlayPresentCollectionEffect();
        
        // Integrazione con altri sistemi
        NotifyGameSystems();
        
        Debug.Log($"[Presents] Logica specifica eseguita per {GetName()}");
    }
    
    private void PlayPresentCollectionEffect()
    {
        // Effetti visivi specifici per present
        if (presentCollectionParticles != null)
        {
            GameObject particles = Instantiate(presentCollectionParticles, transform.position, Quaternion.identity);
            Destroy(particles, 3f); // Pulisci dopo 3 secondi
        }
        
        // Audio specifico per present
        if (presentJingleSound != null)
        {
            AudioSource.PlayClipAtPoint(presentJingleSound, transform.position, 0.7f);
        }
        
        Debug.Log($"[Presents] Effetti presente riprodotti per {GetName()}");
    }
    
    private void NotifyGameSystems()
    {
        // Notifica sistemi di gioco specifici per present
        
        // Esempio: Sistema punteggio
        // if (GameManager.Instance != null)
        // {
        //     GameManager.Instance.AddScore(GetValue() * 10);
        // }
        
        // Esempio: Sistema achievement
        // if (AchievementManager.Instance != null)
        // {
        //     AchievementManager.Instance.NotifyPresentCollected(GetName());
        // }
        
        // Esempio: Sistema audio globale
        // if (AudioManager.Instance != null)
        // {
        //     AudioManager.Instance.PlayPresentJingle();
        // }
    }
    
    // Configurazioni preset per diversi tipi di present
    public void ConfigureAsChristmasPresent()
    {
        SetName("Christmas Gift");
        SetValue(10);
        SetRotationSpeed(30f);
        SetFloatSettings(1.5f, 0.25f);
        Debug.Log("[Presents] Configurato come regalo di Natale");
    }
    
    public void ConfigureAsBirthdayPresent()
    {
        SetName("Birthday Gift");
        SetValue(15);
        SetRotationSpeed(45f);
        SetFloatSettings(2.5f, 0.4f);
        Debug.Log("[Presents] Configurato come regalo di compleanno");
    }
    
    public void ConfigureAsSpecialPresent()
    {
        SetName("Special Gift");
        SetValue(25);
        SetRotationSpeed(60f);
        SetFloatSettings(3f, 0.5f);
        Debug.Log("[Presents] Configurato come regalo speciale");
    }
    
    // Metodo per cambiare dinamicamente le proprietà
    public void SetPresentProperties(string newName, int newValue, float rotSpeed = 45f)
    {
        SetName(newName);
        SetValue(newValue);
        SetRotationSpeed(rotSpeed);
    }
    
    private void OnDestroy()
    {
        // Cleanup dell'evento
        OnCollected.RemoveListener(OnPresentCollected);
        Debug.Log($"[Presents] Cleanup eventi per {GetName()}");
    }
    
    // Debug e utility
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private void OnDrawGizmos()
    {
        // Gizmo per area di raccolta
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, 1f);
        
        // Indicatore Present
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position + Vector3.up * 2f, Vector3.one * 0.3f);
    }
    
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private void OnDrawGizmosSelected()
    {
        // Info dettagliate quando selezionato
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, 1.5f);
        
        // Visualizza movimento float
        if (Application.isPlaying)
        {
            Vector3 currentPos = transform.position;
            Vector3 maxFloatPos = currentPos;
            maxFloatPos.y += presentFloatStrength;
            Vector3 minFloatPos = currentPos;
            minFloatPos.y -= presentFloatStrength;
            
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(minFloatPos, maxFloatPos);
        }
    }
}