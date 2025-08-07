using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

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
    [HideInInspector] public Transform currentCheckpoint;

    [Header("Fade Settings")]
    public Image fadeImage;
    public float fadeDuration = 1f;

    [Header("Collectibles Tracking")]
    [SerializeField] private int totalGlobalMemories = 0;
    [SerializeField] private int collectedGlobalMemories = 0;
    [SerializeField] private int totalPresents = 0;
    [SerializeField] private int collectedPresents = 0;
    
    [Header("Memory Archive (Global)")]
    [SerializeField] private List<string> collectedMemoryNames = new List<string>();
    [SerializeField] private List<string> collectedMemoryScenes = new List<string>();
    
    [Header("Debug Collectibles")]
    [SerializeField] private bool enableCollectibleLogs = true;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            // Carica i dati salvati dei collectibles
            LoadCollectibleData();
        }
        else
        {
            Destroy(gameObject);
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
    }

    private void Update()
    {
        // Disabilito per ora la pausa con ESC
        /*
        if (Input.GetKeyDown(KeyCode.Escape) && !startMenu.activeSelf)
        {
            if (!isPaused)
                OpenPauseMenu();
            else
                ResumeGame();
        }
        */
    }

    public void StartGame()
    {
        startMenu.SetActive(false);
        Time.timeScale = 1f;
        //  isPaused = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        Debug.Log("Gioco iniziato");

        if (playerAttack != null && playerAttack.TryGetComponent<PlayerPowerUp>(out var powerUp))
            if (powerUp.powerUI != null)
                powerUp.powerUI.SetActive(true);

        IgnorePlayerAttackClick();
    }

    private void OpenPauseMenu()
    {
        // isPaused = true;
        pauseMenu.SetActive(true);
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Debug.Log("Menu pausa aperto");

        IgnorePlayerAttackClick();
    }

    public void ResumeGame()
    {
        //  isPaused = false;
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

    public void SetCheckpoint(Transform checkpoint)
    {
        currentCheckpoint = checkpoint;
        Debug.Log("Checkpoint aggiornato a: " + checkpoint.name);
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

    // ========== METODI CHIAMATI DAL SCENEMANAGER01 ==========
    
    public void OnMemoryCollected(int current, int total)
    {
        CollectibleLog($"Memory raccolta: {current}/{total}");
        
        // Aggiorna il conteggio globale
        collectedGlobalMemories++;
        
        // Salva i dati
        SaveCollectibleData();
    }
    
    public void OnPresentCollected(int current, int total)
    {
        CollectibleLog($"Present raccolto: {current}/{total}");
        
        collectedPresents = current;
        totalPresents = total;
        
        // Salva i dati
        SaveCollectibleData();
    }
    
    public void OnAllMemoriesCompleted()
    {
        CollectibleLog("🧠 Tutte le memorie della scena completate!");
        
        // Salva il progresso
        SaveCollectibleData();
    }
    
    public void OnAllPresentsCompleted()
    {
        CollectibleLog("🎁 Tutti i present completati!");
        
        // Salva il progresso  
        SaveCollectibleData();
    }
    
    public void OnAllCollectiblesCompleted()
    {
        CollectibleLog("🎉 Livello completato al 100%!");
        
        // Salva il progresso
        SaveCollectibleData();
    }
    
    // ========== GESTIONE ARCHIVIO MEMORIE ==========
    
    public void AddMemoryToArchive(string memoryName, string sceneName)
    {
        if (!collectedMemoryNames.Contains(memoryName))
        {
            collectedMemoryNames.Add(memoryName);
            collectedMemoryScenes.Add(sceneName);
            
            CollectibleLog($"Memory '{memoryName}' aggiunta all'archivio dalla scena '{sceneName}'");
            
            SaveCollectibleData();
        }
    }
    
    public List<string> GetCollectedMemoryNames()
    {
        return new List<string>(collectedMemoryNames);
    }
    
    public List<string> GetCollectedMemoryScenes()
    {
        return new List<string>(collectedMemoryScenes);
    }
    
    public bool HasMemoryInArchive(string memoryName)
    {
        return collectedMemoryNames.Contains(memoryName);
    }
    
    public int GetTotalMemoriesInArchive()
    {
        return collectedMemoryNames.Count;
    }
    
    // ========== METODI PUBBLICI PER COLLECTIBLES ==========
    
    public int GetCollectedGlobalMemories() => collectedGlobalMemories;
    public int GetTotalGlobalMemories() => totalGlobalMemories;
    public int GetCollectedPresents() => collectedPresents;
    public int GetTotalPresents() => totalPresents;
    
    public void SetTotalGlobalMemories(int total)
    {
        totalGlobalMemories = total;
        SaveCollectibleData();
    }
    
    // ========== SALVATAGGIO E CARICAMENTO ==========
    
    private void SaveCollectibleData()
    {
        PlayerPrefs.SetInt("GlobalMemoriesCollected", collectedGlobalMemories);
        PlayerPrefs.SetInt("GlobalMemoriesTotal", totalGlobalMemories);
        PlayerPrefs.SetInt("PresentsCollected", collectedPresents);
        PlayerPrefs.SetInt("PresentsTotal", totalPresents);
        
        // Salva l'archivio delle memorie
        PlayerPrefs.SetInt("ArchiveMemoryCount", collectedMemoryNames.Count);
        for (int i = 0; i < collectedMemoryNames.Count; i++)
        {
            PlayerPrefs.SetString($"ArchiveMemoryName_{i}", collectedMemoryNames[i]);
            PlayerPrefs.SetString($"ArchiveMemoryScene_{i}", collectedMemoryScenes[i]);
        }
        
        PlayerPrefs.Save();
        CollectibleLog("Dati collectibles salvati");
    }
    
    private void LoadCollectibleData()
    {
        collectedGlobalMemories = PlayerPrefs.GetInt("GlobalMemoriesCollected", 0);
        totalGlobalMemories = PlayerPrefs.GetInt("GlobalMemoriesTotal", 0);
        collectedPresents = PlayerPrefs.GetInt("PresentsCollected", 0);
        totalPresents = PlayerPrefs.GetInt("PresentsTotal", 0);
        
        // Carica l'archivio delle memorie
        int archiveCount = PlayerPrefs.GetInt("ArchiveMemoryCount", 0);
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
        
        CollectibleLog($"Dati collectibles caricati - Memorie globali: {collectedGlobalMemories}/{totalGlobalMemories}, Present: {collectedPresents}/{totalPresents}, Archivio: {collectedMemoryNames.Count} memorie");
    }
    
    // ========== RESET E DEBUG ==========
    
    public void ResetCollectibleData()
    {
        collectedGlobalMemories = 0;
        totalGlobalMemories = 0;
        collectedPresents = 0;
        totalPresents = 0;
        collectedMemoryNames.Clear();
        collectedMemoryScenes.Clear();
        
        // Cancella i dati salvati
        PlayerPrefs.DeleteKey("GlobalMemoriesCollected");
        PlayerPrefs.DeleteKey("GlobalMemoriesTotal");
        PlayerPrefs.DeleteKey("PresentsCollected");
        PlayerPrefs.DeleteKey("PresentsTotal");
        
        int archiveCount = PlayerPrefs.GetInt("ArchiveMemoryCount", 0);
        for (int i = 0; i < archiveCount; i++)
        {
            PlayerPrefs.DeleteKey($"ArchiveMemoryName_{i}");
            PlayerPrefs.DeleteKey($"ArchiveMemoryScene_{i}");
        }
        PlayerPrefs.DeleteKey("ArchiveMemoryCount");
        
        PlayerPrefs.Save();
        CollectibleLog("Tutti i dati collectibles resettati");
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
    
    // Metodi per debug nell'inspector
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [ContextMenu("Debug Collectible State")]
    public void DebugCollectibleState()
    {
        Debug.Log($"=== GameManager Collectibles Debug ===\n" +
                  $"Global Memories: {collectedGlobalMemories}/{totalGlobalMemories}\n" +
                  $"Presents: {collectedPresents}/{totalPresents}\n" +
                  $"Memory Archive: {collectedMemoryNames.Count} entries\n" +
                  $"Archive Names: [{string.Join(", ", collectedMemoryNames)}]");
    }
    
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [ContextMenu("Reset All Collectibles")]
    public void DebugResetCollectibles()
    {
        ResetCollectibleData();
    }
}