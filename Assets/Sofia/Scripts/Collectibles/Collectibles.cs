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
    
    // Events
    [System.Serializable]
    public class CollectibleEvent : UnityEvent<Collectibles> { }
    public CollectibleEvent OnCollected = new CollectibleEvent();
    
    private Vector3 startPosition;
    private AudioSource audioSource;
    private Renderer objectRenderer;
    private Collider triggerCollider;
    
    private void Start()
    {
        InitializeComponents();
        SetupCollider();
        SetupAudio();
        StartVisualEffects();
        RegisterWithManagers();
    }
    
    private void InitializeComponents()
    {
        startPosition = transform.position;
        objectRenderer = GetComponent<Renderer>();
        triggerCollider = GetComponent<Collider>();
        
        if (targetCamera == null)
            targetCamera = Camera.main;
    }
    
    private void SetupCollider()
    {
        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
            if (triggerCollider is MeshCollider meshCol)
            {
                meshCol.convex = true;
            }
        }
    }
    
    private void SetupAudio()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        // Configure AudioSource based on type
        if (collectibleType == CollectibleType.Memory)
        {
            // 3D spatial audio for memories
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f;
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
            }
        }
        else
        {
            // Standard setup for presents
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0.5f; // Mix of 2D and 3D
        }
    }
    
    private void StartVisualEffects()
    {
        // Start CFXR effect for memories
        if (collectibleType == CollectibleType.Memory && cfxrEffect != null)
        {
            cfxrEffect.PlayEffect();
            Debug.Log($"[Collectibles] CFXR effect started on {gameObject.name}");
        }
    }
    
    private void RegisterWithManagers()
    {
        if (!isCollected)
        {
            if (collectibleType == CollectibleType.Present && SceneManager01.Instance != null)
            {
                // Register with present manager if it exists
                // SceneManager01.Instance.RegisterPresent(this);
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
            // Billboard behavior for memories
            if (targetCamera != null)
            {
                transform.LookAt(targetCamera.transform);
                transform.Rotate(0, 180, 0);
            }
        }
    }
    
    private void HandleAnimations()
    {
        // Rotation
        if (rotateObject)
        {
            transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);
        }
        
        // Floating/Bobbing effect
        if (floatUpDown)
        {
            float newY = startPosition.y + Mathf.Sin(Time.time * floatSpeed) * floatStrength;
            transform.position = new Vector3(startPosition.x, newY, startPosition.z);
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (isCollected) return;
        
        if (other.CompareTag("Player"))
        {
            Debug.Log($"[Collectibles] Player entered trigger of {gameObject.name}");
            CollectItem();
        }
    }
    
    public void CollectItem()
    {
        if (isCollected) return;
        
        isCollected = true;
        Debug.Log($"[Collectibles] CollectItem called on {gameObject.name}");
        
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
        
        // Invoke event
        OnCollected?.Invoke(this);
    }
    
    private void NotifyManagers()
    {
        if (collectibleType == CollectibleType.Memory)
        {
            PlayerCollectibleTracker collector = Object.FindFirstObjectByType<PlayerCollectibleTracker>();
            if (collector != null)
            {
                collector.NotifyMemoryCollected();
            }
            else
            {
                Debug.LogWarning("[Collectibles] PlayerMemoryCollector not found!");
            }
        }
        // Add present manager notification here if needed
    }
    
    private void StopVisualEffects()
    {
        if (collectibleType == CollectibleType.Memory && cfxrEffect != null)
        {
            cfxrEffect.StopEffect();
            Debug.Log($"[Collectibles] CFXR effect stopped on {gameObject.name}");
        }
    }
    
    private void PlayCollectionEffects()
    {
        // Visual effect
        if (visualEffect != null)
        {
            Instantiate(visualEffect, transform.position, transform.rotation);
        }
        
        // Audio handling
        HandleCollectionAudio();
    }
    
    private void HandleCollectionAudio()
    {
        if (audioSource != null)
        {
            // Stop ambient sound for memories
            if (collectibleType == CollectibleType.Memory)
            {
                audioSource.Stop();
            }
            
            // Play collection sound
            if (collectSound != null)
            {
                audioSource.PlayOneShot(collectSound, collectVolume);
                Debug.Log($"[Collectibles] Playing collection sound with volume={collectVolume}");
                
                // Wait for audio to finish before destroying
                StartCoroutine(DestroyAfterAudio());
            }
            else
            {
                Debug.LogWarning($"[Collectibles] No collection sound, destroying immediately: {gameObject.name}");
                Destroy(gameObject, destroyDelay);
            }
        }
        else
        {
            // Fallback: play sound at point
            if (collectSound != null)
            {
                AudioSource.PlayClipAtPoint(collectSound, transform.position, collectVolume);
            }
            Destroy(gameObject, destroyDelay);
        }
    }
    
    private IEnumerator DestroyAfterAudio()
    {
        if (collectSound != null)
        {
            float duration = collectSound.length;
            Debug.Log($"[Collectibles] Waiting {duration} seconds for audio before destroying {gameObject.name}");
            yield return new WaitForSeconds(duration);
        }
        
        Debug.Log($"[Collectibles] Destroying {gameObject.name}");
        Destroy(gameObject);
    }
    
    public void ResetItem()
    {
        isCollected = false;
        gameObject.SetActive(true);
        
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
    }
    
    // Getters
    public string GetName() => itemName;
    public int GetValue() => value;
    public bool IsCollected() => isCollected;
    public CollectibleType GetCollectibleType() => collectibleType;
    
    // Setters for runtime configuration
    public void SetCollectibleType(CollectibleType type)
    {
        collectibleType = type;
        
        // Configura automaticamente le impostazioni in base al tipo
        if (type == CollectibleType.Memory)
        {
            useBillboard = true;
            rotateObject = false; // Le memory di solito non ruotano
        }
        else if (type == CollectibleType.Present)
        {
            useBillboard = false;
            rotateObject = true; // I present di solito ruotano
        }
    }
    
    public void SetName(string name) => itemName = name;
    public void SetValue(int newValue) => value = newValue;
}