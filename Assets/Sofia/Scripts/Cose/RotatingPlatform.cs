using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class RotatingObject : MonoBehaviour
{
    public Vector3 rotationAxis = Vector3.up;
    public float rotationSpeed = 360f;
    private float speedMultiplier = 1f;

    [Header("Overlay Patina")]
    [SerializeField] private Material patinaMaterial;

    [Header("Slowdown FX")]
    [SerializeField] private CFXR_EffectController slowdownEffect;

    [Header("Modalità Girandola")]
    public bool usePinwheelMode = false;
    public Transform visualToRotate; // Questo è il figlio che ruota visivamente

    [Header("Rotazione attorno a oggetto")]
    public bool rotateAroundObject = false;
    public Transform targetObject;
    public bool maintainOrientation = true;

    [Header("Slowdown Custom Settings")]
    public bool useCustomSlowdown = false;
    [Range(0.01f, 1f)]
    public float customSlowdownFactor = 0.5f;

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
        if (usePinwheelMode && visualToRotate != null)
        {
            // Ruota solo la parte visiva su sé stessa
            visualToRotate.Rotate(rotationAxis.normalized, rotationSpeed * speedMultiplier * Time.deltaTime, Space.Self);
        }
        else if (rotateAroundObject && targetObject != null)
        {
            // Rotazione orbitale attorno al target
            transform.RotateAround(targetObject.position, rotationAxis.normalized, rotationSpeed * speedMultiplier * Time.deltaTime);

            if (maintainOrientation)
            {
                transform.rotation = Quaternion.Euler(0, transform.rotation.eulerAngles.y, 0);
            }
        }
        else
        {
            // Rotazione su sé stesso
            transform.Rotate(rotationAxis.normalized, rotationSpeed * speedMultiplier * Time.deltaTime, Space.Self);
        }
    }

    public void SetSpeedMultiplier(float multiplier)
    {
        speedMultiplier = useCustomSlowdown ? customSlowdownFactor : multiplier;
        Debug.Log($"[RotatingObject] {gameObject.name} speed multiplier impostato a {speedMultiplier}");
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

    public void PlaySlowdownEffect(float duration = 1f)
    {
        if (slowdownEffect == null) return;
        StartCoroutine(PlayEffectRoutine(duration));
    }

    private IEnumerator PlayEffectRoutine(float duration)
    {
        slowdownEffect.gameObject.SetActive(true);
        slowdownEffect.PlayEffect();

        yield return new WaitForSeconds(duration);

        slowdownEffect.StopEffect();
        slowdownEffect.gameObject.SetActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        var player = other.GetComponent<ThirdPersonController>();
        if (player != null && speedMultiplier > 0.99f)
        {
            // Danno
            player.TakeDamage(DAMAGE_AMOUNT);

            // Spinta all'indietro
            Rigidbody playerRb = player.GetComponent<Rigidbody>();
            if (playerRb != null)
            {
                Vector3 pushDirection = (player.transform.position - transform.position).normalized;
                float pushForce = 5f; // Puoi regolare questo valore
                playerRb.AddForce(pushDirection * pushForce, ForceMode.Impulse);
            }
        }
    }

}
