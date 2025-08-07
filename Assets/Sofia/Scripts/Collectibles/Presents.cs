using UnityEngine;

public class Presents : Collectibles
{
    [Header("Present Specific Settings")]
    [SerializeField] private bool customRotationSpeed = false;
    [SerializeField] private float presentRotationSpeed = 60f;
    [SerializeField] private bool customFloatSettings = false;
    [SerializeField] private float presentFloatSpeed = 2.5f;
    [SerializeField] private float presentFloatStrength = 0.4f;
    
    private void Awake()
    {
        // Forza il tipo a Present e disabilita billboard
        SetCollectibleType(CollectibleType.Present);
        
        // Configura impostazioni specifiche per i present se personalizzate
        if (customRotationSpeed)
        {
            SetRotationSpeed(presentRotationSpeed);
        }
        
        if (customFloatSettings)
        {
            SetFloatSettings(presentFloatSpeed, presentFloatStrength);
        }
    }
    
    private void Start()
    {
        // Registrati al SceneManager01 se disponibile
        if (!IsCollected() && SceneManager01.Instance != null)
        {
            SceneManager01.Instance.RegisterPresent(this);
        }
        
        // Sottoscrivi all'evento di raccolta per logica specifica dei present
        OnCollected.AddListener(OnPresentCollected);
        
        Debug.Log($"[Presents] '{GetName()}' inizializzato - Valore: {GetValue()}");
    }
    
    private void OnPresentCollected(Collectibles collectible)
    {
        Debug.Log($"[Presents] Present '{GetName()}' raccolto! Valore: {GetValue()}");
        
        // Notifica il PlayerCollectibleTracker se non usiamo SceneManager
        // (La notifica viene gestita automaticamente dalla classe base Collectibles)
        
        // Logica specifica per i present
        HandlePresentSpecificLogic();
    }
    
    private void HandlePresentSpecificLogic()
    {
        // Aggiungi qui logica specifica per i present
        // Es: incrementa punteggio, attiva effetti speciali, particelle colorate, etc.
        
        // Esempi:
        // GameManager.Instance?.AddScore(GetValue());
        // AudioManager.Instance?.PlayPresentCollectionSound();
        // ParticleManager.Instance?.PlayPresentExplosion(transform.position);
        
        // Effetto visivo specifico per present (se diverso dalle memory)
        PlayPresentCollectionEffect();
    }
    
    private void PlayPresentCollectionEffect()
    {
        // Effetti specifici per i present
        // Es: particelle colorate, suoni gioiosi, scaling effect, etc.
        
        // Se hai effetti diversi per present vs memory, mettili qui
        // Altrimenti la classe base Collectibles gestisce già gli effetti standard
    }
    
    // Metodi di configurazione specifici per Present
    private void SetRotationSpeed(float speed)
    {
        // Accedi alle variabili protected/private della classe base se necessario
        // O esponi un setter pubblico nella classe Collectibles
    }
    
    private void SetFloatSettings(float speed, float strength)
    {
        // Configura impostazioni float specifiche
    }
    
    // Override per comportamenti specifici dei present se necessario
    protected virtual void OnTriggerEnter(Collider other)
    {
        // Se vuoi comportamenti di trigger specifici per i present
        // altrimenti la classe base gestisce tutto
    }
    
    private void OnDestroy()
    {
        // Cleanup dell'evento
        OnCollected.RemoveListener(OnPresentCollected);
    }
    
    // Metodi di utilità per debug
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private void OnDrawGizmos()
    {
        // Disegna gizmo per visualizzare l'area di raccolta in editor
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, 1f);
    }
}