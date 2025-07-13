using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("UI Menus")]
    public GameObject startMenu;
    public GameObject pauseMenu;
    public GameObject inGameUI;  // ✅ Aggiunto riferimento esplicito alla UI in-game
    public PlayerAttack playerAttack;

    [Header("Respawn Settings")]
    [Tooltip("Transform del punto iniziale di spawn, se non c'è un checkpoint attivo.")]
    public Transform levelStartPoint;

    [HideInInspector]
    public Transform currentCheckpoint;

    [Header("Fade Settings")]
    public Image fadeImage;
    public float fadeDuration = 1f;

    private bool isPaused = false;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (inGameUI != null)
                DontDestroyOnLoad(inGameUI);
        }
        else
        {
            Destroy(gameObject);
        }
    }


    private void Start()
    {
        string currentScene = SceneManager.GetActiveScene().name;

        if (currentScene == "00 - Landing in the Dreamworld")
        {
            Time.timeScale = 0f;
            startMenu.SetActive(true);
            pauseMenu.SetActive(false);

            if (inGameUI != null)
                inGameUI.SetActive(false); // ❌ Non mostrare UI in-game all’inizio
        }
        else
        {
            Time.timeScale = 1f;
            startMenu.SetActive(false);
            pauseMenu.SetActive(false);

            if (inGameUI != null)
                inGameUI.SetActive(true); // ✅ Mostra UI in-game nelle altre scene
        }

        if (fadeImage != null)
            StartCoroutine(FadeIn());
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape) && !startMenu.activeSelf)
        {
            if (!isPaused)
                OpenPauseMenu();
            else
                ResumeGame();
        }
    }

    public void StartGame()
    {
        startMenu.SetActive(false);
        Time.timeScale = 1f;
        isPaused = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        Debug.Log("Gioco iniziato");

        if (inGameUI != null)
            inGameUI.SetActive(true); // ✅ Attiva UI in-game

        IgnorePlayerAttackClick();
    }

    private void OpenPauseMenu()
    {
        isPaused = true;
        pauseMenu.SetActive(true);
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Debug.Log("Menu pausa aperto");

        IgnorePlayerAttackClick();
    }

    public void ResumeGame()
    {
        isPaused = false;
        pauseMenu.SetActive(false);
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        Debug.Log("Gioco ripreso");

        IgnorePlayerAttackClick();
    }

    public void ExitGame()
    {
        Debug.Log("Uscita dal gioco");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void SetCheckpoint(Transform checkpoint)
    {
        currentCheckpoint = checkpoint;
        Debug.Log("Checkpoint aggiornato a: " + checkpoint.name);
    }

    private void IgnorePlayerAttackClick()
    {
        if (playerAttack != null)
        {
            playerAttack.IgnoreNextClick();
            Debug.Log("IgnoreNextClick chiamato");
        }
        else
        {
            Debug.LogWarning("playerAttack non assegnato!");
        }
    }

    public void LoadSceneWithFade(string sceneName)
    {
        StartCoroutine(FadeAndLoad(sceneName));
    }

    private IEnumerator FadeAndLoad(string sceneName)
    {
        yield return StartCoroutine(FadeOut());

        SceneManager.LoadScene(sceneName);
        yield return new WaitForSeconds(0.1f); // piccolo delay per sicurezza

        if (startMenu != null)
            startMenu.SetActive(false);

        if (pauseMenu != null)
            pauseMenu.SetActive(false);

        if (inGameUI != null)
            inGameUI.SetActive(true); // ✅ Assicurati che UI in-game sia visibile

        StartCoroutine(FadeIn());
    }

    private IEnumerator FadeOut()
    {
        float t = 0;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            SetFadeAlpha(t / fadeDuration);
            yield return null;
        }
        SetFadeAlpha(1);
    }

    private IEnumerator FadeIn()
    {
        float t = fadeDuration;
        while (t > 0)
        {
            t -= Time.unscaledDeltaTime;
            SetFadeAlpha(t / fadeDuration);
            yield return null;
        }
        SetFadeAlpha(0);
    }

    private void SetFadeAlpha(float alpha)
    {
        if (fadeImage != null)
        {
            Color c = fadeImage.color;
            c.a = Mathf.Clamp01(alpha);
            fadeImage.color = c;
        }
    }
}
