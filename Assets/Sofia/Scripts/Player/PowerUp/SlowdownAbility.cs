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

    // Aspetta la durata totale meno il tempo di lampeggio
    yield return new WaitForSeconds(duration - blinkDuration);

    // Inizia il lampeggio su tutte le piattaforme interessate
    foreach (var data in affectedPlatforms)
    {
        if (data.platform != null)
            data.platform.StartBlinkingOverlay(blinkDuration);
    }

    // Aspetta che finisca il lampeggio
    yield return new WaitForSeconds(blinkDuration);

    // Disattiva l'abilità
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
                Debug.Log($"→ RotatingPlatform {col.name} rallentata.");
            }
            else if (col.CompareTag("TurtleShellHurtbox"))
            {
                TurtleShell ts = col.GetComponentInParent<TurtleShell>();
                if (ts != null && !affectedTurtleShells.Contains(ts) && !ts.isSlow)
                {
                    ts.slowFactor = slowdownFactor;
                    ts.SetSlow(true);
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

        if (powerUpScript.playerAnimator != null)
        {
            powerUpScript.playerAnimator.SetTrigger("SlowdownEffect");
            Debug.Log("[SlowdownAbility] Trigger SlowdownEffect animazione inviato.");
        }
        else
        {
            Debug.LogWarning("[SlowdownAbility] playerAnimator non assegnato in PlayerPowerUp!");
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
                Debug.Log($"Ripristinato RotatingPlatform: {ro.name}");
            }
        }

        foreach (var ts in affectedTurtleShells)
        {
            if (ts != null)
            {
                ts.SetSlow(false);
                Debug.Log($"Ripristinata TurtleShell: {ts.name}");
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
