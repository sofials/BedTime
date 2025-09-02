using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Script per la UI del menu di pausa che si collega automaticamente al GameManager
/// Attaccare questo script al GameObject principale del menu di pausa
/// </summary>
public class PauseMenuUI : MonoBehaviour
{
    [Header("Auto-Detection Settings")]
    [SerializeField] private bool debugMode = false;
    [SerializeField] private bool autoFindButtons = true; // Se true, trova automaticamente i bottoni
    
    [Header("Button References (Opzionale - vengono trovati automaticamente)")]
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button mainMenuButton;
    [SerializeField] private Button exitButton;
    
    [Header("UI Elements (Opzionale)")]
    [SerializeField] private GameObject pausePanel; // Panel principale del menu
    [SerializeField] private CanvasGroup canvasGroup; // Per animazioni fade
    
    // Riferimenti automatici
    private GameManager gameManager;
    private Canvas parentCanvas;
    
    // Stati
    private bool isInitialized = false;

    private void Awake()
    {
        // Trova il Canvas genitore se non specificato
        if (parentCanvas == null)
        {
            parentCanvas = GetComponentInParent<Canvas>();
        }
        
        // Trova il CanvasGroup se non specificato
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }
        
        // Trova il panel principale se non specificato
        if (pausePanel == null)
        {
            pausePanel = gameObject;
        }
    }

    private void Start()
    {
        // Inizializzazione ritardata per permettere al GameManager di caricarsi
        StartCoroutine(DelayedInitialization());
    }

    /// <summary>
    /// Inizializzazione ritardata per assicurarsi che il GameManager sia pronto
    /// </summary>
    private IEnumerator DelayedInitialization()
    {
        // Aspetta alcuni frame
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();
        
        // Cerca il GameManager
        FindGameManager();
        
        // Trova e collega i bottoni automaticamente
        if (autoFindButtons)
        {
            FindAndConnectButtons();
        }
        else
        {
            // Se non auto, collega i bottoni già assegnati
            ConnectAssignedButtons();
        }
        
        // Assicurati che il menu sia nascosto all'inizio
        Hide();
        
        // Sottoscrivi agli eventi del GameManager se disponibile
        SubscribeToGameManagerEvents();
        
        isInitialized = true;
        
        if (debugMode)
        {
            Debug.Log($"[PauseMenuUI] Inizializzazione completata per: {gameObject.name}");
            LogDebugInfo();
        }
    }

    /// <summary>
    /// Trova automaticamente il GameManager nella scena
    /// </summary>
    private void FindGameManager()
    {
        // Prima cerca il singleton
        if (GameManager.Instance != null)
        {
            gameManager = GameManager.Instance;
            if (debugMode) Debug.Log("[PauseMenuUI] GameManager trovato tramite Instance");
            return;
        }
        
        // Se non trovato come singleton, cerca nell'oggetto
        gameManager = Object.FindFirstObjectByType<GameManager>();
        
        if (gameManager != null)
        {
            if (debugMode) Debug.Log($"[PauseMenuUI] GameManager trovato: {gameManager.name}");
        }
        else
        {
            Debug.LogError("[PauseMenuUI] GameManager non trovato! Assicurati che ci sia un GameManager nella scena.");
        }
    }

    /// <summary>
    /// Trova automaticamente i bottoni nel menu e li collega ai metodi
    /// </summary>
    private void FindAndConnectButtons()
    {
        if (debugMode) Debug.Log("[PauseMenuUI] Ricerca automatica bottoni...");
        
        // Ottieni tutti i bottoni figli di questo oggetto
        Button[] allButtons = GetComponentsInChildren<Button>(true);
        
        foreach (Button button in allButtons)
        {
            string buttonName = button.name.ToLower();
            
            // Identifica i bottoni in base al nome
            if (IsResumeButton(buttonName))
            {
                if (resumeButton == null) resumeButton = button;
                ConnectResumeButton(button);
            }
            else if (IsRestartButton(buttonName))
            {
                if (restartButton == null) restartButton = button;
                ConnectRestartButton(button);
            }
            else if (IsMainMenuButton(buttonName))
            {
                if (mainMenuButton == null) mainMenuButton = button;
                ConnectMainMenuButton(button);
            }
            else if (IsExitButton(buttonName))
            {
                if (exitButton == null) exitButton = button;
                ConnectExitButton(button);
            }
            else if (debugMode)
            {
                Debug.Log($"[PauseMenuUI] Bottone non riconosciuto: {button.name}");
            }
        }
        
        if (debugMode)
        {
            Debug.Log($"[PauseMenuUI] Bottoni trovati - Resume: {resumeButton != null}, Restart: {restartButton != null}, MainMenu: {mainMenuButton != null}, Exit: {exitButton != null}");
        }
    }

    /// <summary>
    /// Collega i bottoni già assegnati manualmente nell'inspector
    /// </summary>
    private void ConnectAssignedButtons()
    {
        if (resumeButton != null) ConnectResumeButton(resumeButton);
        if (restartButton != null) ConnectRestartButton(restartButton);
        if (mainMenuButton != null) ConnectMainMenuButton(mainMenuButton);
        if (exitButton != null) ConnectExitButton(exitButton);
        
        if (debugMode) Debug.Log("[PauseMenuUI] Bottoni assegnati manualmente collegati");
    }

    // ========== METODI DI IDENTIFICAZIONE BOTTONI ==========

    private bool IsResumeButton(string name)
    {
        return name.Contains("resume") || name.Contains("riprendi") || name.Contains("continue") || 
               name.Contains("continua") || name.Contains("play");
    }

    private bool IsRestartButton(string name)
    {
        return name.Contains("restart") || name.Contains("riavvia") || name.Contains("reload") || 
               name.Contains("ricarica") || name.Contains("retry");
    }

    private bool IsMainMenuButton(string name)
    {
        return name.Contains("mainmenu") || name.Contains("main") || name.Contains("menu") || 
               name.Contains("home") || name.Contains("back") || name.Contains("indietro");
    }

    private bool IsExitButton(string name)
    {
        return name.Contains("exit") || name.Contains("quit") || name.Contains("esci") || 
               name.Contains("chiudi") || name.Contains("close");
    }

    // ========== METODI DI COLLEGAMENTO BOTTONI ==========

    private void ConnectResumeButton(Button button)
    {
        button.onClick.RemoveAllListeners(); // Pulisce listener esistenti
        button.onClick.AddListener(OnResumeClicked);
        if (debugMode) Debug.Log($"[PauseMenuUI] Bottone Resume collegato: {button.name}");
    }

    private void ConnectRestartButton(Button button)
    {
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(OnRestartClicked);
        if (debugMode) Debug.Log($"[PauseMenuUI] Bottone Restart collegato: {button.name}");
    }

    private void ConnectMainMenuButton(Button button)
    {
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(OnMainMenuClicked);
        if (debugMode) Debug.Log($"[PauseMenuUI] Bottone MainMenu collegato: {button.name}");
    }

    private void ConnectExitButton(Button button)
    {
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(OnExitClicked);
        if (debugMode) Debug.Log($"[PauseMenuUI] Bottone Exit collegato: {button.name}");
    }

    // ========== GESTORI EVENTI BOTTONI ==========

    public void OnResumeClicked()
    {
        if (debugMode) Debug.Log("[PauseMenuUI] Resume button clicked");
        
        if (gameManager != null)
        {
            gameManager.ResumeGame();
        }
        else
        {
            Debug.LogError("[PauseMenuUI] GameManager non disponibile per Resume!");
            // Fallback: nascondi il menu e ripristina il gioco manualmente
            Hide();
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    public void OnRestartClicked()
    {
        if (debugMode) Debug.Log("[PauseMenuUI] Restart button clicked");
        
        if (gameManager != null)
        {
            gameManager.RestartLevel();
        }
        else
        {
            Debug.LogError("[PauseMenuUI] GameManager non disponibile per Restart!");
            // Fallback: ricarica la scena corrente
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        }
    }

    public void OnMainMenuClicked()
    {
        if (debugMode) Debug.Log("[PauseMenuUI] MainMenu button clicked");
        
        if (gameManager != null)
        {
            gameManager.GoToMainMenu();
        }
        else
        {
            Debug.LogError("[PauseMenuUI] GameManager non disponibile per MainMenu!");
            // Fallback: carica la scena "Title Screen"
            UnityEngine.SceneManagement.SceneManager.LoadScene("Title Screen");
        }
    }

    public void OnExitClicked()
    {
        if (debugMode) Debug.Log("[PauseMenuUI] Exit button clicked");
        
        if (gameManager != null)
        {
            gameManager.ExitGame();
        }
        else
        {
            Debug.LogError("[PauseMenuUI] GameManager non disponibile per Exit!");
            // Fallback: chiudi l'applicazione
            #if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
            #else
                Application.Quit();
            #endif
        }
    }

    // ========== GESTIONE EVENTI GAMEMANAGER ==========

    /// <summary>
    /// Sottoscrivi agli eventi del GameManager per sincronizzare lo stato
    /// </summary>
    private void SubscribeToGameManagerEvents()
    {
        if (gameManager != null && gameManager.OnPauseStateChanged != null)
        {
            gameManager.OnPauseStateChanged += OnPauseStateChanged;
            if (debugMode) Debug.Log("[PauseMenuUI] Sottoscritto agli eventi del GameManager");
        }
    }

    /// <summary>
    /// Chiamato quando lo stato di pausa del GameManager cambia
    /// </summary>
    private void OnPauseStateChanged(bool isPaused)
    {
        if (debugMode) Debug.Log($"[PauseMenuUI] Stato pausa cambiato: {isPaused}");
        
        if (isPaused)
        {
            Show();
        }
        else
        {
            Hide();
        }
    }

    // ========== GESTIONE VISIBILITÀ ==========

    /// <summary>
    /// Mostra il menu di pausa
    /// </summary>
    public void Show()
    {
        if (pausePanel != null)
        {
            pausePanel.SetActive(true);
        }
        
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }
        
        if (debugMode) Debug.Log("[PauseMenuUI] Menu mostrato");
    }

    /// <summary>
    /// Nascondi il menu di pausa
    /// </summary>
    public void Hide()
    {
        if (pausePanel != null)
        {
            pausePanel.SetActive(false);
        }
        
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
        
        if (debugMode) Debug.Log("[PauseMenuUI] Menu nascosto");
    }

    /// <summary>
    /// Mostra/Nascondi con animazione fade (se disponibile CanvasGroup)
    /// </summary>
    public void ShowWithFade(float duration = 0.3f)
    {
        if (canvasGroup != null)
        {
            StartCoroutine(FadeIn(duration));
        }
        else
        {
            Show();
        }
    }

    public void HideWithFade(float duration = 0.3f)
    {
        if (canvasGroup != null)
        {
            StartCoroutine(FadeOut(duration));
        }
        else
        {
            Hide();
        }
    }

    private IEnumerator FadeIn(float duration)
    {
        if (pausePanel != null) pausePanel.SetActive(true);
        
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = true;
        
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsed / duration);
            yield return null;
        }
        
        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
    }

    private IEnumerator FadeOut(float duration)
    {
        canvasGroup.interactable = false;
        
        float elapsed = 0f;
        float startAlpha = canvasGroup.alpha;
        
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, elapsed / duration);
            yield return null;
        }
        
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        if (pausePanel != null) pausePanel.SetActive(false);
    }

    // ========== DEBUG E UTILITY ==========

    /// <summary>
    /// Ottieni lo stato di inizializzazione
    /// </summary>
    public bool IsInitialized()
    {
        return isInitialized;
    }

    /// <summary>
    /// Forza la ricerca del GameManager (utile se viene creato dopo questo script)
    /// </summary>
    [ContextMenu("Force Find GameManager")]
    public void ForceFindGameManager()
    {
        FindGameManager();
        SubscribeToGameManagerEvents();
        Debug.Log("[PauseMenuUI] GameManager cercato forzatamente");
    }

    /// <summary>
    /// Forza il ricollegamento dei bottoni
    /// </summary>
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
        Debug.Log("[PauseMenuUI] Bottoni ricollegati forzatamente");
    }

    /// <summary>
    /// Stampa informazioni di debug
    /// </summary>
    [ContextMenu("Debug Info")]
    public void LogDebugInfo()
    {
        Debug.Log($"=== PauseMenuUI Debug Info ===\n" +
                  $"GameObject: {gameObject.name}\n" +
                  $"Initialized: {isInitialized}\n" +
                  $"GameManager: {(gameManager != null ? "✅" : "❌")}\n" +
                  $"Auto Find Buttons: {autoFindButtons}\n" +
                  $"Resume Button: {(resumeButton != null ? resumeButton.name : "❌")}\n" +
                  $"Restart Button: {(restartButton != null ? restartButton.name : "❌")}\n" +
                  $"MainMenu Button: {(mainMenuButton != null ? mainMenuButton.name : "❌")}\n" +
                  $"Exit Button: {(exitButton != null ? exitButton.name : "❌")}\n" +
                  $"Canvas Group: {(canvasGroup != null ? "✅" : "❌")}\n" +
                  $"Pause Panel: {(pausePanel != null ? pausePanel.name : "❌")}\n" +
                  $"Parent Canvas: {(parentCanvas != null ? parentCanvas.name : "❌")}");
    }

    // ========== CLEANUP ==========

    private void OnDestroy()
    {
        // Rimuovi sottoscrizioni eventi
        if (gameManager != null && gameManager.OnPauseStateChanged != null)
        {
            gameManager.OnPauseStateChanged -= OnPauseStateChanged;
        }
    }
}