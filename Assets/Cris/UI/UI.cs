using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using System.Text.RegularExpressions;
using System.Collections;
using System.Reflection;

public class PlayerUI : MonoBehaviour
{
    [Header("Health UI")]
    public Image healthFill;

    [Header("Mana/Power UI")]
    public Image powerFill;

    [Header("Scene Collectibles UI")]
    public TextMeshProUGUI memoryCounterText;
    public TextMeshProUGUI presentCounterText;
    public Image memoryIcon; // opzionale, icona della memoria
    public Image presentIcon; // opzionale, icona del present
    
    [Header("Collectible Panels Animation")]
    public GameObject memoryPanel; // Panel che contiene memoryCounterText e memoryIcon
    public GameObject presentPanel; // Panel che contiene presentCounterText e presentIcon
    
    [Header("Panel Animation Settings")]
    public bool showPanelsOnCollect = true;
    public float panelShowDuration = 3f; // Quanto tempo mostrare il panel
    public float panelAnimationSpeed = 0.5f; // Velocità animazione entrata/uscita
    public AnimationCurve panelEaseInOut = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    
    [Header("Collectibles Animation")]
    public bool animateOnCollect = true;
    public float punchScale = 1.2f;
    public float animationDuration = 0.3f;

    [Header("Player References")]
    public ThirdPersonController playerController;
    public PlayerPowerUp playerPowerUp;

    [Header("Ability Icons")]
    public UIEffectHandler[] abilityIcons;

    private bool useGamepad = false;
    
    // Scene collectibles tracking
    private int currentSceneMemories = 0;
    private int totalSceneMemories = 0;
    private int currentScenePresents = 0;
    private int totalScenePresents = 0;
    
    // Template strings per preservare la formattazione (IMMUTABILI)
    private string memoryTextTemplate = "";
    private string presentTextTemplate = "";
    private Color originalMemoryColor;
    private Color originalPresentColor;
    
    // Controllo animazioni per evitare conflitti
    private Coroutine memoryPanelCoroutine = null;
    private Coroutine presentPanelCoroutine = null;
    private Coroutine memoryAnimationCoroutine = null;
    private Coroutine presentAnimationCoroutine = null;
    
    // Flag per prevenire aggiornamenti multipli simultanei
    private bool isUpdatingMemoryUI = false;
    private bool isUpdatingPresentUI = false;
    
    // 🔥 UNIVERSAL SCENE MANAGER SUPPORT
    private MonoBehaviour currentSceneManager = null;
    private System.Type currentSceneManagerType = null;
    private bool isConnectedToSceneManager = false;
    
    // 🔥 SALVATAGGIO POSIZIONI ORIGINALI - La chiave per risolvere il problema!
    private struct UIElementState
    {
        public Vector3 anchoredPosition;
        public Vector3 localScale;
        public Quaternion rotation;
        public float alpha;
        
        public UIElementState(RectTransform rect, CanvasGroup canvas = null)
        {
            anchoredPosition = rect.anchoredPosition;
            localScale = rect.localScale;
            rotation = rect.rotation;
            alpha = canvas != null ? canvas.alpha : 1f;
        }
    }
    
    private UIElementState memoryPanelOriginalState;
    private UIElementState presentPanelOriginalState;
    private UIElementState memoryTextOriginalState;
    private UIElementState presentTextOriginalState;
    private UIElementState memoryIconOriginalState;
    private UIElementState presentIconOriginalState;
    
    private RectTransform memoryPanelRect;
    private RectTransform presentPanelRect;
    private RectTransform memoryTextRect;
    private RectTransform presentTextRect;
    private RectTransform memoryIconRect;
    private RectTransform presentIconRect;

    public static PlayerUI Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        // PRIMA: Cache delle posizioni originali - FONDAMENTALE!
        CacheOriginalUIStates();
        
        // DOPO: Tutto il resto
        SaveOriginalTextFormats();
        HidePanelsAtStart();
        InitializeCollectibleSystem();
    }
    
    private void CacheOriginalUIStates()
    {
        // 🔥 SALVA lo stato ORIGINALE di ogni elemento UI (posizione, scale, rotazione)
        
        if (memoryPanel != null)
        {
            memoryPanelRect = memoryPanel.GetComponent<RectTransform>();
            if (memoryPanelRect != null)
            {
                CanvasGroup canvas = memoryPanel.GetComponent<CanvasGroup>();
                memoryPanelOriginalState = new UIElementState(memoryPanelRect, canvas);
            }
        }
        
        if (presentPanel != null)
        {
            presentPanelRect = presentPanel.GetComponent<RectTransform>();
            if (presentPanelRect != null)
            {
                CanvasGroup canvas = presentPanel.GetComponent<CanvasGroup>();
                presentPanelOriginalState = new UIElementState(presentPanelRect, canvas);
            }
        }
        
        if (memoryCounterText != null)
        {
            memoryTextRect = memoryCounterText.GetComponent<RectTransform>();
            if (memoryTextRect != null)
            {
                memoryTextOriginalState = new UIElementState(memoryTextRect);
            }
        }
        
        if (presentCounterText != null)
        {
            presentTextRect = presentCounterText.GetComponent<RectTransform>();
            if (presentTextRect != null)
            {
                presentTextOriginalState = new UIElementState(presentTextRect);
            }
        }
        
        if (memoryIcon != null)
        {
            memoryIconRect = memoryIcon.GetComponent<RectTransform>();
            if (memoryIconRect != null)
            {
                memoryIconOriginalState = new UIElementState(memoryIconRect);
            }
        }
        
        if (presentIcon != null)
        {
            presentIconRect = presentIcon.GetComponent<RectTransform>();
            if (presentIconRect != null)
            {
                presentIconOriginalState = new UIElementState(presentIconRect);
            }
        }
        
        Debug.Log("[PlayerUI] 🔥 Stati originali UI cachati!");
    }
    
    // 🔥 METODO CHIAVE: Ripristina ESATTAMENTE lo stato originale
    private void RestoreUIElementState(RectTransform rect, UIElementState originalState, CanvasGroup canvas = null)
    {
        if (rect == null) return;
        
        rect.anchoredPosition = originalState.anchoredPosition;
        rect.localScale = originalState.localScale;
        rect.rotation = originalState.rotation;
        
        if (canvas != null)
        {
            canvas.alpha = originalState.alpha;
        }
        
        Debug.Log($"[PlayerUI] Ripristinato stato originale per {rect.name}: pos={originalState.anchoredPosition}, scale={originalState.localScale}");
    }
    
    private void RestoreAllOriginalStates()
    {
        // Ripristina TUTTI gli elementi alle loro posizioni originali
        RestoreUIElementState(memoryPanelRect, memoryPanelOriginalState, memoryPanel?.GetComponent<CanvasGroup>());
        RestoreUIElementState(presentPanelRect, presentPanelOriginalState, presentPanel?.GetComponent<CanvasGroup>());
        RestoreUIElementState(memoryTextRect, memoryTextOriginalState);
        RestoreUIElementState(presentTextRect, presentTextOriginalState);
        RestoreUIElementState(memoryIconRect, memoryIconOriginalState);
        RestoreUIElementState(presentIconRect, presentIconOriginalState);
        
        Debug.Log("[PlayerUI] 🔥 TUTTI gli stati originali ripristinati!");
    }
    
    private void HidePanelsAtStart()
    {
        // Nascondi i pannelli dei collectibles all'inizio del gioco
        if (memoryPanel != null)
        {
            memoryPanel.SetActive(false);
        }
        else if (memoryCounterText != null)
        {
            memoryCounterText.gameObject.SetActive(false);
        }
        
        if (presentPanel != null)
        {
            presentPanel.SetActive(false);
        }
        else if (presentCounterText != null)
        {
            presentCounterText.gameObject.SetActive(false);
        }
        
        Debug.Log("[PlayerUI] Pannelli collectibles nascosti all'avvio");
    }
    
    private void SaveOriginalTextFormats()
    {
        // Salva il template per le memories
        if (memoryCounterText != null)
        {
            memoryTextTemplate = memoryCounterText.text;
            originalMemoryColor = memoryCounterText.color;
            
            // Se il testo è vuoto o non contiene numeri, usa un template di default
            if (string.IsNullOrEmpty(memoryTextTemplate) || !memoryTextTemplate.Contains("/"))
            {
                memoryTextTemplate = "Memories: 0/0";
            }
            
            Debug.Log($"[PlayerUI] Memory template salvato: '{memoryTextTemplate}' - Colore: {originalMemoryColor}");
        }
        
        // Salva il template per i presents
        if (presentCounterText != null)
        {
            presentTextTemplate = presentCounterText.text;
            originalPresentColor = presentCounterText.color;
            
            // Se il testo è vuoto o non contiene numeri, usa un template di default
            if (string.IsNullOrEmpty(presentTextTemplate) || !presentTextTemplate.Contains("/"))
            {
                presentTextTemplate = "Presents: 0/0";
            }
            
            Debug.Log($"[PlayerUI] Present template salvato: '{presentTextTemplate}' - Colore: {originalPresentColor}");
        }
    }

    // 🔥 UNIVERSAL SCENE MANAGER INITIALIZATION
    private void InitializeCollectibleSystem()
    {
        Debug.Log("[PlayerUI] 🌟 Inizializzazione sistema collectibles universale...");
        
        // Disconnetti vecchie connessioni
        DisconnectFromAllSystems();
        
        // Prova prima con SceneManager specifici (SceneManager01, SceneManager02, etc.)
        if (TryConnectToSceneManager())
        {
            Debug.Log($"[PlayerUI] ✅ Connesso a {currentSceneManagerType.Name}");
            return;
        }
        
        // Fallback con PlayerCollectibleTracker
        if (TryConnectToCollectibleTracker())
        {
            Debug.Log("[PlayerUI] ✅ Connesso a PlayerCollectibleTracker");
            return;
        }
        
        Debug.LogError("[PlayerUI] ❌ Nessun sistema di tracking collectibles trovato!");
    }
    
    // 🔥 TROVA E CONNETTI A QUALSIASI SCENEMANAGER
    private bool TryConnectToSceneManager()
    {
        // Lista di possibili SceneManager da cercare
        string[] possibleSceneManagers = {
            "SceneManager01", "SceneManager02", "SceneManager03", "SceneManager04", "SceneManager05",
            "SceneManager", "LevelManager", "CollectibleManager", "GameSceneManager"
        };
        
        foreach (string managerName in possibleSceneManagers)
        {
            // Cerca per nome del tipo
            System.Type managerType = System.Type.GetType(managerName);
            if (managerType != null)
            {
                MonoBehaviour manager = Object.FindFirstObjectByType(managerType) as MonoBehaviour;
                if (manager != null)
                {
                    if (TryConnectToSpecificSceneManager(manager, managerType))
                    {
                        return true;
                    }
                }
            }
        }
        
        // Se non trova nessuno specifico, cerca qualsiasi MonoBehaviour che ha "Instance" e i metodi giusti
        return TryConnectToGenericSceneManager();
    }
    
    // 🔥 CONNETTI A UN SCENE MANAGER SPECIFICO
    private bool TryConnectToSpecificSceneManager(MonoBehaviour manager, System.Type managerType)
    {
        try
        {
            // Verifica che abbia una proprietà Instance
            PropertyInfo instanceProperty = managerType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            if (instanceProperty == null)
            {
                Debug.Log($"[PlayerUI] {managerType.Name} non ha proprietà Instance statica");
                return false;
            }
            
            object instance = instanceProperty.GetValue(null);
            if (instance == null)
            {
                Debug.Log($"[PlayerUI] {managerType.Name}.Instance è null");
                return false;
            }
            
            // Verifica che abbia i metodi necessari
            if (!HasRequiredSceneManagerMethods(managerType))
            {
                Debug.Log($"[PlayerUI] {managerType.Name} non ha tutti i metodi richiesti");
                return false;
            }
            
            // Connetti agli eventi
            ConnectToSceneManagerEvents(instance, managerType);
            InitializeFromSceneManager(instance, managerType);
            
            currentSceneManager = manager;
            currentSceneManagerType = managerType;
            isConnectedToSceneManager = true;
            
            Debug.Log($"[PlayerUI] ✅ Connesso con successo a {managerType.Name}");
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[PlayerUI] Errore connessione a {managerType.Name}: {e.Message}");
            return false;
        }
    }
    
    // 🔥 CERCA SCENE MANAGER GENERICO
    private bool TryConnectToGenericSceneManager()
    {
        // Cerca tutti i MonoBehaviour nella scena
        MonoBehaviour[] allMonoBehaviours = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
        
        foreach (MonoBehaviour mb in allMonoBehaviours)
        {
            System.Type type = mb.GetType();
            
            // Salta se è PlayerCollectibleTracker (lo gestiamo separatamente)
            if (type == typeof(PlayerCollectibleTracker)) continue;
            
            // Controlla se ha i metodi/proprietà che ci servono
            if (HasRequiredSceneManagerMethods(type))
            {
                try
                {
                    // Prova a ottenere l'istanza
                    PropertyInfo instanceProp = type.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                    object instance = instanceProp?.GetValue(null) ?? mb;
                    
                    if (instance != null)
                    {
                        ConnectToSceneManagerEvents(instance, type);
                        InitializeFromSceneManager(instance, type);
                        
                        currentSceneManager = mb;
                        currentSceneManagerType = type;
                        isConnectedToSceneManager = true;
                        
                        Debug.Log($"[PlayerUI] ✅ Connesso a SceneManager generico: {type.Name}");
                        return true;
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[PlayerUI] Errore connessione a {type.Name}: {e.Message}");
                }
            }
        }
        
        return false;
    }
    
    // 🔥 VERIFICA SE UN TIPO HA I METODI RICHIESTI PER SCENE MANAGER
    private bool HasRequiredSceneManagerMethods(System.Type type)
    {
        // Metodi essenziali per essere considerato un SceneManager compatibile
        string[] requiredMethods = {
            "GetCollectedMemories", "GetTotalMemories",
            "GetCollectedPresents", "GetTotalPresents"
        };
        
        foreach (string methodName in requiredMethods)
        {
            if (type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance) == null)
            {
                return false;
            }
        }
        
        return true;
    }
    
    // 🔥 CONNETTI AGLI EVENTI DI UN SCENE MANAGER
    private void ConnectToSceneManagerEvents(object instance, System.Type managerType)
    {
        // Lista degli eventi da cercare e connettere
        var eventConnections = new[]
        {
            ("OnMemoryCountChanged", "UpdateSceneMemoryCounter"),
            ("OnPresentCountChanged", "UpdateScenePresentCounter"),
            ("OnAllMemoriesCollected", "OnAllSceneMemoriesCompleted"),
            ("OnAllPresentsCollected", "OnAllScenePresentsCompleted"),
            ("OnAllCollectiblesCompleted", "OnAllSceneCollectiblesCompleted")
        };
        
        foreach (var (eventName, handlerName) in eventConnections)
        {
            TryConnectEvent(instance, managerType, eventName, handlerName);
        }
    }
    
    // 🔥 CONNETTI UN SINGOLO EVENTO
    private void TryConnectEvent(object instance, System.Type managerType, string eventName, string handlerName)
    {
        try
        {
            EventInfo eventInfo = managerType.GetEvent(eventName, BindingFlags.Public | BindingFlags.Instance);
            if (eventInfo != null)
            {
                MethodInfo handlerMethod = this.GetType().GetMethod(handlerName, BindingFlags.NonPublic | BindingFlags.Instance);
                if (handlerMethod != null)
                {
                    System.Delegate handler = System.Delegate.CreateDelegate(eventInfo.EventHandlerType, this, handlerMethod);
                    eventInfo.AddEventHandler(instance, handler);
                    Debug.Log($"[PlayerUI] Evento {eventName} connesso a {handlerName}");
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[PlayerUI] Impossibile connettere evento {eventName}: {e.Message}");
        }
    }
    
    // 🔥 INIZIALIZZA VALORI DA SCENE MANAGER
    private void InitializeFromSceneManager(object instance, System.Type managerType)
    {
        try
        {
            // Ottieni i valori attuali usando reflection
            MethodInfo getCollectedMemories = managerType.GetMethod("GetCollectedMemories");
            MethodInfo getTotalMemories = managerType.GetMethod("GetTotalMemories");
            MethodInfo getCollectedPresents = managerType.GetMethod("GetCollectedPresents");
            MethodInfo getTotalPresents = managerType.GetMethod("GetTotalPresents");
            
            if (getCollectedMemories != null && getTotalMemories != null)
            {
                currentSceneMemories = (int)getCollectedMemories.Invoke(instance, null);
                totalSceneMemories = (int)getTotalMemories.Invoke(instance, null);
            }
            
            if (getCollectedPresents != null && getTotalPresents != null)
            {
                currentScenePresents = (int)getCollectedPresents.Invoke(instance, null);
                totalScenePresents = (int)getTotalPresents.Invoke(instance, null);
            }
            
            UpdateAllCounterDisplays();
            
            Debug.Log($"[PlayerUI] Valori inizializzati da {managerType.Name} - Memories: {currentSceneMemories}/{totalSceneMemories}, Presents: {currentScenePresents}/{totalScenePresents}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[PlayerUI] Errore inizializzazione da {managerType.Name}: {e.Message}");
        }
    }
    
    // 🔥 CONNETTI AL COLLECTIBLE TRACKER (FALLBACK)
    private bool TryConnectToCollectibleTracker()
    {
        PlayerCollectibleTracker tracker = PlayerCollectibleTracker.Instance;
        if (tracker == null)
        {
            tracker = Object.FindFirstObjectByType<PlayerCollectibleTracker>();
        }
        
        if (tracker != null)
        {
            // Disconnetti eventuali vecchi eventi
            CleanupTrackerEvents(tracker);
            
            // Connetti agli eventi del tracker
            tracker.OnMemoryCollected += UpdateSceneMemoryCounter;
            tracker.OnPresentCollected += UpdateScenePresentCounter;
            tracker.OnAllMemoriesCollected += OnAllSceneMemoriesCompleted;
            tracker.OnAllPresentsCollected += OnAllScenePresentsCompleted;
            tracker.OnAllCollectiblesCompleted += OnAllSceneCollectiblesCompleted;
            
            // Inizializza i valori
            currentSceneMemories = tracker.GetCollectedMemories();
            totalSceneMemories = tracker.GetTotalMemories();
            currentScenePresents = tracker.GetCollectedPresents();
            totalScenePresents = tracker.GetTotalPresents();
            
            UpdateAllCounterDisplays();
            
            Debug.Log($"[PlayerUI] Connesso a PlayerCollectibleTracker - Memories: {currentSceneMemories}/{totalSceneMemories}, Presents: {currentScenePresents}/{totalScenePresents}");
            return true;
        }
        
        return false;
    }
    
    // 🔥 DISCONNETTI DA TUTTI I SISTEMI
    private void DisconnectFromAllSystems()
    {
        // Disconnetti da Scene Manager se connesso
        if (isConnectedToSceneManager && currentSceneManager != null && currentSceneManagerType != null)
        {
            DisconnectFromSceneManager();
        }
        
        // Disconnetti da Collectible Tracker
        PlayerCollectibleTracker tracker = PlayerCollectibleTracker.Instance;
        if (tracker != null)
        {
            CleanupTrackerEvents(tracker);
        }
        
        // Reset flags
        isConnectedToSceneManager = false;
        currentSceneManager = null;
        currentSceneManagerType = null;
        
        Debug.Log("[PlayerUI] Disconnesso da tutti i sistemi");
    }
    
    // 🔥 DISCONNETTI DA SCENE MANAGER
    private void DisconnectFromSceneManager()
    {
        if (currentSceneManagerType == null) return;
        
        try
        {
            PropertyInfo instanceProperty = currentSceneManagerType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            if (instanceProperty != null)
            {
                object instance = instanceProperty.GetValue(null);
                if (instance != null)
                {
                    // Disconnetti eventi
                    var eventConnections = new[]
                    {
                        ("OnMemoryCountChanged", "UpdateSceneMemoryCounter"),
                        ("OnPresentCountChanged", "UpdateScenePresentCounter"),
                        ("OnAllMemoriesCollected", "OnAllSceneMemoriesCompleted"),
                        ("OnAllPresentsCollected", "OnAllScenePresentsCompleted"),
                        ("OnAllCollectiblesCompleted", "OnAllSceneCollectiblesCompleted")
                    };
                    
                    foreach (var (eventName, handlerName) in eventConnections)
                    {
                        TryDisconnectEvent(instance, currentSceneManagerType, eventName, handlerName);
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[PlayerUI] Errore durante disconnessione da {currentSceneManagerType.Name}: {e.Message}");
        }
    }
    
    // 🔥 DISCONNETTI UN SINGOLO EVENTO
    private void TryDisconnectEvent(object instance, System.Type managerType, string eventName, string handlerName)
    {
        try
        {
            EventInfo eventInfo = managerType.GetEvent(eventName, BindingFlags.Public | BindingFlags.Instance);
            if (eventInfo != null)
            {
                MethodInfo handlerMethod = this.GetType().GetMethod(handlerName, BindingFlags.NonPublic | BindingFlags.Instance);
                if (handlerMethod != null)
                {
                    System.Delegate handler = System.Delegate.CreateDelegate(eventInfo.EventHandlerType, this, handlerMethod);
                    eventInfo.RemoveEventHandler(instance, handler);
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[PlayerUI] Impossibile disconnettere evento {eventName}: {e.Message}");
        }
    }

    private void OnEnable()
    {
        InputSystem.onActionChange += OnInputActionChange;
        UpdateInputLayout();
    }

    private void OnDisable()
{
    // SAFETY CHECK: Non fare cleanup se stiamo inizializzando
    if (Time.timeSinceLevelLoad < 1f)
    {
        Debug.LogWarning("[PlayerUI] OnDisable chiamato troppo presto - SKIP cleanup per evitare distruzione");
        return;
    }
    
    Debug.Log("[PlayerUI] OnDisable - Eseguendo cleanup normale");
    InputSystem.onActionChange -= OnInputActionChange;
    CleanupEvents();
}
    
    private void CleanupEvents()
    {
        DisconnectFromAllSystems();
        
        // Ferma tutte le animazioni attive e ripristina posizioni
        StopAllUIAnimations();
    }
    
    private void StopAllUIAnimations()
    {
        if (memoryPanelCoroutine != null)
        {
            StopCoroutine(memoryPanelCoroutine);
            memoryPanelCoroutine = null;
        }
        
        if (presentPanelCoroutine != null)
        {
            StopCoroutine(presentPanelCoroutine);
            presentPanelCoroutine = null;
        }
        
        if (memoryAnimationCoroutine != null)
        {
            StopCoroutine(memoryAnimationCoroutine);
            memoryAnimationCoroutine = null;
        }
        
        if (presentAnimationCoroutine != null)
        {
            StopCoroutine(presentAnimationCoroutine);
            presentAnimationCoroutine = null;
        }
        
        // 🔥 FONDAMENTALE: Ripristina le posizioni originali
        RestoreAllOriginalStates();
        
        Debug.Log("[PlayerUI] Tutte le animazioni UI fermate e posizioni originali ripristinate");
    }
    
    private void CleanupTrackerEvents(PlayerCollectibleTracker tracker)
    {
        if (tracker != null)
        {
            tracker.OnMemoryCollected -= UpdateSceneMemoryCounter;
            tracker.OnPresentCollected -= UpdateScenePresentCounter;
            tracker.OnAllMemoriesCollected -= OnAllSceneMemoriesCompleted;
            tracker.OnAllPresentsCollected -= OnAllScenePresentsCompleted;
            tracker.OnAllCollectiblesCompleted -= OnAllSceneCollectiblesCompleted;
        }
    }

    private void OnInputActionChange(object obj, InputActionChange change)
    {
        if (change == InputActionChange.ActionPerformed)
        {
            UpdateInputLayout();
        }
    }

    private void UpdateInputLayout()
    {
        bool wasGamepad = useGamepad;
        useGamepad = Gamepad.current != null && Gamepad.current.wasUpdatedThisFrame;

        if (wasGamepad != useGamepad && abilityIcons != null)
        {
            for (int i = 0; i < abilityIcons.Length; i++)
            {
                if (abilityIcons[i] != null)
                {
                    abilityIcons[i].UpdateInputText(useGamepad);
                }
            }
        }
    }

    // ========== SCENE COLLECTIBLES COUNTER METHODS ==========
    
    public void UpdateSceneMemoryCounter(int collected, int total)
    {
        // Previeni aggiornamenti multipli simultanei
        if (isUpdatingMemoryUI)
        {
            Debug.Log("[PlayerUI] Memory UI già in aggiornamento, ignorato");
            return;
        }
        
        isUpdatingMemoryUI = true;
        
        currentSceneMemories = collected;
        totalSceneMemories = total;
        UpdateMemoryCounterDisplay();
        
        // Mostra il pannello con animazione quando viene raccolta una memory
        if (showPanelsOnCollect)
        {
            ShowMemoryPanel();
        }
        
        if (animateOnCollect)
        {
            AnimateMemoryCounter();
        }
        
        Debug.Log($"[PlayerUI] Scene memory counter aggiornato: {currentSceneMemories}/{totalSceneMemories}");
        
        // Libera il flag dopo un breve delay
        StartCoroutine(ReleaseMemoryUILock());
    }
    
    public void UpdateScenePresentCounter(int collected, int total)
    {
        // Previeni aggiornamenti multipli simultanei
        if (isUpdatingPresentUI)
        {
            Debug.Log("[PlayerUI] Present UI già in aggiornamento, ignorato");
            return;
        }
        
        isUpdatingPresentUI = true;
        
        currentScenePresents = collected;
        totalScenePresents = total;
        UpdatePresentCounterDisplay();
        
        // Mostra il pannello con animazione quando viene raccolto un present
        if (showPanelsOnCollect)
        {
            ShowPresentPanel();
        }
        
        if (animateOnCollect)
        {
            AnimatePresentCounter();
        }
        
        Debug.Log($"[PlayerUI] Scene present counter aggiornato: {currentScenePresents}/{totalScenePresents}");
        
        // Libera il flag dopo un breve delay
        StartCoroutine(ReleasePresentUILock());
    }
    
    private IEnumerator ReleaseMemoryUILock()
    {
        yield return new WaitForSeconds(0.1f);
        isUpdatingMemoryUI = false;
    }
    
    private IEnumerator ReleasePresentUILock()
    {
        yield return new WaitForSeconds(0.1f);
        isUpdatingPresentUI = false;
    }
    
    private void UpdateMemoryCounterDisplay()
    {
        if (memoryCounterText != null && totalSceneMemories > 0)
        {
            // USA SEMPRE IL TEMPLATE ORIGINALE, non il testo corrente
            string updatedText = UpdateNumbersInTemplate(memoryTextTemplate, currentSceneMemories, totalSceneMemories);
            memoryCounterText.text = updatedText;
            
            // Cambia colore se tutte raccolte, altrimenti usa il colore originale
            if (currentSceneMemories >= totalSceneMemories)
            {
                memoryCounterText.color = Color.green;
            }
            else
            {
                memoryCounterText.color = originalMemoryColor;
            }
            
            Debug.Log($"[PlayerUI] Memory text aggiornato: '{updatedText}'");
        }
    }
    
    private void UpdatePresentCounterDisplay()
    {
        if (presentCounterText != null && totalScenePresents > 0)
        {
            // USA SEMPRE IL TEMPLATE ORIGINALE, non il testo corrente
            string updatedText = UpdateNumbersInTemplate(presentTextTemplate, currentScenePresents, totalScenePresents);
            presentCounterText.text = updatedText;
            
            // Cambia colore se tutti raccolti, altrimenti usa il colore originale
            if (currentScenePresents >= totalScenePresents)
            {
                presentCounterText.color = Color.green;
            }
            else
            {
                presentCounterText.color = originalPresentColor;
            }
            
            Debug.Log($"[PlayerUI] Present text aggiornato: '{updatedText}'");
        }
    }
    
    private string UpdateNumbersInTemplate(string template, int current, int total)
    {
        // Metodo semplice: cerca il pattern X/Y e sostituiscilo
        if (template.Contains("/"))
        {
            // Usa Regex per trovare e sostituire numeri nel formato X/Y
            string result = Regex.Replace(template, @"\d+/\d+", $"{current}/{total}");
            Debug.Log($"[PlayerUI] Template aggiornato: '{template}' -> '{result}'");
            return result;
        }
        
        // Se non trova il pattern, aggiungi alla fine
        string fallbackResult = template.TrimEnd() + $" {current}/{total}";
        Debug.Log($"[PlayerUI] Fallback: '{template}' -> '{fallbackResult}'");
        return fallbackResult;
    }
    
    // ========== PANEL ANIMATION METHODS ==========
    
    private void ShowMemoryPanel()
    {
        GameObject targetPanel = memoryPanel != null ? memoryPanel : memoryCounterText?.gameObject;
        if (targetPanel != null)
        {
            // Ferma animazione precedente se attiva
            if (memoryPanelCoroutine != null)
            {
                StopCoroutine(memoryPanelCoroutine);
            }
            
            memoryPanelCoroutine = StartCoroutine(ShowPanelWithAnimation(targetPanel, "Memory"));
        }
    }
    
    private void ShowPresentPanel()
    {
        GameObject targetPanel = presentPanel != null ? presentPanel : presentCounterText?.gameObject;
        if (targetPanel != null)
        {
            // Ferma animazione precedente se attiva
            if (presentPanelCoroutine != null)
            {
                StopCoroutine(presentPanelCoroutine);
            }
            
            presentPanelCoroutine = StartCoroutine(ShowPanelWithAnimation(targetPanel, "Present"));
        }
    }
    
    // 🔥 ANIMAZIONE CHE PRESERVA LA POSIZIONE ORIGINALE
    private System.Collections.IEnumerator ShowPanelWithAnimation(GameObject panel, string panelType)
    {
        if (panel == null) yield break;
        
        Debug.Log($"[PlayerUI] Avvio animazione {panelType} panel: {panel.name}");
        
        // 🔥 PRIMO: Ripristina lo stato originale PRIMA di iniziare l'animazione
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        CanvasGroup canvasGroup = panel.GetComponent<CanvasGroup>();
        
        if (panelRect == null) yield break;
        
        // Ottieni lo stato originale
        UIElementState originalState;
        if (panelType == "Memory")
        {
            originalState = memoryPanelOriginalState;
        }
        else if (panelType == "Present")
        {
            originalState = presentPanelOriginalState;
        }
        else
        {
            yield break;
        }
        
        // Se non ha un CanvasGroup, aggiungilo per l'animazione di fade
        if (canvasGroup == null)
        {
            canvasGroup = panel.AddComponent<CanvasGroup>();
        }
        
        // 🔥 RIPRISTINA la posizione originale PRIMA dell'animazione
        panelRect.anchoredPosition = originalState.anchoredPosition;
        panelRect.rotation = originalState.rotation;
        
        // Attiva il pannello e prepara l'animazione
        panel.SetActive(true);
        
        // Inizia l'animazione di entrata (scale + fade)
        float elapsed = 0f;
        panelRect.localScale = Vector3.zero;
        canvasGroup.alpha = 0f;
        
        // Animazione di entrata
        while (elapsed < panelAnimationSpeed)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / panelAnimationSpeed;
            float easedProgress = panelEaseInOut.Evaluate(progress);
            
            panelRect.localScale = Vector3.Lerp(Vector3.zero, originalState.localScale, easedProgress);
            canvasGroup.alpha = Mathf.Lerp(0f, 1f, easedProgress);
            
            // 🔥 MANTIENI SEMPRE la posizione originale durante l'animazione
            panelRect.anchoredPosition = originalState.anchoredPosition;
            panelRect.rotation = originalState.rotation;
            
            yield return null;
        }
        
        // 🔥 ASSICURA che sia completamente nello stato originale
        panelRect.localScale = originalState.localScale;
        panelRect.anchoredPosition = originalState.anchoredPosition;
        panelRect.rotation = originalState.rotation;
        canvasGroup.alpha = 1f;
        
        Debug.Log($"[PlayerUI] {panelType} panel mostrato nella posizione originale: {originalState.anchoredPosition}");
        
        // Aspetta il tempo di visualizzazione
        yield return new WaitForSeconds(panelShowDuration);
        
        // Inizia l'animazione di uscita
        yield return StartCoroutine(HidePanelWithAnimation(panel, panelType));
        
        // Resetta la coroutine reference
        if (panelType == "Memory")
        {
            memoryPanelCoroutine = null;
        }
        else if (panelType == "Present")
        {
            presentPanelCoroutine = null;
        }
    }
    
    private System.Collections.IEnumerator HidePanelWithAnimation(GameObject panel, string panelType)
    {
        if (panel == null || !panel.activeInHierarchy) yield break;
        
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        CanvasGroup canvasGroup = panel.GetComponent<CanvasGroup>();
        
        if (canvasGroup == null || panelRect == null) yield break;
        
        Debug.Log($"[PlayerUI] Nascondo {panelType} panel: {panel.name}");
        
        // Ottieni lo stato originale
        UIElementState originalState;
        if (panelType == "Memory")
        {
            originalState = memoryPanelOriginalState;
        }
        else if (panelType == "Present")
        {
            originalState = presentPanelOriginalState;
        }
        else
        {
            yield break;
        }
        
        // Animazione di uscita
        float elapsed = 0f;
        
        while (elapsed < panelAnimationSpeed)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / panelAnimationSpeed;
            float easedProgress = panelEaseInOut.Evaluate(progress);
            
            panelRect.localScale = Vector3.Lerp(originalState.localScale, Vector3.zero, easedProgress);
            canvasGroup.alpha = Mathf.Lerp(1f, 0f, easedProgress);
            
            // 🔥 MANTIENI SEMPRE la posizione originale anche durante l'uscita
            panelRect.anchoredPosition = originalState.anchoredPosition;
            panelRect.rotation = originalState.rotation;
            
            yield return null;
        }
        
        // 🔥 RIPRISTINA COMPLETAMENTE lo stato originale prima di nascondere
        panelRect.localScale = originalState.localScale;
        panelRect.anchoredPosition = originalState.anchoredPosition;
        panelRect.rotation = originalState.rotation;
        canvasGroup.alpha = originalState.alpha;
        panel.SetActive(false);
        
        Debug.Log($"[PlayerUI] {panelType} panel nascosto e ripristinato alla posizione originale: {originalState.anchoredPosition}");
    }
    
    private void UpdateAllCounterDisplays()
    {
        UpdateMemoryCounterDisplay();
        UpdatePresentCounterDisplay();
    }
    
    // Metodo per forzare la visualizzazione di un pannello (per debug)
    [ContextMenu("Show Memory Panel")]
    public void DebugShowMemoryPanel()
    {
        ShowMemoryPanel();
    }
    
    [ContextMenu("Show Present Panel")]  
    public void DebugShowPresentPanel()
    {
        ShowPresentPanel();
    }
    
    private void OnAllSceneMemoriesCompleted()
    {
        Debug.Log("[PlayerUI] Tutte le memorie della scena completate!");
        
        if (memoryCounterText != null)
        {
            memoryCounterText.color = new Color(1f, 0.84f, 0f, 1f); // Colore oro
            StartCoroutine(CelebrationEffect(memoryCounterText.transform, memoryIcon?.transform));
        }
    }
    
    private void OnAllScenePresentsCompleted()
    {
        Debug.Log("[PlayerUI] Tutti i presents della scena completati!");
        
        if (presentCounterText != null)
        {
            presentCounterText.color = new Color(1f, 0.84f, 0f, 1f); // Colore oro
            StartCoroutine(CelebrationEffect(presentCounterText.transform, presentIcon?.transform));
        }
    }
    
    private void OnAllSceneCollectiblesCompleted()
    {
        Debug.Log("[PlayerUI] TUTTI i collectibles della scena completati! 🎉");
        
        // Celebrazione completa
        StartCoroutine(FullSceneCelebration());
    }
    
    private void AnimateMemoryCounter()
    {
        // Ferma animazione precedente
        if (memoryAnimationCoroutine != null)
        {
            StopCoroutine(memoryAnimationCoroutine);
        }
        
        memoryAnimationCoroutine = StartCoroutine(AnimateMemoryCounterCoroutine());
    }
    
    private void AnimatePresentCounter()
    {
        // Ferma animazione precedente
        if (presentAnimationCoroutine != null)
        {
            StopCoroutine(presentAnimationCoroutine);
        }
        
        presentAnimationCoroutine = StartCoroutine(AnimatePresentCounterCoroutine());
    }
    
    private System.Collections.IEnumerator AnimateMemoryCounterCoroutine()
    {
        if (memoryCounterText != null)
        {
            yield return StartCoroutine(PositionPreservingScaleAnimation(memoryTextRect, memoryTextOriginalState));
        }
        
        if (memoryIcon != null)
        {
            yield return StartCoroutine(PositionPreservingScaleAnimation(memoryIconRect, memoryIconOriginalState));
        }
        
        memoryAnimationCoroutine = null;
    }
    
    private System.Collections.IEnumerator AnimatePresentCounterCoroutine()
    {
        if (presentCounterText != null)
        {
            yield return StartCoroutine(PositionPreservingScaleAnimation(presentTextRect, presentTextOriginalState));
        }
        
        if (presentIcon != null)
        {
            yield return StartCoroutine(PositionPreservingScaleAnimation(presentIconRect, presentIconOriginalState));
        }
        
        presentAnimationCoroutine = null;
    }
    
    // 🔥 ANIMAZIONE CHE PRESERVA LA POSIZIONE ORIGINALE
    private System.Collections.IEnumerator PositionPreservingScaleAnimation(RectTransform targetRect, UIElementState originalState)
    {
        if (targetRect == null) yield break;
        
        float elapsed = 0f;
        float halfDuration = animationDuration * 0.5f;
        
        // Scale up
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / halfDuration;
            targetRect.localScale = Vector3.Lerp(originalState.localScale, originalState.localScale * punchScale, t);
            
            // 🔥 MANTIENI SEMPRE la posizione e rotazione originale
            targetRect.anchoredPosition = originalState.anchoredPosition;
            targetRect.rotation = originalState.rotation;
            
            yield return null;
        }
        
        elapsed = 0f;
        
        // Scale down
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / halfDuration;
            targetRect.localScale = Vector3.Lerp(originalState.localScale * punchScale, originalState.localScale, t);
            
            // 🔥 MANTIENI SEMPRE la posizione e rotazione originale
            targetRect.anchoredPosition = originalState.anchoredPosition;
            targetRect.rotation = originalState.rotation;
            
            yield return null;
        }
        
        // 🔥 RIPRISTINA COMPLETAMENTE lo stato originale
        targetRect.localScale = originalState.localScale;
        targetRect.anchoredPosition = originalState.anchoredPosition;
        targetRect.rotation = originalState.rotation;
    }
    
    private System.Collections.IEnumerator CelebrationEffect(Transform textTransform, Transform iconTransform)
    {
        // Effetto di celebrazione multiplo
        for (int i = 0; i < 3; i++)
        {
            if (textTransform != null)
            {
                RectTransform textRect = textTransform as RectTransform;
                if (textRect != null)
                {
                    UIElementState state = textRect == memoryTextRect ? memoryTextOriginalState : presentTextOriginalState;
                    StartCoroutine(PositionPreservingScaleAnimation(textRect, state));
                }
            }
                
            if (iconTransform != null)
            {
                RectTransform iconRect = iconTransform as RectTransform;
                if (iconRect != null)
                {
                    UIElementState state = iconRect == memoryIconRect ? memoryIconOriginalState : presentIconOriginalState;
                    StartCoroutine(PositionPreservingRotationEffect(iconRect, state));
                }
            }
                
            yield return new WaitForSeconds(0.2f);
        }
    }
    
    private System.Collections.IEnumerator FullSceneCelebration()
    {
        // Celebrazione completa per tutti i collectibles
        if (memoryCounterText != null && memoryCounterText.gameObject.activeInHierarchy)
        {
            StartCoroutine(CelebrationEffect(memoryCounterText.transform, memoryIcon?.transform));
        }
        
        yield return new WaitForSeconds(0.1f);
        
        if (presentCounterText != null && presentCounterText.gameObject.activeInHierarchy)
        {
            StartCoroutine(CelebrationEffect(presentCounterText.transform, presentIcon?.transform));
        }
    }
    
    // 🔥 ROTAZIONE CHE PRESERVA LA POSIZIONE ORIGINALE
    private System.Collections.IEnumerator PositionPreservingRotationEffect(RectTransform targetRect, UIElementState originalState)
    {
        if (targetRect == null) yield break;
        
        float elapsed = 0f;
        float duration = 0.5f;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float rotation = Mathf.Lerp(0f, 360f, elapsed / duration);
            targetRect.rotation = Quaternion.Euler(0, 0, rotation);
            
            // 🔥 MANTIENI SEMPRE la posizione e scale originale
            targetRect.anchoredPosition = originalState.anchoredPosition;
            targetRect.localScale = originalState.localScale;
            
            yield return null;
        }
        
        // 🔥 RIPRISTINA COMPLETAMENTE lo stato originale
        targetRect.rotation = originalState.rotation;
        targetRect.anchoredPosition = originalState.anchoredPosition;
        targetRect.localScale = originalState.localScale;
    }
    
    // Metodo per reinizializzare quando cambi scena
    public void OnSceneChanged()
    {
        // Ferma tutte le animazioni attive
        StopAllUIAnimations();
        
        // Reset valori
        currentSceneMemories = 0;
        totalSceneMemories = 0;
        currentScenePresents = 0;
        totalScenePresents = 0;
        isUpdatingMemoryUI = false;
        isUpdatingPresentUI = false;
        
        // 🔥 RICACHE le posizioni per la nuova scena
        CacheOriginalUIStates();
        
        // Risalva i template per la nuova scena
        SaveOriginalTextFormats();
        
        InitializeCollectibleSystem();
        
        Debug.Log("[PlayerUI] Reinizializzato per nuova scena con nuove posizioni originali");
    }
    
    // ========== PUBLIC API METHODS FOR UNIVERSAL COMPATIBILITY ==========
    
    // 🔥 METODI PUBBLICI PER CONNESSIONE MANUALE DA QUALSIASI SCENE MANAGER
    
    /// <summary>
    /// Connetti manualmente a un SceneManager specifico
    /// Utile se il tuo SceneManager ha un nome diverso o logica particolare
    /// </summary>
    public bool ConnectToSceneManager(MonoBehaviour sceneManager)
    {
        if (sceneManager == null)
        {
            Debug.LogError("[PlayerUI] SceneManager fornito è null");
            return false;
        }
        
        // Disconnetti prima da eventuali connessioni esistenti
        DisconnectFromAllSystems();
        
        System.Type managerType = sceneManager.GetType();
        
        // Verifica che abbia i metodi necessari
        if (!HasRequiredSceneManagerMethods(managerType))
        {
            Debug.LogError($"[PlayerUI] {managerType.Name} non ha i metodi richiesti per essere compatibile");
            return false;
        }
        
        try
        {
            // Connetti agli eventi
            ConnectToSceneManagerEvents(sceneManager, managerType);
            InitializeFromSceneManager(sceneManager, managerType);
            
            currentSceneManager = sceneManager;
            currentSceneManagerType = managerType;
            isConnectedToSceneManager = true;
            
            Debug.Log($"[PlayerUI] ✅ Connesso manualmente a {managerType.Name}");
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[PlayerUI] Errore durante connessione manuale a {managerType.Name}: {e.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// Aggiorna manualmente i contatori (utile per debug o casi speciali)
    /// </summary>
    public void UpdateCountersManually(int currentMemories, int totalMemories, int currentPresents, int totalPresents)
    {
        Debug.Log($"[PlayerUI] Aggiornamento manuale contatori: M={currentMemories}/{totalMemories}, P={currentPresents}/{totalPresents}");
        
        currentSceneMemories = currentMemories;
        totalSceneMemories = totalMemories;
        currentScenePresents = currentPresents;
        totalScenePresents = totalPresents;
        
        UpdateAllCounterDisplays();
    }
    
    /// <summary>
    /// Verifica se il PlayerUI è attualmente connesso a un sistema di tracking
    /// </summary>
    public bool IsConnectedToTrackingSystem()
    {
        return isConnectedToSceneManager || PlayerCollectibleTracker.Instance != null;
    }
    
    /// <summary>
    /// Ottieni informazioni sul sistema di tracking attualmente connesso
    /// </summary>
    public string GetConnectedSystemInfo()
    {
        if (isConnectedToSceneManager && currentSceneManagerType != null)
        {
            return $"SceneManager: {currentSceneManagerType.Name}";
        }
        else if (PlayerCollectibleTracker.Instance != null)
        {
            return "PlayerCollectibleTracker";
        }
        else
        {
            return "Nessun sistema connesso";
        }
    }
    
    // ========== GETTERS PUBBLICI ==========
    
    public int GetCurrentSceneMemories() => currentSceneMemories;
    public int GetTotalSceneMemories() => totalSceneMemories;
    public int GetCurrentScenePresents() => currentScenePresents;
    public int GetTotalScenePresents() => totalScenePresents;
    public int GetCurrentSceneCollectibles() => currentSceneMemories + currentScenePresents;
    public int GetTotalSceneCollectibles() => totalSceneMemories + totalScenePresents;
    
    // ========== DEBUG METHODS ==========
    
    [ContextMenu("🌟 Debug - Show Connected System")]
    public void DebugShowConnectedSystem()
    {
        Debug.Log($"=== Sistema Connesso ===\n" +
                  $"Connesso a: {GetConnectedSystemInfo()}\n" +
                  $"Scene Manager Type: {currentSceneManagerType?.Name ?? "N/A"}\n" +
                  $"Is Connected: {IsConnectedToTrackingSystem()}");
    }
    
    [ContextMenu("🔄 Debug - Force Reconnect")]
    public void DebugForceReconnect()
    {
        Debug.Log("[PlayerUI] Forzando riconnessione...");
        InitializeCollectibleSystem();
        DebugShowConnectedSystem();
    }
    
    [ContextMenu("Debug - Force Update Present Counter")]
    public void DebugForceUpdatePresents()
    {
        UpdateScenePresentCounter(currentScenePresents + 1, totalScenePresents);
    }
    
    [ContextMenu("Debug - Force Update Memory Counter")]
    public void DebugForceUpdateMemories()
    {
        UpdateSceneMemoryCounter(currentSceneMemories + 1, totalSceneMemories);
    }
    
    [ContextMenu("Debug - Reset All Counters")]
    public void DebugResetCounters()
    {
        currentSceneMemories = 0;
        currentScenePresents = 0;
        UpdateAllCounterDisplays();
    }
    
    [ContextMenu("Debug - Stop All Animations")]
    public void DebugStopAllAnimations()
    {
        StopAllUIAnimations();
    }
    
    [ContextMenu("🔥 Debug - Restore Original Positions")]
    public void DebugRestoreOriginalPositions()
    {
        RestoreAllOriginalStates();
    }
    
    [ContextMenu("🔥 Debug - Show Original States")]
    public void DebugShowOriginalStates()
    {
        Debug.Log($"=== Stati Originali UI ===\n" +
                  $"Memory Panel: pos={memoryPanelOriginalState.anchoredPosition}, scale={memoryPanelOriginalState.localScale}\n" +
                  $"Present Panel: pos={presentPanelOriginalState.anchoredPosition}, scale={presentPanelOriginalState.localScale}\n" +
                  $"Memory Text: pos={memoryTextOriginalState.anchoredPosition}, scale={memoryTextOriginalState.localScale}\n" +
                  $"Present Text: pos={presentTextOriginalState.anchoredPosition}, scale={presentTextOriginalState.localScale}");
    }
    
    [ContextMenu("Debug - Show All Info")]
    public void DebugShowInfo()
    {
        Debug.Log($"=== PlayerUI Debug Info ===\n" +
                  $"Sistema Connesso: {GetConnectedSystemInfo()}\n" +
                  $"Memories: {currentSceneMemories}/{totalSceneMemories}\n" +
                  $"Presents: {currentScenePresents}/{totalScenePresents}\n" +
                  $"Memory Template: '{memoryTextTemplate}'\n" +
                  $"Present Template: '{presentTextTemplate}'\n" +
                  $"Updating Memory UI: {isUpdatingMemoryUI}\n" +
                  $"Updating Present UI: {isUpdatingPresentUI}\n" +
                  $"Memory Panel Coroutine: {memoryPanelCoroutine != null}\n" +
                  $"Present Panel Coroutine: {presentPanelCoroutine != null}");
    }
    
    // ========== EXISTING METHODS ==========

    public void UpdateAbilityIconState(int index, bool canActivate)
    {
        if (abilityIcons != null && index >= 0 && index < abilityIcons.Length && abilityIcons[index] != null)
        {
            abilityIcons[index].SetGrayscale(!canActivate);
            abilityIcons[index].UpdateInputText(useGamepad);
        }
    }

    public void PulseIconAt(int index)
    {
        if (abilityIcons != null && index >= 0 && index < abilityIcons.Length)
        {
            abilityIcons[index]?.PulseIcon();
        }
    }

    public void UpdateHealth(float currentHealth)
    {
        if (healthFill == null || playerController == null)
        {
            Debug.LogWarning("[PlayerUI] healthFill o playerController non assegnato!");
            return;
        }

        float fillAmount = currentHealth / playerController.MaxHealth;
        healthFill.fillAmount = fillAmount;
    }

    public void UpdatePower(float currentPower)
    {
        Debug.Log($"[PlayerUI] UpdatePower chiamato con: {currentPower}");
        
        if (powerFill == null)
        {
            Debug.LogError("[PlayerUI] powerFill non assegnato!");
            return;
        }

        if (playerPowerUp == null)
        {
            Debug.LogError("[PlayerUI] playerPowerUp non assegnato!");
            playerPowerUp = Object.FindFirstObjectByType<PlayerPowerUp>();
            
            if (playerPowerUp == null)
            {
                Debug.LogError("[PlayerUI] PlayerPowerUp non trovato nemmeno in scena!");
                return;
            }
            Debug.Log("[PlayerUI] PlayerPowerUp trovato automaticamente!");
        }

        float maxPower = playerPowerUp.MaxPower;
        Debug.Log($"[PlayerUI] MaxPower: {maxPower}, CurrentPower: {currentPower}");
        
        if (maxPower <= 0f)
        {
            Debug.LogWarning("[PlayerUI] MaxPower è 0 o negativo!");
            powerFill.fillAmount = 0f;
            return;
        }

        float fillAmount = currentPower / maxPower;
        fillAmount = Mathf.Clamp01(fillAmount);

        Debug.Log($"[PlayerUI] Settando fillAmount a: {fillAmount}");
        
        if (!powerFill.gameObject.activeInHierarchy)
        {
            Debug.LogWarning("[PlayerUI] powerFill non è attivo nella gerarchia!");
            powerFill.gameObject.SetActive(true);
        }
        
        powerFill.fillAmount = fillAmount;
        powerFill.SetAllDirty();
        
        Debug.Log($"[PlayerUI] Verificando fillAmount settato: {powerFill.fillAmount}");
    }

    public void SetMaxValues(float maxHealth, float maxPower)
    {
        if (playerController != null)
        {
            playerController.maxHealth = maxHealth;
            UpdateHealth(playerController.currentHealth);
        }
        if (playerPowerUp != null)
        {
            playerPowerUp.maxPower = maxPower;
            UpdatePower(playerPowerUp.currentPower);
        }
    }
    
    private void OnDestroy()
    {
        CleanupEvents();
        
        if (Instance == this)
        {
            Instance = null;
        }
    }
}