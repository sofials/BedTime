using UnityEngine;
using UnityEngine.Splines;

public class GemSpawnerSpline : MonoBehaviour
{
    [Header("Prefab della Gemma")]
    [SerializeField] private GameObject gemPrefab;

    [Header("Configurazione Spline")]
    [SerializeField] private SplineSettings[] splineSettings;

    [Header("Controllo Spawning Globale")]
    [SerializeField] private bool canSpawn = true; // Toggle per abilitare/disabilitare lo spawning

    [System.Serializable]
    public class SplineSettings
    {
        [Header("Spline Container")]
        public SplineContainer splineContainer;
        
        [Header("Spawning Settings")]
        public bool spawnOnStart = false; // Toggle per spawning automatico per questa spline
        public int gemCount = 20; // Numero di gemme per questa spline specifica
        
        [Header("Rotazione (opzionale)")]
        public Vector3 customRotation = new Vector3(-90f, 0f, 0f); // Rotazione personalizzata
    }

    // Lista per tenere traccia delle gemme istanziate per ogni spline
    private GameObject[][] spawnedGems;

    private void Start()
    {
        // Inizializza l'array per tenere traccia delle gemme
        if (splineSettings != null && splineSettings.Length > 0)
        {
            spawnedGems = new GameObject[splineSettings.Length][];
            for (int i = 0; i < splineSettings.Length; i++)
            {
                if (splineSettings[i] != null)
                {
                    spawnedGems[i] = new GameObject[splineSettings[i].gemCount];
                    
                    // Spawna solo se il toggle è attivo per questa spline specifica
                    if (splineSettings[i].spawnOnStart)
                    {
                        SpawnGemsOnSpecificSpline(i);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Metodo pubblico per spawnare le gemme lungo tutte le spline.
    /// Può essere chiamato da altri script come evento.
    /// </summary>
    public void SpawnGemsAlongSplines()
    {
        if (!canSpawn)
        {
            Debug.LogWarning("Spawning delle gemme è disabilitato!");
            return;
        }

        if (splineSettings == null || splineSettings.Length == 0)
        {
            Debug.LogWarning("Nessuna spline setting configurata!");
            return;
        }

        if (gemPrefab == null)
        {
            Debug.LogWarning("Nessun prefab gemma assegnato!");
            return;
        }

        for (int splineIndex = 0; splineIndex < splineSettings.Length; splineIndex++)
        {
            var setting = splineSettings[splineIndex];
            
            if (setting == null || setting.splineContainer == null)
                continue;

            SpawnGemsOnSpecificSpline(splineIndex);
        }
    }

    /// <summary>
    /// Spawna gemme su una spline specifica tramite indice.
    /// </summary>
    /// <param name="splineIndex">Indice della spline nel array</param>
    public void SpawnGemsOnSpecificSpline(int splineIndex)
    {
        if (!canSpawn)
        {
            Debug.LogWarning("Spawning delle gemme è disabilitato!");
            return;
        }

        if (splineIndex < 0 || splineIndex >= splineSettings.Length)
        {
            Debug.LogWarning($"Indice spline {splineIndex} non valido!");
            return;
        }

        var setting = splineSettings[splineIndex];
        if (setting == null || setting.splineContainer == null)
        {
            Debug.LogWarning($"Spline setting all'indice {splineIndex} è null o non ha un container!");
            return;
        }

        if (setting.gemCount <= 0)
        {
            Debug.LogWarning($"Il numero di gemme per la spline {splineIndex} deve essere maggiore di 0!");
            return;
        }

        // Rimuovi le gemme esistenti per questa spline se presenti
        ClearGemsOnSpecificSpline(splineIndex);

        Spline spline = setting.splineContainer.Spline;

        for (int i = 0; i < setting.gemCount; i++)
{
    float t = (float)i / (setting.gemCount - 1);
    Vector3 localPos = spline.EvaluatePosition(t);
    Vector3 worldPos = setting.splineContainer.transform.TransformPoint(localPos);

    // Usa la rotazione originale del prefab
    GameObject newGem = Instantiate(gemPrefab, worldPos, gemPrefab.transform.rotation);

    // Salva il riferimento alla gemma
    spawnedGems[splineIndex][i] = newGem;
    // Rimuovi: newGem.transform.rotation = rotation;
}

        Debug.Log($"Spawnatе {setting.gemCount} gemme sulla spline {splineIndex}");
    }

    /// <summary>
    /// Rimuove tutte le gemme da tutte le spline.
    /// </summary>
    public void ClearAllGems()
    {
        if (spawnedGems == null) return;

        for (int splineIndex = 0; splineIndex < spawnedGems.Length; splineIndex++)
        {
            ClearGemsOnSpecificSpline(splineIndex);
        }

        Debug.Log("Tutte le gemme sono state rimosse");
    }

    /// <summary>
    /// Rimuove le gemme da una spline specifica.
    /// </summary>
    /// <param name="splineIndex">Indice della spline</param>
    public void ClearGemsOnSpecificSpline(int splineIndex)
    {
        if (spawnedGems == null || splineIndex < 0 || splineIndex >= spawnedGems.Length) return;

        for (int i = 0; i < spawnedGems[splineIndex].Length; i++)
        {
            if (spawnedGems[splineIndex][i] != null)
            {
                DestroyImmediate(spawnedGems[splineIndex][i]);
                spawnedGems[splineIndex][i] = null;
            }
        }

        Debug.Log($"Gemme rimosse dalla spline {splineIndex}");
    }

    /// <summary>
    /// Abilita o disabilita la possibilità di spawnare gemme.
    /// </summary>
    /// <param name="enabled">True per abilitare, false per disabilitare</param>
    public void SetSpawningEnabled(bool enabled)
    {
        canSpawn = enabled;
        Debug.Log($"Spawning gemme: {(enabled ? "Abilitato" : "Disabilitato")}");
    }

    /// <summary>
    /// Restituisce se lo spawning è attualmente abilitato.
    /// </summary>
    /// <returns>True se lo spawning è abilitato</returns>
    public bool IsSpawningEnabled()
    {
        return canSpawn;
    }

    /// <summary>
    /// Toggle dello stato di spawning.
    /// </summary>
    public void ToggleSpawning()
    {
        SetSpawningEnabled(!canSpawn);
    }

    /// <summary>
    /// Abilita o disabilita lo spawn on start per una spline specifica.
    /// </summary>
    /// <param name="splineIndex">Indice della spline</param>
    /// <param name="enabled">True per abilitare spawn on start</param>
    public void SetSplineSpawnOnStart(int splineIndex, bool enabled)
    {
        if (splineIndex >= 0 && splineIndex < splineSettings.Length && splineSettings[splineIndex] != null)
        {
            splineSettings[splineIndex].spawnOnStart = enabled;
            Debug.Log($"Spawn on start per spline {splineIndex}: {(enabled ? "Abilitato" : "Disabilitato")}");
        }
    }

    /// <summary>
    /// Restituisce il numero di spline configurate.
    /// </summary>
    /// <returns>Numero di spline</returns>
    public int GetSplineCount()
    {
        return splineSettings != null ? splineSettings.Length : 0;
    }

    /// <summary>
    /// Restituisce le impostazioni di una spline specifica.
    /// </summary>
    /// <param name="splineIndex">Indice della spline</param>
    /// <returns>SplineSettings o null se l'indice non è valido</returns>
    public SplineSettings GetSplineSettings(int splineIndex)
    {
        if (splineIndex >= 0 && splineIndex < splineSettings.Length)
            return splineSettings[splineIndex];
        return null;
    }
}