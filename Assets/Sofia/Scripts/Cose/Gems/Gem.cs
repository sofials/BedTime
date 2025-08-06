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

    /* ───────────────── Floating ───────────────── */
    [Header("Floating / Rotation")]
    [SerializeField] private float floatAmplitude = 0.3f;  // Più contenuto delle LifeGem
    [SerializeField] private float floatFrequency = 2f;    // Leggermente più veloce
    [SerializeField] private float rotationSpeed = 30f;   // Rotazione più lenta

    private Collider _coll;
    private Renderer[] _renderers;
    private Vector3 startPos;

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

    private void Start()
    {
        // Salva la posizione iniziale per il galleggiamento
        startPos = transform.position;
    }

    private void Update()
    {
        // Solo se la gem è visibile (non durante il respawn)
        if (_renderers.Length > 0 && _renderers[0].enabled)
        {
            // Galleggiamento
            float y = startPos.y + Mathf.Sin(Time.time * floatFrequency) * floatAmplitude;
            transform.position = new Vector3(transform.position.x, y, transform.position.z);

            // Rotazione
            transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);
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
        // Riproduci audio raccolta con volume ridotto al 30%
        if (collectSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(collectSound, 0.1f);
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