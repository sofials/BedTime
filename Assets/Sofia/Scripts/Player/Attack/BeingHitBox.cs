using UnityEngine;

public class BeingHitBox : MonoBehaviour
{
    public float pushForce = 50f;

    private void OnTriggerEnter(Collider other)
    {
        ThirdPersonController playerController = other.GetComponentInParent<ThirdPersonController>();
        EnemyAI enemy = GetComponentInParent<EnemyAI>();

        // Attiva solo se il nemico è in attacco
        if (playerController != null && enemy != null && enemy.isAttacking)
        {
            playerController.PlayHitAnimation();

            Vector3 pushDir = (playerController.transform.position - transform.position).normalized;
            playerController.ApplyExternalPush(pushDir * pushForce);
            playerController.Stun(1.0f); // stordisce per 1 secondo
        }
    }
}
