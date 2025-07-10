using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class Gem : MonoBehaviour
{
    [Header("Gameplay")]
    [SerializeField] private int gemValue = 10;
    [SerializeField] private float respawnSec = 10f;

    [Header("Audio")]
    [SerializeField] private AudioClip collectSound;  // Clip da assegnare in Inspector
    private AudioSource audioSource;

    private Collider _coll;
    private Renderer[] _renderers;

    private void Awake()
    {
        _coll = GetComponent<Collider>();
        _coll.isTrigger = true;
        _renderers = GetComponentsInChildren<Renderer>(includeInactive: true);

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        Collect();
    }

    public int GetGemValue() => gemValue;

    public void Collect()
    {
        // Riproduci audio raccolta
        if (collectSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(collectSound);
        }

        SetActiveVisual(false);
        _coll.enabled = false;

        StartCoroutine(RespawnCoroutine());
    }

    private IEnumerator RespawnCoroutine()
    {
        yield return new WaitForSeconds(respawnSec);

        _coll.enabled = true;
        SetActiveVisual(true);
    }

    private void SetActiveVisual(bool state)
    {
        foreach (var r in _renderers)
            r.enabled = state;
    }
}
