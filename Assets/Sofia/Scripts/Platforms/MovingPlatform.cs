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
    
    [Header("Rotation Settings")]
    public bool enableRotation = true;
    public Vector3 forwardAxis = Vector3.forward;
    public Vector3 upAxis = Vector3.up;
    public float rotationSpeed = 5f;

    private List<Vector3> sampledPoints = new List<Vector3>();
    private List<Vector3> sampledTangents = new List<Vector3>();
    private List<float> cumulativeDistances = new List<float>();
    private float currentDistance = 0f;
    private float totalLength = 0f;
    private int direction = 1;

    private Vector3 lastPosition;
    private CharacterController playerController = null;

    private float speedMultiplier = 1f;
    private Vector3 deltaMovement;
    public Vector3 DeltaMovement => deltaMovement;

    private Quaternion initialRotation;

    [Header("Overlay Emission")]
    [SerializeField] private Color overlayColor = Color.red;
    [SerializeField] private float overlayIntensity = 2f; // Aumentato per più luminosità
    [Tooltip("Moltiplicatore aggiuntivo per HDR emission (valori alti = più luce)")]
    [SerializeField] private float hdrMultiplier = 3f;
    [Tooltip("Se true, mantiene anche il tint del Base Color oltre all'emission")]
    [SerializeField] private bool applyColorTint = true;

    [Header("Slowdown FX")]
    [SerializeField] private CFXR_EffectController slowdownEffect;

    [Header("Slowdown Custom Settings")]
    public bool useCustomSlowdown = false;
    [Tooltip("Velocità da applicare temporaneamente durante lo slowdown.")]
    public float customSlowdownFactor = 3f;

    private MeshRenderer meshRenderer;
    private bool patinaActive = false;
    private float originalSpeed;
    
    // Per salvare i colori originali dei materiali
    private Dictionary<Material, Material> materialInstances = new Dictionary<Material, Material>();
    private Dictionary<Material, Color> originalBaseColors = new Dictionary<Material, Color>();
    private Dictionary<Material, Color> originalEmissionColors = new Dictionary<Material, Color>();

    void Awake()
    {
        meshRenderer = GetComponentInChildren<MeshRenderer>();
        if (meshRenderer == null)
        {
            Debug.LogWarning($"[MovingPlatform] Nessun MeshRenderer trovato su {gameObject.name}");
        }
        else
        {
            // Salva i colori originali dei materiali
            foreach (Material mat in meshRenderer.sharedMaterials)
            {
                if (mat != null)
                {
                    if (mat.HasProperty("_BaseColor"))
                        originalBaseColors[mat] = mat.GetColor("_BaseColor");
                    if (mat.HasProperty("_EmissionColor"))
                        originalEmissionColors[mat] = mat.GetColor("_EmissionColor");
                }
            }
        }

        if (slowdownEffect != null)
        {
            slowdownEffect.gameObject.SetActive(false);
        }

        initialRotation = transform.rotation;
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

        if (enableRotation)
        {
            Vector3 tangent = GetTangentAtDistance(currentDistance);
            if (tangent != Vector3.zero)
            {
                if (direction < 0)
                {
                    tangent = -tangent;
                }
                
                UpdateRotation(tangent);
            }
        }

        lastPosition = newPosition;
    }

    void SampleSpline()
    {
        sampledPoints.Clear();
        sampledTangents.Clear();
        cumulativeDistances.Clear();

        totalLength = 0f;
        Vector3 prevPoint = splineContainer.EvaluatePosition(0f);
        Vector3 tangent = splineContainer.EvaluateTangent(0f);
        
        sampledPoints.Add(prevPoint);
        sampledTangents.Add(tangent.normalized);
        cumulativeDistances.Add(0f);

        for (int i = 1; i <= sampleResolution; i++)
        {
            float t = (float)i / sampleResolution;
            Vector3 point = splineContainer.EvaluatePosition(t);
            tangent = splineContainer.EvaluateTangent(t);
            
            float dist = Vector3.Distance(prevPoint, point);
            totalLength += dist;

            sampledPoints.Add(point);
            sampledTangents.Add(tangent.normalized);
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

    Vector3 GetTangentAtDistance(float distance)
    {
        if (distance <= 0f) return sampledTangents[0];
        if (distance >= totalLength) return sampledTangents[sampledTangents.Count - 1];

        for (int i = 1; i < cumulativeDistances.Count; i++)
        {
            if (cumulativeDistances[i] >= distance)
            {
                float prevDist = cumulativeDistances[i - 1];
                float nextDist = cumulativeDistances[i];
                float segmentT = Mathf.InverseLerp(prevDist, nextDist, distance);
                return Vector3.Slerp(sampledTangents[i - 1], sampledTangents[i], segmentT).normalized;
            }
        }

        return sampledTangents[sampledTangents.Count - 1];
    }

    void UpdateRotation(Vector3 tangent)
    {
        Quaternion splineRotation = Quaternion.LookRotation(tangent, upAxis);
        
        if (forwardAxis != Vector3.forward)
        {
            Quaternion axisOffset = Quaternion.FromToRotation(Vector3.forward, forwardAxis);
            splineRotation = splineRotation * axisOffset;
        }

        Quaternion targetRotation = splineRotation * initialRotation;

        if (rotationSpeed > 0)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
        else
        {
            transform.rotation = targetRotation;
        }
    }

    public void SetSpeedMultiplier(float multiplier)
    {
        if (useCustomSlowdown)
        {
            originalSpeed = speed;
            speed = customSlowdownFactor;
            Debug.Log($"[MovingPlatform] Velocità impostata direttamente a {speed} (da {originalSpeed})");
        }
        else
        {
            speed *= multiplier;
            Debug.Log($"[MovingPlatform] Velocità moltiplicata, nuova velocità = {speed}");
        }
    }

    public void RestoreOriginalSpeed()
    {
        if (useCustomSlowdown)
        {
            speed = originalSpeed;
            Debug.Log($"[MovingPlatform] Velocità ripristinata a {speed}");
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

    // OVERLAY EMISSIVO LUMINOSO per URP Simple Lit
    public void SetOverlayActive(bool active)
    {
        if (meshRenderer == null) return;
        SetEmissiveOverlay(active);
    }

    private void SetEmissiveOverlay(bool active)
    {
        Material[] currentMaterials = meshRenderer.materials;

        for (int i = 0; i < currentMaterials.Length; i++)
        {
            Material mat = currentMaterials[i];
            if (mat != null)
            {
                // Crea istanza del materiale se non esiste
                if (!materialInstances.ContainsKey(mat))
                {
                    materialInstances[mat] = new Material(mat);
                    currentMaterials[i] = materialInstances[mat];
                }

                Material instanceMat = materialInstances[mat];

                if (active)
                {
                    // 1. EMISSION LUMINOSO (principale)
                    if (instanceMat.HasProperty("_EmissionColor"))
                    {
                        // Calcola colore emission HDR per massima luminosità
                        Color hdrEmission = overlayColor * overlayIntensity * hdrMultiplier;
                        instanceMat.SetColor("_EmissionColor", hdrEmission);
                        
                        // Abilita emission
                        instanceMat.EnableKeyword("_EMISSION");
                        
                        // Forza il material a essere emission-enabled
                        instanceMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                    }

                    // 2. BASE COLOR TINT (opzionale, per colorare anche la texture)
                    if (applyColorTint && instanceMat.HasProperty("_BaseColor"))
                    {
                        Material originalMat = GetOriginalMaterial(instanceMat);
                        if (originalMat != null && originalBaseColors.ContainsKey(originalMat))
                        {
                            Color originalColor = originalBaseColors[originalMat];
                            // Mescola il colore originale con l'overlay
                            Color tintedColor = Color.Lerp(originalColor, originalColor * overlayColor, 0.3f);
                            tintedColor.a = originalColor.a;
                            instanceMat.SetColor("_BaseColor", tintedColor);
                        }
                    }
                }
                else
                {
                    // Ripristina colori originali
                    Material originalMat = GetOriginalMaterial(instanceMat);
                    if (originalMat != null)
                    {
                        if (instanceMat.HasProperty("_EmissionColor") && originalEmissionColors.ContainsKey(originalMat))
                        {
                            instanceMat.SetColor("_EmissionColor", originalEmissionColors[originalMat]);
                            // Se l'originale non aveva emission, disabilitalo
                            if (originalEmissionColors[originalMat] == Color.black)
                            {
                                instanceMat.DisableKeyword("_EMISSION");
                                instanceMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                            }
                        }

                        if (instanceMat.HasProperty("_BaseColor") && originalBaseColors.ContainsKey(originalMat))
                        {
                            instanceMat.SetColor("_BaseColor", originalBaseColors[originalMat]);
                        }
                    }
                }
            }
        }

        meshRenderer.materials = currentMaterials;
        patinaActive = active;
    }

    // Helper per trovare il materiale originale da un'istanza
    private Material GetOriginalMaterial(Material instance)
    {
        foreach (var kvp in materialInstances)
        {
            if (kvp.Value == instance)
                return kvp.Key;
        }
        return null;
    }

    public void StartBlinkingOverlay(float duration)
    {
        if (meshRenderer == null) return;
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

    // Cleanup quando l'oggetto viene distrutto
    void OnDestroy()
    {
        // Pulisci le istanze di materiali e dizionari
        foreach (var instance in materialInstances.Values)
        {
            if (instance != null)
                Destroy(instance);
        }
        materialInstances.Clear();
        originalBaseColors.Clear();
        originalEmissionColors.Clear();
    }
}