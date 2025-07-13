using UnityEngine;
using TMPro;
using System.Collections;

public class Dialog_verde : MonoBehaviour
{
    public AudioSource audioSource;

    [Header("Sottotitoli assegnati da Inspector")]
    public TextMeshProUGUI[] subtitleTexts; // Assegna i 4 oggetti con testo già scritto

    public float[] displayDurations; // Durate per ciascuno

    void Start()
    {
        foreach (var txt in subtitleTexts)
        {
            if (txt != null)
                txt.enabled = false;
        }
    }

    public IEnumerator PlayDialog(int index)
    {
        if (index < 0 || index >= subtitleTexts.Length)
        {
            Debug.LogWarning("Indice sottotitolo non valido.");
            yield break;
        }

        TextMeshProUGUI currentSubtitle = subtitleTexts[index];
        if (currentSubtitle != null)
        {
            currentSubtitle.enabled = true;
        }

        if (audioSource != null && audioSource.clip != null)
            audioSource.Play();

        float duration = (displayDurations != null && index < displayDurations.Length)
            ? displayDurations[index]
            : 5f;

        yield return new WaitForSeconds(duration);

        if (currentSubtitle != null)
            currentSubtitle.enabled = false;
    }
}
