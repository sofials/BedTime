using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

/// <summary>
/// Manager universale per la gestione dei checkpoint in qualsiasi scena
/// Compatibile con qualunque SceneManager
/// </summary>
public class CheckpointManager : MonoBehaviour
{
    [Header("Checkpoint Configuration")]
    [SerializeField] private string sceneName = "";
    [SerializeField] private bool autoDetectSceneName = true;
    [SerializeField] private Transform defaultSpawnPoint;
    
    [Header("Checkpoint Settings")]
    [SerializeField] private bool enableCheckpointSystem = true;
    [SerializeField] private bool autoSaveOnCheckpoint = true;
    [SerializeField] private bool enableDebugLogs = true;
    
    [Header("Test & Debug")]
    [SerializeField] private bool testMode = false;
    [SerializeField] private bool resetOnSceneReload = false; // ← Nuovo flag specifico
    
    [Header("Checkpoint Events")]
    public UnityEvent<string> OnCheckpointActivated;
    public UnityEvent<string> OnCheckpointCleared;
    public UnityEvent<Vector3, Quaternion> OnSpawnPointChanged;
    
    [Header("Raft Integration")]
    public UnityEvent<Vector3> OnPlayerCheckpointChanged; 
    
    // Stato interno
    private string currentCheckpoint = "";
    private Dictionary<string, CheckpointData> registeredCheckpoints = new Dictionary<string, CheckpointData>();
    
    // Singleton pattern (opzionale - può esistere un manager per scena)
    public static CheckpointManager Instance { get; private set; }
    
    [System.Serializable]
    public class CheckpointData
    {
        public string checkpointName;
        public Vector3 position;
        public Quaternion rotation;
        public string description;
        public bool isActive;
        
        public CheckpointData(string name, Vector3 pos, Quaternion rot, string desc = "")
        {
            checkpointName = name;
            position = pos;
            rotation = rot;
            description = desc;
            isActive = true;
        }
    }
    
    private void Awake()
    {
        // Setup singleton (permette multiple istanze se necessario)
        if (Instance == null)
        {
            Instance = this;
        }
        
        // Auto-detect scene name se abilitato
        if (autoDetectSceneName && string.IsNullOrEmpty(sceneName))
        {
            sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        }
        
        DebugLog($"[CheckpointManager] Inizializzato per scena: '{sceneName}'");
    }
    
    private void Start()
    {
        bool shouldReset = testMode || resetOnSceneReload;
        
        if (shouldReset)
        {
            ClearSavedCheckpointData();
            currentCheckpoint = "";
            DebugLog($"[CheckpointManager] 🧪 RESET: {(testMode ? "Test Mode" : "Scene Reload Reset")}");
        }
        else
        {
            LoadCheckpointData();
        }
        
        // Registra automaticamente il defaultSpawnPoint se presente
        if (defaultSpawnPoint != null)
        {
            RegisterCheckpoint("DefaultSpawn", defaultSpawnPoint.position, defaultSpawnPoint.rotation, "Default spawn point");
        }
    }
    
    // ========== GESTIONE CHECKPOINT ==========
    
    /// <summary>
    /// Registra un nuovo checkpoint nella scena
    /// </summary>
    public void RegisterCheckpoint(string checkpointName, Vector3 position, Quaternion rotation, string description = "")
    {
        if (string.IsNullOrEmpty(checkpointName))
        {
            DebugLog("[CheckpointManager] ⚠️ Nome checkpoint vuoto, ignorato");
            return;
        }
        
        CheckpointData data = new CheckpointData(checkpointName, position, rotation, description);
        registeredCheckpoints[checkpointName] = data;
        
        DebugLog($"[CheckpointManager] ✅ Checkpoint registrato: '{checkpointName}' at {position}");
    }
    
    /// <summary>
    /// Registra un checkpoint usando un Transform
    /// </summary>
    public void RegisterCheckpoint(string checkpointName, Transform checkpointTransform, string description = "")
    {
        if (checkpointTransform == null)
        {
            DebugLog("[CheckpointManager] ⚠️ Transform checkpoint nullo, ignorato");
            return;
        }
        
        RegisterCheckpoint(checkpointName, checkpointTransform.position, checkpointTransform.rotation, description);
    }
    
    /// <summary>
    /// Attiva un checkpoint (lo imposta come checkpoint corrente)
    /// </summary>
    public bool ActivateCheckpoint(string checkpointName)
    {
        if (!enableCheckpointSystem)
        {
            DebugLog("[CheckpointManager] Sistema checkpoint disabilitato");
            return false;
        }
        
        if (string.IsNullOrEmpty(checkpointName))
        {
            DebugLog("[CheckpointManager] ⚠️ Nome checkpoint vuoto per attivazione");
            return false;
        }
        
        // Se il checkpoint non è registrato, registralo come posizione corrente
        if (!registeredCheckpoints.ContainsKey(checkpointName))
        {
            DebugLog($"[CheckpointManager] ⚠️ Checkpoint '{checkpointName}' non registrato, uso posizione di default");
            RegisterCheckpoint(checkpointName, GetCurrentSpawnPosition(), GetCurrentSpawnRotation(), "Auto-registered");
        }
        
        currentCheckpoint = checkpointName;
        DebugLog($"[CheckpointManager] ✅ Checkpoint attivato: '{checkpointName}'");
        
        // Salva automaticamente se abilitato (e non in modalità test)
        if (autoSaveOnCheckpoint && !testMode)
        {
            SaveCheckpointData();
        }
        else if (testMode)
        {
            DebugLog("[CheckpointManager] 🧪 Test Mode: Salvataggio saltato");
        }
        
        // Notifica eventi
        OnCheckpointActivated?.Invoke(checkpointName);
        
        if (registeredCheckpoints.ContainsKey(checkpointName))
        {
            var data = registeredCheckpoints[checkpointName];
            OnSpawnPointChanged?.Invoke(data.position, data.rotation);
            
            // ✅ NUOVO: Notifica alle zattere la nuova posizione del player
            OnPlayerCheckpointChanged?.Invoke(data.position);
        }
        
        return true;
    }
    
    /// <summary>
    /// Cancella il checkpoint corrente
    /// </summary>
    public void ClearCurrentCheckpoint()
    {
        if (string.IsNullOrEmpty(currentCheckpoint))
        {
            DebugLog("[CheckpointManager] Nessun checkpoint da cancellare");
            return;
        }
        
        string previousCheckpoint = currentCheckpoint;
        currentCheckpoint = "";
        
        DebugLog($"[CheckpointManager] 🗑️ Checkpoint '{previousCheckpoint}' cancellato");
        
        if (autoSaveOnCheckpoint && !testMode)
        {
            SaveCheckpointData();
        }
        
        OnCheckpointCleared?.Invoke(previousCheckpoint);
    }
    
    /// <summary>
    /// Rimuove un checkpoint registrato
    /// </summary>
    public bool UnregisterCheckpoint(string checkpointName)
    {
        if (registeredCheckpoints.ContainsKey(checkpointName))
        {
            registeredCheckpoints.Remove(checkpointName);
            
            // Se era il checkpoint corrente, cancellalo
            if (currentCheckpoint == checkpointName)
            {
                ClearCurrentCheckpoint();
            }
            
            DebugLog($"[CheckpointManager] ❌ Checkpoint '{checkpointName}' rimosso");
            return true;
        }
        
        return false;
    }
    
    // ========== GETTERS ==========
    
    /// <summary>
    /// Ottieni il nome del checkpoint corrente
    /// </summary>
    public string GetCurrentCheckpoint() => currentCheckpoint;
    
    /// <summary>
    /// Verifica se c'è un checkpoint attivo
    /// </summary>
    public bool HasActiveCheckpoint() => !string.IsNullOrEmpty(currentCheckpoint);
    
    /// <summary>
    /// Ottieni la posizione di spawn corrente (checkpoint attivo o default)
    /// </summary>
    public Vector3 GetCurrentSpawnPosition()
    {
        // Se c'è un checkpoint attivo, usa quello
        if (HasActiveCheckpoint() && registeredCheckpoints.ContainsKey(currentCheckpoint))
        {
            return registeredCheckpoints[currentCheckpoint].position;
        }
        
        // Altrimenti usa il default spawn point
        if (defaultSpawnPoint != null)
        {
            return defaultSpawnPoint.position;
        }
        
        // Ultimo fallback
        return Vector3.zero;
    }
    
    /// <summary>
    /// Ottieni la rotazione di spawn corrente
    /// </summary>
    public Quaternion GetCurrentSpawnRotation()
    {
        // Se c'è un checkpoint attivo, usa quello
        if (HasActiveCheckpoint() && registeredCheckpoints.ContainsKey(currentCheckpoint))
        {
            return registeredCheckpoints[currentCheckpoint].rotation;
        }
        
        // Altrimenti usa il default spawn point
        if (defaultSpawnPoint != null)
        {
            return defaultSpawnPoint.rotation;
        }
        
        // Ultimo fallback
        return Quaternion.identity;
    }
    
    /// <summary>
    /// Ottieni i dati di un checkpoint specifico
    /// </summary>
    public CheckpointData GetCheckpointData(string checkpointName)
    {
        return registeredCheckpoints.ContainsKey(checkpointName) ? registeredCheckpoints[checkpointName] : null;
    }
    
    /// <summary>
    /// Ottieni tutti i checkpoint registrati
    /// </summary>
    public Dictionary<string, CheckpointData> GetAllCheckpoints()
    {
        return new Dictionary<string, CheckpointData>(registeredCheckpoints);
    }
    
    /// <summary>
    /// Verifica se un checkpoint è registrato
    /// </summary>
    public bool IsCheckpointRegistered(string checkpointName)
    {
        return registeredCheckpoints.ContainsKey(checkpointName);
    }
    
    // ========== SALVATAGGIO E CARICAMENTO ==========
    
    /// <summary>
    /// Salva i dati del checkpoint corrente
    /// </summary>
    public void SaveCheckpointData()
    {
        if (testMode)
        {
            DebugLog("[CheckpointManager] 🧪 Test Mode: Salvataggio ignorato");
            return;
        }
        
        if (string.IsNullOrEmpty(sceneName))
        {
            DebugLog("[CheckpointManager] ⚠️ Scene name vuoto, impossibile salvare");
            return;
        }
        
        string saveKey = $"Checkpoint_{sceneName.Replace(" ", "_").Replace("-", "_")}";
        PlayerPrefs.SetString(saveKey, currentCheckpoint);
        PlayerPrefs.Save();
        
        DebugLog($"[CheckpointManager] 💾 Checkpoint salvato: '{currentCheckpoint}' per scena '{sceneName}'");
    }
    
    /// <summary>
    /// Carica i dati del checkpoint
    /// </summary>
    public void LoadCheckpointData()
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            DebugLog("[CheckpointManager] ⚠️ Scene name vuoto, impossibile caricare");
            return;
        }
        
        string saveKey = $"Checkpoint_{sceneName.Replace(" ", "_").Replace("-", "_")}";
        currentCheckpoint = PlayerPrefs.GetString(saveKey, "");
        
        DebugLog($"[CheckpointManager] 📁 Checkpoint caricato: '{currentCheckpoint}' per scena '{sceneName}'");
    }
    
    /// <summary>
    /// Cancella i dati salvati del checkpoint
    /// </summary>
    public void ClearSavedCheckpointData()
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            DebugLog("[CheckpointManager] ⚠️ Scene name vuoto, impossibile cancellare");
            return;
        }
        
        string saveKey = $"Checkpoint_{sceneName.Replace(" ", "_").Replace("-", "_")}";
        PlayerPrefs.DeleteKey(saveKey);
        PlayerPrefs.Save();
        
        currentCheckpoint = "";
        DebugLog($"[CheckpointManager] 🗑️ Dati checkpoint cancellati per scena '{sceneName}'");
    }
    
    // ========== METODI PUBBLICI PER SCENEMANAGER ==========
    
    /// <summary>
    /// Metodo di compatibilità per SceneManager esistenti
    /// </summary>
    public void NotifyCheckpointReached(string checkpointName)
    {
        ActivateCheckpoint(checkpointName);
    }
    
    /// <summary>
    /// Metodo alternativo di compatibilità per SceneManager esistenti
    /// </summary>
    public void OnCheckpointTriggered(string checkpointName)
    {
        ActivateCheckpoint(checkpointName);
    }
    
    /// <summary>
    /// Imposta manualmente il scene name (utile se auto-detect fallisce)
    /// </summary>
    public void SetSceneName(string newSceneName)
    {
        sceneName = newSceneName;
        DebugLog($"[CheckpointManager] Scene name impostato manualmente: '{sceneName}'");
    }
    
    /// <summary>
    /// Imposta il default spawn point
    /// </summary>
    public void SetDefaultSpawnPoint(Transform spawnPoint)
    {
        defaultSpawnPoint = spawnPoint;
        
        if (spawnPoint != null)
        {
            RegisterCheckpoint("DefaultSpawn", spawnPoint.position, spawnPoint.rotation, "Default spawn point");
            DebugLog($"[CheckpointManager] Default spawn point impostato: {spawnPoint.position}");
        }
    }
    
    // ========== METODI PER MODALITÀ TEST ==========
    
    /// <summary>
    /// Imposta la modalità test (disabilita salvataggio e carica sempre da zero)
    /// </summary>
    public void SetTestMode(bool enabled)
    {
        testMode = enabled;
        
        if (testMode)
        {
            ClearSavedCheckpointData();
            currentCheckpoint = "";
            DebugLog("[CheckpointManager] 🧪 Modalità test ATTIVATA");
        }
        else
        {
            LoadCheckpointData();
            DebugLog("[CheckpointManager] 🎮 Modalità normale ATTIVATA");
        }
    }
    
    /// <summary>
    /// Imposta il flag di reset al reload della scena
    /// </summary>
    public void SetResetOnSceneReload(bool enabled)
    {
        resetOnSceneReload = enabled;
        DebugLog($"[CheckpointManager] Reset on Scene Reload: {(enabled ? "ATTIVATO" : "DISATTIVATO")}");
    }
    
    /// <summary>
    /// Forza un reset completo (utile per bottone "Restart Level")
    /// </summary>
    public void ForceResetToBeginning()
    {
        ClearSavedCheckpointData();
        currentCheckpoint = "";
        
        DebugLog("[CheckpointManager] 🔄 RESET FORZATO: Torna all'inizio");
        
        // Notifica il cambio spawn point
        if (defaultSpawnPoint != null)
        {
            OnSpawnPointChanged?.Invoke(defaultSpawnPoint.position, defaultSpawnPoint.rotation);
            OnPlayerCheckpointChanged?.Invoke(defaultSpawnPoint.position);
        }
    }
    
    /// <summary>
    /// Verifica se siamo in modalità test
    /// </summary>
    public bool IsTestModeActive() => testMode;
    
    /// <summary>
    /// Verifica se il reset al reload è attivo
    /// </summary>
    public bool IsResetOnReloadActive() => resetOnSceneReload;
    
    // ========== UTILITY ==========
    
    /// <summary>
    /// Reset completo del sistema checkpoint
    /// </summary>
    public void ResetCheckpointSystem()
    {
        currentCheckpoint = "";
        registeredCheckpoints.Clear();
        ClearSavedCheckpointData();
        
        // Re-registra il default spawn se presente
        if (defaultSpawnPoint != null)
        {
            RegisterCheckpoint("DefaultSpawn", defaultSpawnPoint.position, defaultSpawnPoint.rotation, "Default spawn point");
        }
        
        DebugLog("[CheckpointManager] 🔄 Sistema checkpoint resettato");
    }
    
    /// <summary>
    /// Abilita/disabilita il sistema checkpoint
    /// </summary>
    public void SetCheckpointSystemEnabled(bool enabled)
    {
        enableCheckpointSystem = enabled;
        DebugLog($"[CheckpointManager] Sistema checkpoint {(enabled ? "abilitato" : "disabilitato")}");
    }
    
    /// <summary>
    /// Abilita/disabilita il salvataggio automatico
    /// </summary>
    public void SetAutoSaveEnabled(bool enabled)
    {
        autoSaveOnCheckpoint = enabled;
        DebugLog($"[CheckpointManager] Auto-save {(enabled ? "abilitato" : "disabilitato")}");
    }
    
    // ========== DEBUG ==========
    
    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log(message);
        }
    }
    
    public void SetDebugLogsEnabled(bool enabled)
    {
        enableDebugLogs = enabled;
    }
    
    [ContextMenu("Debug Current State")]
    public void DebugCurrentState()
    {
        Debug.Log($"=== CheckpointManager State ===\n" +
                  $"Scene: '{sceneName}'\n" +
                  $"Current Checkpoint: '{currentCheckpoint}'\n" +
                  $"Has Active Checkpoint: {HasActiveCheckpoint()}\n" +
                  $"Spawn Position: {GetCurrentSpawnPosition()}\n" +
                  $"Spawn Rotation: {GetCurrentSpawnRotation()}\n" +
                  $"Registered Checkpoints: {registeredCheckpoints.Count}\n" +
                  $"System Enabled: {enableCheckpointSystem}\n" +
                  $"Auto-Save: {autoSaveOnCheckpoint}\n" +
                  $"Test Mode: {testMode}\n" +
                  $"Reset on Scene Reload: {resetOnSceneReload}\n" +
                  $"Default Spawn: {(defaultSpawnPoint != null ? defaultSpawnPoint.position.ToString() : "NULL")}");
        
        if (registeredCheckpoints.Count > 0)
        {
            Debug.Log("=== Registered Checkpoints ===");
            foreach (var kvp in registeredCheckpoints)
            {
                var data = kvp.Value;
                Debug.Log($"'{kvp.Key}': {data.position} | {data.description} | Active: {data.isActive}");
            }
        }
    }
    
    [ContextMenu("Test - Add Random Checkpoint")]
    public void DebugAddRandomCheckpoint()
    {
        string testName = "TestCheckpoint_" + System.DateTime.Now.Ticks;
        Vector3 randomPos = new Vector3(Random.Range(-10f, 10f), 0, Random.Range(-10f, 10f));
        RegisterCheckpoint(testName, randomPos, Quaternion.identity, "Test checkpoint");
        ActivateCheckpoint(testName);
    }
    
    [ContextMenu("Test - Clear Current Checkpoint")]
    public void DebugClearCheckpoint()
    {
        ClearCurrentCheckpoint();
    }
    
    [ContextMenu("Test - Reset System")]
    public void DebugResetSystem()
    {
        ResetCheckpointSystem();
    }
    
    [ContextMenu("Force Save")]
    public void DebugForceSave()
    {
        SaveCheckpointData();
    }
    
    [ContextMenu("Force Load")]
    public void DebugForceLoad()
    {
        LoadCheckpointData();
    }
    
    // ========== CONTEXT MENU PER MODALITÀ TEST ==========
    
    [ContextMenu("Enable Test Mode")]
    public void DebugEnableTestMode()
    {
        SetTestMode(true);
    }
    
    [ContextMenu("Disable Test Mode")]
    public void DebugDisableTestMode()
    {
        SetTestMode(false);
    }
    
    [ContextMenu("Toggle Test Mode")]
    public void DebugToggleTestMode()
    {
        SetTestMode(!testMode);
    }
    
    [ContextMenu("Enable Reset on Scene Reload")]
    public void DebugEnableResetOnReload()
    {
        SetResetOnSceneReload(true);
    }
    
    [ContextMenu("Disable Reset on Scene Reload")]
    public void DebugDisableResetOnReload()
    {
        SetResetOnSceneReload(false);
    }
    
    [ContextMenu("Toggle Reset on Scene Reload")]
    public void DebugToggleResetOnReload()
    {
        SetResetOnSceneReload(!resetOnSceneReload);
    }
    
    [ContextMenu("Force Reset to Beginning")]
    public void DebugForceResetToBeginning()
    {
        ForceResetToBeginning();
    }
    
    // ========== CLEANUP ==========
    
    private void OnDestroy()
    {
        if (autoSaveOnCheckpoint && !testMode)
        {
            SaveCheckpointData();
        }
        
        if (Instance == this)
        {
            Instance = null;
        }
        
        DebugLog("[CheckpointManager] Cleanup completato");
    }
}