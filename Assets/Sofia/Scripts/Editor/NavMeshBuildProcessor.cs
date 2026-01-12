using UnityEngine;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine.AI;
using Unity.AI.Navigation;

/// <summary>
/// Assicura che il NavMesh possa essere rebakato durante il runtime in una build
/// </summary>
public class NavMeshBuildProcessor : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        Debug.Log("[NavMeshBuildProcessor] Preparazione build con supporto NavMesh runtime baking...");

        // Trova tutte le NavMeshSurface nella scena
        NavMeshSurface[] allSurfaces = Object.FindObjectsByType<NavMeshSurface>(FindObjectsSortMode.None);

        if (allSurfaces.Length == 0)
        {
            Debug.LogWarning("[NavMeshBuildProcessor] Nessuna NavMeshSurface trovata nelle scene. Il rebaking runtime potrebbe non funzionare.");
            return;
        }

        // Forza il baking di tutte le NavMeshSurface prima della build
        int bakedCount = 0;
        foreach (NavMeshSurface surface in allSurfaces)
        {
            if (surface != null && surface.gameObject.activeInHierarchy)
            {
                try
                {
                    surface.BuildNavMesh();
                    bakedCount++;
                    Debug.Log($"[NavMeshBuildProcessor] ✅ NavMesh '{surface.name}' bakata per la build");
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[NavMeshBuildProcessor] ❌ Errore nel baking di '{surface.name}': {e.Message}");
                }
            }
        }

        Debug.Log($"[NavMeshBuildProcessor] Build preparata con {bakedCount}/{allSurfaces.Length} NavMeshSurface bakate");
    }
}
