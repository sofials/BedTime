using UnityEngine;

public class Checkpoint : MonoBehaviour
{
    [Header("Checkpoint Settings")]
    [SerializeField] private string checkpointName;
    [SerializeField] private bool isActivated = false;
    [SerializeField] private string description = "";
    
    [Header("Reactivation Settings")]
    [SerializeField] private bool allowReactivation = true;
    [SerializeField] private float reactivationCooldown = 1f; // Cooldown in secondi
    
    private float lastActivationTime = -1f;
    
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
            // ✅ MODIFICA: Ora i checkpoint possono essere riattivati
            if (isActivated && !allowReactivation)
            {
                Debug.Log($"[Checkpoint] '{checkpointName}' già attivo e riattivazione disabilitata - ignorato");
                return;
            }
            
            // Controlla il cooldown per evitare attivazioni troppo frequenti
            if (Time.time - lastActivationTime < reactivationCooldown)
            {
                Debug.Log($"[Checkpoint] '{checkpointName}' in cooldown - ignorato");
                return;
            }
            
            // Attiva checkpoint tramite CheckpointManager
            ActivateCheckpoint();
        }
    }
    
    private void ActivateCheckpoint()
    {
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
                bool wasAlreadyActive = isActivated;
                isActivated = true;
                lastActivationTime = Time.time;
                
                string statusMessage = wasAlreadyActive ? "riattivato" : "attivato";
                Debug.Log($"[Checkpoint] '{checkpointName}' {statusMessage} tramite CheckpointManager");
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
    public bool CanBeReactivated() => allowReactivation;
    public float GetReactivationCooldown() => reactivationCooldown;
    
    /// <summary>
    /// Forza l'attivazione del checkpoint (utile per script esterni)
    /// </summary>
    public void ForceActivate()
    {
        // ✅ MODIFICA: Rimuove il controllo isActivated per permettere riattivazione
        ActivateCheckpoint();
    }
    
    /// <summary>
    /// Reset del checkpoint (utile per testing)
    /// </summary>
    public void ResetCheckpoint()
    {
        isActivated = false;
        lastActivationTime = -1f;
        
        // Notifica al CheckpointManager se questo era il checkpoint attivo
        CheckpointManager manager = CheckpointManager.Instance;
        if (manager != null && manager.GetCurrentCheckpoint() == checkpointName)
        {
            manager.ClearCurrentCheckpoint();
        }
        
        Debug.Log($"[Checkpoint] '{checkpointName}' resetato");
    }
    
    /// <summary>
    /// Abilita o disabilita la riattivazione per questo checkpoint
    /// </summary>
    public void SetReactivationEnabled(bool enabled)
    {
        allowReactivation = enabled;
        Debug.Log($"[Checkpoint] '{checkpointName}' riattivazione {(enabled ? "abilitata" : "disabilitata")}");
    }
    
    /// <summary>
    /// Imposta il cooldown per la riattivazione
    /// </summary>
    public void SetReactivationCooldown(float cooldownSeconds)
    {
        reactivationCooldown = Mathf.Max(0f, cooldownSeconds);
        Debug.Log($"[Checkpoint] '{checkpointName}' cooldown riattivazione impostato a {reactivationCooldown}s");
    }
    
    /// <summary>
    /// Verifica se il checkpoint può essere attivato adesso (considerando cooldown)
    /// </summary>
    public bool CanActivateNow()
    {
        if (!allowReactivation && isActivated)
            return false;
            
        return Time.time - lastActivationTime >= reactivationCooldown;
    }
    
    /// <summary>
    /// Ottieni il tempo rimanente per il prossimo utilizzo
    /// </summary>
    public float GetRemainingCooldown()
    {
        if (lastActivationTime < 0f) return 0f;
        
        float remaining = reactivationCooldown - (Time.time - lastActivationTime);
        return Mathf.Max(0f, remaining);
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
    
    [ContextMenu("Toggle Reactivation")]
    public void DebugToggleReactivation()
    {
        SetReactivationEnabled(!allowReactivation);
    }
    
    [ContextMenu("Debug Info")]
    public void DebugInfo()
    {
        CheckpointManager manager = CheckpointManager.Instance;
        if (manager == null) manager = FindFirstObjectByType<CheckpointManager>();
        
        Debug.Log($"=== Checkpoint Info ===\n" +
                  $"Name: {checkpointName}\n" +
                  $"Activated: {isActivated}\n" +
                  $"Allow Reactivation: {allowReactivation}\n" +
                  $"Cooldown: {reactivationCooldown}s\n" +
                  $"Can Activate Now: {CanActivateNow()}\n" +
                  $"Remaining Cooldown: {GetRemainingCooldown():F1}s\n" +
                  $"Description: {description}\n" +
                  $"CheckpointManager Found: {manager != null}\n" +
                  $"Current Active Checkpoint: {(manager != null ? manager.GetCurrentCheckpoint() : "MANAGER NOT FOUND")}\n" +
                  $"Is Current: {(manager != null ? manager.GetCurrentCheckpoint() == checkpointName : false)}");
    }
    
    // ========== GIZMOS ==========
    
    private void OnDrawGizmos()
    {
        // Colore base
        if (isActivated)
        {
            Gizmos.color = Color.green;
        }
        else if (allowReactivation)
        {
            Gizmos.color = Color.yellow;
        }
        else
        {
            Gizmos.color = Color.gray;
        }
        
        Gizmos.DrawWireSphere(transform.position, 1f);
        
        // Indicatore riattivazione
        if (allowReactivation)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(transform.position + Vector3.up * 2f, Vector3.one * 0.3f);
        }
        
        // Indicatore cooldown attivo
        if (GetRemainingCooldown() > 0f)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(transform.position + Vector3.up * 2.5f, Vector3.one * 0.2f);
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 2f);
        
        // Mostra area del trigger se presente
        Collider col = GetComponent<Collider>();
        if (col != null && col.isTrigger)
        {
            Gizmos.color = Color.blue;
            if (col is SphereCollider sphere)
            {
                Gizmos.DrawWireSphere(transform.position, sphere.radius);
            }
            else if (col is BoxCollider box)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawWireCube(box.center, box.size);
                Gizmos.matrix = Matrix4x4.identity;
            }
        }
    }
}