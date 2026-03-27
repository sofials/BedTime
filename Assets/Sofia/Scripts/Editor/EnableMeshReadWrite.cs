using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// Script per abilitare automaticamente Read/Write su tutte le mesh del progetto
/// Questo è necessario per il rebake runtime della NavMesh nelle build
/// </summary>
public class EnableMeshReadWrite : Editor
{
    [MenuItem("Tools/NavMesh/Enable Read/Write on All Meshes")]
    public static void EnableReadWriteOnAllMeshes()
    {
        // Trova tutti i file .fbx, .obj, .blend, etc. nel progetto
        string[] modelGUIDs = AssetDatabase.FindAssets("t:Model");

        int processedCount = 0;
        int changedCount = 0;
        List<string> changedAssets = new List<string>();

        Debug.Log($"[EnableMeshReadWrite] Trovati {modelGUIDs.Length} modelli nel progetto...");

        foreach (string guid in modelGUIDs)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;

            if (importer != null)
            {
                processedCount++;

                // Verifica se Read/Write è già abilitato
                if (!importer.isReadable)
                {
                    importer.isReadable = true;
                    importer.SaveAndReimport();
                    changedCount++;
                    changedAssets.Add(path);

                    Debug.Log($"[EnableMeshReadWrite] ✅ Abilitato Read/Write su: {path}");
                }
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[EnableMeshReadWrite] ✅ COMPLETATO!");
        Debug.Log($"[EnableMeshReadWrite] Modelli processati: {processedCount}");
        Debug.Log($"[EnableMeshReadWrite] Modelli modificati: {changedCount}");

        if (changedCount > 0)
        {
            Debug.Log($"[EnableMeshReadWrite] Lista dei modelli modificati:");
            foreach (string asset in changedAssets)
            {
                Debug.Log($"  - {asset}");
            }

            EditorUtility.DisplayDialog(
                "Read/Write Abilitato",
                $"Read/Write è stato abilitato su {changedCount} modelli.\n\n" +
                $"La NavMesh ora può essere rebakata correttamente nelle build!",
                "OK"
            );
        }
        else
        {
            EditorUtility.DisplayDialog(
                "Nessuna Modifica Necessaria",
                $"Tutti i {processedCount} modelli hanno già Read/Write abilitato.",
                "OK"
            );
        }
    }

    [MenuItem("Tools/NavMesh/Check Read/Write Status")]
    public static void CheckReadWriteStatus()
    {
        string[] modelGUIDs = AssetDatabase.FindAssets("t:Model");

        int totalCount = 0;
        int readableCount = 0;
        int notReadableCount = 0;
        List<string> notReadableAssets = new List<string>();

        foreach (string guid in modelGUIDs)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;

            if (importer != null)
            {
                totalCount++;

                if (importer.isReadable)
                {
                    readableCount++;
                }
                else
                {
                    notReadableCount++;
                    notReadableAssets.Add(path);
                }
            }
        }

        Debug.Log($"[EnableMeshReadWrite] Status Check:");
        Debug.Log($"  Totale modelli: {totalCount}");
        Debug.Log($"  Con Read/Write: {readableCount}");
        Debug.Log($"  Senza Read/Write: {notReadableCount}");

        if (notReadableCount > 0)
        {
            Debug.Log($"[EnableMeshReadWrite] Modelli senza Read/Write:");
            foreach (string asset in notReadableAssets)
            {
                Debug.Log($"  - {asset}");
            }
        }

        EditorUtility.DisplayDialog(
            "Read/Write Status",
            $"Totale modelli: {totalCount}\n" +
            $"Con Read/Write: {readableCount}\n" +
            $"Senza Read/Write: {notReadableCount}\n\n" +
            (notReadableCount > 0
                ? "Usa 'Tools > NavMesh > Enable Read/Write on All Meshes' per abilitarlo automaticamente."
                : "Tutti i modelli sono pronti per il NavMesh runtime baking!"),
            "OK"
        );
    }
}
