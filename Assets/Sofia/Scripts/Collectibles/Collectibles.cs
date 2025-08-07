using UnityEngine;
using UnityEngine.Events;
using System.Collections;

public enum CollectibleType
{
    Present,
    Memory
}

public class Collectibles : MonoBehaviour
{
    [Header("Collectible Settings")]
    [SerializeField] private CollectibleType collectibleType = CollectibleType.Present;
    [SerializeField] private string itemName = "Collectible";
    [SerializeField] private int value = 1;
    [SerializeField] private bool isCollected = false;
    
    [Header("Billboard (Memory Only)")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private bool useBillboard = false;
    
    [Header("Animation")]
    [SerializeField] private bool rotateObject = true;
    [SerializeField] private float rotationSpeed = 90f;
    [SerializeField] private bool floatUpDown = true;
    [SerializeField] private float floatSpeed = 2f;
    [SerializeField] private float floatStrength = 0.5f;
    
    [Header("Visual Effects")]
    [SerializeField] private GameObject visualEffect;
    [SerializeField] private CFXR_EffectController cfxrEffect; // For memories
    [SerializeField] private float destroyDelay = 0.5f;
    
    [Header("Audio")]
    [SerializeField] private AudioClip collectSound;
    [SerializeField] private AudioClip ambientSound; // For memories
    [SerializeField] private float collectVolume = 0.3f;
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;
    
    // Events
    [System.Serializable]
    public class CollectibleEvent : UnityEvent<Collectibles> { }
    public CollectibleEvent OnCollected = new CollectibleEvent();
    
    // Private variables
    private Vector3 startPosition;
    private AudioSource audioSource;
    private Renderer objectRenderer;
    private Collider triggerCollider;
    private bool hasBeenInitialized = false;
    private float timeOffset; // AGGIUNTO: Offset temporale per floating sincronizzato
    
    private void Awake()
    {
        // Pre-inizializzazione in Awake per permettere override nelle classi derivate
        PreInitialize();
    }
    
    private void Start()
    {
        // Inizializzazione completa in Start
        if (!hasBeenInitialized)
        {
            Initialize();
        }
    }
    
    private void PreInitialize()
    {
        // Setup di base che può essere modificato dalle classi derivate
        startPosition = transform.position;
        objectRenderer = GetComponent<Renderer>();
        triggerCollider = GetComponent<Collider>();
        
        // AGGIUNTO: Inizializza offset temporale per floating sincronizzato
        timeOffset = Random.Range(0f, Mathf.PI * 2f); // Offset casuale per variare il floating
        
        if (targetCamera == null)
            targetCamera = Camera.main;
            
        DebugLog($"[Collectibles] Pre-inizializzazione completata per {gameObject.name}");
    }
    
    private void Initialize()
    {
        hasBeenInitialized = true;
        
        InitializeComponents();
        SetupCollider();
        SetupAudio();
        StartVisualEffects();
        RegisterWithManagers();
        
        DebugLog($"[Collectibles] Inizializzazione completa per {gameObject.name} - Tipo: {collectibleType}");
    }
    
    private void InitializeComponents()
    {
        // Verifica componenti essenziali
        if (objectRenderer == null)
        {
            objectRenderer = GetComponent<Renderer>();
            if (objectRenderer == null)
            {
                Debug.LogWarning($"[Collectibles] Nessun Renderer trovato su {gameObject.name}");
            }
        }
        
        if (triggerCollider == null)
        {
            triggerCollider = GetComponent<Collider>();
            if (triggerCollider == null)
            {
                Debug.LogWarning($"[Collectibles] Nessun Collider trovato su {gameObject.name}");
            }
        }
    }
    
    private void SetupCollider()
    {
        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
            
            // Assicurati che i MeshCollider siano convessi per i trigger
            if (triggerCollider is MeshCollider meshCol)
            {
                meshCol.convex = true;
            }
            
            DebugLog($"[Collectibles] Collider configurato per {gameObject.name}");
        }
        else
        {
            Debug.LogError($"[Collectibles] ERRORE: Nessun collider su {gameObject.name}! Il collectible non sarà raccoglibile.");
        }
    }
    
    private void SetupAudio()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        // Configurazione audio basata sul tipo di collectible
        ConfigureAudioForType();
        
        DebugLog($"[Collectibles] Audio configurato per {gameObject.name} - Tipo: {collectibleType}");
    }
    
    private void ConfigureAudioForType()
    {
        if (collectibleType == CollectibleType.Memory)
        {
            // Audio 3D spaziale per le memory
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f; // 3D completo
            audioSource.rolloffMode = AudioRolloffMode.Linear;
            audioSource.minDistance = 20f;
            audioSource.maxDistance = 100f;
            audioSource.volume = 1f;
            
            // Avvia suono ambientale per le memory
            if (ambientSound != null)
            {
                audioSource.clip = ambientSound;
                audioSource.loop = true;
                audioSource.Play();
                DebugLog($"[Collectibles] Suono ambientale avviato per memory {gameObject.name}");
            }
        }
        else if (collectibleType == CollectibleType.Present)
        {
            // Setup standard per present
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0.5f; // Mix di 2D e 3D
            audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
            audioSource.minDistance = 5f;
            audioSource.maxDistance = 50f;
            audioSource.volume = 0.8f;
        }
    }
    
    private void StartVisualEffects()
    {
        // Avvia effetti CFXR per le memory
        if (collectibleType == CollectibleType.Memory && cfxrEffect != null)
        {
            cfxrEffect.PlayEffect();
            DebugLog($"[Collectibles] Effetto CFXR avviato per memory {gameObject.name}");
        }
    }
    
    private void RegisterWithManagers()
    {
        if (!isCollected)
        {
            // Registrazione automatica con SceneManager01 se disponibile
            if (SceneManager01.Instance != null)
            {
                SceneManager01.Instance.RegisterCollectible(this);
                DebugLog($"[Collectibles] Registrato con SceneManager01: {gameObject.name}");
            }
            else
            {
                DebugLog($"[Collectibles] SceneManager01 non disponibile per {gameObject.name}");
            }
        }
    }
    
    private void Update()
    {
        if (!isCollected)
        {
            HandleAnimations();
        }
    }
    
    private void LateUpdate()
    {
        if (!isCollected && useBillboard && collectibleType == CollectibleType.Memory)
        {
            HandleBillboard();
        }
    }
    
    private void HandleAnimations()
    {
        // Rotazione
        if (rotateObject)
        {
            transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);
        }
        
        // Effetto floating/bobbing - CORRETTO per non spostare dalla posizione originale
        if (floatUpDown)
        {
            float newY = startPosition.y + Mathf.Sin((Time.time + timeOffset) * floatSpeed) * floatStrength;
            transform.position = new Vector3(startPosition.x, newY, startPosition.z);
        }
    }
    
    private void HandleBillboard()
    {
        // Comportamento billboard per le memory
        if (targetCamera != null)
        {
            Vector3 targetPosition = targetCamera.transform.position;
            Vector3 direction = targetPosition - transform.position;
            
            // Calcola rotazione verso la camera
            if (direction.magnitude > 0.1f)
            {
                Quaternion lookRotation = Quaternion.LookRotation(direction);
                transform.rotation = lookRotation * Quaternion.Euler(0, 180, 0);
            }
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (isCollected) return;
        
        if (other.CompareTag("Player"))
        {
            DebugLog($"[Collectibles] Player entrato nel trigger di {gameObject.name}");
            CollectItem();
        }
    }
    
    public void CollectItem()
    {
        if (isCollected) 
        {
            DebugLog($"[Collectibles] Tentativo di raccogliere {gameObject.name} già raccolto!");
            return;
        }
        
        isCollected = true;
        DebugLog($"[Collectibles] Inizio raccolta di {gameObject.name}");
        
        // Disabilita il collider per prevenire trigger multipli
        if (triggerCollider != null)
            triggerCollider.enabled = false;
        
        // Notifica i manager appropriati
        NotifyManagers();
        
        // Ferma effetti visivi
        StopVisualEffects();
        
        // Nascondi oggetto immediatamente
        if (objectRenderer != null)
            objectRenderer.enabled = false;
        
        // Riproduci effetti di raccolta e gestisci distruzione
        PlayCollectionEffects();
        
        // Invoca evento per le classi derivate
        OnCollected?.Invoke(this);
        
        DebugLog($"[Collectibles] Raccolta completata per {gameObject.name}");
    }
    
    private void NotifyManagers()
    {
        // Prima strategia: Notifica SceneManager01 se disponibile e abilitato
        bool notifiedSceneManager = false;
        
        if (SceneManager01.Instance != null)
        {
            // Il SceneManager gestisce automaticamente la raccolta attraverso gli eventi
            // Non serve notifica diretta, il collectible è già registrato
            notifiedSceneManager = true;
            DebugLog($"[Collectibles] SceneManager01 disponibile per {gameObject.name}");
        }
        
        // Seconda strategia: Notifica sempre anche il PlayerCollectibleTracker
        // Questo assicura che funzioni con o senza SceneManager
        PlayerCollectibleTracker tracker = PlayerCollectibleTracker.Instance;
        if (tracker == null)
        {
            tracker = Object.FindFirstObjectByType<PlayerCollectibleTracker>();
        }
        
        if (tracker != null)
        {
            if (collectibleType == CollectibleType.Memory)
            {
                tracker.NotifyMemoryCollected();
                DebugLog($"[Collectibles] Memory notificata al tracker: {gameObject.name}");
            }
            else if (collectibleType == CollectibleType.Present)
            {
                tracker.NotifyPresentCollected();
                DebugLog($"[Collectibles] Present notificato al tracker: {gameObject.name}");
            }
        }
        else
        {
            Debug.LogWarning($"[Collectibles] PlayerCollectibleTracker non trovato per {gameObject.name}!");
        }
        
        // Se nessun manager è disponibile, logga l'errore
        if (!notifiedSceneManager && tracker == null)
        {
            Debug.LogError($"[Collectibles] ERRORE: Nessun manager disponibile per gestire la raccolta di {gameObject.name}!");
        }
    }
    
    private void StopVisualEffects()
    {
        if (collectibleType == CollectibleType.Memory && cfxrEffect != null)
        {
            cfxrEffect.StopEffect();
            DebugLog($"[Collectibles] Effetto CFXR fermato per {gameObject.name}");
        }
    }
    
    private void PlayCollectionEffects()
    {
        // Effetto visivo generico
        if (visualEffect != null)
        {
            GameObject effect = Instantiate(visualEffect, transform.position, transform.rotation);
            DebugLog($"[Collectibles] Effetto visivo istanziato per {gameObject.name}");
            
            // Pulisci l'effetto dopo un po' se non ha auto-distruzione
            Destroy(effect, 5f);
        }
        
        // Gestione audio
        HandleCollectionAudio();
    }
    
    private void HandleCollectionAudio()
    {
        if (audioSource != null)
        {
            // Ferma audio ambientale per le memory
            if (collectibleType == CollectibleType.Memory && ambientSound != null)
            {
                audioSource.Stop();
                DebugLog($"[Collectibles] Audio ambientale fermato per {gameObject.name}");
            }
            
            // Riproduci suono di raccolta
            if (collectSound != null)
            {
                audioSource.PlayOneShot(collectSound, collectVolume);
                DebugLog($"[Collectibles] Suono di raccolta riprodotto per {gameObject.name} - Volume: {collectVolume}");
                
                // Aspetta che l'audio finisca prima di distruggere
                StartCoroutine(DestroyAfterAudio());
            }
            else
            {
                DebugLog($"[Collectibles] Nessun suono di raccolta - distruzione immediata: {gameObject.name}");
                Destroy(gameObject, destroyDelay);
            }
        }
        else
        {
            // Fallback: riproduci suono al punto se disponibile
            if (collectSound != null)
            {
                AudioSource.PlayClipAtPoint(collectSound, transform.position, collectVolume);
                DebugLog($"[Collectibles] Suono riprodotto al punto per {gameObject.name}");
            }
            Destroy(gameObject, destroyDelay);
        }
    }
    
    private IEnumerator DestroyAfterAudio()
    {
        if (collectSound != null)
        {
            float duration = collectSound.length;
            DebugLog($"[Collectibles] Aspettando {duration:F2}s per l'audio prima di distruggere {gameObject.name}");
            yield return new WaitForSeconds(duration);
        }
        
        DebugLog($"[Collectibles] Distruggendo {gameObject.name}");
        Destroy(gameObject);
    }
    
    // ========== METODI PUBBLICI ==========
    
    public void ResetItem()
    {
        isCollected = false;
        gameObject.SetActive(true);
        
        // Riabilita componenti
        if (triggerCollider != null)
            triggerCollider.enabled = true;
        
        if (objectRenderer != null)
            objectRenderer.enabled = true;
        
        // Riavvia effetti per memory
        if (collectibleType == CollectibleType.Memory)
        {
            if (cfxrEffect != null)
                cfxrEffect.PlayEffect();
            
            if (ambientSound != null && audioSource != null)
            {
                audioSource.clip = ambientSound;
                audioSource.loop = true;
                audioSource.Play();
            }
        }
        
        DebugLog($"[Collectibles] Reset completato per {gameObject.name}");
    }
    
    public void ForceInitialize()
    {
        if (!hasBeenInitialized)
        {
            Initialize();
        }
    }
    
    // ========== GETTERS ==========
    
    public string GetName() => itemName;
    public int GetValue() => value;
    public bool IsCollected() => isCollected;
    public CollectibleType GetCollectibleType() => collectibleType;
    public Vector3 GetStartPosition() => startPosition;
    public bool IsInitialized() => hasBeenInitialized;
    
    // ========== SETTERS ==========
    
    public void SetCollectibleType(CollectibleType type)
    {
        CollectibleType oldType = collectibleType;
        collectibleType = type;
        
        // Configura automaticamente le impostazioni in base al tipo
        ApplyTypeSpecificSettings(type);
        
        // Riconfigura audio se già inizializzato e il tipo è cambiato
        if (hasBeenInitialized && oldType != type && audioSource != null)
        {
            ConfigureAudioForType();
        }
        
        DebugLog($"[Collectibles] Tipo cambiato da {oldType} a {type} per {gameObject.name}");
    }
    
    private void ApplyTypeSpecificSettings(CollectibleType type)
    {
        if (type == CollectibleType.Memory)
        {
            useBillboard = true;
            rotateObject = false; // Il billboard gestisce l'orientamento
            floatSpeed = 4f; // Movimento più mistico
            floatStrength = 1f; // Ampiezza maggiore
        }
        else if (type == CollectibleType.Present)
        {
            useBillboard = false;
            rotateObject = true; // I present ruotano per effetto carino
            floatSpeed = 2f; // Movimento più leggero
            floatStrength = 0.3f; // Ampiezza minore
        }
    }
    
    public void SetName(string name) 
    { 
        itemName = name;
        DebugLog($"[Collectibles] Nome cambiato in '{name}' per {gameObject.name}");
    }
    
    public void SetValue(int newValue) 
    { 
        value = newValue;
        DebugLog($"[Collectibles] Valore cambiato in {newValue} per {gameObject.name}");
    }
    
    // Setters per configurazione animazioni
    public void SetRotationSpeed(float speed)
    {
        rotationSpeed = speed;
        DebugLog($"[Collectibles] Velocità rotazione impostata a {speed} per {gameObject.name}");
    }
    
    public void SetFloatSettings(float speed, float strength)
    {
        floatSpeed = speed;
        floatStrength = strength;
        DebugLog($"[Collectibles] Float impostato: Speed={speed}, Strength={strength} per {gameObject.name}");
    }
    
    public void SetRotationEnabled(bool enabled)
    {
        rotateObject = enabled;
        DebugLog($"[Collectibles] Rotazione {(enabled ? "abilitata" : "disabilitata")} per {gameObject.name}");
    }
    
    public void SetFloatEnabled(bool enabled)
    {
        floatUpDown = enabled;
        DebugLog($"[Collectibles] Float {(enabled ? "abilitato" : "disabilitato")} per {gameObject.name}");
    }
    
    public void SetBillboardEnabled(bool enabled)
    {
        useBillboard = enabled;
        DebugLog($"[Collectibles] Billboard {(enabled ? "abilitato" : "disabilitato")} per {gameObject.name}");
    }
    
    public void SetTargetCamera(Camera camera)
    {
        targetCamera = camera;
        DebugLog($"[Collectibles] Camera target impostata per {gameObject.name}");
    }
    
    public void SetAudioSettings(AudioClip collect, AudioClip ambient = null, float volume = 0.3f)
    {
        collectSound = collect;
        ambientSound = ambient;
        collectVolume = volume;
        
        // Riapplica audio ambientale se è una memory già inizializzata
        if (hasBeenInitialized && collectibleType == CollectibleType.Memory && ambient != null && audioSource != null)
        {
            audioSource.clip = ambient;
            audioSource.loop = true;
            audioSource.Play();
        }
        
        DebugLog($"[Collectibles] Audio configurato per {gameObject.name}");
    }
    
    public void SetVisualEffects(GameObject effect, CFXR_EffectController cfxr = null)
    {
        visualEffect = effect;
        cfxrEffect = cfxr;
        
        // Avvia CFXR se è una memory già inizializzata
        if (hasBeenInitialized && collectibleType == CollectibleType.Memory && cfxr != null)
        {
            cfxr.PlayEffect();
        }
        
        DebugLog($"[Collectibles] Effetti visivi configurati per {gameObject.name}");
    }
    
    // ========== UTILITY E DEBUG ==========
    
    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log(message);
        }
    }
    
    public void EnableDebugLogs(bool enabled)
    {
        enableDebugLogs = enabled;
    }
    
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public void ValidateSetup()
    {
        string issues = "";
        
        if (triggerCollider == null)
            issues += "- Manca Collider per trigger\n";
        
        if (objectRenderer == null)
            issues += "- Manca Renderer per visualizzazione\n";
        
        if (collectibleType == CollectibleType.Memory && useBillboard && targetCamera == null)
            issues += "- Memory con billboard ma senza target camera\n";
        
        if (collectSound == null)
            issues += "- Manca audio di raccolta\n";
        
        if (collectibleType == CollectibleType.Memory && cfxrEffect == null)
            issues += "- Memory senza effetto CFXR\n";
        
        if (string.IsNullOrEmpty(issues))
        {
            Debug.Log($"[Collectibles] ✅ Setup corretto per {gameObject.name}");
        }
        else
        {
            Debug.LogWarning($"[Collectibles] ⚠️ Problemi setup per {gameObject.name}:\n{issues}");
        }
    }
    
    // Debug visual in editor
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private void OnDrawGizmos()
    {
        // Gizmo base per area di raccolta
        Color gizmoColor = collectibleType == CollectibleType.Memory ? Color.cyan : Color.green;
        Gizmos.color = gizmoColor;
        Gizmos.DrawWireSphere(transform.position, 1f);
        
        // Indicatore tipo
        Gizmos.color = collectibleType == CollectibleType.Memory ? Color.blue : Color.yellow;
        Vector3 indicatorPos = transform.position + Vector3.up * 2f;
        
        if (collectibleType == CollectibleType.Memory)
        {
            // Diamante per memory
            Gizmos.matrix = Matrix4x4.TRS(indicatorPos, Quaternion.Euler(45, 0, 45), Vector3.one * 0.3f);
            Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
            Gizmos.matrix = Matrix4x4.identity;
        }
        else
        {
            // Cubo per present
            Gizmos.DrawWireCube(indicatorPos, Vector3.one * 0.3f);
        }
    }
    
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private void OnDrawGizmosSelected()
    {
        // Info dettagliate quando selezionato
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, 1.5f);
        
        // Mostra range di movimento floating
        if (Application.isPlaying && floatUpDown)
        {
            Vector3 currentPos = transform.position;
            Vector3 maxPos = startPosition + Vector3.up * floatStrength;
            Vector3 minPos = startPosition - Vector3.up * floatStrength;
            
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(minPos, maxPos);
        }
        
        // Indicatore billboard per memory
        if (collectibleType == CollectibleType.Memory && useBillboard && targetCamera != null)
        {
            Gizmos.color = Color.yellow;
            Vector3 toCam = (targetCamera.transform.position - transform.position).normalized;
            Gizmos.DrawRay(transform.position, toCam * 2f);
        }
    }
    
    // Cleanup
    private void OnDestroy()
    {
        DebugLog($"[Collectibles] Distruzione di {gameObject.name}");
    }
}