using UnityEngine;
using System.Collections.Generic;

public class SlowdownAbility : AbilityBase
{
    [Header("Slowdown Settings")]
    public float slowdownRadius = 20f;
    public float slowdownFactor = 0.5f;

    [Header("Durata Override")]
    public float customDuration = 10f;

    private struct PlatformData
    {
        public Rigidbody rb;
        public Vector3 originalVelocity;
    }

    private List<PlatformData> affectedPlatforms = new List<PlatformData>();

    void Awake()
    {
        duration = customDuration;
    }

    public override void Activate()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            Debug.LogWarning("[SlowdownAbility] Camera.main non trovata!");
            return;
        }

        Collider[] colliders = Physics.OverlapSphere(transform.position, slowdownRadius);
        affectedPlatforms.Clear();

        foreach (Collider col in colliders)
        {
            GameObject obj = col.gameObject;

            if (obj.CompareTag("MovingPlatform"))
            {
                Vector3 viewportPos = cam.WorldToViewportPoint(obj.transform.position);
                bool isVisible = viewportPos.z > 0 &&
                                 viewportPos.x >= 0 && viewportPos.x <= 1 &&
                                 viewportPos.y >= 0 && viewportPos.y <= 1;

                if (isVisible)
                {
                    Rigidbody rb = obj.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        affectedPlatforms.Add(new PlatformData
                        {
                            rb = rb,
                            originalVelocity = rb.linearVelocity
                        });

                        rb.linearVelocity = rb.linearVelocity * slowdownFactor;
                    }
                    else
                    {
                        Debug.LogWarning("[SlowdownAbility] Oggetto con tag MovingPlatform senza Rigidbody.");
                    }
                }
            }
        }

        if (affectedPlatforms.Count > 0)
        {
            Debug.Log($"[SlowdownAbility] Slowdown attivato su {affectedPlatforms.Count} piattaforme.");
            IsActive = true;
        }
        else
        {
            Debug.Log("[SlowdownAbility] Nessuna piattaforma trovata per rallentare.");
            IsActive = false;
        }
    }

    public override void Deactivate()
    {
        foreach (var data in affectedPlatforms)
        {
            if (data.rb != null)
            {
                data.rb.linearVelocity = data.originalVelocity;
            }
        }

        affectedPlatforms.Clear();
        IsActive = false;

        Debug.Log("[SlowdownAbility] Slowdown terminato, velocità piattaforme ripristinate.");
    }
}
