using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class SlowdownAbility : AbilityBase
{
    public float slowdownRadius = 20f;
    public float slowdownFactor = 0.5f;
    public float customDuration = 10f;

    public override int powerCost => 20;

    private struct PlatformData
    {
        public MovingPlatform platform;
        public float originalSpeedMultiplier;
    }

    private List<PlatformData> affectedPlatforms = new List<PlatformData>();
    private List<RotatingObject> affectedRotators = new List<RotatingObject>();

    private Coroutine deactivateCoroutine;

    protected override bool HasFixedDuration => true;

    void Awake()
    {
        duration = customDuration;
        Debug.Log("[SlowdownAbility] Awake() - Durata impostata a: " + duration);
        effectIconIndex = 3;
    }

    public override void TryActivate()
    {
        if (CanActivate())
        {
            if (!IsActive)
            {
                Activate();
                IsActive = true;

                if (PlayerUI.Instance != null)
                    PlayerUI.Instance.PulseIconAt(effectIconIndex);

                if (HasFixedDuration)
                {
                    if (deactivateCoroutine != null)
                        StopCoroutine(deactivateCoroutine);

                    deactivateCoroutine = StartCoroutine(DeactivateAfterDuration());
                }
            }
            else
            {
                Debug.Log("[SlowdownAbility] Abilità già attiva, ignoro riattivazione.");
                // Qui potresti voler fare altro, ma per ora non rifaccio nulla
            }
        }
        else
        {
            Debug.Log("Impossibile attivare l'abilità.");
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

        if (IsActive)
        {
            Debug.Log("[SlowdownAbility] Abilità già attiva, salto Activate.");
            return;
        }

        affectedPlatforms.Clear();
        affectedRotators.Clear();

        Collider[] colliders = Physics.OverlapSphere(powerUpScript.transform.position, slowdownRadius);

        Debug.Log($"[SlowdownAbility] Numero di collider trovati nel raggio di {slowdownRadius}: {colliders.Length}");

        foreach (Collider col in colliders)
        {
            Debug.Log($"[SlowdownAbility] Controllo collider: {col.name}, Tag: {col.tag}");

            if (col.CompareTag("MovingPlatform"))
            {
                MovingPlatform mp = col.GetComponent<MovingPlatform>();
                if (mp != null)
                {
                    affectedPlatforms.Add(new PlatformData
                    {
                        platform = mp,
                        originalSpeedMultiplier = 1f
                    });

                    mp.SetSpeedMultiplier(slowdownFactor);
                    Debug.Log($"[SlowdownAbility] Rallentata piattaforma {col.name} con fattore {slowdownFactor}");
                }
                else
                {
                    Debug.LogWarning($"[SlowdownAbility] {col.name} ha il tag ma manca MovingPlatform");
                }
            }
            else if (col.CompareTag("RotatingPlatform"))
            {
                RotatingObject ro = col.GetComponent<RotatingObject>();
                if (ro != null)
                {
                    affectedRotators.Add(ro);
                    ro.SetSpeedMultiplier(slowdownFactor);
                    Debug.Log($"[SlowdownAbility] Rallentato oggetto rotante {col.name} con fattore {slowdownFactor}");
                }
                else
                {
                    Debug.LogWarning($"[SlowdownAbility] {col.name} tag corretto ma manca RotatingObject");
                }
            }
            else
            {
                Debug.Log($"[SlowdownAbility] {col.name} tag diverso: {col.tag}");
            }
        }

        if (affectedPlatforms.Count > 0 || affectedRotators.Count > 0)
        {
            powerUpScript.SpendPower(powerCost);
            IsActive = true;
            Debug.Log($"[SlowdownAbility] Slowdown attivato su {affectedPlatforms.Count} piattaforme e {affectedRotators.Count} rotatori.");
        }
        else
        {
            Debug.LogWarning("[SlowdownAbility] Nessuna piattaforma o rotatore trovato da rallentare.");
            IsActive = false;
        }

        Debug.Log("=== [SlowdownAbility] Fine Activate() ===\n");
    }

    public override void Deactivate()
    {
        Debug.Log("\n=== [SlowdownAbility] Deactivate() chiamato ===");

        foreach (var data in affectedPlatforms)
        {
            if (data.platform != null)
            {
                data.platform.SetSpeedMultiplier(1f);
                Debug.Log($"[SlowdownAbility] Ripristinata velocità piattaforma: {data.platform.name}");
            }
            else
            {
                Debug.LogWarning("[SlowdownAbility] Piattaforma null durante ripristino.");
            }
        }

        foreach (var rotator in affectedRotators)
        {
            if (rotator != null)
            {
                rotator.SetSpeedMultiplier(1f);
                Debug.Log($"[SlowdownAbility] Ripristinata velocità rotatore: {rotator.name}");
            }
            else
            {
                Debug.LogWarning("[SlowdownAbility] Rotatore null durante ripristino.");
            }
        }

        affectedPlatforms.Clear();
        affectedRotators.Clear();
        IsActive = false;

        Debug.Log("[SlowdownAbility] Slowdown disattivato e lista piattaforme svuotata.");
        Debug.Log("=== [SlowdownAbility] Fine Deactivate() ===\n");
    }
}
