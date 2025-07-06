using UnityEngine;
using CartoonFX;

public class DeathEffectController : MonoBehaviour
{
    private CFXR_Effect effect;
    private ParticleSystem myParticleSystem;

    private void Awake()
    {
        effect = GetComponent<CFXR_Effect>();
        myParticleSystem = effect.GetComponent<ParticleSystem>();

        if (myParticleSystem != null)
        {
            myParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            effect.ResetState();
            gameObject.SetActive(false);
        }
        else
        {
            Debug.LogWarning("ParticleSystem component not found on effect.");
        }
    }

    // Attiva e fa partire l'effetto di morte
    public void PlayDeathEffect()
    {
        if (effect != null && myParticleSystem != null)
        {
            gameObject.SetActive(true);
            effect.ResetState();
            myParticleSystem.Play();
        }
    }

    // Ferma l'effetto e disattiva il GameObject
    public void StopDeathEffect()
    {
        if (effect != null && myParticleSystem != null)
        {
            myParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            gameObject.SetActive(false);
        }
    }
}
