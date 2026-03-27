using UnityEngine;
using UnityEngine.Rendering;
using System.Collections;

[RequireComponent(typeof(Renderer))]
[RequireComponent(typeof(Collider))]
public class LifeGem : MonoBehaviour
{
    /* ───────────────── Gameplay ───────────────── */
    [Header("Gameplay")]
    [SerializeField] private float healAmount      = 25f;

    /* ───────────────── Audio ──────────────────── */
    [Header("Audio")]
    [SerializeField] private AudioClip collectSound;
    private AudioSource audioSource;

    /* ───────────────── Appearance ─────────────── */
    [Header("Material & Fade‑In")]
    [SerializeField] private Color baseColor       = Color.green;
    [SerializeField] private Color emissionColor   = Color.green;
    [SerializeField] private float emissionInt     = 2f;
    [SerializeField] private float fadeDuration    = 1.5f;

    /* ───────────────── Floating ───────────────── */
    [Header("Floating / Rotation")]
    [SerializeField] private float floatAmplitude  = 1f;
    [SerializeField] private float floatFrequency  = 1.5f;
    [SerializeField] private float rotationSpeed   = 50f;

    /* ───────────────── Light ──────────────────── */
    [Header("Point‑Light")]
    [SerializeField] private float lightRange      = 6f;
    [SerializeField] private float lightIntensity  = 3f;

    /* ───────────────── Internals ──────────────── */
    private Vector3 startPos;
    private Material mat;

    /* ───────────────── Awake ──────────────────── */
    private void Awake()
    {
        /* Collider trigger */
        Collider coll = GetComponent<Collider>();
        coll.isTrigger = true;

        /* AudioSource setup */
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }

        /* Material setup (URP Lit → Transparent) */
        mat = GetComponent<Renderer>().material;

        mat.SetFloat("_Surface", 1f);                                   // 0 Opaque ‑> 1 Transparent
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.SetInt("_SrcBlend",  (int)BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend",  (int)BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword ("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)RenderQueue.Transparent;

        /* Base‑color alpha 0 (inizio fade) */
        Color c0 = baseColor;  c0.a = 0f;
        mat.SetColor("_BaseColor", c0);

        /* Emission */
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", emissionColor * emissionInt);

        /* Point‑light green */
        GameObject lightGO = new GameObject("GemLight");
        lightGO.transform.SetParent(transform);
        lightGO.transform.localPosition = Vector3.zero;

        Light p = lightGO.AddComponent<Light>();
        p.type       = LightType.Point;
        p.color      = Color.green;
        p.range      = lightRange;
        p.intensity  = lightIntensity;
        p.shadows    = LightShadows.None;
    }

    /* ───────────────── Start ──────────────────── */
    private void Start()
    {
        startPos = transform.position + new Vector3(0, 0.5f, 0);
        StartCoroutine(FadeIn());
    }

    /* ───────────────── Update ─────────────────── */
    private void Update()
    {
        // Galleggiamento
        float y = startPos.y + Mathf.Sin(Time.time * floatFrequency) * floatAmplitude;
        transform.position = new Vector3(transform.position.x, y, transform.position.z);

        // Rotazione
        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);
    }

    /* ───────────────── Fade Coroutine ─────────── */
    private IEnumerator FadeIn()
    {
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            float a = Mathf.Clamp01(t / fadeDuration);
            Color c = baseColor; c.a = a;
            mat.SetColor("_BaseColor", c);
            yield return null;
        }
        // assicura alpha 1
        Color f = baseColor; f.a = 1f;
        mat.SetColor("_BaseColor", f);
    }

    /* ───────────────── Trigger ────────────────── */
    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        ThirdPersonController player = other.GetComponentInParent<ThirdPersonController>();
        if (player != null) player.Heal(healAmount);

        StartCoroutine(CollectSequence());
    }

    /* ───────────────── Collect Sequence ──────── */
    private IEnumerator CollectSequence()
    {
        // Disabilita immediatamente collider per evitare doppie raccolte
        GetComponent<Collider>().enabled = false;
        
        // Nascondi TUTTI gli effetti visivi sincronizzati
        Renderer[] allRenderers = GetComponentsInChildren<Renderer>(includeInactive: false);
        foreach (var renderer in allRenderers)
        {
            renderer.enabled = false;
        }
        
        Light[] allLights = GetComponentsInChildren<Light>(includeInactive: false);
        foreach (var light in allLights)
        {
            light.enabled = false;
        }
        
        // Disabilita anche eventuali particle systems
        ParticleSystem[] allParticles = GetComponentsInChildren<ParticleSystem>(includeInactive: false);
        foreach (var particles in allParticles)
        {
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        // SUONA ESATTAMENTE QUANDO TUTTO SPARISCE
        if (collectSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(collectSound, 0.1f);
            // Aspetta che il suono finisca prima di distruggere
            yield return new WaitForSeconds(collectSound.length);
        }
        
        Destroy(gameObject);
    }
}