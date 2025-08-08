using UnityEngine;

// Enum per i tipi di collectible
public enum CollectibleType
{
    Memory,
    Present,
    Checkpoint
}

/// <summary>
/// Classe base per tutti gli oggetti collezionabili.
/// Gestisce la logica comune di raccolta, registrazione con SceneManager, feedback base, e sistema audio completo.
/// </summary>
public class Collectibles : MonoBehaviour
{
    [Header("Collectible Settings")]
    [SerializeField] protected string collectibleName;
    [SerializeField] protected CollectibleType collectibleType;
    [SerializeField] protected bool isCollected = false;
    [SerializeField] protected int collectibleValue = 1;
    
    [Header("Base Visual Feedback")]
    [SerializeField] protected GameObject collectEffect;
    [SerializeField] protected float effectDuration = 1f;
    
    [Header("Complete Audio System")]
    [SerializeField] protected AudioClip backgroundLoopSound; // Suono continuo pre-raccolta
    [SerializeField] protected AudioClip collectSound; // Suono alla raccolta
    [SerializeField] protected float backgroundVolume = 0.3f;
    [SerializeField] protected float collectionVolume = 0.7f;
    [SerializeField] protected bool enableBackgroundLoop = false; // Abilita/disabilita il loop
    [SerializeField] protected bool use3DAudio = true; // Audio 3D vs 2D
    
    [Header("Base UI Feedback")]
    [SerializeField] protected string displayMessage = "";
    
    [Header("Base Animation")]
    [SerializeField] protected bool enableFloating = false;
    [SerializeField] protected float floatSpeed = 2f;
    [SerializeField] protected float floatStrength = 0.3f;
    
    [Header("Base Interaction")]
    [SerializeField] protected bool canBeClickedToCollect = true;
    [SerializeField] protected bool canBeTriggerToCollect = true;
    
    // Proprietà comuni
    protected Vector3 startPosition;
    protected bool collectibleInitialized = false;
    
    // Sistema Audio Unificato
    protected AudioSource backgroundAudioSource; // Per il loop di background
    protected AudioSource collectionAudioSource; // Per l'audio di raccolta
    protected bool backgroundAudioPlaying = false;
    
    protected virtual void Awake()
    {
        // Salva la posizione iniziale
        startPosition = transform.position;
        
        // Auto-assign name se non impostato
        if (string.IsNullOrEmpty(collectibleName))
        {
            collectibleName = gameObject.name;
        }
        
        // Auto-assign display message se non impostato
        if (string.IsNullOrEmpty(displayMessage))
        {
            displayMessage = $"{collectibleType} raccolto!";
        }
        
        // Setup sistema audio unificato
        SetupAudioSystem();
        
        Debug.Log($"[Collectibles] Awake completato per {collectibleName} ({collectibleType}) alla posizione {startPosition}");
    }
    
    protected virtual void Start()
    {
        InitializeCollectible();
        RegisterWithSceneManager();
        
        // Avvia background audio se abilitato
        StartBackgroundAudio();
        
        Debug.Log($"[Collectibles] '{collectibleName}' inizializzato come {collectibleType}");
    }
    
    protected virtual void Update()
    {
        // Animazione floating base se abilitata
        if (enableFloating && !isCollected)
        {
            FloatAnimation();
        }
    }
    
    /// <summary>
    /// Setup del sistema audio unificato per evitare conflitti
    /// </summary>
    protected virtual void SetupAudioSystem()
    {
        Debug.Log($"[Collectibles] === SETUP AUDIO SYSTEM per {collectibleName} ===");
        
        // BACKGROUND AUDIO SOURCE (sempre creato ma attivato solo se necessario)
        if (backgroundLoopSound != null || enableBackgroundLoop)
        {
            // Crea un GameObject figlio per il background audio
            GameObject backgroundAudioObj = new GameObject($"{gameObject.name}_BackgroundAudio");
            backgroundAudioObj.transform.SetParent(transform);
            backgroundAudioObj.transform.localPosition = Vector3.zero;
            
            backgroundAudioSource = backgroundAudioObj.AddComponent<AudioSource>();
            backgroundAudioSource.clip = backgroundLoopSound;
            backgroundAudioSource.loop = true;
            backgroundAudioSource.volume = backgroundVolume;
            backgroundAudioSource.playOnAwake = false;
            backgroundAudioSource.spatialBlend = use3DAudio ? 1f : 0f;
            
            Debug.Log($"[Collectibles] ✅ Background AudioSource creato per {collectibleName} - " +
                     $"Clip: {(backgroundLoopSound != null ? backgroundLoopSound.name : "NULL")}, " +
                     $"3D: {use3DAudio}");
        }
        
        // COLLECTION AUDIO SOURCE (sempre creato)
        GameObject collectionAudioObj = new GameObject($"{gameObject.name}_CollectionAudio");
        collectionAudioObj.transform.SetParent(transform);
        collectionAudioObj.transform.localPosition = Vector3.zero;
        
        collectionAudioSource = collectionAudioObj.AddComponent<AudioSource>();
        collectionAudioSource.loop = false;
        collectionAudioSource.playOnAwake = false;
        collectionAudioSource.volume = collectionVolume;
        collectionAudioSource.spatialBlend = use3DAudio ? 1f : 0f;
        
        Debug.Log($"[Collectibles] ✅ Collection AudioSource creato per {collectibleName}");
        
        Debug.Log($"[Collectibles] === FINE SETUP AUDIO SYSTEM ===");
    }
    
    /// <summary>
    /// Avvia l'audio di background se abilitato
    /// </summary>
    protected virtual void StartBackgroundAudio()
    {
        if (enableBackgroundLoop && backgroundAudioSource != null && backgroundLoopSound != null && !isCollected)
        {
            if (!backgroundAudioSource.isPlaying)
            {
                backgroundAudioSource.Play();
                backgroundAudioPlaying = true;
                Debug.Log($"[Collectibles] ✅ Background audio avviato per {collectibleName} - Volume: {backgroundVolume}");
            }
        }
        else
        {
            if (!enableBackgroundLoop)
                Debug.Log($"[Collectibles] Background loop disabilitato per {collectibleName}");
            else if (backgroundAudioSource == null)
                Debug.LogWarning($"[Collectibles] Background AudioSource NULL per {collectibleName}");
            else if (backgroundLoopSound == null)
                Debug.LogWarning($"[Collectibles] Background loop sound NULL per {collectibleName}");
        }
    }
    
    /// <summary>
    /// Ferma l'audio di background
    /// </summary>
    protected virtual void StopBackgroundAudio()
    {
        if (backgroundAudioSource != null && backgroundAudioPlaying)
        {
            backgroundAudioSource.Stop();
            backgroundAudioPlaying = false;
            Debug.Log($"[Collectibles] ✅ Background audio fermato per {collectibleName}");
        }
    }
    
    /// <summary>
    /// Riproduce l'audio di raccolta
    /// </summary>
    protected virtual void PlayCollectionAudio()
    {
        if (collectSound != null && collectionAudioSource != null)
        {
            collectionAudioSource.clip = collectSound;
            collectionAudioSource.volume = collectionVolume;
            collectionAudioSource.Play();
            
            // Verifica immediata
            if (collectionAudioSource.isPlaying)
            {
                Debug.Log($"[Collectibles] ✅ Audio di raccolta riprodotto per {collectibleName} - " +
                         $"Clip: {collectSound.name}, Volume: {collectionVolume}");
            }
            else
            {
                Debug.LogError($"[Collectibles] ❌ Audio di raccolta NON si avvia per {collectibleName}!");
            }
        }
        else
        {
            if (collectSound == null)
                Debug.LogWarning($"[Collectibles] ❌ Collect sound NULL per {collectibleName}");
            if (collectionAudioSource == null)
                Debug.LogWarning($"[Collectibles] ❌ Collection AudioSource NULL per {collectibleName}");
        }
    }
    
    protected virtual void InitializeCollectible()
    {
        if (collectibleInitialized) return;
        
        collectibleInitialized = true;
        
        // Assicurati che l'oggetto sia attivo
        gameObject.SetActive(true);
        
        // Verifica che la posizione sia corretta
        if (Vector3.Distance(transform.position, startPosition) > 0.1f)
        {
            transform.position = startPosition;
            Debug.Log($"[Collectibles] Posizione corretta a {startPosition}");
        }
        
        // Setup collider come trigger se presente
        Collider collectibleCollider = GetComponent<Collider>();
        if (collectibleCollider != null && !collectibleCollider.isTrigger)
        {
            collectibleCollider.isTrigger = true;
            Debug.Log($"[Collectibles] Collider impostato come trigger");
        }
        
        Debug.Log($"[Collectibles] Inizializzazione base completata per {collectibleName}");
    }
    
    protected virtual void FloatAnimation()
    {
        // Animazione floating base - solo sull'asse Y
        float newY = startPosition.y + Mathf.Sin(Time.time * floatSpeed) * floatStrength;
        transform.position = new Vector3(startPosition.x, newY, startPosition.z);
    }
    
    protected virtual void RegisterWithSceneManager()
    {
        // Registra con lo SceneManager appropriato
        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        
        if (currentScene == "01 - Party in Lukelandia" && SceneManager01.Instance != null)
        {
            SceneManager01.Instance.RegisterCollectible(this);
            Debug.Log($"[Collectibles] Registrato con SceneManager01: {collectibleName}");
        }
        else
        {
            Debug.LogWarning($"[Collectibles] SceneManager non trovato per la scena '{currentScene}'");
        }
    }
    
    protected virtual void OnItemCollected()
    {
        // Implementazione base vuota - le classi derivate possono sovrascrivere
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
    /// Metodo principale per raccogliere l'oggetto - può essere sovrascritto dalle classi derivate
    /// </summary>
    public virtual void CollectItem()
    {
        if (isCollected)
        {
            Debug.LogWarning($"Collectible {collectibleName} già raccolto!");
            return;
        }
        
        Debug.Log($"[Collectibles] === INIZIO RACCOLTA {collectibleType} {collectibleName} ===");
        
        isCollected = true;
        
        // Ferma background audio immediatamente
        StopBackgroundAudio();
        
        // Chiama il metodo di feedback (può essere sovrascritto)
        PlayCollectionFeedback();
        
        // Notifica allo SceneManager
        NotifySceneManager();
        
        // Chiama metodo virtuale per le classi derivate
        OnItemCollected();
        
        // Nascondi l'oggetto dopo un delay
        if (gameObject.activeInHierarchy)
        {
            StartCoroutine(HideAfterEffect());
        }
        
        Debug.Log($"[Collectibles] === FINE RACCOLTA {collectibleType} {collectibleName} ===");
    }
    
    /// <summary>
    /// Feedback base di raccolta - può essere sovrascritto dalle classi derivate
    /// </summary>
    protected virtual void PlayCollectionFeedback()
    {
        Debug.Log($"[Collectibles] === FEEDBACK BASE per {collectibleName} ===");
        
        // Effetto visivo base
        if (collectEffect != null)
        {
            GameObject effect = Instantiate(collectEffect, transform.position, Quaternion.identity);
            Destroy(effect, effectDuration);
            Debug.Log($"[Collectibles] ✅ Effetto visivo creato");
        }
        else
        {
            Debug.LogWarning($"[Collectibles] ❌ Collect effect NULL per {collectibleName}");
        }
        
        // Audio di raccolta tramite sistema unificato
        PlayCollectionAudio();
        
        // Messaggio UI base
        if (!string.IsNullOrEmpty(displayMessage))
        {
            Debug.Log($"[Collectible] {displayMessage}");
            // Qui potresti chiamare un sistema di UI per mostrare il messaggio
            // UIManager.Instance?.ShowMessage(displayMessage);
        }
        
        Debug.Log($"[Collectibles] === FINE FEEDBACK BASE ===");
    }
    
    protected virtual void NotifySceneManager()
    {
        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        
        if (currentScene == "01 - Party in Lukelandia" && SceneManager01.Instance != null)
        {
            SceneManager01.Instance.OnCollectibleCollected(collectibleName, collectibleType);
            
            // Chiama anche metodi specifici per compatibilità
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
            // Fallback diretto al GameManager
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
    
    protected virtual System.Collections.IEnumerator HideAfterEffect()
    {
        // Calcola tempo di attesa basato su audio + effetti
        float waitTime = effectDuration * 0.5f;
        
        // Se c'è audio di raccolta, aspetta che finisca
        if (collectionAudioSource != null && collectionAudioSource.isPlaying && collectionAudioSource.clip != null)
        {
            float audioLength = collectionAudioSource.clip.length;
            waitTime = Mathf.Max(waitTime, audioLength);
        }
        
        Debug.Log($"[Collectibles] Aspettando {waitTime} secondi prima di nascondere {collectibleName}");
        
        yield return new WaitForSeconds(waitTime);
        gameObject.SetActive(false);
        
        Debug.Log($"[Collectibles] {collectibleName} nascosto");
    }
    
    // ========== GESTIONE AUDIO ON DESTROY/DISABLE ==========
    
    protected virtual void OnDestroy()
    {
        StopAllAudio();
    }
    
    protected virtual void OnDisable()
    {
        StopAllAudio();
    }
    
    /// <summary>
    /// Ferma tutti gli audio del collectible
    /// </summary>
    protected virtual void StopAllAudio()
    {
        StopBackgroundAudio();
        
        if (collectionAudioSource != null && collectionAudioSource.isPlaying)
        {
            collectionAudioSource.Stop();
        }
        
        Debug.Log($"[Collectibles] Tutti gli audio fermati per {collectibleName}");
    }
    
    // ========== GETTERS PUBBLICI ==========
    
    public string GetName() => collectibleName;
    public CollectibleType GetCollectibleType() => collectibleType;
    public bool IsCollected() => isCollected;
    public string GetDisplayMessage() => displayMessage;
    public int GetCollectibleValue() => collectibleValue;
    public Vector3 GetStartPosition() => startPosition;
    
    // Getters audio
    public AudioSource GetBackgroundAudioSource() => backgroundAudioSource;
    public AudioSource GetCollectionAudioSource() => collectionAudioSource;
    public bool IsBackgroundAudioPlaying() => backgroundAudioPlaying;
    public AudioClip GetBackgroundLoopSound() => backgroundLoopSound;
    public AudioClip GetCollectSound() => collectSound;
    
    // ========== SETTERS ==========
    
    public virtual void SetCollectibleName(string name)
    {
        collectibleName = name;
    }
    
    public virtual void SetCollectibleType(CollectibleType type)
    {
        collectibleType = type;
    }
    
    public virtual void SetDisplayMessage(string message)
    {
        displayMessage = message;
    }
    
    public virtual void SetCollectibleValue(int value)
    {
        collectibleValue = value;
    }
    
    public virtual void SetFloatSettings(float speed, float strength)
    {
        floatSpeed = speed;
        floatStrength = strength;
    }
    
    public virtual void SetFloatingEnabled(bool enabled)
    {
        enableFloating = enabled;
    }
    
    public virtual void SetInteractionSettings(bool canClick, bool canTrigger)
    {
        canBeClickedToCollect = canClick;
        canBeTriggerToCollect = canTrigger;
    }
    
    // Setters audio
    public virtual void SetBackgroundLoopSound(AudioClip clip)
    {
        backgroundLoopSound = clip;
        if (backgroundAudioSource != null)
        {
            backgroundAudioSource.clip = clip;
        }
    }
    
    public virtual void SetCollectSound(AudioClip clip)
    {
        collectSound = clip;
    }
    
    public virtual void SetAudioVolumes(float backgroundVol, float collectionVol)
    {
        backgroundVolume = backgroundVol;
        collectionVolume = collectionVol;
        
        if (backgroundAudioSource != null)
        {
            backgroundAudioSource.volume = backgroundVolume;
        }
        
        if (collectionAudioSource != null)
        {
            collectionAudioSource.volume = collectionVolume;
        }
    }
    
    public virtual void SetBackgroundLoopEnabled(bool enabled)
    {
        enableBackgroundLoop = enabled;
        
        if (enabled && !isCollected)
        {
            StartBackgroundAudio();
        }
        else
        {
            StopBackgroundAudio();
        }
    }
    
    public virtual void SetAudio3D(bool use3D)
    {
        use3DAudio = use3D;
        
        float spatialBlend = use3D ? 1f : 0f;
        
        if (backgroundAudioSource != null)
        {
            backgroundAudioSource.spatialBlend = spatialBlend;
        }
        
        if (collectionAudioSource != null)
        {
            collectionAudioSource.spatialBlend = spatialBlend;
        }
    }
    
    // ========== METODI DI CONTROLLO ==========
    
    /// <summary>
    /// Forza la raccolta dell'oggetto (utile per testing o eventi speciali)
    /// </summary>
    public virtual void ForceCollect()
    {
        if (!isCollected)
        {
            CollectItem();
        }
    }
    
    /// <summary>
    /// Reset dello stato raccolto (utile per testing)
    /// </summary>
    public virtual void ResetCollected()
    {
        isCollected = false;
        transform.position = startPosition;
        gameObject.SetActive(true);
        
        // Reset audio
        StopAllAudio();
        StartBackgroundAudio();
        
        Debug.Log($"[Collectible] {collectibleName} resetato");
    }
    
    /// <summary>
    /// Metodo per nascondere l'oggetto se già raccolto (chiamato dal SceneManager)
    /// </summary>
    public virtual void HideIfCollected()
    {
        if (isCollected)
        {
            gameObject.SetActive(false);
        }
    }
    
    // ========== METODI PER COMPATIBILITÀ CON VECCHIO CODICE ==========
    
    public void CollectAsMemory()
    {
        if (collectibleType == CollectibleType.Memory)
        {
            CollectItem();
        }
    }
    
    public void CollectAsPresent()
    {
        if (collectibleType == CollectibleType.Present)
        {
            CollectItem();
        }
    }
    
    // ========== CONTROLLI AUDIO MANUALI ==========
    
    [ContextMenu("Start Background Audio")]
    public void ManualStartBackgroundAudio()
    {
        StartBackgroundAudio();
    }
    
    [ContextMenu("Stop Background Audio")]
    public void ManualStopBackgroundAudio()
    {
        StopBackgroundAudio();
    }
    
    [ContextMenu("Test Collection Audio")]
    public void ManualTestCollectionAudio()
    {
        PlayCollectionAudio();
    }
    
    [ContextMenu("Stop All Audio")]
    public void ManualStopAllAudio()
    {
        StopAllAudio();
    }
    
    // ========== DEBUG ==========
    
    [ContextMenu("Force Collect")]
    public void DebugForceCollect()
    {
        ForceCollect();
    }
    
    [ContextMenu("Reset Collected")]
    public void DebugResetCollected()
    {
        ResetCollected();
    }
    
    [ContextMenu("Debug Audio State")]
    public void DebugAudioState()
    {
        Debug.Log($"=== STATO AUDIO {collectibleName} ===\n" +
                  $"Background Loop Enabled: {enableBackgroundLoop}\n" +
                  $"Background Audio Source: {(backgroundAudioSource != null ? "Exists" : "NULL")}\n" +
                  $"Background Playing: {backgroundAudioPlaying}\n" +
                  $"Background Clip: {(backgroundLoopSound != null ? backgroundLoopSound.name : "NULL")}\n" +
                  $"Background Volume: {backgroundVolume}\n" +
                  $"Collection Audio Source: {(collectionAudioSource != null ? "Exists" : "NULL")}\n" +
                  $"Collection Clip: {(collectSound != null ? collectSound.name : "NULL")}\n" +
                  $"Collection Volume: {collectionVolume}\n" +
                  $"Use 3D Audio: {use3DAudio}");
    }
    
    [ContextMenu("Debug Info")]
    public virtual void DebugInfo()
    {
        Debug.Log($"=== Collectible Info ===\n" +
                  $"Name: {collectibleName}\n" +
                  $"Type: {collectibleType}\n" +
                  $"Value: {collectibleValue}\n" +
                  $"Collected: {isCollected}\n" +
                  $"Display Message: {displayMessage}\n" +
                  $"Floating: {enableFloating}\n" +
                  $"Can Click: {canBeClickedToCollect}\n" +
                  $"Can Trigger: {canBeTriggerToCollect}\n" +
                  $"Background Loop: {enableBackgroundLoop}\n" +
                  $"3D Audio: {use3DAudio}");
    }
    
    // ========== GIZMOS BASE ==========
    
    protected virtual void OnDrawGizmos()
    {
        // Area di raccolta base
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 1.2f);
        
        // Posizione iniziale
        if (Application.isPlaying)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(startPosition, Vector3.one * 0.2f);
        }
        
        // Indicatore tipo collectible
        Color typeColor = collectibleType switch
        {
            CollectibleType.Memory => Color.cyan,
            CollectibleType.Present => Color.yellow,
            CollectibleType.Checkpoint => Color.magenta,
            _ => Color.white
        };
        
        Gizmos.color = typeColor;
        Gizmos.matrix = Matrix4x4.TRS(transform.position + Vector3.up * 2.5f, 
                                     Quaternion.Euler(45, 0, 45), 
                                     Vector3.one * 0.4f);
        Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
        Gizmos.matrix = Matrix4x4.identity;
        
        // Indicatore audio
        if (enableBackgroundLoop && backgroundLoopSound != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 1.5f, 0.3f);
        }
        
        // Warning se invisibile
        if (!gameObject.activeInHierarchy)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, 2f);
        }
    }
    
    protected virtual void OnDrawGizmosSelected()
    {
        // Info dettagliate quando selezionato
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, 2f);
        
        // Mostra range floating se abilitato
        if (Application.isPlaying && enableFloating)
        {
            Vector3 maxFloatPos = startPosition;
            maxFloatPos.y += floatStrength;
            Vector3 minFloatPos = startPosition;
            minFloatPos.y -= floatStrength;
            
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(minFloatPos, maxFloatPos);
            
            // Mostra il movimento attuale
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, 0.1f);
        }
        
        // Range audio 3D
        if (enableBackgroundLoop && use3DAudio)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, 10f); // Range approssimativo
        }
        
        // Linea di connessione alla posizione iniziale
        if (Application.isPlaying)
        {
            Gizmos.color = Color.white;
            Gizmos.DrawLine(transform.position, startPosition);
        }
    }
}