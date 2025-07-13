using UnityEngine;
using TMPro;  // TextMeshPro namespace
using System.Collections;

public class Dialog_verde : MonoBehaviour
{
    public AudioSource audioSource;
    public TextMeshProUGUI subtitleText;

    [TextArea]
    public string dialogText;

    public float displayDuration = 5f;

    public IEnumerator PlayDialog()
    {
        subtitleText.text = dialogText;
        subtitleText.enabled = true;

        audioSource.Play();

        yield return new WaitForSeconds(displayDuration);

        subtitleText.enabled = false;
    }
}
