using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("UI References - Solo per Title Screen")]
    public GameObject startMenu;  // Solo nella Title Screen
    
    [Header("Fade Settings")]
    public Image fadeImage;
    public float fadeDuration = 1f;

    [Header("Scene Configuration")]
    [Tooltip("Scene attualmente supportate dal gioco")]
    public List<string> supportedScenes = new List<string>
    {
        "Title Screen",
        "00 - Landing in the Dreamworld",
        "01 - Party in Lukelandia", 
        "02 - Finding Pietro"
    };

    [Header("SceneManager Configuration")]
    [Tooltip("Configurazione degli SceneManager per ogni scena")]
    [SerializeField] private Dictionary<string, string> expectedSceneManagers = new Dictionary<string, string>
    {
        ["00 - Landing in the Dreamworld"] = "SceneManager00",
        ["01 - Party in Lukelandia"] = "SceneManager01",
        ["02 - Finding Pietro"] = "SceneManager02"
    };
    
    [Header("SceneManager Detection")]
    [SerializeField] private float sceneManagerTimeout = 5f;
    [SerializeField] private float sceneManagerCheckInterval = 0.1f;
    [SerializeField] private bool waitForSceneManagerBeforeStart = true;

    [Header("Settings")]
    [SerializeField] private bool enableDebugLogs = true;

    // Eventi per notificare altri sistemi
    public System.Action<string> OnSceneManagerFound;
    public System.Action<string> OnSceneManagerTimeout;
    public System.Action<string> OnSceneReady; // Nuovo evento per comunicare con SceneManager
    
    // Variabili per tracking SceneManager
    private bool sceneManagerFound = false;
    private bool gameStarted = false;
    private string currentSceneManagerName = "";

    private void Awake()
    {
        DebugLog("=== AWAKE CHIAMATO ===");
        DebugLog($"GameObject: {gameObject.name}");
        
        // Gestione singleton semplificata
        if (Instance != null && Instance != this)
        {
            DebugLog("GameManager duplicato trovato - distruggo il duplicato");
            Destroy(gameObject);
            return;
        }
        
        if (Instance == null)
        {
            Instance = this;
            InitializeSceneManagerData();
            DebugLog($"Inizializzato nella scena: {SceneManager.GetActiveScene().name}");
        }
    }

    private void InitializeSceneManagerData()
    {
        // Inizializza expectedSceneManagers se vuoto
        if (expectedSceneManagers.Count == 0)
        {
            expectedSceneManagers["00 - Landing in the Dreamworld"] = "SceneManager00";
            expectedSceneManagers["01 - Party in Lukelandia"] = "SceneManager01";
            expectedSceneManagers["02 - Finding Pietro"] = "SceneManager02";
        }
    }

    private void Start()
    {
        string currentScene = SceneManager.GetActiveScene().name;
        DebugLog($"Start() chiamato nella scena: {currentScene}");

        // Gestione specifica per tipo di scena
        if (currentScene == "Title Screen")
        {
            HandleTitleScreen();
        }
        else
        {
            HandleGameScene(currentScene);
        }

        // Fade sempre attivo per tutte le scene
        if (fadeImage != null)
        {
            StartCoroutine(FadeInSafe());
        }
    }

    private void HandleTitleScreen()
    {
        DebugLog("Gestione Title Screen");
        
        // Attiva il menu start (dovrebbe essere presente solo qui)
        if (startMenu != null)
        {
            startMenu.SetActive(true);
            DebugLog("StartMenu attivato nella Title Screen");
        }
        else
        {
            Debug.LogError("[GameManager] StartMenu non assegnato nella Title Screen!");
        }
        
        // Configurazione cursore per il menu
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        
        // Nella Title Screen non serve aspettare SceneManager
        sceneManagerFound = true;
        gameStarted = true;
        
        DebugLog("Title Screen configurata completamente");
    }

    private void HandleGameScene(string currentScene)
    {
        DebugLog($"Gestione scena di gioco: {currentScene}");

        // Verifica se dobbiamo aspettare uno SceneManager specifico
        if (waitForSceneManagerBeforeStart && expectedSceneManagers.ContainsKey(currentScene))
        {
            string expectedManagerName = expectedSceneManagers[currentScene];
            DebugLog($"Scena '{currentScene}' richiede SceneManager: '{expectedManagerName}'");
            StartCoroutine(WaitForSpecificSceneManager(expectedManagerName));
        }
        else
        {
            // Avvio immediato se non serve SceneManager
            DebugLog($"Scena '{currentScene}' non richiede SceneManager specifico - avvio immediato");
            StartCoroutine(StartGameImmediately());
        }
    }

    private IEnumerator WaitForSpecificSceneManager(string expectedManagerName)
    {
        DebugLog($"Inizio ricerca per SceneManager: '{expectedManagerName}'");
        
        float elapsedTime = 0f;
        bool found = false;

        while (elapsedTime < sceneManagerTimeout && !found)
        {
            GameObject sceneManagerObj = GameObject.Find(expectedManagerName);
            
            if (sceneManagerObj != null)
            {
                DebugLog($"✅ SceneManager '{expectedManagerName}' trovato!");
                currentSceneManagerName = expectedManagerName;
                sceneManagerFound = true;
                found = true;
                
                OnSceneManagerFound?.Invoke(expectedManagerName);
                
                // Notifica lo SceneManager che il GameManager è pronto
                sceneManagerObj.SendMessage("OnGameManagerReady", SendMessageOptions.DontRequireReceiver);
                
                yield return StartCoroutine(StartGameImmediately());
            }
            else
            {
                elapsedTime += sceneManagerCheckInterval;
                yield return new WaitForSeconds(sceneManagerCheckInterval);
            }
        }

        if (!found)
        {
            Debug.LogWarning($"[GameManager] ⚠️ Timeout! SceneManager '{expectedManagerName}' non trovato dopo {sceneManagerTimeout}s");
            OnSceneManagerTimeout?.Invoke(expectedManagerName);
            
            // Avvia comunque il gioco
            yield return StartCoroutine(StartGameImmediately());
        }
    }

    private IEnumerator StartGameImmediately()
    {
        if (gameStarted)
        {
            DebugLog("Gioco già avviato, skip");
            yield break;
        }

        // Piccolo delay per il fade
        yield return new WaitForSeconds(0.2f);

        // Configura il gioco per essere attivo
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        
        gameStarted = true;
        
        string currentScene = SceneManager.GetActiveScene().name;
        DebugLog($"Scena '{currentScene}' avviata completamente");
        
        // Notifica che la scena è pronta (gli SceneManager possono ascoltare questo evento)
        OnSceneReady?.Invoke(currentScene);
    }

    private IEnumerator FadeInSafe()
    {
        if (fadeImage == null)
        {
            DebugLog("FadeImage è null, skip fade");
            yield break;
        }

        // Parti da opaco
        SetFadeAlpha(1f);
        
        // Piccolo delay per il caricamento
        yield return new WaitForSeconds(0.1f);
        
        // Fade in
        float t = fadeDuration;
        while (t > 0)
        {
            t -= Time.unscaledDeltaTime;
            SetFadeAlpha(t / fadeDuration);
            yield return null;
        }
        SetFadeAlpha(0);
        
        DebugLog("Fade in completato");
    }

    // ========== METODI SCENEMANAGER ==========
    
    public bool IsSceneManagerFound() => sceneManagerFound;
    public string GetCurrentSceneManagerName() => currentSceneManagerName;
    public string GetExpectedSceneManagerName()
    {
        string currentScene = SceneManager.GetActiveScene().name;
        return expectedSceneManagers.ContainsKey(currentScene) ? expectedSceneManagers[currentScene] : "";
    }
    
    [ContextMenu("Force SceneManager Search")]
    public void ForceSceneManagerSearch()
    {
        string currentScene = SceneManager.GetActiveScene().name;
        if (expectedSceneManagers.ContainsKey(currentScene) && !sceneManagerFound)
        {
            string expectedManagerName = expectedSceneManagers[currentScene];
            StartCoroutine(WaitForSpecificSceneManager(expectedManagerName));
        }
    }
    
    public void SetExpectedSceneManager(string sceneName, string sceneManagerName)
    {
        expectedSceneManagers[sceneName] = sceneManagerName;
        DebugLog($"Configurato SceneManager '{sceneManagerName}' per la scena '{sceneName}'");
    }

    // ========== GESTIONE MENU E GIOCO - SOLO TITLE SCREEN ==========
    
    public void StartGame()
    {
        string currentScene = SceneManager.GetActiveScene().name;
        DebugLog($"StartGame() chiamato nella scena: {currentScene}");
        
        // Dalla Title Screen, carica la prima scena di gioco
        if (currentScene == "Title Screen")
        {
            if (startMenu != null)
            {
                startMenu.SetActive(false);
                DebugLog("StartMenu disattivato prima del cambio scena");
            }
            
            LoadSceneWithFade("00 - Landing in the Dreamworld");
            return;
        }
        
        // Per altre scene, il controllo è delegato allo SceneManager specifico
        DebugLog("StartGame() chiamato in scena di gioco - delegato allo SceneManager");
    }

    public void ExitGame()
    {
        DebugLog("Uscita dal gioco");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ========== FADE E SCENE LOADING ==========
    
    public void LoadSceneWithFade(string sceneName)
    {
        DebugLog($"Caricamento scena con fade: {sceneName}");
        StartCoroutine(FadeAndLoad(sceneName));
    }

    private IEnumerator FadeAndLoad(string sceneName)
    {
        yield return StartCoroutine(FadeOut());
        
        // Reset singleton prima del cambio scena
        if (Instance == this)
        {
            Instance = null;
            DebugLog("Singleton resettato prima del cambio scena");
        }
        
        SceneManager.LoadScene(sceneName);
        yield return new WaitForSeconds(0.1f);
        StartCoroutine(FadeIn());
    }
    
    private IEnumerator FadeOut()
    {
        float t = 0;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            SetFadeAlpha(t / fadeDuration);
            yield return null;
        }
        SetFadeAlpha(1);
    }

    private IEnumerator FadeIn()
    {
        float t = fadeDuration;
        while (t > 0)
        {
            t -= Time.unscaledDeltaTime;
            SetFadeAlpha(t / fadeDuration);
            yield return null;
        }
        SetFadeAlpha(0);
    }

    private void SetFadeAlpha(float alpha)
    {
        if (fadeImage != null)
        {
            Color c = fadeImage.color;
            c.a = Mathf.Clamp01(alpha);
            fadeImage.color = c;
        }
    }

    // ========== DEBUG ==========
    
    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[GameManager] {message}");
        }
    }
    
    public void EnableDebugLogs(bool enabled)
    {
        enableDebugLogs = enabled;
    }
    
    [ContextMenu("Debug Global State")]
    public void DebugGlobalState()
    {
        string currentScene = SceneManager.GetActiveScene().name;
        string expectedManager = GetExpectedSceneManagerName();
        
        string info = $"=== GameManager State ===\n" +
                     $"Scena: {currentScene}\n" +
                     $"SceneManager atteso: {expectedManager}\n" +
                     $"SceneManager trovato: {sceneManagerFound}\n" +
                     $"Nome corrente: {currentSceneManagerName}\n" +
                     $"Gioco avviato: {gameStarted}\n" +
                     $"Aspetta SceneManager: {waitForSceneManagerBeforeStart}\n" +
                     $"Timeout: {sceneManagerTimeout}s";
        
        Debug.Log(info);
    }

    // ========== CLEANUP ==========
    
    private void OnDestroy()
    {
        DebugLog($"Cleanup nella scena: {SceneManager.GetActiveScene().name}");
        
        if (Instance == this)
        {
            Instance = null;
        }
    }
}