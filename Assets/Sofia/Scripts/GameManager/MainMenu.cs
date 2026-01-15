using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

/// <summary>
/// Script per il Main Menu - RISCRITTO seguendo la logica di PauseMenuUI che funziona
/// </summary>
public class MainMenu : MonoBehaviour
{
    [Header("Scene Loading")]
    [SerializeField] private SceneController _sceneController;
    
    [Header("Video Intro")]
    [SerializeField] private VideoIntroManager videoIntroManager;
    [SerializeField] private bool useVideoIntro = false;

    [Header("Auto-Detection Settings")]
    [SerializeField] private bool debugMode = false;
    [SerializeField] private bool autoFindButtons = true;
    
    [Header("Button References (Opzionale - vengono trovati automaticamente)")]
    [SerializeField] private Button playButton;
    [SerializeField] private Button levelsButton;
    [SerializeField] private Button exitButton;
    
    [Header("UI Elements")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject levelsUI; // UI dei livelli da attivare
    
    // Stati
    private bool isInitialized = false;

    private void Awake()
    {
        if (mainMenuPanel == null)
        {
            mainMenuPanel = gameObject;
        }
        
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
    /// Trova automaticamente l'oggetto LEVELSUI nella scena - IDENTICO a PauseMenuUI
    /// </summary>
    private GameObject FindLevelsUI()
    {
        GameObject levelsUIObject = GameObject.Find("LEVELSUI");
        if (levelsUIObject != null)
        {
            if (debugMode) Debug.Log($"[MainMenu] LEVELSUI trovato per nome: {levelsUIObject.name}");
            return levelsUIObject;
        }
        
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
        
        levelsUIObject = GameObject.FindGameObjectWithTag("LevelsUI");
        if (levelsUIObject != null)
        {
            if (debugMode) Debug.Log($"[MainMenu] LEVELSUI trovato tramite tag: {levelsUIObject.name}");
            return levelsUIObject;
        }
        
        if (debugMode) Debug.LogWarning("[MainMenu] LEVELSUI non trovato automaticamente. Assegnalo manualmente nell'Inspector.");
        return null;
    }

    /// <summary>
    /// Inizializzazione ritardata - IDENTICA a PauseMenuUI
    /// </summary>
    private IEnumerator DelayedInitialization()
    {
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();
        yield return new WaitForSeconds(0.1f);
        
        bool buttonsConnected = false;
        int retryCount = 0;
        int maxRetries = 5;
        
        while (!buttonsConnected && retryCount < maxRetries)
        {
            if (autoFindButtons)
            {
                FindAndConnectButtons();
            }
            else
            {
                ConnectAssignedButtons();
            }
            
            buttonsConnected = (playButton != null || levelsButton != null || exitButton != null);
            
            if (!buttonsConnected)
            {
                retryCount++;
                if (debugMode) Debug.LogWarning($"[MainMenu] Tentativo {retryCount}/{maxRetries} - Bottoni non trovati, riprovo...");
                yield return new WaitForSeconds(0.2f);
            }
        }
        
        if (!buttonsConnected)
        {
            Debug.LogError("[MainMenu] ERRORE: Nessun bottone è stato collegato dopo tutti i tentativi!");
        }
        
        // Assicurati che il LEVELSUI sia nascosto all'inizio
        if (levelsUI != null)
        {
            levelsUI.SetActive(false);
        }
        
        // Assicurati che il main menu sia visibile
        ShowMainMenu();
        
        isInitialized = true;
        
        if (debugMode)
        {
            Debug.Log($"[MainMenu] Inizializzazione completata per: {gameObject.name}");
            LogDebugInfo();
        }
    }

    /// <summary>
    /// Trova automaticamente i bottoni - IDENTICO a PauseMenuUI
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

    private void ConnectAssignedButtons()
    {
        if (playButton != null) ConnectPlayButton(playButton);
        if (levelsButton != null) ConnectLevelsButton(levelsButton);
        if (exitButton != null) ConnectExitButton(exitButton);
        
        if (debugMode) Debug.Log("[MainMenu] Bottoni assegnati manualmente collegati");
    }

    // ========== IDENTIFICAZIONE BOTTONI ==========

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

    // ========== COLLEGAMENTO BOTTONI ==========

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

    // ========== GESTORI EVENTI - Play e Exit rimangono come prima ==========

    public void Play()
    {
        if (debugMode) Debug.Log("[MainMenu] Play button clicked");
        
        if (useVideoIntro && videoIntroManager != null)
        {
            Debug.Log("[MainMenu] Avvio video intro...");
            videoIntroManager.StartIntro();
        }
        else
        {
            Debug.Log("[MainMenu] Caricamento diretto della scena (no video)...");
            LoadGameScene();
        }
    }
    
    public void LoadGameScene()
    {
        if (_sceneController != null)
        {
            _sceneController.LoadScene("00 - Landing in the Dreamworld");
        }
        else
        {
            SceneManager.LoadScene("00 - Landing in the Dreamworld");
        }
    }
    
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

    public void Exit()
    {
        if (debugMode) Debug.Log("[MainMenu] Exit button clicked");
        Application.Quit();
    }

    // ========== GESTORE LEVELS - IDENTICO a PauseMenuUI.OnLevelsClicked() ==========

    public void OnLevelsClicked()
    {
        if (debugMode) Debug.Log("[MainMenu] Levels button clicked");
        
        // IDENTICO A PAUSEMENUUI: Nascondi completamente questo menu prima di aprire Levels UI
        HideMainMenu();
        
        // Attiva il LEVELSUI se disponibile
        if (levelsUI != null)
        {
            levelsUI.SetActive(true);
            
            // Imposta il riferimento al MainMenu nel LevelsMenuUI per il ritorno
            LevelsMenuUI levelsComponent = levelsUI.GetComponent<LevelsMenuUI>();
            if (levelsComponent != null)
            {
                levelsComponent.SetPauseMenuUI(gameObject); // Usa lo stesso metodo!
            }
            
            if (debugMode) Debug.Log($"[MainMenu] LEVELSUI attivato: {levelsUI.name}");
        }
        else
        {
            Debug.LogWarning("[MainMenu] LEVELSUI non trovato! Non posso aprire il menu dei livelli.");
            ShowMainMenu();
        }
    }

    // ========== GESTIONE VISIBILITÀ ==========

    public void ShowMainMenu()
    {
        if (mainMenuPanel != null)
        {
            mainMenuPanel.SetActive(true);
        }
        
        if (levelsUI != null && levelsUI.activeSelf)
        {
            levelsUI.SetActive(false);
        }
        
        if (debugMode) Debug.Log("[MainMenu] Main menu mostrato, Levels UI nascosto");
    }
    
    public void HideMainMenu()
    {
        if (mainMenuPanel != null)
        {
            mainMenuPanel.SetActive(false);
        }
        
        if (debugMode) Debug.Log("[MainMenu] Main menu nascosto");
    }

    /// <summary>
    /// Equivalente a Show() per compatibilità con LevelsMenuUI
    /// </summary>
    public void Show()
    {
        ShowMainMenu();
    }

    // ========== METODI PUBBLICI ==========

    public GameObject GetLevelsUI()
    {
        return levelsUI;
    }

    public bool IsLevelsUIActive()
    {
        return levelsUI != null && levelsUI.activeSelf;
    }

    public void SetLevelsUI(GameObject levelsUIObject)
    {
        levelsUI = levelsUIObject;
        if (debugMode) Debug.Log($"[MainMenu] LEVELSUI impostato manualmente: {levelsUI.name}");
    }

    public bool IsInitialized()
    {
        return isInitialized;
    }

    public void ForceReturnToMainMenu()
    {
        if (debugMode) Debug.Log("[MainMenu] Forzato ritorno al main menu");
        ShowMainMenu();
    }

    // ========== DEBUG ==========

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
                  $"Levels UI Active: {(levelsUI != null ? levelsUI.activeSelf.ToString() : "N/A")}\n" +
                  $"Scene Controller: {(_sceneController != null ? _sceneController.name : "❌")}\n" +
                  $"Video Intro Manager: {(videoIntroManager != null ? videoIntroManager.name : "❌")}");
    }
}