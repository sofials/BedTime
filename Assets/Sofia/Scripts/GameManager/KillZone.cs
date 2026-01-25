using UnityEngine;

public class KillZone : MonoBehaviour
{
    // ⭐ Flag per evitare respawn multipli
    private bool isRespawning = false;
    
    [SerializeField] private float respawnCooldown = 1f; // Cooldown tra respawn

    private void OnTriggerEnter(Collider other)
    {
        // ⭐ Usa OnTriggerEnter invece di OnTriggerStay per triggerare solo una volta
        if (isRespawning) return;
        
        ThirdPersonController player = other.GetComponent<ThirdPersonController>();
        if (player != null)
        {
            isRespawning = true;
            
            // ⭐ Cancella esplicitamente il ghost PRIMA del respawn
            // NOTA: PlatformSpawnerForwardAbility è figlio dello SceneManager, non del player
            PlatformSpawnerForwardAbility platformAbility = FindFirstObjectByType<PlatformSpawnerForwardAbility>();
            if (platformAbility != null)
            {
                platformAbility.CancelPlacement();
                Debug.Log("[KillZone] ✅ CancelPlacement chiamato");
            }
            else
            {
                Debug.LogWarning("[KillZone] ⚠️ PlatformSpawnerForwardAbility non trovato nella scena!");
            }
            
            player.Respawn();
            Debug.Log("[KillZone] Player morto, respawn attivato.");
            
            // Reset del flag dopo un delay
            Invoke(nameof(ResetRespawnFlag), respawnCooldown);
        }
    }
    
    private void ResetRespawnFlag()
    {
        isRespawning = false;
    }
}