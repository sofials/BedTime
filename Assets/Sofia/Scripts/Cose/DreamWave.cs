using UnityEngine;

public class DreamWave : MonoBehaviour
{
    [Header("Mesh da nascondere")]
    public GameObject targetObject;
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

            if (targetObject != null)
            {
                MeshRenderer[] meshes = targetObject.GetComponentsInChildren<MeshRenderer>();
                foreach (var m in meshes)
                {
                    m.enabled = false;
                    Debug.Log("Mesh disattivata: " + m.gameObject.name);
                }
            }
        }
    }
}
