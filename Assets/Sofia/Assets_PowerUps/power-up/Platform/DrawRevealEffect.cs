using UnityEngine;

public class DrawRevealEffect : MonoBehaviour
{
    [Tooltip("Durata dell'animazione di disegno in secondi")]
    public float drawDuration = 2f;

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

        ResetDraw();
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
}
