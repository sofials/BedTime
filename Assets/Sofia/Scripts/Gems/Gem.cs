using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class Gem : MonoBehaviour
{
    [Header("Gameplay")]
    [SerializeField] private int gemValue = 10;
    [SerializeField] private float respawnSec = 10f;

    [Header("Audio")]
    [SerializeField] private AudioClip collectSound;
    private AudioSource audioSource;

    [Header("Floating / Rotation")]
    [SerializeField] private float floatAmplitude = 0.3f;
    [SerializeField] private float floatFrequency = 2f;
    [SerializeField] private float rotationSpeed = 30f;

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
        startPos = transform.position;
    }

    private void Update()
    {
        if (_renderers.Length > 0 && _renderers[0].enabled)
        {
            float y = startPos.y + Mathf.Sin(Time.time * floatFrequency) * floatAmplitude;
            transform.position = new Vector3(transform.position.x, y, transform.position.z);
            transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);
        }
    }

    // RIMOSSO: OnTriggerEnter - ora gestito solo da PlayerPowerUp

    public int GetGemValue() => gemValue;

    public void Collect()
    {
        // Controlla se già raccolta per evitare doppie chiamate
        if (!_coll.enabled) return;
        
        // Riproduci audio raccolta
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