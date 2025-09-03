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

    [Header("Slowdown Settings")]
    [Tooltip("Se false, questa piattaforma non può essere rallentata")]
    public bool canBeSlowed = true;
    [Tooltip("Durata personalizzata per lo slowdown (0 = usa durata default dell'abilità)")]
    public float customSlowdownDuration = 15f;
    [Tooltip("Se true, usa una velocità personalizzata durante lo slowdown invece del moltiplicatore")]
    public bool useCustomSlowdown = false;
    [Tooltip("Velocità da applicare temporaneamente durante lo slowdown.")]
    public float customSlowdownFactor = 3f;

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
    [Tooltip("Se false, usa solo il cambio colore base senza emission quando rallentata")]
[SerializeField] private bool useEmissionForSlow = true;

    [Header("Slowdown FX")]
    [SerializeField] private CFXR_EffectController slowdownEffect;

    private MeshRenderer meshRenderer;
    private bool patinaActive = false;
    private float originalSpeed;
    
    // Per salvare i colori originali dei materiali
    private Dictionary<Material, Material> materialInstances = new Dictionary<Material, Material>();
    private Dictionary<Material, Color> originalBaseColors = new Dictionary<Material, Color>();
    private Dictionary<Material, Color> originalEmissionColors = new Dictionary<Material, Color>();
    
    // NUOVO: Salva i materiali originali al primo accesso
    private Material[] originalMaterials = null;
    private bool materialsInitialized = false;

    void Awake()
    {
        meshRenderer = GetComponentInChildren<MeshRenderer>();
        if (meshRenderer == null)
        {
            Debug.LogWarning($"[MovingPlatform] Nessun MeshRenderer trovato su {gameObject.name}");
        }
        else
        {
            // Salva i colori originali dei materiali SHARED (non istanze)
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
        // Controlla se la piattaforma può essere rallentata
        if (!canBeSlowed)
        {
            Debug.Log($"[MovingPlatform] {gameObject.name} non può essere rallentata (canBeSlowed = false)");
            return;
        }

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
        // Anche per il restore controlliamo canBeSlowed per coerenza
        if (!canBeSlowed)
        {
            return;
        }

        if (useCustomSlowdown)
        {
            speed = originalSpeed;
            Debug.Log($"[MovingPlatform] Velocità ripristinata a {speed}");
        }
    }

    public void PlaySlowdownEffect(float duration = 1f)
    {
        // Controlla se la piattaforma può essere rallentata
        if (!canBeSlowed)
        {
            Debug.Log($"[MovingPlatform] {gameObject.name} non può essere rallentata, ignorando slowdown effect");
            return;
        }

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
        Debug.Log($"[MovingPlatform] *** SetOverlayActive({active}) chiamato su {gameObject.name} ***");
        
        if (meshRenderer == null) 
        {
            Debug.LogWarning($"[MovingPlatform] MeshRenderer nullo su {gameObject.name}");
            return;
        }
        
        Debug.Log($"[MovingPlatform] MeshRenderer OK, chiamando SetEmissiveOverlay({active})");
        SetEmissiveOverlay(active);
    }

    private void SetEmissiveOverlay(bool active)
{
    Debug.Log($"[MovingPlatform] SetEmissiveOverlay({active}) - inizio processing su {gameObject.name}");
    
    // INIZIALIZZA i materiali originali solo la prima volta
    if (!materialsInitialized)
    {
        originalMaterials = meshRenderer.sharedMaterials; // USA sharedMaterials per ottenere gli originali
        materialsInitialized = true;
        Debug.Log($"[MovingPlatform] Materiali originali salvati: {originalMaterials.Length}");
    }
    
    Material[] currentMaterials = meshRenderer.materials; // Questi possono essere istanze
    bool materialsChanged = false;

    Debug.Log($"[MovingPlatform] Materiali da processare: {currentMaterials.Length}");

    for (int i = 0; i < originalMaterials.Length; i++)
    {
        Material originalMat = originalMaterials[i];
        if (originalMat == null) continue;

        Debug.Log($"[MovingPlatform] Processando materiale {i}: {originalMat.name}");

        Material instanceMat;

        // Crea istanza del materiale SOLO se non esiste ancora
        if (!materialInstances.ContainsKey(originalMat))
        {
            Material newInstance = new Material(originalMat);
            materialInstances[originalMat] = newInstance;
            currentMaterials[i] = newInstance;
            materialsChanged = true;
            instanceMat = newInstance;
            Debug.Log($"[MovingPlatform] Creata PRIMA istanza per materiale {originalMat.name}");
        }
        else
        {
            // Usa l'istanza esistente
            instanceMat = materialInstances[originalMat];
            if (currentMaterials[i] != instanceMat)
            {
                currentMaterials[i] = instanceMat;
                materialsChanged = true;
            }
            Debug.Log($"[MovingPlatform] Usando istanza ESISTENTE per materiale {originalMat.name}");
        }

        if (active)
        {
            Debug.Log($"[MovingPlatform] ATTIVANDO overlay per materiale {instanceMat.name}");
            
            // EMISSION LUMINOSO (solo se abilitato)
            if (useEmissionForSlow && instanceMat.HasProperty("_EmissionColor"))
            {
                // Calcola colore emission HDR per massima luminosità
                Color hdrEmission = overlayColor * overlayIntensity * hdrMultiplier;
                instanceMat.SetColor("_EmissionColor", hdrEmission);
                
                // Abilita emission
                instanceMat.EnableKeyword("_EMISSION");
                
                // Forza il material a essere emission-enabled
                instanceMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                
                Debug.Log($"[MovingPlatform] Emission attivata con colore {hdrEmission}");
            }
            else if (!useEmissionForSlow)
            {
                Debug.Log($"[MovingPlatform] Emission disabilitata per slowdown, uso solo color tint");
            }
            else if (!instanceMat.HasProperty("_EmissionColor"))
            {
                Debug.LogWarning($"[MovingPlatform] Materiale {instanceMat.name} non ha _EmissionColor");
            }

            // BASE COLOR TINT (sempre applicato quando overlay è attivo)
            if (instanceMat.HasProperty("_BaseColor"))
            {
                if (originalBaseColors.ContainsKey(originalMat))
                {
                    Color originalColor = originalBaseColors[originalMat];
                    // Mescola il colore originale con l'overlay
                    float tintStrength = useEmissionForSlow ? 0.3f : 0.6f; // Più intenso se non usi emission
                    Color tintedColor = Color.Lerp(originalColor, originalColor * overlayColor, tintStrength);
                    tintedColor.a = originalColor.a;
                    instanceMat.SetColor("_BaseColor", tintedColor);
                    Debug.Log($"[MovingPlatform] BaseColor tint applicato con intensità {tintStrength}");
                }
            }
        }
        else
        {
            Debug.Log($"[MovingPlatform] DISATTIVANDO overlay per materiale {instanceMat.name}");
            
            // Ripristina colori originali
            if (instanceMat.HasProperty("_EmissionColor") && originalEmissionColors.ContainsKey(originalMat))
            {
                Color originalEmission = originalEmissionColors[originalMat];
                instanceMat.SetColor("_EmissionColor", originalEmission);
                
                // Se l'originale non aveva emission, disabilitalo
                if (originalEmission == Color.black || originalEmission.maxColorComponent <= 0.01f)
                {
                    instanceMat.DisableKeyword("_EMISSION");
                    instanceMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                }
                
                Debug.Log($"[MovingPlatform] Emission disattivata, ripristinato colore originale {originalEmission}");
            }

            if (instanceMat.HasProperty("_BaseColor") && originalBaseColors.ContainsKey(originalMat))
            {
                instanceMat.SetColor("_BaseColor", originalBaseColors[originalMat]);
                Debug.Log($"[MovingPlatform] BaseColor ripristinato");
            }
        }
    }

    if (materialsChanged)
    {
        meshRenderer.materials = currentMaterials;
        Debug.Log($"[MovingPlatform] Materiali aggiornati nel renderer");
    }
    
    patinaActive = active;
    Debug.Log($"[MovingPlatform] SetEmissiveOverlay completato - patinaActive = {patinaActive}");
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

    // Metodo pubblico per controllare se la piattaforma può essere rallentata
    public bool CanBeSlowed()
    {
        return canBeSlowed;
    }
}