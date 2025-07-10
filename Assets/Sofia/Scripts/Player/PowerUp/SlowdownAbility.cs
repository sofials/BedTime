using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class SlowdownAbility : AbilityBase
{
    [Header("Slowdown Settings")]
    public float slowdownRadius = 20f;
    public float slowdownFactor = 0.5f;
    public float customDuration = 10f;

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
        Debug.Log("[SlowdownAbility] Awake() - Durata impostata a: " + duration);
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
        {
            if (data.platform != null)
                data.platform.StartBlinkingOverlay(blinkDuration);
        }
        foreach (var ro in affectedRotators)
        {
            if (ro != null)
                ro.StartBlinkingOverlay(blinkDuration);
        }
        foreach (var ts in affectedTurtleShells)
        {
            if (ts != null)
                ts.StartBlinkingOverlay(blinkDuration);
        }

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
                Debug.Log($"→ MovingPlatform {col.name} rallentata, patina e effetto attivati.");
            }
            else if (col.CompareTag("RotatingPlatform") && col.TryGetComponent(out RotatingObject ro))
            {
                affectedRotators.Add(ro);
                ro.SetSpeedMultiplier(slowdownFactor);
                ro.SetOverlayActive(true);
                ro.PlaySlowdownEffect(1f);
                Debug.Log($"→ RotatingPlatform {col.name} rallentata, patina e effetto attivati.");
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
                    ts.activeSlowdownAbility = this; // collega la slowdown
                    affectedTurtleShells.Add(ts);
                    Debug.Log($"→ TurtleShell {ts.name} rallentata.");
                }
            }
        }

        if (affectedPlatforms.Count == 0 && affectedRotators.Count == 0 && affectedTurtleShells.Count == 0)
        {
            Debug.LogWarning("[SlowdownAbility] Nessun oggetto da rallentare trovato.");
            IsActive = false;
            return;
        }

        // NON attivare animazione qui, la fa PlayerPowerUp tramite trigger
        if (powerUpScript.playerAnimator == null)
        {
            Debug.LogWarning("[SlowdownAbility] playerAnimator non assegnato in PlayerPowerUp!");
        }
        else
        {
            Debug.Log("[SlowdownAbility] Attivazione effetto slowdown senza animazione diretta qui.");
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
                Debug.Log($"Ripristinata MovingPlatform e patina disattivata: {data.platform.name}");
            }
        }

        foreach (var ro in affectedRotators)
        {
            if (ro != null)
            {
                ro.SetSpeedMultiplier(1f);
                ro.SetOverlayActive(false);
                Debug.Log($"Ripristinato RotatingPlatform e patina disattivata: {ro.name}");
            }
        }

        foreach (var ts in affectedTurtleShells)
        {
            if (ts != null)
            {
                ts.SetSlow(false);
                ts.SetOverlayActive(false);
                ts.activeSlowdownAbility = null; // pulizia riferimento
                Debug.Log($"Ripristinata TurtleShell e patina disattivata: {ts.name}");
            }
        }

        affectedPlatforms.Clear();
        affectedRotators.Clear();
        affectedTurtleShells.Clear();

        IsActive = false;

        Debug.Log("[SlowdownAbility] Slowdown disattivato.");
        Debug.Log("=== [SlowdownAbility] Fine Deactivate() ===\n");
    }
}
