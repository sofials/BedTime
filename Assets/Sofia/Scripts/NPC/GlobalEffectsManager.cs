using UnityEngine;
using System.Collections.Generic;

public class GlobalEffectsManager : MonoBehaviour
{
    [Header("Effetti CFXR Globali da Fermare")]
    [SerializeField] private List<CFXR_EffectController> globalCFXREffects = new List<CFXR_EffectController>();
    
    [Header("Audio Globali da Fermare")]
    [SerializeField] private List<AudioSource> globalAudioSources = new List<AudioSource>();
    
    [Header("Settings")]
    [SerializeField] private bool autoStartEffects = true; // Avvia automaticamente all'inizio
    [SerializeField] private float stopDelay = 0.1f; // Piccolo delay prima di fermare gli effetti
    
    [Header("Debug")]
    [SerializeField] private bool debugMode = true;
    
    private bool effectsStopped = false;
    private bool effectsStarted = false;

    void Start()
    {
        // Iscriviti all'evento del primo slowdown
        Npc_village.OnFirstSlowdownUsed += OnFirstSlowdownTriggered;
        
        // Avvia automaticamente gli effetti se richiesto
        if (autoStartEffects)
        {
            // Piccolo delay per assicurarsi che tutto sia inizializzato
            Invoke(nameof(StartAllEffects), 0.1f);
        }
        
        if (debugMode)
        {
            Debug.Log($"GlobalEffectsManager: Inizializzato - {globalCFXREffects.Count} effetti CFXR, {globalAudioSources.Count} audio. Auto-start: {autoStartEffects}");
        }
    }

    void OnDestroy()
    {
        // Disiscriviti dall'evento per evitare memory leaks
        Npc_village.OnFirstSlowdownUsed -= OnFirstSlowdownTriggered;
    }

    /// <summary>
    /// Chiamato quando viene rilevato il primo slowdown
    /// </summary>
    private void OnFirstSlowdownTriggered()
    {
        if (debugMode) Debug.Log("GlobalEffectsManager: Primo slowdown rilevato! Fermo effetti con delay...");
        
        // Aggiungi un piccolo delay per sincronizzazione con gli effetti degli NPC
        Invoke(nameof(StopGlobalEffects), stopDelay);
    }

    /// <summary>
    /// Avvia tutti gli effetti all'inizio della scena
    /// </summary>
    public void StartAllEffects()
    {
        if (effectsStarted)
        {
            if (debugMode) Debug.Log("GlobalEffectsManager: Effetti già avviati, ignoro.");
            return;
        }

        effectsStarted = true;
        effectsStopped = false; // Reset del flag di stop
        
        if (debugMode)
        {
            Debug.Log("GlobalEffectsManager: Avvio tutti gli effetti...");
        }

        // Avvia tutti gli effetti CFXR
        int cfxrStarted = 0;
        foreach (CFXR_EffectController cfxrEffect in globalCFXREffects)
        {
            if (cfxrEffect != null)
            {
                cfxrEffect.PlayEffect();
                cfxrStarted++;
                if (debugMode) Debug.Log($"GlobalEffectsManager: ✓ Effetto CFXR '{cfxrEffect.name}' avviato.");
            }
            else
            {
                Debug.LogWarning("GlobalEffectsManager: ⚠️ Un effetto CFXR nella lista è null!");
            }
        }

        // Avvia tutte le audio sources
        int audioStarted = 0;
        foreach (AudioSource audioSource in globalAudioSources)
        {
            if (audioSource != null && audioSource.clip != null)
            {
                audioSource.Play();
                audioStarted++;
                if (debugMode) Debug.Log($"GlobalEffectsManager: ✓ Audio '{audioSource.name}' avviato.");
            }
            else if (audioSource != null && audioSource.clip == null)
            {
                Debug.LogWarning($"GlobalEffectsManager: ⚠️ AudioSource '{audioSource.name}' non ha clip assegnata!");
            }
            else
            {
                Debug.LogWarning("GlobalEffectsManager: ⚠️ Un AudioSource nella lista è null!");
            }
        }

        if (debugMode)
        {
            Debug.Log($"GlobalEffectsManager: ✓ AVVIATI {cfxrStarted} effetti CFXR e {audioStarted} audio sources.");
        }
    }

    /// <summary>
    /// Ferma tutti gli effetti globali quando viene usato il primo slowdown
    /// </summary>
    public void StopGlobalEffects()
    {
        if (effectsStopped)
        {
            if (debugMode) Debug.Log("GlobalEffectsManager: Effetti già fermati, ignoro.");
            return;
        }

        effectsStopped = true;
        
        if (debugMode)
        {
            Debug.Log("GlobalEffectsManager: 🛑 PRIMO SLOWDOWN RILEVATO! Fermo effetti globali...");
        }

        // Ferma tutti gli effetti CFXR
        int cfxrStopped = 0;
        foreach (CFXR_EffectController cfxrEffect in globalCFXREffects)
        {
            if (cfxrEffect != null)
            {
                cfxrEffect.StopEffect();
                cfxrStopped++;
                if (debugMode) Debug.Log($"GlobalEffectsManager: 🛑 Effetto CFXR '{cfxrEffect.name}' fermato.");
            }
            else
            {
                Debug.LogWarning("GlobalEffectsManager: ⚠️ Un effetto CFXR nella lista è null!");
            }
        }

        // Ferma tutte le audio sources
        int audioStopped = 0;
        foreach (AudioSource audioSource in globalAudioSources)
        {
            if (audioSource != null)
            {
                audioSource.Stop();
                audioStopped++;
                if (debugMode) Debug.Log($"GlobalEffectsManager: 🛑 Audio '{audioSource.name}' fermato.");
            }
            else
            {
                Debug.LogWarning("GlobalEffectsManager: ⚠️ Un AudioSource nella lista è null!");
            }
        }

        if (debugMode)
        {
            Debug.Log($"GlobalEffectsManager: 🛑 FERMATI {cfxrStopped} effetti CFXR e {audioStopped} audio sources.");
        }
    }

    /// <summary>
    /// Riavvia tutti gli effetti (per testing o restart level)
    /// </summary>
    public void RestartAllEffects()
    {
        if (debugMode) Debug.Log("GlobalEffectsManager: 🔄 Riavvio tutti gli effetti...");
        
        effectsStopped = false;
        effectsStarted = false;
        
        StartAllEffects();
    }

    /// <summary>
    /// Metodo per testare manualmente il fermo degli effetti
    /// </summary>
    [ContextMenu("Test Stop Effects")]
    public void TestStopEffects()
    {
        StopGlobalEffects();
    }

    /// <summary>
    /// Metodo per testare manualmente il riavvio degli effetti
    /// </summary>
    [ContextMenu("Test Restart Effects")]
    public void TestRestartEffects()
    {
        RestartAllEffects();
    }

    /// <summary>
    /// Metodo per simulare il primo slowdown (per testing)
    /// </summary>
    [ContextMenu("Simulate First Slowdown")]
    public void SimulateFirstSlowdown()
    {
        OnFirstSlowdownTriggered();
    }

    /// <summary>
    /// Aggiungi un effetto CFXR alla lista (da codice se necessario)
    /// </summary>
    public void AddCFXREffect(CFXR_EffectController effect)
    {
        if (effect != null && !globalCFXREffects.Contains(effect))
        {
            globalCFXREffects.Add(effect);
            if (debugMode) Debug.Log($"GlobalEffectsManager: ➕ Aggiunto effetto CFXR '{effect.name}' alla lista.");
            
            // Se gli effetti sono già avviati, avvia anche questo
            if (effectsStarted && !effectsStopped)
            {
                effect.PlayEffect();
                if (debugMode) Debug.Log($"GlobalEffectsManager: ▶️ Effetto CFXR '{effect.name}' avviato immediatamente.");
            }
        }
    }

    /// <summary>
    /// Aggiungi un audio source alla lista (da codice se necessario)
    /// </summary>
    public void AddAudioSource(AudioSource audioSource)
    {
        if (audioSource != null && !globalAudioSources.Contains(audioSource))
        {
            globalAudioSources.Add(audioSource);
            if (debugMode) Debug.Log($"GlobalEffectsManager: ➕ Aggiunto audio '{audioSource.name}' alla lista.");
            
            // Se gli effetti sono già avviati, avvia anche questo
            if (effectsStarted && !effectsStopped && audioSource.clip != null)
            {
                audioSource.Play();
                if (debugMode) Debug.Log($"GlobalEffectsManager: ▶️ Audio '{audioSource.name}' avviato immediatamente.");
            }
        }
    }

    /// <summary>
    /// Rimuovi un effetto CFXR dalla lista
    /// </summary>
    public void RemoveCFXREffect(CFXR_EffectController effect)
    {
        if (globalCFXREffects.Contains(effect))
        {
            globalCFXREffects.Remove(effect);
            if (debugMode) Debug.Log($"GlobalEffectsManager: ➖ Rimosso effetto CFXR '{effect.name}' dalla lista.");
        }
    }

    /// <summary>
    /// Rimuovi un audio source dalla lista
    /// </summary>
    public void RemoveAudioSource(AudioSource audioSource)
    {
        if (globalAudioSources.Contains(audioSource))
        {
            globalAudioSources.Remove(audioSource);
            if (debugMode) Debug.Log($"GlobalEffectsManager: ➖ Rimosso audio '{audioSource.name}' dalla lista.");
        }
    }

    /// <summary>
    /// Controlla se tutti gli effetti necessari sono assegnati
    /// </summary>
    [ContextMenu("Validate Setup")]
    public void ValidateSetup()
    {
        Debug.Log("=== GlobalEffectsManager Setup Validation ===");
        
        Debug.Log($"CFXR Effects: {globalCFXREffects.Count} totali");
        for (int i = 0; i < globalCFXREffects.Count; i++)
        {
            if (globalCFXREffects[i] == null)
            {
                Debug.LogError($"⚠️ CFXR Effect {i} è NULL!");
            }
            else
            {
                Debug.Log($"✓ CFXR Effect {i}: {globalCFXREffects[i].name}");
            }
        }
        
        Debug.Log($"Audio Sources: {globalAudioSources.Count} totali");
        for (int i = 0; i < globalAudioSources.Count; i++)
        {
            if (globalAudioSources[i] == null)
            {
                Debug.LogError($"⚠️ Audio Source {i} è NULL!");
            }
            else if (globalAudioSources[i].clip == null)
            {
                Debug.LogWarning($"⚠️ Audio Source {i} ({globalAudioSources[i].name}) non ha clip assegnata!");
            }
            else
            {
                Debug.Log($"✓ Audio Source {i}: {globalAudioSources[i].name} - Clip: {globalAudioSources[i].clip.name}");
            }
        }
        
        Debug.Log("=== Fine Validazione ===");
    }

    // Proprietà per debug e controllo esterno
    public bool EffectsStarted => effectsStarted;
    public bool EffectsStopped => effectsStopped;
    public int TotalCFXREffects => globalCFXREffects.Count;
    public int TotalAudioSources => globalAudioSources.Count;
}