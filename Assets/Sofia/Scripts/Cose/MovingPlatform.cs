using UnityEngine;
using UnityEngine.Splines;
using System.Collections;
using System.Collections.Generic;

public class MovingPlatform : MonoBehaviour
{
    public SplineContainer splineContainer;
    public float speed = 10f;
    public bool pingPong = true;
    public int sampleResolution = 100;

    private List<Vector3> sampledPoints = new List<Vector3>();
    private List<float> cumulativeDistances = new List<float>();
    private float currentDistance = 0f;
    private float totalLength = 0f;
    private int direction = 1;

    private Vector3 lastPosition;
    private CharacterController playerController = null;

    private float speedMultiplier = 1f; // <-- IMPORTANTE per lo slowdown

    private Vector3 deltaMovement; // <-- Salviamo il movimento della piattaforma (per il player)

    // Esportiamo il deltaMovement per il player
    public Vector3 DeltaMovement => deltaMovement;

    [Header("Overlay Patina")]
    [SerializeField] private Material patinaMaterial; // Materiale blu trasparente per patina

    [Header("Slowdown FX")]
    [SerializeField] private CFXR_EffectController slowdownEffect; // Effetto slowdown figlio piattaforma

    private MeshRenderer meshRenderer;
    private bool patinaActive = false;

    void Awake()
    {
        meshRenderer = GetComponentInChildren<MeshRenderer>();
        if (meshRenderer == null)
        {
            Debug.LogWarning($"[MovingPlatform] Nessun MeshRenderer trovato su {gameObject.name}");
        }

        if (slowdownEffect != null)
        {
            slowdownEffect.gameObject.SetActive(false); // Disattiva effetto all’inizio
        }
    }

    void Start()
    {
        SampleSpline();
        lastPosition = transform.position;
    }

    void Update()
    {
        currentDistance += speed * speedMultiplier * direction * Time.deltaTime;

        if (pingPong)
        {
            if (currentDistance >= totalLength)
            {
                currentDistance = totalLength;
                direction = -1;
            }
            else if (currentDistance <= 0)
            {
                currentDistance = 0;
                direction = 1;
            }
        }
        else
        {
            currentDistance %= totalLength;
        }

        Vector3 newPosition = GetPositionAtDistance(currentDistance);
        deltaMovement = newPosition - lastPosition;

        transform.position = newPosition;

        lastPosition = newPosition;
    }

    void SampleSpline()
    {
        sampledPoints.Clear();
        cumulativeDistances.Clear();

        totalLength = 0f;
        Vector3 prevPoint = splineContainer.EvaluatePosition(0f);
        sampledPoints.Add(prevPoint);
        cumulativeDistances.Add(0f);

        for (int i = 1; i <= sampleResolution; i++)
        {
            float t = (float)i / sampleResolution;
            Vector3 point = splineContainer.EvaluatePosition(t);
            float dist = Vector3.Distance(prevPoint, point);
            totalLength += dist;

            sampledPoints.Add(point);
            cumulativeDistances.Add(totalLength);
            prevPoint = point;
        }
    }

    Vector3 GetPositionAtDistance(float distance)
    {
        if (distance <= 0f) return sampledPoints[0];
        if (distance >= totalLength) return sampledPoints[sampledPoints.Count - 1];

        for (int i = 1; i < cumulativeDistances.Count; i++)
        {
            if (cumulativeDistances[i] >= distance)
            {
                float prevDist = cumulativeDistances[i - 1];
                float nextDist = cumulativeDistances[i];
                float segmentT = Mathf.InverseLerp(prevDist, nextDist, distance);
                return Vector3.Lerp(sampledPoints[i - 1], sampledPoints[i], segmentT);
            }
        }

        return sampledPoints[sampledPoints.Count - 1];
    }

    // Metodo chiamato dallo slowdown power-up
    public void SetSpeedMultiplier(float multiplier)
    {
        speedMultiplier = multiplier;
        Debug.Log($"[MovingPlatform] {gameObject.name} speed multiplier impostato a {multiplier}");
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            playerController = collision.gameObject.GetComponent<CharacterController>();
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            if (playerController == collision.gameObject.GetComponent<CharacterController>())
            {
                playerController = null;
            }
        }
    }

    // -----------------------------
    // Gestione patina blu overlay
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
    // Gestione effetto slowdown FX
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
