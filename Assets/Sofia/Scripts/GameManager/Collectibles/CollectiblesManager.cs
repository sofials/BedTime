using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Collections;

/// <summary>
/// Manager universale per la gestione dei collectibles in qualsiasi scena
/// Compatibile con qualunque SceneManager e sistema UI
/// </summary>
public class CollectiblesManager : MonoBehaviour
{
    [Header("Scene Configuration")]
    [SerializeField] private string sceneName = "";
    [SerializeField] private bool autoDetectSceneName = true;
    
    [Header("Collectibles Settings")]
    [SerializeField] private bool enableLocalSaving = true;
    [SerializeField] private bool autoSaveOnCollection = true;
    [SerializeField] private bool enableDebugLogs = true;
    [SerializeField] private bool autoCountOnStart = true;
    [Header("🆕 First Memory Dialogue System")]
[Tooltip("Se true, attiva automaticamente un dialogo alla prima memory raccolta")]
[SerializeField] private bool enableFirstMemoryDialogue = false;
[Tooltip("DialogueSystem da attivare alla prima memory raccolta")]
[SerializeField] private DialogueSystem firstMemoryDialogueSystem;
[Tooltip("Messaggio da loggare quando viene attivato il dialogo per la prima memory")]
[SerializeField] private string firstMemoryDialogueMessage = "Prima memory raccolta! Attivando dialogo...";
[Tooltip("Ritardo prima di attivare il dialogo (in secondi)")]
[SerializeField] private float firstMemoryDialogueDelay = 0.5f;
[Tooltip("Se true, blocca la raccolta di altre memories fino a quando il dialogo non è finito")]
[SerializeField] private bool blockMemoryCollectionDuringDialogue = true;
    
    [Header("Present Events")]
    public UnityEvent<int, int> OnPresentCountChanged; // collected, total
    public UnityEvent<string> OnPresentCollected; // present name
    public UnityEvent OnAllPresentsCollected;
    
    [Header("Memory Events")]
    public UnityEvent<int, int> OnMemoryCountChanged; // collected, total
    public UnityEvent<string> OnMemoryCollected; // memory name
    public UnityEvent OnAllMemoriesCollected;
    // Nuovo evento per il dialogo della prima memory
[Header("🆕 First Memory Events")]
public UnityEvent OnFirstMemoryDialogueTriggered; // Quando viene attivato il dialogo per la prima memory
public UnityEvent OnFirstMemoryDialogueCompleted; // Quando finisce il dialogo per la prima memory
    
    [Header("Combined Events")]
    public UnityEvent<int, int> OnAllCollectiblesCountChanged; // total collected, total available
    public UnityEvent OnAllCollectiblesCompleted;
    public UnityEvent OnCollectibleSystemUpdated; // Generic update event
    
    // Contatori
    [Header("Current Progress (Read Only)")]
    [SerializeField] private int totalPresents = 0;
    [SerializeField] private int collectedPresents = 0;
    [SerializeField] private int totalMemories = 0;
    [SerializeField] private int collectedMemories = 0;
    
    // Tracking dei nomi raccolti
    private List<string> collectedPresentNames = new List<string>();
    private List<string> collectedMemoryNames = new List<string>();
    
    // Tracking di tutti i collectibles nella scena
    private Dictionary<string, CollectibleData> allPresents = new Dictionary<string, CollectibleData>();
    private Dictionary<string, CollectibleData> allMemories = new Dictionary<string, CollectibleData>();
    
    // Singleton pattern (opzionale)
    public static CollectiblesManager Instance { get; private set; }
    
    // Riferimenti UI
    private PlayerCollectiblesUI collectiblesUI = null;
    private PlayerUI playerUI = null;
    // Variabili private per il tracking
private bool firstMemoryDialogueTriggered = false;
    private bool isFirstMemoryDialogueActive = false;
public static event Action<CollectiblesManager> OnAnyFirstMemoryDialogueTriggered;
public static event Action<CollectiblesManager> OnAnyFirstMemoryDialogueCompleted;

    
    [System.Serializable]
    public class CollectibleData
    {
        public string name;
        public CollectibleType type;
        public bool isCollected;
        public Vector3 position;
        public GameObject gameObject;
        
        public CollectibleData(string objName, CollectibleType objType, Vector3 pos, GameObject obj)
        {
            name = objName;
            type = objType;
            isCollected = false;
            position = pos;
            gameObject = obj;
        }
    }
    
    private void Awake()
    {
        // Setup singleton (permette multiple istanze se necessario)
        if (Instance == null)
        {
            Instance = this;
        }
        
        // Auto-detect scene name se abilitato
        if (autoDetectSceneName && string.IsNullOrEmpty(sceneName))
        {
            sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        }
        
        DebugLog($"[CollectiblesManager] Inizializzato per scena: '{sceneName}'");
    }

    private void Start()
    {
        // Carica i dati salvati
        LoadCollectiblesData();
        // Conta automaticamente i collectibles se abilitato
        if (autoCountOnStart && (totalPresents == 0 && totalMemories == 0))
        {
            CountAllCollectibles();
        }



        // Connetti ai sistemi UI
        ConnectToUISystems();

        // Aggiorna l'UI iniziale
        UpdateAllUI();
        ValidateFirstMemoryDialogueSetup();
    }
    // 🆕 NUOVO METODO DI VALIDAZIONE
/// <summary>
/// 🆕 Valida il setup del dialogo prima memory
/// </summary>
void ValidateFirstMemoryDialogueSetup()
{
    if (!enableFirstMemoryDialogue)
    {
        DebugLog("[CollectiblesManager] First Memory Dialogue disabilitato");
        return;
    }
    
    if (firstMemoryDialogueSystem == null)
    {
        DebugLog("[CollectiblesManager] ⚠️ First Memory Dialogue abilitato ma nessun DialogueSystem assegnato!");
    }
    else
    {
        DebugLog($"[CollectiblesManager] ✅ First Memory Dialogue setup: '{firstMemoryDialogueSystem.name}'");
        DebugLog($"[CollectiblesManager] 📋 Impostazioni: Delay={firstMemoryDialogueDelay}s, BlockCollection={blockMemoryCollectionDuringDialogue}");
    }
}
    // ========== CONTEGGIO COLLECTIBLES ==========
    
    /// <summary>
    /// Conta tutti i collectibles nella scena usando tag e classi
    /// </summary>
    public void CountAllCollectibles()
{
    allPresents.Clear();
    allMemories.Clear();
    totalPresents = 0;
    totalMemories = 0;
    
    DebugLog("[CollectiblesManager] Inizio conteggio collectibles...");
    
    // Prova prima con i tag
    CountCollectiblesByTags();
    
    // Solo se non trova nulla, usa le classi
    if (totalPresents == 0 && totalMemories == 0)
    {
        DebugLog("[CollectiblesManager] Nessun collectible trovato tramite tag, provo con le classi...");
        CountCollectiblesByClass();
    }
    else
    {
        DebugLog($"[CollectiblesManager] Trovati tramite tag: {totalPresents} presents, {totalMemories} memories");
    }
    
    DebugLog($"[CollectiblesManager] Conteggio finale: {totalPresents} presents, {totalMemories} memories");
}
    private void CountCollectiblesByTags()
    {
        // Conta i Present usando il tag
        GameObject[] presentObjects = GameObject.FindGameObjectsWithTag("Present");
        totalPresents = 0;
        
        foreach (GameObject obj in presentObjects)
        {
            if (obj != null)
            {
                CollectibleData data = new CollectibleData(obj.name, CollectibleType.Present, obj.transform.position, obj);
                allPresents[obj.name] = data;
                totalPresents++;
            }
        }
        
        // Conta le Memories usando il tag
        GameObject[] memoryObjects = GameObject.FindGameObjectsWithTag("Memories");
        totalMemories = 0;
        
        foreach (GameObject obj in memoryObjects)
        {
            if (obj != null)
            {
                CollectibleData data = new CollectibleData(obj.name, CollectibleType.Memory, obj.transform.position, obj);
                allMemories[obj.name] = data;
                totalMemories++;
            }
        }
        
        DebugLog($"[CollectiblesManager] Conteggio tramite tag: {totalPresents} presents, {totalMemories} memories");
    }
    
    private void CountCollectiblesByClass()
    {
        // Fallback: conta usando la classe Collectibles
        Collectibles[] allCollectibles = UnityEngine.Object.FindObjectsByType<Collectibles>(FindObjectsSortMode.None);
        
        totalPresents = 0;
        totalMemories = 0;
        
        foreach (Collectibles collectible in allCollectibles)
        {
            if (collectible != null && !collectible.IsCollected())
            {
                CollectibleData data = new CollectibleData(
                    collectible.GetName(), 
                    collectible.GetCollectibleType(), 
                    collectible.transform.position, 
                    collectible.gameObject
                );
                
                if (collectible.GetCollectibleType() == CollectibleType.Present)
                {
                    allPresents[collectible.GetName()] = data;
                    totalPresents++;
                }
                else if (collectible.GetCollectibleType() == CollectibleType.Memory)
                {
                    allMemories[collectible.GetName()] = data;
                    totalMemories++;
                }
            }
        }
        
        DebugLog($"[CollectiblesManager] Conteggio tramite classe: {totalPresents} presents, {totalMemories} memories");
    }

    // ========== GESTIONE RACCOLTA ==========

    /// <summary>
    /// Notifica che un present è stato raccolto
    /// </summary>
    public void NotifyPresentCollected(string presentName = "")
    {
        if (string.IsNullOrEmpty(presentName))
        {
            presentName = $"Present_{collectedPresents + 1}";
        }

        // Evita duplicati
        if (collectedPresentNames.Contains(presentName))
        {
            DebugLog($"[CollectiblesManager] Present '{presentName}' già raccolto, ignorato");
            return;
        }

        // Aggiungi alla lista
        collectedPresentNames.Add(presentName);
        collectedPresents++;

        // Marca come raccolto nei dati se presente
        if (allPresents.ContainsKey(presentName))
        {
            allPresents[presentName].isCollected = true;
        }

        DebugLog($"[CollectiblesManager] ✅ Present '{presentName}' raccolto! Progresso: {collectedPresents}/{totalPresents}");

        // Salva automaticamente se abilitato
        if (autoSaveOnCollection)
        {
            SaveCollectiblesData();
        }

        // 🔥 NUOVO: Aggiorna UI PRIMA degli eventi
        UpdateCollectiblesUI();

        // Notifica eventi
        OnPresentCollected?.Invoke(presentName);
        OnPresentCountChanged?.Invoke(collectedPresents, totalPresents);

        // Controlla completamento
        if (collectedPresents >= totalPresents && totalPresents > 0)
        {
            DebugLog("[CollectiblesManager] 🎉 Tutti i presents completati!");
            OnAllPresentsCollected?.Invoke();
            CheckAllCollectiblesCompletion();
        }

        // 🔥 NUOVO: Aggiorna UI DOPO gli eventi per sicurezza
        UpdateAllUI();
    }
public void AutoDiscoverUISystems()
{
    DebugLog("[CollectiblesManager] Ricerca automatica sistemi UI...");
    
    // Cerca PlayerCollectiblesUI
    PlayerCollectiblesUI[] allCollectiblesUI = UnityEngine.Object.FindObjectsByType<PlayerCollectiblesUI>(FindObjectsSortMode.None);
    if (allCollectiblesUI.Length > 0)
    {
        collectiblesUI = allCollectiblesUI[0];
        if (allCollectiblesUI.Length > 1)
        {
            DebugLog($"[CollectiblesManager] ⚠️ Trovati {allCollectiblesUI.Length} PlayerCollectiblesUI, usando il primo");
        }
        DebugLog($"[CollectiblesManager] ✅ PlayerCollectiblesUI trovato: {collectiblesUI.name}");
    }
    
    // Cerca PlayerUI
    PlayerUI[] allPlayerUI = UnityEngine.Object.FindObjectsByType<PlayerUI>(FindObjectsSortMode.None);
    if (allPlayerUI.Length > 0)
    {
        playerUI = allPlayerUI[0];
        DebugLog($"[CollectiblesManager] ✅ PlayerUI trovato: {playerUI.name}");
    }
}

    /// <summary>
    /// Notifica che una memory è stata raccolta
    /// </summary>
    public void NotifyMemoryCollected(string memoryName = "")
    {
        if (string.IsNullOrEmpty(memoryName))
        {
            memoryName = $"Memory_{collectedMemories + 1}";
        }

        // Evita duplicati
        if (collectedMemoryNames.Contains(memoryName))
        {
            DebugLog($"[CollectiblesManager] Memory '{memoryName}' già raccolta, ignorata");
            return;
        }

        // 🆕 CONTROLLO BLOCCO DURANTE DIALOGO PRIMA MEMORY
        if (blockMemoryCollectionDuringDialogue && isFirstMemoryDialogueActive)
        {
            DebugLog($"[CollectiblesManager] ⏸️ Raccolta memory '{memoryName}' bloccata - dialogo prima memory attivo");
            return;
        }

        // 🆕 CONTROLLO PRIMA MEMORY
        bool isFirstMemory = (collectedMemories == 0 && enableFirstMemoryDialogue && !firstMemoryDialogueTriggered);

        // Aggiungi alla lista
        collectedMemoryNames.Add(memoryName);
        collectedMemories++;

        // Marca come raccolta nei dati se presente
        if (allMemories.ContainsKey(memoryName))
        {
            allMemories[memoryName].isCollected = true;
        }

        DebugLog($"[CollectiblesManager] ✅ Memory '{memoryName}' raccolta! Progresso: {collectedMemories}/{totalMemories}" +
                 (isFirstMemory ? " 🌟 PRIMA MEMORY!" : ""));

        // Salva automaticamente se abilitato
        if (autoSaveOnCollection)
        {
            SaveCollectiblesData();
        }

        // Aggiorna UI PRIMA degli eventi
        UpdateCollectiblesUI();

        // Notifica eventi standard
        OnMemoryCollected?.Invoke(memoryName);
        OnMemoryCountChanged?.Invoke(collectedMemories, totalMemories);

        // 🆕 ATTIVA DIALOGO PRIMA MEMORY SE È IL CASO
        if (isFirstMemory)
        {
            if (firstMemoryDialogueDelay > 0f)
            {
                StartCoroutine(TriggerFirstMemoryDialogueWithDelay(memoryName));
            }
            else
            {
                TriggerFirstMemoryDialogue(memoryName);
            }
        }

        // Controlla completamento
        if (collectedMemories >= totalMemories && totalMemories > 0)
        {
            DebugLog("[CollectiblesManager] 🎉 Tutte le memories completate!");
            OnAllMemoriesCollected?.Invoke();
            CheckAllCollectiblesCompletion();
        }

        // Aggiorna UI DOPO gli eventi per sicurezza
        UpdateAllUI();
    }
// 🆕 NUOVI METODI PER IL SISTEMA FIRST MEMORY DIALOGUE

/// <summary>
/// 🆕 Attiva il dialogo per la prima memory con ritardo
/// </summary>
IEnumerator TriggerFirstMemoryDialogueWithDelay(string memoryName)
{
    DebugLog($"[CollectiblesManager] ⏳ Ritardo dialogo prima memory: {firstMemoryDialogueDelay}s...");
    yield return new WaitForSeconds(firstMemoryDialogueDelay);
    TriggerFirstMemoryDialogue(memoryName);
}

/// <summary>
/// 🆕 Attiva il dialogo per la prima memory
/// </summary>
void TriggerFirstMemoryDialogue(string memoryName)
{
    if (firstMemoryDialogueTriggered)
    {
        DebugLog("[CollectiblesManager] Dialogo prima memory già triggerato, skip.");
        return;
    }

    if (firstMemoryDialogueSystem == null)
    {
        DebugLog("[CollectiblesManager] ⚠️ First Memory Dialogue abilitato ma nessun DialogueSystem assegnato!");
        return;
    }

    DebugLog($"[CollectiblesManager] 🌟 ATTIVAZIONE DIALOGO PRIMA MEMORY per '{memoryName}'!");
    
    firstMemoryDialogueTriggered = true;
    isFirstMemoryDialogueActive = true;
    
    // Log messaggio personalizzato
    if (!string.IsNullOrEmpty(firstMemoryDialogueMessage))
    {
        DebugLog($"[CollectiblesManager] 📢 {firstMemoryDialogueMessage}");
    }
    
    // Registra listener per la fine del dialogo
    RegisterFirstMemoryDialogueListeners();
    
    try
    {
        // Attiva il dialogo tramite il metodo TriggerDialogue (senza trigger)
        firstMemoryDialogueSystem.TriggerDialogueFromCollectiblesManager(memoryName);
        DebugLog($"[CollectiblesManager] ✅ Dialogo prima memory '{firstMemoryDialogueSystem.name}' attivato con successo!");
    }
    catch (System.Exception e)
    {
        DebugLog($"[CollectiblesManager] ❌ Errore nell'attivare dialogo prima memory: {e.Message}");
        isFirstMemoryDialogueActive = false; // Reset stato in caso di errore
    }
    
    // Invoca eventi
    OnFirstMemoryDialogueTriggered?.Invoke();
    OnAnyFirstMemoryDialogueTriggered?.Invoke(this);
}

/// <summary>
/// 🆕 Registra i listener per il dialogo della prima memory
/// </summary>
void RegisterFirstMemoryDialogueListeners()
{
    if (firstMemoryDialogueSystem != null)
    {
        // Ascolta quando il dialogo finisce
        firstMemoryDialogueSystem.OnDialogueEnded.AddListener(OnFirstMemoryDialogueEnded);
        
        // Ascolta anche l'evento statico per maggiore sicurezza
        DialogueSystem.OnAnyDialogueEnded += OnAnyDialogueEndedHandler;
        
        DebugLog("[CollectiblesManager] 👂 Listener dialogo prima memory registrati");
    }
}

/// <summary>
/// 🆕 Rimuove i listener per il dialogo della prima memory
/// </summary>
void UnregisterFirstMemoryDialogueListeners()
{
    if (firstMemoryDialogueSystem != null)
    {
        firstMemoryDialogueSystem.OnDialogueEnded.RemoveListener(OnFirstMemoryDialogueEnded);
        DialogueSystem.OnAnyDialogueEnded -= OnAnyDialogueEndedHandler;
        
        DebugLog("[CollectiblesManager] 🔇 Listener dialogo prima memory rimossi");
    }
}

/// <summary>
/// 🆕 Chiamato quando finisce il dialogo della prima memory
/// </summary>
void OnFirstMemoryDialogueEnded()
{
    DebugLog("[CollectiblesManager] 🏁 Dialogo prima memory terminato!");
    
    isFirstMemoryDialogueActive = false;
    
    // Rimuovi listener per evitare chiamate multiple
    UnregisterFirstMemoryDialogueListeners();
    
    // Invoca eventi di completamento
    OnFirstMemoryDialogueCompleted?.Invoke();
    OnAnyFirstMemoryDialogueCompleted?.Invoke(this);
    
    DebugLog("[CollectiblesManager] ✅ Raccolta memories sbloccata");
}

/// <summary>
/// 🆕 Handler per l'evento statico di fine dialogo
/// </summary>
void OnAnyDialogueEndedHandler(DialogueSystem dialogueSystem)
{
    // Controlla se è il nostro dialogo della prima memory
    if (dialogueSystem == firstMemoryDialogueSystem && isFirstMemoryDialogueActive)
    {
        OnFirstMemoryDialogueEnded();
    }
}

// 🆕 METODI PUBBLICI PER IL CONTROLLO DEL SISTEMA

/// <summary>
/// 🆕 Abilita/disabilita il sistema dialogo prima memory
/// </summary>
public void SetFirstMemoryDialogueEnabled(bool enabled)
{
    enableFirstMemoryDialogue = enabled;
    DebugLog($"[CollectiblesManager] First Memory Dialogue {(enabled ? "abilitato" : "disabilitato")}");
}

/// <summary>
/// 🆕 Imposta il DialogueSystem per la prima memory
/// </summary>
public void SetFirstMemoryDialogueSystem(DialogueSystem dialogueSystem)
{
    firstMemoryDialogueSystem = dialogueSystem;
    DebugLog($"[CollectiblesManager] First Memory DialogueSystem impostato: {(dialogueSystem != null ? dialogueSystem.name : "NULL")}");
}

/// <summary>
/// 🆕 Imposta il ritardo del dialogo prima memory
/// </summary>
public void SetFirstMemoryDialogueDelay(float delay)
{
    firstMemoryDialogueDelay = delay;
    DebugLog($"[CollectiblesManager] Ritardo dialogo prima memory impostato: {delay}s");
}

/// <summary>
/// 🆕 Imposta il messaggio del dialogo prima memory
/// </summary>
public void SetFirstMemoryDialogueMessage(string message)
{
    firstMemoryDialogueMessage = message;
    DebugLog($"[CollectiblesManager] Messaggio dialogo prima memory impostato: {message}");
}

/// <summary>
/// 🆕 Abilita/disabilita il blocco raccolta durante dialogo
/// </summary>
public void SetBlockMemoryCollectionDuringDialogue(bool block)
{
    blockMemoryCollectionDuringDialogue = block;
    DebugLog($"[CollectiblesManager] Blocco raccolta durante dialogo: {(block ? "abilitato" : "disabilitato")}");
}

/// <summary>
/// 🆕 Forza l'attivazione del dialogo prima memory (per test)
/// </summary>
public void ForceFirstMemoryDialogue()
{
    if (enableFirstMemoryDialogue && firstMemoryDialogueSystem != null)
    {
        firstMemoryDialogueTriggered = false; // Reset flag
        TriggerFirstMemoryDialogue("TestMemory_Forced");
    }
    else
    {
        DebugLog("[CollectiblesManager] ⚠️ Impossibile forzare dialogo prima memory - sistema disabilitato o DialogueSystem mancante");
    }
}

/// <summary>
/// 🆕 Reset del sistema dialogo prima memory
/// </summary>
public void ResetFirstMemoryDialogue()
{
    firstMemoryDialogueTriggered = false;
    isFirstMemoryDialogueActive = false;
    UnregisterFirstMemoryDialogueListeners();
    DebugLog("[CollectiblesManager] 🔄 Sistema dialogo prima memory resettato");
}

// 🆕 GETTERS PER IL SISTEMA FIRST MEMORY DIALOGUE

public bool IsFirstMemoryDialogueEnabled() => enableFirstMemoryDialogue;
public DialogueSystem GetFirstMemoryDialogueSystem() => firstMemoryDialogueSystem;
public float GetFirstMemoryDialogueDelay() => firstMemoryDialogueDelay;
public string GetFirstMemoryDialogueMessage() => firstMemoryDialogueMessage;
public bool GetBlockMemoryCollectionDuringDialogue() => blockMemoryCollectionDuringDialogue;
public bool WasFirstMemoryDialogueTriggered() => firstMemoryDialogueTriggered;
public bool IsFirstMemoryDialogueActive() => isFirstMemoryDialogueActive;

    
    /// <summary>
    /// Metodo generico per raccogliere collectibles
    /// </summary>
    public void OnCollectibleCollected(string collectibleName, CollectibleType collectibleType)
    {
        DebugLog($"[CollectiblesManager] Collectible raccolto: '{collectibleName}' ({collectibleType})");
        
        switch (collectibleType)
        {
            case CollectibleType.Present:
                NotifyPresentCollected(collectibleName);
                break;
                
            case CollectibleType.Memory:
                NotifyMemoryCollected(collectibleName);
                break;
                
            default:
                DebugLog($"[CollectiblesManager] ⚠️ Tipo collectible non riconosciuto: {collectibleType}");
                break;
        }
    }
    
    private void CheckAllCollectiblesCompletion()
    {
        bool presentsComplete = totalPresents == 0 || collectedPresents >= totalPresents;
        bool memoriesComplete = totalMemories == 0 || collectedMemories >= totalMemories;
        
        if (presentsComplete && memoriesComplete && (totalPresents > 0 || totalMemories > 0))
        {
            DebugLog("[CollectiblesManager] 🏆 TUTTI i collectibles completati!");
            OnAllCollectiblesCompleted?.Invoke();
        }
    }

    /// <summary>
    /// Registra manualmente un collectible - VERSIONE CORRETTA
    /// </summary>
    public void RegisterCollectible(string name, CollectibleType type, Vector3 position, GameObject gameObject = null)
    {
        if (string.IsNullOrEmpty(name))
        {
            DebugLog("[CollectiblesManager] ⚠️ Nome collectible vuoto, ignorato");
            return;
        }

        CollectibleData data = new CollectibleData(name, type, position, gameObject);

        switch (type)
        {
            case CollectibleType.Present:
                if (!allPresents.ContainsKey(name))
                {
                    allPresents[name] = data;
                    totalPresents++;
                    DebugLog($"[CollectiblesManager] ✅ Nuovo Present registrato: '{name}' (totale: {totalPresents})");
                }
                else
                {
                    allPresents[name] = data; // Aggiorna solo i dati, non il contatore
                    DebugLog($"[CollectiblesManager] 🔄 Present aggiornato: '{name}' (totale rimane: {totalPresents})");
                }
                break;

            case CollectibleType.Memory:
                // 🔥 FIX: Usa la stessa logica dei Presents!
                if (!allMemories.ContainsKey(name))
                {
                    allMemories[name] = data;
                    totalMemories++;
                    DebugLog($"[CollectiblesManager] ✅ Nuova Memory registrata: '{name}' (totale: {totalMemories})");
                }
                else
                {
                    allMemories[name] = data; // Aggiorna solo i dati, non il contatore
                    DebugLog($"[CollectiblesManager] 🔄 Memory aggiornata: '{name}' (totale rimane: {totalMemories})");
                }
                break;
        }

        DebugLog($"[CollectiblesManager] ✅ Collectible gestito: '{name}' ({type}) - P:{totalPresents}, M:{totalMemories}");
        UpdateAllUI();
    }
// AGGIUNGI QUESTO METODO AL COLLECTIBLESMANAGER dopo il RegisterCollectible esistente

/// <summary>
/// Registra un collectible usando la classe Collectibles (OVERLOAD)
/// </summary>
public void RegisterCollectible(Collectibles collectible)
{
    if (collectible == null) 
    {
        DebugLog("[CollectiblesManager] ⚠️ Collectible null, ignorato");
        return;
    }
    
    // Chiama il metodo principale con i parametri estratti dall'oggetto Collectibles
    RegisterCollectible(
        collectible.GetName(), 
        collectible.GetCollectibleType(), 
        collectible.transform.position, 
        collectible.gameObject
    );
    
    DebugLog($"[CollectiblesManager] ✅ Collectible registrato tramite overload: {collectible.GetName()}");
}
 
    /// <summary>
    /// Rimuove un collectible registrato
    /// </summary>
    public bool UnregisterCollectible(string name, CollectibleType type)
    {
        bool removed = false;
        
        switch (type)
        {
            case CollectibleType.Present:
                if (allPresents.ContainsKey(name))
                {
                    allPresents.Remove(name);
                    if (!collectedPresentNames.Contains(name))
                    {
                        totalPresents--;
                    }
                    removed = true;
                }
                break;
                
            case CollectibleType.Memory:
                if (allMemories.ContainsKey(name))
                {
                    allMemories.Remove(name);
                    if (!collectedMemoryNames.Contains(name))
                    {
                        totalMemories--;
                    }
                    removed = true;
                }
                break;
        }
        
        if (removed)
        {
            DebugLog($"[CollectiblesManager] ❌ Collectible rimosso: '{name}' ({type})");
            UpdateAllUI();
        }
        
        return removed;
    }
    
    // ========== CONNESSIONE UI ==========
    
    private void ConnectToUISystems()
    {
        StartCoroutine(ConnectToUISystemsCoroutine());
    }
    
   public void ForceReconnectUI()
{
    DebugLog("[CollectiblesManager] Forzando riconnessione UI...");
    StartCoroutine(ConnectToUISystemsCoroutine());
}
private System.Collections.IEnumerator ConnectToUISystemsCoroutine()
{
    yield return null; // Aspetta un frame
    
    // 🔥 NUOVO: Connetti PRIMA al PlayerCollectiblesUI usando il metodo dedicato
    collectiblesUI = PlayerCollectiblesUI.Instance;
        if (collectiblesUI == null)
        {
        collectiblesUI = UnityEngine.Object.FindFirstObjectByType<PlayerCollectiblesUI>();
    }
    
    if (collectiblesUI != null)
    {
        // 🎯 USA IL METODO DEDICATO del PlayerCollectiblesUI
        bool connected = collectiblesUI.ConnectToCollectiblesManager(this);
        if (connected)
        {
            DebugLog("[CollectiblesManager] ✅ PlayerCollectiblesUI connesso tramite metodo dedicato");
            UpdateCollectiblesUI();
        }
        else
        {
            DebugLog("[CollectiblesManager] ❌ Connessione PlayerCollectiblesUI fallita - provo metodo legacy");
            
            // Fallback: prova il metodo legacy se esiste
            var legacyMethod = collectiblesUI.GetType().GetMethod("ConnectToCollectiblesManager", 
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            
            if (legacyMethod != null)
            {
                try
                {
                    bool legacyConnected = (bool)legacyMethod.Invoke(collectiblesUI, new object[] { this });
                    if (legacyConnected)
                    {
                        DebugLog("[CollectiblesManager] ✅ PlayerCollectiblesUI connesso tramite metodo legacy");
                        UpdateCollectiblesUI();
                    }
                }
                catch (System.Exception e)
                {
                    DebugLog($"[CollectiblesManager] ❌ Errore metodo legacy: {e.Message}");
                }
            }
        }
    }
    else
    {
        DebugLog("[CollectiblesManager] ⚠️ PlayerCollectiblesUI non trovato");
    }
    
    // Connetti al PlayerUI (opzionale) - CODICE ESISTENTE
    playerUI = PlayerUI.Instance;
    if (playerUI == null)
    {
       playerUI = UnityEngine.Object.FindFirstObjectByType<PlayerUI>();
    }
    
    if (playerUI != null)
    {
        DebugLog("[CollectiblesManager] ✅ PlayerUI trovato");
    }
}
    public bool IsUIConnected()
{
    return collectiblesUI != null;
}
    private void UpdateAllUI()
    {
        // Aggiorna eventi generici
        int totalCollected = collectedPresents + collectedMemories;
        int totalAvailable = totalPresents + totalMemories;
        OnAllCollectiblesCountChanged?.Invoke(totalCollected, totalAvailable);
        OnCollectibleSystemUpdated?.Invoke();
        
        // Aggiorna UI specifiche
        UpdateCollectiblesUI();
    }
    
  private void UpdateCollectiblesUI()
{
    if (collectiblesUI != null)
    {
        // Metodo 1: Prova il metodo UpdateCountersManually (più diretto)
        var updateMethod = collectiblesUI.GetType().GetMethod("UpdateCountersManually", 
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        
        if (updateMethod != null)
        {
            try
            {
                updateMethod.Invoke(collectiblesUI, new object[] { 
                    collectedMemories, totalMemories, collectedPresents, totalPresents 
                });
                DebugLog($"[CollectiblesManager] UI aggiornata: M={collectedMemories}/{totalMemories}, P={collectedPresents}/{totalPresents}");
                return;
            }
            catch (System.Exception e)
            {
                DebugLog($"[CollectiblesManager] Errore aggiornamento UI diretto: {e.Message}");
            }
        }
        
        // Metodo 2: Fallback - forza gli eventi se l'aggiornamento diretto fallisce
        try
        {
            OnMemoryCountChanged?.Invoke(collectedMemories, totalMemories);
            OnPresentCountChanged?.Invoke(collectedPresents, totalPresents);
            DebugLog("[CollectiblesManager] UI aggiornata tramite eventi");
        }
        catch (System.Exception e)
        {
            DebugLog($"[CollectiblesManager] Errore aggiornamento UI tramite eventi: {e.Message}");
        }
    }
    else
    {
        DebugLog("[CollectiblesManager] ⚠️ Impossibile aggiornare UI: collectiblesUI è null");
    }
}
    
    // ========== SALVATAGGIO E CARICAMENTO ==========
    
    /// <summary>
    /// Salva tutti i dati dei collectibles localmente
    /// </summary>
    public void SaveCollectiblesData()
    {
        if (!enableLocalSaving) return;
        
        string sceneKey = sceneName.Replace(" ", "_").Replace("-", "_");
        
        // Salva contatori
        PlayerPrefs.SetInt($"CollectiblesLocal_{sceneKey}_CollectedPresents", collectedPresents);
        PlayerPrefs.SetInt($"CollectiblesLocal_{sceneKey}_TotalPresents", totalPresents);
        PlayerPrefs.SetInt($"CollectiblesLocal_{sceneKey}_CollectedMemories", collectedMemories);
        PlayerPrefs.SetInt($"CollectiblesLocal_{sceneKey}_TotalMemories", totalMemories);
        
        // Salva nomi presents raccolti
        PlayerPrefs.SetInt($"CollectiblesLocal_{sceneKey}_PresentNamesCount", collectedPresentNames.Count);
        for (int i = 0; i < collectedPresentNames.Count; i++)
        {
            PlayerPrefs.SetString($"CollectiblesLocal_{sceneKey}_Present_{i}", collectedPresentNames[i]);
        }
        
        // Salva nomi memories raccolte
        PlayerPrefs.SetInt($"CollectiblesLocal_{sceneKey}_MemoryNamesCount", collectedMemoryNames.Count);
        for (int i = 0; i < collectedMemoryNames.Count; i++)
        {
            PlayerPrefs.SetString($"CollectiblesLocal_{sceneKey}_Memory_{i}", collectedMemoryNames[i]);
        }
        
        PlayerPrefs.Save();
        DebugLog($"[CollectiblesManager] 💾 Dati salvati: P={collectedPresents}/{totalPresents}, M={collectedMemories}/{totalMemories}");
    }
    
    /// <summary>
    /// Carica tutti i dati dei collectibles localmente
    /// </summary>
    public void LoadCollectiblesData()
    {
        if (!enableLocalSaving) return;
        
        string sceneKey = sceneName.Replace(" ", "_").Replace("-", "_");
        
        // Carica contatori
        collectedPresents = PlayerPrefs.GetInt($"CollectiblesLocal_{sceneKey}_CollectedPresents", 0);
        totalPresents = PlayerPrefs.GetInt($"CollectiblesLocal_{sceneKey}_TotalPresents", totalPresents);
        collectedMemories = PlayerPrefs.GetInt($"CollectiblesLocal_{sceneKey}_CollectedMemories", 0);
        totalMemories = PlayerPrefs.GetInt($"CollectiblesLocal_{sceneKey}_TotalMemories", totalMemories);
        
        // Carica nomi presents raccolti
        int presentNamesCount = PlayerPrefs.GetInt($"CollectiblesLocal_{sceneKey}_PresentNamesCount", 0);
        collectedPresentNames.Clear();
        for (int i = 0; i < presentNamesCount; i++)
        {
            string presentName = PlayerPrefs.GetString($"CollectiblesLocal_{sceneKey}_Present_{i}", "");
            if (!string.IsNullOrEmpty(presentName))
            {
                collectedPresentNames.Add(presentName);
            }
        }
        
        // Carica nomi memories raccolte
        int memoryNamesCount = PlayerPrefs.GetInt($"CollectiblesLocal_{sceneKey}_MemoryNamesCount", 0);
        collectedMemoryNames.Clear();
        for (int i = 0; i < memoryNamesCount; i++)
        {
            string memoryName = PlayerPrefs.GetString($"CollectiblesLocal_{sceneKey}_Memory_{i}", "");
            if (!string.IsNullOrEmpty(memoryName))
            {
                collectedMemoryNames.Add(memoryName);
            }
        }
        
        // Marca come raccolti nei dati registrati
        foreach (string presentName in collectedPresentNames)
        {
            if (allPresents.ContainsKey(presentName))
            {
                allPresents[presentName].isCollected = true;
            }
        }
        
        foreach (string memoryName in collectedMemoryNames)
        {
            if (allMemories.ContainsKey(memoryName))
            {
                allMemories[memoryName].isCollected = true;
            }
        }
        
        DebugLog($"[CollectiblesManager] 📁 Dati caricati: P={collectedPresents}/{totalPresents}, M={collectedMemories}/{totalMemories}");
    }
    
    /// <summary>
    /// Reset completo dei dati locali
    /// </summary>
    public void ResetCollectiblesData()
    {
        string sceneKey = sceneName.Replace(" ", "_").Replace("-", "_");
        
        // Cancella tutte le chiavi relative a questa scena
        PlayerPrefs.DeleteKey($"CollectiblesLocal_{sceneKey}_CollectedPresents");
        PlayerPrefs.DeleteKey($"CollectiblesLocal_{sceneKey}_TotalPresents");
        PlayerPrefs.DeleteKey($"CollectiblesLocal_{sceneKey}_CollectedMemories");
        PlayerPrefs.DeleteKey($"CollectiblesLocal_{sceneKey}_TotalMemories");
        
        // Cancella nomi presents
        int presentCount = PlayerPrefs.GetInt($"CollectiblesLocal_{sceneKey}_PresentNamesCount", 0);
        for (int i = 0; i < presentCount; i++)
        {
            PlayerPrefs.DeleteKey($"CollectiblesLocal_{sceneKey}_Present_{i}");
        }
        PlayerPrefs.DeleteKey($"CollectiblesLocal_{sceneKey}_PresentNamesCount");
        
        // Cancella nomi memories
        int memoryCount = PlayerPrefs.GetInt($"CollectiblesLocal_{sceneKey}_MemoryNamesCount", 0);
        for (int i = 0; i < memoryCount; i++)
        {
            PlayerPrefs.DeleteKey($"CollectiblesLocal_{sceneKey}_Memory_{i}");
        }
        PlayerPrefs.DeleteKey($"CollectiblesLocal_{sceneKey}_MemoryNamesCount");
        
        PlayerPrefs.Save();
        DebugLog($"[CollectiblesManager] 🗑️ Dati resettati per '{sceneName}'");
    }
    
    // ========== GETTERS - PRESENTS ==========
    
    public int GetCollectedPresents() => collectedPresents;
    public int GetTotalPresents() => totalPresents;
    public float GetPresentsProgress() => totalPresents > 0 ? (float)collectedPresents / totalPresents : 0f;
    public float GetPresentsCompletionPercentage() => GetPresentsProgress() * 100f;
    public bool AreAllPresentsCollected() => collectedPresents >= totalPresents && totalPresents > 0;
    public List<string> GetCollectedPresentNames() => new List<string>(collectedPresentNames);
    public Dictionary<string, CollectibleData> GetAllPresentsData() => new Dictionary<string, CollectibleData>(allPresents);
    
    // ========== GETTERS - MEMORIES ==========
    
    public int GetCollectedMemories() => collectedMemories;
    public int GetTotalMemories() => totalMemories;
    public float GetMemoriesProgress() => totalMemories > 0 ? (float)collectedMemories / totalMemories : 0f;
    public float GetMemoriesCompletionPercentage() => GetMemoriesProgress() * 100f;
    public bool AreAllMemoriesCollected() => collectedMemories >= totalMemories && totalMemories > 0;
    public List<string> GetCollectedMemoryNames() => new List<string>(collectedMemoryNames);
    public Dictionary<string, CollectibleData> GetAllMemoriesData() => new Dictionary<string, CollectibleData>(allMemories);
    
    // ========== GETTERS - COMBINED ==========
    
    public int GetTotalCollected() => collectedPresents + collectedMemories;
    public int GetTotalAvailable() => totalPresents + totalMemories;
    public float GetOverallProgress() 
    {
        int total = GetTotalAvailable();
        return total > 0 ? (float)GetTotalCollected() / total : 0f;
    }
    public float GetOverallCompletionPercentage() => GetOverallProgress() * 100f;
    public bool AreAllCollectiblesCompleted()
    {
        return AreAllPresentsCollected() && AreAllMemoriesCollected();
    }
    
    // ========== QUERY METHODS ==========
    
    /// <summary>
    /// Verifica se un collectible specifico è stato raccolto
    /// </summary>
    public bool IsCollectibleCollected(string name, CollectibleType type)
    {
        switch (type)
        {
            case CollectibleType.Present:
                return collectedPresentNames.Contains(name);
            case CollectibleType.Memory:
                return collectedMemoryNames.Contains(name);
            default:
                return false;
        }
    }
    
    /// <summary>
    /// Ottieni i dati di un collectible specifico
    /// </summary>
    public CollectibleData GetCollectibleData(string name, CollectibleType type)
    {
        switch (type)
        {
            case CollectibleType.Present:
                return allPresents.ContainsKey(name) ? allPresents[name] : null;
            case CollectibleType.Memory:
                return allMemories.ContainsKey(name) ? allMemories[name] : null;
            default:
                return null;
        }
    }
    
    /// <summary>
    /// Ottieni tutti i collectibles non ancora raccolti
    /// </summary>
    public List<CollectibleData> GetUncollectedCollectibles()
    {
        List<CollectibleData> uncollected = new List<CollectibleData>();
        
        foreach (var present in allPresents.Values)
        {
            if (!present.isCollected)
                uncollected.Add(present);
        }
        
        foreach (var memory in allMemories.Values)
        {
            if (!memory.isCollected)
                uncollected.Add(memory);
        }
        
        return uncollected;
    }
    
    /// <summary>
    /// Ottieni tutti i collectibles raccolti
    /// </summary>
    public List<CollectibleData> GetCollectedCollectibles()
    {
        List<CollectibleData> collected = new List<CollectibleData>();
        
        foreach (var present in allPresents.Values)
        {
            if (present.isCollected)
                collected.Add(present);
        }
        
        foreach (var memory in allMemories.Values)
        {
            if (memory.isCollected)
                collected.Add(memory);
        }
        
        return collected;
    }
    
    // ========== UTILITY METHODS ==========
    
    /// <summary>
    /// Imposta manualmente il scene name
    /// </summary>
    public void SetSceneName(string newSceneName)
    {
        sceneName = newSceneName;
        DebugLog($"[CollectiblesManager] Scene name impostato: '{sceneName}'");
    }
    
    /// <summary>
    /// Refresh completo del sistema
    /// </summary>
    public void RefreshCollectiblesSystem()
    {
        DebugLog("[CollectiblesManager] Refresh sistema collectibles");
        CountAllCollectibles();
        LoadCollectiblesData();
        UpdateAllUI();
    }
    
    /// <summary>
    /// Reset completo del progresso
    /// </summary>
    public void ResetCollectiblesProgress()
    {
        DebugLog("[CollectiblesManager] Reset progresso collectibles");
        
        collectedPresents = 0;
        collectedMemories = 0;
        collectedPresentNames.Clear();
        collectedMemoryNames.Clear();
        
        // Reset dati registrati
        foreach (var present in allPresents.Values)
        {
            present.isCollected = false;
        }
        
        foreach (var memory in allMemories.Values)
        {
            memory.isCollected = false;
        }
        
        // Reset dati salvati
        if (enableLocalSaving)
        {
            ResetCollectiblesData();
        }
        
        UpdateAllUI();
    }
    
    /// <summary>
    /// Forza il salvataggio dei dati
    /// </summary>
    public void ForceSave()
    {
        SaveCollectiblesData();
    }
    
    /// <summary>
    /// Forza il caricamento dei dati
    /// </summary>
    public void ForceLoad()
    {
        LoadCollectiblesData();
        UpdateAllUI();
    }
    
    // ========== METODI DI COMPATIBILITÀ ==========
    
    /// <summary>
    /// Metodi di compatibilità per SceneManager esistenti
    /// </summary>
    public void NotifyPresentCollected() => NotifyPresentCollected("");
    public void NotifyMemoryCollected() => NotifyMemoryCollected("");
    
    // ========== SETTINGS ==========
    
    public void SetLocalSavingEnabled(bool enabled)
    {
        enableLocalSaving = enabled;
        DebugLog($"[CollectiblesManager] Local saving {(enabled ? "abilitato" : "disabilitato")}");
    }
    
    public void SetAutoSaveEnabled(bool enabled)
    {
        autoSaveOnCollection = enabled;
        DebugLog($"[CollectiblesManager] Auto-save {(enabled ? "abilitato" : "disabilitato")}");
    }
    
    public void SetAutoCountEnabled(bool enabled)
    {
        autoCountOnStart = enabled;
        DebugLog($"[CollectiblesManager] Auto-count {(enabled ? "abilitato" : "disabilitato")}");
    }
    
    // ========== DEBUG ==========
    
    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log(message);
        }
    }
    
    public void SetDebugLogsEnabled(bool enabled)
    {
        enableDebugLogs = enabled;
    }
    
    [ContextMenu("Debug Current State")]
    public void DebugCurrentState()
    {
        string presentsList = string.Join(", ", collectedPresentNames);
        string memoriesList = string.Join(", ", collectedMemoryNames);
        
        Debug.Log($"=== CollectiblesManager State ===\n" +
                  $"Scene: '{sceneName}'\n" +
                  $"Presents: {collectedPresents}/{totalPresents} ({GetPresentsCompletionPercentage():F1}%)\n" +
                  $"Collected Presents: [{presentsList}]\n" +
                  $"Memories: {collectedMemories}/{totalMemories} ({GetMemoriesCompletionPercentage():F1}%)\n" +
                  $"Collected Memories: [{memoriesList}]\n" +
                  $"Total: {GetTotalCollected()}/{GetTotalAvailable()} ({GetOverallCompletionPercentage():F1}%)\n" +
                  $"All Complete: {AreAllCollectiblesCompleted()}\n" +
                  $"Local Saving: {enableLocalSaving} | Auto-Save: {autoSaveOnCollection}\n" +
                  $"Auto-Count: {autoCountOnStart}\n" +
                  $"UI Connections: CollectiblesUI={collectiblesUI != null}, PlayerUI={playerUI != null}\n" +
                  $"Registered Presents: {allPresents.Count} | Registered Memories: {allMemories.Count}");
    }
    
    [ContextMenu("Force UI Update")]
    public void DebugForceUIUpdate()
    {
        UpdateAllUI();
    }
    
    [ContextMenu("Force Recount")]
    public void DebugForceRecount()
    {
        CountAllCollectibles();
        UpdateAllUI();
        DebugCurrentState();
    }
    
    [ContextMenu("Test - Collect Random Present")]
    public void DebugCollectRandomPresent()
    {
        string testName = "TestPresent_" + System.DateTime.Now.Ticks;
        NotifyPresentCollected(testName);
    }
    
    [ContextMenu("Test - Collect Random Memory")]
    public void DebugCollectRandomMemory()
    {
        string testName = "TestMemory_" + System.DateTime.Now.Ticks;
        NotifyMemoryCollected(testName);
    }
    
    [ContextMenu("Reset Progress")]
    public void DebugResetProgress()
    {
        ResetCollectiblesProgress();
        DebugCurrentState();
    }
    
    [ContextMenu("Force Save")]
    public void DebugForceSave()
    {
        ForceSave();
    }
    
    [ContextMenu("Force Load")]
    public void DebugForceLoad()
    {
        ForceLoad();
        DebugCurrentState();
    }
    
    [ContextMenu("Show Registered Collectibles")]
    public void DebugShowRegisteredCollectibles()
    {
        Debug.Log("=== Registered Presents ===");
        foreach (var present in allPresents)
        {
            Debug.Log($"'{present.Key}': {present.Value.position} | Collected: {present.Value.isCollected}");
        }
        
        Debug.Log("=== Registered Memories ===");
        foreach (var memory in allMemories)
        {
            Debug.Log($"'{memory.Key}': {memory.Value.position} | Collected: {memory.Value.isCollected}");
        }
    }
    
    // ========== CLEANUP ==========
    
   // Modifica il metodo OnDestroy esistente per includere la pulizia dei listener:
private void OnDestroy()
{
    if (autoSaveOnCollection && enableLocalSaving)
    {
        SaveCollectiblesData();
    }
    
    // 🆕 PULIZIA LISTENER FIRST MEMORY DIALOGUE
    UnregisterFirstMemoryDialogueListeners();
    
    if (Instance == this)
    {
        Instance = null;
    }
    
    DebugLog("[CollectiblesManager] Cleanup completato");
}
}