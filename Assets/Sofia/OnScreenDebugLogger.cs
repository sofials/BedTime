using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class OnScreenDebugLogger : MonoBehaviour
{
    [Header("Display Settings")]
    [SerializeField] private bool showDebugPanel = true;
    [SerializeField] private int maxLogEntries = 50;
    [SerializeField] private float logLifetime = 10f; // Secondi prima che un log sparisca
    [SerializeField] private bool showTimestamps = true;
    [SerializeField] private bool showInBuildOnly = false; // Se true, mostra solo nelle build
    
    [Header("UI Settings")]
    [SerializeField] private KeyCode toggleKey = KeyCode.F1;
    [SerializeField] private KeyCode exportKey = KeyCode.F2;
    [SerializeField] private Vector2 panelSize = new Vector2(800, 400);
    [SerializeField] private Vector2 panelPosition = new Vector2(10, 10);
    [SerializeField] private int fontSize = 12;
    
    [Header("Filters")]
    [SerializeField] private bool showInfoLogs = true;
    [SerializeField] private bool showWarningLogs = true;
    [SerializeField] private bool showErrorLogs = true;
    [SerializeField] private string filterText = ""; // Filtra per testo specifico
    
    public static OnScreenDebugLogger Instance { get; private set; }
    
    private List<LogEntry> logEntries = new List<LogEntry>();
    private Vector2 scrollPosition = Vector2.zero;
    private bool isPanelVisible = true;
    private GUIStyle logStyle;
    private GUIStyle panelStyle;
    private GUIStyle buttonStyle;
    
    private struct LogEntry
    {
        public string message;
        public LogType type;
        public float timestamp;
        public string timeString;
        
        public LogEntry(string msg, LogType logType)
        {
            message = msg;
            type = logType;
            timestamp = Time.unscaledTime;
            timeString = System.DateTime.Now.ToString("HH:mm:ss");
        }
    }
    
    private void Awake()
    {
        // Singleton
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            // Solo nelle build o sempre?
            if (showInBuildOnly && Application.isEditor)
            {
                showDebugPanel = false;
                return;
            }
            
            // Ascolta i log di Unity
            Application.logMessageReceived += HandleLog;
            
            // Log iniziale
            LogInfo("[OnScreenDebugLogger] Sistema di debug inizializzato");
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    private void HandleLog(string logString, string stackTrace, LogType type)
    {
        // Filtra solo i log che ci interessano
        if (!showDebugPanel) return;
        
        // Filtra per tipo
        if (type == LogType.Log && !showInfoLogs) return;
        if (type == LogType.Warning && !showWarningLogs) return;
        if ((type == LogType.Error || type == LogType.Exception) && !showErrorLogs) return;
        
        // Filtra per testo (se specificato)
        if (!string.IsNullOrEmpty(filterText) && !logString.ToLower().Contains(filterText.ToLower()))
            return;
            
        // Aggiungi il log
        AddLogEntry(logString, type);
    }
    
    private void AddLogEntry(string message, LogType type)
    {
        logEntries.Add(new LogEntry(message, type));
        
        // Mantieni solo i log recenti
        if (logEntries.Count > maxLogEntries)
        {
            logEntries.RemoveAt(0);
        }
        
        // Auto-scroll verso il basso
        scrollPosition.y = float.MaxValue;
    }
    
    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            isPanelVisible = !isPanelVisible;
        }
        
        // NUOVO: Export con F2
        if (Input.GetKeyDown(exportKey))
        {
            ExportLogsToFile();
        }
        
        // Rimuovi log vecchi
        float currentTime = Time.unscaledTime;
        logEntries.RemoveAll(entry => currentTime - entry.timestamp > logLifetime);
    }
    
    private void OnGUI()
    {
        if (!showDebugPanel || !isPanelVisible) return;
        
        InitializeStyles();
        
        // Panel principale
        Rect panelRect = new Rect(panelPosition.x, panelPosition.y, panelSize.x, panelSize.y);
        GUI.Box(panelRect, "", panelStyle);
        
        GUILayout.BeginArea(panelRect);
        
        // Header MODIFICATO
        GUILayout.BeginHorizontal();
        GUILayout.Label($"Debug Logger ({logEntries.Count} logs)", logStyle);
        GUILayout.FlexibleSpace();
        
        if (GUILayout.Button("Export", buttonStyle, GUILayout.Width(60)))
        {
            ExportLogsToFile();
        }
        
        if (GUILayout.Button("Clear", buttonStyle, GUILayout.Width(60)))
        {
            logEntries.Clear();
        }
        
        if (GUILayout.Button("X", buttonStyle, GUILayout.Width(25)))
        {
            isPanelVisible = false;
        }
        GUILayout.EndHorizontal();
        
        // Filtri
        GUILayout.BeginHorizontal();
        GUILayout.Label("Filter:", GUILayout.Width(40));
        filterText = GUILayout.TextField(filterText, GUILayout.Width(200));
        
        showInfoLogs = GUILayout.Toggle(showInfoLogs, "Info", GUILayout.Width(50));
        showWarningLogs = GUILayout.Toggle(showWarningLogs, "Warn", GUILayout.Width(55));
        showErrorLogs = GUILayout.Toggle(showErrorLogs, "Error", GUILayout.Width(55));
        GUILayout.EndHorizontal();
        
        // Area scrollabile per i log
        scrollPosition = GUILayout.BeginScrollView(scrollPosition);
        
        foreach (var entry in logEntries)
        {
            Color originalColor = GUI.color;
            
            // Colore basato sul tipo di log
            switch (entry.type)
            {
                case LogType.Warning:
                    GUI.color = Color.yellow;
                    break;
                case LogType.Error:
                case LogType.Exception:
                    GUI.color = Color.red;
                    break;
                default:
                    GUI.color = Color.white;
                    break;
            }
            
            string displayText = showTimestamps ? $"[{entry.timeString}] {entry.message}" : entry.message;
            GUILayout.Label(displayText, logStyle);
            
            GUI.color = originalColor;
        }
        
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }
    
    private void InitializeStyles()
    {
        if (logStyle == null)
        {
            logStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize,
                wordWrap = true,
                richText = true
            };
        }
        
        if (panelStyle == null)
        {
            panelStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = MakeTexture(2, 2, new Color(0, 0, 0, 0.8f)) }
            };
        }
        
        if (buttonStyle == null)
        {
            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = fontSize - 2
            };
        }
    }
    
    private Texture2D MakeTexture(int width, int height, Color color)
    {
        Color[] pixels = new Color[width * height];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = color;
        
        Texture2D texture = new Texture2D(width, height);
        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }
    
    // NUOVO METODO PER ESPORTARE
    public void ExportLogsToFile()
    {
        try
        {
            string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string fileName = $"DebugLogs_{timestamp}.txt";
            
            // Percorso del file
            string filePath;
            
            #if UNITY_EDITOR
            filePath = System.IO.Path.Combine(Application.dataPath, "..", "Logs", fileName);
            #else
            filePath = System.IO.Path.Combine(Application.persistentDataPath, fileName);
            #endif
            
            // Crea la directory se non esiste
            string directory = System.IO.Path.GetDirectoryName(filePath);
            if (!System.IO.Directory.Exists(directory))
            {
                System.IO.Directory.CreateDirectory(directory);
            }
            
            // Scrivi i log nel file
            using (System.IO.StreamWriter writer = new System.IO.StreamWriter(filePath))
            {
                writer.WriteLine($"Debug Log Export - {System.DateTime.Now}");
                writer.WriteLine($"Unity Version: {Application.unityVersion}");
                writer.WriteLine($"Platform: {Application.platform}");
                writer.WriteLine($"Scene: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
                writer.WriteLine("".PadLeft(50, '='));
                writer.WriteLine();
                
                foreach (var entry in logEntries)
                {
                    string logLevel = entry.type.ToString().ToUpper();
                    writer.WriteLine($"[{entry.timeString}] [{logLevel}] {entry.message}");
                }
            }
            
            Debug.Log($"[OnScreenDebugLogger] Log esportati in: {filePath}");
            
            // Mostra notifica su schermo
            AddLogEntry($"Log esportati in: {filePath}", LogType.Log);
            
            #if UNITY_EDITOR
            // Apri la cartella nell'editor
            System.Diagnostics.Process.Start(directory);
            #endif
            
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[OnScreenDebugLogger] Errore durante l'esportazione: {e.Message}");
        }
    }
    
    // METODI PUBBLICI PER LOGGARE
    public static void LogInfo(string message)
    {
        Debug.Log($"[DEBUG] {message}");
    }
    
    public static void LogWarning(string message)
    {
        Debug.LogWarning($"[DEBUG] {message}");
    }
    
    public static void LogError(string message)
    {
        Debug.LogError($"[DEBUG] {message}");
    }
    
    public static void LogCamera(string message)
    {
        Debug.Log($"[CAMERA] {message}");
    }
    
    public static void LogScene(string message)
    {
        Debug.Log($"[SCENE] {message}");
    }
    
    public static void LogGameManager(string message)
    {
        Debug.Log($"[GAMEMANAGER] {message}");
    }
    
    // METODO PUBBLICO PER ESPORTARE
    public static void ExportLogs()
    {
        if (Instance != null)
        {
            Instance.ExportLogsToFile();
        }
    }
    
    // METODI PER CONTROLLARE IL LOGGER
    public void SetVisible(bool visible)
    {
        isPanelVisible = visible;
    }
    
    public void SetFilter(string filter)
    {
        filterText = filter;
    }
    
    public void ClearLogs()
    {
        logEntries.Clear();
    }
    
    private void OnDestroy()
    {
        if (Instance == this)
        {
            Application.logMessageReceived -= HandleLog;
            Instance = null;
        }
    }
}