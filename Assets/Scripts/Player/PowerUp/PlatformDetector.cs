using UnityEngine;

public class PlatformDetector : MonoBehaviour
{
    public PlatformAbility platformAbility;  // riferimento allo script PlatformAbility

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Platform"))
        {
            platformAbility.playerPlatform = other.transform;
            Debug.Log("Player su piattaforma: " + other.name);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Platform"))
        {
            if (platformAbility.playerPlatform == other.transform)
            {
                platformAbility.playerPlatform = null;
                Debug.Log("Player ha lasciato la piattaforma");
            }
        }
    }
}
