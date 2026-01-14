using UnityEngine;

public class DrawRevealEffect : MonoBehaviour
{
    [Tooltip("Durata dell'animazione di disegno in secondi")]
    public float drawDuration = 2f;

    [Tooltip("Se true, l'animazione NON parte automaticamente allo Start (utile per switch runtime)")]
    public bool skipAutoStart = false;

    private Material material;
    private float timer = 0f;
    private bool isDrawing = false;

    private AudioSource audioSource;

    void Start()
    {
        Renderer rend = GetComponent<Renderer>();
        if (rend == null)
        {
            Debug.LogError("DrawRevealEffect: Renderer non trovato!");
            enabled = false;
            return;
        }

        material = rend.material;

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            Debug.LogWarning("DrawRevealEffect: AudioSource non trovato!");
        }

        // Avvia l'animazione solo se non è stato richiesto di saltarla
        if (!skipAutoStart)
        {
            ResetDraw();
        }
        else
        {
            // Se skipAutoStart è true, completa istantaneamente
            CompleteInstantly();
        }
    }

    void Update()
    {
        if (!isDrawing) return;

        timer += Time.deltaTime;
        float progress = Mathf.Clamp01(timer / drawDuration);
        material.SetFloat("_RevealProgress", progress);

        if (progress >= 1f)
        {
            isDrawing = false;

            if (audioSource != null && audioSource.isPlaying)
            {
                audioSource.Stop();
            }
        }
    }

    /// <summary>
    /// Resetta e fa partire l'animazione di disegno da capo.
    /// </summary>
    public void ResetDraw()
    {
        timer = 0f;
        isDrawing = true;
        if (material != null)
            material.SetFloat("_RevealProgress", 0f);

        if (audioSource != null)
        {
            audioSource.Stop();
        }
    }

    /// <summary>
    /// Fa partire il loop audio del disegno.
    /// </summary>
    public void PlayLoopAudio()
    {
        if (audioSource != null && !audioSource.isPlaying)
        {
            audioSource.Play();
        }
    }

    /// <summary>
    /// Completa istantaneamente l'animazione di disegno (salta alla fine).
    /// </summary>
    public void CompleteInstantly()
    {
        timer = drawDuration;
        isDrawing = false;
        if (material != null)
            material.SetFloat("_RevealProgress", 1f);

        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
        }
    }
}
