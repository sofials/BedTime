using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("UI References")]
    public GameObject startMenu;

    [Header("Scene Management")]
    [SerializeField] private SceneController sceneController;
    
    [Header("Cursor Settings")]
    [SerializeField] private bool debugCursorState = false; // Per debugging

    // Eventi per compatibilità con SceneManager
    public System.Action<string> OnSceneReady;

    private void Awake()
    {
        // Singleton semplice
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        string currentScene = SceneManager.GetActiveScene().name;
        
        if (currentScene == "Title Screen")
        {
            HandleTitleScreen();
        }
        else
        {
            HandleGameScene();
        }
    }

    // Update per forzare lo stato del cursore se necessario (solo per debugging)
    private void Update()
    {
        if (debugCursorState && !IsInMainMenu())
        {
            // Forza lo stato del cursore se si "sblocca" accidentalmente
            if (Cursor.visible || Cursor.lockState != CursorLockMode.Locked)
            {
                Debug.LogWarning("[GameManager] Cursore si è sbloccato, lo riforzo nascosto");
                SetGameCursorState();
            }
        }
    }

    private void HandleTitleScreen()
    {
        if (startMenu != null)
            startMenu.SetActive(true);
            
        // Configurazione cursore per menu
        Time.timeScale = 1f;
        SetMenuCursorState();
        
        Debug.Log("[GameManager] Configurazione Title Screen - Cursore visibile");
    }

    private void HandleGameScene()
    {
        // Configurazione cursore per gioco
        Time.timeScale = 1f;
        SetGameCursorState();
        
        Debug.Log("[GameManager] Configurazione Game Scene - Cursore nascosto");
        
        // Notifica lo SceneManager che il GameManager è pronto
        StartCoroutine(NotifySceneManagerAfterDelay());
    }
    
    /// <summary>
    /// Imposta il cursore per il gameplay (nascosto e bloccato)
    /// </summary>
    private void SetGameCursorState()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
    
    /// <summary>
    /// Imposta il cursore per i menu (visibile e libero)
    /// </summary>
    private void SetMenuCursorState()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
    
    private IEnumerator NotifySceneManagerAfterDelay()
    {
        // Piccolo delay per assicurarsi che tutto sia inizializzato
        yield return new WaitForSeconds(0.1f);
        
        // Ri-forza lo stato del cursore dopo l'inizializzazione
        if (!IsInMainMenu())
        {
            SetGameCursorState();
        }
        
        string currentScene = SceneManager.GetActiveScene().name;
        
        // Cerca lo SceneManager specifico della scena
        string[] possibleNames = { 
            "SceneManager00", "SceneManager01", "SceneManager02",
            "SceneManager", currentScene + "_Manager" 
        };
        
        GameObject sceneManagerObj = null;
        foreach (string name in possibleNames)
        {
            sceneManagerObj = GameObject.Find(name);
            if (sceneManagerObj != null) break;
        }
        
        if (sceneManagerObj != null)
        {
            // Notifica tramite SendMessage (compatibilità con SceneManager00)
            sceneManagerObj.SendMessage("OnGameManagerReady", SendMessageOptions.DontRequireReceiver);
        }
        
        // Notifica anche tramite evento
        OnSceneReady?.Invoke(currentScene);
    }

    // ========== METODI PUBBLICI PER CARICAMENTO SCENE ==========
    
    /// <summary>
    /// Carica una scena usando il SceneController per il fade
    /// </summary>
    public void LoadSceneWithFade(string sceneName)
    {
        // Trova il SceneController nella scena corrente
        if (sceneController == null)
        {
            sceneController = Object.FindFirstObjectByType<SceneController>();
        }
        
        if (sceneController != null)
        {
            Debug.Log($"[GameManager] Caricamento scena con fade: {sceneName}");
            sceneController.LoadScene(sceneName);
        }
        else
        {
            // Fallback senza fade
            Debug.LogWarning($"[GameManager] SceneController non trovato, caricamento senza fade: {sceneName}");
            SceneManager.LoadScene(sceneName);
        }
    }
    
    /// <summary>
    /// Carica una scena direttamente senza fade (per compatibilità)
    /// </summary>
    public void LoadScene(string sceneName)
    {
        Debug.Log($"[GameManager] Caricamento diretto scena: {sceneName}");
        SceneManager.LoadScene(sceneName);
    }

    /// <summary>
    /// Riavvia la scena corrente con fade
    /// </summary>
    public void RestartCurrentScene()
    {
        string currentScene = SceneManager.GetActiveScene().name;
        LoadSceneWithFade(currentScene);
    }

    /// <summary>
    /// Torna al menu principale
    /// </summary>
    public void ReturnToMainMenu()
    {
        LoadSceneWithFade("Title Screen");
    }

    // ========== GESTIONE EVENTI SCENA ==========
    
    /// <summary>
    /// Chiamato automaticamente quando una scena viene caricata
    /// </summary>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"[GameManager] Scena caricata: {scene.name}");
        
        // Aspetta un frame per permettere l'inizializzazione
        StartCoroutine(ConfigureCursorForNewScene(scene.name));
    }
    
    private IEnumerator ConfigureCursorForNewScene(string sceneName)
    {
        yield return null; // Aspetta un frame
        
        if (sceneName == "Title Screen")
        {
            SetMenuCursorState();
            Debug.Log("[GameManager] Nuovo caricamento - Cursore configurato per menu");
        }
        else
        {
            SetGameCursorState();
            Debug.Log("[GameManager] Nuovo caricamento - Cursore configurato per gameplay");
        }
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // ========== METODI UTILITY ==========

    /// <summary>
    /// Ottieni il nome della scena corrente
    /// </summary>
    public string GetCurrentSceneName()
    {
        return SceneManager.GetActiveScene().name;
    }

    /// <summary>
    /// Controlla se siamo nel menu principale
    /// </summary>
    public bool IsInMainMenu()
    {
        return GetCurrentSceneName() == "Title Screen";
    }

    /// <summary>
    /// Pausa/Riprendi il gioco
    /// </summary>
    public void SetGamePaused(bool paused)
    {
        Time.timeScale = paused ? 0f : 1f;
        
        if (paused)
        {
            // Quando in pausa, mostra il cursore (per future pause menu)
            SetMenuCursorState();
        }
        else if (!IsInMainMenu())
        {
            // Quando riprendi, nascondi il cursore se non siamo nel menu
            SetGameCursorState();
        }
    }

    /// <summary>
    /// Forza lo stato del cursore per il gameplay
    /// </summary>
    public void ForceCursorForGameplay()
    {
        if (!IsInMainMenu())
        {
            SetGameCursorState();
            Debug.Log("[GameManager] Cursore forzato per gameplay");
        }
    }

    /// <summary>
    /// Imposta la configurazione del cursore (per compatibilità)
    /// </summary>
    public void SetCursorState(bool visible, CursorLockMode lockMode)
    {
        Cursor.visible = visible;
        Cursor.lockState = lockMode;
        Debug.Log($"[GameManager] Cursore impostato manualmente - Visible: {visible}, Lock: {lockMode}");
    }

    // ========== DEBUG ==========

    [ContextMenu("Debug State")]
    public void DebugState()
    {
        Debug.Log($"=== GameManager State ===\n" +
                  $"Current Scene: {GetCurrentSceneName()}\n" +
                  $"Is Main Menu: {IsInMainMenu()}\n" +
                  $"Time Scale: {Time.timeScale}\n" +
                  $"Cursor Visible: {Cursor.visible}\n" +
                  $"Cursor Lock: {Cursor.lockState}\n" +
                  $"SceneController: {(sceneController != null ? "✅" : "❌")}\n" +
                  $"Debug Mode: {debugCursorState}");
    }

    [ContextMenu("Test - Force Cursor for Gameplay")]
    public void DebugForceCursor()
    {
        ForceCursorForGameplay();
    }

    [ContextMenu("Test - Restart Scene")]
    public void DebugRestartScene()
    {
        RestartCurrentScene();
    }

    [ContextMenu("Test - Go to Main Menu")]
    public void DebugGoToMainMenu()
    {
        ReturnToMainMenu();
    }

    // ========== CLEANUP ==========

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}