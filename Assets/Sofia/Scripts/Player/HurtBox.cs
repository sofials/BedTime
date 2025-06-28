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
        if (playerController != null)
        {
            playerController.ApplyExternalPush(push * force);

            // Trigger animazione "Hit"
            Animator anim = playerController.GetComponentInChildren<Animator>();
            if (anim != null)
                anim.SetTrigger("Hit");

            // Applica danno
            if (damage > 0f)
                playerController.TakeDamage(damage);
        }
    }
}