using UnityEngine;

public class DreamWave : MonoBehaviour
{
    [Header("NPC da distruggere (array)")]
    public GameObject[] npcsToDestroy;

    [Header("Oggetto da attivare")]
    public GameObject objectToActivate;

    [Header("Posizione di attivazione")]
    public Transform activationPoint;

    private bool triggered = false;
    public bool IsTriggered => triggered;

    private void Start()
    {
        var mesh = GetComponent<MeshRenderer>();
        if (mesh != null)
            mesh.enabled = false;

        if (objectToActivate != null)
            objectToActivate.SetActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (triggered) return;

        if (other.CompareTag("Player"))
        {
            Debug.Log("Player entrato nel trigger!");
            triggered = true;

            if (objectToActivate != null && activationPoint != null)
            {
                objectToActivate.transform.position = activationPoint.position;
                objectToActivate.transform.rotation = activationPoint.rotation;
                objectToActivate.SetActive(true);
            }

            // 🔥 Distruggi tutti gli NPC specificati
            foreach (var npc in npcsToDestroy)
            {
                if (npc != null)
                {
                    Destroy(npc);
                    Debug.Log($"Distrutto NPC: {npc.name}");
                }
            }
        }
    }
}
