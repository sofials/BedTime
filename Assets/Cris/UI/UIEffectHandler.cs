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
    
    // Memorizza le scale originali delle icone input
    private Vector3 keyboardOriginalScale;
    private Vector3 controllerOriginalScale;
    
    private Coroutine pulseCoroutineKeyboard;
    private Coroutine pulseCoroutineController;

    void Awake()
    {
        iconImage = GetComponent<Image>();
        baseScale = transform.localScale;
        
        // Memorizza le scale originali delle icone input
        if (keyboardIconImage != null)
            keyboardOriginalScale = keyboardIconImage.rectTransform.localScale;
        if (controllerIconImage != null)
            controllerOriginalScale = controllerIconImage.rectTransform.localScale;
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
            pulseCoroutineKeyboard = StartCoroutine(PulseRoutineOnImage(keyboardIconImage, keyboardOriginalScale));
        }
        if (controllerIconImage != null)
        {
            if (pulseCoroutineController != null)
                StopCoroutine(pulseCoroutineController);
            pulseCoroutineController = StartCoroutine(PulseRoutineOnImage(controllerIconImage, controllerOriginalScale));
        }
    }

    private IEnumerator PulseRoutineOnImage(Image img, Vector3 originalScale)
    {
        if (img == null) yield break;
        
        float duration = 0.2f;
        float maxScale = 1.2f;

        // Forza il reset alla scala originale prima di iniziare
        img.rectTransform.localScale = originalScale;

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

        // Assicurati che alla fine sia esattamente la scala originale
        img.rectTransform.localScale = originalScale;
    }
}