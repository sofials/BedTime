using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Classe Present COMPLETAMENTE COMPATIBILE con la nuova classe base Collectibles.
/// Sistema audio PURO: usa solo AudioSource configurati direttamente nell'Inspector.
/// NON modifica mai parametri degli AudioSource (volume, distanze, 3D) - rispetta configurazione Inspector.
/// NON disattiva mai l'intero GameObject - solo nasconde mesh e avvia effetti.
/// AGGIUNTA: Sistema Sweet Plaza per attivare oggetti alla raccolta.
/// </summary>
public class Presents : Collectibles
{
    [Header("Present Events (Optional)")]
    [SerializeField] private bool enablePresentEvents = false;
    [SerializeField] private UnityEvent<Presents> OnPresentCollected;

    [Header("Present Debug")]
    [SerializeField] private bool enableDetailedLogs = false;
    
    [Header("Present Specific Settings")]
    [SerializeField] private PresentSize presentSize = PresentSize.Medium;
    
    [Header("Sweet Plaza Special")]
    [SerializeField] private bool isSweetPlazaPresent = false;
    [Tooltip("Oggetto che verrà attivato quando questo regalo della Sweet Plaza viene raccolto")]
    [SerializeField] private GameObject sweetPlazaObjectToEnable;
    [Tooltip("Messaggio speciale per i regali della Sweet Plaza")]
    [SerializeField] private string sweetPlazaMessage = "Dolce sorpresa della Sweet Plaza sbloccata!";
    
    [Header("Playground Arena Special")]
    [SerializeField] private bool isPlaygroundArenaPresent = false;
    [Tooltip("Oggetto che verrà attivato quando questo regalo del Playground Arena viene raccolto")]
    [SerializeField] private GameObject playgroundArenaObjectToEnable;
    [Tooltip("Messaggio speciale per i regali del Playground Arena")]
    [SerializeField] private string playgroundArenaMessage = "Playground Arena sbloccato!";
    
    [Header("Collection Completion System")]
    [Tooltip("Oggetto che verrà attivato quando ENTRAMBI i tipi speciali vengono raccolti nella scena")]
    [SerializeField] private GameObject completionObjectToEnable;
    [Tooltip("Messaggio quando la collezione è completata")]
    [SerializeField] private string completionMessage = "🎉 COLLEZIONE COMPLETA! Tutti i regali speciali raccolti!";
    
    // Sistema di tracking globale per la scena
    private static bool sweetPlazaCollectedInScene = false;
    private static bool playgroundArenaCollectedInScene = false;
    private static GameObject globalCompletionObject = null;
    private static string globalCompletionMessage = "";
    
    // Enum per le dimensioni dei regali
    public enum PresentSize
    {
        Small,   // Effetti veloci, raccolta rapida
        Medium,  // Comportamento standard
        Large    // Effetti prolungati, più spettacolari
    }

    protected override void Awake()
    {
        // FASE 1: Imposta tipo Present PRIMA del base Awake
        collectibleType = CollectibleType.Present;
        
        // FASE 2: Abilita debug se richiesto
        if (enableDetailedLogs)
        {
            Debug.Log($"[Presents] Debug abilitato per {gameObject.name}");
        }
        
        // FASE 3: Chiama il base Awake (gestisce auto-detection, configurazioni default e audio)
        base.Awake();
        
        // FASE 4: Configurazioni specifiche Present DOPO il setup base
        ConfigurePresentDefaults();
        
        // FASE 5: Registra l'oggetto di completamento se impostato
        RegisterCompletionObject();
    }

    /// <summary>
    /// Registra l'oggetto di completamento per il sistema globale
    /// </summary>
    private void RegisterCompletionObject()
    {
        if (completionObjectToEnable != null && globalCompletionObject == null)
        {
            globalCompletionObject = completionObjectToEnable;
            globalCompletionMessage = completionMessage;
            LogDebug($"Oggetto di completamento registrato: {completionObjectToEnable.name}");
        }
    }

    /// <summary>
    /// Configurazioni default specifiche per i Present
    /// NON tocca le impostazioni audio che sono gestite completamente dalla classe base
    /// </summary>
    protected virtual void ConfigurePresentDefaults()
    {
        
        // Configurazioni basate sulla dimensione del presente
        switch (presentSize)
        {
            case PresentSize.Small:
                rotationSpeed = 60f;
                rotationAxis = Vector3.up;
                floatSpeed = 3f;
                floatStrength = 0.2f;
                if (collectibleValue == 1) collectibleValue = 5; // Small = 5 points
                effectDuration = 1.5f;
                break;
                
            case PresentSize.Medium:
                rotationSpeed = 45f;
                rotationAxis = Vector3.up;
                floatSpeed = 2.5f;
                floatStrength = 0.3f;
                if (collectibleValue == 1) collectibleValue = 10; // Medium = 10 points
                effectDuration = 2f;
                break;
                
            case PresentSize.Large:
                rotationSpeed = 30f;
                rotationAxis = Vector3.up;
                floatSpeed = 2f;
                floatStrength = 0.4f;
                if (collectibleValue == 1) collectibleValue = 20; // Large = 20 points
                effectDuration = 3f;
                break;
        }
        
        // Configurazioni specifiche Present
        canBeClickedToCollect = false; // Solo trigger per Present
        canBeTriggerToCollect = true;
        enableRotation = true;
        enableFloating = true;
        
        // ⭐ IMPORTANTE: NON tocchiamo audio - è gestito dalla classe base ⭐
        // enablePreAudioLoop è già impostato a TRUE per Present da ConfigureDefaultsForType()
        
      
    }

    /// <summary>
    /// Override del metodo di raccolta con logging specifico Present
    /// </summary>
    public override void CollectItem()
    {
      
        
        // Verifica componenti prima della raccolta (solo se debug attivo)
        if (enableDetailedLogs)
        {
            ValidatePresentComponentsQuick();
        }
        
        // Chiama il base CollectItem che gestisce tutto (audio, effetti, mesh, etc.)
        base.CollectItem();
        
    }

    /// <summary>
    /// Hook specifico Present per eventi post-raccolta
    /// </summary>
    protected override void OnItemCollected()
    {
        // Gestione speciale Sweet Plaza PRIMA degli eventi base
        if (isSweetPlazaPresent)
        {
            HandleSweetPlazaCollection();
        }
        
        // Gestione speciale Playground Arena
        if (isPlaygroundArenaPresent)
        {
            HandlePlaygroundArenaCollection();
        }
        
        // Invoca eventi Present specifici
        if (enablePresentEvents)
        {
            try
            {
                OnPresentCollected?.Invoke(this);
                LogDebug($"Evento OnPresentCollected invocato per {collectibleName}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Presents] Errore nell'invocare OnPresentCollected per {collectibleName}: {e.Message}");
            }
        }
    }

    // ========== SWEET PLAZA SYSTEM ==========

    /// <summary>
    /// Configura questo Present come regalo speciale della Sweet Plaza
    /// </summary>
    public void ConfigureAsSweetPlazaPresent(GameObject objectToEnable = null, string customMessage = null)
    {
        isSweetPlazaPresent = true;
        isPlaygroundArenaPresent = false; // Esclude gli altri tipi
        
        if (objectToEnable != null)
            sweetPlazaObjectToEnable = objectToEnable;
        
        if (!string.IsNullOrEmpty(customMessage))
            sweetPlazaMessage = customMessage;
        
        // Configurazioni specifiche Sweet Plaza
        collectibleName = "Sweet Plaza Gift";
        presentSize = PresentSize.Large;
        collectibleValue = 30;
        rotationSpeed = 35f;
        floatSpeed = 1.8f;
        floatStrength = 0.45f;
        displayMessage = string.IsNullOrEmpty(customMessage) ? sweetPlazaMessage : customMessage;
        delayBeforeHiding = 4f; // Tempo extra per ammirare l'effetto
        
        // Riapplica le configurazioni
        ConfigurePresentDefaults();
        
        LogDebug($"Configurato come Sweet Plaza Present - Oggetto da abilitare: {(sweetPlazaObjectToEnable != null ? sweetPlazaObjectToEnable.name : "NESSUNO")}");
    }

    /// <summary>
    /// Gestisce la logica specifica per i regali della Sweet Plaza
    /// </summary>
    private void HandleSweetPlazaCollection()
    {
        LogDebug($"🍭 Sweet Plaza Present raccolto: {collectibleName}");
        
        // Attiva l'oggetto specificato
        if (sweetPlazaObjectToEnable != null)
        {
            try
            {
                bool wasActive = sweetPlazaObjectToEnable.activeInHierarchy;
                sweetPlazaObjectToEnable.SetActive(true);
                
                LogDebug($"✅ Oggetto Sweet Plaza abilitato: {sweetPlazaObjectToEnable.name} (era attivo: {wasActive})");
                
                // Log aggiuntivo se l'oggetto era già attivo
                if (wasActive)
                {
                    LogDebug($"ℹ️ L'oggetto {sweetPlazaObjectToEnable.name} era già attivo");
                }
                
                // Feedback visivo/audio aggiuntivo per Sweet Plaza
                PlaySweetPlazaFeedback();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Presents] ERRORE nell'abilitare oggetto Sweet Plaza {sweetPlazaObjectToEnable.name}: {e.Message}");
            }
        }
        else
        {
            Debug.LogWarning($"[Presents] Sweet Plaza Present raccolto ma nessun oggetto da abilitare assegnato!");
        }
        
        // Marca Sweet Plaza come raccolto e controlla completamento
        sweetPlazaCollectedInScene = true;
        CheckCollectionCompletion();
    }

    /// <summary>
    /// Feedback aggiuntivo specifico per Sweet Plaza
    /// </summary>
    private void PlaySweetPlazaFeedback()
    {
        // Potresti aggiungere qui effetti speciali per Sweet Plaza
        // Ad esempio: particelle speciali, suoni diversi, etc.
        
        LogDebug($"🎉 Feedback Sweet Plaza riprodotto per {collectibleName}");
        
        // Esempio: potresti aggiungere effetti particelle speciali
        // o modificare temporaneamente i colori degli effetti esistenti
    }

    // ========== PLAYGROUND ARENA SYSTEM ==========

    /// <summary>
    /// Configura questo Present come regalo speciale del Playground Arena
    /// </summary>
    public void ConfigureAsPlaygroundArenaPresent(GameObject objectToEnable = null, string customMessage = null)
    {
        isPlaygroundArenaPresent = true;
        isSweetPlazaPresent = false; // Esclude gli altri tipi
        
        if (objectToEnable != null)
            playgroundArenaObjectToEnable = objectToEnable;
        
        if (!string.IsNullOrEmpty(customMessage))
            playgroundArenaMessage = customMessage;
        
        // Configurazioni specifiche Playground Arena
        collectibleName = "Playground Arena Gift";
        presentSize = PresentSize.Large;
        collectibleValue = 35;
        rotationSpeed = 40f; // Leggermente più veloce per l'energia del playground
        floatSpeed = 2.2f;
        floatStrength = 0.5f;
        displayMessage = string.IsNullOrEmpty(customMessage) ? playgroundArenaMessage : customMessage;
        delayBeforeHiding = 5f; // Tempo extra per playground
        
        // Riapplica le configurazioni
        ConfigurePresentDefaults();
        
        LogDebug($"Configurato come Playground Arena Present - Oggetto da abilitare: {(playgroundArenaObjectToEnable != null ? playgroundArenaObjectToEnable.name : "NESSUNO")}");
    }

    /// <summary>
    /// Gestisce la logica specifica per i regali del Playground Arena
    /// </summary>
    private void HandlePlaygroundArenaCollection()
    {
        LogDebug($"🎮 Playground Arena Present raccolto: {collectibleName}");
        
        // Attiva l'oggetto specificato
        if (playgroundArenaObjectToEnable != null)
        {
            try
            {
                bool wasActive = playgroundArenaObjectToEnable.activeInHierarchy;
                playgroundArenaObjectToEnable.SetActive(true);
                
                LogDebug($"✅ Oggetto Playground Arena abilitato: {playgroundArenaObjectToEnable.name} (era attivo: {wasActive})");
                
                // Log aggiuntivo se l'oggetto era già attivo
                if (wasActive)
                {
                    LogDebug($"ℹ️ L'oggetto {playgroundArenaObjectToEnable.name} era già attivo");
                }
                
                // Feedback visivo/audio aggiuntivo per Playground Arena
                PlayPlaygroundArenaFeedback();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Presents] ERRORE nell'abilitare oggetto Playground Arena {playgroundArenaObjectToEnable.name}: {e.Message}");
            }
        }
        else
        {
            Debug.LogWarning($"[Presents] Playground Arena Present raccolto ma nessun oggetto da abilitare assegnato!");
        }
        
        // Marca Playground Arena come raccolto e controlla completamento
        playgroundArenaCollectedInScene = true;
        CheckCollectionCompletion();
    }

    /// <summary>
    /// Feedback aggiuntivo specifico per Playground Arena
    /// </summary>
    private void PlayPlaygroundArenaFeedback()
    {
        // Potresti aggiungere qui effetti speciali per Playground Arena
        // Ad esempio: particelle energetiche, suoni di attivazione, etc.
        
        LogDebug($"🎮 Feedback Playground Arena riprodotto per {collectibleName}");
        
        // Esempio: potresti aggiungere effetti particelle più dinamici
        // o suoni più energici per il playground
    }

    // ========== COLLECTION COMPLETION SYSTEM ==========

    /// <summary>
    /// Controlla se entrambi i regali speciali sono stati raccolti e attiva l'oggetto di completamento
    /// </summary>
    private static void CheckCollectionCompletion()
    {
        if (sweetPlazaCollectedInScene && playgroundArenaCollectedInScene)
        {
            Debug.Log($"🎉 [Presents] COLLEZIONE COMPLETA! Entrambi i regali speciali sono stati raccolti!");
            
            // Attiva l'oggetto di completamento se presente
            if (globalCompletionObject != null)
            {
                try
                {
                    bool wasActive = globalCompletionObject.activeInHierarchy;
                    globalCompletionObject.SetActive(true);
                    
                    Debug.Log($"✅ [Presents] Oggetto di completamento attivato: {globalCompletionObject.name} (era attivo: {wasActive})");
                    
                    if (!string.IsNullOrEmpty(globalCompletionMessage))
                    {
                        Debug.Log($"📢 [Presents] {globalCompletionMessage}");
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[Presents] ERRORE nell'attivare oggetto di completamento {globalCompletionObject.name}: {e.Message}");
                }
            }
            else
            {
                Debug.LogWarning($"[Presents] Collezione completata ma nessun oggetto di completamento configurato!");
            }
        }
        else
        {
            Debug.Log($"[Presents] Progresso collezione: Sweet Plaza={sweetPlazaCollectedInScene}, Playground Arena={playgroundArenaCollectedInScene}");
        }
    }

    /// <summary>
    /// Resetta il tracking della collezione (utile per test o riavvio livello)
    /// </summary>
    public static void ResetCollectionTracking()
    {
        sweetPlazaCollectedInScene = false;
        playgroundArenaCollectedInScene = false;
        globalCompletionObject = null;
        globalCompletionMessage = "";
        Debug.Log($"[Presents] Tracking collezione resettato");
    }

    /// <summary>
    /// Forza il completamento della collezione (per test)
    /// </summary>
    public static void ForceCollectionCompletion()
    {
        sweetPlazaCollectedInScene = true;
        playgroundArenaCollectedInScene = true;
        CheckCollectionCompletion();
        Debug.Log($"[Presents] Completamento collezione forzato");
    }

    /// <summary>
    /// Validazione rapida dei componenti (solo per debug)
    /// </summary>
    private void ValidatePresentComponentsQuick()
    {
        if (meshContainer == null && (meshRenderers == null || meshRenderers.Length == 0))
        {
            LogDebug($"⚠️ ATTENZIONE: Nessun mesh configurato per nascondere!");
        }
        
        if (preCollectionEffects == null || preCollectionEffects.Length == 0)
        {
            LogDebug($"⚠️ ATTENZIONE: Nessun effetto pre-raccolta configurato!");
        }
        
        if (postCollectionEffects == null || postCollectionEffects.Length == 0)
        {
            LogDebug($"⚠️ ATTENZIONE: Nessun effetto post-raccolta configurato!");
        }
        
        if (GetPreAudioSource() == null)
        {
            LogDebug($"⚠️ ATTENZIONE: Pre AudioSource non assegnato!");
        }
        
        if (GetPostAudioSource() == null)
        {
            LogDebug($"⚠️ ATTENZIONE: Post AudioSource non assegnato!");
        }
    }

    private int CountPlayingEffects(ParticleSystem[] effects)
    {
        int count = 0;
        foreach (var effect in effects)
        {
            if (effect != null && effect.isPlaying)
                count++;
        }
        return count;
    }

    // ========== CONFIGURAZIONI PRESET (AGGIORNATE) ==========

    public void ConfigureAsChristmasPresent()
    {
        ConfigurePreset("Christmas Gift", PresentSize.Medium, 15, 40f, 2f, 0.3f, "Regalo di Natale trovato!");
        LogDebug("Configurato come regalo di Natale");
    }

    public void ConfigureAsBirthdayPresent()
    {
        ConfigurePreset("Birthday Gift", PresentSize.Large, 25, 50f, 2.5f, 0.4f, "Buon compleanno! Regalo trovato!");
        delayBeforeHiding = 5f; // Più tempo per i compleanni
        LogDebug("Configurato come regalo di compleanno");
    }

    public void ConfigureAsSpecialPresent()
    {
        ConfigurePreset("Special Gift", PresentSize.Large, 35, 25f, 1.5f, 0.5f, "Regalo speciale scoperto!");
        delayBeforeHiding = 6f; // Massimo tempo per regali speciali
        LogDebug("Configurato come regalo speciale");
    }

    public void ConfigureAsSmallSurprise()
    {
        ConfigurePreset("Small Surprise", PresentSize.Small, 8, 80f, 4f, 0.15f, "Piccola sorpresa!");
        delayBeforeHiding = 1.5f; // Veloce per piccole sorprese
        LogDebug("Configurato come piccola sorpresa");
    }

    private void ConfigurePreset(string name, PresentSize size, int value, float rotSpeed, float floatSpd, float floatStr, string message)
    {
        collectibleName = name;
        presentSize = size;
        collectibleValue = value;
        rotationSpeed = rotSpeed;
        rotationAxis = Vector3.up;
        floatSpeed = floatSpd;
        floatStrength = floatStr;
        displayMessage = message;
        
        // Riapplica le configurazioni basate sulla size
        ConfigurePresentDefaults();
    }

    // ========== VALIDAZIONE COMPLETA PRESENT ==========

    /// <summary>
    /// Verifica che i componenti Present siano correttamente configurati
    /// </summary>
    public bool ValidatePresentComponents()
    {
        bool valid = true;
        
        LogDebug($"=== VALIDAZIONE COMPONENTI PRESENT {collectibleName} ===");
        
        // Usa il metodo di validazione della classe base
        bool baseValid = ValidateComponents();
        valid = valid && baseValid;
        
        // Validazioni specifiche Present
        if (presentSize < PresentSize.Small || presentSize > PresentSize.Large)
        {
            LogDebug("❌ Present Size non valido!");
            valid = false;
        }
        else
        {
            LogDebug($"✅ Present Size: {presentSize}");
        }
        
        // Verifica che enablePreAudioLoop sia true per Present
        if (!IsPreAudioLoopEnabled())
        {
            LogDebug("⚠️ ATTENZIONE: enablePreAudioLoop dovrebbe essere TRUE per i Present!");
        }
        else
        {
            LogDebug($"✅ Pre Audio Loop: {IsPreAudioLoopEnabled()}");
        }
        
        // Verifica che non sia configurato per click (solo trigger)
        if (canBeClickedToCollect)
        {
            LogDebug("⚠️ ATTENZIONE: Present dovrebbe essere solo trigger-collectable!");
        }
        else
        {
            LogDebug($"✅ Collect Mode: Solo Trigger");
        }
        
        LogDebug($"Validazione Present completata - Risultato: {(valid ? "VALIDO" : "PROBLEMI TROVATI")}");
        return valid;
    }

    /// <summary>
    /// Validazione specifica per Sweet Plaza Present
    /// </summary>
    public bool ValidateSweetPlazaSetup()
    {
        bool valid = true;
        
        LogDebug($"=== VALIDAZIONE SWEET PLAZA SETUP ===");
        
        if (!isSweetPlazaPresent)
        {
            LogDebug($"ℹ️ Non è un Sweet Plaza Present - validazione non necessaria");
            return true;
        }
        
        // Verifica oggetto da abilitare
        if (sweetPlazaObjectToEnable == null)
        {
            LogDebug($"❌ Sweet Plaza Present senza oggetto da abilitare!");
            valid = false;
        }
        else
        {
            LogDebug($"✅ Oggetto da abilitare: {sweetPlazaObjectToEnable.name}");
            
            // Controlla se l'oggetto è già distrutto
            if (sweetPlazaObjectToEnable == null)
            {
                LogDebug($"❌ L'oggetto assegnato è stato distrutto!");
                valid = false;
            }
        }
        
        // Verifica messaggio
        if (string.IsNullOrEmpty(sweetPlazaMessage))
        {
            LogDebug($"⚠️ Messaggio Sweet Plaza vuoto");
        }
        else
        {
            LogDebug($"✅ Messaggio: {sweetPlazaMessage}");
        }
        
        LogDebug($"Validazione Sweet Plaza: {(valid ? "✅ VALIDA" : "❌ PROBLEMI")}");
        return valid;
    }

    /// <summary>
    /// Validazione specifica per Playground Arena Present
    /// </summary>
    public bool ValidatePlaygroundArenaSetup()
    {
        bool valid = true;
        
        LogDebug($"=== VALIDAZIONE PLAYGROUND ARENA SETUP ===");
        
        if (!isPlaygroundArenaPresent)
        {
            LogDebug($"ℹ️ Non è un Playground Arena Present - validazione non necessaria");
            return true;
        }
        
        // Verifica oggetto da abilitare
        if (playgroundArenaObjectToEnable == null)
        {
            LogDebug($"❌ Playground Arena Present senza oggetto da abilitare!");
            valid = false;
        }
        else
        {
            LogDebug($"✅ Oggetto da abilitare: {playgroundArenaObjectToEnable.name}");
            
            // Controlla se l'oggetto è già distrutto
            if (playgroundArenaObjectToEnable == null)
            {
                LogDebug($"❌ L'oggetto assegnato è stato distrutto!");
                valid = false;
            }
        }
        
        // Verifica messaggio
        if (string.IsNullOrEmpty(playgroundArenaMessage))
        {
            LogDebug($"⚠️ Messaggio Playground Arena vuoto");
        }
        else
        {
            LogDebug($"✅ Messaggio: {playgroundArenaMessage}");
        }
        
        LogDebug($"Validazione Playground Arena: {(valid ? "✅ VALIDA" : "❌ PROBLEMI")}");
        return valid;
    }

    /// <summary>
    /// Test del ciclo di vita completo del Present
    /// </summary>
    public void TestPresentCycle()
    {
        LogDebug($"=== TEST CICLO PRESENT {collectibleName} ===");
        
        // Se già raccolto, resetta prima
        if (IsCollected())
        {
            ResetCollected();
            LogDebug("✅ Reset completato");
        }
        
        // Validazione componenti
        ValidatePresentComponents();
        
        // Simula raccolta
        LogDebug("🎁 Simulazione raccolta...");
        ForceCollect();
        
        LogDebug("=== TEST CICLO COMPLETATO ===");
    }

    /// <summary>
    /// Test della funzionalità Sweet Plaza
    /// </summary>
    public void TestSweetPlazaToggle()
    {
        if (!isSweetPlazaPresent)
        {
            LogDebug("❌ Questo non è un Sweet Plaza Present. Configuralo prima con ConfigureAsSweetPlazaPresent()");
            return;
        }
        
        LogDebug($"🧪 === TEST SWEET PLAZA TOGGLE ===");
        LogDebug($"Oggetto da abilitare: {(sweetPlazaObjectToEnable != null ? sweetPlazaObjectToEnable.name : "NESSUNO")}");
        
        if (sweetPlazaObjectToEnable != null)
        {
            bool currentState = sweetPlazaObjectToEnable.activeInHierarchy;
            LogDebug($"Stato attuale oggetto: {(currentState ? "ATTIVO" : "DISATTIVO")}");
            
            // Simula la raccolta
            LogDebug($"🍭 Simulazione raccolta Sweet Plaza...");
            HandleSweetPlazaCollection();
            
            bool newState = sweetPlazaObjectToEnable.activeInHierarchy;
            LogDebug($"Nuovo stato oggetto: {(newState ? "ATTIVO" : "DISATTIVO")}");
            LogDebug($"Cambio stato: {(currentState != newState ? "✅ SUCCESSO" : "⚠️ NESSUN CAMBIO")}");
        }
        else
        {
            LogDebug($"⚠️ Nessun oggetto assegnato per il test!");
        }
    }

    // ========== GETTERS SPECIFICI PRESENT ==========

    public bool IsDetailedLogsEnabled() => enableDetailedLogs;
    public bool ArePresentEventsEnabled() => enablePresentEvents;
    public PresentSize GetPresentSize() => presentSize;
    
    public string GetPresentSizeString() => presentSize.ToString();
    public int GetPresentValueBySize()
    {
        return presentSize switch
        {
            PresentSize.Small => 5,
            PresentSize.Medium => 10,
            PresentSize.Large => 20,
            _ => GetCollectibleValue()
        };
    }

    // Sweet Plaza Getters
    public bool IsSweetPlazaPresent() => isSweetPlazaPresent;
    public GameObject GetSweetPlazaObjectToEnable() => sweetPlazaObjectToEnable;
    public string GetSweetPlazaMessage() => sweetPlazaMessage;

    // Playground Arena Getters
    public bool IsPlaygroundArenaPresent() => isPlaygroundArenaPresent;
    public GameObject GetPlaygroundArenaObjectToEnable() => playgroundArenaObjectToEnable;
    public string GetPlaygroundArenaMessage() => playgroundArenaMessage;

    // Special Present Type Detection
    public string GetSpecialPresentType()
    {
        if (isSweetPlazaPresent) return "Sweet Plaza";
        if (isPlaygroundArenaPresent) return "Playground Arena";
        return "Standard";
    }

    public bool IsSpecialPresent() => isSweetPlazaPresent || isPlaygroundArenaPresent;

    // Collection Completion Getters
    public GameObject GetCompletionObjectToEnable() => completionObjectToEnable;
    public string GetCompletionMessage() => completionMessage;
    public static bool IsSweetPlazaCollectedInScene() => sweetPlazaCollectedInScene;
    public static bool IsPlaygroundArenaCollectedInScene() => playgroundArenaCollectedInScene;
    public static bool IsCollectionComplete() => sweetPlazaCollectedInScene && playgroundArenaCollectedInScene;
    public static GameObject GetGlobalCompletionObject() => globalCompletionObject;

    // ========== SETTERS SPECIFICI PRESENT ==========

    public void SetDetailedLogs(bool enabled) 
    { 
        enableDetailedLogs = enabled;
        LogDebug($"Detailed logs {(enabled ? "abilitati" : "disabilitati")}");
    }
    
    public void SetPresentEventsEnabled(bool enabled) 
    { 
        enablePresentEvents = enabled;
        LogDebug($"Present events {(enabled ? "abilitati" : "disabilitati")}");
    }
    
    public void SetPresentSize(PresentSize size) 
    { 
        presentSize = size;
        ConfigurePresentDefaults(); // Riapplica configurazioni
        LogDebug($"Present size cambiato a: {size}");
    }

    public void SetAnimationEnabled(bool rotation, bool floating)
    {
        enableRotation = rotation;
        enableFloating = floating;
        LogDebug($"Animazioni: Rotation={rotation}, Floating={floating}");
    }

    // Sweet Plaza Setters
    public void SetSweetPlazaPresent(bool isSweet) 
    {
        isSweetPlazaPresent = isSweet;
        if (isSweet) isPlaygroundArenaPresent = false; // Esclusività
        LogDebug($"Sweet Plaza Present: {(isSweet ? "ABILITATO" : "DISABILITATO")}");
    }

    public void SetSweetPlazaObjectToEnable(GameObject obj)
    {
        sweetPlazaObjectToEnable = obj;
        LogDebug($"Oggetto Sweet Plaza da abilitare impostato: {(obj != null ? obj.name : "NESSUNO")}");
    }

    public void SetSweetPlazaMessage(string message)
    {
        sweetPlazaMessage = message;
        if (isSweetPlazaPresent && !string.IsNullOrEmpty(message))
        {
            displayMessage = message;
        }
        LogDebug($"Messaggio Sweet Plaza impostato: {message}");
    }

    // Playground Arena Setters
    public void SetPlaygroundArenaPresent(bool isArena) 
    {
        isPlaygroundArenaPresent = isArena;
        if (isArena) isSweetPlazaPresent = false; // Esclusività
        LogDebug($"Playground Arena Present: {(isArena ? "ABILITATO" : "DISABILITATO")}");
    }

    public void SetPlaygroundArenaObjectToEnable(GameObject obj)
    {
        playgroundArenaObjectToEnable = obj;
        LogDebug($"Oggetto Playground Arena da abilitare impostato: {(obj != null ? obj.name : "NESSUNO")}");
    }

    public void SetPlaygroundArenaMessage(string message)
    {
        playgroundArenaMessage = message;
        if (isPlaygroundArenaPresent && !string.IsNullOrEmpty(message))
        {
            displayMessage = message;
        }
        LogDebug($"Messaggio Playground Arena impostato: {message}");
    }

    // Collection Completion Setters
    public void SetCompletionObjectToEnable(GameObject obj)
    {
        completionObjectToEnable = obj;
        if (obj != null && globalCompletionObject == null)
        {
            globalCompletionObject = obj;
            LogDebug($"Oggetto di completamento globale impostato: {obj.name}");
        }
    }

    public void SetCompletionMessage(string message)
    {
        completionMessage = message;
        if (!string.IsNullOrEmpty(message) && string.IsNullOrEmpty(globalCompletionMessage))
        {
            globalCompletionMessage = message;
        }
        LogDebug($"Messaggio di completamento impostato: {message}");
    }

    // ========== METODI PER COMPATIBILITÀ ==========

    public void CollectPresent() => CollectItem();
    public string GetPresentName() => GetName();
    public int GetPresentValue() => GetCollectibleValue();
    public void SetPresentName(string name) => collectibleName = name;
    public void SetPresentValue(int value) => collectibleValue = value;
    public void ResetPresent() => ResetCollected();

    // ========== DEBUG METHODS ==========

    private void LogDebug(string message)
    {
        if (enableDetailedLogs)
            Debug.Log($"[Presents] {message}");
    }

    [ContextMenu("🎄 Configure as Christmas Present")]
    public void DebugConfigureChristmas() => ConfigureAsChristmasPresent();

    [ContextMenu("🎂 Configure as Birthday Present")]
    public void DebugConfigureBirthday() => ConfigureAsBirthdayPresent();

    [ContextMenu("⭐ Configure as Special Present")]
    public void DebugConfigureSpecial() => ConfigureAsSpecialPresent();

    [ContextMenu("🎁 Configure as Small Surprise")]
    public void DebugConfigureSmallSurprise() => ConfigureAsSmallSurprise();

    [ContextMenu("🍭 Configure as Sweet Plaza Present")]
    public void DebugConfigureSweetPlaza()
    {
        ConfigureAsSweetPlazaPresent();
        Debug.Log($"[Presents] {collectibleName} configurato come Sweet Plaza Present");
    }

    [ContextMenu("🎮 Configure as Playground Arena Present")]
    public void DebugConfigurePlaygroundArena()
    {
        ConfigureAsPlaygroundArenaPresent();
        Debug.Log($"[Presents] {collectibleName} configurato come Playground Arena Present");
    }

    [ContextMenu("📝 Toggle Detailed Logs")]
    public void DebugToggleDetailedLogs()
    {
        SetDetailedLogs(!enableDetailedLogs);
        Debug.Log($"[Presents] Detailed Logs per {collectibleName}: {(enableDetailedLogs ? "ABILITATI" : "DISABILITATI")}");
    }

    [ContextMenu("🎉 Toggle Present Events")]
    public void DebugTogglePresentEvents()
    {
        enablePresentEvents = !enablePresentEvents;
        Debug.Log($"[Presents] Present Events per {collectibleName}: {(enablePresentEvents ? "ABILITATI" : "DISABILITATI")}");
    }

    [ContextMenu("🍭 Toggle Sweet Plaza Mode")]
    public void DebugToggleSweetPlazaMode()
    {
        SetSweetPlazaPresent(!isSweetPlazaPresent);
        
        if (isSweetPlazaPresent && sweetPlazaObjectToEnable == null)
        {
            Debug.LogWarning($"[Presents] Sweet Plaza mode attivato ma nessun oggetto assegnato! Assegna 'Sweet Plaza Object To Enable' nell'Inspector.");
        }
        
        Debug.Log($"[Presents] Sweet Plaza mode per {collectibleName}: {(isSweetPlazaPresent ? "ABILITATO" : "DISABILITATO")}");
    }

    [ContextMenu("🎮 Toggle Playground Arena Mode")]
    public void DebugTogglePlaygroundArenaMode()
    {
        SetPlaygroundArenaPresent(!isPlaygroundArenaPresent);
        
        if (isPlaygroundArenaPresent && playgroundArenaObjectToEnable == null)
        {
            Debug.LogWarning($"[Presents] Playground Arena mode attivato ma nessun oggetto assegnato! Assegna 'Playground Arena Object To Enable' nell'Inspector.");
        }
        
        Debug.Log($"[Presents] Playground Arena mode per {collectibleName}: {(isPlaygroundArenaPresent ? "ABILITATO" : "DISABILITATO")}");
    }

    [ContextMenu("✅ Validate Present Components")]
    public void DebugValidatePresentComponents() => ValidatePresentComponents();

    [ContextMenu("✅ Validate Sweet Plaza Setup")]
    public void DebugValidateSweetPlaza() => ValidateSweetPlazaSetup();

    [ContextMenu("✅ Validate Playground Arena Setup")]
    public void DebugValidatePlaygroundArena() => ValidatePlaygroundArenaSetup();

    [ContextMenu("🎉 Test Collection Completion")]
    public void DebugTestCollectionCompletion()
    {
        Debug.Log($"🧪 === TEST COLLECTION COMPLETION ===");
        Debug.Log($"Sweet Plaza raccolto: {sweetPlazaCollectedInScene}");
        Debug.Log($"Playground Arena raccolto: {playgroundArenaCollectedInScene}");
        Debug.Log($"Collezione completa: {IsCollectionComplete()}");
        Debug.Log($"Oggetto di completamento: {(globalCompletionObject != null ? globalCompletionObject.name : "NESSUNO")}");
        
        if (!IsCollectionComplete())
        {
            Debug.Log($"🔄 Forzando completamento per test...");
            ForceCollectionCompletion();
        }
    }

    [ContextMenu("🔄 Reset Collection Tracking")]
    public void DebugResetCollectionTracking() => ResetCollectionTracking();

    [ContextMenu("📊 Debug Present State")]
    public void DebugPresentState()
    {
        string state = $"=== STATO PRESENT {collectibleName} ===\n" +
                      $"Present Size: {presentSize}\n" +
                      $"Is Collected: {IsCollected()}\n" +
                      $"GameObject Active: {gameObject.activeInHierarchy}\n" +
                      $"Mesh Container: {(meshContainer != null ? meshContainer.name + " (Active: " + meshContainer.gameObject.activeInHierarchy + ")" : "NULL")}\n" +
                      $"Effects Container: {(effectsContainer != null ? effectsContainer.name + " (Active: " + effectsContainer.gameObject.activeInHierarchy + ")" : "NULL")}\n" +
                      $"Pre Effects: {(preCollectionEffects != null ? preCollectionEffects.Length + " (" + CountPlayingEffects(preCollectionEffects) + " playing)" : "NULL")}\n" +
                      $"Post Effects: {(postCollectionEffects != null ? postCollectionEffects.Length + " (" + CountPlayingEffects(postCollectionEffects) + " playing)" : "NULL")}\n" +
                      $"Main Collider: {(mainCollider != null ? mainCollider.GetType().Name + " (enabled: " + mainCollider.enabled + ")" : "NULL")}\n" +
                      $"🔊 Audio Settings:\n" +
                      $"   - Pre AudioSource: {(GetPreAudioSource() != null ? GetPreAudioSource().name : "NULL")}\n" +
                      $"   - Post AudioSource: {(GetPostAudioSource() != null ? GetPostAudioSource().name : "NULL")}\n" +
                      $"   - Pre Audio Clip: {(GetPreAudioSource()?.clip != null ? GetPreAudioSource().clip.name : "NULL")}\n" +
                      $"   - Post Audio Clip: {(GetPostAudioSource()?.clip != null ? GetPostAudioSource().clip.name : "NULL")}\n" +
                      $"   - Max Distance: {GetAudioMaxDistance()}\n" +
                      $"   - Pre Audio Loop Enabled: {IsPreAudioLoopEnabled()}\n" +
                      $"   - Pre Audio Playing: {IsPreAudioPlaying()}\n" +
                      $"Rotation Enabled: {enableRotation}\n" +
                      $"Rotation Speed: {rotationSpeed}\n" +
                      $"Present Events Enabled: {enablePresentEvents}\n" +
                      $"Detailed Logs: {enableDetailedLogs}\n" +
                      $"Delay Before Hiding: {delayBeforeHiding}s\n" +
                      $"Collectible Value: {collectibleValue}\n" +
                      $"Expected Value by Size: {GetPresentValueBySize()}\n" +
                      $"Will Disable GameObject After Collection: {WillDisableGameObjectAfterCollection()}\n" +
                      $"Will Hide Mesh Immediately: {WillHideMeshImmediately()}\n" +
                      $"🍭 Sweet Plaza Settings:\n" +
                      $"   - Is Sweet Plaza Present: {isSweetPlazaPresent}\n" +
                      $"   - Object To Enable: {(sweetPlazaObjectToEnable != null ? sweetPlazaObjectToEnable.name : "NULL")}\n" +
                      $"   - Object Current State: {(sweetPlazaObjectToEnable != null ? (sweetPlazaObjectToEnable.activeInHierarchy ? "ACTIVE" : "INACTIVE") : "N/A")}\n" +
                      $"   - Sweet Plaza Message: {(string.IsNullOrEmpty(sweetPlazaMessage) ? "EMPTY" : sweetPlazaMessage)}\n" +
                      $"🎮 Playground Arena Settings:\n" +
                      $"   - Is Playground Arena Present: {isPlaygroundArenaPresent}\n" +
                      $"   - Object To Enable: {(playgroundArenaObjectToEnable != null ? playgroundArenaObjectToEnable.name : "NULL")}\n" +
                      $"   - Object Current State: {(playgroundArenaObjectToEnable != null ? (playgroundArenaObjectToEnable.activeInHierarchy ? "ACTIVE" : "INACTIVE") : "N/A")}\n" +
                      $"   - Playground Arena Message: {(string.IsNullOrEmpty(playgroundArenaMessage) ? "EMPTY" : playgroundArenaMessage)}\n" +
                      $"Special Present Type: {GetSpecialPresentType()}\n" +
                      $"🎉 Collection Completion:\n" +
                      $"   - Sweet Plaza Collected: {sweetPlazaCollectedInScene}\n" +
                      $"   - Playground Arena Collected: {playgroundArenaCollectedInScene}\n" +
                      $"   - Collection Complete: {IsCollectionComplete()}\n" +
                      $"   - Completion Object: {(globalCompletionObject != null ? globalCompletionObject.name : "NULL")}\n" +
                      $"   - Completion Message: {(string.IsNullOrEmpty(globalCompletionMessage) ? "EMPTY" : globalCompletionMessage)}";

        Debug.Log(state);
    }

    [ContextMenu("📊 Debug Collection Status")]
    public void DebugCollectionStatus()
    {
        string status = $"=== STATO COLLEZIONE GLOBALE ===\n" +
                       $"Sweet Plaza Collected: {sweetPlazaCollectedInScene}\n" +
                       $"Playground Arena Collected: {playgroundArenaCollectedInScene}\n" +
                       $"Collection Complete: {IsCollectionComplete()}\n" +
                       $"Global Completion Object: {(globalCompletionObject != null ? globalCompletionObject.name : "NULL")}\n" +
                       $"Global Completion Object Active: {(globalCompletionObject != null ? globalCompletionObject.activeInHierarchy.ToString() : "N/A")}\n" +
                       $"Global Completion Message: {(string.IsNullOrEmpty(globalCompletionMessage) ? "EMPTY" : globalCompletionMessage)}\n" +
                       $"This Present Completion Object: {(completionObjectToEnable != null ? completionObjectToEnable.name : "NULL")}";
        
        Debug.Log(status);
    }

    [ContextMenu("📊 Debug Sweet Plaza State")]
    public void DebugSweetPlazaState()
    {
        string state = $"=== STATO SWEET PLAZA per {collectibleName} ===\n" +
                      $"Is Sweet Plaza Present: {isSweetPlazaPresent}\n" +
                      $"Object To Enable: {(sweetPlazaObjectToEnable != null ? sweetPlazaObjectToEnable.name : "NULL")}\n" +
                      $"Object Current State: {(sweetPlazaObjectToEnable != null ? (sweetPlazaObjectToEnable.activeInHierarchy ? "ACTIVE" : "INACTIVE") : "N/A")}\n" +
                      $"Sweet Plaza Message: {(string.IsNullOrEmpty(sweetPlazaMessage) ? "EMPTY" : sweetPlazaMessage)}\n" +
                      $"Setup Valid: {(isSweetPlazaPresent ? ValidateSweetPlazaSetup().ToString() : "N/A")}";
        
        Debug.Log(state);
    }

    [ContextMenu("📊 Debug Playground Arena State")]
    public void DebugPlaygroundArenaState()
    {
        string state = $"=== STATO PLAYGROUND ARENA per {collectibleName} ===\n" +
                      $"Is Playground Arena Present: {isPlaygroundArenaPresent}\n" +
                      $"Object To Enable: {(playgroundArenaObjectToEnable != null ? playgroundArenaObjectToEnable.name : "NULL")}\n" +
                      $"Object Current State: {(playgroundArenaObjectToEnable != null ? (playgroundArenaObjectToEnable.activeInHierarchy ? "ACTIVE" : "INACTIVE") : "N/A")}\n" +
                      $"Playground Arena Message: {(string.IsNullOrEmpty(playgroundArenaMessage) ? "EMPTY" : playgroundArenaMessage)}\n" +
                      $"Setup Valid: {(isPlaygroundArenaPresent ? ValidatePlaygroundArenaSetup().ToString() : "N/A")}";
        
        Debug.Log(state);
    }

    [ContextMenu("📐 Cycle Present Size")]
    public void DebugCyclePresentSize()
    {
        PresentSize newSize = presentSize switch
        {
            PresentSize.Small => PresentSize.Medium,
            PresentSize.Medium => PresentSize.Large,
            PresentSize.Large => PresentSize.Small,
            _ => PresentSize.Medium
        };
        
        SetPresentSize(newSize);
        Debug.Log($"[Presents] {collectibleName} size cambiata a: {newSize} (Value: {GetPresentValueBySize()})");
    }

    [ContextMenu("🔊 Test Present Audio")]
    public void DebugTestPresentAudio()
    {
        Debug.Log($"🔊 === TEST AUDIO PRESENT {collectibleName} ===");
        
        // Validazione AudioSource
        if (GetPreAudioSource() == null)
        {
            Debug.LogError($"❌ Pre AudioSource NON assegnato per {collectibleName}! Assegnare nell'Inspector.");
            return;
        }
        
        if (GetPostAudioSource() == null)
        {
            Debug.LogError($"❌ Post AudioSource NON assegnato per {collectibleName}! Assegnare nell'Inspector.");
            return;
        }
        
        // Test pre-audio
        var preSource = GetPreAudioSource();
        if (preSource.clip != null)
        {
            Debug.Log($"🎵 Pre-Audio Test: Source={preSource.name}, Clip={preSource.clip.name}, Loop={preSource.loop}");
            
            if (!IsPreAudioPlaying() && IsPreAudioLoopEnabled())
            {
                Debug.Log($"🔄 Avvio pre-audio manualmente...");
                DebugForcePlayPreAudio();
            }
            else
            {
                Debug.Log($"ℹ️ Pre-audio status: Playing={IsPreAudioPlaying()}, Loop={IsPreAudioLoopEnabled()}");
            }
        }
        else
        {
            Debug.LogError($"❌ Pre-audio clip NON assegnato su AudioSource {preSource.name}!");
        }
        
        // Test post-audio
        var postSource = GetPostAudioSource();
        if (postSource.clip != null)
        {
            Debug.Log($"🎵 Post-Audio Test: Source={postSource.name}, Clip={postSource.clip.name}");
            Debug.Log($"🔄 Test post-audio...");
            DebugForcePlayPostAudio();
        }
        else
        {
            Debug.LogError($"❌ Post-audio clip NON assegnato su AudioSource {postSource.name}!");
        }
        
        // Test distanza dalla classe base
        DebugTestAudioDistance();
    }

    [ContextMenu("📋 Show Audio Setup Instructions")]
    new public void DebugShowAudioInstructions()
    {
        Debug.Log($"📋 === SETUP AUDIO per PRESENT {collectibleName} ===\n" +
                  $"🎯 PRINCIPIO: Configura TUTTO direttamente sugli AudioSource!\n\n" +
                  $"🔧 SETUP CONSIGLIATO:\n" +
                  $"1. Crea Audio_Container sotto il prefab Present\n" +
                  $"2. Crea PreAudio_Source + aggiungi AudioSource component\n" +
                  $"3. Crea PostAudio_Source + aggiungi AudioSource component\n" +
                  $"4. CONFIGURA COMPLETAMENTE ogni AudioSource nell'Inspector:\n" +
                  $"   ✅ Audio Clip (NECESSARIO!)\n" +
                  $"   ✅ Volume: PreAudio=0.8-0.9, PostAudio=1.0\n" +
                  $"   ✅ Spatial Blend: 1.0 per 3D\n" +
                  $"   ✅ Min Distance: 0.5\n" +
                  $"   ✅ Max Distance: 25-30\n" +
                  $"   ✅ Rolloff Mode: Linear\n" +
                  $"   ✅ Loop: PreAudio=TRUE, PostAudio=FALSE\n" +
                  $"   ✅ Play On Awake: FALSE (sempre!)\n" +
                  $"5. Assegna i due AudioSource nei campi Inspector del Collectibles\n" +
                  $"6. Imposta 'Enable Pre Audio Loop' = TRUE per Present\n" +
                  $"7. Test con '🔊 Test Present Audio'\n\n" +
                  $"⚠️ IMPORTANTE per PRESENT:\n" +
                  $"- Pre-audio DEVE essere in loop (suona continuamente)\n" +
                  $"- Post-audio NON deve essere in loop (suona solo alla raccolta)\n" +
                  $"- Present NON si disattiva mai (solo nasconde mesh)\n" +
                  $"- Usa solo trigger per la raccolta (non click)\n\n" +
                  $"🍭 SWEET PLAZA SPECIFICO:\n" +
                  $"- Abilita 'Is Sweet Plaza Present' nell'Inspector\n" +
                  $"- Assegna l'oggetto da attivare in 'Sweet Plaza Object To Enable'\n" +
                  $"- Personalizza il messaggio se necessario\n" +
                  $"- Test con '🧪 Test Sweet Plaza Toggle'\n\n" +
                  $"🎮 PLAYGROUND ARENA SPECIFICO:\n" +
                  $"- Abilita 'Is Playground Arena Present' nell'Inspector\n" +
                  $"- Assegna l'oggetto da attivare in 'Playground Arena Object To Enable'\n" +
                  $"- Personalizza il messaggio se necessario\n" +
                  $"- Test con '🧪 Test Playground Arena Toggle'\n\n" +
                  $"🎉 COLLECTION COMPLETION SYSTEM:\n" +
                  $"- Su UNO dei Present: assegna 'Completion Object To Enable'\n" +
                  $"- Questo oggetto si attiverà quando ENTRAMBI i tipi sono raccolti\n" +
                  $"- Personalizza il messaggio di completamento\n" +
                  $"- Test con '🎉 Test Collection Completion'\n" +
                  $"- Reset con '🔄 Reset Collection Tracking'\n\n" +
                  $"⚠️ NOTA: Sweet Plaza e Playground Arena sono MUTUAMENTE ESCLUSIVI!\n" +
                  $"💡 SUGGERIMENTO: Il sistema traccia automaticamente la collezione globale!");
    }
}