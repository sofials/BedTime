using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class RotatingObject : MonoBehaviour
{
    public Vector3 rotationAxis = Vector3.up;
    public float rotationSpeed = 360f;
    private float speedMultiplier = 1f;
    private float originalRotationSpeed;

    [Header("Overlay Patina")]
    [SerializeField] private Material patinaMaterial;

    [Header("Slowdown FX")]
    [SerializeField] private CFXR_EffectController slowdownEffect;

    [Header("Modalità Girandola")]
    public bool usePinwheelMode = false;
    public Transform visualToRotate;

    [Header("Rotazione attorno a oggetto")]
    public bool rotateAroundObject = false;
    public Transform targetObject;
    public bool maintainOrientation = true;

    [Header("Slowdown Custom Settings")]
    public bool useCustomSlowdown = false;
    [Tooltip("Velocità assoluta temporanea durante lo slowdown.")]
    public float customSlowdownFactor = 90f;

    private MeshRenderer meshRenderer;
    private bool patinaActive = false;

    private const float DAMAGE_AMOUNT = 10f;

    void Awake()
    {
        meshRenderer = GetComponentInChildren<MeshRenderer>();
        if (meshRenderer == null)
        {
            Debug.LogWarning($"[RotatingObject] Nessun MeshRenderer trovato su {gameObject.name}");
        }

        if (slowdownEffect != null)
        {
            slowdownEffect.gameObject.SetActive(false);
        }

        if ((rotateAroundObject || usePinwheelMode) && targetObject == null)
        {
            Debug.LogWarning($"[RotatingObject] Modalità attivata ma targetObject non assegnato su {gameObject.name}");
        }

        if (usePinwheelMode && visualToRotate == null)
        {
            Debug.LogWarning($"[RotatingObject] usePinwheelMode attivo ma nessun visualToRotate assegnato su {gameObject.name}");
        }
    }

    void Update()
    {
        float rotationThisFrame = rotationSpeed * speedMultiplier * Time.deltaTime;

        if (usePinwheelMode && visualToRotate != null)
        {
            visualToRotate.Rotate(rotationAxis.normalized, rotationThisFrame, Space.Self);
        }
        else if (rotateAroundObject && targetObject != null)
        {
            transform.RotateAround(targetObject.position, rotationAxis.normalized, rotationThisFrame);

            if (maintainOrientation)
            {
                transform.rotation = Quaternion.Euler(0, transform.rotation.eulerAngles.y, 0);
            }
        }
        else
        {
            transform.Rotate(rotationAxis.normalized, rotationThisFrame, Space.Self);
        }
    }

    public void SetSpeedMultiplier(float multiplier)
    {
        if (useCustomSlowdown)
        {
            originalRotationSpeed = rotationSpeed;
            rotationSpeed = customSlowdownFactor;
            Debug.Log($"[RotatingObject] {gameObject.name} - velocità impostata direttamente a {rotationSpeed} (da {originalRotationSpeed})");
        }
        else
        {
            speedMultiplier = multiplier;
            Debug.Log($"[RotatingObject] {gameObject.name} - speed multiplier impostato a {speedMultiplier}");
        }
    }

    public void RestoreOriginalSpeed()
    {
        if (useCustomSlowdown)
        {
            rotationSpeed = originalRotationSpeed;
            Debug.Log($"[RotatingObject] {gameObject.name} - velocità ripristinata a {rotationSpeed}");
        }
        else
        {
            speedMultiplier = 1f;
        }
    }

    public void PlaySlowdownEffect(float duration = 1f)
    {
        if (slowdownEffect != null)
        {
            StartCoroutine(SlowdownWithFxRoutine(duration));
        }
        else
        {
            StartCoroutine(SlowdownRoutine(duration));
        }
    }

    private IEnumerator SlowdownRoutine(float duration)
    {
        SetSpeedMultiplier(1f);
        yield return new WaitForSeconds(duration);
        RestoreOriginalSpeed();
    }

    private IEnumerator SlowdownWithFxRoutine(float duration)
    {
        slowdownEffect.gameObject.SetActive(true);
        slowdownEffect.PlayEffect();

        SetSpeedMultiplier(1f);
        yield return new WaitForSeconds(duration);

        RestoreOriginalSpeed();
        slowdownEffect.StopEffect();
        slowdownEffect.gameObject.SetActive(false);
    }

    public void SetOverlayActive(bool active)
    {
        if (meshRenderer == null || patinaMaterial == null) return;

        var materials = new List<Material>(meshRenderer.sharedMaterials);

        if (active && !patinaActive)
        {
            if (!materials.Contains(patinaMaterial))
            {
                materials.Add(patinaMaterial);
                meshRenderer.materials = materials.ToArray();
                patinaActive = true;
            }
        }
        else if (!active && patinaActive)
        {
            materials.Remove(patinaMaterial);
            meshRenderer.materials = materials.ToArray();
            patinaActive = false;
        }
    }

    public void StartBlinkingOverlay(float duration)
    {
        if (meshRenderer == null || patinaMaterial == null) return;
        StartCoroutine(BlinkOverlay(duration));
    }

    private IEnumerator BlinkOverlay(float duration)
    {
        float elapsed = 0f;
        float blinkRate = 0.2f;
        bool state = true;

        while (elapsed < duration)
        {
            SetOverlayActive(state);
            state = !state;
            yield return new WaitForSeconds(blinkRate);
            elapsed += blinkRate;
        }

        SetOverlayActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        var player = other.GetComponent<ThirdPersonController>();
        if (player != null && speedMultiplier > 0.99f)
        {
            player.TakeDamage(DAMAGE_AMOUNT);

            Rigidbody playerRb = player.GetComponent<Rigidbody>();
            if (playerRb != null)
            {
                Vector3 pushDirection = (player.transform.position - transform.position).normalized;
                float pushForce = 5f;
                playerRb.AddForce(pushDirection * pushForce, ForceMode.Impulse);
            }
        }
    }
}
