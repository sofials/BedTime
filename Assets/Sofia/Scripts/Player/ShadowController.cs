using UnityEngine;
using System.Collections;

[System.Serializable]
public class ProceduralShadowSettings
{
    [Header("Circle Settings")]
    public int segments = 32;
    public float radius = 1f;
    public Color shadowColor = new Color(0f, 0f, 0f, 0.5f);
    public Material shadowMaterial = null;
}

public class ShadowController : MonoBehaviour
{
    [Header("References")]
    public Transform raycastStartPoint;
    
    [Header("Raycast Settings")]
    public float raycastDistance = 20f;
    public LayerMask groundLayer = 1;
    
    [Header("Shadow Settings")]
    public float baseScale = 1f;
    public float minScale = 0.3f;
    public float maxHeight = 10f;
    
    [Header("Performance")]
    [Range(1, 10)]
    public int updateFrequency = 2; // Ogni N frame
    [Range(0.01f, 0.5f)]
    public float minMovementThreshold = 0.1f; // Distanza minima per aggiornare
    
    [Header("Smoothing")]
    public bool useSmoothTransitions = true;
    [Range(1f, 20f)]
    public float positionSmoothSpeed = 10f;
    [Range(1f, 20f)]
    public float scaleSmoothSpeed = 8f;
    
    [Header("Jump Integration")]
    public bool onlyShowWhenJumping = true;
    [Range(0.1f, 5f)]
    public float jumpDetectionHeight = 1.5f; // Altezza minima da terra per considerare "salto"
    
    [Header("Procedural Shadow")]
    public ProceduralShadowSettings proceduralSettings = new ProceduralShadowSettings();
    [Space]
    public Material customShadowMaterial;
    
    [Header("Debug")]
    public bool showDebugRays = true;
    
    [Header("Teleport Integration")]
    public TeleportAbility teleportAbility;
    
    private GameObject proceduralShadowObject;
    private MeshRenderer shadowRenderer;
    private MeshFilter shadowMeshFilter;
    private Transform shadowObject;
    
    // Ottimizzazioni prestazioni
    private int frameCounter = 0;
    private Vector3 lastPlayerPosition;
    private Vector3 lastShadowPosition;
    private Vector3 targetShadowPosition;
    private float targetScale;
    private float currentScale;
    
    // Cache per evitare allocazioni
    private RaycastHit hitInfo;
    private readonly Vector3 rayOffset = Vector3.up * 2f;
    private readonly Vector3 shadowOffset = Vector3.up * 0.1f;
    
    // Anti-flicker migliorato
    private bool wasShadowActive = false;
    private bool isTransitioning = false;
    private float transitionTimer = 0f;
    private const float TRANSITION_DURATION = 0.1f;
    
    // Jump detection
    private ThirdPersonController playerController;
    
    // Pool per ottimizzazione
    private static readonly int ColorPropertyID = Shader.PropertyToID("_Color");
    
    void Start()
    {
        ValidateComponents();
        SetupJumpDetection();
        CreateProceduralShadow();
        InitializeCache();
        
        Debug.Log($"ShadowController inizializzato. Raycast da: {raycastStartPoint?.name ?? "NULL"}");
        Debug.Log($"Jump-only mode: {onlyShowWhenJumping}");
    }
    
    void ValidateComponents()
    {
        if (raycastStartPoint == null)
        {
            Debug.LogError("RaycastStartPoint non assegnato!");
            return;
        }
        
        if (teleportAbility == null)
        {
            teleportAbility = GetComponent<TeleportAbility>() ?? GetComponentInChildren<TeleportAbility>();
            
            if (teleportAbility != null)
            {
                Debug.Log($"TeleportAbility trovato automaticamente: {teleportAbility.name}");
            }
        }
    }
    
    void SetupJumpDetection()
    {
        if (!onlyShowWhenJumping) return;
        
        // Cerca il ThirdPersonController
        playerController = GetComponent<ThirdPersonController>();
        if (playerController == null)
        {
            playerController = GetComponentInParent<ThirdPersonController>();
        }
        if (playerController == null)
        {
            playerController = FindFirstObjectByType<ThirdPersonController>();
        }
        
        if (playerController != null)
        {
            Debug.Log($"Jump detection: ThirdPersonController trovato su {playerController.name}");
        }
        else
        {
            Debug.LogWarning("Nessun ThirdPersonController trovato per jump detection!");
        }
    }
    
    void InitializeCache()
    {
        if (raycastStartPoint != null)
        {
            lastPlayerPosition = raycastStartPoint.position;
            currentScale = baseScale;
            targetScale = baseScale;
        }
    }
    
    void Update()
    {
        if (shadowObject == null || raycastStartPoint == null) return;
        
        // Controllo teleport ottimizzato
        if (IsTeleporting())
        {
            HandleTeleportState();
            return;
        }
        
        // Controllo modalità salto
        if (onlyShowWhenJumping && !IsJumping())
        {
            HandleNotJumpingState();
            return;
        }
        
        // Aggiornamento con frequenza ridotta
        frameCounter++;
        if (frameCounter >= updateFrequency)
        {
            frameCounter = 0;
            
            // Solo se il player si è mosso abbastanza
            if (HasPlayerMovedSignificantly())
            {
                PerformRaycastUpdate();
                lastPlayerPosition = raycastStartPoint.position;
            }
        }
        
        // Smooth transitions sempre attive
        if (useSmoothTransitions)
        {
            ApplySmoothTransitions();
        }
        
        HandleTransitions();
    }
    
    bool IsTeleporting()
    {
        return teleportAbility != null && teleportAbility.IsActive;
    }
    
    void HandleTeleportState()
    {
        if (shadowObject.gameObject.activeSelf)
        {
            shadowObject.gameObject.SetActive(false);
            wasShadowActive = false;
        }
    }
    
    // OPTION 1: Remove the unused variable (Simplest fix)
// Just delete this line since you're not using it:
// private bool wasGrounded = true; // DELETE THIS LINE

// OPTION 2: Use the variable for improved jump detection (Better approach)
// Replace your IsJumping() method with this enhanced version:

bool IsJumping()
{
    if (!onlyShowWhenJumping) return true; // Se il modo salto è disabilitato, sempre "saltando"

    // Metodo 1: Usa ThirdPersonController (preferito)
    // Usa IsActuallyJumping() che verifica se il player ha premuto il tasto di salto,
    // non semplicemente se non è a terra (evita l'ombra su superfici inclinate)
    if (playerController != null)
    {
        return playerController.IsActuallyJumping();
    }

    // Metodo 2: Fallback - raycast veloce verso il basso per rilevare distanza da terra
    // Questo fallback non distingue tra salto vero e perdita di contatto, ma è meglio di niente
    Vector3 rayStart = raycastStartPoint.position;
    bool isNearGround = Physics.Raycast(rayStart, Vector3.down, jumpDetectionHeight, groundLayer);

    return !isNearGround;
}
    
    void HandleNotJumpingState()
    {
        if (shadowObject.gameObject.activeSelf)
        {
            shadowObject.gameObject.SetActive(false);
            wasShadowActive = false;
        }
    }
    
    bool HasPlayerMovedSignificantly()
    {
        return Vector3.Distance(lastPlayerPosition, raycastStartPoint.position) > minMovementThreshold;
    }
    
    void PerformRaycastUpdate()
    {
        Vector3 raycastOrigin = raycastStartPoint.position + rayOffset;
        Vector3 raycastDirection = Vector3.down;
        float totalDistance = raycastDistance + rayOffset.y;
        
        bool hasHit = Physics.Raycast(raycastOrigin, raycastDirection, out hitInfo, totalDistance, groundLayer);
        
        if (showDebugRays)
        {
            Color rayColor = hasHit ? Color.green : Color.red;
            Debug.DrawRay(raycastOrigin, raycastDirection * totalDistance, rayColor, Time.deltaTime * updateFrequency);
        }
        
        if (hasHit)
        {
            UpdateShadowFromHit();
        }
        else
        {
            HandleNoHit();
        }
    }
    
    void UpdateShadowFromHit()
    {
        // Calcola nuova posizione target
        targetShadowPosition = new Vector3(
            raycastStartPoint.position.x,
            hitInfo.point.y + shadowOffset.y,
            raycastStartPoint.position.z
        );
        
        // Rotazione ottimizzata per terreni inclinati
        UpdateShadowRotation();
        
        // Calcola scala target
        CalculateTargetScale();
        
        // Attiva shadow se necessario
        ActivateShadowIfNeeded();
    }
    
    void UpdateShadowRotation()
    {
        float terrainAngle = Vector3.Angle(Vector3.up, hitInfo.normal);
        
        if (terrainAngle > 15f)
        {
            Quaternion surfaceRotation = Quaternion.FromToRotation(Vector3.up, hitInfo.normal);
            Quaternion baseRotation = Quaternion.Euler(90, 0, 0);
            shadowObject.rotation = surfaceRotation * baseRotation;
        }
        else
        {
            shadowObject.rotation = Quaternion.Euler(90, 0, 0);
        }
    }
    
    void CalculateTargetScale()
    {
        float playerHeight = raycastStartPoint.position.y;
        float groundHeight = hitInfo.point.y;
        float distanceToGround = playerHeight - groundHeight;
        
        float normalizedHeight = Mathf.Clamp01(distanceToGround / maxHeight);
        targetScale = Mathf.Lerp(baseScale, baseScale * minScale, normalizedHeight);
    }
    
    void HandleNoHit()
    {
        DeactivateShadowIfNeeded();
    }
    
    void ApplySmoothTransitions()
    {
        if (shadowObject.gameObject.activeSelf)
        {
            // Smooth position
            if (Vector3.Distance(shadowObject.position, targetShadowPosition) > 0.01f)
            {
                shadowObject.position = Vector3.Lerp(
                    shadowObject.position,
                    targetShadowPosition,
                    positionSmoothSpeed * Time.deltaTime
                );
            }
            
            // Smooth scale
            if (Mathf.Abs(currentScale - targetScale) > 0.01f)
            {
                currentScale = Mathf.Lerp(currentScale, targetScale, scaleSmoothSpeed * Time.deltaTime);
                shadowObject.localScale = Vector3.one * currentScale;
            }
        }
    }
    
    void HandleTransitions()
    {
        if (isTransitioning)
        {
            transitionTimer += Time.deltaTime;
            if (transitionTimer >= TRANSITION_DURATION)
            {
                isTransitioning = false;
                transitionTimer = 0f;
            }
        }
    }
    
    void ActivateShadowIfNeeded()
    {
        if (!wasShadowActive && !isTransitioning)
        {
            shadowObject.gameObject.SetActive(true);
            wasShadowActive = true;
            StartTransition();
        }
    }
    
    void DeactivateShadowIfNeeded()
    {
        if (wasShadowActive && !isTransitioning)
        {
            shadowObject.gameObject.SetActive(false);
            wasShadowActive = false;
            StartTransition();
        }
    }
    
    void StartTransition()
    {
        isTransitioning = true;
        transitionTimer = 0f;
    }
    
    void CreateProceduralShadow()
    {
        proceduralShadowObject = new GameObject("ProceduralShadow");
        proceduralShadowObject.layer = gameObject.layer; // Eredita layer
        
        shadowMeshFilter = proceduralShadowObject.AddComponent<MeshFilter>();
        shadowRenderer = proceduralShadowObject.AddComponent<MeshRenderer>();
        
        shadowMeshFilter.mesh = GenerateCircleMesh();
        
        SetupShadowMaterial();
        ConfigureShadowRenderer();
        InitializeShadowTransform();
        
        shadowObject = proceduralShadowObject.transform;
    }
    
    void SetupShadowMaterial()
    {
        Material shadowMat = GetShadowMaterial();
        shadowRenderer.material = shadowMat;
        
        // Optimization: use MaterialPropertyBlock for dynamic properties
        if (shadowMat.HasProperty(ColorPropertyID))
        {
            var propertyBlock = new MaterialPropertyBlock();
            propertyBlock.SetColor(ColorPropertyID, proceduralSettings.shadowColor);
            shadowRenderer.SetPropertyBlock(propertyBlock);
        }
    }
    
    Material GetShadowMaterial()
    {
        if (customShadowMaterial != null)
            return customShadowMaterial;
        
        if (proceduralSettings.shadowMaterial != null)
            return proceduralSettings.shadowMaterial;
        
        // Fallback material
        Material shadowMat = new Material(Shader.Find("Unlit/Color"));
        shadowMat.color = new Color(0.2f, 0.2f, 0.2f, 0.4f);
        shadowMat.name = "Auto_ShadowMaterial";
        return shadowMat;
    }
    
    void ConfigureShadowRenderer()
    {
        shadowRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        shadowRenderer.receiveShadows = false;
        shadowRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        shadowRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
    }
    
    void InitializeShadowTransform()
    {
        proceduralShadowObject.transform.rotation = Quaternion.Euler(90, 0, 0);
        proceduralShadowObject.transform.localScale = Vector3.one * baseScale;
        proceduralShadowObject.transform.position = raycastStartPoint.position + Vector3.down * 8f;
        proceduralShadowObject.SetActive(false);
        
        wasShadowActive = false;
        targetShadowPosition = proceduralShadowObject.transform.position;
    }
    
    Mesh GenerateCircleMesh()
    {
        // Usa cache se possibile
        string meshName = $"ProceduralCircle_{proceduralSettings.segments}_{proceduralSettings.radius}";
        
        Mesh mesh = new Mesh();
        mesh.name = meshName;
        
        int segments = Mathf.Clamp(proceduralSettings.segments, 8, 64); // Limita per performance
        float radius = proceduralSettings.radius;
        
        Vector3[] vertices = new Vector3[segments + 1];
        Vector2[] uvs = new Vector2[segments + 1];
        int[] triangles = new int[segments * 3];
        
        // Centro
        vertices[0] = Vector3.zero;
        uvs[0] = new Vector2(0.5f, 0.5f);
        
        // Vertici del cerchio
        float angleStep = Mathf.PI * 2f / segments;
        for (int i = 0; i < segments; i++)
        {
            float angle = i * angleStep;
            float x = Mathf.Cos(angle) * radius;
            float y = Mathf.Sin(angle) * radius;
            
            vertices[i + 1] = new Vector3(x, y, 0);
            uvs[i + 1] = new Vector2(
                0.5f + x / radius * 0.5f,
                0.5f + y / radius * 0.5f
            );
        }
        
        // Triangoli
        for (int i = 0; i < segments; i++)
        {
            int triangleIndex = i * 3;
            triangles[triangleIndex] = 0;
            triangles[triangleIndex + 1] = (i + 1) % segments + 1;
            triangles[triangleIndex + 2] = i + 1;
        }
        
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        mesh.MarkDynamic(); // Optimization hint
        
        return mesh;
    }
    
    // Metodi pubblici ottimizzati
    public void ForceDisableShadow()
    {
        if (shadowObject != null && wasShadowActive)
        {
            shadowObject.gameObject.SetActive(false);
            wasShadowActive = false;
            StartTransition();
        }
    }
    
    public void ForceEnableShadow()
    {
        if (shadowObject != null && !wasShadowActive)
        {
            shadowObject.gameObject.SetActive(true);
            wasShadowActive = true;
            StartTransition();
        }
    }
    
    public void SetShadowColor(Color newColor)
    {
        if (shadowRenderer != null)
        {
            var propertyBlock = new MaterialPropertyBlock();
            propertyBlock.SetColor(ColorPropertyID, newColor);
            shadowRenderer.SetPropertyBlock(propertyBlock);
        }
        proceduralSettings.shadowColor = newColor;
    }
    
    public void SetShadowAlpha(float alpha)
    {
        Color currentColor = proceduralSettings.shadowColor;
        currentColor.a = Mathf.Clamp01(alpha);
        SetShadowColor(currentColor);
    }
    
    // Metodi di utilità per modalità salto
    public void SetJumpOnlyMode(bool enabled)
    {
        onlyShowWhenJumping = enabled;
        
        if (!enabled && shadowObject != null)
        {
            // Se disabilito la modalità salto, riattiva l'ombra se necessario
            ForceEnableShadow();
        }
    }
    
    public void SetJumpDetectionHeight(float height)
    {
        jumpDetectionHeight = Mathf.Clamp(height, 0.1f, 10f);
    }
    
    public bool IsCurrentlyJumping()
    {
        return IsJumping();
    }
    
    // Metodi di utilità
    public void SetUpdateFrequency(int frequency)
    {
        updateFrequency = Mathf.Clamp(frequency, 1, 10);
    }
    
    public void EnableSmoothTransitions(bool enable)
    {
        useSmoothTransitions = enable;
    }
    
    [ContextMenu("Regenerate Procedural Shadow")]
    public void RegenerateProceduralShadow()
    {
        if (proceduralShadowObject != null)
        {
            DestroyImmediate(proceduralShadowObject);
        }
        
        CreateProceduralShadow();
        InitializeCache();
    }
    
    void OnDrawGizmosSelected()
    {
        if (raycastStartPoint == null) return;
        
        Vector3 startPos = raycastStartPoint.position;
        
        // Player position
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(startPos, 0.3f);
        
        // Raycast direction
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(startPos + rayOffset, Vector3.down * (raycastDistance + rayOffset.y));
        
        // Movement threshold
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(startPos, minMovementThreshold);
        
        #if UNITY_EDITOR
        UnityEditor.Handles.Label(startPos + Vector3.up * 0.5f, 
            $"{raycastStartPoint.name}\nFreq: {updateFrequency}\nSmooth: {useSmoothTransitions}\nJump Only: {onlyShowWhenJumping}\nJumping: {(onlyShowWhenJumping ? IsJumping().ToString() : "N/A")}\nController: {(playerController != null ? "✓" : "✗")}");
        #endif
    }
    
    void OnDestroy()
    {
        // Cleanup
        if (proceduralShadowObject != null)
        {
            if (Application.isPlaying)
                Destroy(proceduralShadowObject);
            else
                DestroyImmediate(proceduralShadowObject);
        }
    }
}