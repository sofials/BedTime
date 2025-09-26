using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

/// <summary>
/// Script per il Main Menu con supporto per navigazione verso Levels UI
/// Include auto-detection dei bottoni e integrazione con VideoIntroManager
/// </summary>
public class MainMenu : MonoBehaviour
{
    [Header("Scene Loading")]
    [SerializeField] private SceneController _sceneController;
    
    [Header("Video Intro")]
    [SerializeField] private VideoIntroManager videoIntroManager;
    [SerializeField] private bool useVideoIntro = true; // Flag per abilitare/disabilitare il video

    [Header("Auto-Detection Settings")]
    [SerializeField] private bool debugMode = false;
    [SerializeField] private bool autoFindButtons = true; // Se true, trova automaticamente i bottoni
    
    [Header("Button References (Opzionale - vengono trovati automaticamente)")]
    [SerializeField] private Button playButton;
    [SerializeField] private Button levelsButton; // Nuovo bottone per i livelli
    [SerializeField] private Button exitButton;
    
    [Header("UI Elements")]
    [SerializeField] private GameObject mainMenuPanel; // Panel principale del main menu
    [SerializeField] private GameObject levelsUI; // UI dei livelli da attivare
    
    // Stati
    private bool isInitialized = false;

    private void Awake()
    {
        // Trova il panel principale se non specificato
        if (mainMenuPanel == null)
        {
            mainMenuPanel = gameObject;
        }
        
        // Trova automaticamente il LEVELSUI se non specificato
        if (levelsUI == null)
        {
            levelsUI = FindLevelsUI();
        }
    }

    private void Start()
    {
        StartCoroutine(DelayedInitialization());
    }

    /// <summary>
    /// Trova automaticamente l'oggetto LEVELSUI nella scena
    /// </summary>
    private GameObject FindLevelsUI()
    {
        // Prima cerca per nome esatto
        GameObject levelsUIObject = GameObject.Find("LEVELSUI");
        if (levelsUIObject != null)
        {
            if (debugMode) Debug.Log($"[MainMenu] LEVELSUI trovato per nome: {levelsUIObject.name}");
            return levelsUIObject;
        }
        
        // Cerca con varianti del nome
        string[] possibleNames = { "LevelsUI", "Levels UI", "LevelSelect", "Level Select", "LevelSelection" };
        
        foreach (string name in possibleNames)
        {
            levelsUIObject = GameObject.Find(name);
            if (levelsUIObject != null)
            {
                if (debugMode) Debug.Log($"[MainMenu] LEVELSUI trovato con nome alternativo: {levelsUIObject.name}");
                return levelsUIObject;
            }
        }
        
        // Cerca tramite tag se disponibile
        levelsUIObject = GameObject.FindGameObjectWithTag("LevelsUI");
        if (levelsUIObject != null)
        {
            if (debugMode) Debug.Log($"[MainMenu] LEVELSUI trovato tramite tag: {levelsUIObject.name}");
            return levelsUIObject;
        }
        
        // Cerca tramite componente LevelsMenuUI
        LevelsMenuUI levelsComponent = FindFirstObjectByType<LevelsMenuUI>();
        if (levelsComponent != null)
        {
            if (debugMode) Debug.Log($"[MainMenu] LEVELSUI trovato tramite componente: {levelsComponent.gameObject.name}");
            return levelsComponent.gameObject;
        }
        
        if (debugMode) Debug.LogWarning("[MainMenu] LEVELSUI non trovato automaticamente. Assegnalo manualmente nell'Inspector.");
        return null;
    }

    /// <summary>
    /// Inizializzazione ritardata
    /// </summary>
    private IEnumerator DelayedInitialization()
    {
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();
        
        if (autoFindButtons)
        {
            FindAndConnectButtons();
        }
        else
        {
            ConnectAssignedButtons();
        }
        
        // Assicurati che il LEVELSUI sia nascosto all'inizio
        if (levelsUI != null)
        {
            levelsUI.SetActive(false);
        }
        
        isInitialized = true;
        
        if (debugMode)
        {
            Debug.Log($"[MainMenu] Inizializzazione completata per: {gameObject.name}");
            LogDebugInfo();
        }
    }

    /// <summary>
    /// Trova automaticamente i bottoni nel main menu
    /// </summary>
    private void FindAndConnectButtons()
    {
        if (debugMode) Debug.Log("[MainMenu] Ricerca automatica bottoni...");
        
        Button[] allButtons = GetComponentsInChildren<Button>(true);
        
        foreach (Button button in allButtons)
        {
            string buttonName = button.name.ToLower();
            
            if (IsPlayButton(buttonName))
            {
                if (playButton == null) playButton = button;
                ConnectPlayButton(button);
            }
            else if (IsLevelsButton(buttonName))
            {
                if (levelsButton == null) levelsButton = button;
                ConnectLevelsButton(button);
            }
            else if (IsExitButton(buttonName))
            {
                if (exitButton == null) exitButton = button;
                ConnectExitButton(button);
            }
            else if (debugMode)
            {
                Debug.Log($"[MainMenu] Bottone non riconosciuto: {button.name}");
            }
        }
        
        if (debugMode)
        {
            Debug.Log($"[MainMenu] Bottoni trovati - Play: {playButton != null}, Levels: {levelsButton != null}, Exit: {exitButton != null}");
        }
    }

    /// <summary>
    /// Collega i bottoni già assegnati manualmente
    /// </summary>
    private void ConnectAssignedButtons()
    {
        if (playButton != null) ConnectPlayButton(playButton);
        if (levelsButton != null) ConnectLevelsButton(levelsButton);
        if (exitButton != null) ConnectExitButton(exitButton);
        
        if (debugMode) Debug.Log("[MainMenu] Bottoni assegnati manualmente collegati");
    }

    // ========== METODI DI IDENTIFICAZIONE BOTTONI ==========

    private bool IsPlayButton(string name)
    {
        return name.Contains("play") || name.Contains("gioca") || name.Contains("start") || 
               name.Contains("inizia") || name.Contains("begin");
    }

    private bool IsLevelsButton(string name)
    {
        return name.Contains("levels") || name.Contains("livelli") || name.Contains("level") || 
               name.Contains("livello") || name.Contains("select") || name.Contains("selezione");
    }

    private bool IsExitButton(string name)
    {
        return name.Contains("exit") || name.Contains("quit") || name.Contains("esci") || 
               name.Contains("chiudi") || name.Contains("close");
    }

    // ========== METODI DI COLLEGAMENTO BOTTONI ==========

    private void ConnectPlayButton(Button button)
    {
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(Play);
        if (debugMode) Debug.Log($"[MainMenu] Bottone Play collegato: {button.name}");
    }

    private void ConnectLevelsButton(Button button)
    {
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(OnLevelsClicked);
        if (debugMode) Debug.Log($"[MainMenu] Bottone Levels collegato: {button.name}");
    }

    private void ConnectExitButton(Button button)
    {
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(Exit);
        if (debugMode) Debug.Log($"[MainMenu] Bottone Exit collegato: {button.name}");
    }

    // ========== GESTORI EVENTI BOTTONI ==========

    /// <summary>
    /// Funzionalità originale del bottone Play
    /// </summary>
    public void Play()
    {
        if (debugMode) Debug.Log("[MainMenu] Play button clicked");
        
        if (useVideoIntro && videoIntroManager != null)
        {
            // Avvia il video intro che poi caricherà automaticamente la scena
            Debug.Log("[MainMenu] Avvio video intro...");
            videoIntroManager.StartIntro();
        }
        else
        {
            // Carica direttamente la scena (comportamento originale)
            Debug.Log("[MainMenu] Caricamento diretto della scena (no video)...");
            LoadGameScene();
        }
    }
    
    /// <summary>
    /// Carica la scena di gioco direttamente (senza video)
    /// </summary>
    public void LoadGameScene()
    {
        if (_sceneController != null)
        {
            _sceneController.LoadScene("00 - Landing in the Dreamworld");
        }
        else
        {
            // Fallback
            SceneManager.LoadScene("00 - Landing in the Dreamworld");
        }
    }
    
    /// <summary>
    /// Salta il video e carica direttamente la scena
    /// </summary>
    public void SkipVideoAndPlay()
    {
        if (videoIntroManager != null)
        {
            videoIntroManager.SkipVideo();
        }
        else
        {
            LoadGameScene();
        }
    }

    /// <summary>
    /// Gestisce il click sul bottone Levels
    /// </summary>
    public void OnLevelsClicked()
    {
        if (debugMode) Debug.Log("[MainMenu] Levels button clicked");
        
        // Nascondi il main menu
        HideMainMenu();
        
        // Attiva il LEVELSUI se disponibile
        if (levelsUI != null)
        {
            levelsUI.SetActive(true);
            
            // Imposta il riferimento al main menu nel LevelsMenuUI per il ritorno
            LevelsMenuUI levelsComponent = levelsUI.GetComponent<LevelsMenuUI>();
            if (levelsComponent != null)
            {
                levelsComponent.SetPauseMenuUI(gameObject); // Usa il MainMenu come "PauseMenu" per il back
            }
            
            if (debugMode) Debug.Log($"[MainMenu] LEVELSUI attivato: {levelsUI.name}");
        }
        else
        {
            Debug.LogWarning("[MainMenu] LEVELSUI non trovato! Non posso aprire il menu dei livelli.");
            // Se non trova il Levels UI, rimetti il main menu visibile
            ShowMainMenu();
        }
    }

    /// <summary>
    /// Chiudi l'applicazione
    /// </summary>
    public void Exit()
    {
        if (debugMode) Debug.Log("[MainMenu] Exit button clicked");
        Application.Quit();
    }

    // ========== GESTIONE VISIBILITÀ ==========

    /// <summary>
    /// Mostra il main menu
    /// </summary>
    public void ShowMainMenu()
    {
        if (mainMenuPanel != null)
        {
            mainMenuPanel.SetActive(true);
        }
        
        if (debugMode) Debug.Log("[MainMenu] Main menu mostrato");
    }
    
    /// <summary>
    /// Nascondi il main menu
    /// </summary>
    public void HideMainMenu()
    {
        if (mainMenuPanel != null)
        {
            mainMenuPanel.SetActive(false);
        }
        
        if (debugMode) Debug.Log("[MainMenu] Main menu nascosto");
    }

    /// <summary>
    /// Equivalente al metodo Show() del PauseMenuUI per compatibilità con LevelsMenuUI
    /// </summary>
    public void Show()
    {
        ShowMainMenu();
    }

    // ========== METODI PUBBLICI PER CONTROLLO ESTERNO ==========

    /// <summary>
    /// Ottieni il riferimento al Levels UI
    /// </summary>
    public GameObject GetLevelsUI()
    {
        return levelsUI;
    }

    /// <summary>
    /// Controlla se il Levels UI è attualmente attivo
    /// </summary>
    public bool IsLevelsUIActive()
    {
        return levelsUI != null && levelsUI.activeSelf;
    }

    /// <summary>
    /// Imposta manualmente il riferimento al LEVELSUI
    /// </summary>
    public void SetLevelsUI(GameObject levelsUIObject)
    {
        levelsUI = levelsUIObject;
        if (debugMode) Debug.Log($"[MainMenu] LEVELSUI impostato manualmente: {levelsUI.name}");
    }

    /// <summary>
    /// Ottieni lo stato di inizializzazione
    /// </summary>
    public bool IsInitialized()
    {
        return isInitialized;
    }

    // ========== DEBUG E CONTEXT MENU ==========

    [ContextMenu("Test Play")]
    public void TestPlay()
    {
        Play();
    }

    [ContextMenu("Test Levels")]
    public void TestLevels()
    {
        OnLevelsClicked();
    }

    [ContextMenu("Test Exit")]
    public void TestExit()
    {
        Debug.Log("[MainMenu] Exit test (non chiude in editor)");
    }

    [ContextMenu("Force Find LevelsUI")]
    public void ForceFindLevelsUI()
    {
        levelsUI = FindLevelsUI();
        if (levelsUI != null)
        {
            Debug.Log($"[MainMenu] LEVELSUI trovato: {levelsUI.name}");
        }
        else
        {
            Debug.LogWarning("[MainMenu] LEVELSUI non trovato!");
        }
    }

    [ContextMenu("Force Reconnect Buttons")]
    public void ForceReconnectButtons()
    {
        if (autoFindButtons)
        {
            FindAndConnectButtons();
        }
        else
        {
            ConnectAssignedButtons();
        }
        Debug.Log("[MainMenu] Bottoni ricollegati forzatamente");
    }

    [ContextMenu("Debug Info")]
    public void LogDebugInfo()
    {
        Debug.Log($"=== MainMenu Debug Info ===\n" +
                  $"GameObject: {gameObject.name}\n" +
                  $"Initialized: {isInitialized}\n" +
                  $"Auto Find Buttons: {autoFindButtons}\n" +
                  $"Use Video Intro: {useVideoIntro}\n" +
                  $"Play Button: {(playButton != null ? playButton.name : "❌")}\n" +
                  $"Levels Button: {(levelsButton != null ? levelsButton.name : "❌")}\n" +
                  $"Exit Button: {(exitButton != null ? exitButton.name : "❌")}\n" +
                  $"Main Menu Panel: {(mainMenuPanel != null ? mainMenuPanel.name : "❌")}\n" +
                  $"Levels UI: {(levelsUI != null ? levelsUI.name : "❌")}\n" +
                  $"Scene Controller: {(_sceneController != null ? _sceneController.name : "❌")}\n" +
                  $"Video Intro Manager: {(videoIntroManager != null ? videoIntroManager.name : "❌")}");
    }
}