using UnityEngine;
using System.Collections;

public class Memories : MonoBehaviour
{
    [Header("Billboard")]
    public Camera targetCamera;
    
    [Header("Floating Effect")]
    public float floatStrength = 1f;
    public float floatSpeed = 4f;
    
    [Header("Audio")]
    public AudioClip collectSound;
    public AudioClip ambientSound;
    public float collectVolume = 0.3f;
    
    [Header("Visual Effects")]
    public CFXR_EffectController defaultEffect; // Drag l'effetto CFXR qui
    
    private Vector3 startPos;
    private bool isCollected = false;
    private Renderer objectRenderer;
    private Collider triggerCollider;
    private AudioSource audioSource;
    
    void Start()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;
            
        startPos = transform.position;
        objectRenderer = GetComponent<Renderer>();
        triggerCollider = GetComponent<Collider>();
        
        // Setup del collider se non configurato
        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
            if (triggerCollider is MeshCollider meshCol)
            {
                meshCol.convex = true;
            }
        }
        
        // Setup AudioSource con impostazioni specifiche
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        // Configurazione AudioSource per ambientale
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1f; // 3D completo
        audioSource.rolloffMode = AudioRolloffMode.Linear;
        audioSource.minDistance = 20f; // Min distance
        audioSource.maxDistance = 100f; // Max distance
        audioSource.volume = 1f; // Volume massimo
        
        // Avvia audio ambientale
        if (ambientSound != null)
        {
            audioSource.clip = ambientSound;
            audioSource.loop = true;
            audioSource.Play();
        }
        
        // Avvia l'effetto visuale di default
        if (defaultEffect != null)
        {
            defaultEffect.PlayEffect();
            Debug.Log($"[Memories] Effetto CFXR avviato su {gameObject.name}");
        }
    }
    
    void Update()
    {
        if (!isCollected)
        {
            // Effetto fluttuante
            FloatingEffect();
        }
    }
    
    void LateUpdate()
    {
        if (!isCollected)
        {
            // Billboard behavior
            transform.LookAt(targetCamera.transform);
            transform.Rotate(0, 180, 0);
        }
    }
    
    void FloatingEffect()
    {
        // Movimento su e giù sinusoidale
        float newY = startPos.y + Mathf.Sin(Time.time * floatSpeed) * floatStrength;
        transform.position = new Vector3(startPos.x, newY, startPos.z);
    }
    
    void OnTriggerEnter(Collider other)
    {
        if (!isCollected && other.CompareTag("Player"))
        {
            Debug.Log($"[Memories] Player entrato nel trigger di {gameObject.name}");
            CollectMemory();
        }
    }
    
    void CollectMemory()
    {
        isCollected = true;
        
        Debug.Log($"[Memories] CollectMemory chiamato su {gameObject.name}");
        
        // Notifica il collector
        PlayerMemoryCollector collector = Object.FindFirstObjectByType<PlayerMemoryCollector>();
        if (collector != null)
        {
            collector.NotifyMemoryCollected();
        }
        else
        {
            Debug.LogWarning("[Memories] PlayerMemoryCollector non trovato!");
        }
        
        // Disabilita il collider per evitare trigger multipli
        if (triggerCollider != null)
            triggerCollider.enabled = false;
        
        // Ferma l'effetto visuale
        if (defaultEffect != null)
        {
            defaultEffect.StopEffect();
            Debug.Log($"[Memories] Effetto CFXR fermato su {gameObject.name}");
        }
        
        // Disabilita il renderer - oggetto sparisce immediatamente
        if (objectRenderer != null)
        {
            objectRenderer.enabled = false;
        }
        
        // Ferma audio ambientale e riproduci suono di raccolta
        if (audioSource != null)
        {
            audioSource.Stop(); // Ferma ambientale
            
            if (collectSound != null)
            {
                audioSource.PlayOneShot(collectSound, collectVolume);
                Debug.Log($"[Memories] Riproduco suono raccolta con PlayOneShot, volume={collectVolume}");
                
                // Aspetta durata audio
                StartCoroutine(DestroyAfterAudio());
            }
            else
            {
                Debug.LogWarning($"[Memories] Nessun suono di raccolta, distruggo subito {gameObject.name}");
                Destroy(gameObject);
            }
        }
        else
        {
            Debug.LogWarning($"[Memories] AudioSource non trovato, distruggo subito {gameObject.name}");
            Destroy(gameObject);
        }
    }
    
    IEnumerator DestroyAfterAudio()
    {
        if (collectSound != null)
        {
            float duration = collectSound.length;
            Debug.Log($"[Memories] Aspetto {duration} secondi per l'audio prima di distruggere {gameObject.name}");
            yield return new WaitForSeconds(duration);
        }
        
        Debug.Log($"[Memories] Distruggendo {gameObject.name}");
        Destroy(gameObject);
    }
}