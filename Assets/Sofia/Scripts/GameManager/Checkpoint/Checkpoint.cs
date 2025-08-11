using UnityEngine;

public class Checkpoint : MonoBehaviour
{
    [Header("Checkpoint Settings")]
    [SerializeField] private string checkpointName;
    [SerializeField] private bool isActivated = false;
    [SerializeField] private string description = "";
    
    private void Start()
    {
        // Auto-assign name se non impostato
        if (string.IsNullOrEmpty(checkpointName))
        {
            checkpointName = gameObject.name;
        }
        
        // Registra automaticamente questo checkpoint nel CheckpointManager
        RegisterWithCheckpointManager();
        
        // Controlla se questo checkpoint è già stato raggiunto
        CheckIfAlreadyActivated();
    }
    
    private void RegisterWithCheckpointManager()
    {
        CheckpointManager manager = CheckpointManager.Instance;
        if (manager == null)
        {
            manager = FindFirstObjectByType<CheckpointManager>();
        }
        
        if (manager != null)
        {
            manager.RegisterCheckpoint(checkpointName, transform, description);
            Debug.Log($"[Checkpoint] '{checkpointName}' registrato nel CheckpointManager");
        }
        else
        {
            Debug.LogError($"[Checkpoint] CheckpointManager non trovato! Impossibile registrare '{checkpointName}'");
        }
    }
    
    private void CheckIfAlreadyActivated()
    {
        CheckpointManager manager = CheckpointManager.Instance;
        if (manager == null)
        {
            manager = FindFirstObjectByType<CheckpointManager>();
        }
        
        if (manager != null && manager.GetCurrentCheckpoint() == checkpointName)
        {
            isActivated = true;
            Debug.Log($"[Checkpoint] '{checkpointName}' era già attivato");
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
                Debug.Log($"[Checkpoint] '{checkpointName}' già attivo - ignorato");
                return;
            }
            
            // Attiva checkpoint tramite CheckpointManager
            ActivateCheckpoint();
        }
    }
    
    private void ActivateCheckpoint()
    {
        if (isActivated) return;
        
        CheckpointManager manager = CheckpointManager.Instance;
        if (manager == null)
        {
            manager = FindFirstObjectByType<CheckpointManager>();
        }
        
        if (manager != null)
        {
            bool success = manager.ActivateCheckpoint(checkpointName);
            if (success)
            {
                isActivated = true;
                Debug.Log($"[Checkpoint] '{checkpointName}' attivato tramite CheckpointManager");
            }
            else
            {
                Debug.LogError($"[Checkpoint] Errore nell'attivazione di '{checkpointName}'");
            }
        }
        else
        {
            Debug.LogError($"[Checkpoint] CheckpointManager non trovato! Impossibile attivare '{checkpointName}'");
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
        }
    }
    
    /// <summary>
    /// Reset del checkpoint (utile per testing)
    /// </summary>
    public void ResetCheckpoint()
    {
        isActivated = false;
        
        // Notifica al CheckpointManager se questo era il checkpoint attivo
        CheckpointManager manager = CheckpointManager.Instance;
        if (manager != null && manager.GetCurrentCheckpoint() == checkpointName)
        {
            manager.ClearCurrentCheckpoint();
        }
        
        Debug.Log($"[Checkpoint] '{checkpointName}' resetato");
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
        CheckpointManager manager = CheckpointManager.Instance;
        if (manager == null) manager = FindFirstObjectByType<CheckpointManager>();
        
        Debug.Log($"=== Checkpoint Info ===\n" +
                  $"Name: {checkpointName}\n" +
                  $"Activated: {isActivated}\n" +
                  $"Description: {description}\n" +
                  $"CheckpointManager Found: {manager != null}\n" +
                  $"Current Active Checkpoint: {(manager != null ? manager.GetCurrentCheckpoint() : "MANAGER NOT FOUND")}\n" +
                  $"Is Current: {(manager != null ? manager.GetCurrentCheckpoint() == checkpointName : false)}");
    }
    
    // ========== GIZMOS ==========
    
    private void OnDrawGizmos()
    {
        Gizmos.color = isActivated ? Color.green : Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 1f);
        
        Gizmos.color = isActivated ? Color.green : Color.white;
        Gizmos.DrawWireCube(transform.position + Vector3.up * 2f, Vector3.one * 0.5f);
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 2f);
    }
}