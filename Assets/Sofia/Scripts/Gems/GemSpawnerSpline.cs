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
    [SerializeField] private bool listenToGameManager = true;
    [SerializeField] private bool autoReinitializeOnSceneReady = true;
    
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
    
    // ✅ NUOVO: Contenitore per organizzare le gemme nella Hierarchy
    private Transform gemsContainer;

    private void Awake()
    {
        if (debugMode)
        {
            Debug.Log($"[GemSpawnerSpline] Awake() - GameObject: {gameObject.name}");
        }

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
        if (!autoReinitializeOnSceneReady) return;

        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

        if (sceneName == currentScene)
        {
            if (debugMode)
            {
                Debug.Log($"[GemSpawnerSpline] GameManager pronto - reinizializzazione opzionale");
            }

            if (!hasInitialized)
            {
                StartCoroutine(SimpleReinitialize());
            }
        }
    }

    private IEnumerator SimpleReinitialize()
    {
        yield return null;
        
        if (!hasInitialized)
        {
            InitializeSpawner();
            hasInitialized = true;
        }
    }

    private IEnumerator SimpleInitialization()
    {
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

    private IEnumerator HandleGameManagerReady()
    {
        var allGems = FindObjectsByType<Gem>(FindObjectsSortMode.None);
        foreach (var gem in allGems)
        {
            if (gem != null)
            {
                Destroy(gem.gameObject);
            }
        }

        yield return null;

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
    /// ✅ NUOVO: Crea o ottiene il contenitore per le gemme (con scala uniforme)
    /// </summary>
    private Transform GetOrCreateGemsContainer()
    {
        if (gemsContainer == null)
        {
            GameObject container = new GameObject($"SpawnedGems_{gameObject.name}");
            // NON parentare - lascia come root per evitare ereditarietà di scale non uniformi
            container.transform.position = Vector3.zero;
            container.transform.rotation = Quaternion.identity;
            container.transform.localScale = Vector3.one;
            gemsContainer = container.transform;

            if (debugMode)
            {
                Debug.Log($"[GemSpawnerSpline] Contenitore '{container.name}' creato come root con scala (1,1,1)");
            }
        }
        return gemsContainer;
    }

    private void InitializeSpawner()
    {
        if (debugMode)
        {
            Debug.Log($"[GemSpawnerSpline] InitializeSpawner() - canSpawn: {canSpawn}");
        }

        if (spawnedGems != null)
        {
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

    private bool ShouldSpawnOnThisSpline(int splineIndex)
    {
        var setting = splineSettings[splineIndex];
        if (setting?.splineContainer == null) return false;

        if (setting.spawnOnStart) return true;

        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        
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

    /// <summary>
    /// ✅ MODIFICATO: Spawna gemme senza parentarle alla spline per evitare problemi di scala
    /// </summary>
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
        
        // ✅ Ottieni il contenitore con scala uniforme
        Transform container = GetOrCreateGemsContainer();

        for (int i = 0; i < setting.gemCount; i++)
        {
            float t = (float)i / (setting.gemCount - 1);
            Vector3 localPos = spline.EvaluatePosition(t);
            Vector3 worldPos = setting.splineContainer.transform.TransformPoint(localPos);

            // Combina rotazioni
            Quaternion prefabRotation = gemPrefab.transform.rotation;
            Quaternion customRotation = Quaternion.Euler(setting.customRotation);
            Quaternion finalRotation = customRotation * prefabRotation;

            // ✅ FIX: Parenta al contenitore con scala uniforme invece che alla spline
            // Questo garantisce che la scala della gemma rimanga corretta
            GameObject newGem = Instantiate(gemPrefab, worldPos, finalRotation, container);
            
            // La scala rimane quella del prefab perché il container ha scala (1,1,1)

            if (debugMode)
            {
                Debug.Log($"[GemSpawner] Spline {splineIndex}, Gem {i}: Pos={worldPos}, Scale={newGem.transform.localScale}");
            }

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
        
        // ✅ Pulisci anche il contenitore
        if (gemsContainer != null)
        {
            if (Application.isPlaying)
                Destroy(gemsContainer.gameObject);
            else
                DestroyImmediate(gemsContainer.gameObject);
            gemsContainer = null;
        }
    }

    // Getters e utility
    public bool IsSpawningEnabled() => canSpawn;
    public void SetSpawningEnabled(bool enabled) => canSpawn = enabled;
    public int GetSplineCount() => splineSettings?.Length ?? 0;

    /// <summary>
    /// Ottiene il contenitore delle gemme spawnate (utile per muoverle insieme alla spline)
    /// </summary>
    public Transform GetGemsContainer() => gemsContainer;

    [ContextMenu("Debug State")]
    public void DebugState()
    {
        Debug.Log($"=== GemSpawnerSpline Debug ===\n" +
                  $"Scene: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}\n" +
                  $"hasInitialized: {hasInitialized}\n" +
                  $"canSpawn: {canSpawn}\n" +
                  $"listenToGameManager: {listenToGameManager}\n" +
                  $"autoReinitializeOnSceneReady: {autoReinitializeOnSceneReady}\n" +
                  $"splineSettings: {splineSettings?.Length ?? 0}\n" +
                  $"gemsContainer: {(gemsContainer != null ? gemsContainer.name : "NULL")}");
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnSceneReady -= OnGameManagerSceneReady;
        }
        
        ImmediateClearAllGems();
    }
}