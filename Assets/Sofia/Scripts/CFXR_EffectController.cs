using UnityEngine;
using CartoonFX;

[RequireComponent(typeof(CFXR_Effect))]
public class CFXR_EffectController : MonoBehaviour
{
    private CFXR_Effect effect;
    private ParticleSystem ps;

    private void Awake()
    {
        effect = GetComponent<CFXR_Effect>();
        ps = GetComponent<ParticleSystem>();

        if (ps != null)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            effect.ResetState();
            gameObject.SetActive(false);
        }
        else
        {
            Debug.LogWarning("CFXR_EffectController: Nessun ParticleSystem trovato sul GameObject.");
        }
    }

    public void PlayEffect()
    {
        if (effect != null && ps != null)
        {
            gameObject.SetActive(true);
            effect.ResetState();
            ps.Play();
        }
    }

    public void StopEffect()
    {
        if (effect != null && ps != null)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            gameObject.SetActive(false);
        }
    }
}
