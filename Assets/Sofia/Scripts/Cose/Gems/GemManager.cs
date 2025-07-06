using UnityEngine;

public class GemManager : MonoBehaviour
{
    public static GemManager Instance { get; private set; }

    [Header("Gem Prefabs")]
    public GameObject lifeGemPrefab;   // assegna UNA VOLTA il prefab qui!

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

public void SpawnLifeGem(Vector3 position)
{
    if (lifeGemPrefab != null)
    {
       Vector3 spawnPos = position + new Vector3(0, 3f, 0);
       Instantiate(lifeGemPrefab, spawnPos, Quaternion.Euler(270f, 0f, 0f));

    }
}
}
