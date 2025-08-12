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
    public Image fadeImage;  // Sarà trovato automaticamente in ogni scena
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
    public System.Action<string> OnSceneReady;
    
    // Variabili per tracking SceneManager
    private bool sceneManagerFound = false;
    private bool gameStarted = false;
    private string currentSceneManagerName = "";

    private void Awake()
    {
        DebugLog("=== AWAKE CHIAMATO ===");
        
        if (Instance != null && Instance != this)
        {
            DebugLog("GameManager duplicato trovato - distruggo il duplicato");
            Destroy(gameObject);
            return;
        }
        
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Solo GameManager persistente
            InitializeSceneManagerData();
            DebugLog($"GameManager persistente inizializzato");
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

        // ⭐ TROVA IL FADEIMAGE DELLA SCENA CORRENTE
        FindFadeImageInScene();

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
        else
        {
            Debug.LogWarning("[GameManager] FadeImage non trovato nella scena - fade disabilitato");
        }
    }

    // ⭐ NUOVO METODO: Auto-trova il FadeImage in ogni scena
    private void FindFadeImageInScene()
    {
        // Reset della referenza precedente
        fadeImage = null;
        
        DebugLog("=== RICERCA FADEIMAGE ===");
        
        // Debug: Lista tutti i GameObjects nella scena
        Canvas[] allCanvas = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        DebugLog($"Canvas trovati nella scena: {allCanvas.Length}");
        foreach (Canvas canvas in allCanvas)
        {
            DebugLog($"- Canvas: {canvas.name}, Attivo: {canvas.gameObject.activeInHierarchy}, Tag: {canvas.tag}");
            Image[] images = canvas.GetComponentsInChildren<Image>(true); // Include anche oggetti disattivati
            foreach (Image img in images)
            {
                DebugLog($"  -- Image: {img.name}, Attiva: {img.gameObject.activeInHierarchy}");
            }
        }
        
        // Metodo 1: Trova per Tag (RACCOMANDATO)
        GameObject fadeCanvasObj = GameObject.FindGameObjectWithTag("FadeCanvas");
        if (fadeCanvasObj != null)
        {
            DebugLog($"Canvas con tag FadeCanvas trovato: {fadeCanvasObj.name}");
            fadeImage = fadeCanvasObj.GetComponentInChildren<Image>(true); // Include oggetti disattivati
            if (fadeImage != null)
            {
                DebugLog($"✅ FadeImage trovato tramite Tag: {fadeImage.name}");
                return;
            }
        }
        else
        {
            DebugLog("❌ Nessun Canvas con tag 'FadeCanvas' trovato");
        }

        // Metodo 2: Trova per nome se il tag non funziona
        GameObject fadeObj = GameObject.Find("FadeImage");
        if (fadeObj != null)
        {
            fadeImage = fadeObj.GetComponent<Image>();
            if (fadeImage != null)
            {
                DebugLog($"✅ FadeImage trovato per nome: {fadeImage.name}");
                return;
            }
        }

        // Metodo 3: Fallback - trova il primo Canvas con un'Image figlia
        foreach (Canvas canvas in allCanvas)
        {
            if (canvas.name.Contains("Transition") || canvas.name.Contains("Fade"))
            {
                Image image = canvas.GetComponentInChildren<Image>(true); // Include oggetti disattivati
                if (image != null)
                {
                    fadeImage = image;
                    DebugLog($"✅ FadeImage trovato tramite fallback: {fadeImage.name} nel Canvas: {canvas.name}");
                    return;
                }
            }
        }

        // Metodo 4: ULTIMO FALLBACK - prendi la prima Image di qualsiasi Canvas
        foreach (Canvas canvas in allCanvas)
        {
            Image image = canvas.GetComponentInChildren<Image>(true);
            if (image != null)
            {
                fadeImage = image;
                DebugLog($"⚠️ FadeImage trovato come ultimo fallback: {fadeImage.name} nel Canvas: {canvas.name}");
                return;
            }
        }

        DebugLog("❌ FadeImage non trovato in questa scena");
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

        DebugLog("=== INIZIO FADE IN ===");
        Canvas fadeCanvas = fadeImage.GetComponentInParent<Canvas>();
        DebugLog($"FadeImage attiva: {fadeImage.gameObject.activeInHierarchy}");
        DebugLog($"Canvas attivo: {fadeCanvas.gameObject.activeInHierarchy}");

        // Parti da opaco
        SetFadeAlpha(1f);
        DebugLog($"Alpha impostato a 1: {fadeImage.color.a}");
        
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
        
        // ⭐ DISABILITA IL CANVAS QUANDO IL FADE È COMPLETATO
        if (fadeCanvas != null)
        {
            fadeCanvas.gameObject.SetActive(false);
            DebugLog("Canvas fade disabilitato dopo fade in");
        }
        
        DebugLog("=== FADE IN COMPLETATO ===");
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
        
        SceneManager.LoadScene(sceneName);
        // Il FadeIn sarà gestito automaticamente dal Start() della nuova scena
    }

    private IEnumerator FadeOut()
    {
        if (fadeImage == null)
        {
            DebugLog("FadeImage null durante FadeOut - skip");
            yield break;
        }

        // ⭐ RIATTIVA IL CANVAS PRIMA DEL FADE OUT
        Canvas fadeCanvas = fadeImage.GetComponentInParent<Canvas>();
        if (fadeCanvas != null && !fadeCanvas.gameObject.activeInHierarchy)
        {
            fadeCanvas.gameObject.SetActive(true);
            DebugLog("Canvas fade riattivato per fade out");
        }

        DebugLog("=== INIZIO FADE OUT ===");
        
        float t = 0;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            SetFadeAlpha(t / fadeDuration);
            yield return null;
        }
        SetFadeAlpha(1);
        
        DebugLog("=== FADE OUT COMPLETATO ===");
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
    
    [ContextMenu("Test Fade Manuale")]
    public void TestFadeManuale()
    {
        if (fadeImage != null)
        {
            Debug.Log($"[TEST] FadeImage trovata: {fadeImage.name}");
            Debug.Log($"[TEST] Canvas attivo: {fadeImage.GetComponentInParent<Canvas>().gameObject.activeInHierarchy}");
            Debug.Log($"[TEST] FadeImage attiva: {fadeImage.gameObject.activeInHierarchy}");
            
            // Forza nero opaco per 2 secondi
            StartCoroutine(TestFadeCoroutine());
        }
        else
        {
            Debug.LogError("[TEST] FadeImage è NULL!");
        }
    }

    private IEnumerator TestFadeCoroutine()
    {
        SetFadeAlpha(1f);
        Debug.Log($"[TEST] Schermo nero per 2 secondi - Alpha: {fadeImage.color.a}");
        yield return new WaitForSeconds(2f);
        SetFadeAlpha(0f);
        Debug.Log("[TEST] Fade test completato");
    }

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
                     $"FadeImage: {(fadeImage != null ? fadeImage.name : "NULL")}\n" +
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