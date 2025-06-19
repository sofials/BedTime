using UnityEngine;

public class PlayerAttack : MonoBehaviour
{
    public GameObject attackHitbox;

    private Animator animator;

    // Variabile per ignorare input click per alcuni frame
    private int ignoreFrames = 0;

    private void Start()
    {
        animator = GetComponentInChildren<Animator>();
        DeactivateHitbox();
    }

    private void Update()
    {
        if (ignoreFrames > 0)
        {
            ignoreFrames--;
            return;  // ignora input finché ignoreFrames > 0
        }

        if (Time.timeScale == 0f)
        {
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            animator.SetTrigger("Attack");
        }
    }

    // Metodo da chiamare per ignorare i prossimi click (es. dopo il resume)
    public void IgnoreNextClick()
    {
        ignoreFrames = 2;  // ignora i prossimi 2 frame di click
    }

    // Chiamato dall'evento nell'animazione all'inizio dell'attacco
    public void ActivateHitbox()
    {
        attackHitbox.SetActive(true);
        Debug.Log("Hitbox ATTIVA");
    }

    // Chiamato dall'evento nell'animazione alla fine dell'attacco
    public void DeactivateHitbox()
    {
        attackHitbox.SetActive(false);
        Debug.Log("Hitbox DISATTIVATA");
    }
}
