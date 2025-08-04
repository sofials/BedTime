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
    public PlayerAttack playerAttack;

    [Header("Respawn Settings")]
    [Tooltip("Transform del punto iniziale di spawn, se non c'è un checkpoint attivo.")]
    public Transform levelStartPoint;
    [HideInInspector] public Transform currentCheckpoint;

    [Header("Fade Settings")]
    public Image fadeImage;
    public float fadeDuration = 1f;

    [Header("Collectibles Settings")]
    public Presents collectible1; // assegna da Inspector
    public Presents collectible2; // assegna da Inspector
    [Tooltip("Collider (BoxCollider) del muro da disabilitare quando entrambi i regali sono raccolti")]
    public Collider wallColliderToDisable;

   // private bool isPaused = false;
    private int collectedCount = 0;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
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

            if (playerAttack != null && playerAttack.TryGetComponent<PlayerPowerUp>(out var powerUp))
                if (powerUp.powerUI != null)
                    powerUp.powerUI.SetActive(false);
        }
        else
        {
            Time.timeScale = 1f;
            startMenu.SetActive(false);
            pauseMenu.SetActive(false);

            if (playerAttack != null && playerAttack.TryGetComponent<PlayerPowerUp>(out var powerUp))
                if (powerUp.powerUI != null)
                    powerUp.powerUI.SetActive(true);
        }

        if (fadeImage != null)
            StartCoroutine(FadeIn());

        // Inizializza regali (se presenti)
        if (collectible1 != null) collectible1.gameManager = this;
        if (collectible2 != null) collectible2.gameManager = this;
    }

    private void Update()
{
    // Disabilito per ora la pausa con ESC
    /*
    if (Input.GetKeyDown(KeyCode.Escape) && !startMenu.activeSelf)
    {
        if (!isPaused)
            OpenPauseMenu();
        else
            ResumeGame();
    }
    */
}


    public void StartGame()
    {
        startMenu.SetActive(false);
        Time.timeScale = 1f;
      //  isPaused = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        Debug.Log("Gioco iniziato");

        if (playerAttack != null && playerAttack.TryGetComponent<PlayerPowerUp>(out var powerUp))
            if (powerUp.powerUI != null)
                powerUp.powerUI.SetActive(true);

        IgnorePlayerAttackClick();
    }

    private void OpenPauseMenu()
    {
       // isPaused = true;
        pauseMenu.SetActive(true);
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Debug.Log("Menu pausa aperto");

        IgnorePlayerAttackClick();
    }

    public void ResumeGame()
    {
      //  isPaused = false;
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
        yield return new WaitForSeconds(0.1f);
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

    // Metodo chiamato dai collectible (Presents)
    public void NotifyCollected(Presents collectedObject)
    {
        collectedCount++;
        Debug.Log("Oggetto raccolto: " + collectedObject.name);

        if (collectedCount >= 2)
        {
            Debug.Log("Entrambi gli oggetti raccolti!");
            if (wallColliderToDisable != null)
            {
                wallColliderToDisable.enabled = false;
                Debug.Log("Collider muro disabilitato");
            }
        }
    }
}
