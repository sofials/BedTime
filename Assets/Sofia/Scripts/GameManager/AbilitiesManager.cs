using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;
using System.Collections;

/// <summary>
/// Manager universale per la gestione delle abilità in qualsiasi scena
/// Compatibile con qualunque SceneManager
/// </summary>
public class AbilitiesManager : MonoBehaviour
{
    [System.Serializable]
    public class AbilityReference
    {
        [Header("Configurazione Abilità")]
        public AbilityBase ability;
        public string abilityName; // Nome per debug/riferimenti
        
        [Header("Stato nel Livello")]
        [Tooltip("Se true, l'abilità è completamente disabilitata in questo livello")]
        public bool disabledInLevel = false;
        
        [Tooltip("Se true, l'abilità può essere attivata da eventi durante il livello")]
        public bool canBeActivatedByEvents = true;
    }

    [System.Serializable]
    public class EventActivation
    {
        [Header("Configurazione Evento")]
        public string eventName;
        public string abilityToActivate;
        
        [Header("Opzioni Attivazione")]
        [Tooltip("Se true, usa ForceActivate (bypassa energia), altrimenti TryActivate")]
        public bool forceActivation = true;
        
        [Tooltip("Se true, rimuove l'abilità dalla lista dopo l'attivazione")]
        public bool oneTimeActivation = false;
        
        [Header("Feedback")]
        public string activationMessage = "";
    }

    [Header("Scene Configuration")]
    [SerializeField] private string sceneName = "";
    [SerializeField] private bool autoDetectSceneName = true;

    [Header("Configurazione Abilità")]
    [SerializeField] private List<AbilityReference> abilities = new List<AbilityReference>();
    
    [Header("Eventi di Attivazione")]
    [SerializeField] private List<EventActivation> eventActivations = new List<EventActivation>();
    
    [Header("Auto-Setup")]
    [SerializeField] private bool autoFindAbilities = true;
    [SerializeField] private bool applyConfigurationOnStart = true;
    [SerializeField] private bool autoSaveConfiguration = true;
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;
    
    [Header("Eventi")]
    public UnityEvent OnAbilitiesConfigured;
    public UnityEvent<string> OnAbilityActivatedByEvent;
    public UnityEvent<string> OnAbilityFailedToActivate;
    public UnityEvent<string> OnAbilityEnabled;
    public UnityEvent<string> OnAbilityDisabled;
    
    // Singleton pattern (opzionale - può esistere un manager per scena)
    public static AbilitiesManager Instance { get; private set; }
    
    // Riferimenti
    private Dictionary<string, AbilityBase> abilityLookup = new Dictionary<string, AbilityBase>();
    private List<EventActivation> availableActivations = new List<EventActivation>();
    private Dictionary<string, bool> originalAbilityStates = new Dictionary<string, bool>();
    
    // Stato
    private bool abilitiesConfigured = false;
    
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
        
        DebugLog($"[AbilitiesManager] Inizializzato per scena: '{sceneName}'");
    }
    
    private void Start()
    {
        StartCoroutine(InitializeAbilitiesCoroutine());
    }
    
  private IEnumerator InitializeAbilitiesCoroutine()
{
    yield return null; // Aspetta un frame
    
    // ❌ RIMOSSO: LoadAbilityConfiguration();
    
    if (autoFindAbilities)
    {
        FindAllAbilities();
    }
    
    if (applyConfigurationOnStart)
    {
        ConfigureAbilities();
    }
    
    // Copia gli eventi disponibili
    availableActivations = new List<EventActivation>(eventActivations);
    
    DebugLog("[AbilitiesManager] Inizializzazione completata (SENZA caricamento dati salvati)");
}

    /// <summary>
    /// Trova automaticamente tutte le abilità nella scena
    /// </summary>
    private void FindAllAbilities()
{
    AbilityBase[] foundAbilities = FindObjectsByType<AbilityBase>(FindObjectsSortMode.None);

    foreach (var ability in foundAbilities)
    {
        // Verifica se già presente nella lista
        bool alreadyAdded = abilities.Exists(a => a.ability == ability);
        
        if (!alreadyAdded)
        {
            // Aggiungi SOLO se non c'è già una configurazione manuale
            abilities.Add(new AbilityReference
            {
                ability = ability,
                abilityName = ability.gameObject.name,
                disabledInLevel = false, // Default: abilitata (modificabile dall'Inspector)
                canBeActivatedByEvents = true
            });
            
            DebugLog($"[AbilitiesManager] ✅ Nuova abilità aggiunta: '{ability.gameObject.name}' (modificabile dall'Inspector)");
        }
    }
    
    DebugLog($"[AbilitiesManager] Trovate {foundAbilities.Length} abilità, {abilities.Count} configurate");
}

    
    /// <summary>
    /// Applica la configurazione delle abilità
    /// </summary>
public void ConfigureAbilities()
{
    if (abilitiesConfigured) return;
    
    DebugLog("[AbilitiesManager] Configurazione abilità (SOLO da Inspector)...");
    
    // Costruisci lookup dictionary
    abilityLookup.Clear();
    originalAbilityStates.Clear();
    
    foreach (var abilityRef in abilities)
    {
        if (abilityRef.ability == null) continue;
        
        string key = !string.IsNullOrEmpty(abilityRef.abilityName) 
            ? abilityRef.abilityName 
            : abilityRef.ability.gameObject.name;
            
        abilityLookup[key] = abilityRef.ability;
        
        // Salva lo stato originale
        originalAbilityStates[key] = abilityRef.ability.IsEnabled;
        
        // Applica configurazione livello DIRETTAMENTE dall'Inspector
        abilityRef.ability.SetLevelAllowed(!abilityRef.disabledInLevel);
        
        DebugLog($"[AbilitiesManager] {key}: " +
                 $"DisabledInLevel={abilityRef.disabledInLevel}, " +
                 $"SetLevelAllowed({!abilityRef.disabledInLevel}), " +
                 $"FinalEnabled={abilityRef.ability.IsEnabled}, " +
                 $"Events={abilityRef.canBeActivatedByEvents}");
    }
    
    abilitiesConfigured = true;
    
    // ❌ RIMOSSO: Auto-save
    
    OnAbilitiesConfigured?.Invoke();
    
    DebugLog($"[AbilitiesManager] Configurazione completata per {abilities.Count} abilità (SENZA salvataggio)");
}

    
    /// <summary>
    /// Attiva un'abilità tramite evento
    /// </summary>
    public bool ActivateAbilityByEvent(string eventName)
    {
        DebugLog($"[AbilitiesManager] Evento ricevuto: '{eventName}'");
        
        // Trova l'attivazione corrispondente
        EventActivation activation = availableActivations.Find(e => e.eventName == eventName);
        
        if (activation == null)
        {
            DebugLog($"[AbilitiesManager] ⚠️ Evento '{eventName}' non configurato");
            return false;
        }
        
        // Trova l'abilità
        if (!abilityLookup.TryGetValue(activation.abilityToActivate, out AbilityBase ability))
        {
            DebugLog($"[AbilitiesManager] ⚠️ Abilità '{activation.abilityToActivate}' non trovata");
            OnAbilityFailedToActivate?.Invoke(activation.abilityToActivate);
            return false;
        }
        
        // Verifica se l'abilità può essere attivata da eventi
        AbilityReference abilityRef = abilities.Find(a => a.ability == ability);
        if (abilityRef != null && !abilityRef.canBeActivatedByEvents)
        {
            DebugLog($"[AbilitiesManager] ⚠️ Abilità '{activation.abilityToActivate}' non può essere attivata da eventi");
            OnAbilityFailedToActivate?.Invoke(activation.abilityToActivate);
            return false;
        }
        
        // Attiva l'abilità
        bool success = false;
        
        if (activation.forceActivation)
        {
            success = ability.ForceActivate();
        }
        else
        {
            if (ability.CanActivate())
            {
                ability.TryActivate();
                success = true;
            }
        }
        
        if (success)
        {
            DebugLog($"[AbilitiesManager] ✅ Abilità '{activation.abilityToActivate}' attivata da evento '{eventName}'");
            
            if (!string.IsNullOrEmpty(activation.activationMessage))
            {
                DebugLog($"[AbilitiesManager] 💬 {activation.activationMessage}");
            }
            
            OnAbilityActivatedByEvent?.Invoke(activation.abilityToActivate);
            
            // Rimuovi se è una attivazione una tantum
            if (activation.oneTimeActivation)
            {
                availableActivations.Remove(activation);
                DebugLog($"[AbilitiesManager] Evento '{eventName}' rimosso (one-time)");
            }
        }
        else
        {
            DebugLog($"[AbilitiesManager] ❌ Fallita attivazione di '{activation.abilityToActivate}' per evento '{eventName}'");
            OnAbilityFailedToActivate?.Invoke(activation.abilityToActivate);
        }
        
        return success;
    }
    
    /// <summary>
    /// Abilita temporaneamente un'abilità disabilitata nel livello
    /// </summary>
    public bool EnableAbilityTemporarily(string abilityName, float duration = 0f)
    {
        if (!abilityLookup.TryGetValue(abilityName, out AbilityBase ability))
        {
            DebugLog($"[AbilitiesManager] ⚠️ Abilità '{abilityName}' non trovata per abilitazione temporanea");
            return false;
        }
        
        ability.SetLevelAllowed(true);
        DebugLog($"[AbilitiesManager] Abilità '{abilityName}' abilitata temporaneamente");
        OnAbilityEnabled?.Invoke(abilityName);
        
        if (duration > 0f)
        {
            StartCoroutine(DisableAbilityAfterDelay(abilityName, duration));
        }
        
        return true;
    }
    
    private IEnumerator DisableAbilityAfterDelay(string abilityName, float delay)
    {
        yield return new WaitForSeconds(delay);
        
        if (abilityLookup.TryGetValue(abilityName, out AbilityBase ability))
        {
            // Verifica la configurazione originale
            AbilityReference abilityRef = abilities.Find(a => a.ability == ability);
            if (abilityRef != null && abilityRef.disabledInLevel)
            {
                ability.SetLevelAllowed(false);
                DebugLog($"[AbilitiesManager] Abilità '{abilityName}' ri-disabilitata dopo {delay}s");
                OnAbilityDisabled?.Invoke(abilityName);
            }
        }
    }
    
    /// <summary>
    /// Forza l'attivazione di un'abilità specifica
    /// </summary>
    public bool ForceActivateAbility(string abilityName)
    {
        if (!abilityLookup.TryGetValue(abilityName, out AbilityBase ability))
        {
            DebugLog($"[AbilitiesManager] ⚠️ Abilità '{abilityName}' non trovata per attivazione forzata");
            return false;
        }
        
        bool success = ability.ForceActivate();
        
        if (success)
        {
            DebugLog($"[AbilitiesManager] ✅ Abilità '{abilityName}' attivata forzatamente");
            OnAbilityActivatedByEvent?.Invoke(abilityName);
        }
        else
        {
            DebugLog($"[AbilitiesManager] ❌ Fallita attivazione forzata di '{abilityName}'");
            OnAbilityFailedToActivate?.Invoke(abilityName);
        }
        
        return success;
    }
    
    /// <summary>
    /// Abilita un'abilità specifica
    /// </summary>
    public bool EnableAbility(string abilityName)
    {
        if (!abilityLookup.TryGetValue(abilityName, out AbilityBase ability))
        {
            DebugLog($"[AbilitiesManager] ⚠️ Abilità '{abilityName}' non trovata per abilitazione");
            return false;
        }
        
        ability.SetLevelAllowed(true);
        DebugLog($"[AbilitiesManager] Abilità '{abilityName}' abilitata");
        OnAbilityEnabled?.Invoke(abilityName);
        
        // Aggiorna la configurazione
        AbilityReference abilityRef = abilities.Find(a => a.ability == ability);
        if (abilityRef != null)
        {
            abilityRef.disabledInLevel = false;
        }
        
        return true;
    }
    
/// <summary>
/// Abilita un'abilità dall'inspector (la rende utilizzabile nel livello)
/// </summary>
public void EnableAbilityFromInspector(string abilityName)
{
    EnableAbility(abilityName);
}

/// <summary>
/// Disabilita un'abilità dall'inspector (la rende non utilizzabile nel livello)
/// </summary>
public void DisableAbilityFromInspector(string abilityName)
{
    DisableAbility(abilityName);
}
    
    /// <summary>
    /// Disabilita un'abilità specifica
    /// </summary>
    public bool DisableAbility(string abilityName)
    {
        if (!abilityLookup.TryGetValue(abilityName, out AbilityBase ability))
        {
            DebugLog($"[AbilitiesManager] ⚠️ Abilità '{abilityName}' non trovata per disabilitazione");
            return false;
        }

        ability.SetLevelAllowed(false);
        DebugLog($"[AbilitiesManager] Abilità '{abilityName}' disabilitata");
        OnAbilityDisabled?.Invoke(abilityName);

        // Aggiorna la configurazione
        AbilityReference abilityRef = abilities.Find(a => a.ability == ability);
        if (abilityRef != null)
        {
            abilityRef.disabledInLevel = true;
        }

        return true;
    }
    
    /// <summary>
    /// Registra manualmente un'abilità
    /// </summary>
    public void RegisterAbility(AbilityBase ability, bool disabledInLevel = false, bool canBeActivatedByEvents = true)
    {
        if (ability == null)
        {
            DebugLog("[AbilitiesManager] ⚠️ Abilità nulla, ignorata");
            return;
        }
        
        string abilityName = ability.gameObject.name;
        
        // Verifica se già presente
        bool alreadyAdded = abilities.Exists(a => a.ability == ability);
        
        if (!alreadyAdded)
        {
            abilities.Add(new AbilityReference
            {
                ability = ability,
                abilityName = abilityName,
                disabledInLevel = disabledInLevel,
                canBeActivatedByEvents = canBeActivatedByEvents
            });
            
            abilityLookup[abilityName] = ability;
            DebugLog($"[AbilitiesManager] ✅ Abilità registrata: '{abilityName}'");
        }
    }
    
    /// <summary>
    /// Rimuove un'abilità registrata
    /// </summary>
    public bool UnregisterAbility(string abilityName)
    {
        bool removed = false;
        
        if (abilityLookup.ContainsKey(abilityName))
        {
            abilityLookup.Remove(abilityName);
            abilities.RemoveAll(a => a.abilityName == abilityName);
            removed = true;
            DebugLog($"[AbilitiesManager] ❌ Abilità '{abilityName}' rimossa");
        }
        
        return removed;
    }
    
    /// <summary>
    /// Reset del sistema abilità
    /// </summary>
    public void ResetAbilities()
    {
        DebugLog("[AbilitiesManager] Reset del sistema abilità...");
        
        // Ripristina gli eventi disponibili
        availableActivations = new List<EventActivation>(eventActivations);
        
        // Ripristina stati originali
        foreach (var kvp in originalAbilityStates)
        {
            if (abilityLookup.TryGetValue(kvp.Key, out AbilityBase ability))
            {
                ability.SetLevelAllowed(kvp.Value);
            }
        }
        
        // Riapplica la configurazione
        abilitiesConfigured = false;
        ConfigureAbilities();
        
        DebugLog("[AbilitiesManager] Reset completato");
    }
    
    // ========== SALVATAGGIO E CARICAMENTO ==========
    
    /// <summary>
    /// Salva la configurazione delle abilità
    /// </summary>
    public void SaveAbilityConfiguration()
    {
        if (string.IsNullOrEmpty(sceneName)) return;
        
        string sceneKey = sceneName.Replace(" ", "_").Replace("-", "_");
        
        // Salva configurazioni
        foreach (var abilityRef in abilities)
        {
            if (abilityRef.ability == null) continue;
            
            string abilityKey = $"Ability_{sceneKey}_{abilityRef.abilityName}";
            PlayerPrefs.SetInt($"{abilityKey}_DisabledInLevel", abilityRef.disabledInLevel ? 1 : 0);
            PlayerPrefs.SetInt($"{abilityKey}_CanBeActivated", abilityRef.canBeActivatedByEvents ? 1 : 0);
        }
        
        PlayerPrefs.Save();
        DebugLog($"[AbilitiesManager] 💾 Configurazione salvata per '{sceneName}'");
    }
    
    /// <summary>
    /// Carica la configurazione delle abilità
    /// </summary>
    public void LoadAbilityConfiguration()
    {
        if (string.IsNullOrEmpty(sceneName)) return;
        
        string sceneKey = sceneName.Replace(" ", "_").Replace("-", "_");
        
        // Carica configurazioni
        foreach (var abilityRef in abilities)
        {
            if (abilityRef.ability == null) continue;
            
            string abilityKey = $"Ability_{sceneKey}_{abilityRef.abilityName}";
            
            if (PlayerPrefs.HasKey($"{abilityKey}_DisabledInLevel"))
            {
                abilityRef.disabledInLevel = PlayerPrefs.GetInt($"{abilityKey}_DisabledInLevel", 0) == 1;
                abilityRef.canBeActivatedByEvents = PlayerPrefs.GetInt($"{abilityKey}_CanBeActivated", 1) == 1;
            }
        }
        
        DebugLog($"[AbilitiesManager] 📁 Configurazione caricata per '{sceneName}'");
    }
    
    /// <summary>
    /// Cancella i dati salvati
    /// </summary>
    public void ClearSavedConfiguration()
    {
        if (string.IsNullOrEmpty(sceneName)) return;
        
        string sceneKey = sceneName.Replace(" ", "_").Replace("-", "_");
        
        foreach (var abilityRef in abilities)
        {
            if (abilityRef.ability == null) continue;
            
            string abilityKey = $"Ability_{sceneKey}_{abilityRef.abilityName}";
            PlayerPrefs.DeleteKey($"{abilityKey}_DisabledInLevel");
            PlayerPrefs.DeleteKey($"{abilityKey}_CanBeActivated");
        }
        
        PlayerPrefs.Save();
        DebugLog($"[AbilitiesManager] 🗑️ Configurazione cancellata per '{sceneName}'");
    }
    
    // ========== METODI DI CONVENIENZA ==========
    
    /// <summary>
    /// Verifica se un'abilità è disponibile
    /// </summary>
    public bool IsAbilityAvailable(string abilityName)
    {
        if (abilityLookup.TryGetValue(abilityName, out AbilityBase ability))
        {
            return ability.IsEnabled;
        }
        return false;
    }
    
    /// <summary>
    /// Verifica se un'abilità è attiva
    /// </summary>
    public bool IsAbilityActive(string abilityName)
    {
        if (abilityLookup.TryGetValue(abilityName, out AbilityBase ability))
        {
            return ability.IsActive;
        }
        return false;
    }
    
    /// <summary>
    /// Ottieni riferimento a un'abilità
    /// </summary>
    public AbilityBase GetAbility(string abilityName)
    {
        abilityLookup.TryGetValue(abilityName, out AbilityBase ability);
        return ability;
    }
    
    /// <summary>
    /// Lista di tutte le abilità configurate
    /// </summary>
    public List<string> GetAllAbilityNames()
    {
        return new List<string>(abilityLookup.Keys);
    }
    
    /// <summary>
    /// Ottieni tutte le abilità disponibili
    /// </summary>
    public List<AbilityBase> GetAvailableAbilities()
    {
        List<AbilityBase> available = new List<AbilityBase>();
        foreach (var ability in abilityLookup.Values)
        {
            if (ability.IsEnabled)
                available.Add(ability);
        }
        return available;
    }
    
    /// <summary>
    /// Ottieni tutte le abilità disabilitate
    /// </summary>
    public List<AbilityBase> GetDisabledAbilities()
    {
        List<AbilityBase> disabled = new List<AbilityBase>();
        foreach (var ability in abilityLookup.Values)
        {
            if (!ability.IsEnabled)
                disabled.Add(ability);
        }
        return disabled;
    }
    
    // ========== METODI PER COMPATIBILITÀ CON SCENEMANAGER ==========
    
    /// <summary>
    /// Metodo di compatibilità per SceneManager esistenti
    /// </summary>
    public void OnCheckpointReached(string checkpointName)
    {
        ActivateAbilityByEvent($"checkpoint_{checkpointName}");
    }
    
    public void OnCollectibleCollected(string collectibleName)
    {
        ActivateAbilityByEvent($"collectible_{collectibleName}");
    }
    
    public void OnSceneCompleted()
    {
        ActivateAbilityByEvent("scene_completed");
    }
    
    public void OnBossSpawn()
    {
        ActivateAbilityByEvent("boss_spawn");
    }
    
    public void OnPowerUpCollected(string powerUpType)
    {
        ActivateAbilityByEvent($"powerup_{powerUpType}");
    }
    
    /// <summary>
    /// Imposta manualmente il scene name
    /// </summary>
    public void SetSceneName(string newSceneName)
    {
        sceneName = newSceneName;
        DebugLog($"[AbilitiesManager] Scene name impostato: '{sceneName}'");
    }
    
    /// <summary>
    /// Refresh completo del sistema
    /// </summary>
    public void RefreshAbilitiesSystem()
    {
        DebugLog("[AbilitiesManager] Refresh sistema abilità");
        FindAllAbilities();
        LoadAbilityConfiguration();
        ConfigureAbilities();
    }
    
    /// <summary>
    /// Reset completo del progresso
    /// </summary>
    public void ResetAbilitiesProgress()
    {
        DebugLog("[AbilitiesManager] Reset progresso abilità");
        
        // Reset eventi disponibili
        availableActivations = new List<EventActivation>(eventActivations);
        
        // Reset configurazione
        abilitiesConfigured = false;
        ConfigureAbilities();
        
        // Reset dati salvati
        if (autoSaveConfiguration)
        {
            ClearSavedConfiguration();
        }
    }
    
    /// <summary>
    /// Forza il salvataggio dei dati
    /// </summary>
    public void ForceSave()
    {
        SaveAbilityConfiguration();
    }
    
    /// <summary>
    /// Forza il caricamento dei dati
    /// </summary>
    public void ForceLoad()
    {
        LoadAbilityConfiguration();
        ConfigureAbilities();
    }
    
    // ========== SETTINGS ==========
    
    public void SetAutoSaveEnabled(bool enabled)
    {
        autoSaveConfiguration = enabled;
        DebugLog($"[AbilitiesManager] Auto-save {(enabled ? "abilitato" : "disabilitato")}");
    }
    
    public void SetAutoFindEnabled(bool enabled)
    {
        autoFindAbilities = enabled;
        DebugLog($"[AbilitiesManager] Auto-find {(enabled ? "abilitato" : "disabilitato")}");
    }
    
    // ========== DEBUG METHODS ==========
    
    [ContextMenu("Debug - Configure Abilities")]
    public void DebugConfigureAbilities()
    {
        ConfigureAbilities();
    }
    
    [ContextMenu("Debug - Find All Abilities")]
    public void DebugFindAllAbilities()
    {
        FindAllAbilities();
    }
    
    [ContextMenu("Debug - Reset Abilities")]
    public void DebugResetAbilities()
    {
        ResetAbilities();
    }
    
    [ContextMenu("Debug - Show Current State")]
    public void DebugShowCurrentState()
    {
        Debug.Log($"=== AbilitiesManager State ===\n" +
                  $"Scene: '{sceneName}'\n" +
                  $"Configured: {abilitiesConfigured}\n" +
                  $"Total Abilities: {abilities.Count}\n" +
                  $"Available Events: {availableActivations.Count}\n" +
                  $"Ability Lookup: {abilityLookup.Count}\n" +
                  $"Auto-Save: {autoSaveConfiguration}\n" +
                  $"Auto-Find: {autoFindAbilities}");
        
        foreach (var ability in abilities)
        {
            if (ability.ability != null)
            {
                Debug.Log($"- {ability.abilityName}: " +
                          $"LevelDisabled={ability.disabledInLevel}, " +
                          $"EventActivation={ability.canBeActivatedByEvents}, " +
                          $"Available={ability.ability.IsEnabled}, " +
                          $"Active={ability.ability.IsActive}");
            }
        }
    }
    
    [ContextMenu("Test - Enable All Abilities")]
    public void DebugEnableAllAbilities()
    {
        foreach (var abilityName in abilityLookup.Keys)
        {
            EnableAbility(abilityName);
        }
    }
    
    [ContextMenu("Test - Disable All Abilities")]
    public void DebugDisableAllAbilities()
    {
        foreach (var abilityName in abilityLookup.Keys)
        {
            DisableAbility(abilityName);
        }
    }
    
    [ContextMenu("Force Save")]
    public void DebugForceSave()
    {
        ForceSave();
    }
    
    [ContextMenu("Force Load")]
    public void DebugForceLoad()
    {
        ForceLoad();
        DebugShowCurrentState();
    }
    
    // ========== UTILITY ==========
    
    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log(message);
        }
    }
    
    /// <summary>
    /// Abilita/disabilita i log di debug
    /// </summary>
    public void SetDebugLogsEnabled(bool enabled)
    {
        enableDebugLogs = enabled;
    }
    
    // ========== CLEANUP ==========

}