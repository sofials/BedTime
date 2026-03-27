using UnityEngine;
using UnityEditor;
using System;
using System.Diagnostics;
using System.Text;

#if UNITY_EDITOR
[InitializeOnLoad]
public class TransientArtifactErrorTracker
{
    static TransientArtifactErrorTracker()
    {
        // Sottoscrivi agli eventi di log
        Application.logMessageReceived += OnLogMessageReceived;
        EditorApplication.update += OnEditorUpdate;
        
        UnityEngine.Debug.Log("[TransientTracker] Error tracker attivato");
    }

    private static void OnLogMessageReceived(string logString, string stackTrace, LogType type)
    {
        // Cerca specificamente il nostro errore
        if (logString.Contains("TransientArtifactProvider") && 
            logString.Contains("call is not allowed"))
        {
            UnityEngine.Debug.LogError("=== TRANSIENT ARTIFACT ERROR DETECTED ===");
            UnityEngine.Debug.LogError($"Messaggio: {logString}");
            UnityEngine.Debug.LogError($"Stack Trace: {stackTrace}");
            UnityEngine.Debug.LogError($"Timestamp: {DateTime.Now}");
            
            // Ottieni lo stack trace completo del thread corrente
            StackTrace st = new StackTrace(true);
            StringBuilder sb = new StringBuilder();
            
            for (int i = 0; i < st.FrameCount; i++)
            {
                StackFrame sf = st.GetFrame(i);
                sb.AppendLine($"Frame {i}: {sf.GetMethod()?.DeclaringType?.Name}.{sf.GetMethod()?.Name}");
                sb.AppendLine($"  File: {sf.GetFileName()}:{sf.GetFileLineNumber()}");
            }
            
            UnityEngine.Debug.LogError($"Call Stack Completo:\n{sb.ToString()}");
            UnityEngine.Debug.LogError("===========================================");
        }
    }

    private static void OnEditorUpdate()
    {
        // Monitora cambiamenti negli asset durante l'update
        if (EditorApplication.isCompiling)
        {
            UnityEngine.Debug.Log("[TransientTracker] Compilazione in corso...");
        }
    }
}

// Script alternativo per monitoraggio più aggressivo
[InitializeOnLoad]
public class AssetDatabaseTracker
{
    static AssetDatabaseTracker()
    {
        AssetDatabase.importPackageStarted += OnImportPackageStarted;
        AssetDatabase.importPackageCompleted += OnImportPackageCompleted;
        AssetDatabase.importPackageFailed += OnImportPackageFailed;
        
        // Hook per refresh asset database
        EditorApplication.projectChanged += OnProjectChanged;
    }

    private static void OnImportPackageStarted(string packageName)
    {
        UnityEngine.Debug.Log($"[AssetTracker] Import started: {packageName}");
    }

    private static void OnImportPackageCompleted(string packageName)
    {
        UnityEngine.Debug.Log($"[AssetTracker] Import completed: {packageName}");
    }

    private static void OnImportPackageFailed(string packageName, string errorMessage)
    {
        UnityEngine.Debug.LogError($"[AssetTracker] Import failed: {packageName} - {errorMessage}");
    }

    private static void OnProjectChanged()
    {
        UnityEngine.Debug.Log("[AssetTracker] Project changed detected");
    }
}

// Menu per debugging manuale
public class TransientArtifactDebugMenu
{
    [MenuItem("Debug/Transient Artifacts/Show Asset Database Info")]
    public static void ShowAssetDatabaseInfo()
    {
        UnityEngine.Debug.Log("=== ASSET DATABASE INFO ===");
        UnityEngine.Debug.Log($"Is Asset Database Ready: {AssetDatabase.IsAssetImportWorkerProcess()}");
        UnityEngine.Debug.Log($"Can Open Asset In Editor: {AssetDatabase.CanOpenAssetInEditor(0)}");
        
        // Lista tutti gli asset che potrebbero causare problemi
        string[] allAssets = AssetDatabase.GetAllAssetPaths();
        int problematicAssets = 0;
        
        foreach (string assetPath in allAssets)
        {
            if (string.IsNullOrEmpty(assetPath) || assetPath.StartsWith("Packages/"))
                continue;
                
            var obj = AssetDatabase.LoadMainAssetAtPath(assetPath);
            if (obj == null)
            {
                UnityEngine.Debug.LogWarning($"Asset problematico: {assetPath}");
                problematicAssets++;
            }
        }
        
        UnityEngine.Debug.Log($"Asset problematici trovati: {problematicAssets}");
        UnityEngine.Debug.Log("=============================");
    }

    [MenuItem("Debug/Transient Artifacts/Force Refresh Assets")]
    public static void ForceRefreshAssets()
    {
        UnityEngine.Debug.Log("Forzando refresh asset database...");
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        UnityEngine.Debug.Log("Refresh completato");
    }

    [MenuItem("Debug/Transient Artifacts/Clear Console And Test")]
    public static void ClearConsoleAndTest()
    {
        // Pulisci console
        var assembly = System.Reflection.Assembly.GetAssembly(typeof(SceneView));
        var type = assembly.GetType("UnityEditor.LogEntries");
        var method = type.GetMethod("Clear");
        method.Invoke(new object(), null);
        
        UnityEngine.Debug.Log("Console pulita - monitorando per nuovi errori TransientArtifact...");
    }
}
#endif