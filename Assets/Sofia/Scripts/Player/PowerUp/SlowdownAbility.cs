using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class SlowdownAbility : AbilityBase
{
    [Header("Slowdown Settings")]
    public float slowdownRadius = 50f;
    public float slowdownFactor = 0.5f;
    public float customDuration = 10f;

    [Header("Audio")]
    public AudioClip effectAudioClip;
    private AudioSource effectAudioSource;

    [Header("NPC Village da bloccare")]
    public Npc_village[] npcsToSlow;

    public override int powerCost => 20;
    protected override bool HasFixedDuration => true;

    private struct PlatformData
    {
        public MovingPlatform platform;
        public float originalSpeedMultiplier;
    }

    private List<PlatformData> affectedPlatforms = new();
    private List<RotatingObject> affectedRotators = new();
    private List<TurtleShell> affectedTurtleShells = new();

    private Coroutine deactivateCoroutine;

    protected override void Awake()
    {
        base.Awake();
        duration = customDuration;
        effectIconIndex = 3;

        effectAudioSource = gameObject.AddComponent<AudioSource>();
        effectAudioSource.playOnAwake = false;
        effectAudioSource.clip = effectAudioClip;
    }

    public override void TryActivate()
    {
        if (IsActive)
        {
            Debug.Log("[SlowdownAbility] Abilità già attiva – ignoro attivazione.");
            return;
        }
        base.TryActivate();
    }

    private IEnumerator DeactivateAfterDuration()
    {
        float blinkDuration = 1.5f;

        yield return new WaitForSeconds(duration - blinkDuration);

        foreach (var data in affectedPlatforms)
            if (data.platform != null)
                data.platform.StartBlinkingOverlay(blinkDuration);

        foreach (var ro in affectedRotators)
            if (ro != null)
                ro.StartBlinkingOverlay(blinkDuration);

        foreach (var ts in affectedTurtleShells)
            if (ts != null)
                ts.StartBlinkingOverlay(blinkDuration);

        yield return new WaitForSeconds(blinkDuration);

        Deactivate();
        deactivateCoroutine = null;
    }

    public override void Activate()
    {
        Debug.Log("\n=== [SlowdownAbility] Activate() chiamato ===");

        affectedPlatforms.Clear();
        affectedRotators.Clear();
        affectedTurtleShells.Clear();

        Collider[] colliders = Physics.OverlapSphere(powerUpScript.transform.position, slowdownRadius);
        Debug.Log($"[SlowdownAbility] Collider trovati: {colliders.Length}");

        foreach (Collider col in colliders)
        {
            if (col.CompareTag("MovingPlatform") && col.TryGetComponent(out MovingPlatform mp))
            {
                affectedPlatforms.Add(new PlatformData
                {
                    platform = mp,
                    originalSpeedMultiplier = 1f
                });
                mp.SetSpeedMultiplier(slowdownFactor);
                mp.SetOverlayActive(true);
                mp.PlaySlowdownEffect(1f);
                Debug.Log($"→ MovingPlatform {col.name} rallentata.");
            }
            else if (col.CompareTag("RotatingPlatform") && col.TryGetComponent(out RotatingObject ro))
            {
                affectedRotators.Add(ro);
                ro.SetSpeedMultiplier(slowdownFactor);
                ro.SetOverlayActive(true);
                ro.PlaySlowdownEffect(1f);
                Debug.Log($"→ RotatingPlatform {col.name} rallentata.");
            }
            else if (col.CompareTag("TurtleShellHurtbox"))
            {
                TurtleShell ts = col.GetComponentInParent<TurtleShell>();
                if (ts != null && !affectedTurtleShells.Contains(ts) && !ts.isSlow)
                {
                    ts.slowFactor = slowdownFactor;
                    ts.SetSlow(true);
                    ts.SetOverlayActive(true);
                    ts.PlaySlowdownEffect(1f);
                    ts.activeSlowdownAbility = this;
                    affectedTurtleShells.Add(ts);
                    Debug.Log($"→ TurtleShell {ts.name} rallentata.");
                }
            }
            else if (col.CompareTag("Wall_Village"))
            {
                Debug.Log("[SlowdownAbility] Wall_Village colpito: " + col.name);

                // Blocca gli NPC assegnati da inspector (solo slow = true)
                foreach (Npc_village npc in npcsToSlow)
                {
                    if (npc != null)
                    {
                        npc.SetSlow(true);
                        Debug.Log($"→ NPC {npc.name} bloccato (Slow=true).");
                    }
                }

                // Distruggi il muro e il suo genitore (se esiste)
                Transform wallTransform = col.transform;
                if (wallTransform.parent != null)
                {
                    Destroy(wallTransform.parent.gameObject);
                    Debug.Log($"→ Distrutto parent: {wallTransform.parent.name}");
                }
                else
                {
                    Destroy(wallTransform.gameObject);
                    Debug.Log($"→ Distrutto muro: {wallTransform.name}");
                }
            }
        }

        if (affectedPlatforms.Count == 0 && affectedRotators.Count == 0 && affectedTurtleShells.Count == 0)
        {
            Debug.LogWarning("[SlowdownAbility] Nessun oggetto da rallentare trovato.");
            IsActive = false;
            return;
        }

        if (effectAudioSource != null && effectAudioClip != null)
        {
            effectAudioSource.Play();
            Debug.Log("[SlowdownAbility] Audio effetto slowdown riprodotto.");
        }

        Debug.Log($"[SlowdownAbility] Slowdown attivato su {affectedPlatforms.Count} piattaforme, " +
                  $"{affectedRotators.Count} rotatori, {affectedTurtleShells.Count} TurtleShell.");
        Debug.Log("=== [SlowdownAbility] Fine Activate() ===\n");

        if (HasFixedDuration)
        {
            if (deactivateCoroutine != null)
                StopCoroutine(deactivateCoroutine);

            deactivateCoroutine = StartCoroutine(DeactivateAfterDuration());
        }
    }

    public override void Deactivate()
    {
        Debug.Log("\n=== [SlowdownAbility] Deactivate() chiamato ===");

        foreach (var data in affectedPlatforms)
        {
            if (data.platform != null)
            {
                data.platform.SetSpeedMultiplier(data.originalSpeedMultiplier);
                data.platform.SetOverlayActive(false);
                Debug.Log($"→ Ripristinata MovingPlatform: {data.platform.name}");
            }
        }

        foreach (var ro in affectedRotators)
        {
            if (ro != null)
            {
                ro.SetSpeedMultiplier(1f);
                ro.SetOverlayActive(false);
                Debug.Log($"→ Ripristinato RotatingPlatform: {ro.name}");
            }
        }

        foreach (var ts in affectedTurtleShells)
        {
            if (ts != null)
            {
                ts.SetSlow(false);
                ts.SetOverlayActive(false);
                ts.activeSlowdownAbility = null;
                Debug.Log($"→ Ripristinata TurtleShell: {ts.name}");
            }
        }

        // Non sbloccare mai gli NPC!

        affectedPlatforms.Clear();
        affectedRotators.Clear();
        affectedTurtleShells.Clear();

        IsActive = false;

        Debug.Log("[SlowdownAbility] Slowdown disattivato.");
        Debug.Log("=== [SlowdownAbility] Fine Deactivate() ===\n");
    }
}
