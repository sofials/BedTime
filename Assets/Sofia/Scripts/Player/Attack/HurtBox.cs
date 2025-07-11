using UnityEngine;

public class HurtBox : MonoBehaviour
{
    public ThirdPersonController playerController;

    private void Awake()
    {
        if (playerController == null)
            playerController = GetComponentInParent<ThirdPersonController>();
    }

    public void OnHit(Vector3 push, float force, float damage = 0f)
    {
        if (playerController == null) return;

        // 1) Applica la spinta esterna
        playerController.ApplyExternalPush(push * force);

        // 2) Applica eventualmente il danno (con gestione animazioni dentro TakeDamage)
        if (damage > 0f)
            playerController.TakeDamage(damage);
    }
}
