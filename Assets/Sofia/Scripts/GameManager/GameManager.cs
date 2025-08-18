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

    private void HandleTitleScreen()
    {
        if (startMenu != null)
            startMenu.SetActive(true);
            
        // Configurazione cursore per menu
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void HandleGameScene()
    {
        // Configurazione cursore per gioco
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        
        // Notifica lo SceneManager che il GameManager è pronto
        StartCoroutine(NotifySceneManagerAfterDelay());
    }
    
    private IEnumerator NotifySceneManagerAfterDelay()
    {
        // Piccolo delay per assicurarsi che tutto sia inizializzato
        yield return new WaitForSeconds(0.1f);
        
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
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else if (!IsInMainMenu())
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    /// <summary>
    /// Imposta la configurazione del cursore
    /// </summary>
    public void SetCursorState(bool visible, CursorLockMode lockMode)
    {
        Cursor.visible = visible;
        Cursor.lockState = lockMode;
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
                  $"SceneController: {(sceneController != null ? "✅" : "❌")}");
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