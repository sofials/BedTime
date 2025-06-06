using UnityEngine;

public class PlayerAttack : MonoBehaviour
{
    public GameObject attackHitbox;
    public ParticleSystem windEffect;  // effetto aria mossa dallo spin

    private Animator animator;

    // Variabile per ignorare input click per alcuni frame
    private int ignoreFrames = 0;

    private void Start()
    {
        animator = GetComponentInChildren<Animator>();
        DeactivateHitbox();
        StopWindEffect();
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
        PlayWindEffect();
    }

    // Chiamato dall'evento nell'animazione alla fine dell'attacco
    public void DeactivateHitbox()
    {
        attackHitbox.SetActive(false);
        Debug.Log("Hitbox DISATTIVATA");
        StopWindEffect();
    }

    private void PlayWindEffect()
    {
        if (windEffect != null && !windEffect.isPlaying)
        {
            windEffect.Play();
        }
    }

    private void StopWindEffect()
    {
        if (windEffect != null && windEffect.isPlaying)
        {
            windEffect.Stop();
        }
    }
}
