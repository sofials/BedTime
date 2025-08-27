using UnityEngine;

public class SceneTrigger : MonoBehaviour
{
    [SerializeField]
    private string sceneToLoad = "01 - Party in Lukelandia";
    
    [SerializeField]
    private string playerTag = "Player";
    
    [SerializeField]
    private SceneController _sceneController;
    
    [Header("Toggle Settings")]
    [SerializeField]
    [Tooltip("Abilita/Disabilita la modalità trigger automatico")]
    private bool enableTriggerMode = true;
    
    private bool _isLoading = false;
    
    // Debug per monitorare il valore
    [Header("Debug Info")]
    [SerializeField, Tooltip("Solo per debug - mostra il valore corrente")]
    private bool debugEnableTriggerMode;
    
    private void Start()
    {
        // Assicurati che il collider sia impostato come trigger
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
        else
        {
            Debug.LogWarning("Nessun Collider trovato su " + gameObject.name);
        }
        
        // Inizializza il debug
        UpdateDebugInfo();
    }
    
    private void Update()
    {
        // Aggiorna continuamente il valore di debug per monitorare cambiamenti
        UpdateDebugInfo();
    }
    
    private void UpdateDebugInfo()
    {
        debugEnableTriggerMode = enableTriggerMode;
    }
    
    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"OnTriggerEnter chiamato. EnableTriggerMode: {enableTriggerMode}, Player: {other.CompareTag(playerTag)}, IsLoading: {_isLoading}");
        
        // Controlla se la modalità trigger è abilitata, se l'oggetto che entra è il player e se non stiamo già caricando
        if (enableTriggerMode && other.CompareTag(playerTag) && !_isLoading)
        {
            Debug.Log("Player entrato nel trigger, caricamento scena: " + sceneToLoad);
            LoadSceneInternal();
        }
        else if (!enableTriggerMode)
        {
            Debug.Log("Trigger mode disabilitato - non carico la scena");
        }
        else if (!other.CompareTag(playerTag))
        {
            Debug.Log($"Oggetto entrato non è il player: {other.name} (tag: {other.tag})");
        }
        else if (_isLoading)
        {
            Debug.Log("Caricamento già in corso");
        }
    }
    
    /// <summary>
    /// Metodo pubblico per attivare l'oggetto SceneTrigger
    /// NOTA: Questo metodo attiva il GameObject contenente questo script, non carica direttamente la scena
    /// </summary>
    public void ActivateSceneTrigger()
    {
        Debug.Log($"ActivateSceneTrigger chiamato. Attivando l'oggetto: {gameObject.name}");
        
        // Attiva l'oggetto se è disattivato
        if (!gameObject.activeInHierarchy)
        {
            gameObject.SetActive(true);
            Debug.Log($"Oggetto {gameObject.name} attivato. Il trigger ora è funzionante.");
        }
        else
        {
            Debug.Log($"Oggetto {gameObject.name} è già attivo.");
        }
    }
    
    /// <summary>
    /// Versione alternativa che rispetta enableTriggerMode anche per l'attivazione
    /// </summary>
    public void ActivateSceneTriggerWithCheck()
    {
        Debug.Log($"ActivateSceneTriggerWithCheck chiamato. EnableTriggerMode: {enableTriggerMode}");
        
        if (enableTriggerMode)
        {
            if (!gameObject.activeInHierarchy)
            {
                gameObject.SetActive(true);
                Debug.Log($"Oggetto {gameObject.name} attivato (con check). Il trigger ora è funzionante.");
            }
            else
            {
                Debug.Log($"Oggetto {gameObject.name} è già attivo.");
            }
        }
        else
        {
            Debug.Log("Trigger mode disabilitato - attivazione dell'oggetto bloccata");
        }
    }
    
    /// <summary>
    /// Metodo per disattivare l'oggetto SceneTrigger
    /// </summary>
    public void DeactivateSceneTrigger()
    {
        Debug.Log($"DeactivateSceneTrigger chiamato. Disattivando l'oggetto: {gameObject.name}");
        gameObject.SetActive(false);
    }
    
    /// <summary>
    /// Metodo pubblico per caricare direttamente la scena (mantiene la funzionalità originale)
    /// </summary>
    public void ForceLoadScene()
    {
        Debug.Log($"ForceLoadScene chiamato. EnableTriggerMode: {enableTriggerMode}, IsLoading: {_isLoading}");
        
        if (!_isLoading)
        {
            Debug.Log("Caricamento scena forzato: " + sceneToLoad);
            LoadSceneInternal();
        }
        else
        {
            Debug.Log("Caricamento già in corso, impossibile forzare il caricamento");
        }
    }
    
    /// <summary>
    /// Abilita o disabilita la modalità trigger automatico
    /// </summary>
    /// <param name="enabled">True per abilitare, False per disabilitare</param>
    public void SetTriggerMode(bool enabled)
    {
        bool previousValue = enableTriggerMode;
        enableTriggerMode = enabled;
        Debug.Log($"SetTriggerMode: {previousValue} -> {enableTriggerMode}. Modalità trigger " + (enabled ? "abilitata" : "disabilitata"));
        UpdateDebugInfo();
    }
    
    /// <summary>
    /// Restituisce se la modalità trigger è attualmente abilitata
    /// </summary>
    /// <returns>True se abilitata, False se disabilitata</returns>
    public bool IsTriggerModeEnabled()
    {
        Debug.Log($"IsTriggerModeEnabled chiamato, valore: {enableTriggerMode}");
        return enableTriggerMode;
    }
    
    /// <summary>
    /// Toggle della modalità trigger (abilita se disabilitata, disabilita se abilitata)
    /// </summary>
    public void ToggleTriggerMode()
    {
        Debug.Log($"ToggleTriggerMode chiamato, valore prima: {enableTriggerMode}");
        SetTriggerMode(!enableTriggerMode);
        Debug.Log($"ToggleTriggerMode completato, valore dopo: {enableTriggerMode}");
    }
    
    /// <summary>
    /// Metodo per forzare il valore di enableTriggerMode (utile per debug)
    /// </summary>
    [ContextMenu("Force Enable Trigger Mode")]
    public void ForceEnableTriggerMode()
    {
        enableTriggerMode = true;
        Debug.Log("Trigger Mode forzato a TRUE");
        UpdateDebugInfo();
    }
    
    [ContextMenu("Force Disable Trigger Mode")]
    public void ForceDisableTriggerMode()
    {
        enableTriggerMode = false;
        Debug.Log("Trigger Mode forzato a FALSE");
        UpdateDebugInfo();
    }
    
    /// <summary>
    /// Metodo interno per gestire il caricamento della scena
    /// </summary>
    private void LoadSceneInternal()
    {
        if (_sceneController != null)
        {
            _isLoading = true;
            Debug.Log($"Iniziando caricamento scena: {sceneToLoad}");
            _sceneController.LoadScene(sceneToLoad);
        }
        else
        {
            Debug.LogError("SceneController non è assegnato nell'Inspector!");
        }
    }
    
    /// <summary>
    /// Metodo per resettare lo stato di loading (utile se il caricamento fallisce)
    /// </summary>
    [ContextMenu("Reset Loading State")]
    public void ResetLoadingState()
    {
        _isLoading = false;
        Debug.Log("Stato di loading resettato");
    }
}