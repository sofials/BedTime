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
    
    private bool memoryInitialized = false;
    private Vector3 memoryStartPosition; // Store our own start position as backup
    
    private void Awake()
    {
        // Store the position IMMEDIATELY before any other code runs
        memoryStartPosition = transform.position;
        
        // Force the type to Memory FIRST, before any other initialization
        SetCollectibleType(CollectibleType.Memory);
        
        // Configure memory-specific settings
        ApplyMemorySettings();
        
        Debug.Log($"[Memories] Awake configuration completed for {gameObject.name} at position {memoryStartPosition}");
    }
    
    private void Start()
    {
        // Make sure base initialization is complete first
        if (!IsInitialized())
        {
            ForceInitialize();
        }
        
        // Verify position hasn't drifted
        VerifyPosition();
        
        // Now do memory-specific initialization
        InitializeMemorySpecifics();
        
        Debug.Log($"[Memories] '{GetName()}' fully initialized as Memory at {transform.position}");
    }
    
    private void VerifyPosition()
    {
        // Check if position has drifted from where we expect it
        Vector3 currentPos = transform.position;
        float drift = Vector3.Distance(currentPos, memoryStartPosition);
        
        if (drift > 0.1f) // If drifted more than 0.1 units
        {
            Debug.LogWarning($"[Memories] Position drift detected for {gameObject.name}: {drift:F3} units. Correcting...");
            transform.position = memoryStartPosition;
            
            // Force the base class to update its start position too
            ForceUpdateStartPosition();
        }
    }
    
    private void ForceUpdateStartPosition()
    {
        // This ensures the base class has the correct start position
        // We'll call this through reflection or add a public method to base class
        
        // For now, let's set transform position and hope base class updates
        transform.position = memoryStartPosition;
    }
    
    private void InitializeMemorySpecifics()
    {
        if (memoryInitialized) return;
        
        memoryInitialized = true;
        
        // Ensure object is visible and active
        gameObject.SetActive(true);
        
        // Double-check position is correct
        if (Vector3.Distance(transform.position, memoryStartPosition) > 0.01f)
        {
            transform.position = memoryStartPosition;
            Debug.Log($"[Memories] Position corrected to {memoryStartPosition}");
        }
        
        // Register with SceneManager
        RegisterWithSceneManager();
        
        // Subscribe to collection event for memory-specific logic
        OnCollected.AddListener(OnMemoryCollected);
        
        Debug.Log($"[Memories] Memory-specific initialization complete for {GetName()}");
    }
    
    // Override the floating behavior to ensure position stability
    private void Update()
    {
        // Let base class handle animations, but monitor for position drift
        if (!IsCollected() && IsInitialized())
        {
            // Check for unexpected position changes (excluding Y for floating)
            Vector2 currentXZ = new Vector2(transform.position.x, transform.position.z);
            Vector2 expectedXZ = new Vector2(memoryStartPosition.x, memoryStartPosition.z);
            
            float drift = Vector2.Distance(currentXZ, expectedXZ);
            if (drift > 0.1f)
            {
                Debug.LogWarning($"[Memories] XZ drift detected: {drift:F3}. Correcting {gameObject.name}");
                
                // Preserve the Y (floating) but fix X and Z
                float currentY = transform.position.y;
                transform.position = new Vector3(memoryStartPosition.x, currentY, memoryStartPosition.z);
            }
        }
    }
    
    private void ApplyMemorySettings()
    {
        // Memories always use billboard and don't rotate
        SetBillboardEnabled(true);
        SetRotationEnabled(false);
        
        if (customFloatSettings)
        {
            SetFloatSettings(memoryFloatSpeed, memoryFloatStrength);
        }
        else
        {
            // Default settings for memories - broader, more mystical movement
            SetFloatSettings(4f, 1f);
        }
        
        // Ensure they float
        SetFloatEnabled(true);
        
        Debug.Log($"[Memories] Memory settings applied to {gameObject.name}");
    }
    
    private void RegisterWithSceneManager()
    {
        if (!IsCollected())
        {
            try
            {
                if (SceneManager01.Instance != null)
                {
                    SceneManager01.Instance.RegisterCollectible(this);
                    Debug.Log($"[Memories] Registered with SceneManager01: {GetName()}");
                }
                else
                {
                    Debug.LogWarning($"[Memories] SceneManager01 not found for {GetName()}");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Memories] Failed to register with SceneManager01: {e.Message}");
            }
        }
    }
    
    private void OnMemoryCollected(Collectibles collectible)
    {
        Debug.Log($"[Memories] Memory '{GetName()}' collected!");
        
        // The notification to PlayerCollectibleTracker is handled automatically 
        // by the base Collectibles class in the NotifyManagers() method
        
        // Memory-specific logic
        HandleMemorySpecificLogic();
    }
    
    private void HandleMemorySpecificLogic()
    {
        // Memory-specific effects
        PlayMemoryCollectionEffect();
        
        // Integration with narrative systems
        NotifyNarrativeSystems();
        
        Debug.Log($"[Memories] Specific logic executed for {GetName()}");
    }
    
    private void PlayMemoryCollectionEffect()
    {
        // Memory-specific visual effects
        if (memoryCollectionAura != null)
        {
            GameObject aura = Instantiate(memoryCollectionAura, transform.position, Quaternion.identity);
            Destroy(aura, 5f); // Longer effect for memories
        }
        
        // Memory-specific audio - more ethereal and mysterious
        if (memoryEchoSound != null)
        {
            AudioSource.PlayClipAtPoint(memoryEchoSound, transform.position, 0.5f);
        }
        
        Debug.Log($"[Memories] Memory effects played for {GetName()}");
    }
    
    private void NotifyNarrativeSystems()
    {
        // Notify narrative systems specific to memories
        
        // Example: Narrative/dialogue system
        // if (NarrativeManager.Instance != null)
        // {
        //     NarrativeManager.Instance.UnlockMemory(GetName());
        // }
        
        // Example: Cutscene system
        // if (CutsceneManager.Instance != null)
        // {
        //     CutsceneManager.Instance.TriggerMemoryScene(GetValue());
        // }
        
        // Example: Journal/lore system
        // if (JournalManager.Instance != null)
        // {
        //     JournalManager.Instance.AddMemoryEntry(GetName(), GetValue());
        // }
    }
    
    // Preset configurations for different types of memories
    public void ConfigureAsStoryMemory()
    {
        SetName("Story Fragment");
        SetValue(1);
        SetFloatSettings(3f, 0.8f);
        Debug.Log("[Memories] Configured as narrative memory");
    }
    
    public void ConfigureAsLoreMemory()
    {
        SetName("Ancient Knowledge");
        SetValue(5);
        SetFloatSettings(5f, 1.2f);
        Debug.Log("[Memories] Configured as lore memory");
    }
    
    public void ConfigureAsSecretMemory()
    {
        SetName("Hidden Truth");
        SetValue(10);
        SetFloatSettings(6f, 1.5f);
        Debug.Log("[Memories] Configured as secret memory");
    }
    
    // Method to set memory-specific properties
    public void SetMemoryProperties(string newName, int importance, float mysticalLevel = 1f)
    {
        SetName(newName);
        SetValue(importance);
        SetFloatSettings(3f + mysticalLevel, 0.5f + (mysticalLevel * 0.3f));
    }
    
    // Override reset to ensure memory settings are maintained
    public new void ResetItem()
    {
        // Reset to our stored position first
        transform.position = memoryStartPosition;
        
        base.ResetItem();
        
        // Reapply memory settings after reset
        ApplyMemorySettings();
        
        Debug.Log($"[Memories] Memory reset completed for {GetName()} at {memoryStartPosition}");
    }
    
    // Force memory to be visible (debugging)
    [ContextMenu("Force Memory Visible")]
    public void ForceMemoryVisible()
    {
        // Reset position first
        transform.position = memoryStartPosition;
        
        ForceVisible();
        
        // Ensure memory-specific settings
        SetCollectibleType(CollectibleType.Memory);
        ApplyMemorySettings();
        
        Debug.Log($"[Memories] Forced memory visible: {gameObject.name} at {memoryStartPosition}");
    }
    
    // Debug method to manually set the correct position
    [ContextMenu("Reset to Start Position")]
    public void ResetToStartPosition()
    {
        transform.position = memoryStartPosition;
        Debug.Log($"[Memories] Position reset to {memoryStartPosition} for {gameObject.name}");
    }
    
    // Debug method to update start position to current position
    [ContextMenu("Update Start Position to Current")]
    public void UpdateStartPositionToCurrent()
    {
        memoryStartPosition = transform.position;
        Debug.Log($"[Memories] Start position updated to {memoryStartPosition} for {gameObject.name}");
    }
    
    private void OnDestroy()
    {
        // Event cleanup
        if (OnCollected != null)
        {
            OnCollected.RemoveListener(OnMemoryCollected);
        }
        Debug.Log($"[Memories] Event cleanup for {GetName()}");
    }
    
    // Debug and utility
    private void OnDrawGizmos()
    {
        // Collection area gizmo
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 1.2f);
        
        // Show the intended start position
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(memoryStartPosition, Vector3.one * 0.2f);
        
        // Memory indicator with mystical effect
        Gizmos.color = memoryGlowColor;
        Gizmos.matrix = Matrix4x4.TRS(transform.position + Vector3.up * 2.5f, 
                                     Quaternion.Euler(45, 0, 45), 
                                     Vector3.one * 0.4f);
        Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
        Gizmos.matrix = Matrix4x4.identity;
        
        // Visibility warning
        if (!gameObject.activeInHierarchy)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, 2f);
        }
        
        // Position drift warning
        float drift = Vector3.Distance(transform.position, memoryStartPosition);
        if (drift > 0.1f)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(memoryStartPosition, transform.position);
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        // Detailed info when selected
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, 2f);
        
        // Show start position clearly
        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(memoryStartPosition, 0.5f);
        
        // Visualize broader floating range for memories
        if (Application.isPlaying)
        {
            Vector3 maxFloatPos = memoryStartPosition;
            maxFloatPos.y += memoryFloatStrength;
            Vector3 minFloatPos = memoryStartPosition;
            minFloatPos.y -= memoryFloatStrength;
            
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(minFloatPos, maxFloatPos);
            
            // Billboard indicators
            Gizmos.color = Color.yellow;
            if (Camera.main != null)
            {
                Vector3 toCam = (Camera.main.transform.position - transform.position).normalized;
                Gizmos.DrawRay(transform.position, toCam * 1.5f);
            }
        }
    }
}