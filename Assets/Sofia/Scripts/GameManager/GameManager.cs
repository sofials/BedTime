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
    public GameObject pauseMenu;
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
        "00 - Landing in the Dreamworld",
        "01 - Party in Lukelandia", 
        "02 - Finding Pietro"
    };

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

    // Eventi per notificare altri sistemi
    public System.Action<string, string> OnMemoryCollected;
    public System.Action<string, int, int> OnSceneProgressUpdated;
    public System.Action<int, int> OnGlobalProgressUpdated;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            // Inizializza le strutture dati per le scene
            InitializeSceneData();
            
            // Carica i dati salvati
            LoadGlobalData();
        }
        else
        {
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

    private void Start()
    {
        string currentScene = SceneManager.GetActiveScene().name;

        if (currentScene == "00 - Landing in the Dreamworld")
        {
            Time.timeScale = 0f;
            startMenu.SetActive(true);
            pauseMenu.SetActive(false);

            if (playerAttack != null && playerAttack.TryGetComponent<PlayerPowerUp>(out var powerUp))
                if (powerUp.powerUI != null)
                    powerUp.powerUI.SetActive(false);
        }
        else
        {
            Time.timeScale = 1f;
            startMenu.SetActive(false);
            pauseMenu.SetActive(false);

            if (playerAttack != null && playerAttack.TryGetComponent<PlayerPowerUp>(out var powerUp))
                if (powerUp.powerUI != null)
                    powerUp.powerUI.SetActive(true);
        }

        if (fadeImage != null)
            StartCoroutine(FadeIn());

        // Notifica agli SceneManager che il GameManager è pronto
        NotifySceneManagersReady();
    }

    private void NotifySceneManagersReady()
    {
        // Invia un messaggio broadcast per notificare che il GameManager è pronto
        GameObject[] allObjects = FindObjectsOfType<GameObject>();
        foreach (GameObject obj in allObjects)
        {
            if (obj.name.Contains("SceneManager"))
            {
                obj.SendMessage("OnGameManagerReady", SendMessageOptions.DontRequireReceiver);
            }
        }
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

    // ========== MENU E GIOCO (mantenuto dal codice originale) ==========
    
    public void StartGame()
    {
        startMenu.SetActive(false);
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        Debug.Log("Gioco iniziato");

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

    private void OpenPauseMenu()
    {
        pauseMenu.SetActive(true);
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Debug.Log("Menu pausa aperto");
        IgnorePlayerAttackClick();
    }

    public void ResumeGame()
    {
        pauseMenu.SetActive(false);
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        Debug.Log("Gioco ripreso");
        IgnorePlayerAttackClick();
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

    private void IgnorePlayerAttackClick()
    {
        if (playerAttack != null)
        {
            playerAttack.IgnoreNextClick();
            Debug.Log("IgnoreNextClick chiamato");
        }
        else
        {
            Debug.LogWarning("playerAttack non assegnato!");
        }
    }

    // ========== FADE E SCENE LOADING (mantenuto) ==========
    
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
        
        Debug.Log($"=== GameManager Global State ===\n" +
                  $"Global Memories: {collectedGlobalMemories}/{totalGlobalMemories} ({GetGlobalMemoriesProgress():P1})\n" +
                  $"Scene 01 Presents: {collectedScene01Presents}/{totalScene01Presents} ({GetScene01PresentsProgress():P1})\n" +
                  $"Memory Archive: {GetMemoryArchiveCount()} entries\n" +
                  $"Archive: [{string.Join(", ", collectedMemoryNames)}]\n" +
                  $"Total Items: {GetTotalCollectedItems()}/{GetTotalAvailableItems()}\n" +
                  $"Memories by Scene:\n{sceneMemoriesInfo}" +
                  $"Scene Checkpoints:\n{checkpointsInfo}");
    }
    
    [ContextMenu("Reset All Global Data")]
    public void DebugResetGlobalData()
    {
        ResetGlobalData();
    }
}