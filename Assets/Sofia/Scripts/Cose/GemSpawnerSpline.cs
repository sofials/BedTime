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
        SpawnGemsAlongSplines();
    }

    private void SpawnGemsAlongSplines()
    {
        if (splineContainers == null || splineContainers.Length == 0)
            return;
        if (gemPrefab == null)
            return;
        if (gemCount <= 0)
            return;

        foreach (var splineContainer in splineContainers)
        {
            if (splineContainer == null)
                continue;

            Spline spline = splineContainer.Spline;

            for (int i = 0; i < gemCount; i++)
            {
                float t = (float)i / (gemCount - 1);
                Vector3 localPos = spline.EvaluatePosition(t);
                Vector3 worldPos = splineContainer.transform.TransformPoint(localPos);

                Quaternion rotation = Quaternion.Euler(-90f, 0f, 0f);
                Instantiate(gemPrefab, worldPos, rotation);
            }
        }
    }
}
