using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("UI Menus")]
    public GameObject startMenu;
    public PlayerAttack playerAttack;

    [Header("Respawn Settings")]
    [Tooltip("Transform del punto iniziale di spawn, se non c'è un checkpoint attivo.")]
    public Transform levelStartPoint;
    
    // Dizionario per memorizzare l'ultimo checkpoint per ogni scena
    [SerializeField] private Dictionary<string, string> sceneCheckpoints = new Dictionary<string, string>();
    
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

    [Header("Global Memories Tracking")]
    [SerializeField] private int totalGlobalMemories = 0;
    [SerializeField] private int collectedGlobalMemories = 0;
    [SerializeField] private List<string> collectedMemoryNames = new List<string>();
    [SerializeField] private List<string> collectedMemoryScenes = new List<string>();
    
    // Tracciamento per scena delle memorie
    [SerializeField] private Dictionary<string, List<string>> memoriesByScene = new Dictionary<string, List<string>>();
    [SerializeField] private Dictionary<string, int> totalMemoriesByScene = new Dictionary<string, int>();
    
    [Header("Scene 01 Presents Tracking")]
    [SerializeField] private int totalScene01Presents = 0;
    [SerializeField] private int collectedScene01Presents = 0;
    [SerializeField] private List<string> collectedScene01PresentNames = new List<string>();
    
    [Header("Settings")]
    [SerializeField] private bool enableCollectibleLogs = true;
    [SerializeField] private bool autoSaveOnCollection = true;
    [SerializeField] private bool enablePeriodicAutoSave = true;
    [SerializeField] private float autoSaveInterval = 30f;

    // Eventi per notificare altri sistemi
    public System.Action<string, string> OnMemoryCollected;
    public System.Action<string, int, int> OnSceneProgressUpdated;
    public System.Action<int, int> OnGlobalProgressUpdated;
    public System.Action<string> OnSceneManagerFound;
    public System.Action<string> OnSceneManagerTimeout;

    [SerializeField] private GameObject levelTitleUI;
    
    // Variabili per tracking SceneManager
    private bool sceneManagerFound = false;
    private bool gameStarted = false;
    private string currentSceneManagerName = "";

    private void Awake()
    {
        Debug.Log($"[GameManager] === AWAKE CHIAMATO ===");
        Debug.Log($"[GameManager] GameObject: {gameObject.name}");
        Debug.Log($"[GameManager] startMenu assegnato: {startMenu != null}");
        Debug.Log($"[GameManager] playerAttack assegnato: {playerAttack != null}");
        Debug.Log($"[GameManager] Instance corrente: {Instance != null}");
        
        GameManager[] allManagers = FindObjectsByType<GameManager>(FindObjectsSortMode.None);
        Debug.Log($"[GameManager] Totale GameManager nella scena: {allManagers.Length}");
        
        // ⭐ RIMUOVI DontDestroyOnLoad - ogni scena ha il suo GameManager ⭐
        if (Instance == null)
        {
            Instance = this;

            // Inizializza le strutture dati per le scene
            InitializeSceneData();
            InitializeSceneManagerData();

            // ⭐ CARICA SEMPRE i dati salvati all'avvio di ogni scena ⭐
            LoadGlobalData();

            Debug.Log($"[GameManager] Inizializzato nella scena: {SceneManager.GetActiveScene().name}");
        }
        else
        {
            // Se per qualche motivo esistono due GameManager nella stessa scena
            Debug.LogWarning("[GameManager] GameManager duplicato nella stessa scena - distruggo il duplicato");
            Destroy(gameObject);
        }
    }

    private void InitializeSceneData()
    {
        foreach (string sceneName in supportedScenes)
        {
            if (!memoriesByScene.ContainsKey(sceneName))
            {
                memoriesByScene[sceneName] = new List<string>();
            }
            
            if (!totalMemoriesByScene.ContainsKey(sceneName))
            {
                totalMemoriesByScene[sceneName] = 0;
            }
        }
    }

    private void InitializeSceneManagerData()
    {
        // Se expectedSceneManagers è vuoto, inizializza con i valori di default
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

        // Gestione del menu start: mostralo solo nella Title Screen
        if (currentScene == "Title Screen")
        {
            HandleTitleScreen();
        }
        else
        {
            HandleGameScene(currentScene);
        }

        // Il fade è sempre attivo per tutte le scene
        if (fadeImage != null)
        {
            StartCoroutine(FadeInSafe());
        }
        
        // ⭐ Auto-save periodico per sicurezza ⭐
        if (enablePeriodicAutoSave && autoSaveOnCollection)
        {
            InvokeRepeating(nameof(AutoSave), autoSaveInterval, autoSaveInterval);
        }
    }

    private void HandleTitleScreen()
    {
        if (startMenu != null)
        {
            startMenu.SetActive(true);
        }
        else
        {
            Debug.LogError("[GameManager] StartMenu non assegnato nella Title Screen!");
        }
        
        // Nella title screen, nascondi il levelTitleUI
        if (levelTitleUI != null)
            levelTitleUI.SetActive(false);
            
        // Disattiva la UI dei power up nel menu
        if (playerAttack != null && playerAttack.TryGetComponent<PlayerPowerUp>(out var powerUpTitle))
        {
            if (powerUpTitle.powerUI != null)
                powerUpTitle.powerUI.SetActive(false);
        }
        
        // Nel menu, il cursore deve essere visibile
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        
        // Non serve aspettare SceneManager nella Title Screen
        sceneManagerFound = true;
        gameStarted = true;
    }

    private void HandleGameScene(string currentScene)
    {
        // ⭐ Nelle scene di gioco, startMenu semplicemente non esiste o è null ⭐
        if (startMenu != null)
        {
            startMenu.SetActive(false);
            Debug.Log($"[GameManager] StartMenu nascosto per la scena '{currentScene}'");
        }
        
        // Nascondi sempre il levelTitleUI per tutte le scene di gioco
        if (levelTitleUI != null)
            levelTitleUI.SetActive(false);

        // ⭐ Verifica se dobbiamo aspettare uno SceneManager specifico ⭐
        if (waitForSceneManagerBeforeStart && expectedSceneManagers.ContainsKey(currentScene))
        {
            string expectedManagerName = expectedSceneManagers[currentScene];
            Debug.Log($"[GameManager] Scena '{currentScene}' richiede SceneManager: '{expectedManagerName}'");
            StartCoroutine(WaitForSpecificSceneManager(expectedManagerName));
        }
        else
        {
            // Se non dobbiamo aspettare nessun SceneManager, avvia immediatamente
            Debug.Log($"[GameManager] Scena '{currentScene}' non richiede SceneManager specifico - avvio immediato");
            StartCoroutine(StartGameImmediately());
        }
    }

    // ⭐ NUOVO: Aspetta uno SceneManager specifico ⭐
    private IEnumerator WaitForSpecificSceneManager(string expectedManagerName)
    {
        Debug.Log($"[GameManager] Inizio ricerca per SceneManager: '{expectedManagerName}'");
        
        float elapsedTime = 0f;
        bool found = false;

        while (elapsedTime < sceneManagerTimeout && !found)
        {
            // Cerca lo SceneManager specifico
            GameObject sceneManagerObj = GameObject.Find(expectedManagerName);
            
            if (sceneManagerObj != null)
            {
                Debug.Log($"[GameManager] ✅ SceneManager '{expectedManagerName}' trovato!");
                currentSceneManagerName = expectedManagerName;
                sceneManagerFound = true;
                found = true;
                
                // Notifica che abbiamo trovato lo SceneManager
                OnSceneManagerFound?.Invoke(expectedManagerName);
                
                // Notifica lo SceneManager che siamo pronti
                sceneManagerObj.SendMessage("OnGameManagerReady", SendMessageOptions.DontRequireReceiver);
                
                // Avvia il gioco
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
            
            // Avvia comunque il gioco anche senza SceneManager
            yield return StartCoroutine(StartGameImmediately());
        }
    }

    // ⭐ Avvia immediatamente il gioco per TUTTE le scene diverse da Title Screen ⭐
    private IEnumerator StartGameImmediately()
    {
        if (gameStarted)
        {
            Debug.Log("[GameManager] Gioco già avviato, skip");
            yield break;
        }

        // Piccolo delay per permettere al fade di iniziare
        yield return new WaitForSeconds(0.2f);

        // Attiva immediatamente la UI di gioco
        if (playerAttack != null && playerAttack.TryGetComponent<PlayerPowerUp>(out var powerUp))
        {
            if (powerUp.powerUI != null)
            {
                powerUp.powerUI.SetActive(true);
                Debug.Log("[GameManager] UI PowerUp attivata immediatamente");
            }
        }

        // Imposta il gioco come attivo
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        
        gameStarted = true;
        
        string currentScene = SceneManager.GetActiveScene().name;
        Debug.Log($"[GameManager] Scena '{currentScene}' avviata immediatamente");
    }

    // ⭐ FIX: Fade più sicuro con controlli ⭐
    private IEnumerator FadeInSafe()
    {
        if (fadeImage == null)
        {
            Debug.LogWarning("[GameManager] FadeImage è null, skip fade");
            yield break;
        }

        // Assicurati che il fade parta da opaco
        SetFadeAlpha(1f);
        
        // Piccolo delay per permettere alla scena di caricarsi completamente
        yield return new WaitForSeconds(0.1f);
        
        // Fade in normale
        float t = fadeDuration;
        while (t > 0)
        {
            t -= Time.unscaledDeltaTime;
            SetFadeAlpha(t / fadeDuration);
            yield return null;
        }
        SetFadeAlpha(0);
        
        Debug.Log("[GameManager] Fade in completato");
    }

    // ⭐ NUOVO: Metodi per gestire gli SceneManager ⭐
    
    /// <summary>
    /// Verifica se lo SceneManager atteso è stato trovato
    /// </summary>
    public bool IsSceneManagerFound()
    {
        return sceneManagerFound;
    }
    
    /// <summary>
    /// Ottieni il nome dello SceneManager corrente
    /// </summary>
    public string GetCurrentSceneManagerName()
    {
        return currentSceneManagerName;
    }
    
    /// <summary>
    /// Ottieni il nome dello SceneManager atteso per la scena corrente
    /// </summary>
    public string GetExpectedSceneManagerName()
    {
        string currentScene = SceneManager.GetActiveScene().name;
        if (expectedSceneManagers.ContainsKey(currentScene))
        {
            return expectedSceneManagers[currentScene];
        }
        return "";
    }
    
    /// <summary>
    /// Forza la ricerca dello SceneManager se non è stato ancora trovato
    /// </summary>
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
    
    /// <summary>
    /// Configura un nuovo SceneManager atteso per una scena
    /// </summary>
    public void SetExpectedSceneManager(string sceneName, string sceneManagerName)
    {
        expectedSceneManagers[sceneName] = sceneManagerName;
        Debug.Log($"[GameManager] Configurato SceneManager '{sceneManagerName}' per la scena '{sceneName}'");
    }

    // ========== GESTIONE MEMORIES PER SCENA ==========
    
    /// <summary>
    /// Chiamato dagli SceneManager per inizializzare il conteggio delle memorie di una scena
    /// </summary>
    public void InitializeSceneMemories(string sceneName, int totalMemories, List<string> memoryNames = null)
    {
        if (!supportedScenes.Contains(sceneName))
        {
            CollectibleLog($"⚠️ Scena '{sceneName}' non supportata!");
            return;
        }

        totalMemoriesByScene[sceneName] = totalMemories;
        
        // Se forniti, salva i nomi delle memorie per riferimento
        if (memoryNames != null && memoryNames.Count > 0)
        {
            // Non sovrascrivere le memorie già raccolte
            foreach (string memoryName in memoryNames)
            {
                if (!HasMemoryInScene(sceneName, memoryName))
                {
                    // Questa memoria non è ancora stata raccolta
                }
            }
        }

        CollectibleLog($"📋 Scena '{sceneName}' inizializzata: {GetCollectedMemoriesInScene(sceneName)}/{totalMemories} memorie");
        
        // Ricalcola il totale globale
        RecalculateTotalGlobalMemories();
        
        if (autoSaveOnCollection)
            SaveGlobalData();
    }

    /// <summary>
    /// Chiamato dagli SceneManager quando viene raccolta una memoria
    /// </summary>
    public void OnSceneMemoryCollected(string sceneName, string memoryName)
    {
        if (!supportedScenes.Contains(sceneName))
        {
            CollectibleLog($"⚠️ Scena '{sceneName}' non supportata!");
            return;
        }

        // Verifica se la memoria è già stata raccolta
        if (HasMemoryInScene(sceneName, memoryName))
        {
            CollectibleLog($"Memory '{memoryName}' già raccolta nella scena '{sceneName}'");
            return;
        }

        // Aggiungi alla collezione per scena
        memoriesByScene[sceneName].Add(memoryName);
        
        // Aggiungi alla collezione globale (mantenendo compatibilità con il codice esistente)
        collectedMemoryNames.Add(memoryName);
        collectedMemoryScenes.Add(sceneName);
        collectedGlobalMemories++;

        CollectibleLog($"🧠 Memory '{memoryName}' raccolta nella scena '{sceneName}' - Totale scena: {GetCollectedMemoriesInScene(sceneName)}/{GetTotalMemoriesInScene(sceneName)}");

        // Notifica eventi
        OnMemoryCollected?.Invoke(sceneName, memoryName);
        OnSceneProgressUpdated?.Invoke(sceneName, GetCollectedMemoriesInScene(sceneName), GetTotalMemoriesInScene(sceneName));
        OnGlobalProgressUpdated?.Invoke(collectedGlobalMemories, totalGlobalMemories);

        if (autoSaveOnCollection)
            SaveGlobalData();
    }

    /// <summary>
    /// Verifica se una memoria specifica è stata raccolta in una scena
    /// </summary>
    public bool HasMemoryInScene(string sceneName, string memoryName)
    {
        if (!memoriesByScene.ContainsKey(sceneName))
            return false;
            
        return memoriesByScene[sceneName].Contains(memoryName);
    }

    /// <summary>
    /// Ottieni il numero di memorie raccolte in una scena specifica
    /// </summary>
    public int GetCollectedMemoriesInScene(string sceneName)
    {
        if (!memoriesByScene.ContainsKey(sceneName))
            return 0;
            
        return memoriesByScene[sceneName].Count;
    }

    /// <summary>
    /// Ottieni il numero totale di memorie in una scena specifica
    /// </summary>
    public int GetTotalMemoriesInScene(string sceneName)
    {
        if (!totalMemoriesByScene.ContainsKey(sceneName))
            return 0;
            
        return totalMemoriesByScene[sceneName];
    }

    /// <summary>
    /// Ottieni la lista delle memorie raccolte in una scena
    /// </summary>
    public List<string> GetCollectedMemoriesNamesInScene(string sceneName)
    {
        if (!memoriesByScene.ContainsKey(sceneName))
            return new List<string>();
            
        return new List<string>(memoriesByScene[sceneName]);
    }

    /// <summary>
    /// Verifica se tutte le memorie di una scena sono state raccolte
    /// </summary>
    public bool AreAllMemoriesCollectedInScene(string sceneName)
    {
        int collected = GetCollectedMemoriesInScene(sceneName);
        int total = GetTotalMemoriesInScene(sceneName);
        return collected >= total && total > 0;
    }

    /// <summary>
    /// Ricalcola il totale globale delle memorie basandosi sui totali per scena
    /// </summary>
    private void RecalculateTotalGlobalMemories()
    {
        totalGlobalMemories = totalMemoriesByScene.Values.Sum();
        CollectibleLog($"📊 Totale memorie globali ricalcolato: {totalGlobalMemories}");
    }

    // ========== GESTIONE PRESENTS SCENA 01 ==========
    
    /// <summary>
    /// Chiamato solo dallo SceneManager della scena 01 per inizializzare i presents
    /// </summary>
    public void InitializeScene01Presents(int totalPresents, List<string> presentNames = null)
    {
        totalScene01Presents = totalPresents;
        
        CollectibleLog($"🎁 Scena 01 inizializzata: {collectedScene01Presents}/{totalPresents} presents");
        
        if (autoSaveOnCollection)
            SaveGlobalData();
    }

    /// <summary>
    /// Chiamato dallo SceneManager della scena 01 quando viene raccolto un present
    /// </summary>
    public void OnScene01PresentCollected(string presentName)
    {
        // Verifica se il present è già stato raccolto
        if (collectedScene01PresentNames.Contains(presentName))
        {
            CollectibleLog($"Present '{presentName}' già raccolto nella scena 01");
            return;
        }

        collectedScene01PresentNames.Add(presentName);
        collectedScene01Presents = collectedScene01PresentNames.Count;

        CollectibleLog($"🎁 Present '{presentName}' raccolto nella scena 01 - Totale: {collectedScene01Presents}/{totalScene01Presents}");

        if (collectedScene01Presents >= totalScene01Presents && totalScene01Presents > 0)
        {
            CollectibleLog("🎉 Tutti i present della Scena 01 completati!");
        }

        if (autoSaveOnCollection)
            SaveGlobalData();
    }

    // ========== GESTIONE CHECKPOINT (mantenuto dal codice originale) ==========
    
    public void NotifySceneCheckpoint(string sceneName, string checkpointName)
    {
        if (sceneCheckpoints.ContainsKey(sceneName))
        {
            sceneCheckpoints[sceneName] = checkpointName;
        }
        else
        {
            sceneCheckpoints.Add(sceneName, checkpointName);
        }
        
        CollectibleLog($"Checkpoint '{checkpointName}' impostato per la scena '{sceneName}'");
        
        if (autoSaveOnCollection)
            SaveGlobalData();
    }
    
    public string GetSceneCheckpoint(string sceneName)
    {
        if (sceneCheckpoints.ContainsKey(sceneName))
        {
            return sceneCheckpoints[sceneName];
        }
        return null;
    }
    
    public bool HasSceneCheckpoint(string sceneName)
    {
        return sceneCheckpoints.ContainsKey(sceneName) && !string.IsNullOrEmpty(sceneCheckpoints[sceneName]);
    }
    
    public void ClearSceneCheckpoint(string sceneName)
    {
        if (sceneCheckpoints.ContainsKey(sceneName))
        {
            sceneCheckpoints.Remove(sceneName);
            CollectibleLog($"Checkpoint rimosso per la scena '{sceneName}'");
            if (autoSaveOnCollection)
                SaveGlobalData();
        }
    }
    
    public Dictionary<string, string> GetAllCheckpoints()
    {
        return new Dictionary<string, string>(sceneCheckpoints);
    }

    // ========== METODI COMPATIBILITÀ (mantenuti per non rompere codice esistente) ==========
    
    public void OnGlobalMemoryCollected(string memoryName, string sceneName)
    {
        OnSceneMemoryCollected(sceneName, memoryName);
    }
    
    public void NotifySceneMemoryCollected(string memoryName, string sceneName)
    {
        OnSceneMemoryCollected(sceneName, memoryName);
    }
    
    public void NotifyScene01PresentCollected(int current, int total)
    {
        // Metodo di compatibilità - ora usiamo il nome del present
        if (current > collectedScene01Presents)
        {
            string presentName = $"Present_{current}"; // Nome generico se non fornito
            OnScene01PresentCollected(presentName);
        }
    }

    // ========== GETTERS PUBBLICI ==========
    
    // Global Memories
    public int GetCollectedGlobalMemories() => collectedGlobalMemories;
    public int GetTotalGlobalMemories() => totalGlobalMemories;
    public float GetGlobalMemoriesProgress() => totalGlobalMemories > 0 ? (float)collectedGlobalMemories / totalGlobalMemories : 0f;
    public bool AreAllGlobalMemoriesCollected() => collectedGlobalMemories >= totalGlobalMemories && totalGlobalMemories > 0;
    
    // Scene 01 Presents
    public int GetCollectedScene01Presents() => collectedScene01Presents;
    public int GetTotalScene01Presents() => totalScene01Presents;
    public float GetScene01PresentsProgress() => totalScene01Presents > 0 ? (float)collectedScene01Presents / totalScene01Presents : 0f;
    public bool AreAllScene01PresentsCollected() => collectedScene01Presents >= totalScene01Presents && totalScene01Presents > 0;
    public List<string> GetCollectedScene01PresentNames() => new List<string>(collectedScene01PresentNames);
    
    // Memory Archive (compatibilità)
    public List<string> GetCollectedMemoryNames() => new List<string>(collectedMemoryNames);
    public List<string> GetCollectedMemoryScenes() => new List<string>(collectedMemoryScenes);
    public bool HasMemoryInArchive(string memoryName) => collectedMemoryNames.Contains(memoryName);
    public int GetMemoryArchiveCount() => collectedMemoryNames.Count;
    
    // Scene-specific
    public Dictionary<string, int> GetMemoriesProgressByScene()
    {
        Dictionary<string, int> progress = new Dictionary<string, int>();
        foreach (string sceneName in supportedScenes)
        {
            progress[sceneName] = GetCollectedMemoriesInScene(sceneName);
        }
        return progress;
    }
    
    // Combined
    public int GetTotalCollectedItems() => collectedGlobalMemories + collectedScene01Presents;
    public int GetTotalAvailableItems() => totalGlobalMemories + totalScene01Presents;

    // ========== MENU E GIOCO ==========
    
    // ⭐ SEMPLIFICATO: StartGame ora gestisce solo Title Screen e pause ⭐
    public void StartGame()
    {
        string currentScene = SceneManager.GetActiveScene().name;
        
        // Se siamo nella Title Screen, carica la scena 00
        if (currentScene == "Title Screen")
        {
            LoadSceneWithFade("00 - Landing in the Dreamworld");
            return;
        }
        
        // Per tutte le altre scene, riprendi semplicemente il gioco (caso di pausa)
        if (startMenu != null)
            startMenu.SetActive(false);
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        Debug.Log("Gioco ripreso");

        if (playerAttack != null && playerAttack.TryGetComponent<PlayerPowerUp>(out var powerUp))
            if (powerUp.powerUI != null)
                powerUp.powerUI.SetActive(true);

        StartCoroutine(DelayedIgnoreClick());
    }

    private IEnumerator DelayedIgnoreClick()
    {
        yield return new WaitForSecondsRealtime(0.1f);
        
        if (playerAttack != null)
        {
            playerAttack.IgnoreNextClick();
            Debug.Log("IgnoreNextClick chiamato dopo delay");
        }
    }

    public void ExitGame()
    {
        Debug.Log("Uscita dal gioco");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ========== FADE E SCENE LOADING ==========
    
    public void LoadSceneWithFade(string sceneName)
    {
        StartCoroutine(FadeAndLoad(sceneName));
    }

    private IEnumerator FadeAndLoad(string sceneName)
    {
        yield return StartCoroutine(FadeOut());
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

    // ========== SALVATAGGIO E CARICAMENTO AGGIORNATO ==========
    
    private void SaveGlobalData()
    {
        // Global Memories
        PlayerPrefs.SetInt("GlobalMemoriesCollected", collectedGlobalMemories);
        PlayerPrefs.SetInt("GlobalMemoriesTotal", totalGlobalMemories);
        
        // Scene 01 Presents
        PlayerPrefs.SetInt("Scene01PresentsCollected", collectedScene01Presents);
        PlayerPrefs.SetInt("Scene01PresentsTotal", totalScene01Presents);
        
        // Scene 01 Present Names
        PlayerPrefs.SetInt("Scene01PresentNamesCount", collectedScene01PresentNames.Count);
        for (int i = 0; i < collectedScene01PresentNames.Count; i++)
        {
            PlayerPrefs.SetString($"Scene01PresentName_{i}", collectedScene01PresentNames[i]);
        }
        
        // Memory Archive (compatibilità)
        PlayerPrefs.SetInt("MemoryArchiveCount", collectedMemoryNames.Count);
        for (int i = 0; i < collectedMemoryNames.Count; i++)
        {
            PlayerPrefs.SetString($"ArchiveMemoryName_{i}", collectedMemoryNames[i]);
            PlayerPrefs.SetString($"ArchiveMemoryScene_{i}", collectedMemoryScenes[i]);
        }
        
        // Memories by Scene
        foreach (string sceneName in supportedScenes)
        {
            string sceneKey = sceneName.Replace(" ", "_").Replace("-", "_");
            
            // Salva il totale per la scena
            PlayerPrefs.SetInt($"TotalMemories_{sceneKey}", GetTotalMemoriesInScene(sceneName));
            
            // Salva le memorie raccolte
            List<string> sceneMemories = GetCollectedMemoriesNamesInScene(sceneName);
            PlayerPrefs.SetInt($"CollectedMemories_{sceneKey}_Count", sceneMemories.Count);
            
            for (int i = 0; i < sceneMemories.Count; i++)
            {
                PlayerPrefs.SetString($"CollectedMemory_{sceneKey}_{i}", sceneMemories[i]);
            }
        }
        
        // Checkpoint per scena
        PlayerPrefs.SetInt("CheckpointScenesCount", sceneCheckpoints.Count);
        int checkpointIndex = 0;
        foreach (var kvp in sceneCheckpoints)
        {
            PlayerPrefs.SetString($"CheckpointScene_{checkpointIndex}", kvp.Key);
            PlayerPrefs.SetString($"CheckpointName_{checkpointIndex}", kvp.Value);
            checkpointIndex++;
        }
        
        PlayerPrefs.Save();
        CollectibleLog("Dati globali salvati");
    }
    
    private void LoadGlobalData()
    {
        // Global Memories
        collectedGlobalMemories = PlayerPrefs.GetInt("GlobalMemoriesCollected", 0);
        totalGlobalMemories = PlayerPrefs.GetInt("GlobalMemoriesTotal", 0);
        
        // Scene 01 Presents
        collectedScene01Presents = PlayerPrefs.GetInt("Scene01PresentsCollected", 0);
        totalScene01Presents = PlayerPrefs.GetInt("Scene01PresentsTotal", 0);
        
        // Scene 01 Present Names
        int presentNamesCount = PlayerPrefs.GetInt("Scene01PresentNamesCount", 0);
        collectedScene01PresentNames.Clear();
        for (int i = 0; i < presentNamesCount; i++)
        {
            string presentName = PlayerPrefs.GetString($"Scene01PresentName_{i}", "");
            if (!string.IsNullOrEmpty(presentName))
            {
                collectedScene01PresentNames.Add(presentName);
            }
        }
        
        // Memory Archive (compatibilità)
        int archiveCount = PlayerPrefs.GetInt("MemoryArchiveCount", 0);
        collectedMemoryNames.Clear();
        collectedMemoryScenes.Clear();
        
        for (int i = 0; i < archiveCount; i++)
        {
            string memoryName = PlayerPrefs.GetString($"ArchiveMemoryName_{i}", "");
            string memoryScene = PlayerPrefs.GetString($"ArchiveMemoryScene_{i}", "");
            
            if (!string.IsNullOrEmpty(memoryName))
            {
                collectedMemoryNames.Add(memoryName);
                collectedMemoryScenes.Add(memoryScene);
            }
        }
        
        // Memories by Scene
        foreach (string sceneName in supportedScenes)
        {
            string sceneKey = sceneName.Replace(" ", "_").Replace("-", "_");
            
            // Carica il totale per la scena
            int totalForScene = PlayerPrefs.GetInt($"TotalMemories_{sceneKey}", 0);
            totalMemoriesByScene[sceneName] = totalForScene;
            
            // Carica le memorie raccolte
            int collectedCount = PlayerPrefs.GetInt($"CollectedMemories_{sceneKey}_Count", 0);
            memoriesByScene[sceneName].Clear();
            
            for (int i = 0; i < collectedCount; i++)
            {
                string memoryName = PlayerPrefs.GetString($"CollectedMemory_{sceneKey}_{i}", "");
                if (!string.IsNullOrEmpty(memoryName))
                {
                    memoriesByScene[sceneName].Add(memoryName);
                }
            }
        }
        
        // Checkpoint per scena
        int checkpointScenesCount = PlayerPrefs.GetInt("CheckpointScenesCount", 0);
        sceneCheckpoints.Clear();
        
        for (int i = 0; i < checkpointScenesCount; i++)
        {
            string sceneName = PlayerPrefs.GetString($"CheckpointScene_{i}", "");
            string checkpointName = PlayerPrefs.GetString($"CheckpointName_{i}", "");
            
            if (!string.IsNullOrEmpty(sceneName) && !string.IsNullOrEmpty(checkpointName))
            {
                sceneCheckpoints.Add(sceneName, checkpointName);
            }
        }
        
        CollectibleLog($"Dati globali caricati - Memories: {collectedGlobalMemories}/{totalGlobalMemories}, Presents S01: {collectedScene01Presents}/{totalScene01Presents}, Archivio: {collectedMemoryNames.Count}, Checkpoints: {sceneCheckpoints.Count}");
    }

    // ⭐ Auto-save periodico per sicurezza ⭐
    private void AutoSave()
    {
        SaveGlobalData();
        Debug.Log("[GameManager] Auto-save periodico completato");
    }
    
    // ========== NUOVO: Salva automaticamente quando cambi scena ⭐
    private void OnDestroy()
    {
        // Salva i dati prima che questo GameManager venga distrutto
        SaveGlobalData();
        Debug.Log($"[GameManager] Dati salvati prima della distruzione nella scena: {SceneManager.GetActiveScene().name}");
        
        // Reset del singleton
        if (Instance == this)
        {
            Instance = null;
        }
    }
    
    // ========== RESET E DEBUG ==========
    
    public void ResetGlobalData()
    {
        collectedGlobalMemories = 0;
        totalGlobalMemories = 0;
        collectedScene01Presents = 0;
        totalScene01Presents = 0;
        collectedMemoryNames.Clear();
        collectedMemoryScenes.Clear();
        collectedScene01PresentNames.Clear();
        sceneCheckpoints.Clear();
        
        // Reset strutture per scena
        foreach (string sceneName in supportedScenes)
        {
            memoriesByScene[sceneName].Clear();
            totalMemoriesByScene[sceneName] = 0;
        }
        
        // Reset SceneManager tracking
        sceneManagerFound = false;
        gameStarted = false;
        currentSceneManagerName = "";
        
        // Cancella tutti i dati salvati
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
        
        CollectibleLog("Tutti i dati globali resettati");
    }
    
    private void CollectibleLog(string message)
    {
        if (enableCollectibleLogs)
        {
            Debug.Log($"[GameManager] {message}");
        }
    }
    
    public void EnableCollectibleLogs(bool enabled)
    {
        enableCollectibleLogs = enabled;
    }
    
    [ContextMenu("Debug Global State")]
    public void DebugGlobalState()
    {
        string checkpointsInfo = "";
        foreach (var kvp in sceneCheckpoints)
        {
            checkpointsInfo += $"  {kvp.Key}: {kvp.Value}\n";
        }
        
        string sceneMemoriesInfo = "";
        foreach (string sceneName in supportedScenes)
        {
            int collected = GetCollectedMemoriesInScene(sceneName);
            int total = GetTotalMemoriesInScene(sceneName);
            sceneMemoriesInfo += $"  {sceneName}: {collected}/{total}\n";
        }
        
        string sceneManagerInfo = "";
        string currentScene = SceneManager.GetActiveScene().name;
        string expectedManager = GetExpectedSceneManagerName();
        sceneManagerInfo = $"Scena: {currentScene}\n" +
                          $"SceneManager atteso: {expectedManager}\n" +
                          $"SceneManager trovato: {sceneManagerFound}\n" +
                          $"Nome corrente: {currentSceneManagerName}\n" +
                          $"Gioco avviato: {gameStarted}\n";
        
        Debug.Log($"=== GameManager Global State ===\n" +
                  $"{sceneManagerInfo}" +
                  $"Global Memories: {collectedGlobalMemories}/{totalGlobalMemories} ({GetGlobalMemoriesProgress():P1})\n" +
                  $"Scene 01 Presents: {collectedScene01Presents}/{totalScene01Presents} ({GetScene01PresentsProgress():P1})\n" +
                  $"Memory Archive: {GetMemoryArchiveCount()} entries\n" +
                  $"Archive: [{string.Join(", ", collectedMemoryNames)}]\n" +
                  $"Total Items: {GetTotalCollectedItems()}/{GetTotalAvailableItems()}\n" +
                  $"Memories by Scene:\n{sceneMemoriesInfo}" +
                  $"Scene Checkpoints:\n{checkpointsInfo}" +
                  $"Auto-Save: {autoSaveOnCollection} | Periodic: {enablePeriodicAutoSave} ({autoSaveInterval}s)\n" +
                  $"Wait for SceneManager: {waitForSceneManagerBeforeStart} | Timeout: {sceneManagerTimeout}s");
    }
    
    [ContextMenu("Debug SceneManager Configuration")]
    public void DebugSceneManagerConfiguration()
    {
        string configInfo = "=== SceneManager Configuration ===\n";
        
        foreach (var kvp in expectedSceneManagers)
        {
            configInfo += $"Scena: '{kvp.Key}' -> SceneManager: '{kvp.Value}'\n";
        }
        
        string currentScene = SceneManager.GetActiveScene().name;
        string expectedManager = GetExpectedSceneManagerName();
        
        configInfo += $"\n--- Stato Corrente ---\n";
        configInfo += $"Scena attiva: {currentScene}\n";
        configInfo += $"SceneManager atteso: {(string.IsNullOrEmpty(expectedManager) ? "NESSUNO" : expectedManager)}\n";
        configInfo += $"SceneManager trovato: {sceneManagerFound}\n";
        configInfo += $"Nome SceneManager corrente: {(string.IsNullOrEmpty(currentSceneManagerName) ? "NESSUNO" : currentSceneManagerName)}\n";
        configInfo += $"Aspetta SceneManager: {waitForSceneManagerBeforeStart}\n";
        configInfo += $"Timeout: {sceneManagerTimeout}s\n";
        configInfo += $"Intervallo controllo: {sceneManagerCheckInterval}s\n";
        
        Debug.Log(configInfo);
    }
    
    [ContextMenu("Reset All Global Data")]
    public void DebugResetGlobalData()
    {
        ResetGlobalData();
    }
    
    [ContextMenu("Force Save Data")]
    public void DebugForceSave()
    {
        SaveGlobalData();
        Debug.Log("[GameManager] Salvataggio forzato completato!");
    }
    
    [ContextMenu("Force Load Data")]
    public void DebugForceLoad()
    {
        LoadGlobalData();
        Debug.Log("[GameManager] Caricamento forzato completato!");
    }
    
    // ========== METODI UTILITY PER DEBUG ==========
    
    public void PrintDetailedState()
    {
        Debug.Log("=== DETTAGLIO COMPLETO GAMEMANAGER ===");
        Debug.Log($"Scena: {SceneManager.GetActiveScene().name}");
        Debug.Log($"Singleton Instance: {(Instance != null ? "OK" : "NULL")}");
        Debug.Log($"StartMenu: {(startMenu != null ? "Assegnato" : "NULL")}");
        Debug.Log($"PlayerAttack: {(playerAttack != null ? "Assegnato" : "NULL")}");
        Debug.Log($"FadeImage: {(fadeImage != null ? "Assegnato" : "NULL")}");
        Debug.Log($"LevelTitleUI: {(levelTitleUI != null ? "Assegnato" : "NULL")}");
        
        Debug.Log("\n--- SCENEMANAGER ---");
        Debug.Log($"SceneManager atteso: {GetExpectedSceneManagerName()}");
        Debug.Log($"SceneManager trovato: {sceneManagerFound}");
        Debug.Log($"Nome corrente: {currentSceneManagerName}");
        Debug.Log($"Gioco avviato: {gameStarted}");
        Debug.Log($"Aspetta prima di avviare: {waitForSceneManagerBeforeStart}");
        
        Debug.Log("\n--- MEMORIES ---");
        foreach (string sceneName in supportedScenes)
        {
            int collected = GetCollectedMemoriesInScene(sceneName);
            int total = GetTotalMemoriesInScene(sceneName);
            List<string> names = GetCollectedMemoriesNamesInScene(sceneName);
            Debug.Log($"{sceneName}: {collected}/{total} [{string.Join(", ", names)}]");
        }
        
        Debug.Log($"\n--- PRESENTS ---");
        Debug.Log($"Scene 01: {collectedScene01Presents}/{totalScene01Presents} [{string.Join(", ", collectedScene01PresentNames)}]");
        
        Debug.Log($"\n--- CHECKPOINTS ---");
        foreach (var kvp in sceneCheckpoints)
        {
            Debug.Log($"{kvp.Key}: {kvp.Value}");
        }
        
        Debug.Log($"\n--- SETTINGS ---");
        Debug.Log($"CollectibleLogs: {enableCollectibleLogs}");
        Debug.Log($"AutoSave: {autoSaveOnCollection}");
        Debug.Log($"PeriodicAutoSave: {enablePeriodicAutoSave} ({autoSaveInterval}s)");
        Debug.Log($"SceneManagerTimeout: {sceneManagerTimeout}s");
        Debug.Log($"CheckInterval: {sceneManagerCheckInterval}s");
    }
    
    // ========== METODI PER TESTING ==========
    
    [ContextMenu("Simulate Memory Collection")]
    public void DebugSimulateMemoryCollection()
    {
        string currentScene = SceneManager.GetActiveScene().name;
        string testMemoryName = $"TestMemory_{System.DateTime.Now.Ticks}";
        
        OnSceneMemoryCollected(currentScene, testMemoryName);
        Debug.Log($"[DEBUG] Simulata raccolta memory '{testMemoryName}' nella scena '{currentScene}'");
    }
    
    [ContextMenu("Simulate Present Collection")]
    public void DebugSimulatePresentCollection()
    {
        string testPresentName = $"TestPresent_{System.DateTime.Now.Ticks}";
        
        OnScene01PresentCollected(testPresentName);
        Debug.Log($"[DEBUG] Simulata raccolta present '{testPresentName}' nella scena 01");
    }
    
    [ContextMenu("Test SceneManager Search")]
    public void DebugTestSceneManagerSearch()
    {
        string currentScene = SceneManager.GetActiveScene().name;
        Debug.Log($"[DEBUG] Test ricerca SceneManager per la scena '{currentScene}'");
        
        if (expectedSceneManagers.ContainsKey(currentScene))
        {
            string expectedManagerName = expectedSceneManagers[currentScene];
            Debug.Log($"[DEBUG] Cercando SceneManager: '{expectedManagerName}'");
            
            GameObject sceneManagerObj = GameObject.Find(expectedManagerName);
            if (sceneManagerObj != null)
            {
                Debug.Log($"[DEBUG] ✅ SceneManager '{expectedManagerName}' TROVATO!");
            }
            else
            {
                Debug.Log($"[DEBUG] ❌ SceneManager '{expectedManagerName}' NON trovato!");
            }
        }
        else
        {
            Debug.Log($"[DEBUG] ℹ️ Nessun SceneManager configurato per la scena '{currentScene}'");
        }
    }
}