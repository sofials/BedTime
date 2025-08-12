using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Classe per le Memory che eredita da Collectibles.
/// COMPLETAMENTE COMPATIBILE con la nuova classe base con sistema audio puro.
/// Sistema audio: usa SOLO AudioSource configurati direttamente nell'Inspector.
/// NON modifica mai parametri degli AudioSource - rispetta la configurazione originale.
/// Gestisce immagini 2D billboard, effetti, categorizzazione e glow.
/// </summary>
public class Memories : Collectibles
{
    [Header("Memory Specific Settings")]
    [SerializeField] private MemoryCategory memoryCategory = MemoryCategory.Story;
    [SerializeField] private Color memoryGlowColor = Color.cyan;
    
    [Header("Memory Billboard System - Manual Assignment")]
    [SerializeField] private Transform billboardImage; // Immagine 2D da orientare verso camera
    [SerializeField] private Camera targetCamera; // Camera verso cui orientarsi (se null usa Camera.main)
    [SerializeField] private bool enableBillboard = true; // Abilita orientamento billboard
    [SerializeField] private bool lockYAxis = true; // Blocca rotazione asse Y per billboard
    
    [Header("Memory Events")]
    public UnityEvent<Memories> OnMemoryCollected;
    
    [Header("Memory Debug")]
    [SerializeField] private bool enableDetailedLogs = true; // Per debug dettagliato
    
    // Enum per categorie di memorie
    public enum MemoryCategory
    {
        Story,      // Memorie narrative principali
        Lore,       // Conoscenza del mondo/lore
        Secret,     // Memorie segrete/nascoste
        Character   // Memorie dei personaggi
    }
    
    // Cache per billboard
    private Transform cachedCameraTransform;
    
    protected override void Awake()
    {
        // FASE 1: Imposta il tipo come Memory PRIMA del base Awake
        collectibleType = CollectibleType.Memory;
        
        // FASE 2: Imposta default specifici per le Memory
        if (collectibleValue == 1) // Se è ancora il valore di default
        {
            collectibleValue = GetDefaultValueForCategory();
        }
        
        // FASE 3: Configurazioni animazione specifiche Memory
        enableFloating = true;
        floatSpeed = 1.5f;
        floatStrength = 0.4f;
        
        LogDebug($"Memory Awake - inizio configurazione per {gameObject.name}");
        
        // FASE 4: Chiama il base Awake che configurerà tutto tramite auto-detection
        // La classe base imposterà automaticamente enablePreAudioLoop=false per Memory
        base.Awake();
        
        // FASE 5: Setup specifico delle Memory
        SetupBillboardSystem();
        ValidateMemoryConfiguration();
        
        LogDebug($"Memory Awake completato per {collectibleName} - Categoria: {memoryCategory}");
    }
    
    protected override void Start()
    {
        base.Start();
        
        LogDebug($"Memory '{collectibleName}' inizializzata come {memoryCategory} Memory");
    }
    
    protected override void Update()
    {
        base.Update();
        
        if (!isCollected)
        {
            // Sistema Billboard - orienta l'immagine verso la camera
            if (enableBillboard && billboardImage != null)
            {
                UpdateBillboardRotation();
            }
        }
    }
    
    /// <summary>
    /// Valida che tutti i componenti necessari siano assegnati
    /// </summary>
    private void ValidateMemoryConfiguration()
    {
        LogDebug($"=== VALIDAZIONE CONFIGURAZIONE MEMORY {collectibleName} ===");
        
        // Valida componenti dalla classe base
        if (meshContainer == null && (meshRenderers == null || meshRenderers.Length == 0))
        {
            Debug.LogWarning($"[Memories] ⚠️ Nessun Mesh Container o Mesh Renderers trovato per {collectibleName}! " +
                           "Verifica la gerarchia o assegna manualmente nell'Inspector.");
        }
        else
        {
            LogDebug($"✅ Mesh system configurato correttamente");
        }
        
        // Valida collider principale
        if (mainCollider == null)
        {
            Debug.LogWarning($"[Memories] ⚠️ Nessun Main Collider trovato per {collectibleName}! " +
                           "Assicurati che ci sia un Collider sul GameObject principale.");
        }
        else
        {
            LogDebug($"✅ Main Collider trovato: {mainCollider.GetType().Name}");
        }
        
        // Valida billboard image
        if (billboardImage == null)
        {
            Debug.LogWarning($"[Memories] ⚠️ Nessun Billboard Image assegnato per {collectibleName}! " +
                           "Assegna il Transform dell'immagine 2D nell'Inspector.");
        }
        else
        {
            LogDebug($"✅ Billboard Image assegnato: {billboardImage.name}");
        }
        
        // Valida effetti dalla classe base
        if (preCollectionEffects == null || preCollectionEffects.Length == 0)
        {
            Debug.LogWarning($"[Memories] ⚠️ Nessun Pre-Collection Effect trovato per {collectibleName}! " +
                           "Verifica la gerarchia Effects_Container o assegna manualmente.");
        }
        else
        {
            LogDebug($"✅ Pre-Collection Effects: {preCollectionEffects.Length} effetti");
        }
        
        if (postCollectionEffects == null || postCollectionEffects.Length == 0)
        {
            Debug.LogWarning($"[Memories] ⚠️ Nessun Post-Collection Effect trovato per {collectibleName}! " +
                           "Verifica la gerarchia Effects_Container o assegna manualmente.");
        }
        else
        {
            LogDebug($"✅ Post-Collection Effects: {postCollectionEffects.Length} effetti");
        }
        
        // ⭐ VALIDAZIONE AUDIO PURA - SOLO AudioSource ⭐
        if (GetPreAudioSource() == null)
        {
            Debug.LogWarning($"[Memories] ⚠️ Nessun Pre AudioSource assegnato per {collectibleName}! " +
                           "Assegna l'AudioSource pre-raccolta nell'Inspector.");
        }
        else
        {
            if (GetPreAudioSource().clip == null)
            {
                Debug.LogError($"[Memories] ❌ Pre AudioSource '{GetPreAudioSource().name}' non ha AudioClip assegnato! " +
                             "Assegna il clip direttamente sull'AudioSource nell'Inspector.");
            }
            else
            {
                LogDebug($"✅ Pre AudioSource configurato: {GetPreAudioSource().name} con clip {GetPreAudioSource().clip.name}");
                LogDebug($"   - Volume: {GetPreAudioSource().volume} (configurato sull'AudioSource)");
                LogDebug($"   - 3D: {GetPreAudioSource().spatialBlend} (configurato sull'AudioSource)");
                LogDebug($"   - Max Distance: {GetPreAudioSource().maxDistance} (configurato sull'AudioSource)");
                LogDebug($"   - Loop: {GetPreAudioSource().loop} (dovrebbe essere FALSE per Memory)");
            }
        }
        
        if (GetPostAudioSource() == null)
        {
            Debug.LogWarning($"[Memories] ⚠️ Nessun Post AudioSource assegnato per {collectibleName}! " +
                           "Assegna l'AudioSource post-raccolta nell'Inspector.");
        }
        else
        {
            if (GetPostAudioSource().clip == null)
            {
                Debug.LogError($"[Memories] ❌ Post AudioSource '{GetPostAudioSource().name}' non ha AudioClip assegnato! " +
                             "Assegna il clip direttamente sull'AudioSource nell'Inspector.");
            }
            else
            {
                LogDebug($"✅ Post AudioSource configurato: {GetPostAudioSource().name} con clip {GetPostAudioSource().clip.name}");
                LogDebug($"   - Volume: {GetPostAudioSource().volume} (configurato sull'AudioSource)");
                LogDebug($"   - 3D: {GetPostAudioSource().spatialBlend} (configurato sull'AudioSource)");
                LogDebug($"   - Max Distance: {GetPostAudioSource().maxDistance} (configurato sull'AudioSource)");
                LogDebug($"   - Loop: {GetPostAudioSource().loop} (dovrebbe essere FALSE)");
            }
        }
        
        LogDebug($"=== FINE VALIDAZIONE CONFIGURAZIONE ===");
    }
    
    /// <summary>
    /// Setup del sistema billboard per orientare l'immagine verso la camera
    /// </summary>
    private void SetupBillboardSystem()
    {
        LogDebug($"=== SETUP BILLBOARD SYSTEM per {collectibleName} ===");
        
        // Auto-trova la camera se non assegnata
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
            if (targetCamera == null)
            {
                targetCamera = FindFirstObjectByType<Camera>();
            }
        }
        
        if (targetCamera != null)
        {
            cachedCameraTransform = targetCamera.transform;
            LogDebug($"✅ Camera target trovata: {targetCamera.name}");
        }
        else
        {
            Debug.LogWarning($"[Memories] ❌ Nessuna camera trovata per billboard di {collectibleName}!");
        }
        
        LogDebug($"=== FINE SETUP BILLBOARD SYSTEM ===");
    }
    
    /// <summary>
    /// Aggiorna la rotazione del billboard per guardare sempre la camera
    /// </summary>
    private void UpdateBillboardRotation()
    {
        if (cachedCameraTransform == null || billboardImage == null) return;
        
        // Calcola direzione verso la camera
        Vector3 directionToCamera = cachedCameraTransform.position - billboardImage.position;
        
        if (lockYAxis)
        {
            // Blocca l'asse Y per evitare inclinazioni strane
            directionToCamera.y = 0;
        }
        
        if (directionToCamera != Vector3.zero)
        {
            // Orienta verso la camera
            Quaternion targetRotation = Quaternion.LookRotation(directionToCamera);
            billboardImage.rotation = targetRotation;
        }
    }
    
    private int GetDefaultValueForCategory()
    {
        return memoryCategory switch
        {
            MemoryCategory.Story => 1,
            MemoryCategory.Lore => 5,
            MemoryCategory.Secret => 10,
            MemoryCategory.Character => 3,
            _ => 1
        };
    }
    
    // ========== OVERRIDE METODI BASE ==========
    
    /// <summary>
    /// Override del metodo virtuale chiamato quando l'item viene raccolto
    /// </summary>
    protected override void OnItemCollected()
{
    // ⭐ SINCRONIZZAZIONE IMMEDIATA: Ferma gli effetti pre-raccolta SUBITO
    // Prima di qualsiasi altra operazione
    if (preCollectionEffects != null && preCollectionEffects.Length > 0)
    {
        foreach (var effect in preCollectionEffects)
        {
            if (effect != null)
            {
                // Stop immediato dell'emissione
                var emission = effect.emission;
                emission.enabled = false;
                
                // Stop completo del sistema particellare
                effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                
                LogDebug($"✅ Pre-effect {effect.name} fermato immediatamente");
            }
        }
    }
    
    // Nasconde il billboard quando viene raccolto (sincronizzato con mesh)
    if (billboardImage != null)
    {
        billboardImage.gameObject.SetActive(false);
        LogDebug($"✅ Billboard nascosto per {collectibleName}");
    }
    
    // ⭐ OPZIONE AGGIUNTIVA: Forza disattivazione container effetti se necessario
    if (effectsContainer != null)
    {
        // Opzione 1: Disattiva solo i pre-effects
        var preEffectsContainer = effectsContainer.Find("PreEffects_Container");
        if (preEffectsContainer != null)
        {
            preEffectsContainer.gameObject.SetActive(false);
            LogDebug($"✅ Pre-effects container disattivato immediatamente");
        }
        
        // Opzione 2: Se vuoi essere ancora più aggressivo, disattiva tutto il container
        // (sconsigliato se hai post-effects che devono rimanere visibili)
        // effectsContainer.gameObject.SetActive(false);
    }
    
    // Evento specifico Memory
    try
    {
        OnMemoryCollected?.Invoke(this);
        LogDebug($"Evento OnMemoryCollected invocato per {collectibleName}");
    }
    catch (System.Exception e)
    {
        Debug.LogError($"[Memories] Errore nell'invocare OnMemoryCollected per {collectibleName}: {e.Message}");
    }
}
    
    // ========== OVERRIDE RESET ==========
    
    public override void ResetCollected()
    {
        // Chiama il reset della classe base
        base.ResetCollected();
        
        // Reset specifico delle Memory
        
        // Riattiva billboard se presente
        if (billboardImage != null)
        {
            billboardImage.gameObject.SetActive(true);
            LogDebug($"✅ Billboard riattivato per {collectibleName}");
        }
        
        LogDebug($"Memory {collectibleName} resetata completamente");
    }
    
    // ========== GETTERS E SETTERS SPECIFICI ==========
    
    public MemoryCategory GetMemoryCategory() => memoryCategory;
    public Color GetMemoryGlowColor() => memoryGlowColor;
    public Transform GetBillboardImage() => billboardImage;
    public Camera GetTargetCamera() => targetCamera;
    public bool IsDetailedLogsEnabled() => enableDetailedLogs;
    
    public void SetMemoryCategory(MemoryCategory category)
    {
        memoryCategory = category;
        
        // Aggiorna il valore se è ancora quello di default
        if (collectibleValue == GetDefaultValueForCategory())
        {
            collectibleValue = GetDefaultValueForCategory();
        }
        
        LogDebug($"Memory category cambiata a: {category}");
    }
    
    public void SetMemoryGlowColor(Color color)
    {
        memoryGlowColor = color;
        
        // Applica il colore agli effetti se presenti
        if (preCollectionEffects != null)
        {
            foreach (var effect in preCollectionEffects)
            {
                if (effect != null)
                {
                    var main = effect.main;
                    main.startColor = color;
                }
            }
        }
        
        if (postCollectionEffects != null)
        {
            foreach (var effect in postCollectionEffects)
            {
                if (effect != null)
                {
                    var main = effect.main;
                    main.startColor = color;
                }
            }
        }
        
        LogDebug($"Glow color cambiato a: {color}");
    }
    
    public void SetBillboardImage(Transform billboard)
    {
        billboardImage = billboard;
        LogDebug($"Billboard image assegnato: {billboard?.name ?? "NULL"}");
    }
    
    public void SetTargetCamera(Camera camera)
    {
        targetCamera = camera;
        if (camera != null)
        {
            cachedCameraTransform = camera.transform;
            LogDebug($"Target camera impostata: {camera.name}");
        }
    }
    
    public void SetBillboardSettings(bool enableBill, bool lockY)
    {
        enableBillboard = enableBill;
        lockYAxis = lockY;
        LogDebug($"Billboard settings: Enabled={enableBill}, LockY={lockY}");
    }
    
    public void SetDetailedLogs(bool enabled)
    {
        enableDetailedLogs = enabled;
        LogDebug($"Detailed logs {(enabled ? "abilitati" : "disabilitati")}");
    }
    
    // ========== CONFIGURAZIONI PRESET (AGGIORNATE) ==========
    
    public void ConfigureAsStoryMemory()
    {
        SetMemoryCategory(MemoryCategory.Story);
        collectibleName = "Story Fragment";
        collectibleValue = 1;
        floatSpeed = 2f;
        floatStrength = 0.6f;
        SetMemoryGlowColor(Color.cyan);
        displayMessage = "Storia recuperata!";
        delayBeforeHiding = 3f;
        
        LogDebug("Configurata come memory narrativa");
    }
    
    public void ConfigureAsLoreMemory()
    {
        SetMemoryCategory(MemoryCategory.Lore);
        collectibleName = "Ancient Knowledge";
        collectibleValue = 5;
        floatSpeed = 3f;
        floatStrength = 0.8f;
        SetMemoryGlowColor(Color.magenta);
        displayMessage = "Antica conoscenza acquisita!";
        delayBeforeHiding = 4f;
        
        LogDebug("Configurata come memory lore");
    }
    
    public void ConfigureAsSecretMemory()
    {
        SetMemoryCategory(MemoryCategory.Secret);
        collectibleName = "Hidden Truth";
        collectibleValue = 10;
        floatSpeed = 4f;
        floatStrength = 1.0f;
        SetMemoryGlowColor(Color.yellow);
        displayMessage = "Verità nascosta rivelata!";
        delayBeforeHiding = 5f;
        
        LogDebug("Configurata come memory segreta");
    }
    
    public void ConfigureAsCharacterMemory(string characterName = "Unknown")
    {
        SetMemoryCategory(MemoryCategory.Character);
        collectibleName = $"{characterName} Memory";
        collectibleValue = 3;
        floatSpeed = 2.5f;
        floatStrength = 0.7f;
        SetMemoryGlowColor(Color.green);
        displayMessage = $"Ricordo di {characterName} recuperato!";
        delayBeforeHiding = 3.5f;
        
        LogDebug($"Configurata come memory di {characterName}");
    }
    
    // ========== METODI PER COMPATIBILITÀ CON VECCHIO CODICE ==========
    
    public void CollectMemory() => CollectItem();
    public string GetMemoryName() => GetName();
    public int GetMemoryValue() => GetCollectibleValue();
    public void SetMemoryName(string name) => collectibleName = name;
    public void SetMemoryValue(int value) => collectibleValue = value;
    public void ResetMemory() => ResetCollected();
    public void TriggerMemoryCollection() => CollectItem();
    
    // ========== DEBUG METHODS AGGIORNATI ==========
    
    private void LogDebug(string message)
    {
        if (enableDetailedLogs)
            Debug.Log($"[Memories] {message}");
    }
    
    [ContextMenu("🔄 Test Memory Collection")]
    public void TestMemoryCollection() => ForceCollect();
    
    [ContextMenu("🔍 Validate Configuration")]
    public void ManualValidateConfiguration() => ValidateMemoryConfiguration();
    
    [ContextMenu("🔄 Test Billboard Rotation")]
    public void TestBillboardRotation()
    {
        if (billboardImage != null && enableBillboard)
        {
            UpdateBillboardRotation();
            Debug.Log($"[Memories] Billboard aggiornato per {collectibleName}");
        }
        else
        {
            Debug.LogWarning($"[Memories] Billboard non configurato per {collectibleName}");
        }
    }
    
    [ContextMenu("📖 Configure as Story Memory")]
    public void DebugConfigureStory() => ConfigureAsStoryMemory();
    
    [ContextMenu("🔮 Configure as Lore Memory")]
    public void DebugConfigureLore() => ConfigureAsLoreMemory();
    
    [ContextMenu("🤫 Configure as Secret Memory")]
    public void DebugConfigureSecret() => ConfigureAsSecretMemory();
    
    [ContextMenu("👤 Configure as Character Memory")]
    public void DebugConfigureCharacter() => ConfigureAsCharacterMemory("TestCharacter");
    
    [ContextMenu("📝 Toggle Detailed Logs")]
    public void DebugToggleDetailedLogs()
    {
        SetDetailedLogs(!enableDetailedLogs);
        Debug.Log($"[Memories] Detailed Logs per {collectibleName}: {(enableDetailedLogs ? "ABILITATI" : "DISABILITATI")}");
    }
    
    [ContextMenu("📱 Toggle Billboard")]
    public void DebugToggleBillboard()
    {
        enableBillboard = !enableBillboard;
        Debug.Log($"[Memories] Billboard per {collectibleName}: {(enableBillboard ? "ABILITATO" : "DISABILITATO")}");
    }
    
    [ContextMenu("🔊 Test Memory Audio")]
    public void DebugTestMemoryAudio()
    {
        Debug.Log($"🔊 === TEST AUDIO MEMORY {collectibleName} ===");
        
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
        
        // Test pre-audio (Memory non hanno loop di default)
        var preSource = GetPreAudioSource();
        if (preSource.clip != null)
        {
            Debug.Log($"🎵 Pre-Audio Test: Source={preSource.name}, Clip={preSource.clip.name}, Loop={preSource.loop}");
            
            if (!IsPreAudioPlaying())
            {
                Debug.Log($"🔄 Test pre-audio manualmente (Memory non hanno loop di default)...");
                DebugForcePlayPreAudio();
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
    
    [ContextMenu("📊 Debug Memory State")]
    public void DebugMemoryState()
    {
        string state = $"=== STATO MEMORY {collectibleName} ===\n" +
                      $"Memory Category: {memoryCategory}\n" +
                      $"Memory Glow Color: {memoryGlowColor}\n" +
                      $"Is Collected: {IsCollected()}\n" +
                      $"GameObject Active: {gameObject.activeInHierarchy}\n" +
                      $"Billboard Image: {(billboardImage != null ? billboardImage.name + " (Active: " + billboardImage.gameObject.activeInHierarchy + ")" : "NULL")}\n" +
                      $"Enable Billboard: {enableBillboard}\n" +
                      $"Lock Y Axis: {lockYAxis}\n" +
                      $"Target Camera: {(targetCamera != null ? targetCamera.name : "NULL")}\n" +
                      $"Mesh Container: {(meshContainer != null ? meshContainer.name + " (Active: " + meshContainer.gameObject.activeInHierarchy + ")" : "NULL")}\n" +
                      $"Effects Container: {(effectsContainer != null ? effectsContainer.name + " (Active: " + effectsContainer.gameObject.activeInHierarchy + ")" : "NULL")}\n" +
                      $"Pre Effects: {(preCollectionEffects != null ? preCollectionEffects.Length.ToString() : "NULL")}\n" +
                      $"Post Effects: {(postCollectionEffects != null ? postCollectionEffects.Length.ToString() : "NULL")}\n" +
                      $"Main Collider: {(mainCollider != null ? mainCollider.GetType().Name + " (enabled: " + mainCollider.enabled + ")" : "NULL")}\n" +
                      $"🔊 Audio Settings:\n" +
                      $"   - Pre AudioSource: {(GetPreAudioSource() != null ? GetPreAudioSource().name : "NULL")}\n" +
                      $"   - Post AudioSource: {(GetPostAudioSource() != null ? GetPostAudioSource().name : "NULL")}\n" +
                      $"   - Pre Audio Clip: {(GetPreAudioSource()?.clip != null ? GetPreAudioSource().clip.name : "NULL")}\n" +
                      $"   - Post Audio Clip: {(GetPostAudioSource()?.clip != null ? GetPostAudioSource().clip.name : "NULL")}\n" +
                      $"   - Max Distance: {GetAudioMaxDistance()}\n" +
                      $"   - Pre Audio Loop Enabled: {IsPreAudioLoopEnabled()}\n" +
                      $"   - Pre Audio Playing: {IsPreAudioPlaying()}\n" +
                      $"Floating Enabled: {enableFloating}\n" +
                      $"Float Speed: {floatSpeed}\n" +
                      $"Float Strength: {floatStrength}\n" +
                      $"Rotation Enabled: {enableRotation}\n" +
                      $"Detailed Logs: {enableDetailedLogs}\n" +
                      $"Delay Before Hiding: {delayBeforeHiding}s\n" +
                      $"Collectible Value: {collectibleValue}\n" +
                      $"Expected Value by Category: {GetDefaultValueForCategory()}\n" +
                      $"Will Disable GameObject After Collection: {WillDisableGameObjectAfterCollection()}\n" +
                      $"Will Hide Mesh Immediately: {WillHideMeshImmediately()}";

        Debug.Log(state);
    }
    
    [ContextMenu("📐 Cycle Memory Category")]
    public void DebugCycleMemoryCategory()
    {
        MemoryCategory newCategory = memoryCategory switch
        {
            MemoryCategory.Story => MemoryCategory.Lore,
            MemoryCategory.Lore => MemoryCategory.Secret,
            MemoryCategory.Secret => MemoryCategory.Character,
            MemoryCategory.Character => MemoryCategory.Story,
            _ => MemoryCategory.Story
        };
        
        SetMemoryCategory(newCategory);
        Debug.Log($"[Memories] {collectibleName} category cambiata a: {newCategory} (Value: {GetDefaultValueForCategory()})");
    }
    
    [ContextMenu("📋 Show Audio Setup Instructions")]
    new public void DebugShowAudioInstructions()
    {
        Debug.Log($"📋 === SETUP AUDIO per MEMORY {collectibleName} ===\n" +
                  $"🎯 PRINCIPIO: Configura TUTTO direttamente sugli AudioSource!\n\n" +
                  $"🔧 SETUP CONSIGLIATO:\n" +
                  $"1. Crea Audio_Container sotto il prefab Memory\n" +
                  $"2. Crea PreAudio_Source + aggiungi AudioSource component\n" +
                  $"3. Crea PostAudio_Source + aggiungi AudioSource component\n" +
                  $"4. CONFIGURA COMPLETAMENTE ogni AudioSource nell'Inspector:\n" +
                  $"   ✅ Audio Clip (NECESSARIO!)\n" +
                  $"   ✅ Volume: PreAudio=0.7-0.8, PostAudio=0.9-1.0\n" +
                  $"   ✅ Spatial Blend: 1.0 per 3D\n" +
                  $"   ✅ Min Distance: 0.5\n" +
                  $"   ✅ Max Distance: 20-30 (secondo categoria)\n" +
                  $"   ✅ Rolloff Mode: Linear\n" +
                  $"   ✅ Loop: FALSE (Memory NON hanno loop)\n" +
                  $"   ✅ Play On Awake: FALSE (sempre!)\n" +
                  $"5. Assegna i due AudioSource nei campi Inspector del Collectibles\n" +
                  $"6. 'Enable Pre Audio Loop' sarà automaticamente FALSE per Memory\n" +
                  $"7. Test con '🔊 Test Memory Audio'\n\n" +
                  $"⚠️ IMPORTANTE per MEMORY:\n" +
                  $"- Pre-audio NON deve essere in loop (silenzioso finché non attivato)\n" +
                  $"- Post-audio NON deve essere in loop (suona solo alla raccolta)\n" +
                  $"- Memory si disattivano dopo la raccolta (non come Present)\n" +
                  $"- Usa trigger E/O click per la raccolta\n" +
                  $"- Distanze audio variano per categoria (Secret=più lontane)");
    }
}