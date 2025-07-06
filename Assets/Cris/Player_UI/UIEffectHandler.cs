using UnityEngine;
using UnityEngine.UI;
using System.Collections;

[RequireComponent(typeof(Image))]
public class UIEffectHandler : MonoBehaviour
{
    [Header("Pulse")]
    [SerializeField] float pulseFactor  = 1.2f;
    [SerializeField] float pulseTime    = 0.3f;
    [SerializeField] int   pulseLoops   = 1;

    Image image;
    Coroutine pulseCoroutine;
    Vector3 baseScale;

    void Awake()
    {
        image     = GetComponent<Image>();
        baseScale = transform.localScale;
    }

    void OnEnable()       => transform.localScale = baseScale;
    void OnDisable()      => StopPulse();

    /* =========== PUBLIC API =========== */

    public void PulseIcon()
    {
        // se non è attivo oppure già in corso: esco
        if (!isActiveAndEnabled) return;

        StopPulse();
        pulseCoroutine = StartCoroutine(PulseRoutine());
    }

    public void SetGrayscale(bool gray)
    {
        if (image) image.color = gray ? Color.gray : Color.white;
    }

    /* =========== PRIVATE ============== */

    void StopPulse()
    {
        if (pulseCoroutine != null)
        {
            StopCoroutine(pulseCoroutine);
            pulseCoroutine = null;
            transform.localScale = baseScale;
        }
    }

    IEnumerator PulseRoutine()
    {
        Vector3 target = baseScale * pulseFactor;
        float   half   = pulseTime * 0.5f;

        for (int loop = 0; loop < pulseLoops; loop++)
        {
            // zoom‑in
            float t = 0f;
            while (t < half)
            {
                t += Time.unscaledDeltaTime;
                transform.localScale = Vector3.Lerp(baseScale, target, t / half);
                yield return null;
            }

            // zoom‑out
            t = 0f;
            while (t < half)
            {
                t += Time.unscaledDeltaTime;
                transform.localScale = Vector3.Lerp(target, baseScale, t / half);
                yield return null;
            }
        }

        transform.localScale = baseScale;
        pulseCoroutine = null;
    }
}
