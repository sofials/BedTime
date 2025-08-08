using UnityEngine;
using UnityEngine.Events;

public class Presents : MonoBehaviour
{
    [Header("Present Specific Settings")]
    [SerializeField] private string presentName;
    [SerializeField] private bool isCollected = false;
    [SerializeField] private int presentValue = 10;
    
    [Header("Present Animation")]
    [SerializeField] private bool enableRotation = true;
    [SerializeField] private float rotationSpeed = 45f;
    [SerializeField] private bool enableFloating = true;
    [SerializeField] private float floatSpeed = 2f;
    [SerializeField] private float floatStrength = 0.3f;
    
    [Header("Present Effects")]
    [SerializeField] private GameObject presentCollectionParticles;
    [SerializeField] private AudioClip presentJingleSound;
    [SerializeField] private GameObject collectEffect;
    
    [Header("Present Events")]
    public UnityEvent<Presents> OnPresentCollected;
    
    [Header("Parent Object Components")]
    [SerializeField] private Transform visualContainer; // Container per le mesh/LOD
    [SerializeField] private Transform effectsContainer; // Container per effetti
    [SerializeField] private Collider presentCollider; // Reference al collider
    [SerializeField] private LODGroup lodGroup; // Reference al LOD Group
    
    private Vector3 startPosition;
    private bool presentInitialized = false;
    private Renderer[] childRenderers; // Cache dei renderer per ottimizzazione
    private AudioSource audioSource; // AudioSource locale se presente
    
    private void Awake()
    {
        // Salva la posizione iniziale
        startPosition = transform.position;
        
        // Auto-assign name se non impostato
        if (string.IsNullOrEmpty(presentName))
        {
            presentName = gameObject.name;
        }
        
        // Cache dei componenti child
        CacheChildComponents();
        
        Debug.Log($"[Presents] Awake completato per {presentName} alla posizione {startPosition}");
    }
    
    private void Start()
    {
        InitializePresent();
        RegisterWithSceneManager();
        
        Debug.Log($"[Presents] '{presentName}' inizializzato come Present");
    }
    
    private void Update()
    {
        if (!isCollected)
        {
            // Animazione rotazione - applica al visual container se presente, altrimenti al parent
            if (enableRotation)
            {
                Transform targetTransform = visualContainer != null ? visualContainer : transform;
                targetTransform.Rotate(Vector3.up * rotationSpeed * Time.deltaTime);
            }
            
            // Animazione floating - sempre applicata al parent per mantenere collider allineato
            if (enableFloating)
            {
                FloatAnimation();
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
        
        // Cache tutti i renderer per gestione visibilità
        childRenderers = GetComponentsInChildren<Renderer>();
        
        // Cache AudioSource locale
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null && effectsContainer != null)
        {
            audioSource = effectsContainer.GetComponent<AudioSource>();
        }
        
        Debug.Log($"[Presents] Componenti cachati: Collider={presentCollider != null}, LOD={lodGroup != null}, Renderers={childRenderers.Length}");
    }
    
    private void InitializePresent()
    {
        if (presentInitialized) return;
        
        presentInitialized = true;
        
        // Assicurati che l'oggetto e i suoi child siano attivi
        gameObject.SetActive(true);
        SetChildVisibility(true);
        
        // Verifica che la posizione sia corretta
        if (Vector3.Distance(transform.position, startPosition) > 0.1f)
        {
            transform.position = startPosition;
            Debug.Log($"[Presents] Posizione corretta a {startPosition}");
        }
        
        // Setup collider come trigger se non già impostato
        if (presentCollider != null && !presentCollider.isTrigger)
        {
            presentCollider.isTrigger = true;
            Debug.Log($"[Presents] Collider impostato come trigger");
        }
        
        Debug.Log($"[Presents] Inizializzazione specifica completata per {presentName}");
    }
    
    private void SetChildVisibility(bool visible)
    {
        // Gestisce la visibilità di tutti i renderer child
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
        
        // Gestisce il LOD Group
        if (lodGroup != null)
        {
            lodGroup.enabled = visible;
        }
    }
    
    private void FloatAnimation()
    {
        // Animazione floating - solo sull'asse Y
        float newY = startPosition.y + Mathf.Sin(Time.time * floatSpeed) * floatStrength;
        transform.position = new Vector3(startPosition.x, newY, startPosition.z);
    }
    
    private void RegisterWithSceneManager()
    {
        // Registra con lo SceneManager appropriato (solo per scena 01)
        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        
        if (currentScene == "01 - Party in Lukelandia" && SceneManager01.Instance != null)
        {
            // Per la scena 01, usa il sistema Collectibles
            Collectibles collectible = gameObject.GetComponent<Collectibles>();
            if (collectible == null)
            {
                // Aggiungi componente Collectibles se non presente
                collectible = gameObject.AddComponent<Collectibles>();
                collectible.SetCollectibleName(presentName);
                collectible.SetCollectibleType(CollectibleType.Present);
            }
            
            SceneManager01.Instance.RegisterCollectible(collectible);
            Debug.Log($"[Presents] Registrato con SceneManager01: {presentName}");
        }
        else if (currentScene != "01 - Party in Lukelandia")
        {
            Debug.LogWarning($"[Presents] Present trovato nella scena '{currentScene}' - i Present dovrebbero essere solo nella scena 01!");
        }
        else
        {
            Debug.LogWarning($"[Presents] SceneManager01 non trovato per {presentName}");
        }
    }
    
    // ========== INTERAZIONE PRESENT ==========
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !isCollected)
        {
            CollectPresent();
        }
    }
    
    private void OnMouseDown()
    {
        if (!isCollected)
        {
            CollectPresent();
        }
    }
    
    public void CollectPresent()
    {
        if (isCollected)
        {
            Debug.LogWarning($"Present {presentName} già raccolto!");
            return;
        }
        
        isCollected = true;
        
        // Feedback visivo/audio
        PlayPresentFeedback();
        
        // Notifica allo SceneManager
        NotifySceneManager();
        
        // Eventi
        OnPresentCollected?.Invoke(this);
        
        // Nascondi dopo un breve delay
        StartCoroutine(HideAfterEffect());
        
        Debug.Log($"[Presents] Present '{presentName}' raccolto! Valore: {presentValue}");
    }
    
    private void PlayPresentFeedback()
    {
        // Posizione per gli effetti - preferisce il centro visuale
        Vector3 effectPosition = visualContainer != null ? visualContainer.position : transform.position;
        
        // Effetti visivi specifici per i present
        if (presentCollectionParticles != null)
        {
            GameObject particles = Instantiate(presentCollectionParticles, effectPosition, Quaternion.identity);
            Destroy(particles, 3f);
        }
        else if (collectEffect != null)
        {
            GameObject effect = Instantiate(collectEffect, effectPosition, Quaternion.identity);
            Destroy(effect, 2f);
        }
        
        // Audio specifico per present
        if (presentJingleSound != null)
        {
            if (audioSource != null)
            {
                audioSource.PlayOneShot(presentJingleSound, 0.7f);
            }
            else
            {
                AudioSource.PlayClipAtPoint(presentJingleSound, effectPosition, 0.7f);
            }
        }
        
        Debug.Log($"[Presents] Effetti present riprodotti per {presentName}");
    }
    
    private void NotifySceneManager()
    {
        // Solo la scena 01 dovrebbe avere presents
        if (SceneManager01.Instance != null)
        {
            SceneManager01.Instance.NotifyPresentCollected(presentName);
        }
        else
        {
            // Fallback diretto al GameManager
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnScene01PresentCollected(presentName);
            }
        }
        
        Debug.Log($"[Presents] Notifica inviata per present '{presentName}'");
    }
    
    private System.Collections.IEnumerator HideAfterEffect()
    {
        // Disabilita prima i renderer per feedback immediato
        SetChildVisibility(false);
        
        yield return new WaitForSeconds(1f);
        
        // Poi disattiva completamente l'oggetto
        gameObject.SetActive(false);
    }
    
    // ========== GETTERS E SETTERS ==========
    
    public string GetPresentName() => presentName;
    public bool IsCollected() => isCollected;
    public int GetPresentValue() => presentValue;
    public Collider GetCollider() => presentCollider;
    public LODGroup GetLODGroup() => lodGroup;
    
    public void SetPresentName(string name)
    {
        presentName = name;
    }
    
    public void SetPresentValue(int value)
    {
        presentValue = value;
    }
    
    public void SetRotationSpeed(float speed)
    {
        rotationSpeed = speed;
    }
    
    public void SetFloatSettings(float speed, float strength)
    {
        floatSpeed = speed;
        floatStrength = strength;
    }
    
    public void SetAnimationEnabled(bool rotation, bool floating)
    {
        enableRotation = rotation;
        enableFloating = floating;
    }
    
    // ========== CONFIGURAZIONI PRESET ==========
    
    public void ConfigureAsChristmasPresent()
    {
        SetPresentName("Christmas Gift");
        SetPresentValue(10);
        SetRotationSpeed(30f);
        SetFloatSettings(1.5f, 0.25f);
        Debug.Log("[Presents] Configurato come regalo di Natale");
    }
    
    public void ConfigureAsBirthdayPresent()
    {
        SetPresentName("Birthday Gift");
        SetPresentValue(15);
        SetRotationSpeed(45f);
        SetFloatSettings(2.5f, 0.4f);
        Debug.Log("[Presents] Configurato come regalo di compleanno");
    }
    
    public void ConfigureAsSpecialPresent()
    {
        SetPresentName("Special Gift");
        SetPresentValue(25);
        SetRotationSpeed(60f);
        SetFloatSettings(3f, 0.5f);
        Debug.Log("[Presents] Configurato come regalo speciale");
    }
    
    // ========== UTILITY METHODS ==========
    
    public void ResetPresent()
    {
        isCollected = false;
        transform.position = startPosition;
        gameObject.SetActive(true);
        SetChildVisibility(true);
        
        // Reset rotazione del visual container se presente
        if (visualContainer != null)
        {
            visualContainer.rotation = Quaternion.identity;
        }
        
        Debug.Log($"[Presents] Present {presentName} resetato");
    }
    
    public void ForceCollect()
    {
        if (!isCollected)
        {
            CollectPresent();
        }
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
    
    // ========== DEBUG ==========
    
    [ContextMenu("Force Collect")]
    public void DebugForceCollect()
    {
        ForceCollect();
    }
    
    [ContextMenu("Reset Present")]
    public void DebugResetPresent()
    {
        ResetPresent();
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
    
    // ========== GIZMOS ==========
    
    private void OnDrawGizmos()
    {
        // Area di raccolta basata sul collider se presente
        if (presentCollider != null)
        {
            Gizmos.color = Color.green;
            if (presentCollider is SphereCollider sphereCol)
            {
                Gizmos.DrawWireSphere(transform.position + sphereCol.center, sphereCol.radius);
            }
            else if (presentCollider is BoxCollider boxCol)
            {
                Gizmos.matrix = Matrix4x4.TRS(transform.position + boxCol.center, transform.rotation, boxCol.size);
                Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
                Gizmos.matrix = Matrix4x4.identity;
            }
        }
        else
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, 1f);
        }
        
        // Posizione iniziale
        if (Application.isPlaying)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireCube(startPosition, Vector3.one * 0.2f);
        }
        
        // Indicatore Present
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position + Vector3.up * 2f, Vector3.one * 0.3f);
        
        // Warning se invisibile
        if (!gameObject.activeInHierarchy)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, 2f);
        }
        
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
    }
    
    private void OnDrawGizmosSelected()
    {
        // Info dettagliate quando selezionato
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
        
        // Visualizza movimento float se abilitato
        if (Application.isPlaying && enableFloating)
        {
            Vector3 maxFloatPos = startPosition;
            maxFloatPos.y += floatStrength;
            Vector3 minFloatPos = startPosition;
            minFloatPos.y -= floatStrength;
            
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(minFloatPos, maxFloatPos);
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
    }
}