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

    // ✔ 1) Applica la spinta esterna
    playerController.ApplyExternalPush(push * force);

    // ✔ 2) Verifica se il player è nel mezzo di un attacco
    PlayerAttack playerAttack = playerController.GetComponentInChildren<PlayerAttack>();
    bool isSwinging = playerAttack != null && playerAttack.isAttacking;

    // ✔ 3) Se NON sta colpendo, mostra animazione "Hit"
    if (!isSwinging)
    {
        Animator anim = playerController.GetComponentInChildren<Animator>();
        if (anim != null)
            anim.SetTrigger("Hit");
    }

    // ✔ 4) Applica eventualmente il danno
    if (damage > 0f)
        playerController.TakeDamage(damage);
}


}