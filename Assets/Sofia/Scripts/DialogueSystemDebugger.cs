using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

#if UNITY_EDITOR
[InitializeOnLoad]
public class DialogueSystemDebugger
{
    private static bool isHookingValidation = false;
    
    static DialogueSystemDebugger()
    {
        // Ascolta quando la scena viene caricata
        UnityEditor.SceneManagement.EditorSceneManager.sceneOpened += OnSceneOpened;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }
    
    static void OnSceneOpened(UnityEngine.SceneManagement.Scene scene, UnityEditor.SceneManagement.OpenSceneMode mode)
    {
        // Aspetta un frame per assicurarsi che tutti gli oggetti siano inizializzati
        EditorApplication.delayCall += () => AnalyzeDialogueSystemsInScene();
    }
    
    static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode && !isHookingValidation)
        {
            // Hook into the validation when entering play mode
            EditorApplication.delayCall += () => HookIntoValidationMethod();
        }
    }
    
    [MenuItem("Tools/Debug DialogueSystems in Scene")]
    public static void HookIntoValidationMethod()
    {
        if (Application.isPlaying)
        {
            isHookingValidation = true;
            
            DialogueSystem[] allSystems = Object.FindObjectsByType<DialogueSystem>(FindObjectsSortMode.None);
            
            Debug.Log($"[ValidationHook] Trovati {allSystems.Length} DialogueSystems - Iniziando monitoraggio dettagliato...");
            
            foreach (DialogueSystem ds in allSystems)
            {
                if (ds != null)
                {
                    // Forza la chiamata al metodo di validazione e monitora i risultati
                    PerformDetailedValidation(ds);
                }
            }
        }
        else
        {
            Debug.LogWarning("[ValidationHook] Questo metodo deve essere chiamato in Play Mode!");
        }
    }
    
    static void PerformDetailedValidation(DialogueSystem ds)
    {
        string objectPath = GetGameObjectPath(ds.gameObject);
        
        Debug.Log($"[ValidationHook] ===== VALIDAZIONE DETTAGLIATA: {ds.name} =====");
        Debug.Log($"[ValidationHook] Path: {objectPath}");
        
        // Accedi direttamente ai campi privati per vedere i valori reali
        try
        {
            var enableLineEventsField = typeof(DialogueSystem).GetField("enableLineEvents", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var enablePreDialogueEventsField = typeof(DialogueSystem).GetField("enablePreDialogueEvents", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var enablePostDialogueEventsField = typeof(DialogueSystem).GetField("enablePostDialogueEvents", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var globalLineEventsField = typeof(DialogueSystem).GetField("globalLineEvents", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var preDialogueEventsField = typeof(DialogueSystem).GetField("preDialogueEvents", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var postDialogueEventsField = typeof(DialogueSystem).GetField("postDialogueEvents", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var dialogueLinesField = typeof(DialogueSystem).GetField("dialogueLines", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                
            if (enableLineEventsField != null && enablePreDialogueEventsField != null && enablePostDialogueEventsField != null)
            {
                bool enableLineEvents = (bool)enableLineEventsField.GetValue(ds);
                bool enablePreDialogueEvents = (bool)enablePreDialogueEventsField.GetValue(ds);
                bool enablePostDialogueEvents = (bool)enablePostDialogueEventsField.GetValue(ds);
                
                Debug.Log($"[ValidationHook] VALORI DIRETTI DAI CAMPI:");
                Debug.Log($"[ValidationHook]   enableLineEvents = {enableLineEvents}");
                Debug.Log($"[ValidationHook]   enablePreDialogueEvents = {enablePreDialogueEvents}");
                Debug.Log($"[ValidationHook]   enablePostDialogueEvents = {enablePostDialogueEvents}");
                
                // Ora simula esattamente la logica di ValidateLineEventsSetup()
                if (!enableLineEvents && !enablePreDialogueEvents && !enablePostDialogueEvents)
                {
                    Debug.Log($"[ValidationHook]   ✅ Tutti i sistemi eventi disabilitati - nessun warning dovrebbe apparire");
                    return;
                }
                
                Debug.Log($"[ValidationHook]   ⚠️ ALMENO UN SISTEMA EVENTI È ABILITATO - CONTROLLO DETTAGLIATO:");
                
                int totalEvents = 0;
                int dialogueLineEvents = 0;
                int globalEvents = 0;
                int preDialogueEventsCount = 0;
                int postDialogueEventsCount = 0;
                
                // Controlla eventi nelle DialogueLine
                if (dialogueLinesField != null)
                {
                    DialogueLine[] dialogueLines = (DialogueLine[])dialogueLinesField.GetValue(ds);
                    if (dialogueLines != null)
                    {
                        for (int i = 0; i < dialogueLines.Length; i++)
                        {
                            DialogueLine line = dialogueLines[i];
                            if (line.hasLineEvents && line.lineEvents != null)
                            {
                                dialogueLineEvents += line.lineEvents.Length;
                                totalEvents += line.lineEvents.Length;
                            }
                        }
                    }
                }
                
                // Controlla eventi globali
                if (globalLineEventsField != null)
                {
                    LineEvent[] globalLineEvents = (LineEvent[])globalLineEventsField.GetValue(ds);
                    if (globalLineEvents != null)
                    {
                        globalEvents = globalLineEvents.Length;
                        totalEvents += globalEvents;
                    }
                }
                
                Debug.Log($"[ValidationHook]   Eventi in DialogueLine: {dialogueLineEvents}");
                Debug.Log($"[ValidationHook]   Eventi globali: {globalEvents}");
                Debug.Log($"[ValidationHook]   Totale eventi linea: {totalEvents}");
                
                // Controlla eventi pre-dialogo
                if (enablePreDialogueEvents)
                {
                    if (preDialogueEventsField != null)
                    {
                        LineEvent[] preDialogueEvents = (LineEvent[])preDialogueEventsField.GetValue(ds);
                        preDialogueEventsCount = preDialogueEvents != null ? preDialogueEvents.Length : 0;
                        totalEvents += preDialogueEventsCount;
                        Debug.Log($"[ValidationHook]   Eventi pre-dialogo: {preDialogueEventsCount}");
                        
                        if (preDialogueEventsCount == 0)
                        {
                            Debug.LogError($"[ValidationHook]   ❌ PROBLEMA: Eventi pre-dialogo abilitati ma 0 eventi configurati!");
                        }
                    }
                }
                
                // Controlla eventi post-dialogo
                if (enablePostDialogueEvents)
                {
                    if (postDialogueEventsField != null)
                    {
                        LineEvent[] postDialogueEvents = (LineEvent[])postDialogueEventsField.GetValue(ds);
                        postDialogueEventsCount = postDialogueEvents != null ? postDialogueEvents.Length : 0;
                        totalEvents += postDialogueEventsCount;
                        Debug.Log($"[ValidationHook]   Eventi post-dialogo: {postDialogueEventsCount}");
                        
                        if (postDialogueEventsCount == 0)
                        {
                            Debug.LogError($"[ValidationHook]   ❌ PROBLEMA: Eventi post-dialogo abilitati ma 0 eventi configurati!");
                        }
                    }
                }
                
                // Identifica la condizione specifica per il warning "Sistema Eventi Linea abilitato ma nessun evento configurato!"
                if (enableLineEvents && totalEvents == 0)
                {
                    Debug.LogError($"[ValidationHook]   ❌ PROBLEMA IDENTIFICATO!");
                    Debug.LogError($"[ValidationHook]   QUESTO È IL DIALOGUESYSTEM CHE CAUSA IL WARNING:");
                    Debug.LogError($"[ValidationHook]   Nome: {ds.name}");
                    Debug.LogError($"[ValidationHook]   Path: {objectPath}");
                    Debug.LogError($"[ValidationHook]   Motivo: enableLineEvents = {enableLineEvents} ma totalEvents = {totalEvents}");
                }
                else if (enableLineEvents && (dialogueLineEvents + globalEvents) == 0)
                {
                    Debug.LogError($"[ValidationHook]   ❌ PROBLEMA IDENTIFICATO (solo eventi linea)!");
                    Debug.LogError($"[ValidationHook]   QUESTO È IL DIALOGUESYSTEM CHE CAUSA IL WARNING:");
                    Debug.LogError($"[ValidationHook]   Nome: {ds.name}");
                    Debug.LogError($"[ValidationHook]   Path: {objectPath}");
                    Debug.LogError($"[ValidationHook]   Motivo: enableLineEvents = {enableLineEvents} ma eventi linea = {dialogueLineEvents + globalEvents}");
                }
                else
                {
                    Debug.Log($"[ValidationHook]   ✅ Configurazione corretta per questo DialogueSystem");
                }
            }
            else
            {
                Debug.LogError($"[ValidationHook] Impossibile accedere ai campi privati di {ds.name}");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ValidationHook] Errore nell'analizzare {ds.name}: {e.Message}");
        }
        
        Debug.Log($"[ValidationHook] ===== FINE VALIDAZIONE: {ds.name} =====");
    }
    public static void AnalyzeDialogueSystemsInScene()
    {
        DialogueSystem[] allDialogueSystems = Object.FindObjectsByType<DialogueSystem>(FindObjectsSortMode.None);
        
        if (allDialogueSystems.Length == 0)
        {
            Debug.Log("[DialogueSystemDebugger] Nessun DialogueSystem trovato nella scena corrente.");
            return;
        }
        
        Debug.Log($"[DialogueSystemDebugger] 🔍 ANALISI DI {allDialogueSystems.Length} DIALOGUE SYSTEMS");
        Debug.Log("═══════════════════════════════════════════════════════════");
        
        int problematicoCount = 0;
        
        for (int i = 0; i < allDialogueSystems.Length; i++)
        {
            DialogueSystem ds = allDialogueSystems[i];
            if (ds == null) continue;
            
            // Analizza questo DialogueSystem
            bool hasProblems = AnalyzeSingleDialogueSystem(ds, i + 1);
            if (hasProblems) problematicoCount++;
        }
        
        Debug.Log("═══════════════════════════════════════════════════════════");
        Debug.Log($"[DialogueSystemDebugger] 📊 RIEPILOGO: {problematicoCount}/{allDialogueSystems.Length} DialogueSystems hanno problemi con gli eventi linea");
        
        if (problematicoCount > 0)
        {
            Debug.Log($"[DialogueSystemDebugger] ⚠️ Controlla i DialogueSystems evidenziati sopra per risolvere i warning!");
        }
        else
        {
            Debug.Log($"[DialogueSystemDebugger] ✅ Tutti i DialogueSystems sono configurati correttamente!");
        }
    }
    
    static bool AnalyzeSingleDialogueSystem(DialogueSystem ds, int index)
    {
        string objectPath = GetGameObjectPath(ds.gameObject);
        
        Debug.Log($"[DialogueSystemDebugger] 📋 #{index} - {ds.name}");
        Debug.Log($"[DialogueSystemDebugger]     Path: {objectPath}");
        Debug.Log($"[DialogueSystemDebugger]     GameObject Active: {ds.gameObject.activeInHierarchy}");
        Debug.Log($"[DialogueSystemDebugger]     Component Enabled: {ds.enabled}");
        
        bool hasLineEventProblems = false;
        
        // Controlla se il sistema eventi linea è abilitato
        bool lineEventsEnabled = ds.IsLineEventsEnabled();
        bool preDialogueEnabled = ds.IsPreDialogueEventsEnabled();
        bool postDialogueEnabled = ds.IsPostDialogueEventsEnabled();
        
        Debug.Log($"[DialogueSystemDebugger]     Eventi Linea Abilitati: {lineEventsEnabled}");
        Debug.Log($"[DialogueSystemDebugger]     Eventi Pre-Dialogo Abilitati: {preDialogueEnabled}");
        Debug.Log($"[DialogueSystemDebugger]     Eventi Post-Dialogo Abilitati: {postDialogueEnabled}");
        
        // Se nessun sistema eventi è abilitato, non dovrebbe dare warning
        if (!lineEventsEnabled && !preDialogueEnabled && !postDialogueEnabled)
        {
            Debug.Log($"[DialogueSystemDebugger]     ✅ Tutti i sistemi eventi sono disabilitati - OK");
            return false;
        }
        
        // Analizza eventi linea
        if (lineEventsEnabled)
        {
            int totalLineEvents = 0;
            
            // Conta eventi nelle DialogueLine
            DialogueLine[] dialogueLines = GetDialogueLines(ds);
            int dialogueLineEvents = 0;
            
            if (dialogueLines != null)
            {
                for (int i = 0; i < dialogueLines.Length; i++)
                {
                    DialogueLine line = dialogueLines[i];
                    if (line.hasLineEvents && line.lineEvents != null && line.lineEvents.Length > 0)
                    {
                        dialogueLineEvents += line.lineEvents.Length;
                    }
                }
            }
            
            // Conta eventi globali
            LineEvent[] globalEvents = ds.GetGlobalLineEvents();
            int globalEventsCount = globalEvents != null ? globalEvents.Length : 0;
            
            totalLineEvents = dialogueLineEvents + globalEventsCount;
            
            Debug.Log($"[DialogueSystemDebugger]     Eventi in DialogueLine: {dialogueLineEvents}");
            Debug.Log($"[DialogueSystemDebugger]     Eventi Globali: {globalEventsCount}");
            Debug.Log($"[DialogueSystemDebugger]     Totale Eventi Linea: {totalLineEvents}");
            
            if (totalLineEvents == 0)
            {
                Debug.LogWarning($"[DialogueSystemDebugger] ⚠️ PROBLEMA TROVATO!");
                Debug.LogWarning($"[DialogueSystemDebugger]     DialogueSystem '{ds.name}' ha eventi linea ABILITATI ma NESSUN EVENTO configurato!");
                Debug.LogWarning($"[DialogueSystemDebugger]     Path: {objectPath}");
                Debug.LogWarning($"[DialogueSystemDebugger]     Questo causerà il warning: 'Sistema Eventi Linea abilitato ma nessun evento configurato!'");
                hasLineEventProblems = true;
            }
        }
        
        // Analizza eventi pre-dialogo
        if (preDialogueEnabled)
        {
            LineEvent[] preEvents = ds.GetPreDialogueEvents();
            int preEventsCount = preEvents != null ? preEvents.Length : 0;
            
            Debug.Log($"[DialogueSystemDebugger]     Eventi Pre-Dialogo: {preEventsCount}");
            
            if (preEventsCount == 0)
            {
                Debug.LogWarning($"[DialogueSystemDebugger] ⚠️ PROBLEMA TROVATO!");
                Debug.LogWarning($"[DialogueSystemDebugger]     DialogueSystem '{ds.name}' ha eventi pre-dialogo ABILITATI ma NESSUN EVENTO configurato!");
                Debug.LogWarning($"[DialogueSystemDebugger]     Path: {objectPath}");
                hasLineEventProblems = true;
            }
        }
        
        // Analizza eventi post-dialogo
        if (postDialogueEnabled)
        {
            LineEvent[] postEvents = ds.GetPostDialogueEvents();
            int postEventsCount = postEvents != null ? postEvents.Length : 0;
            
            Debug.Log($"[DialogueSystemDebugger]     Eventi Post-Dialogo: {postEventsCount}");
            
            if (postEventsCount == 0)
            {
                Debug.LogWarning($"[DialogueSystemDebugger] ⚠️ PROBLEMA TROVATO!");
                Debug.LogWarning($"[DialogueSystemDebugger]     DialogueSystem '{ds.name}' ha eventi post-dialogo ABILITATI ma NESSUN EVENTO configurato!");
                Debug.LogWarning($"[DialogueSystemDebugger]     Path: {objectPath}");
                hasLineEventProblems = true;
            }
        }
        
        // Soluzioni suggerite
        if (hasLineEventProblems)
        {
            Debug.Log($"[DialogueSystemDebugger] 💡 SOLUZIONI POSSIBILI:");
            Debug.Log($"[DialogueSystemDebugger]     1. Disabilita il sistema eventi non utilizzato nell'Inspector");
            Debug.Log($"[DialogueSystemDebugger]     2. Configura almeno un evento per il sistema abilitato");
            Debug.Log($"[DialogueSystemDebugger]     3. Usa il codice: ds.SetLineEventsEnabled(false) per disabilitare via script");
        }
        else
        {
            Debug.Log($"[DialogueSystemDebugger]     ✅ Configurazione eventi corretta");
        }
        
        Debug.Log($"[DialogueSystemDebugger] ───────────────────────────────────");
        
        return hasLineEventProblems;
    }
    
    static string GetGameObjectPath(GameObject obj)
    {
        string path = obj.name;
        Transform parent = obj.transform.parent;
        
        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }
        
        return path;
    }
    
    // Metodo helper per ottenere DialogueLine[] tramite reflection (dato che è privato)
    static DialogueLine[] GetDialogueLines(DialogueSystem ds)
    {
        try
        {
            var field = typeof(DialogueSystem).GetField("dialogueLines", 
                System.Reflection.BindingFlags.NonPublic | 
                System.Reflection.BindingFlags.Public | 
                System.Reflection.BindingFlags.Instance);
            
            if (field != null)
            {
                return (DialogueLine[])field.GetValue(ds);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[DialogueSystemDebugger] Errore nell'accedere a dialogueLines: {e.Message}");
        }
        
        return new DialogueLine[0];
    }
}

// Versione Runtime per quando non sei in Editor
public class DialogueSystemRuntimeDebugger : MonoBehaviour
{
    [ContextMenu("Analyze All DialogueSystems")]
    public void AnalyzeDialogueSystemsRuntime()
    {
        DialogueSystem[] allDialogueSystems = FindObjectsByType<DialogueSystem>(FindObjectsSortMode.None);
        
        if (allDialogueSystems.Length == 0)
        {
            Debug.Log("[DialogueSystemDebugger] Nessun DialogueSystem trovato nella scena corrente.");
            return;
        }
        
        Debug.Log($"[DialogueSystemDebugger] 🔍 ANALISI RUNTIME DI {allDialogueSystems.Length} DIALOGUE SYSTEMS");
        
        int problemCount = 0;
        
        foreach (DialogueSystem ds in allDialogueSystems)
        {
            if (ds == null) continue;
            
            string objectName = ds.name;
            string hierarchyPath = GetHierarchyPath(ds.gameObject);
            
            bool lineEventsEnabled = ds.IsLineEventsEnabled();
            bool preDialogueEnabled = ds.IsPreDialogueEventsEnabled();
            bool postDialogueEnabled = ds.IsPostDialogueEventsEnabled();
            
            bool hasProblem = false;
            
            // Controlla eventi linea
            if (lineEventsEnabled)
            {
                LineEvent[] globalEvents = ds.GetGlobalLineEvents();
                int globalCount = globalEvents != null ? globalEvents.Length : 0;
                
                if (globalCount == 0)
                {
                    Debug.LogWarning($"[DialogueSystemDebugger] ⚠️ '{objectName}' ha eventi linea abilitati ma nessun evento globale!");
                    Debug.LogWarning($"[DialogueSystemDebugger]     Path: {hierarchyPath}");
                    hasProblem = true;
                }
            }
            
            // Controlla eventi pre-dialogo
            if (preDialogueEnabled)
            {
                LineEvent[] preEvents = ds.GetPreDialogueEvents();
                int preCount = preEvents != null ? preEvents.Length : 0;
                
                if (preCount == 0)
                {
                    Debug.LogWarning($"[DialogueSystemDebugger] ⚠️ '{objectName}' ha eventi pre-dialogo abilitati ma nessun evento!");
                    Debug.LogWarning($"[DialogueSystemDebugger]     Path: {hierarchyPath}");
                    hasProblem = true;
                }
            }
            
            // Controlla eventi post-dialogo
            if (postDialogueEnabled)
            {
                LineEvent[] postEvents = ds.GetPostDialogueEvents();
                int postCount = postEvents != null ? postEvents.Length : 0;
                
                if (postCount == 0)
                {
                    Debug.LogWarning($"[DialogueSystemDebugger] ⚠️ '{objectName}' ha eventi post-dialogo abilitati ma nessun evento!");
                    Debug.LogWarning($"[DialogueSystemDebugger]     Path: {hierarchyPath}");
                    hasProblem = true;
                }
            }
            
            if (hasProblem)
            {
                problemCount++;
                Debug.Log($"[DialogueSystemDebugger] 💡 Per '{objectName}': Disabilita i sistemi eventi non utilizzati o aggiungi eventi");
            }
        }
        
        if (problemCount > 0)
        {
            Debug.Log($"[DialogueSystemDebugger] 📊 Trovati {problemCount} DialogueSystems con problemi di configurazione eventi");
        }
        else
        {
            Debug.Log($"[DialogueSystemDebugger] ✅ Tutti i {allDialogueSystems.Length} DialogueSystems sono configurati correttamente!");
        }
    }
    
    string GetHierarchyPath(GameObject obj)
    {
        string path = obj.name;
        Transform parent = obj.transform.parent;
        
        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }
        
        return path;
    }
}
#endif