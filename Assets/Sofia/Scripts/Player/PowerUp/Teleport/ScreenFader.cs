using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class ScreenFader : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private Image fadeImage;
    [SerializeField] private Color fadeColor = Color.black;
    
    private Canvas fadeCanvas;
    
    private void Awake()
    {
        // Auto-setup se non configurato
        if (fadeImage == null)
        {
            SetupFadeCanvas();
        }
        
        // Inizia trasparente
        SetAlpha(0f);
    }
    
    private void SetupFadeCanvas()
    {
        // Crea Canvas
        fadeCanvas = gameObject.GetComponent<Canvas>();
        if (fadeCanvas == null)
        {
            fadeCanvas = gameObject.AddComponent<Canvas>();
        }
        fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        fadeCanvas.sortingOrder = 999; // Sempre sopra tutto
        
        // Aggiungi CanvasScaler
        if (GetComponent<CanvasScaler>() == null)
        {
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
        }
        
        // Crea Image per il fade
        GameObject imageGO = new GameObject("FadeImage");
        imageGO.transform.SetParent(transform, false);
        
        fadeImage = imageGO.AddComponent<Image>();
        fadeImage.color = fadeColor;
        fadeImage.raycastTarget = false; // Non blocca i click
        
        // Stretcha per coprire tutto lo schermo
        RectTransform rt = fadeImage.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
    
    /// <summary>
    /// Fade verso il nero (o colore configurato)
    /// </summary>
    public Coroutine FadeOut(float duration)
    {
        return StartCoroutine(FadeRoutine(0f, 1f, duration));
    }
    
    /// <summary>
    /// Fade dal nero verso trasparente
    /// </summary>
    public Coroutine FadeIn(float duration)
    {
        return StartCoroutine(FadeRoutine(1f, 0f, duration));
    }
    
    /// <summary>
    /// Fade completo: out, attendi, poi in
    /// </summary>
    public Coroutine FadeOutIn(float fadeOutDuration, float holdDuration, float fadeInDuration)
    {
        return StartCoroutine(FadeOutInRoutine(fadeOutDuration, holdDuration, fadeInDuration));
    }
    
    private IEnumerator FadeRoutine(float startAlpha, float endAlpha, float duration)
    {
        if (fadeImage == null) yield break;
        
        fadeImage.gameObject.SetActive(true);
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime; // Usa unscaled per funzionare anche in pausa
            float t = Mathf.Clamp01(elapsed / duration);
            float alpha = Mathf.Lerp(startAlpha, endAlpha, t);
            SetAlpha(alpha);
            yield return null;
        }
        
        SetAlpha(endAlpha);
        
        // Disattiva se completamente trasparente
        if (endAlpha <= 0f)
        {
            fadeImage.gameObject.SetActive(false);
        }
    }
    
    private IEnumerator FadeOutInRoutine(float fadeOutDuration, float holdDuration, float fadeInDuration)
    {
        yield return FadeRoutine(0f, 1f, fadeOutDuration);
        yield return new WaitForSecondsRealtime(holdDuration);
        yield return FadeRoutine(1f, 0f, fadeInDuration);
    }
    
    private void SetAlpha(float alpha)
    {
        if (fadeImage != null)
        {
            Color c = fadeColor;
            c.a = alpha;
            fadeImage.color = c;
        }
    }
    
    /// <summary>
    /// Imposta immediatamente lo schermo nero
    /// </summary>
    public void SetBlack()
    {
        if (fadeImage != null)
        {
            fadeImage.gameObject.SetActive(true);
            SetAlpha(1f);
        }
    }
    
    /// <summary>
    /// Imposta immediatamente lo schermo trasparente
    /// </summary>
    public void SetClear()
    {
        if (fadeImage != null)
        {
            SetAlpha(0f);
            fadeImage.gameObject.SetActive(false);
        }
    }
}