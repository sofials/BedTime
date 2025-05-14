using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    public GameObject startMenu;
    public GameObject pauseMenu;

    private bool isPaused = false;

    public Button resumeButton;
    public Button quitButton;

    void Start()
    {
        Time.timeScale = 0f;
        startMenu.SetActive(true);
        pauseMenu.SetActive(false);

        resumeButton.onClick.AddListener(ResumeGame);
        quitButton.onClick.AddListener(ExitGame);
    }

    void Update()
    {
        // ESC apre il menu di pausa solo se non siamo nel menu di avvio e non è già in pausa
        if (Input.GetKeyDown(KeyCode.Escape) && !startMenu.activeSelf && !isPaused)
        {
            Debug.Log("Premuto ESC → Apri Pausa");
            OpenPauseMenu();
        }
    }

    public void StartGame()
    {
        Debug.Log("StartGame() chiamato");
        startMenu.SetActive(false);
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // Apre il menu di pausa
    private void OpenPauseMenu()
    {
        isPaused = true;
        pauseMenu.SetActive(true);
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // Riprende il gioco
    public void ResumeGame()
    {
        Debug.Log("ResumeGame() chiamato");
        isPaused = false;
        pauseMenu.SetActive(false);
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void ExitGame()
    {
        Debug.Log("ExitGame() chiamato");
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #else
        Application.Quit();
        #endif
    }
}
