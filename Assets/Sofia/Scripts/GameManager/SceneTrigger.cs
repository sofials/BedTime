using UnityEngine;

public class SceneTrigger : MonoBehaviour
{
    [SerializeField]
    private string sceneToLoad = "01 - Party in Lukelandia";
    
    [SerializeField]
    private string playerTag = "Player";
    
    [SerializeField]
    private SceneController _sceneController;
    
    private bool _isLoading = false;
    
    private void Start()
    {
        
        // Assicurati che il collider sia impostato come trigger
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
        else
        {
            Debug.LogWarning("Nessun Collider trovato su " + gameObject.name);
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        // Controlla se l'oggetto che entra nel trigger è il player e se non stiamo già caricando
        if (other.CompareTag(playerTag) && !_isLoading)
        {
            Debug.Log("Player entrato nel trigger, caricamento scena: " + sceneToLoad);
            
            if (_sceneController != null)
            {
                _isLoading = true;
                _sceneController.LoadScene(sceneToLoad);
            }
            else
            {
                Debug.LogError("SceneController non è assegnato nell'Inspector!");
            }
        }
    }
}