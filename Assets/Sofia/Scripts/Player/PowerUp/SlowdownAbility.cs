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

    private void Awake()
    {
        duration = customDuration;
        Debug.Log("[SlowdownAbility] Awake() - Durata impostata a: " + duration);
        effectIconIndex = 3;
    }

    public override void TryActivate()
    {
        if (!CanActivate())
        {
            Debug.Log("Impossibile attivare l'abilità.");
            return;
        }

        if (IsActive)
        {
            Debug.Log("[SlowdownAbility] Abilità già attiva – ignoro.");
            return;
        }

        Activate();
        IsActive = true;

        PlayerUI.Instance?.PulseIconAt(effectIconIndex);

        if (HasFixedDuration)
        {
            if (deactivateCoroutine != null)
                StopCoroutine(deactivateCoroutine);

            deactivateCoroutine = StartCoroutine(DeactivateAfterDuration());
        }
    }

    private IEnumerator DeactivateAfterDuration()
    {
        yield return new WaitForSeconds(duration);
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
            if (col.CompareTag("MovingPlatform"))
            {
                if (col.TryGetComponent(out MovingPlatform mp))
                {
                    affectedPlatforms.Add(new PlatformData
                    {
                        platform = mp,
                        originalSpeedMultiplier = 1f
                    });
                    mp.SetSpeedMultiplier(slowdownFactor);
                    Debug.Log($"→ MovingPlatform {col.name} rallentata.");
                }
            }
            else if (col.CompareTag("RotatingPlatform"))
            {
                if (col.TryGetComponent(out RotatingObject ro))
                {
                    affectedRotators.Add(ro);
                    ro.SetSpeedMultiplier(slowdownFactor);
                    Debug.Log($"→ RotatingPlatform {col.name} rallentata.");
                }
            }
            else if (col.CompareTag("TurtleShellHurtbox"))
            {
                TurtleShell ts = col.GetComponentInParent<TurtleShell>();
                if (ts != null && !affectedTurtleShells.Contains(ts))
                {
                   if (!ts.isSlow) // evita di chiamare più volte SetSlow(true)
                      {
                             Debug.Log($"SetSlow(true) chiamato su {ts.name}");
                             ts.slowFactor = slowdownFactor; // opzionale
                             ts.SetSlow(true);
                             affectedTurtleShells.Add(ts);
                             Debug.Log($"→ TurtleShell {ts.name} rallentata.");
                      }

                }
            }
        }

        if (affectedPlatforms.Count == 0 && affectedRotators.Count == 0 && affectedTurtleShells.Count == 0)
        {
            Debug.LogWarning("[SlowdownAbility] Nessun oggetto da rallentare trovato.");
            IsActive = false;
            return;
        }

        powerUpScript.SpendPower(powerCost);
        Debug.Log($"[SlowdownAbility] Slowdown attivato su " +
                  $"{affectedPlatforms.Count} piattaforme, " +
                  $"{affectedRotators.Count} rotatori, " +
                  $"{affectedTurtleShells.Count} TurtleShell.");
        Debug.Log("=== [SlowdownAbility] Fine Activate() ===\n");
    }

    public override void Deactivate()
    {
        Debug.Log("\n=== [SlowdownAbility] Deactivate() chiamato ===");

        foreach (var data in affectedPlatforms)
        {
            if (data.platform != null)
            {
                data.platform.SetSpeedMultiplier(data.originalSpeedMultiplier);
                Debug.Log($"Ripristinata MovingPlatform: {data.platform.name}");
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
