using UnityEngine;
using UnityEditor;
using Unity.AI.Navigation;

[InitializeOnLoad]
public class NavMeshPlayModeHandler
{
    static NavMeshPlayModeHandler()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        // Quando ESCI dal Play Mode (torna all'Editor)
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            Debug.Log("[NavMeshPlayModeHandler] Uscito dal Play Mode - Rebaking NavMesh...");
            
            // Trova tutte le NavMeshSurface nella scena e rebaka
            NavMeshSurface[] surfaces = Object.FindObjectsByType<NavMeshSurface>(FindObjectsSortMode.None);
            
            foreach (NavMeshSurface surface in surfaces)
            {
                if (surface != null && surface.gameObject.activeInHierarchy)
                {
                    surface.BuildNavMesh();
                    Debug.Log($"[NavMeshPlayModeHandler] ✅ Rebakata NavMesh: {surface.name}");
                }
            }
            
            Debug.Log($"[NavMeshPlayModeHandler] Rebake completato per {surfaces.Length} NavMeshSurface");
        }
    }
}