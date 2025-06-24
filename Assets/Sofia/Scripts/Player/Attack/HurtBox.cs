using UnityEngine;

public class Hurtbox : MonoBehaviour
{
    public ThirdPersonController playerController;

    private void Reset()
    {
        playerController = GetComponentInParent<ThirdPersonController>();
    }

    // Questo viene chiamato direttamente da Enemy.OnAttackHit()
    public void OnHit(Vector3 pushDirection, float force)
    {
        if (playerController == null) return;

        playerController.Stun(0.5f); // o parametro personalizzato se vuoi
        playerController.ApplyExternalPush(pushDirection * force);
        playerController.PlayHitAnimation();
    }
}
