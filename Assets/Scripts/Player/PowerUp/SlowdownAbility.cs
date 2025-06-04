using UnityEngine;
using System.Collections.Generic;

public class SlowdownAbility : MonoBehaviour
{
    [Header("Power-Up")]
    public PlayerPowerUp powerUpManager;
    public float slowdownRadius = 5f;
    public float slowdownFactor = 0.5f;
    public int powerCost = 40;
    public float duration = 5f;

    private bool isSlowdownActive = false;
    private float slowdownTimer = 0f;

    private struct PlatformData
    {
        public MovingPlatform platform;
        public float originalSpeed;
    }

    private List<PlatformData> affectedPlatforms = new List<PlatformData>();

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Q))
        {
            TryActivateSlowdown();
        }

        if (isSlowdownActive)
        {
            slowdownTimer -= Time.deltaTime;
            if (slowdownTimer <= 0f)
            {
                ResetPlatformSpeeds();
            }
        }
    }

    void TryActivateSlowdown()
    {
        if (isSlowdownActive) return;
        if (powerUpManager == null)
        {
            Debug.LogWarning("[SlowdownAbility] PowerUpManager non assegnato!");
            return;
        }

        if (!powerUpManager.HasEnoughPower(powerCost)) return;

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
            MovingPlatform platform = col.GetComponent<MovingPlatform>();
            if (platform != null)
            {
                Vector3 viewportPos = cam.WorldToViewportPoint(platform.transform.position);
                bool isVisible = viewportPos.z > 0 && viewportPos.x >= 0 && viewportPos.x <= 1 && viewportPos.y >= 0 && viewportPos.y <= 1;

                if (isVisible)
                {
                    affectedPlatforms.Add(new PlatformData
                    {
                        platform = platform,
                        originalSpeed = platform.speed
                    });
                    platform.SetSpeedMultiplier(slowdownFactor);
                }
            }
        }

        if (affectedPlatforms.Count > 0)
        {
            powerUpManager.SpendPower(powerCost);
            isSlowdownActive = true;
            slowdownTimer = duration;
            Debug.Log($"[SlowdownAbility] Slowdown attivato su {affectedPlatforms.Count} piattaforme.");
        }
    }

    void ResetPlatformSpeeds()
    {
        foreach (var p in affectedPlatforms)
        {
            if (p.platform != null)
            {
                p.platform.SetSpeedMultiplier(1f);
            }
        }
        affectedPlatforms.Clear();
        isSlowdownActive = false;
        Debug.Log("[SlowdownAbility] Slowdown terminato, velocità piattaforme ripristinate.");
    }
}
