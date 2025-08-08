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
    private float timeOffset;
    
    private void Awake()
    {
        // Store the start position immediately
        startPosition = transform.position;
        
        // Generate time offset for floating animation
        timeOffset = Random.Range(0f, Mathf.PI * 2f);
        
        // Pre-initialize components
        PreInitialize();
        
        DebugLog($"[Collectibles] Awake completed for {gameObject.name}");
    }
    
    private void Start()
    {
        // Complete initialization
        Initialize();
    }
    
    private void PreInitialize()
    {
        // Get essential components
        objectRenderer = GetComponent<Renderer>();
        triggerCollider = GetComponent<Collider>();
        
        // Ensure object is visible and active
        gameObject.SetActive(true);
        
        // Set target camera if not assigned
        if (targetCamera == null)
            targetCamera = Camera.main;
            
        DebugLog($"[Collectibles] Pre-initialization completed for {gameObject.name}");
    }
    
    private void Initialize()
    {
        if (hasBeenInitialized) return;
        
        hasBeenInitialized = true;
        
        InitializeComponents();
        SetupCollider();
        SetupAudio();
        StartVisualEffects();
        RegisterWithManagers();
        
        DebugLog($"[Collectibles] Full initialization completed for {gameObject.name} - Type: {collectibleType}");
    }
    
    private void InitializeComponents()
    {
        // Verify essential components
        if (objectRenderer == null)
        {
            objectRenderer = GetComponent<Renderer>();
            if (objectRenderer == null)
            {
                Debug.LogWarning($"[Collectibles] No Renderer found on {gameObject.name} - object may not be visible!");
                
                // Try to find renderer in children
                objectRenderer = GetComponentInChildren<Renderer>();
                if (objectRenderer != null)
                {
                    DebugLog($"[Collectibles] Found Renderer in children for {gameObject.name}");
                }
            }
        }
        
        // Ensure renderer is enabled
        if (objectRenderer != null)
        {
            objectRenderer.enabled = true;
        }
        
        if (triggerCollider == null)
        {
            triggerCollider = GetComponent<Collider>();
            if (triggerCollider == null)
            {
                Debug.LogWarning($"[Collectibles] No Collider found on {gameObject.name} - adding default SphereCollider");
                
                // Auto-create a collider if missing
                triggerCollider = gameObject.AddComponent<SphereCollider>();
                ((SphereCollider)triggerCollider).radius = 1f;
            }
        }
    }
    
    private void SetupCollider()
    {
        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
            triggerCollider.enabled = true; // Ensure collider is enabled
            
            // Ensure MeshColliders are convex for triggers
            if (triggerCollider is MeshCollider meshCol)
            {
                meshCol.convex = true;
            }
            
            DebugLog($"[Collectibles] Collider configured for {gameObject.name}");
        }
        else
        {
            Debug.LogError($"[Collectibles] ERROR: No collider on {gameObject.name}! Collectible won't be collectable.");
        }
    }
    
    private void SetupAudio()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        // Configure audio based on collectible type
        ConfigureAudioForType();
        
        DebugLog($"[Collectibles] Audio configured for {gameObject.name} - Type: {collectibleType}");
    }
    
    private void ConfigureAudioForType()
    {
        if (audioSource == null) return;
        
        if (collectibleType == CollectibleType.Memory)
        {
            // 3D spatial audio for memories
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f; // Full 3D
            audioSource.rolloffMode = AudioRolloffMode.Linear;
            audioSource.minDistance = 20f;
            audioSource.maxDistance = 100f;
            audioSource.volume = 1f;
            
            // Start ambient sound for memories
            if (ambientSound != null)
            {
                audioSource.clip = ambientSound;
                audioSource.loop = true;
                audioSource.Play();
                DebugLog($"[Collectibles] Ambient sound started for memory {gameObject.name}");
            }
        }
        else if (collectibleType == CollectibleType.Present)
        {
            // Standard setup for presents
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0.5f; // Mix of 2D and 3D
            audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
            audioSource.minDistance = 5f;
            audioSource.maxDistance = 50f;
            audioSource.volume = 0.8f;
        }
    }
    
    private void StartVisualEffects()
    {
        // Start CFXR effects for memories
        if (collectibleType == CollectibleType.Memory && cfxrEffect != null)
        {
            cfxrEffect.PlayEffect();
            DebugLog($"[Collectibles] CFXR effect started for memory {gameObject.name}");
        }
    }
    
    private void RegisterWithManagers()
    {
        if (!isCollected)
        {
            // Auto-register with SceneManager01 if available
            try
            {
                if (SceneManager01.Instance != null)
                {
                    SceneManager01.Instance.RegisterCollectible(this);
                    DebugLog($"[Collectibles] Registered with SceneManager01: {gameObject.name}");
                }
                else
                {
                    DebugLog($"[Collectibles] SceneManager01 not available for {gameObject.name}");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Collectibles] Failed to register with SceneManager01: {e.Message}");
            }
        }
    }
    
    private void Update()
    {
        if (!isCollected && hasBeenInitialized)
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
        // Rotation
        if (rotateObject)
        {
            transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);
        }
        
        // Floating/bobbing effect - FIXED to maintain original X and Z position
        if (floatUpDown)
        {
            float newY = startPosition.y + Mathf.Sin((Time.time + timeOffset) * floatSpeed) * floatStrength;
            // CRITICAL FIX: Always use startPosition.x and startPosition.z to prevent drift
            transform.position = new Vector3(startPosition.x, newY, startPosition.z);
        }
    }
    
    private void HandleBillboard()
    {
        // Billboard behavior for memories
        if (targetCamera != null)
        {
            Vector3 targetPosition = targetCamera.transform.position;
            Vector3 direction = targetPosition - transform.position;
            
            // Calculate rotation towards camera
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
    
    Debug.Log($"[Collectibles] TRIGGER DETECTED: {other.name} with tag '{other.tag}'"); // ← AGGIUNGI QUESTO
    
    if (other.CompareTag("Player"))
    {
        Debug.Log($"[Collectibles] PLAYER CONFIRMED - collecting {gameObject.name}"); // ← E QUESTO
        CollectItem();
    }
    else
    {
        Debug.Log($"[Collectibles] NOT PLAYER - ignoring {other.name}"); // ← E QUESTO
    }
}
    
    public void CollectItem()
    {
        if (isCollected) 
        {
            DebugLog($"[Collectibles] Attempt to collect already collected {gameObject.name}!");
            return;
        }
        
        isCollected = true;
        DebugLog($"[Collectibles] Starting collection of {gameObject.name}");
        
        // Disable collider to prevent multiple triggers
        if (triggerCollider != null)
            triggerCollider.enabled = false;
        
        // Notify appropriate managers
        NotifyManagers();
        
        // Stop visual effects
        StopVisualEffects();
        
        // Hide object immediately
        if (objectRenderer != null)
            objectRenderer.enabled = false;
        
        // Play collection effects and handle destruction
        PlayCollectionEffects();
        
        // Invoke event for derived classes
        OnCollected?.Invoke(this);
        
        DebugLog($"[Collectibles] Collection completed for {gameObject.name}");
    }
    
    private void NotifyManagers()
    {
        // Prima strategia: Notifica SceneManager01 se disponibile
        bool notifiedSceneManager = false;
        
        if (SceneManager01.Instance != null)
        {
            try
            {
                if (collectibleType == CollectibleType.Memory)
                {
                    // Passa il nome della memory al SceneManager
                    SceneManager01.Instance.NotifyMemoryCollected(itemName);
                    DebugLog($"[Collectibles] Memory '{itemName}' notificata al SceneManager01");
                }
                else if (collectibleType == CollectibleType.Present)
                {
                    SceneManager01.Instance.NotifyPresentCollected();
                    DebugLog($"[Collectibles] Present notificato al SceneManager01: {gameObject.name}");
                }
                notifiedSceneManager = true;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Collectibles] Errore notificando SceneManager01: {e.Message}");
            }
        }
        
        // Seconda strategia: Notifica anche il PlayerCollectibleTracker
        // Questo assicura che funzioni con o senza SceneManager
        PlayerCollectibleTracker tracker = PlayerCollectibleTracker.Instance;
        if (tracker == null)
        {
            tracker = Object.FindFirstObjectByType<PlayerCollectibleTracker>();
        }
        
        if (tracker != null)
        {
            try
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
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Collectibles] Errore notificando PlayerCollectibleTracker: {e.Message}");
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
            DebugLog($"[Collectibles] CFXR effect stopped for {gameObject.name}");
        }
    }
    
    private void PlayCollectionEffects()
    {
        // Generic visual effect
        if (visualEffect != null)
        {
            GameObject effect = Instantiate(visualEffect, transform.position, transform.rotation);
            DebugLog($"[Collectibles] Visual effect instantiated for {gameObject.name}");
            
            // Clean up effect after a while if it doesn't have auto-destruction
            Destroy(effect, 5f);
        }
        
        // Handle audio
        HandleCollectionAudio();
    }
    
    private void HandleCollectionAudio()
    {
        if (audioSource != null)
        {
            // Stop ambient audio for memories
            if (collectibleType == CollectibleType.Memory && ambientSound != null)
            {
                audioSource.Stop();
                DebugLog($"[Collectibles] Ambient audio stopped for {gameObject.name}");
            }
            
            // Play collection sound
            if (collectSound != null)
            {
                audioSource.PlayOneShot(collectSound, collectVolume);
                DebugLog($"[Collectibles] Collection sound played for {gameObject.name} - Volume: {collectVolume}");
                
                // Wait for audio to finish before destroying
                StartCoroutine(DestroyAfterAudio());
            }
            else
            {
                DebugLog($"[Collectibles] No collection sound - immediate destruction: {gameObject.name}");
                Destroy(gameObject, destroyDelay);
            }
        }
        else
        {
            // Fallback: play sound at point if available
            if (collectSound != null)
            {
                AudioSource.PlayClipAtPoint(collectSound, transform.position, collectVolume);
                DebugLog($"[Collectibles] Sound played at point for {gameObject.name}");
            }
            Destroy(gameObject, destroyDelay);
        }
    }
    
    private IEnumerator DestroyAfterAudio()
    {
        if (collectSound != null)
        {
            float duration = collectSound.length;
            DebugLog($"[Collectibles] Waiting {duration:F2}s for audio before destroying {gameObject.name}");
            yield return new WaitForSeconds(duration);
        }
        
        DebugLog($"[Collectibles] Destroying {gameObject.name}");
        Destroy(gameObject);
    }
    
    // ========== PUBLIC METHODS ==========
    
    public void ResetItem()
    {
        isCollected = false;
        gameObject.SetActive(true);
        
        // Re-enable components
        if (triggerCollider != null)
            triggerCollider.enabled = true;
        
        if (objectRenderer != null)
            objectRenderer.enabled = true;
        
        // Restart effects for memories
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
        
        DebugLog($"[Collectibles] Reset completed for {gameObject.name}");
    }
    
    public void ForceInitialize()
    {
        if (!hasBeenInitialized)
        {
            Initialize();
        }
    }
    
    // Force object to be visible (useful for debugging)
    [ContextMenu("Force Visible")]
    public void ForceVisible()
    {
        gameObject.SetActive(true);
        
        if (objectRenderer != null)
        {
            objectRenderer.enabled = true;
        }
        else
        {
            objectRenderer = GetComponent<Renderer>();
            if (objectRenderer == null)
                objectRenderer = GetComponentInChildren<Renderer>();
            
            if (objectRenderer != null)
                objectRenderer.enabled = true;
        }
        
        Debug.Log($"[Collectibles] Forced visible: {gameObject.name}");
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
        
        // Automatically configure settings based on type
        ApplyTypeSpecificSettings(type);
        
        // Reconfigure audio if already initialized and type changed
        if (hasBeenInitialized && oldType != type && audioSource != null)
        {
            ConfigureAudioForType();
        }
        
        DebugLog($"[Collectibles] Type changed from {oldType} to {type} for {gameObject.name}");
    }
    
    private void ApplyTypeSpecificSettings(CollectibleType type)
    {
        if (type == CollectibleType.Memory)
        {
            useBillboard = true;
            rotateObject = false; // Billboard handles orientation
            floatSpeed = 4f; // More mystical movement
            floatStrength = 1f; // Greater amplitude
        }
        else if (type == CollectibleType.Present)
        {
            useBillboard = false;
            rotateObject = true; // Presents rotate for nice effect
            floatSpeed = 2f; // Lighter movement
            floatStrength = 0.3f; // Smaller amplitude
        }
    }
    
    public void SetName(string name) 
    { 
        itemName = name;
        DebugLog($"[Collectibles] Name changed to '{name}' for {gameObject.name}");
    }
    
    public void SetValue(int newValue) 
    { 
        value = newValue;
        DebugLog($"[Collectibles] Value changed to {newValue} for {gameObject.name}");
    }
    
    // Setters for animation configuration
    public void SetRotationSpeed(float speed)
    {
        rotationSpeed = speed;
        DebugLog($"[Collectibles] Rotation speed set to {speed} for {gameObject.name}");
    }
    
    public void SetFloatSettings(float speed, float strength)
    {
        floatSpeed = speed;
        floatStrength = strength;
        DebugLog($"[Collectibles] Float set: Speed={speed}, Strength={strength} for {gameObject.name}");
    }
    
    public void SetRotationEnabled(bool enabled)
    {
        rotateObject = enabled;
        DebugLog($"[Collectibles] Rotation {(enabled ? "enabled" : "disabled")} for {gameObject.name}");
    }
    
    public void SetFloatEnabled(bool enabled)
    {
        floatUpDown = enabled;
        DebugLog($"[Collectibles] Float {(enabled ? "enabled" : "disabled")} for {gameObject.name}");
    }
    
    public void SetBillboardEnabled(bool enabled)
    {
        useBillboard = enabled;
        DebugLog($"[Collectibles] Billboard {(enabled ? "enabled" : "disabled")} for {gameObject.name}");
    }
    
    public void SetTargetCamera(Camera camera)
    {
        targetCamera = camera;
        DebugLog($"[Collectibles] Target camera set for {gameObject.name}");
    }
    
    public void SetAudioSettings(AudioClip collect, AudioClip ambient = null, float volume = 0.3f)
    {
        collectSound = collect;
        ambientSound = ambient;
        collectVolume = volume;
        
        // Reapply ambient audio if it's an already initialized memory
        if (hasBeenInitialized && collectibleType == CollectibleType.Memory && ambient != null && audioSource != null)
        {
            audioSource.clip = ambient;
            audioSource.loop = true;
            audioSource.Play();
        }
        
        DebugLog($"[Collectibles] Audio configured for {gameObject.name}");
    }
    
    public void SetVisualEffects(GameObject effect, CFXR_EffectController cfxr = null)
    {
        visualEffect = effect;
        cfxrEffect = cfxr;
        
        // Start CFXR if it's an already initialized memory
        if (hasBeenInitialized && collectibleType == CollectibleType.Memory && cfxr != null)
        {
            cfxr.PlayEffect();
        }
        
        DebugLog($"[Collectibles] Visual effects configured for {gameObject.name}");
    }
    
    // ========== UTILITY AND DEBUG ==========
    
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
    
    [ContextMenu("Validate Setup")]
    public void ValidateSetup()
    {
        string issues = "";
        
        if (triggerCollider == null)
            issues += "- Missing Collider for trigger\n";
        
        if (objectRenderer == null)
            issues += "- Missing Renderer for visualization\n";
        
        if (collectibleType == CollectibleType.Memory && useBillboard && targetCamera == null)
            issues += "- Memory with billboard but no target camera\n";
        
        if (collectSound == null)
            issues += "- Missing collection audio\n";
        
        if (collectibleType == CollectibleType.Memory && cfxrEffect == null)
            issues += "- Memory without CFXR effect\n";
        
        if (!gameObject.activeInHierarchy)
            issues += "- GameObject is not active in hierarchy\n";
        
        if (objectRenderer != null && !objectRenderer.enabled)
            issues += "- Renderer is disabled\n";
        
        if (string.IsNullOrEmpty(issues))
        {
            Debug.Log($"[Collectibles] ✅ Setup correct for {gameObject.name}");
        }
        else
        {
            Debug.LogWarning($"[Collectibles] ⚠️ Setup issues for {gameObject.name}:\n{issues}");
        }
    }
    
    // Debug visual in editor
    private void OnDrawGizmos()
    {
        // Base gizmo for collection area
        Color gizmoColor = collectibleType == CollectibleType.Memory ? Color.cyan : Color.green;
        Gizmos.color = gizmoColor;
        Gizmos.DrawWireSphere(transform.position, 1f);
        
        // Type indicator
        Gizmos.color = collectibleType == CollectibleType.Memory ? Color.blue : Color.yellow;
        Vector3 indicatorPos = transform.position + Vector3.up * 2f;
        
        if (collectibleType == CollectibleType.Memory)
        {
            // Diamond for memory
            Gizmos.matrix = Matrix4x4.TRS(indicatorPos, Quaternion.Euler(45, 0, 45), Vector3.one * 0.3f);
            Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
            Gizmos.matrix = Matrix4x4.identity;
        }
        else
        {
            // Cube for present
            Gizmos.DrawWireCube(indicatorPos, Vector3.one * 0.3f);
        }
        
        // Visibility indicator
        if (!gameObject.activeInHierarchy)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, 1.5f);
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        // Detailed info when selected
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, 1.5f);
        
        // Show floating movement range
        if (Application.isPlaying && floatUpDown)
        {
            Vector3 currentPos = transform.position;
            Vector3 maxPos = startPosition + Vector3.up * floatStrength;
            Vector3 minPos = startPosition - Vector3.up * floatStrength;
            
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(minPos, maxPos);
        }
        
        // Billboard indicator for memories
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
        DebugLog($"[Collectibles] Destroying {gameObject.name}");
    }
}