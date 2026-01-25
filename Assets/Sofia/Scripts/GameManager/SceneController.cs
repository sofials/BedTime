using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;

public class SceneController : MonoBehaviour
{
    [SerializeField]
    private float _sceneFadeDuration;
    private SceneFade _sceneFade;
    [Header("Credits (opzionale)")]
[SerializeField] private CreditsScroller creditsScroller;
    private void Awake()
    {
        _sceneFade = GetComponentInChildren<SceneFade>();

    }
    private IEnumerator Start()
    {
        yield return _sceneFade.FadeInCoroutine(_sceneFadeDuration);
          // Avvia solo se configurato
    if (creditsScroller != null)
        creditsScroller.StartScrolling();
    }
    public void LoadScene(string sceneName)
    {
        StartCoroutine(LoadSceneCoroutine(sceneName));
    }
    private IEnumerator LoadSceneCoroutine(string sceneName)
    {
        yield return _sceneFade.FadeOutCoroutine(_sceneFadeDuration);
        yield return SceneManager.LoadSceneAsync(sceneName);
    }
}
