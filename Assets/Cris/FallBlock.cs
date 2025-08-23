using UnityEngine;
using System.Collections;

public class FallBlock : MonoBehaviour
{
    [Header("Timing Settings")]
    public float timeBeforeWarning = 0.5f;
    public float warningDuration = 0.5f;
    public float shakeTime = 0.5f;
    public float fallSpeed = 7f;
    public float respawnTime = 3f;

    [Header("Shake Settings")]
    public float shakeAmount = 0.2f;
    public float shakeFrequency = 50f;

    [Header("Visual Settings")]
    public Color warningColor = Color.red;
    public bool useEmission = true;

    private Vector3 originalPosition;
    private Color originalColor;
    private bool isTriggered = false;
    private bool isFalling = false;
    private MeshRenderer meshRenderer;
    private Material material;
    private Rigidbody rb;
    private BoxCollider boxCollider;

    private void Start()
    {
        originalPosition = transform.position;
        meshRenderer = GetComponent<MeshRenderer>();
        material = meshRenderer.material;
        originalColor = material.color;

        rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        boxCollider = GetComponent<BoxCollider>();
        if (boxCollider == null) boxCollider = gameObject.AddComponent<BoxCollider>();
        boxCollider.isTrigger = true; // ✅ SOLO TRIGGER
    }

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"[FallBlock] Trigger con: {other.name} (tag: {other.tag})");

        if (isTriggered || isFalling) return;

        if (other.CompareTag("Player") || other.CompareTag("PlayerAttackHitbox"))
        {
            Debug.Log("[FallBlock] Trigger attivato da Player o AttackHitBox!");
            isTriggered = true;
            StartCoroutine(FallSequence());
        }
    }

    private IEnumerator FallSequence()
    {
        yield return new WaitForSeconds(timeBeforeWarning);

        SetWarningColor(true);
        yield return new WaitForSeconds(warningDuration);

        StartCoroutine(ShakeEffect());
        yield return new WaitForSeconds(shakeTime);

        isFalling = true;
        rb.isKinematic = false;
        rb.useGravity = true;

        yield return new WaitForSeconds(respawnTime);
        Respawn();
    }

    private IEnumerator ShakeEffect()
    {
        Vector3 originalLocalPos = transform.localPosition;
        float elapsed = 0f;

        while (elapsed < shakeTime && !isFalling)
        {
            float x = originalLocalPos.x + Random.Range(-1f, 1f) * shakeAmount;
            float y = originalLocalPos.y + Random.Range(-1f, 1f) * shakeAmount * 0.5f;
            float z = originalLocalPos.z + Random.Range(-1f, 1f) * shakeAmount;

            transform.localPosition = new Vector3(x, y, z);

            elapsed += Time.deltaTime;
            yield return new WaitForSeconds(1f / shakeFrequency);
        }

        if (!isFalling) transform.localPosition = originalLocalPos;
    }

    private void SetWarningColor(bool warning)
    {
        if (useEmission)
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", warning ? warningColor * 0.5f : Color.black);
        }
        material.color = warning ? warningColor : originalColor;
    }

    private void Respawn()
    {
        transform.position = originalPosition;
        transform.rotation = Quaternion.identity;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;
        rb.useGravity = false;

        SetWarningColor(false);

        isTriggered = false;
        isFalling = false;

        Debug.Log("[FallBlock] Respawnato nella posizione originale");
    }

    private void OnDestroy()
    {
        if (material != null && material != meshRenderer.sharedMaterial)
        {
            Destroy(material);
        }
    }
}
