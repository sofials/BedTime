using UnityEngine;

[System.Serializable]
public class ProceduralShadowSettings
{
    [Header("Circle Settings")]
    public int segments = 32;
    public float radius = 1f;
    public Color shadowColor = new Color(0f, 0f, 0f, 0.5f);
    public Material shadowMaterial = null; // Opzionale, usa default se null
}

public class ShadowController : MonoBehaviour
{
    [Header("References")]
    public Transform raycastStartPoint; // Punto da cui parte il raycast
    
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
    [Tooltip("Assegna un materiale personalizzato per l'ombra, altrimenti verrà creato automaticamente")]
    public Material customShadowMaterial;
    
    [Header("Debug")]
    public bool showDebugRays = true;
    
    private GameObject proceduralShadowObject;
    private MeshRenderer shadowRenderer;
    private MeshFilter shadowMeshFilter;
    private Transform shadowObject;
    
    void Start()
    {
        if (raycastStartPoint == null)
        {
            Debug.LogError("RaycastStartPoint non assegnato!");
            return;
        }
        
        CreateProceduralShadow();
        Debug.Log($"ShadowController inizializzato. Raycast da: {raycastStartPoint.name}");
    }
    
    void Update()
    {
        if (shadowObject == null || raycastStartPoint == null) return;
        
        UpdateShadow();
    }
    
    void CreateProceduralShadow()
    {
        // Crea GameObject per l'ombra procedurale
        proceduralShadowObject = new GameObject("ProceduralShadow");
        
        // Aggiungi componenti
        shadowMeshFilter = proceduralShadowObject.AddComponent<MeshFilter>();
        shadowRenderer = proceduralShadowObject.AddComponent<MeshRenderer>();
        
        // Genera il mesh del cerchio
        shadowMeshFilter.mesh = GenerateCircleMesh();
        
        // Configura il materiale
        Material shadowMat;
        if (customShadowMaterial != null)
        {
            // Usa il materiale personalizzato dall'Inspector
            shadowMat = customShadowMaterial;
            Debug.Log($"Usando materiale personalizzato: {customShadowMaterial.name}");
        }
        else if (proceduralSettings.shadowMaterial != null)
        {
            // Fallback al materiale nelle settings
            shadowMat = proceduralSettings.shadowMaterial;
        }
        else
        {
            // Crea materiale automatico come ultima risorsa
            shadowMat = new Material(Shader.Find("Unlit/Color"));
            if (shadowMat.shader == null)
            {
                shadowMat = new Material(Shader.Find("Standard"));
                Debug.LogWarning("Unlit/Color non trovato, uso Standard");
            }
            
            // Colore grigio ombra più trasparente
            shadowMat.color = new Color(0.2f, 0.2f, 0.2f, 0.3f);
            Debug.Log("Materiale automatico creato");
        }
        
        shadowRenderer.material = shadowMat;
        
        // ROTAZIONE FISSA: X=90 per orientare correttamente
        proceduralShadowObject.transform.rotation = Quaternion.Euler(90, 0, 0);
        
        // Scala iniziale normale
        proceduralShadowObject.transform.localScale = Vector3.one * baseScale;
        
        // Posizione iniziale (sarà aggiornata dall'Update)
        proceduralShadowObject.transform.position = raycastStartPoint.position + Vector3.down * 8f;
        
        // Usa l'ombra procedurale come shadowObject
        shadowObject = proceduralShadowObject.transform;
        
    }
    
    Mesh GenerateCircleMesh()
    {
        Mesh mesh = new Mesh();
        mesh.name = "ProceduralCircle";
        
        int segments = proceduralSettings.segments;
        float radius = proceduralSettings.radius;
        
        // Array per vertici, UV e triangoli
        Vector3[] vertices = new Vector3[segments + 1]; // +1 per il centro
        Vector2[] uvs = new Vector2[segments + 1];
        int[] triangles = new int[segments * 3];
        
        // Centro del cerchio
        vertices[0] = Vector3.zero;
        uvs[0] = new Vector2(0.5f, 0.5f);
        
        // Vertici del perimetro NEL PIANO XY (perpendicolare al raycast down)
        for (int i = 0; i < segments; i++)
        {
            float angle = (float)i / segments * Mathf.PI * 2f;
            float x = Mathf.Cos(angle) * radius;
            float y = Mathf.Sin(angle) * radius; // Y per piano XY
            
            vertices[i + 1] = new Vector3(x, y, 0); // Z=0 per piano XY
            
            // UV mapping circolare
            uvs[i + 1] = new Vector2(
                0.5f + x / radius * 0.5f,
                0.5f + y / radius * 0.5f
            );
        }
        
        // Triangoli (dal centro ai vertici del perimetro)
        // IMPORTANTE: ordine dei vertici per far guardare la superficie verso Z+
        for (int i = 0; i < segments; i++)
        {
            int triangleIndex = i * 3;
            triangles[triangleIndex] = 0; // Centro
            triangles[triangleIndex + 1] = (i + 1) % segments + 1; // Invertito per normale corretta
            triangles[triangleIndex + 2] = i + 1;
        }
        
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        
        return mesh;
    }
    
    void UpdateShadow()
    {
        // RAYCAST INTELLIGENTE: parte da sopra per evitare di essere dentro il collider
        Vector3 raycastOrigin = raycastStartPoint.position + Vector3.up * 1f; // 1 metro sopra
        Vector3 raycastDirection = Vector3.down;
        float totalDistance = raycastDistance + 1f; // Aggiungi la distanza dell'offset
        
        // RAYCAST 3D per terreno 3D
        RaycastHit hit;
        bool hasHit = Physics.Raycast(raycastOrigin, raycastDirection, out hit, totalDistance, groundLayer);
        
        // Debug ray visuale
        if (showDebugRays)
        {
            Color rayColor = hasHit ? Color.green : Color.red;
            Debug.DrawRay(raycastOrigin, raycastDirection * totalDistance, rayColor);
        }
        
        if (hasHit)
        {
            // ANCORATA AL TERRENO - X e Z del player, Y del terreno
            Vector3 shadowPosition = new Vector3(
                raycastStartPoint.position.x, // X del player
                hit.point.y + 0.01f,         // Y del terreno colpito
                raycastStartPoint.position.z  // Z del player
            );
            
            shadowObject.position = shadowPosition;
            
            // ADATTA ALLA PENDENZA - usa la normale del terreno per orientare l'ombra
            Vector3 terrainNormal = hit.normal;
            
            // Calcola la rotazione per allineare l'ombra alla superficie
            Quaternion surfaceRotation = Quaternion.FromToRotation(Vector3.up, terrainNormal);
            
            // Combina con la rotazione base (90 gradi su X per orientamento corretto)
            Quaternion baseRotation = Quaternion.Euler(90, 0, 0);
            shadowObject.rotation = surfaceRotation * baseRotation;
            
            // Calcola la scala basata sulla distanza VERTICALE tra player e terreno
            float playerHeight = raycastStartPoint.position.y;
            float groundHeight = hit.point.y;
            float distanceToGround = playerHeight - groundHeight;
            
            float normalizedHeight = Mathf.Clamp01(distanceToGround / maxHeight);
            float currentScale = Mathf.Lerp(baseScale, baseScale * minScale, normalizedHeight);
            
            shadowObject.localScale = Vector3.one * currentScale;
            shadowObject.gameObject.SetActive(true);
            
            // Debug info opzionale
            if (showDebugRays)
            {
                Debug.Log($"Player Y: {playerHeight:F2}, Ground Y: {groundHeight:F2}, Distance: {distanceToGround:F2}, Scale: {currentScale:F2}");
                Debug.Log($"Terrain normal: {terrainNormal}, Surface angle: {Vector3.Angle(Vector3.up, terrainNormal):F1}°");
            }
        }
        else
        {
            // Nessun terreno trovato
            shadowObject.gameObject.SetActive(false);
            
            if (showDebugRays)
            {
                Debug.LogWarning($"Raycast 3D da {raycastOrigin} non ha colpito nulla!");
            }
        }
    }
    
    // Metodo per rigenerare l'ombra con nuove impostazioni
    [ContextMenu("Regenerate Procedural Shadow")]
    public void RegenerateProceduralShadow()
    {
        if (proceduralShadowObject != null)
        {
            DestroyImmediate(proceduralShadowObject);
        }
        
        CreateProceduralShadow();
    }
    
    // Metodo per cambiare colore a runtime
    public void SetShadowColor(Color newColor)
    {
        if (shadowRenderer != null)
        {
            shadowRenderer.material.color = newColor;
        }
        proceduralSettings.shadowColor = newColor;
    }
    
    // Metodo per cambiare trasparenza a runtime
    public void SetShadowAlpha(float alpha)
    {
        Color currentColor = proceduralSettings.shadowColor;
        currentColor.a = Mathf.Clamp01(alpha);
        SetShadowColor(currentColor);
    }
    
    // Debug visuale nell'editor
    void OnDrawGizmosSelected()
    {
        if (raycastStartPoint == null) return;
        
        Vector3 startPos = raycastStartPoint.position;
        
        // Punto di partenza del raycast
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(startPos, 0.3f);
        
        // Linea del raycast 3D
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(startPos, Vector3.down * raycastDistance);
        
        // Label del punto di partenza
        #if UNITY_EDITOR
        UnityEditor.Handles.Label(startPos + Vector3.up * 0.5f, raycastStartPoint.name);
        #endif
    }
}