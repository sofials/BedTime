using UnityEngine;

public class Checkpoint : MonoBehaviour
{
    [Header("Checkpoint Settings")]
    [SerializeField] private string checkpointName;
    [SerializeField] private bool isActivated = false;
    
    private void Start()
    {
        // Auto-assign name se non impostato
        if (string.IsNullOrEmpty(checkpointName))
        {
            checkpointName = gameObject.name;
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
                isActivated = true;
                Debug.Log($"[Checkpoint] {checkpointName} era già attivato");
            }
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        // Controlla se l'oggetto che ha attivato il trigger è il player
        ThirdPersonController player = other.GetComponent<ThirdPersonController>();
        if (player != null)
        {
            // Se già attivato, non fare niente!
            if (isActivated)
            {
                Debug.Log($"[Checkpoint] {checkpointName} già attivo - ignorato");
                return;
            }
            
            // Prima attivazione
            ActivateCheckpoint();
            NotifySceneManager();
            Debug.Log($"[Checkpoint] Checkpoint {checkpointName} attivato");
        }
    }
    
    private void ActivateCheckpoint()
    {
        if (isActivated) return;
        
        isActivated = true;
        // Nessun effetto visivo/audio - è un oggetto empty
    }
    
    private void NotifySceneManager()
    {
        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        
        // Notifica allo SceneManager appropriato basandosi sulla scena corrente
        if (currentScene == "00 - Landing in the Dreamworld")
        {
            SceneManager00 sceneManager00 = FindFirstObjectByType<SceneManager00>();
            if (sceneManager00 != null)
            {
                sceneManager00.OnCheckpointReached(checkpointName);
            }
            else
            {
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
            NotifyGameManagerDirectly(currentScene);
        }
    }
    
    private void NotifyGameManagerDirectly(string sceneName)
    {
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
            ActivateCheckpoint();
            NotifySceneManager();
            Debug.Log($"[Checkpoint] {checkpointName} forzatamente attivato");
        }
        else
        {
            Debug.Log($"[Checkpoint] {checkpointName} già attivo - nessuna azione");
        }
    }
    
    /// <summary>
    /// Reset del checkpoint (utile per testing)
    /// </summary>
    public void ResetCheckpoint()
    {
        isActivated = false;
        Debug.Log($"[Checkpoint] {checkpointName} resetato");
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
    
    // ========== GIZMOS PER VISUALIZZARE IN EDITOR ==========
    
    private void OnDrawGizmos()
    {
        // Visualizza l'area del checkpoint nell'editor
        Gizmos.color = isActivated ? Color.green : Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 1f);
        
        // Icona checkpoint
        Gizmos.color = isActivated ? Color.green : Color.white;
        Gizmos.DrawWireCube(transform.position + Vector3.up * 2f, Vector3.one * 0.5f);
    }
    
    private void OnDrawGizmosSelected()
    {
        // Mostra informazioni dettagliate quando selezionato
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 2f);
        
        // Nome del checkpoint
        if (!string.IsNullOrEmpty(checkpointName))
        {
            Vector3 labelPos = transform.position + Vector3.up * 3f;
            // Il nome viene mostrato tramite i gizmos (visibile solo nell'editor)
        }
    }
}