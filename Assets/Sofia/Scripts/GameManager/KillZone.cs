using UnityEngine;

public class KillZone : MonoBehaviour
{
    private void OnTriggerStay(Collider other)
    {
           ThirdPersonController player = other.GetComponent<ThirdPersonController>();
           if (player != null)
           {
                player.Respawn();
                Debug.Log("Player morto nella KillZone, respawn attivato (OnTriggerStay).");
           }
    }

}
