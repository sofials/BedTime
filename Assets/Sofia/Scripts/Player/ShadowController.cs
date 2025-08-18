using UnityEngine;

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
    
    // SEMPLICE ANTI-FLICKER: stato precedente
    private bool wasShadowActive = false;
    
    void Start()
    {
        if (raycastStartPoint == null)
        {
            Debug.LogError("RaycastStartPoint non assegnato!");
            return;
        }
        
        if (teleportAbility == null)
        {
            teleportAbility = GetComponent<TeleportAbility>();
            if (teleportAbility == null)
            {
                teleportAbility = GetComponentInChildren<TeleportAbility>();
            }
            
            if (teleportAbility != null)
            {
                Debug.Log($"TeleportAbility trovato automaticamente: {teleportAbility.name}");
            }
        }
        
        CreateProceduralShadow();
        Debug.Log($"ShadowController inizializzato. Raycast da: {raycastStartPoint.name}");
    }
    
    void Update()
    {
        if (shadowObject == null || raycastStartPoint == null) return;
        
        // Controlla teleport SOLO se cambia stato
        bool isTeleporting = teleportAbility != null && teleportAbility.IsActive;
        
        if (isTeleporting)
        {
            // Disabilita SOLO se non era già disabilitata
            if (shadowObject.gameObject.activeSelf)
            {
                shadowObject.gameObject.SetActive(false);
            }
            return;
        }
        
        UpdateShadow();
    }
    
    void UpdateShadow()
    {
        // RAYCAST SEMPLIFICATO - offset fisso più sicuro
        Vector3 raycastOrigin = raycastStartPoint.position + Vector3.up * 2f; // 2 metri fissi
        Vector3 raycastDirection = Vector3.down;
        float totalDistance = raycastDistance + 2f;
        
        RaycastHit hit;
        bool hasHit = Physics.Raycast(raycastOrigin, raycastDirection, out hit, totalDistance, groundLayer);
        
        if (showDebugRays)
        {
            Color rayColor = hasHit ? Color.green : Color.red;
            Debug.DrawRay(raycastOrigin, raycastDirection * totalDistance, rayColor);
        }
        
        if (hasHit)
        {
            // Posizione ombra
            Vector3 shadowPosition = new Vector3(
                raycastStartPoint.position.x,
                hit.point.y + 0.1f, // Offset più grande per sicurezza
                raycastStartPoint.position.z
            );
            
            shadowObject.position = shadowPosition;
            
            // Rotazione semplificata - solo per terreni molto inclinati
            Vector3 terrainNormal = hit.normal;
            float terrainAngle = Vector3.Angle(Vector3.up, terrainNormal);
            
            if (terrainAngle > 15f) // Solo se il terreno è molto inclinato
            {
                Quaternion surfaceRotation = Quaternion.FromToRotation(Vector3.up, terrainNormal);
                Quaternion baseRotation = Quaternion.Euler(90, 0, 0);
                shadowObject.rotation = surfaceRotation * baseRotation;
            }
            else
            {
                // Terreno piatto - rotazione fissa
                shadowObject.rotation = Quaternion.Euler(90, 0, 0);
            }
            
            // Scala basata su distanza
            float playerHeight = raycastStartPoint.position.y;
            float groundHeight = hit.point.y;
            float distanceToGround = playerHeight - groundHeight;
            
            float normalizedHeight = Mathf.Clamp01(distanceToGround / maxHeight);
            float currentScale = Mathf.Lerp(baseScale, baseScale * minScale, normalizedHeight);
            
            shadowObject.localScale = Vector3.one * currentScale;
            
            // Attiva SOLO se non era attiva
            if (!wasShadowActive)
            {
                shadowObject.gameObject.SetActive(true);
                wasShadowActive = true;
            }
        }
        else
        {
            // Disattiva SOLO se era attiva
            if (wasShadowActive)
            {
                shadowObject.gameObject.SetActive(false);
                wasShadowActive = false;
            }
        }
    }
    
    void CreateProceduralShadow()
    {
        proceduralShadowObject = new GameObject("ProceduralShadow");
        
        shadowMeshFilter = proceduralShadowObject.AddComponent<MeshFilter>();
        shadowRenderer = proceduralShadowObject.AddComponent<MeshRenderer>();
        
        shadowMeshFilter.mesh = GenerateCircleMesh();
        
        // Materiale semplificato
        Material shadowMat;
        if (customShadowMaterial != null)
        {
            shadowMat = customShadowMaterial;
        }
        else if (proceduralSettings.shadowMaterial != null)
        {
            shadowMat = proceduralSettings.shadowMaterial;
        }
        else
        {
            // Usa sempre Unlit/Color per semplicità
            shadowMat = new Material(Shader.Find("Unlit/Color"));
            shadowMat.color = new Color(0.2f, 0.2f, 0.2f, 0.4f);
        }
        
        shadowRenderer.material = shadowMat;
        
        // Disabilita ombre per evitare conflitti
        shadowRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        shadowRenderer.receiveShadows = false;
        
        proceduralShadowObject.transform.rotation = Quaternion.Euler(90, 0, 0);
        proceduralShadowObject.transform.localScale = Vector3.one * baseScale;
        proceduralShadowObject.transform.position = raycastStartPoint.position + Vector3.down * 8f;
        
        // INIZIA DISATTIVATA
        proceduralShadowObject.SetActive(false);
        wasShadowActive = false;
        
        shadowObject = proceduralShadowObject.transform;
    }
    
    Mesh GenerateCircleMesh()
    {
        Mesh mesh = new Mesh();
        mesh.name = "ProceduralCircle";
        
        int segments = proceduralSettings.segments;
        float radius = proceduralSettings.radius;
        
        Vector3[] vertices = new Vector3[segments + 1];
        Vector2[] uvs = new Vector2[segments + 1];
        int[] triangles = new int[segments * 3];
        
        vertices[0] = Vector3.zero;
        uvs[0] = new Vector2(0.5f, 0.5f);
        
        for (int i = 0; i < segments; i++)
        {
            float angle = (float)i / segments * Mathf.PI * 2f;
            float x = Mathf.Cos(angle) * radius;
            float y = Mathf.Sin(angle) * radius;
            
            vertices[i + 1] = new Vector3(x, y, 0);
            
            uvs[i + 1] = new Vector2(
                0.5f + x / radius * 0.5f,
                0.5f + y / radius * 0.5f
            );
        }
        
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
        
        return mesh;
    }
    
    public void ForceDisableShadow()
    {
        if (shadowObject != null)
        {
            shadowObject.gameObject.SetActive(false);
            wasShadowActive = false;
        }
    }
    
    public void ForceEnableShadow()
    {
        if (shadowObject != null)
        {
            shadowObject.gameObject.SetActive(true);
            wasShadowActive = true;
        }
    }
    
    [ContextMenu("Regenerate Procedural Shadow")]
    public void RegenerateProceduralShadow()
    {
        if (proceduralShadowObject != null)
        {
            DestroyImmediate(proceduralShadowObject);
        }
        
        CreateProceduralShadow();
    }
    
    public void SetShadowColor(Color newColor)
    {
        if (shadowRenderer != null)
        {
            shadowRenderer.material.color = newColor;
        }
        proceduralSettings.shadowColor = newColor;
    }
    
    public void SetShadowAlpha(float alpha)
    {
        Color currentColor = proceduralSettings.shadowColor;
        currentColor.a = Mathf.Clamp01(alpha);
        SetShadowColor(currentColor);
    }
    
    void OnDrawGizmosSelected()
    {
        if (raycastStartPoint == null) return;
        
        Vector3 startPos = raycastStartPoint.position;
        
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(startPos, 0.3f);
        
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(startPos, Vector3.down * raycastDistance);
        
        #if UNITY_EDITOR
        UnityEditor.Handles.Label(startPos + Vector3.up * 0.5f, raycastStartPoint.name);
        #endif
    }
}