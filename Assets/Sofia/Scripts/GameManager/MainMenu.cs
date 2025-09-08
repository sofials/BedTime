using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    [Header("Scene Loading")]
    [SerializeField] private SceneController _sceneController;
    
    [Header("Video Intro")]
    [SerializeField] private VideoIntroManager videoIntroManager;
    [SerializeField] private bool useVideoIntro = true; // Flag per abilitare/disabilitare il video

    public void Play()
    {
        if (useVideoIntro && videoIntroManager != null)
        {
            // Avvia il video intro che poi caricherà automaticamente la scena
            Debug.Log("[MainMenu] Avvio video intro...");
            videoIntroManager.StartIntro();
        }
        else
        {
            // Carica direttamente la scena (comportamento originale)
            Debug.Log("[MainMenu] Caricamento diretto della scena (no video)...");
            LoadGameScene();
        }
    }
    
    /// <summary>
    /// Carica la scena di gioco direttamente (senza video)
    /// </summary>
    public void LoadGameScene()
    {
        if (_sceneController != null)
        {
            _sceneController.LoadScene("00 - Landing in the Dreamworld");
        }
        else
        {
            // Fallback
            SceneManager.LoadScene("00 - Landing in the Dreamworld");
        }
    }
    
    /// <summary>
    /// Salta il video e carica direttamente la scena
    /// </summary>
    public void SkipVideoAndPlay()
    {
        if (videoIntroManager != null)
        {
            videoIntroManager.SkipVideo();
        }
        else
        {
            LoadGameScene();
        }
    }

    public void Exit()
    {
        Application.Quit();
    }
}