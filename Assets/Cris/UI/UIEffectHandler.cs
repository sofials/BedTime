using UnityEngine;
using UnityEngine.UI;
using System.Collections;

[RequireComponent(typeof(Image))]
public class UIEffectHandler : MonoBehaviour
{
    [Header("Input Icons")]
    public Image keyboardIconImage;   // Immagine per la tastiera
    public Image controllerIconImage; // Immagine per il controller

    private Image iconImage;
    private Vector3 baseScale;
    private Coroutine pulseCoroutineKeyboard;
    private Coroutine pulseCoroutineController;

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
        if (keyboardIconImage != null)
        {
            keyboardIconImage.gameObject.SetActive(!isGamepad);
        }
        if (controllerIconImage != null)
        {
            controllerIconImage.gameObject.SetActive(isGamepad);
        }
    }

    public void PulseIcon()
    {
        // Pulse su entrambe le icone input se presenti
        if (keyboardIconImage != null)
        {
            if (pulseCoroutineKeyboard != null)
                StopCoroutine(pulseCoroutineKeyboard);
            pulseCoroutineKeyboard = StartCoroutine(PulseRoutineOnImage(keyboardIconImage));
        }
        if (controllerIconImage != null)
        {
            if (pulseCoroutineController != null)
                StopCoroutine(pulseCoroutineController);
            pulseCoroutineController = StartCoroutine(PulseRoutineOnImage(controllerIconImage));
        }
    }

    private IEnumerator PulseRoutineOnImage(Image img)
    {
        if (img == null) yield break;
        Vector3 originalScale = img.rectTransform.localScale;
        float duration = 0.2f;
        float maxScale = 1.2f;

        // Scale up
        float elapsed = 0;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            img.rectTransform.localScale = Vector3.Lerp(originalScale, originalScale * maxScale, t);
            yield return null;
        }

        // Scale down
        elapsed = 0;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            img.rectTransform.localScale = Vector3.Lerp(originalScale * maxScale, originalScale, t);
            yield return null;
        }

        img.rectTransform.localScale = originalScale;
    }
}
