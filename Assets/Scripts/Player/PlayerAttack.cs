using UnityEngine;

public class PlayerAttack : MonoBehaviour
{
    public GameObject attackHitbox;
    private Animator animator;

    private void Start()
    {
        animator = GetComponent<Animator>();
        DeactivateHitbox();
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            animator.SetTrigger("Attack");
        }
    }

    // Chiamato dall'evento nell'animazione
    public void ActivateHitbox()
    {
        attackHitbox.SetActive(true);
        Debug.Log("Hitbox ATTIVA");
    }

    public void DeactivateHitbox()
    {
        attackHitbox.SetActive(false);
        Debug.Log("Hitbox DISATTIVATA");
    }
}
