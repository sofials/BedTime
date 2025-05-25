using UnityEngine;

public class PlayerAttack : MonoBehaviour
{
    public GameObject attackHitbox;
    public ParticleSystem windEffect;  // effetto aria mossa dallo spin

    private Animator animator;

    private void Start()
    {
        animator = GetComponent<Animator>();
        DeactivateHitbox();
        StopWindEffect();
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            animator.SetTrigger("Attack");
        }
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
