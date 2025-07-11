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

    [Header("Girandola Mode")]
    public bool enableGirandolaRotation = false;
    public Vector3 girandolaPivotOffset = Vector3.zero;

    private MeshRenderer meshRenderer;
    private bool patinaActive = false;

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
    }

    void Update()
    {
        if (enableGirandolaRotation)
        {
            // Rotazione attorno al punto pivot
            Vector3 pivot = transform.position + girandolaPivotOffset;
            transform.RotateAround(pivot, rotationAxis.normalized, rotationSpeed * speedMultiplier * Time.deltaTime);
        }
        else
        {
            // Rotazione normale attorno al proprio asse
            transform.Rotate(rotationAxis.normalized, rotationSpeed * speedMultiplier * Time.deltaTime);
        }
    }


    public void SetSpeedMultiplier(float multiplier)
    {
        speedMultiplier = multiplier;
        Debug.Log($"[RotatingObject] {gameObject.name} speed multiplier impostato a {multiplier}");
    }

    // -----------------------------
    // Overlay patina blu
    // -----------------------------

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

    // -----------------------------
    // FX slowdown
    // -----------------------------

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
}
