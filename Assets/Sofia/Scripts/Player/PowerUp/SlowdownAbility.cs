using UnityEngine;
using System.Collections.Generic;

public class SlowdownAbility : AbilityBase
{
    public float slowdownRadius = 20f;
    public float slowdownFactor = 0.5f;
    public float customDuration = 10f;

    // Override del costo di attivazione: 20
    public override int powerCost => 20;

    private struct PlatformData
    {
        public MovingPlatform platform;
        public float originalSpeedMultiplier;
    }

    private List<PlatformData> affectedPlatforms = new List<PlatformData>();

    // Questa abilità ha durata fissa
    protected override bool HasFixedDuration => true;

    void Awake()
    {
        duration = customDuration;
        Debug.Log("[SlowdownAbility] Awake() - Durata impostata a: " + duration);
        effectIconIndex = 3;
    }

    public override void Activate()
    {
        Debug.Log("\n=== [SlowdownAbility] Activate() chiamato ===");
        affectedPlatforms.Clear();

        Collider[] colliders = Physics.OverlapSphere(powerUpScript.transform.position, slowdownRadius);

        Debug.Log($"[SlowdownAbility] Numero di collider trovati nel raggio di {slowdownRadius}: {colliders.Length}");

        foreach (Collider col in colliders)
        {
            Debug.Log($"[SlowdownAbility] Controllo collider: {col.name}, Tag: {col.tag}");

            if (col.CompareTag("MovingPlatform"))
            {
                Debug.Log($"[SlowdownAbility] {col.name} ha il tag 'MovingPlatform'");

                MovingPlatform mp = col.GetComponent<MovingPlatform>();
                if (mp != null)
                {
                    Debug.Log($"[SlowdownAbility] {col.name} ha componente MovingPlatform");

                    affectedPlatforms.Add(new PlatformData
                    {
                        platform = mp,
                        originalSpeedMultiplier = 1f // Se hai un metodo GetSpeedMultiplier(), puoi usarlo qui
                    });

                    mp.SetSpeedMultiplier(slowdownFactor);
                    Debug.Log($"[SlowdownAbility] Rallentata piattaforma {col.name} con fattore {slowdownFactor}");
                }
                else
                {
                    Debug.LogWarning($"[SlowdownAbility] {col.name} ha il tag corretto ma non ha componente MovingPlatform");
                }
            }
            else
            {
                Debug.Log($"[SlowdownAbility] {col.name} ha un tag diverso: {col.tag}");
            }
        }

        if (affectedPlatforms.Count > 0)
        {
            Debug.Log($"[SlowdownAbility] Slowdown attivato su {affectedPlatforms.Count} piattaforme.");
            powerUpScript.SpendPower(powerCost); // ✅ Questo aggiorna anche la barra tramite PlayerUI
            IsActive = true;
        }
        else
        {
            Debug.LogWarning("[SlowdownAbility] Nessuna piattaforma trovata da rallentare.");
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
                Debug.LogWarning("[SlowdownAbility] Una delle piattaforme è null durante il ripristino.");
            }
        }

        affectedPlatforms.Clear();
        IsActive = false;

        Debug.Log("[SlowdownAbility] Slowdown disattivato e lista piattaforme svuotata.");
        Debug.Log("=== [SlowdownAbility] Fine Deactivate() ===\n");
    }
}
