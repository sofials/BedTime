using UnityEngine;

public class PlatformTutorial : MonoBehaviour
{
    public static PlatformTutorial Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}
