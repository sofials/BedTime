using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Text.RegularExpressions;
using System.Collections;
using System.Reflection;

public class PlayerCollectiblesUI : MonoBehaviour
{
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

    // Scene collectibles tracking
    private int currentSceneMemories = 0;
    private int totalSceneMemories = 0;
    private int currentScenePresents = 0;
    private int totalScenePresents = 0;

    // Template strings per preservare la formattazione (IMMUTABILI)
    private string memoryTextTemplate = "";
    // Removed presentTextTemplate since it was unused
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

    // 🔥 UNIVERSAL SCENE MANAGER SUPPORT - OTTIMIZZATO
    private MonoBehaviour currentCollectiblesManager = null;
    private System.Type currentCollectiblesManagerType = null;
    private bool isConnectedToCollectiblesManager = false;

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

    public static PlayerCollectiblesUI Instance { get; private set; }

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
        AutoConnectToAvailableManagers();

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
    }

    private void SaveOriginalTextFormats()
    {
        // Salva il template per le memories
        if (memoryCounterText != null)
        {
            memoryTextTemplate = memoryCounterText.text;
            originalMemoryColor = memoryCounterText.color;

            // Se il testo è vuoto o non contiene il pattern X/Y, usa un template di default
            if (string.IsNullOrEmpty(memoryTextTemplate) || !Regex.IsMatch(memoryTextTemplate, @"\d+/\d+"))
            {
                memoryTextTemplate = "memories 0/0";
            }
        }

        // Save original color for presents (no template needed since using direct formatting)
        if (presentCounterText != null)
        {
            originalPresentColor = presentCounterText.color;
        }
    }





    // 🔥 VERIFICA SE UN TIPO HA I METODI RICHIESTI PER SCENE MANAGER
    private bool HasRequiredCollectiblesManagerMethods(System.Type type)
    {
        // 🎯 SOLO i metodi per le MEMORIES sono obbligatori
        string[] requiredMethods = {
            "GetCollectedMemories", "GetTotalMemories"
        };

        foreach (string methodName in requiredMethods)
        {
            if (type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance) == null)
            {
                Debug.Log($"[PlayerCollectiblesUI] {type.Name} manca il metodo richiesto: {methodName}");
                return false;
            }
        }

        // 🔥 CONTROLLO OPZIONALE: Verifica se supporta i presents
        bool supportsPresents =
            type.GetMethod("GetCollectedPresents", BindingFlags.Public | BindingFlags.Instance) != null &&
            type.GetMethod("GetTotalPresents", BindingFlags.Public | BindingFlags.Instance) != null;

        Debug.Log($"[PlayerCollectiblesUI] {type.Name} - Memories: ✅, Presents: {(supportsPresents ? "✅" : "❌ (opzionale)")}");

        return true; // Basta che abbia i metodi per le memories
    }

    // 🔥 CONNESSIONE EVENTI OTTIMIZZATA: UnityEvent + C# Events
    private bool ConnectToCollectiblesManagerEventsOptimized(MonoBehaviour manager, System.Type managerType)
    {
        // 🎯 STRATEGIA 1: Prova connessione UnityEvent (per SceneManager00, 01, 02, etc.)
        if (TryConnectUnityEvents(manager, managerType))
        {
            return true;
        }

        // 🎯 STRATEGIA 2: Fallback a C# Events via Reflection
        if (TryConnectCSharpEvents(manager, managerType))
        {
            return true;
        }

        return false;
    }


    private bool TryConnectUnityEvents(MonoBehaviour manager, System.Type managerType)
    {
        try
        {
            // Cerca campi UnityEvent nel tipo
            var memoryEvent = managerType.GetField("OnMemoryCountChanged", BindingFlags.Public | BindingFlags.Instance);
            var presentEvent = managerType.GetField("OnPresentCountChanged", BindingFlags.Public | BindingFlags.Instance);
            var allMemoriesEvent = managerType.GetField("OnAllMemoriesCollected", BindingFlags.Public | BindingFlags.Instance);
            var allPresentsEvent = managerType.GetField("OnAllPresentsCollected", BindingFlags.Public | BindingFlags.Instance);
            var allCollectiblesEvent = managerType.GetField("OnAllCollectiblesCompleted", BindingFlags.Public | BindingFlags.Instance);

            bool hasUnityEvents = false;

            // 🎯 SEMPRE: Connetti eventi memories (obbligatori)
            if (memoryEvent != null && memoryEvent.FieldType.Name.Contains("UnityEvent"))
            {
                var unityEvent = memoryEvent.GetValue(manager) as UnityEngine.Events.UnityEvent<int, int>;
                if (unityEvent != null)
                {
                    unityEvent.AddListener(UpdateSceneMemoryCounter);
                    hasUnityEvents = true;
                    Debug.Log("[PlayerCollectiblesUI] ✅ OnMemoryCountChanged UnityEvent connesso");
                }
            }

            // 🔥 OPZIONALE: Connetti eventi presents solo se esistono
            if (presentEvent != null && presentEvent.FieldType.Name.Contains("UnityEvent"))
            {
                var unityEvent = presentEvent.GetValue(manager) as UnityEngine.Events.UnityEvent<int, int>;
                if (unityEvent != null)
                {
                    unityEvent.AddListener(UpdateScenePresentCounter);
                    hasUnityEvents = true;
                    Debug.Log("[PlayerCollectiblesUI] ✅ OnPresentCountChanged UnityEvent connesso");
                }
            }
            else
            {
                Debug.Log("[PlayerCollectiblesUI] ⚠️ OnPresentCountChanged non disponibile (opzionale)");
            }

            // 🎯 SEMPRE: Connetti eventi completion memories
            if (allMemoriesEvent != null && allMemoriesEvent.FieldType.Name.Contains("UnityEvent"))
            {
                var unityEvent = allMemoriesEvent.GetValue(manager) as UnityEngine.Events.UnityEvent;
                if (unityEvent != null)
                {
                    unityEvent.AddListener(OnAllSceneMemoriesCompleted);
                    hasUnityEvents = true;
                    Debug.Log("[PlayerCollectiblesUI] ✅ OnAllMemoriesCollected UnityEvent connesso");
                }
            }

            // 🔥 OPZIONALE: Connetti eventi completion presents
            if (allPresentsEvent != null && allPresentsEvent.FieldType.Name.Contains("UnityEvent"))
            {
                var unityEvent = allPresentsEvent.GetValue(manager) as UnityEngine.Events.UnityEvent;
                if (unityEvent != null)
                {
                    unityEvent.AddListener(OnAllScenePresentsCompleted);
                    hasUnityEvents = true;
                    Debug.Log("[PlayerCollectiblesUI] ✅ OnAllPresentsCollected UnityEvent connesso");
                }
            }

            // 🔥 OPZIONALE: Connetti evento completion completo
            if (allCollectiblesEvent != null && allCollectiblesEvent.FieldType.Name.Contains("UnityEvent"))
            {
                var unityEvent = allCollectiblesEvent.GetValue(manager) as UnityEngine.Events.UnityEvent;
                if (unityEvent != null)
                {
                    unityEvent.AddListener(OnAllSceneCollectiblesCompleted);
                    hasUnityEvents = true;
                    Debug.Log("[PlayerCollectiblesUI] ✅ OnAllCollectiblesCompleted UnityEvent connesso");
                }
            }

            return hasUnityEvents;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[PlayerCollectiblesUI] Errore connessione UnityEvent: {e.Message}");
            return false;
        }
    }
    /// <summary>
    /// Metodo pubblico specifico per connettere al CollectiblesManager
    /// </summary>
    public bool ConnectToCollectiblesManager(CollectiblesManager manager)
    {
        if (manager == null)
        {
            Debug.LogWarning("[PlayerCollectiblesUI] CollectiblesManager è null, impossibile connettersi");
            return false;
        }

        // Disconnetti da qualsiasi manager precedente
        DisconnectFromCurrentManager();

        // Salva il riferimento al nuovo manager
        currentCollectiblesManager = manager;
        currentCollectiblesManagerType = manager.GetType();

        // Connetti agli eventi del CollectiblesManager
        bool connected = ConnectToCollectiblesManagerEventsOptimized(manager, currentCollectiblesManagerType);

        if (connected)
        {
            // Inizializza i valori dal manager
            InitializeFromSceneManager(manager, currentCollectiblesManagerType);
            isConnectedToCollectiblesManager = true;

            Debug.Log($"[PlayerCollectiblesUI] ✅ Connesso con successo a {currentCollectiblesManagerType.Name}");
            return true;
        }
        else
        {
            Debug.LogError($"[PlayerCollectiblesUI] ❌ Connessione fallita con {currentCollectiblesManagerType.Name}");
            currentCollectiblesManager = null;
            currentCollectiblesManagerType = null;
            return false;
        }
    }
    // AGGIUNGI QUESTO NUOVO METODO per la ricerca automatica:
    private void AutoConnectToAvailableManagers()
    {
        // Priorità 1: Cerca CollectiblesManager
        CollectiblesManager collectiblesManager = CollectiblesManager.Instance;
        if (collectiblesManager == null)
        {
            collectiblesManager = Object.FindFirstObjectByType<CollectiblesManager>();
        }

        if (collectiblesManager != null)
        {
            bool connected = ConnectToCollectiblesManager(collectiblesManager);
            if (connected)
            {
                Debug.Log("[PlayerCollectiblesUI] Auto-connesso a CollectiblesManager");
                return;
            }
        }
    }
    // AGGIUNGI QUESTO METODO per disconnessione sicura:
    private void DisconnectFromCurrentManager()
    {
        if (currentCollectiblesManager == null || currentCollectiblesManagerType == null)
        {
            return;
        }

        Debug.Log($"[PlayerCollectiblesUI] Disconnessione da {currentCollectiblesManagerType.Name}");

        // Prova disconnessione UnityEvents
        TryDisconnectUnityEvents(currentCollectiblesManager, currentCollectiblesManagerType);

        // Prova disconnessione C# Events
        TryDisconnectCSharpEvents(currentCollectiblesManager, currentCollectiblesManagerType);

        // Se era un PlayerCollectibleTracker, usa il metodo specifico
        if (currentCollectiblesManager is PlayerCollectibleTracker tracker)
        {
            CleanupTrackerEvents(tracker);
        }

        // Reset riferimenti
        currentCollectiblesManager = null;
        currentCollectiblesManagerType = null;
        isConnectedToCollectiblesManager = false;
    }

    // 🔥 CONNETTI C# EVENTS (fallback)
    private bool TryConnectCSharpEvents(MonoBehaviour manager, System.Type managerType)
    {
        try
        {
            object instance = GetSceneManagerInstance(manager, managerType);
            if (instance == null) return false;

            // Lista eventi con flag di obbligatorietà
            var eventConnections = new[]
            {
                ("OnMemoryCountChanged", "UpdateSceneMemoryCounter", true),      // Obbligatorio
                ("OnPresentCountChanged", "UpdateScenePresentCounter", false),   // Opzionale
                ("OnAllMemoriesCollected", "OnAllSceneMemoriesCompleted", true), // Obbligatorio
                ("OnAllPresentsCollected", "OnAllScenePresentsCompleted", false), // Opzionale
                ("OnAllCollectiblesCompleted", "OnAllSceneCollectiblesCompleted", false) // Opzionale
            };

            bool hasEvents = false;
            foreach (var (eventName, handlerName, required) in eventConnections)
            {
                bool connected = TryConnectEvent(instance, managerType, eventName, handlerName);
                if (connected)
                {
                    hasEvents = true;
                }
                else if (required)
                {
                    Debug.LogWarning($"[PlayerCollectiblesUI] Evento obbligatorio {eventName} non trovato in {managerType.Name}");
                }
            }

            return hasEvents;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[PlayerCollectiblesUI] Errore connessione C# Events: {e.Message}");
            return false;
        }
    }

    // 🔥 OTTIENI ISTANZA SCENE MANAGER
    private object GetSceneManagerInstance(MonoBehaviour manager, System.Type managerType)
    {
        // Prova prima con proprietà Instance
        PropertyInfo instanceProperty = managerType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
        if (instanceProperty != null)
        {
            object instance = instanceProperty.GetValue(null);
            if (instance != null) return instance;
        }

        // Fallback: usa il MonoBehaviour direttamente
        return manager;
    }

    // 🔥 CONNETTI UN SINGOLO EVENTO
    private bool TryConnectEvent(object instance, System.Type managerType, string eventName, string handlerName)
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
                    Debug.Log($"[PlayerCollectiblesUI] Evento {eventName} connesso a {handlerName}");
                    return true;
                }
            }
            return false;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[PlayerCollectiblesUI] Impossibile connettere evento {eventName}: {e.Message}");
            return false;
        }
    }

    // 🔥 INIZIALIZZA VALORI DA SCENE MANAGER
    private void InitializeFromSceneManager(MonoBehaviour manager, System.Type managerType)
    {
        try
        {
            object instance = GetSceneManagerInstance(manager, managerType);
            if (instance == null) return;

            // 🎯 SEMPRE: Inizializza le memories (obbligatorie)
            MethodInfo getCollectedMemories = managerType.GetMethod("GetCollectedMemories");
            MethodInfo getTotalMemories = managerType.GetMethod("GetTotalMemories");

            if (getCollectedMemories != null && getTotalMemories != null)
            {
                currentSceneMemories = (int)getCollectedMemories.Invoke(instance, null);
                totalSceneMemories = (int)getTotalMemories.Invoke(instance, null);
                Debug.Log($"[PlayerCollectiblesUI] Memories inizializzate: {currentSceneMemories}/{totalSceneMemories}");
            }

            // 🔥 OPZIONALE: Inizializza i presents solo se supportati
            MethodInfo getCollectedPresents = managerType.GetMethod("GetCollectedPresents");
            MethodInfo getTotalPresents = managerType.GetMethod("GetTotalPresents");

            if (getCollectedPresents != null && getTotalPresents != null)
            {
                currentScenePresents = (int)getCollectedPresents.Invoke(instance, null);
                totalScenePresents = (int)getTotalPresents.Invoke(instance, null);
                Debug.Log($"[PlayerCollectiblesUI] Presents inizializzati: {currentScenePresents}/{totalScenePresents}");
            }
            else
            {
                // Reset presents se non supportati
                currentScenePresents = 0;
                totalScenePresents = 0;
                Debug.Log($"[PlayerCollectiblesUI] Presents non supportati da {managerType.Name} - azzerati");
            }

            UpdateAllCounterDisplays();

            Debug.Log($"[PlayerCollectiblesUI] Inizializzazione completata da {managerType.Name}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[PlayerCollectiblesUI] Errore inizializzazione da {managerType.Name}: {e.Message}");
        }
    }



    // 🔥 DISCONNETTI UNITY EVENTS
    private bool TryDisconnectUnityEvents(MonoBehaviour manager, System.Type managerType)
    {
        try
        {
            var memoryEvent = managerType.GetField("OnMemoryCountChanged", BindingFlags.Public | BindingFlags.Instance);
            var presentEvent = managerType.GetField("OnPresentCountChanged", BindingFlags.Public | BindingFlags.Instance);
            var allMemoriesEvent = managerType.GetField("OnAllMemoriesCollected", BindingFlags.Public | BindingFlags.Instance);
            var allPresentsEvent = managerType.GetField("OnAllPresentsCollected", BindingFlags.Public | BindingFlags.Instance);
            var allCollectiblesEvent = managerType.GetField("OnAllCollectiblesCompleted", BindingFlags.Public | BindingFlags.Instance);

            bool hasUnityEvents = false;

            if (memoryEvent != null && memoryEvent.FieldType.Name.Contains("UnityEvent"))
            {
                var unityEvent = memoryEvent.GetValue(manager) as UnityEngine.Events.UnityEvent<int, int>;
                unityEvent?.RemoveListener(UpdateSceneMemoryCounter);
                hasUnityEvents = true;
            }

            if (presentEvent != null && presentEvent.FieldType.Name.Contains("UnityEvent"))
            {
                var unityEvent = presentEvent.GetValue(manager) as UnityEngine.Events.UnityEvent<int, int>;
                unityEvent?.RemoveListener(UpdateScenePresentCounter);
                hasUnityEvents = true;
            }

            if (allMemoriesEvent != null && allMemoriesEvent.FieldType.Name.Contains("UnityEvent"))
            {
                var unityEvent = allMemoriesEvent.GetValue(manager) as UnityEngine.Events.UnityEvent;
                unityEvent?.RemoveListener(OnAllSceneMemoriesCompleted);
                hasUnityEvents = true;
            }

            if (allPresentsEvent != null && allPresentsEvent.FieldType.Name.Contains("UnityEvent"))
            {
                var unityEvent = allPresentsEvent.GetValue(manager) as UnityEngine.Events.UnityEvent;
                unityEvent?.RemoveListener(OnAllScenePresentsCompleted);
                hasUnityEvents = true;
            }

            if (allCollectiblesEvent != null && allCollectiblesEvent.FieldType.Name.Contains("UnityEvent"))
            {
                var unityEvent = allCollectiblesEvent.GetValue(manager) as UnityEngine.Events.UnityEvent;
                unityEvent?.RemoveListener(OnAllSceneCollectiblesCompleted);
                hasUnityEvents = true;
            }

            return hasUnityEvents;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[PlayerCollectiblesUI] Errore disconnessione UnityEvent: {e.Message}");
            return false;
        }
    }

    // 🔥 DISCONNETTI C# EVENTS
    private bool TryDisconnectCSharpEvents(MonoBehaviour manager, System.Type managerType)
    {
        try
        {
            object instance = GetSceneManagerInstance(manager, managerType);
            if (instance == null) return false;

            var eventConnections = new[]
            {
                ("OnMemoryCountChanged", "UpdateSceneMemoryCounter"),
                ("OnPresentCountChanged", "UpdateScenePresentCounter"),
                ("OnAllMemoriesCollected", "OnAllSceneMemoriesCompleted"),
                ("OnAllPresentsCollected", "OnAllScenePresentsCompleted"),
                ("OnAllCollectiblesCompleted", "OnAllSceneCollectiblesCompleted")
            };

            bool hasEvents = false;
            foreach (var (eventName, handlerName) in eventConnections)
            {
                if (TryDisconnectEvent(instance, managerType, eventName, handlerName))
                {
                    hasEvents = true;
                }
            }

            return hasEvents;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[PlayerCollectiblesUI] Errore disconnessione C# Events: {e.Message}");
            return false;
        }
    }

    // 🔥 DISCONNETTI UN SINGOLO EVENTO
    private bool TryDisconnectEvent(object instance, System.Type managerType, string eventName, string handlerName)
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
                    Debug.Log($"[PlayerCollectiblesUI] Evento {eventName} disconnesso da {handlerName}");
                    return true;
                }
            }
            return false;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[PlayerCollectiblesUI] Impossibile disconnettere evento {eventName}: {e.Message}");
            return false;
        }
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

        Debug.Log("[PlayerCollectiblesUI] Tutte le animazioni UI fermate e posizioni originali ripristinate");
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

    // ========== SCENE COLLECTIBLES COUNTER METHODS ==========

    // 🔥 HANDLER UNIVERSALI PER QUALSIASI SCENEMANAGER
    private void UpdateSceneMemoryCounter(int collected, int total)
    {
        // Previeni aggiornamenti multipli simultanei
        if (isUpdatingMemoryUI)
        {
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

        // Libera il flag dopo un breve delay
        StartCoroutine(ReleaseMemoryUILock());
    }

    private void UpdateScenePresentCounter(int collected, int total)
    {
        // Previeni aggiornamenti multipli simultanei
        if (isUpdatingPresentUI)
        {
            return;
        }

        isUpdatingPresentUI = true;

        currentScenePresents = collected;
        totalScenePresents = total;
        UpdatePresentCounterDisplay();

        // 🔥 QUESTA È LA PARTE IMPORTANTE: Mostra il pannello con animazione!
        if (showPanelsOnCollect)
        {
            ShowPresentPanel();
        }

        if (animateOnCollect)
        {
            AnimatePresentCounter();
        }

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
        if (memoryCounterText != null && totalSceneMemories >= 0) // Cambiato da > 0 a >= 0
        {
            // USA SEMPRE IL TEMPLATE ORIGINALE, non il testo corrente
            string updatedText = UpdateNumbersInTemplate(memoryTextTemplate, currentSceneMemories, totalSceneMemories);
            memoryCounterText.text = updatedText;

            // Cambia colore se tutte raccolte, altrimenti usa il colore originale
            if (currentSceneMemories >= totalSceneMemories && totalSceneMemories > 0)
            {
                memoryCounterText.color = Color.green;
            }
            else
            {
                memoryCounterText.color = originalMemoryColor;
            }
        }
    }

    private void UpdatePresentCounterDisplay()
    {
        if (presentCounterText != null && totalScenePresents >= 0)
        {
            Debug.Log($"[PlayerCollectiblesUI] Updating present display: {currentScenePresents}/{totalScenePresents}");

            // 🔥 ALWAYS use standardized format: "X/Y"
            string updatedText = $"{currentScenePresents}/{totalScenePresents}";
            presentCounterText.text = updatedText;

            // 🎯 Color logic: White for all, GREEN only when ALL presents are collected
            if (currentScenePresents >= totalScenePresents && totalScenePresents > 0)
            {
                presentCounterText.color = Color.green; // Green when complete
                Debug.Log($"[PlayerCollectiblesUI] All presents collected! Setting green color");
            }
            else
            {
                presentCounterText.color = Color.white; // White for all other cases
                Debug.Log($"[PlayerCollectiblesUI] Presents in progress, setting white color");
            }

            Debug.Log($"[PlayerCollectiblesUI] Present counter updated: '{updatedText}' with color {presentCounterText.color}");
        }
        else
        {
            Debug.Log($"[PlayerCollectiblesUI] Cannot update present display - presentCounterText: {presentCounterText != null}, totalScenePresents: {totalScenePresents}");
        }
    }

    private string UpdateNumbersInTemplate(string template, int current, int total)
    {
        // For memories, use the template-based logic
        if (string.IsNullOrEmpty(template))
        {
            return $"{current}/{total}";
        }

        // Metodo migliorato: cerca il pattern X/Y e sostituiscilo mantenendo il resto del testo
        if (Regex.IsMatch(template, @"\d+/\d+"))
        {
            string result = Regex.Replace(template, @"\d+/\d+", $"{current}/{total}");
            return result;
        }

        // Se non trova il pattern numerico, cerca solo il simbolo "/" e ricostruisce
        if (template.Contains("/"))
        {
            // Trova la posizione del "/" e ricostruisce mantenendo il resto del testo
            int slashIndex = template.IndexOf('/');

            // Trova i numeri prima e dopo lo slash
            string beforeSlash = "";
            string afterSlash = "";

            // Cerca il numero prima dello slash
            for (int i = slashIndex - 1; i >= 0; i--)
            {
                if (char.IsDigit(template[i]))
                {
                    beforeSlash = template[i] + beforeSlash;
                }
                else
                {
                    break;
                }
            }

            // Cerca il numero dopo lo slash
            for (int i = slashIndex + 1; i < template.Length; i++)
            {
                if (char.IsDigit(template[i]))
                {
                    afterSlash += template[i];
                }
                else
                {
                    break;
                }
            }

            if (!string.IsNullOrEmpty(beforeSlash) && !string.IsNullOrEmpty(afterSlash))
            {
                string oldPattern = beforeSlash + "/" + afterSlash;
                string newPattern = current + "/" + total;
                string result = template.Replace(oldPattern, newPattern);
                return result;
            }
        }

        // Fallback finale: se tutto fallisce, aggiungi alla fine
        string fallbackResult = template.TrimEnd() + $" {current}/{total}";
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

        Debug.Log($"[PlayerCollectiblesUI] {panelType} panel mostrato nella posizione originale: {originalState.anchoredPosition}");

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

        Debug.Log($"[PlayerCollectiblesUI] Nascondo {panelType} panel: {panel.name}");

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

        Debug.Log($"[PlayerCollectiblesUI] {panelType} panel nascosto e ripristinato alla posizione originale: {originalState.anchoredPosition}");
    }

    private void UpdateAllCounterDisplays()
    {
        // Sempre aggiorna le memories
        UpdateMemoryCounterDisplay();

        // Aggiorna i presents solo se ne abbiamo di disponibili
        if (totalScenePresents > 0 || currentScenePresents > 0)
        {
            UpdatePresentCounterDisplay();
        }
        else
        {
            // Nascondi il counter dei presents se non supportati
            if (presentPanel != null)
            {
                presentPanel.SetActive(false);
            }
            else if (presentCounterText != null)
            {
                presentCounterText.gameObject.SetActive(false);
            }
        }
    }

    private void OnAllSceneMemoriesCompleted()
    {


        if (memoryCounterText != null)
        {
            memoryCounterText.color = new Color(1f, 0.84f, 0f, 1f); // Colore oro
            StartCoroutine(CelebrationEffect(memoryCounterText.transform, memoryIcon?.transform));
        }
    }

    private void OnAllScenePresentsCompleted()
    {


        if (presentCounterText != null)
        {
            presentCounterText.color = new Color(1f, 0.84f, 0f, 1f); // Colore oro
            StartCoroutine(CelebrationEffect(presentCounterText.transform, presentIcon?.transform));
        }
    }

    private void OnAllSceneCollectiblesCompleted()
    {


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



        Debug.Log("[PlayerCollectiblesUI] Reinizializzato per nuova scena con nuove posizioni originali");
    }


    public void UpdateCountersManually(int currentMemories, int totalMemories, int currentPresents, int totalPresents)
    {
        Debug.Log($"[PlayerCollectiblesUI] Aggiornamento manuale contatori: M={currentMemories}/{totalMemories}, P={currentPresents}/{totalPresents}");

        currentSceneMemories = currentMemories;
        totalSceneMemories = totalMemories;
        currentScenePresents = currentPresents;
        totalScenePresents = totalPresents;

        UpdateAllCounterDisplays();
    }

    public bool IsConnectedToTrackingSystem()
    {
        return isConnectedToCollectiblesManager && currentCollectiblesManager != null;
    }
    public string GetConnectedSystemInfo()
    {
        if (!isConnectedToCollectiblesManager || currentCollectiblesManager == null)
        {
            return "Nessun sistema collectibles connesso";
        }

        string managerName = currentCollectiblesManagerType?.Name ?? "Unknown";
        string instanceName = currentCollectiblesManager.name;

        return $"Connesso a: {managerName} (GameObject: {instanceName})";
    }
    /// <summary>
    /// Forza la riconnessione del sistema collectibles (utile per debug)
    /// </summary>
    public void DebugForceReconnect()
    {
        Debug.Log("[PlayerCollectiblesUI] Forzando riconnessione...");

        // Disconnetti dal manager corrente
        DisconnectFromCurrentManager();

        // Reset valori
        currentSceneMemories = 0;
        totalSceneMemories = 0;
        currentScenePresents = 0;
        totalScenePresents = 0;

        // Ferma tutte le animazioni
        StopAllUIAnimations();

        // Prova a riconnettersi automaticamente
        AutoConnectToAvailableManagers();

        // Aggiorna i display
        UpdateAllCounterDisplays();

        Debug.Log($"[PlayerCollectiblesUI] Riconnessione completata. Connesso: {IsConnectedToTrackingSystem()}");

        if (IsConnectedToTrackingSystem())
        {
            Debug.Log($"[PlayerCollectiblesUI] Nuovo sistema: {GetConnectedSystemInfo()}");
        }
        else
        {
            Debug.LogWarning("[PlayerCollectiblesUI] Nessun sistema collectibles trovato dopo la riconnessione");
        }
    }


    // ========== GETTERS PUBBLICI ==========

    public int GetCurrentSceneMemories() => currentSceneMemories;
    public int GetTotalSceneMemories() => totalSceneMemories;
    public int GetCurrentScenePresents() => currentScenePresents;
    public int GetTotalScenePresents() => totalScenePresents;
    public int GetCurrentSceneCollectibles() => currentSceneMemories + currentScenePresents;
    public int GetTotalSceneCollectibles() => totalSceneMemories + totalScenePresents;

    private void OnDestroy()
    {


        if (Instance == this)
        {
            Instance = null;
        }
    }
}