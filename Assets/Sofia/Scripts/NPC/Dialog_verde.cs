using UnityEngine;
using TMPro;
using System.Collections;

public class Dialog_verde : MonoBehaviour
{
    public AudioSource audioSource;

    [Header("Sottotitoli assegnati da Inspector")]
    public TextMeshProUGUI[] subtitleTexts;
    public float[] displayDurations;

    [Header("NPC da notificare")]
    public MonoBehaviour[] npcVillagers; // Permette di supportare entrambi i tipi di NPC

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

        var currentSubtitle = subtitleTexts[index];
        if (currentSubtitle != null)
            currentSubtitle.enabled = true;

        if (audioSource != null && audioSource.clip != null)
            audioSource.Play();

        float duration = (displayDurations != null && index < displayDurations.Length) ? displayDurations[index] : 5f;
        yield return new WaitForSeconds(duration);

        if (currentSubtitle != null)
            currentSubtitle.enabled = false;
    }

    public IEnumerator PlayEntireDialog()
    {
        for (int i = 0; i < subtitleTexts.Length; i++)
            yield return StartCoroutine(PlayDialog(i));

        StopAllAudioSources();

        foreach (var npc in npcVillagers)
        {
            if (npc != null)
            {
                var method = npc.GetType().GetMethod("OnDialogFinished");
                if (method != null)
                    method.Invoke(npc, null);
            }
        }
    }

    private void StopAllAudioSources()
    {
        foreach (AudioSource audio in GetComponentsInChildren<AudioSource>())
        {
            if (audio.isPlaying)
                audio.Stop();
        }
    }
}
