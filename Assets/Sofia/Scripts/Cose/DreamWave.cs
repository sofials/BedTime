using UnityEngine;

public class DreamWave : MonoBehaviour
{
    [Header("Oggetti da nascondere (array)")]
    public GameObject[] objectsToHide;

    [Header("Oggetto da spawnare")]
    public GameObject objectToSpawn;

    [Header("Posizione di spawn")]
    public Transform spawnPoint;

    private bool triggered = false;

    private void Start()
    {
        // Nasconde la mesh del trigger (se presente)
        var mesh = GetComponent<MeshRenderer>();
        if (mesh != null)
            mesh.enabled = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (triggered) return;

        if (other.CompareTag("Player"))
        {
            Debug.Log("Player entrato nel trigger!");

            triggered = true;

            // Nasconde tutti gli oggetti nell'array
            foreach (var obj in objectsToHide)
            {
                if (obj == null) continue;

                // Disattiva tutti i MeshRenderer dei figli e dell'oggetto stesso
                MeshRenderer[] meshes = obj.GetComponentsInChildren<MeshRenderer>();
                foreach (var m in meshes)
                {
                    m.enabled = false;
                    Debug.Log("Mesh disattivata: " + m.gameObject.name);
                }

                // Disattiva tutti i Collider dei figli e dell'oggetto stesso
                Collider[] colliders = obj.GetComponentsInChildren<Collider>();
                foreach (var c in colliders)
                {
                    c.enabled = false;
                    Debug.Log("Collider disattivato: " + c.gameObject.name);
                }
            }

            // Fa spawnare l'oggetto specificato nella posizione data
            if (objectToSpawn != null && spawnPoint != null)
            {
                Instantiate(objectToSpawn, spawnPoint.position, spawnPoint.rotation);
                Debug.Log("Oggetto spawnato: " + objectToSpawn.name);
            }
        }
    }
}
