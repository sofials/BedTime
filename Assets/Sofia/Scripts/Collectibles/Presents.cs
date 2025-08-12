using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Classe Present COMPLETAMENTE COMPATIBILE con la nuova classe base Collectibles.
/// Sistema audio PURO: usa solo AudioSource configurati direttamente nell'Inspector.
/// NON modifica mai parametri degli AudioSource (volume, distanze, 3D) - rispetta configurazione Inspector.
/// NON disattiva mai l'intero GameObject - solo nasconde mesh e avvia effetti.
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
        
        LogDebug($"Present Awake - inizio configurazione per {gameObject.name}");
        
        // FASE 3: Chiama il base Awake (gestisce auto-detection, configurazioni default e audio)
        base.Awake();
        
        // FASE 4: Configurazioni specifiche Present DOPO il setup base
        ConfigurePresentDefaults();
        
        LogDebug($"Present Awake completato per {collectibleName}");
    }

    /// <summary>
    /// Configurazioni default specifiche per i Present
    /// NON tocca le impostazioni audio che sono gestite completamente dalla classe base
    /// </summary>
    protected virtual void ConfigurePresentDefaults()
    {
        LogDebug($"Configurazione defaults per Present size: {presentSize}");
        
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
        
        LogDebug($"Present {collectibleName} configurato come {presentSize} (Value: {collectibleValue}, Loop: {enablePreAudioLoop})");
    }

    /// <summary>
    /// Override del metodo di raccolta con logging specifico Present
    /// </summary>
    public override void CollectItem()
    {
        LogDebug($"=== INIZIO RACCOLTA PRESENT {collectibleName} ===");
        
        // Verifica componenti prima della raccolta (solo se debug attivo)
        if (enableDetailedLogs)
        {
            ValidatePresentComponentsQuick();
        }
        
        // Chiama il base CollectItem che gestisce tutto (audio, effetti, mesh, etc.)
        base.CollectItem();
        
        LogDebug($"=== FINE RACCOLTA PRESENT {collectibleName} ===");
    }

    /// <summary>
    /// Hook specifico Present per eventi post-raccolta
    /// </summary>
    protected override void OnItemCollected()
    {
        LogDebug($"Present {collectibleName} raccolto con successo!");
        
        // Log stato componenti (solo se debug attivo)
        if (enableDetailedLogs)
        {
            LogCollectionState();
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

    /// <summary>
    /// Log dello stato dopo la raccolta (solo per debug)
    /// </summary>
    private void LogCollectionState()
    {
        LogDebug($"Stato Componenti Post-Raccolta:");
        LogDebug($"  - Mesh Container Active: {(meshContainer != null ? meshContainer.gameObject.activeInHierarchy.ToString() : "N/A")}");
        LogDebug($"  - Effects Container Active: {(effectsContainer != null ? effectsContainer.gameObject.activeInHierarchy.ToString() : "N/A")}");
        LogDebug($"  - Pre Effects Playing: {(preCollectionEffects != null ? CountPlayingEffects(preCollectionEffects).ToString() : "N/A")}");
        LogDebug($"  - Post Effects Playing: {(postCollectionEffects != null ? CountPlayingEffects(postCollectionEffects).ToString() : "N/A")}");
        LogDebug($"  - Collider Enabled: {(mainCollider != null ? mainCollider.enabled.ToString() : "N/A")}");
        LogDebug($"  - GameObject Active: {gameObject.activeInHierarchy}");
        LogDebug($"  - Pre Audio Playing: {IsPreAudioPlaying()}");
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

    [ContextMenu("✅ Validate Present Components")]
    public void DebugValidatePresentComponents() => ValidatePresentComponents();

    [ContextMenu("🔄 Test Present Cycle")]
    public void DebugTestPresentCycle() => TestPresentCycle();

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
                      $"Will Hide Mesh Immediately: {WillHideMeshImmediately()}";

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
                  $"- Usa solo trigger per la raccolta (non click)");
    }
}