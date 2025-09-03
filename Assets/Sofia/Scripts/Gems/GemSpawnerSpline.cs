using UnityEngine;
using UnityEngine.Splines;
using System.Collections;

public class GemSpawnerSpline : MonoBehaviour
{
    [Header("Prefab della Gemma")]
    [SerializeField] private GameObject gemPrefab;

    [Header("Configurazione Spline")]
    [SerializeField] private SplineSettings[] splineSettings;

    [Header("Controllo Spawning Globale")]
    [SerializeField] private bool canSpawn = true;
    
    [Header("Gestione Indipendente")]
    [SerializeField] private bool listenToGameManager = true; // NUOVO
    [SerializeField] private bool autoReinitializeOnSceneReady = true; // NUOVO
    
    [Header("Debug")]
    [SerializeField] private bool debugMode = false;

    [System.Serializable]
    public class SplineSettings
    {
        [Header("Spline Container")]
        public SplineContainer splineContainer;
        
        [Header("Spawning Settings")]
        public bool spawnOnStart = true;
        public int gemCount = 20;
        
        [Header("Rotazione (opzionale)")]
        public Vector3 customRotation = new Vector3(-90f, 0f, 0f);
    }

    private GameObject[][] spawnedGems;
    private bool hasInitialized = false;

    private void Awake()
    {
        if (debugMode)
        {
            Debug.Log($"[GemSpawnerSpline] Awake() - GameObject: {gameObject.name}");
        }

      // SEMPLIFICATO: Disabilita l'ascolto del GameManager di default per evitare interferenze
    if (listenToGameManager && GameManager.Instance != null)
    {
        GameManager.Instance.OnSceneReady += OnGameManagerSceneReady;
    }
    }

    private void Start()
    {
        if (debugMode)
        {
            Debug.Log($"[GemSpawnerSpline] Start() - Inizializzazione indipendente");
        }
        
        StartCoroutine(SimpleInitialization());
    }

    private void OnGameManagerSceneReady(string sceneName)
    {
        // SEMPLIFICATO: Reinizializza solo se esplicitamente richiesto
        if (!autoReinitializeOnSceneReady) return;

        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

        if (sceneName == currentScene)
        {
            if (debugMode)
            {
                Debug.Log($"[GemSpawnerSpline] GameManager pronto - reinizializzazione opzionale");
            }

            // SEMPLIFICATO: Solo reinizializza se non è già stato fatto
            if (!hasInitialized)
            {
                StartCoroutine(SimpleReinitialize());
            }
        }
    }
/// <summary>
/// SEMPLIFICATO: Reinizializzazione più diretta
/// </summary>
private IEnumerator SimpleReinitialize()
{
    yield return null;
    
    if (!hasInitialized)
    {
        InitializeSpawner();
        hasInitialized = true;
    }
}
    /// <summary>
/// SEMPLIFICATO: Inizializzazione più diretta
/// </summary>
private IEnumerator SimpleInitialization()
{
    // Un solo frame di delay
    yield return null;
    
    if (!hasInitialized)
    {
        if (debugMode)
        {
            Debug.Log($"[GemSpawnerSpline] Inizializzazione semplice avviata");
        }
        
        InitializeSpawner();
        hasInitialized = true;
    }
}

    /// <summary>
    /// NUOVO: Gestisce la reinizializzazione quando il GameManager è pronto
    /// </summary>
    private IEnumerator HandleGameManagerReady()
    {
        // Pulisci gemme esistenti
        var allGems = FindObjectsByType<Gem>(FindObjectsSortMode.None);
        foreach (var gem in allGems)
        {
            if (gem != null)
            {
                Destroy(gem.gameObject);
            }
        }

        yield return null; // Aspetta la distruzione

        // Reinizializza
        ForceReinitialize();
    }

    private IEnumerator DelayedInitialization()
    {
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();
        
        if (!hasInitialized)
        {
            InitializeSpawner();
            hasInitialized = true;
        }
    }

   /// <summary>
/// MODIFICATO: InitializeSpawner semplificato
/// </summary>
private void InitializeSpawner()
{
    if (debugMode)
    {
        Debug.Log($"[GemSpawnerSpline] InitializeSpawner() - canSpawn: {canSpawn}");
    }

    // SEMPLIFICATO: Non pulire se non necessario
    if (spawnedGems != null)
    {
        // Solo pulisci se ci sono già gemme spawnnate
        ClearAllGems();
    }

    if (splineSettings != null && splineSettings.Length > 0 && canSpawn)
    {
        spawnedGems = new GameObject[splineSettings.Length][];
        
        for (int i = 0; i < splineSettings.Length; i++)
        {
            if (splineSettings[i] != null)
            {
                spawnedGems[i] = new GameObject[splineSettings[i].gemCount];
                
                if (ShouldSpawnOnThisSpline(i))
                {
                    if (debugMode)
                    {
                        Debug.Log($"[GemSpawnerSpline] Spawning su spline {i} - {splineSettings[i].gemCount} gemme");
                    }
                    SpawnGemsOnSpecificSpline(i);
                }
            }
        }
    }
    else
    {
        if (debugMode)
        {
            Debug.LogWarning($"[GemSpawnerSpline] Spawning saltato - canSpawn: {canSpawn}, splineSettings: {splineSettings?.Length ?? 0}");
        }
    }
}

    /// <summary>
    /// NUOVO: Logica per determinare se spawnnare su una spline
    /// </summary>
    private bool ShouldSpawnOnThisSpline(int splineIndex)
    {
        var setting = splineSettings[splineIndex];
        if (setting?.splineContainer == null) return false;

        // Se spawnOnStart è true, spawna sempre
        if (setting.spawnOnStart) return true;

        // Logica specifica per scena se necessario
        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        
        // Esempio: spawna sempre nella scena 01
        if (currentScene.Contains("01") || currentScene.Contains("Party"))
        {
            return true;
        }

        return false;
    }

    public void ForceReinitialize()
    {
        if (debugMode)
        {
            Debug.Log("[GemSpawnerSpline] ForceReinitialize indipendente");
        }
        
        hasInitialized = false;
        ClearAllGems();
        StartCoroutine(DelayedReinitialize());
    }
    
    private IEnumerator DelayedReinitialize()
    {
        yield return null;
        
        if (!hasInitialized)
        {
            InitializeSpawner();
            hasInitialized = true;
        }
    }

    // NUOVO: Metodi per configurazione indipendente
    public void SetGameManagerIntegration(bool enabled)
    {
        if (listenToGameManager != enabled)
        {
            listenToGameManager = enabled;
            
            if (enabled && GameManager.Instance != null)
            {
                GameManager.Instance.OnSceneReady += OnGameManagerSceneReady;
            }
            else if (!enabled && GameManager.Instance != null)
            {
                GameManager.Instance.OnSceneReady -= OnGameManagerSceneReady;
            }
        }
    }

    public void SetAutoReinitialize(bool enabled)
    {
        autoReinitializeOnSceneReady = enabled;
    }

    // [Resto dei metodi esistenti rimane uguale...]
    
    public void SpawnGemsAlongSplines()
    {
        if (!canSpawn) return;
        if (splineSettings == null || splineSettings.Length == 0) return;
        if (gemPrefab == null) return;

        for (int splineIndex = 0; splineIndex < splineSettings.Length; splineIndex++)
        {
            var setting = splineSettings[splineIndex];
            if (setting?.splineContainer != null)
            {
                SpawnGemsOnSpecificSpline(splineIndex);
            }
        }
    }

    public void SpawnGemsOnSpecificSpline(int splineIndex)
    {
        if (!canSpawn) return;
        if (splineIndex < 0 || splineIndex >= splineSettings.Length) return;

        var setting = splineSettings[splineIndex];
        if (setting?.splineContainer == null) return;

        if (spawnedGems == null)
        {
            spawnedGems = new GameObject[splineSettings.Length][];
        }
        
        if (spawnedGems[splineIndex] == null)
        {
            spawnedGems[splineIndex] = new GameObject[setting.gemCount];
        }

        ClearGemsOnSpecificSpline(splineIndex);

        Spline spline = setting.splineContainer.Spline;

        for (int i = 0; i < setting.gemCount; i++)
        {
            float t = (float)i / (setting.gemCount - 1);
            Vector3 localPos = spline.EvaluatePosition(t);
            Vector3 worldPos = setting.splineContainer.transform.TransformPoint(localPos);

            GameObject newGem = Instantiate(gemPrefab, worldPos, gemPrefab.transform.rotation);
            spawnedGems[splineIndex][i] = newGem;
        }
    }

    public void ClearAllGems()
    {
        if (spawnedGems == null) return;

        for (int splineIndex = 0; splineIndex < spawnedGems.Length; splineIndex++)
        {
            ClearGemsOnSpecificSpline(splineIndex);
        }
    }

    public void ClearGemsOnSpecificSpline(int splineIndex)
    {
        if (spawnedGems == null || splineIndex < 0 || splineIndex >= spawnedGems.Length) return;
        if (spawnedGems[splineIndex] == null) return;

        for (int i = 0; i < spawnedGems[splineIndex].Length; i++)
        {
            if (spawnedGems[splineIndex][i] != null)
            {
                Destroy(spawnedGems[splineIndex][i]);
                spawnedGems[splineIndex][i] = null;
            }
        }
    }

    public void ImmediateClearAllGems()
    {
        // Pulisci tutte le gemme nella scena
        Gem[] allGems = FindObjectsByType<Gem>(FindObjectsSortMode.None);
        foreach (var gem in allGems)
        {
            if (gem?.gameObject != null)
            {
                if (Application.isPlaying)
                    Destroy(gem.gameObject);
                else
                    DestroyImmediate(gem.gameObject);
            }
        }
        
        // Pulisci riferimenti interni
        if (spawnedGems != null)
        {
            for (int splineIndex = 0; splineIndex < spawnedGems.Length; splineIndex++)
            {
                if (spawnedGems[splineIndex] != null)
                {
                    for (int i = 0; i < spawnedGems[splineIndex].Length; i++)
                    {
                        spawnedGems[splineIndex][i] = null;
                    }
                }
            }
        }
    }

    // Getters e utility
    public bool IsSpawningEnabled() => canSpawn;
    public void SetSpawningEnabled(bool enabled) => canSpawn = enabled;
    public int GetSplineCount() => splineSettings?.Length ?? 0;

    [ContextMenu("Debug State")]
    public void DebugState()
    {
        Debug.Log($"=== GemSpawnerSpline Debug (Indipendente) ===\n" +
                  $"Scene: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}\n" +
                  $"hasInitialized: {hasInitialized}\n" +
                  $"canSpawn: {canSpawn}\n" +
                  $"listenToGameManager: {listenToGameManager}\n" +
                  $"autoReinitializeOnSceneReady: {autoReinitializeOnSceneReady}\n" +
                  $"splineSettings: {splineSettings?.Length ?? 0}");
    }

    private void OnDestroy()
    {
        // Disconnetti eventi
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnSceneReady -= OnGameManagerSceneReady;
        }
        
        ImmediateClearAllGems();
    }
}