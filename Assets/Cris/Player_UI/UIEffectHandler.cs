using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class UIEffectHandler : MonoBehaviour
{
    public Image icon;
    public float pulseScale = 1.2f;
    public float pulseDuration = 0.2f;

    private Vector3 originalScale;

    void Awake()
    {
        if (icon == null)
            icon = GetComponent<Image>();

        originalScale = icon.rectTransform.localScale;
    }

    public void PulseIcon()
    {
        StopAllCoroutines();
        StartCoroutine(DoPulse());
    }

    private IEnumerator DoPulse()
    {
        RectTransform rt = icon.rectTransform;
        rt.localScale = originalScale * pulseScale;

        float elapsed = 0f;
        while (elapsed < pulseDuration)
        {
            rt.localScale = Vector3.Lerp(rt.localScale, originalScale, elapsed / pulseDuration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        rt.localScale = originalScale;
    }
}
