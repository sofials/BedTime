using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class UIEffectHandler : MonoBehaviour
{
    private Image image;
    private Coroutine pulseCoroutine;

    private void Awake()
    {
        image = GetComponent<Image>();
    }

    public void PulseIcon()
    {
        if (!gameObject.activeInHierarchy) return;

        if (pulseCoroutine != null)
            StopCoroutine(pulseCoroutine);

        pulseCoroutine = StartCoroutine(PulseRoutine());
    }

    private IEnumerator PulseRoutine()
    {
        float duration = 0.3f;
        float time = 0f;
        Vector3 originalScale = transform.localScale;
        Vector3 targetScale = originalScale * 1.2f;

        while (time < duration)
        {
            time += Time.deltaTime;
            transform.localScale = Vector3.Lerp(originalScale, targetScale, time / duration);
            yield return null;
        }

        time = 0f;
        while (time < duration)
        {
            time += Time.deltaTime;
            transform.localScale = Vector3.Lerp(targetScale, originalScale, time / duration);
            yield return null;
        }

        transform.localScale = originalScale;
    }
    public void SetGrayscale(bool isGray)
    {
        if (image != null)
        {
            image.color = isGray ? Color.gray : Color.white;
        }
    }

}
