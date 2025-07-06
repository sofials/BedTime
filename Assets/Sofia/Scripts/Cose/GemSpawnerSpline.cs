using UnityEngine;
using UnityEngine.Splines;

public class GemSpawnerSpline : MonoBehaviour
{
    [Header("Prefab della Gemma")]
    [SerializeField] private GameObject gemPrefab;

    [Header("Spline da seguire (puoi assegnarne più di una)")]
    [SerializeField] private SplineContainer[] splineContainers;

    [Header("Numero di Gemme per Spline")]
    [SerializeField] private int gemCount = 20;

    private void Start()
    {
        Debug.Log("[GemSpawnerSpline] Start chiamato");
        SpawnGemsAlongSplines();
    }

    private void SpawnGemsAlongSplines()
    {
        if (splineContainers == null || splineContainers.Length == 0)
        {
            Debug.LogWarning("[GemSpawnerSpline] Nessun splineContainer assegnato!");
            return;
        }
        if (gemPrefab == null)
        {
            Debug.LogWarning("[GemSpawnerSpline] gemPrefab NON assegnato!");
            return;
        }
        if (gemCount <= 0)
        {
            Debug.LogWarning("[GemSpawnerSpline] gemCount <= 0!");
            return;
        }

        foreach (var splineContainer in splineContainers)
        {
            if (splineContainer == null)
            {
                Debug.LogWarning("[GemSpawnerSpline] Uno degli splineContainer è null, salto.");
                continue;
            }

            Spline spline = splineContainer.Spline;

            for (int i = 0; i < gemCount; i++)
            {
                float t = (float)i / (gemCount - 1);
                Vector3 position = spline.EvaluatePosition(t);

                Quaternion rotation = Quaternion.Euler(-90f, 0f, 0f);
                Instantiate(gemPrefab, position, rotation);
            }
        }
    }
}
