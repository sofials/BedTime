using UnityEngine;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public class DebugLogTracker : MonoBehaviour
{
    [Header("Impostazioni")]
    [SerializeField] private bool trackWarnings = true;
    [SerializeField] private bool trackErrors = true;
    [SerializeField] private bool trackAssertions = true;
    [SerializeField] private bool showInConsole = true;
    [SerializeField] private bool logToFile = false;
    [SerializeField] private string logFileName = "DebugTracker.log";
    
    [Header("Debug")]
    [SerializeField] private bool verboseDebug = false;
    
    [Header("Filtri")]
    [SerializeField] private List<string> ignoredStrings = new List<string>();
    
    private Dictionary<string, GameObject> logSourceMap = new Dictionary<string, GameObject>();
    private System.IO.StreamWriter logWriter;
    
    private void Awake()
    {
        // Assicurati che ci sia solo un'istanza
        if (FindObjectsByType<DebugLogTracker>(FindObjectsSortMode.None).Length > 1)
        {
            Destroy(gameObject);
            return;
        }
        
        DontDestroyOnLoad(gameObject);
        
        // Inizializza il file di log se necessario
        if (logToFile)
        {
            InitializeLogFile();
        }
        
        // Registra il callback per i log
        Application.logMessageReceived += HandleLog;
    }
    
    private void InitializeLogFile()
    {
        try
        {
            string filePath = System.IO.Path.Combine(Application.persistentDataPath, logFileName);
            logWriter = new System.IO.StreamWriter(filePath, true);
            logWriter.WriteLine($"\n=== Debug Log Tracker Session Started at {System.DateTime.Now} ===");
            logWriter.Flush();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Impossibile creare il file di log: {e.Message}");
            logToFile = false;
        }
    }
    
    private void HandleLog(string logString, string stackTrace, LogType type)
    {
        // Filtra solo i tipi di log che ci interessano
        if (!ShouldTrackLogType(type)) return;
        
        // Controlla se il log dovrebbe essere ignorato
        if (ShouldIgnoreLog(logString)) return;
        
        if (verboseDebug)
        {
            Debug.Log($"<color=magenta>[DEBUG TRACKER]</color> Analyzing stack trace:\n{stackTrace}");
        }
        
        // Trova l'oggetto sorgente dal stack trace
        GameObject sourceObject = FindSourceObject(stackTrace);
        
        if (verboseDebug && sourceObject == null)
        {
            Debug.Log($"<color=orange>[DEBUG TRACKER]</color> Could not find object for: {logString}");
            ListAllPossibleObjects();
        }
        
        // Crea il messaggio di tracking
        string trackingMessage = CreateTrackingMessage(logString, sourceObject, type);
        
        // Mostra/salva il risultato
        if (showInConsole)
        {
            Debug.Log($"<color=cyan>[TRACKER]</color> {trackingMessage}");
        }
        
        if (logToFile && logWriter != null)
        {
            logWriter.WriteLine($"[{System.DateTime.Now:HH:mm:ss}] {trackingMessage}");
            logWriter.WriteLine($"Stack Trace: {stackTrace}");
            logWriter.WriteLine("---");
            logWriter.Flush();
        }
    }
    
    private void ListAllPossibleObjects()
    {
        Debug.Log("<color=yellow>[DEBUG] Oggetti MonoBehaviour in scena:</color>");
        MonoBehaviour[] allComponents = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
        
        var componentTypes = new Dictionary<string, List<string>>();
        
        foreach (MonoBehaviour component in allComponents)
        {
            if (component != null)
            {
                string typeName = component.GetType().Name;
                if (!componentTypes.ContainsKey(typeName))
                    componentTypes[typeName] = new List<string>();
                    
                componentTypes[typeName].Add(component.gameObject.name);
            }
        }
        
        foreach (var kvp in componentTypes)
        {
            Debug.Log($"<color=green>{kvp.Key}</color>: {string.Join(", ", kvp.Value)}");
        }
    }
    
    private bool ShouldTrackLogType(LogType type)
    {
        return (type == LogType.Warning && trackWarnings) ||
               (type == LogType.Error && trackErrors) ||
               (type == LogType.Exception && trackErrors) ||
               (type == LogType.Assert && trackAssertions);
    }
    
    private bool ShouldIgnoreLog(string logString)
    {
        foreach (string ignored in ignoredStrings)
        {
            if (!string.IsNullOrEmpty(ignored) && logString.Contains(ignored))
            {
                return true;
            }
        }
        return false;
    }
    
    private GameObject FindSourceObject(string stackTrace)
    {
        if (string.IsNullOrEmpty(stackTrace)) return null;
        
        // Cerca pattern comuni nei stack trace per identificare l'oggetto
        string[] lines = stackTrace.Split('\n');
        
        foreach (string line in lines)
        {
            // Prova diversi metodi per trovare l'oggetto
            GameObject foundObject = null;
            
            // Metodo 1: Cerca per nome classe con context object
            foundObject = FindByClassNameFromStackTrace(line);
            if (foundObject != null) return foundObject;
            
            // Metodo 2: Cerca attraverso tutti gli oggetti attivi
            foundObject = FindByComponentSearch(line);
            if (foundObject != null) return foundObject;
        }
        
        return null;
    }
    
    private GameObject FindByClassNameFromStackTrace(string stackLine)
    {
        try
        {
            // Pattern per catturare il nome della classe
            Regex[] patterns = {
                new Regex(@"(\w+):(\w+)\s*\(.*?\)\s*\(at.*?(\w+)\.cs:"),  // ClassName:Method (at Script.cs:line)
                new Regex(@"(\w+)\.(\w+)\s*\(.*?\)\s*\(at.*?(\w+)\.cs:"), // ClassName.Method (at Script.cs:line)
                new Regex(@"(\w+):(\w+)\s*\("),                           // ClassName:Method (
                new Regex(@"(\w+)\.(\w+)\s*\(")                           // ClassName.Method (
            };
            
            foreach (var pattern in patterns)
            {
                Match match = pattern.Match(stackLine);
                if (match.Success)
                {
                    string className = match.Groups[1].Value;
                    return FindObjectByClassName(className);
                }
            }
        }
        catch (System.Exception)
        {
            // Ignora errori
        }
        
        return null;
    }
    
    private GameObject FindByComponentSearch(string stackLine)
    {
        try
        {
            // Estrai possibili nomi di classi dalla linea
            string[] possibleClasses = { "DialogueSystem", "ThirdPersonController", "MovingPlatform", 
                                       "SlowdownAbility", "PlayerPowerUp", "PlayerController", "EnemyAI" };
            
            foreach (string className in possibleClasses)
            {
                if (stackLine.Contains(className))
                {
                    GameObject obj = FindObjectByClassName(className);
                    if (obj != null) return obj;
                }
            }
        }
        catch (System.Exception)
        {
            // Ignora errori
        }
        
        return null;
    }
    
    private GameObject FindObjectByClassName(string className)
    {
        // Cerca prima nei MonoBehaviour
        MonoBehaviour[] allComponents = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
        
        foreach (MonoBehaviour component in allComponents)
        {
            if (component != null && component.GetType().Name == className)
            {
                return component.gameObject;
            }
        }
        
        // Cerca anche negli oggetti per nome (caso in cui il GameObject abbia lo stesso nome della classe)
        GameObject[] allGameObjects = FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        
        foreach (GameObject go in allGameObjects)
        {
            if (go != null && go.name.Contains(className))
            {
                return go;
            }
        }
        
        return null;
    }
    
    private GameObject ExtractGameObjectFromStackLine(string stackLine)
    {
        try
        {
            // Estrai il nome della classe dal stack trace - pattern migliorato
            Regex classRegex = new Regex(@"(\w+):(\w+)\s*\(.*?\)\s*\(at.*?(\w+)\.cs:");
            Match match = classRegex.Match(stackLine);
            
            if (match.Success)
            {
                string className = match.Groups[1].Value;
                
                // Cerca tutti i MonoBehaviour con questo nome di classe
                MonoBehaviour[] allComponents = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
                
                foreach (MonoBehaviour component in allComponents)
                {
                    if (component != null && component.GetType().Name == className)
                    {
                        return component.gameObject;
                    }
                }
            }
            
            // Fallback: prova pattern alternativo
            Regex fallbackRegex = new Regex(@"(\w+)\.(\w+)\s*\(");
            Match fallbackMatch = fallbackRegex.Match(stackLine);
            
            if (fallbackMatch.Success)
            {
                string className = fallbackMatch.Groups[1].Value;
                
                MonoBehaviour[] allComponents = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
                
                foreach (MonoBehaviour component in allComponents)
                {
                    if (component != null && component.GetType().Name == className)
                    {
                        return component.gameObject;
                    }
                }
            }
        }
        catch (System.Exception)
        {
            // Ignora errori nell'estrazione
        }
        
        return null;
    }
    
    private string CreateTrackingMessage(string originalLog, GameObject sourceObject, LogType logType)
    {
        string typeIcon = GetLogTypeIcon(logType);
        string objectInfo = GetObjectInfo(sourceObject);
        string suggestion = GetSuggestionForCommonIssues(originalLog);
        
        string message = $"{typeIcon} {logType}: \"{originalLog}\" → Oggetto: {objectInfo}";
        
        if (!string.IsNullOrEmpty(suggestion))
        {
            message += $"\n💡 Suggerimento: {suggestion}";
        }
        
        return message;
    }
    
    private string GetSuggestionForCommonIssues(string logMessage)
    {
        if (logMessage.Contains("Physics.ClosestPoint can only be used"))
            return "Controlla che il collider sia Box/Sphere/Capsule o MeshCollider convesso";
            
        if (logMessage.Contains("_EmissionColor"))
            return "Aggiungi la proprietà _EmissionColor al materiale o controlla se esiste prima di usarla";
            
        if (logMessage.Contains("nessun evento configurato"))
            return "Configura gli eventi nel DialogueSystem o disabilita il sistema eventi";
            
        if (logMessage.Contains("NullReferenceException"))
            return "Controlla che tutte le reference siano assegnate nell'Inspector";
            
        return string.Empty;
    }
    
    private string GetLogTypeIcon(LogType logType)
    {
        switch (logType)
        {
            case LogType.Warning: return "⚠️";
            case LogType.Error: return "❌";
            case LogType.Exception: return "💥";
            case LogType.Assert: return "🔍";
            default: return "📝";
        }
    }
    
    private string GetObjectInfo(GameObject obj)
    {
        if (obj == null) return "<color=red>OGGETTO NON TROVATO</color>";
        
        string hierarchy = GetGameObjectPath(obj);
        string position = $"Pos({obj.transform.position.x:F1}, {obj.transform.position.y:F1}, {obj.transform.position.z:F1})";
        string activeState = obj.activeInHierarchy ? "Attivo" : "Inattivo";
        
        return $"<color=yellow>{obj.name}</color> [{hierarchy}] {position} ({activeState})";
    }
    
    private string GetGameObjectPath(GameObject obj)
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
    
    // Metodi di utilità pubblici
    [ContextMenu("Test Warning")]
    public void TestWarning()
    {
        Debug.LogWarning("Questo è un test warning dal DebugLogTracker");
    }
    
    [ContextMenu("Test Error")]
    public void TestError()
    {
        Debug.LogError("Questo è un test error dal DebugLogTracker");
    }
    
    [ContextMenu("Clear Log File")]
    public void ClearLogFile()
    {
        if (logWriter != null)
        {
            logWriter.Close();
            logWriter = null;
        }
        
        if (logToFile)
        {
            InitializeLogFile();
        }
    }
    
    [ContextMenu("Open Log File Location")]
    public void OpenLogFileLocation()
    {
        string fullPath = System.IO.Path.Combine(Application.persistentDataPath, logFileName);
        
        Debug.Log($"<color=green>Percorso completo del file log:</color>\n{fullPath}");
        
        // Apri la cartella (solo in Editor)
        #if UNITY_EDITOR
        UnityEditor.EditorUtility.RevealInFinder(fullPath);
        #endif
        
        // Copia il percorso negli appunti
        GUIUtility.systemCopyBuffer = fullPath;
        Debug.Log("<color=cyan>Percorso copiato negli appunti!</color>");
    }
    
    [ContextMenu("Show Log File Content")]
    public void ShowLogFileContent()
    {
        string fullPath = System.IO.Path.Combine(Application.persistentDataPath, logFileName);
        
        if (System.IO.File.Exists(fullPath))
        {
            try
            {
                string content = System.IO.File.ReadAllText(fullPath);
                string[] lines = content.Split('\n');
                
                // Mostra solo le ultime 20 righe per non intasare la console
                Debug.Log("<color=green>=== ULTIMI LOG (ultime 20 righe) ===</color>");
                for (int i = Mathf.Max(0, lines.Length - 20); i < lines.Length; i++)
                {
                    if (!string.IsNullOrEmpty(lines[i]))
                        Debug.Log($"<color=white>{lines[i]}</color>");
                }
                Debug.Log("<color=green>=== FINE LOG ===</color>");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Errore nella lettura del file: {e.Message}");
            }
        }
        else
        {
            Debug.LogWarning("File di log non trovato!");
        }
    }
    
    [ContextMenu("Scan Scene Objects")]
    public void ScanSceneObjects()
    {
        Debug.Log("<color=cyan>=== SCANSIONE OGGETTI IN SCENA ===</color>");
        
        // Trova tutti i componenti dei tuoi script
        string[] targetScripts = { "DialogueSystem", "ThirdPersonController", "MovingPlatform", 
                                 "SlowdownAbility", "PlayerPowerUp", "PlayerController", "EnemyAI" };
        
        foreach (string scriptName in targetScripts)
        {
            MonoBehaviour[] components = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            List<GameObject> foundObjects = new List<GameObject>();
            
            foreach (MonoBehaviour component in components)
            {
                if (component != null && component.GetType().Name == scriptName)
                {
                    foundObjects.Add(component.gameObject);
                }
            }
            
            if (foundObjects.Count > 0)
            {
                Debug.Log($"<color=yellow>{scriptName}</color> trovato su: <color=green>{string.Join(", ", foundObjects.ConvertAll(go => GetGameObjectPath(go)))}</color>");
            }
            else
            {
                Debug.Log($"<color=red>{scriptName}</color> - Nessun oggetto trovato");
            }
        }
        
        Debug.Log("<color=cyan>=== FINE SCANSIONE ===</color>");
    }
    
    private void OnDestroy()
    {
        Application.logMessageReceived -= HandleLog;
        
        if (logWriter != null)
        {
            logWriter.WriteLine($"=== Session Ended at {System.DateTime.Now} ===");
            logWriter.Close();
        }
    }
    
    private void OnApplicationPause(bool pauseStatus)
    {
        if (logWriter != null)
        {
            logWriter.Flush();
        }
    }
}

// Extension class per migliorare il tracking
public static class DebugExtensions
{
    public static void LogWarningWithObject(this MonoBehaviour obj, string message)
    {
        Debug.LogWarning($"[{obj.gameObject.name}] {message}", obj.gameObject);
    }
    
    public static void LogErrorWithObject(this MonoBehaviour obj, string message)
    {
        Debug.LogError($"[{obj.gameObject.name}] {message}", obj.gameObject);
    }
}