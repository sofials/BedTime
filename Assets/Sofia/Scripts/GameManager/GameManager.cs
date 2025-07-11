using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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

    [HideInInspector]
    public Transform currentCheckpoint;

    private bool isPaused = false;

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
        Time.timeScale = 0f;
        startMenu.SetActive(true);
        pauseMenu.SetActive(false);

        // Nascondi barra del potere inizialmente
        if (playerAttack != null && playerAttack.TryGetComponent<PlayerPowerUp>(out var powerUp))
        {
            if (powerUp.powerUI != null)
                powerUp.powerUI.SetActive(false);
        }
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

        // Mostra barra del potere
        if (playerAttack != null && playerAttack.TryGetComponent<PlayerPowerUp>(out var powerUp))
        {
            if (powerUp.powerUI != null)
                powerUp.powerUI.SetActive(true);
        }

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
}
