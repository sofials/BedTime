using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;

[RequireComponent(typeof(Image))]
public class UIEffectHandler : MonoBehaviour
{
    [Header("Input Text")]
    public TextMeshProUGUI inputText;
    public string keyboardText;
    public string controllerText;

    private Image iconImage;
    private Vector3 baseScale;
    private Coroutine pulseCoroutine;

    void Awake()
    {
        iconImage = GetComponent<Image>();
        baseScale = transform.localScale;
    }

    public void SetGrayscale(bool grayscale)
    {
        if (iconImage != null)
        {
            iconImage.color = grayscale ? Color.gray : Color.white;
        }
    }

    public void UpdateInputText(bool isGamepad)
    {
        if (inputText != null)
        {
            inputText.text = isGamepad ? controllerText : keyboardText;
        }
    }

    public void PulseIcon()
    {
        if (pulseCoroutine != null)
        {
            StopCoroutine(pulseCoroutine);
        }
        pulseCoroutine = StartCoroutine(PulseRoutine());
    }

    private void StopPulse()
    {
        if (pulseCoroutine != null)
        {
            StopCoroutine(pulseCoroutine);
            pulseCoroutine = null;
        }
        transform.localScale = baseScale;
    }

    private IEnumerator PulseRoutine()
    {
        float duration = 0.2f;
        float maxScale = 1.2f;
        
        // Scale up
        float elapsed = 0;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            transform.localScale = Vector3.Lerp(baseScale, baseScale * maxScale, t);
            yield return null;
        }
        
        // Scale down
        elapsed = 0;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            transform.localScale = Vector3.Lerp(baseScale * maxScale, baseScale, t);
            yield return null;
        }
        
        transform.localScale = baseScale;
        pulseCoroutine = null;
    }
}
