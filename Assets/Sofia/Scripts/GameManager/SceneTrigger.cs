using UnityEngine;

public class SceneTrigger : MonoBehaviour
{
    [Tooltip("Nome esatto della scena da caricare")]
    public string sceneToLoad;

    private bool hasTriggered = false;

    private void OnTriggerEnter(Collider other)
    {
        if (hasTriggered) return;

        if (other.CompareTag("Player"))
        {
            hasTriggered = true;
            GameManager.Instance.LoadSceneWithFade(sceneToLoad);
            Debug.Log($"[SceneTrigger] Cambio scena a {sceneToLoad}");
        }
    }
}
