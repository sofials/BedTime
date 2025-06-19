using UnityEngine;
using UnityEngine.Splines;

public class GemSpawnerSpline : MonoBehaviour
{
    [Header("Prefab della Gemma")]
    [SerializeField] private GameObject gemPrefab;

    [Header("Spline da seguire")]
    [SerializeField] private SplineContainer splineContainer;

    [Header("Numero di Gemme")]
    [SerializeField] private int gemCount = 20;

    private void Start()
    {
        SpawnGemsAlongSpline();
    }

    private void SpawnGemsAlongSpline()
    {
        if (splineContainer == null || gemPrefab == null || gemCount <= 0)
        {
            Debug.LogWarning("Spline, prefab o gem count non settati correttamente.");
            return;
        }

        Spline spline = splineContainer.Spline;

        for (int i = 0; i < gemCount; i++)
        {
            // Calcola la posizione "t" lungo la spline (da 0 a 1)
            float t = (float)i / (gemCount - 1);

            // Ottieni la posizione sulla spline
            Vector3 position = spline.EvaluatePosition(t);

            // Crea la rotazione desiderata (-90° lungo X)
            Quaternion rotation = Quaternion.Euler(-90f, 0f, 0f);

            // Instanzia la gemma in questa posizione e rotazione
            Instantiate(gemPrefab, position, rotation);
        }
    }
}
