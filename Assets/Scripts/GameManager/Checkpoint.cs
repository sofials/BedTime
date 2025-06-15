using UnityEngine;

public class Checkpoint : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        // Controlla se l'oggetto che ha attivato il trigger è il player
        ThirdPersonController player = other.GetComponent<ThirdPersonController>();
        if (player != null)
        {
            // Aggiorna il checkpoint nel GameManager
            GameManager.Instance.SetCheckpoint(transform);
            Debug.Log("Checkpoint raggiunto: " + name);
        }
    }
}
