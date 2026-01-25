using UnityEngine;
using System.Collections;

public class CreditsScroller : MonoBehaviour
{
    [Header("Scroll Settings")]
    [SerializeField] private float scrollSpeed = 50f;
    [Tooltip("Valore Top iniziale (come mostrato nell'Inspector)")]
    [SerializeField] private float startTop = 455.97f;
    [Tooltip("Valore Top finale (negativo = testo sale oltre lo schermo)")]
    [SerializeField] private float endTop = -2000f;

    [Header("Delay")]
    [Tooltip("Delay aggiuntivo dopo il fade-in prima di iniziare lo scroll")]
    [SerializeField] private float startDelay = 0.5f;

    [Header("Debug")]
    [SerializeField] private bool debugScroll = true;
    [SerializeField] private bool autoStartOnPlay = false;

    private RectTransform rectTransform;
    private bool isScrolling = false;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        if (rectTransform == null)
        {
            Debug.LogError("[CreditsScroller] RectTransform non trovato!");
        }
    }

    private void Start()
    {
        if (autoStartOnPlay)
            StartScrolling();
    }

    /// <summary>
    /// Chiamato da SceneController (o UnityEvent) dopo il fade-in
    /// </summary>
    public void StartScrolling()
    {
        if (debugScroll)
            Debug.Log("[CreditsScroller] StartScrolling chiamato!");

        if (!isScrolling)
            StartCoroutine(ScrollCoroutine());
    }

    private IEnumerator ScrollCoroutine()
    {
        isScrolling = true;

        // Aspetta un frame per assicurarsi che il layout UI sia calcolato
        yield return null;

        // Imposta posizione iniziale
        SetTopValue(startTop);

        if (debugScroll)
            Debug.Log($"[CreditsScroller] Coroutine avviata. Start Top: {startTop}, End Top: {endTop}");

        // Delay iniziale
        if (startDelay > 0)
            yield return new WaitForSeconds(startDelay);

        float currentTop = startTop;

        // Scroll verso l'alto (Top diminuisce, diventa negativo)
        while (currentTop > endTop)
        {
            currentTop -= scrollSpeed * Time.deltaTime;
            SetTopValue(currentTop);
            yield return null;
        }

        // Assicura posizione finale esatta
        SetTopValue(endTop);
        isScrolling = false;

        Debug.Log("[CreditsScroller] Scroll completato!");
    }

    /// <summary>
    /// Imposta il valore "Top" del RectTransform (per modalità stretch)
    /// offsetMax.y = -top
    /// </summary>
    private void SetTopValue(float top)
    {
        Vector2 offsetMax = rectTransform.offsetMax;
        offsetMax.y = -top;
        rectTransform.offsetMax = offsetMax;
    }

    /// <summary>
    /// Legge il valore attuale di Top
    /// </summary>
    private float GetTopValue()
    {
        return -rectTransform.offsetMax.y;
    }

    public void ResetPosition()
    {
        SetTopValue(startTop);
    }

    public void StopScrolling()
    {
        StopAllCoroutines();
        isScrolling = false;
    }

    [ContextMenu("Test - Start Scrolling")]
    public void TestStartScrolling() => StartScrolling();

    [ContextMenu("Test - Reset Position")]
    public void TestResetPosition() => ResetPosition();

    public bool IsScrolling => isScrolling;
}