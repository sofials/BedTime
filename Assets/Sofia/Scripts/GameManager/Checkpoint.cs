using UnityEngine;

public class Checkpoint : MonoBehaviour
{
    [Header("Checkpoint Settings")]
    [SerializeField] private string checkpointName;
    [SerializeField] private bool isActivated = false;
    
    [Header("Visual Feedback")]
    [SerializeField] private GameObject activationEffect;
    [SerializeField] private AudioClip activationSound;
    [SerializeField] private Material activatedMaterial;
    
    private Renderer checkpointRenderer;
    private Material originalMaterial;
    
    private void Start()
    {
        // Auto-assign name se non impostato
        if (string.IsNullOrEmpty(checkpointName))
        {
            checkpointName = gameObject.name;
        }
        
        // Salva il materiale originale
        checkpointRenderer = GetComponent<Renderer>();
        if (checkpointRenderer != null)
        {
            originalMaterial = checkpointRenderer.material;
        }
        
        // Controlla se questo checkpoint è già stato raggiunto
        CheckIfAlreadyActivated();
    }
    
    private void CheckIfAlreadyActivated()
    {
        if (GameManager.Instance != null)
        {
            string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            string savedCheckpoint = GameManager.Instance.GetSceneCheckpoint(currentScene);
            
            if (savedCheckpoint == checkpointName)
            {
                // Questo checkpoint era già attivo
                ActivateCheckpoint(false); // false = non suonare effetti
                Debug.Log($"[Checkpoint] {checkpointName} era già attivato");
            }
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        // Controlla se l'oggetto che ha attivato il trigger è il player
        ThirdPersonController player = other.GetComponent<ThirdPersonController>();
        if (player != null && !isActivated)
        {
            ActivateCheckpoint(true);
            NotifySceneManager();
            Debug.Log($"Checkpoint raggiunto: {checkpointName}");
        }
    }
    
    private void ActivateCheckpoint(bool playEffects)
    {
        if (isActivated) return;
        
        isActivated = true;
        
        // Feedback visivo/audio solo se richiesto
        if (playEffects)
        {
            PlayActivationFeedback();
        }
        
        // Cambia materiale se disponibile
        if (checkpointRenderer != null && activatedMaterial != null)
        {
            checkpointRenderer.material = activatedMaterial;
        }
    }
    
    private void PlayActivationFeedback()
    {
        // Effetto visivo
        if (activationEffect != null)
        {
            GameObject effect = Instantiate(activationEffect, transform.position, Quaternion.identity);
            Destroy(effect, 3f);
        }
        
        // Suono
        if (activationSound != null)
        {
            AudioSource.PlayClipAtPoint(activationSound, transform.position);
        }
    }
    
    private void NotifySceneManager()
    {
        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        
        // Notifica allo SceneManager appropriato basandosi sulla scena corrente
        if (currentScene == "00 - Landing in the Dreamworld")
        {
            // Cerca SceneManager00 (se esiste)
            SceneManager00 sceneManager00 = FindFirstObjectByType<SceneManager00>();
            if (sceneManager00 != null)
            {
                sceneManager00.OnCheckpointReached(checkpointName);
            }
            else
            {
                // Fallback diretto al GameManager
                NotifyGameManagerDirectly(currentScene);
            }
        }
        else if (currentScene == "01 - Party in Lukelandia")
        {
            if (SceneManager01.Instance != null)
            {
                SceneManager01.Instance.OnCheckpointReached(checkpointName);
            }
            else
            {
                NotifyGameManagerDirectly(currentScene);
            }
        }
        else if (currentScene == "02 - Finding Pietro")
        {
            // Cerca SceneManager02 (se esiste)
            SceneManager02 sceneManager02 = FindFirstObjectByType<SceneManager02>();
            if (sceneManager02 != null)
            {
                sceneManager02.OnCheckpointReached(checkpointName);
            }
            else
            {
                NotifyGameManagerDirectly(currentScene);
            }
        }
        else
        {
            // Scena non riconosciuta, notifica diretta al GameManager
            NotifyGameManagerDirectly(currentScene);
        }
    }
    
    private void NotifyGameManagerDirectly(string sceneName)
    {
        // Fallback: notifica diretta al GameManager se non c'è SceneManager
        if (GameManager.Instance != null)
        {
            GameManager.Instance.NotifySceneCheckpoint(sceneName, checkpointName);
            Debug.LogWarning($"[Checkpoint] Nessun SceneManager trovato per la scena '{sceneName}', notifica diretta al GameManager");
        }
        else
        {
            Debug.LogError($"[Checkpoint] GameManager non trovato! Impossibile salvare checkpoint {checkpointName}");
        }
    }
    
    // ========== METODI PUBBLICI ==========
    
    public string GetCheckpointName() => checkpointName;
    public bool IsActivated() => isActivated;
    
    /// <summary>
    /// Forza l'attivazione del checkpoint (utile per script esterni)
    /// </summary>
    public void ForceActivate()
    {
        if (!isActivated)
        {
            ActivateCheckpoint(true);
            NotifySceneManager();
            Debug.Log($"[Checkpoint] {checkpointName} forzatamente attivato");
        }
    }
    
    /// <summary>
    /// Reset del checkpoint (utile per testing)
    /// </summary>
    public void ResetCheckpoint()
    {
        isActivated = false;
        
        // Ripristina materiale originale
        if (checkpointRenderer != null && originalMaterial != null)
        {
            checkpointRenderer.material = originalMaterial;
        }
        
        Debug.Log($"[Checkpoint] {checkpointName} resetato");
    }
    
    // ========== COMPATIBILITÀ CON VECCHIO CODICE ==========
    
    /// <summary>
    /// Metodo per compatibilità con il vecchio sistema (deprecato)
    /// </summary>
    [System.Obsolete("Usa il nuovo sistema che passa attraverso gli SceneManager")]
    private void SetCheckpointOldWay(Transform checkpointTransform)
    {
        if (GameManager.Instance != null)
        {
            // Il vecchio GameManager aveva questo metodo
            // GameManager.Instance.SetCheckpoint(checkpointTransform);
            
            // Ora usiamo il nuovo sistema
            string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            GameManager.Instance.NotifySceneCheckpoint(currentScene, checkpointName);
        }
    }
    
    // ========== DEBUG ==========
    
    [ContextMenu("Force Activate")]
    public void DebugForceActivate()
    {
        ForceActivate();
    }
    
    [ContextMenu("Reset Checkpoint")]
    public void DebugResetCheckpoint()
    {
        ResetCheckpoint();
    }
    
    [ContextMenu("Debug Info")]
    public void DebugInfo()
    {
        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        string savedCheckpoint = GameManager.Instance?.GetSceneCheckpoint(currentScene) ?? "Nessuno";
        
        Debug.Log($"=== Checkpoint Info ===\n" +
                  $"Name: {checkpointName}\n" +
                  $"Activated: {isActivated}\n" +
                  $"Current Scene: {currentScene}\n" +
                  $"Saved Checkpoint for Scene: {savedCheckpoint}\n" +
                  $"Is This Current Checkpoint: {savedCheckpoint == checkpointName}");
    }
}

// ========== SCRIPT AGGIUNTIVI PER LE ALTRE SCENE ==========

// Se non hai ancora SceneManager00 e SceneManager02, ecco dei template base:

public class SceneManager00 : MonoBehaviour
{
    [Header("Scene Configuration")]
    [SerializeField] private string sceneName = "00 - Landing in the Dreamworld";
    
    public static SceneManager00 Instance { get; private set; }
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    public void OnCheckpointReached(string checkpointName)
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.NotifySceneCheckpoint(sceneName, checkpointName);
        }
        Debug.Log($"[SceneManager00] Checkpoint {checkpointName} raggiunto");
    }
    
    // Altri metodi per memorie, etc...
}

public class SceneManager02 : MonoBehaviour
{
    [Header("Scene Configuration")]
    [SerializeField] private string sceneName = "02 - Finding Pietro";
    
    public static SceneManager02 Instance { get; private set; }
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    public void OnCheckpointReached(string checkpointName)
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.NotifySceneCheckpoint(sceneName, checkpointName);
        }
        Debug.Log($"[SceneManager02] Checkpoint {checkpointName} raggiunto");
    }
    
    // Altri metodi per memorie, etc...
}