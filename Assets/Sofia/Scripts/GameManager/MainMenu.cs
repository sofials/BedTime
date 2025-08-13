using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    [SerializeField]
    private SceneController _sceneController;

    public void Play()
    {
         _sceneController.LoadScene("00 - Landing in the Dreamworld");
    }
    public void Exit()
    {
        Application.Quit();
    }
}
