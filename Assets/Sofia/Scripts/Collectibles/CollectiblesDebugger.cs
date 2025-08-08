using UnityEngine;

public class CollectiblesDebugger : MonoBehaviour
{
    [Header("Debug Settings")]
    [SerializeField] private bool autoDebugOnStart = true;
    [SerializeField] private KeyCode debugKey = KeyCode.F1;
    
    private void Start()
    {
        if (autoDebugOnStart)
        {
            DebugAllCollectibles();
        }
    }
    
    private void Update()
    {
        if (Input.GetKeyDown(debugKey))
        {
            DebugAllCollectibles();
        }
    }
    
    [ContextMenu("Debug All Collectibles")]
    public void DebugAllCollectibles()
    {
        Debug.Log("=== COLLECTIBLES DEBUG REPORT ===");
        
        // Trova tutti i collectibles nella scena
        Collectibles[] allCollectibles = FindObjectsByType<Collectibles>(FindObjectsSortMode.None);
        Memories[] allMemories = FindObjectsByType<Memories>(FindObjectsSortMode.None);
        Presents[] allPresents = FindObjectsByType<Presents>(FindObjectsSortMode.None);
        
        Debug.Log($"Trovati: {allCollectibles.Length} Collectibles totali");
        Debug.Log($"- {allMemories.Length} Memories");
        Debug.Log($"- {allPresents.Length} Presents");
        
        // Debug individuale
        foreach (Collectibles collectible in allCollectibles)
        {
            DebugSingleCollectible(collectible);
        }
        
        Debug.Log("=== FINE DEBUG REPORT ===");
    }
    
    private void DebugSingleCollectible(Collectibles collectible)
    {
        if (collectible == null) return;
        
        string type = collectible.GetCollectibleType().ToString();
        string name = collectible.GetName();
        Vector3 pos = collectible.transform.position;
        bool isCollected = collectible.IsCollected();
        bool isInitialized = collectible.IsInitialized();
        
        Debug.Log($"\n--- {collectible.gameObject.name} ({type}) ---");
        Debug.Log($"Nome: {name} | Valore: {collectible.GetValue()}");
        Debug.Log($"Posizione: {pos}");
        Debug.Log($"Raccolto: {isCollected} | Inizializzato: {isInitialized}");
        
        // Verifica componenti essenziali
        DebugComponents(collectible);
        
        // Verifica visibilità
        DebugVisibility(collectible);
        
        // Debug specifico per tipo
        if (collectible is Memories memory)
        {
            DebugMemory(memory);
        }
        else if (collectible is Presents present)
        {
            DebugPresent(present);
        }
    }
    
    private void DebugComponents(Collectibles collectible)
    {
        GameObject obj = collectible.gameObject;
        
        // Renderer
        Renderer renderer = obj.GetComponent<Renderer>();
        Debug.Log($"Renderer: {(renderer != null ? "✓" : "✗")} " +
                 $"{(renderer != null && renderer.enabled ? "Abilitato" : "Disabilitato")}");
        
        // Collider
        Collider collider = obj.GetComponent<Collider>();
        Debug.Log($"Collider: {(collider != null ? "✓" : "✗")} " +
                 $"{(collider != null && collider.enabled ? "Abilitato" : "Disabilitato")} " +
                 $"{(collider != null && collider.isTrigger ? "Trigger" : "Solid")}");
        
        // AudioSource
        AudioSource audio = obj.GetComponent<AudioSource>();
        Debug.Log($"AudioSource: {(audio != null ? "✓" : "✗")} " +
                 $"{(audio != null && audio.isPlaying ? "Suonando" : "Silenzioso")}");
        
        // GameObject attivo
        Debug.Log($"GameObject attivo: {obj.activeInHierarchy}");
    }
    
    private void DebugVisibility(Collectibles collectible)
    {
        GameObject obj = collectible.gameObject;
        Renderer renderer = obj.GetComponent<Renderer>();
        
        if (renderer == null)
        {
            Debug.LogError("❌ PROBLEMA: Nessun Renderer trovato!");
            return;
        }
        
        if (!renderer.enabled)
        {
            Debug.LogError("❌ PROBLEMA: Renderer disabilitato!");
        }
        
        // Controlla se è visibile dalla main camera
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            Plane[] frustumPlanes = GeometryUtility.CalculateFrustumPlanes(mainCam);
            bool isVisible = GeometryUtility.TestPlanesAABB(frustumPlanes, renderer.bounds);
            Debug.Log($"Visibile dalla camera: {isVisible}");
            
            // Distanza dalla camera
            float distance = Vector3.Distance(mainCam.transform.position, obj.transform.position);
            Debug.Log($"Distanza dalla camera: {distance:F1}m");
        }
        
        // Controlla materiali
        if (renderer.materials != null && renderer.materials.Length > 0)
        {
            Debug.Log($"Materiali: {renderer.materials.Length}");
            for (int i = 0; i < renderer.materials.Length; i++)
            {
                Material mat = renderer.materials[i];
                if (mat == null)
                {
                    Debug.LogError($"❌ PROBLEMA: Materiale {i} è null!");
                }
                else
                {
                    Debug.Log($"  - {i}: {mat.name} (Shader: {mat.shader.name})");
                }
            }
        }
        else
        {
            Debug.LogError("❌ PROBLEMA: Nessun materiale assegnato!");
        }
    }
    
    private void DebugMemory(Memories memory)
    {
        Debug.Log("--- DEBUG MEMORY SPECIFICO ---");
        
        // Controlla billboard
        bool billboardEnabled = true; // Le memories hanno sempre billboard
        Camera targetCam = Camera.main; // Assumiamo main camera
        
        Debug.Log($"Billboard: {(billboardEnabled ? "Abilitato" : "Disabilitato")}");
        Debug.Log($"Target Camera: {(targetCam != null ? targetCam.name : "Nessuna")}");
        
        if (billboardEnabled && targetCam != null)
        {
            Vector3 directionToCamera = targetCam.transform.position - memory.transform.position;
            Debug.Log($"Direzione verso camera: {directionToCamera.normalized}");
            Debug.Log($"Rotazione attuale: {memory.transform.rotation.eulerAngles}");
            
            // Controlla se la rotazione ha senso
            if (directionToCamera.magnitude < 0.1f)
            {
                Debug.LogWarning("⚠️ PROBLEMA: Memory troppo vicina alla camera per il billboard!");
            }
        }
        
        // Controlla effetti CFXR
        CFXR_EffectController cfxr = memory.GetComponent<CFXR_EffectController>();
        Debug.Log($"CFXR Effect: {(cfxr != null ? "✓" : "✗")}");
        
        if (cfxr != null)
        {
            Debug.Log($"CFXR attivo: {cfxr.enabled}");
        }
    }
    
    private void DebugPresent(Presents present)
    {
        Debug.Log("--- DEBUG PRESENT SPECIFICO ---");
        
        // I present non dovrebbero mai avere billboard
        Debug.Log("Billboard: Disabilitato (corretto per Present)");
        Debug.Log($"Rotazione abilitata: Sì");
        Debug.Log($"Floating abilitato: Sì");
    }
    
    // Metodo per forzare re-inizializzazione
    [ContextMenu("Force Reinitialize All")]
    public void ForceReinitializeAll()
    {
        Collectibles[] allCollectibles = FindObjectsByType<Collectibles>(FindObjectsSortMode.None);
        
        Debug.Log($"Forzando re-inizializzazione di {allCollectibles.Length} collectibles...");
        
        foreach (Collectibles collectible in allCollectibles)
        {
            if (collectible != null && !collectible.IsCollected())
            {
                collectible.ForceInitialize();
                Debug.Log($"Re-inizializzato: {collectible.gameObject.name}");
            }
        }
        
        Debug.Log("Re-inizializzazione completata!");
    }
    
    // Metodo per resettare tutti i collectibles
    [ContextMenu("Reset All Collectibles")]
    public void ResetAllCollectibles()
    {
        Collectibles[] allCollectibles = FindObjectsByType<Collectibles>(FindObjectsSortMode.None);
        
        Debug.Log($"Resettando {allCollectibles.Length} collectibles...");
        
        foreach (Collectibles collectible in allCollectibles)
        {
            if (collectible != null)
            {
                collectible.ResetItem();
                Debug.Log($"Resettato: {collectible.gameObject.name}");
            }
        }
        
        Debug.Log("Reset completato!");
    }
    
    // Metodo per testare la visibilità dalla posizione del player
    [ContextMenu("Test Visibility From Player")]
    public void TestVisibilityFromPlayer()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            Debug.LogError("Player non trovato!");
            return;
        }
        
        Vector3 playerPos = player.transform.position;
        Collectibles[] allCollectibles = FindObjectsByType<Collectibles>(FindObjectsSortMode.None);
        
        Debug.Log($"=== TEST VISIBILITÀ DAL PLAYER ({playerPos}) ===");
        
        foreach (Collectibles collectible in allCollectibles)
        {
            if (collectible.IsCollected()) continue;
            
            Vector3 collectiblePos = collectible.transform.position;
            float distance = Vector3.Distance(playerPos, collectiblePos);
            
            // Raycast per vedere se c'è qualcosa che blocca la vista
            Vector3 direction = (collectiblePos - playerPos).normalized;
            RaycastHit hit;
            bool blocked = Physics.Raycast(playerPos + Vector3.up, direction, out hit, distance);
            
            Debug.Log($"{collectible.gameObject.name}: Distanza={distance:F1}m, Bloccato={blocked}");
            
            if (blocked && hit.collider.gameObject != collectible.gameObject)
            {
                Debug.LogWarning($"  ⚠️ Bloccato da: {hit.collider.gameObject.name}");
            }
        }
    }
}