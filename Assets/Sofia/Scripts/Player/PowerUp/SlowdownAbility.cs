using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class SlowdownAbility : AbilityBase
{
    [Header("Slowdown Settings")]
    public float slowdownRadius = 50f;
    public float slowdownFactor = 0.5f;
    public float customDuration = 10f;

    [Header("Audio")]
    public AudioClip effectAudioClip;
    private AudioSource effectAudioSource;

    public override int powerCost => 20;
    protected override bool HasFixedDuration => true;

    private struct PlatformData
    {
        public MovingPlatform platform;
        public float originalSpeed;
        public float customDuration; // ✅ NUOVO: Durata personalizzata per questa piattaforma
        public Coroutine deactivationCoroutine; // ✅ NUOVO: Coroutine individuale
    }

    private struct RotatorData
    {
        public RotatingObject rotator;
        public float originalSpeed;
        public float customDuration; // ✅ NUOVO: Durata personalizzata per questo rotatore
        public Coroutine deactivationCoroutine; // ✅ NUOVO: Coroutine individuale
    }

    private struct TurtleShellData
    {
        public TurtleShell turtleShell;
        public float customDuration; // ✅ NUOVO: Durata personalizzata per questa turtle
        public Coroutine deactivationCoroutine; // ✅ NUOVO: Coroutine individuale
    }

    private List<PlatformData> affectedPlatforms = new();
    private List<RotatorData> affectedRotators = new();
    private List<TurtleShellData> affectedTurtleShells = new();
    private List<Npc_village> affectedNPCs = new();

    protected override void Awake()
    {
        base.Awake();
        duration = customDuration;
        effectIconIndex = 1;

        effectAudioSource = gameObject.AddComponent<AudioSource>();
        effectAudioSource.playOnAwake = false;
        effectAudioSource.clip = effectAudioClip;
    }

    /// <summary>
    /// Verifica se ci sono oggetti validi nel raggio d'azione PRIMA di consumare energia
    /// </summary>
    private bool HasValidTargetsInRange()
    {
        Collider[] colliders = Physics.OverlapSphere(powerUpScript.transform.position, slowdownRadius);
        
        foreach (Collider col in colliders)
        {
            // MovingPlatform
            if (col.CompareTag("MovingPlatform") && col.TryGetComponent(out MovingPlatform mp))
            {
                return true;
            }
            
            // RotatingPlatform
            if (col.CompareTag("RotatingPlatform") && col.TryGetComponent(out RotatingObject ro))
            {
                return true;
            }
            
            // TurtleShell (solo se non è già rallentata)
            if (col.CompareTag("TurtleShellHurtbox"))
            {
                TurtleShell ts = col.GetComponentInParent<TurtleShell>();
                if (ts != null && !ts.isSlow)
                {
                    return true;
                }
            }
            
            // Chibi (NPC Village)
            if (col.CompareTag("Chibi"))
            {
                return true;
            }
            
            // Golem (solo se non è già rallentato)
            if (col.CompareTag("GolemHurtbox"))
            {
                Golem golem = col.GetComponentInParent<Golem>();
                if (golem != null && !golem.isSlow)
                {
                    return true;
                }
            }
        }
        
        return false;
    }

    public override void TryActivate()
    {
        if (IsActive)
        {
            Debug.Log("[SlowdownAbility] Abilità già attiva – ignoro attivazione.");
            return;
        }

        // ✅ STEP 1: Verifica energia disponibile
        if (!powerUpScript.HasEnoughPower(powerCost))
        {
            Debug.Log("[SlowdownAbility] Energia insufficiente per attivare l'abilità.");
            
            // Riproduci suono di fallimento
            if (failureSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(failureSound);
            }
            return;
        }

        // ✅ STEP 2: Verifica se ci sono oggetti validi nel raggio
        if (!HasValidTargetsInRange())
        {
            Debug.LogWarning("[SlowdownAbility] Nessun oggetto valido nel raggio d'azione. Energia non consumata.");
            
            // Riproduci suono di fallimento (diverso dalla mancanza di energia)
            if (failureSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(failureSound);
            }
            
            // Pulse dell'icona per feedback visivo
            if (PlayerUI.Instance != null)
            {
                PlayerUI.Instance.PulseIconAt(effectIconIndex);
            }
            
            return;
        }

        // ✅ STEP 3: Se arriviamo qui, tutto è OK - consuma energia e attiva
        Debug.Log($"[SlowdownAbility] Condizioni soddisfatte. Consumo {powerCost} energia.");
        
        powerUpScript.SpendPower(powerCost);
        IsActive = true;
        
        // 🔄 Forza aggiornamento UI per sicurezza
        if (PlayerUI.Instance != null)
        {
            PlayerUI.Instance.UpdatePower(powerUpScript.CurrentPower);
        }
        
        // Audio di attivazione riuscita
        if (activationSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(activationSound);
        }

        // Feedback UI
        if (PlayerUI.Instance != null)
        {
            PlayerUI.Instance.PulseIconAt(effectIconIndex);
        }

        // Attiva l'abilità
        Activate();
    }

    public override void Activate()
    {
        Debug.Log("\n=== [SlowdownAbility] Activate() chiamato ===");

        affectedPlatforms.Clear();
        affectedRotators.Clear();
        affectedTurtleShells.Clear();
        affectedNPCs.Clear();

        Collider[] colliders = Physics.OverlapSphere(powerUpScript.transform.position, slowdownRadius);
        Debug.Log($"[SlowdownAbility] Collider trovati: {colliders.Length}");

        foreach (Collider col in colliders)
        {
            if (col.CompareTag("MovingPlatform") && col.TryGetComponent(out MovingPlatform mp))
            {
                // ✅ NUOVO: Ottieni la durata personalizzata dalla piattaforma
                float platformDuration = mp.customSlowdownDuration > 0 ? mp.customSlowdownDuration : customDuration;
                
                // Salva i dati originali della piattaforma
                var platformData = new PlatformData
                {
                    platform = mp,
                    originalSpeed = mp.speed,
                    customDuration = platformDuration,
                    deactivationCoroutine = null
                };
                
                float originalSpeed = mp.speed;
                
                // Applica il rallentamento in base alla modalità
                if (mp.useCustomSlowdown)
                {
                    // Usa il valore custom dell'oggetto
                    mp.speed = mp.customSlowdownFactor;
                    Debug.Log($"→ MovingPlatform {col.name} rallentata da {originalSpeed} a {mp.speed} (custom) per {platformDuration}s.");
                }
                else
                {
                    // Usa il moltiplicatore dell'abilità
                    mp.speed *= slowdownFactor;
                    Debug.Log($"→ MovingPlatform {col.name} rallentata da {originalSpeed} a {mp.speed} (factor) per {platformDuration}s.");
                }
                
                // Attiva l'overlay emissivo
                mp.SetOverlayActive(true);
                
                // Riproduce l'effetto di rallentamento se disponibile
                mp.PlaySlowdownEffect(1f);

                // ✅ NUOVO: Avvia coroutine individuale per questa piattaforma
                platformData.deactivationCoroutine = StartCoroutine(DeactivatePlatformAfterDuration(platformData));
                
                affectedPlatforms.Add(platformData);
            }
            else if (col.CompareTag("RotatingPlatform") && col.TryGetComponent(out RotatingObject ro))
            {
                // ✅ NUOVO: Ottieni la durata personalizzata dal rotatore
                float rotatorDuration = ro.customSlowdownDuration > 0 ? ro.customSlowdownDuration : customDuration;
                
                // Salva i dati originali del rotatore
                var rotatorData = new RotatorData
                {
                    rotator = ro,
                    originalSpeed = ro.rotationSpeed,
                    customDuration = rotatorDuration,
                    deactivationCoroutine = null
                };
                
                float originalSpeed = ro.rotationSpeed;
                float newSpeed;
                
                // Calcola la nuova velocità in base alla modalità
                if (ro.useCustomSlowdown)
                {
                    newSpeed = ro.customSlowdownFactor;
                    Debug.Log($"→ RotatingPlatform {col.name} rallentata da {originalSpeed} a {newSpeed} (custom) per {rotatorDuration}s.");
                }
                else
                {
                    newSpeed = originalSpeed * slowdownFactor;
                    Debug.Log($"→ RotatingPlatform {col.name} rallentata da {originalSpeed} a {newSpeed} (factor) per {rotatorDuration}s.");
                }
                
                // ✅ NUOVO: Usa il nuovo metodo per impostare slowdown
                ro.SetSlowdownState(true, newSpeed);
                
                // Attiva l'overlay emissivo
                ro.SetOverlayActive(true);
                
                // Riproduce l'effetto di rallentamento se disponibile
                ro.PlaySlowdownEffect(1f);

                // ✅ NUOVO: Avvia coroutine individuale per questo rotatore
                rotatorData.deactivationCoroutine = StartCoroutine(DeactivateRotatorAfterDuration(rotatorData));
                
                affectedRotators.Add(rotatorData);
            }
            else if (col.CompareTag("TurtleShellHurtbox"))
            {
                TurtleShell ts = col.GetComponentInParent<TurtleShell>();
                if (ts != null && !ts.isSlow)
                {
                    // ✅ NUOVO: Ottieni la durata personalizzata dalla turtle shell
                    float turtleDuration = ts.customSlowdownDuration > 0 ? ts.customSlowdownDuration : customDuration;
                    
                    var turtleData = new TurtleShellData
                    {
                        turtleShell = ts,
                        customDuration = turtleDuration,
                        deactivationCoroutine = null
                    };
                    
                    // Controlla se questa turtle shell è già nella lista
                    bool alreadyInList = false;
                    foreach (var existingTurtle in affectedTurtleShells)
                    {
                        if (existingTurtle.turtleShell == ts)
                        {
                            alreadyInList = true;
                            break;
                        }
                    }
                    
                    if (!alreadyInList)
                    {
                        ts.slowFactor = slowdownFactor;
                        ts.SetSlow(true);
                        ts.SetOverlayActive(true);
                        ts.PlaySlowdownEffect(1f);
                        ts.activeSlowdownAbility = this;

                        // ✅ NUOVO: Avvia coroutine individuale per questa turtle shell
                        turtleData.deactivationCoroutine = StartCoroutine(DeactivateTurtleShellAfterDuration(turtleData));
                        
                        affectedTurtleShells.Add(turtleData);
                        Debug.Log($"→ TurtleShell {ts.name} rallentata per {turtleDuration}s.");
                    }
                }
            }
            else if (col.CompareTag("Chibi"))
            {
                Npc_village npc = col.GetComponent<Npc_village>();
                if (npc != null)
                {
                    npc.StopNPC();
                    affectedNPCs.Add(npc);
                    Debug.Log($"→ NPC Village {npc.name} fermato definitivamente.");
                }
            }
            else if (col.CompareTag("GolemHurtbox"))
            {
                Golem golem = col.GetComponentInParent<Golem>();
                if (golem != null && !golem.isSlow)
                {
                    // ✅ NUOVO: Usa durata personalizzata del Golem se disponibile
                    float golemDuration = golem.customSlowdownDuration > 0 ? golem.customSlowdownDuration : customDuration;
                    golem.StartSlow(golemDuration, this);
                    Debug.Log($"→ Golem {golem.name} rallentato da Slowdown per {golemDuration}s.");
                }
            }
        }

        if (affectedPlatforms.Count == 0 && affectedRotators.Count == 0 && affectedTurtleShells.Count == 0 && affectedNPCs.Count == 0)
        {
            Debug.LogWarning("[SlowdownAbility] ⚠️ CASO EDGE: Nessun oggetto rallentato dopo HasValidTargetsInRange() ha restituito true!");
            
            IsActive = false;
            return;
        }

        if (effectAudioSource != null && effectAudioClip != null)
        {
            effectAudioSource.Play();
            Debug.Log("[SlowdownAbility] Audio effetto slowdown riprodotto.");
        }

        Debug.Log($"[SlowdownAbility] Slowdown attivato su {affectedPlatforms.Count} piattaforme, " +
                  $"{affectedRotators.Count} rotatori, {affectedTurtleShells.Count} TurtleShell, {affectedNPCs.Count} NPC.");
        Debug.Log("=== [SlowdownAbility] Fine Activate() ===\n");
    }

    // ✅ NUOVO: Coroutine individuale per le MovingPlatform
    private IEnumerator DeactivatePlatformAfterDuration(PlatformData platformData)
    {
        float blinkDuration = 3f;
        float effectiveDuration = platformData.customDuration - blinkDuration;
        
        if (effectiveDuration > 0)
        {
            Debug.Log($"[SlowdownAbility] Platform {platformData.platform.name} attiva per {effectiveDuration}s, poi {blinkDuration}s di blinking");
            yield return new WaitForSeconds(effectiveDuration);
        }

        // Blinking
        float elapsed = 0f;
        float blinkRate = 0.2f;
        bool blinkState = false;

        while (elapsed < blinkDuration && platformData.platform != null)
        {
            platformData.platform.SetOverlayActive(blinkState);
            blinkState = !blinkState;
            yield return new WaitForSeconds(blinkRate);
            elapsed += blinkRate;
        }

        // Disattiva la piattaforma
        if (platformData.platform != null)
        {
            platformData.platform.speed = platformData.originalSpeed;
            platformData.platform.SetOverlayActive(false);
            Debug.Log($"→ Platform {platformData.platform.name} disattivata dopo {platformData.customDuration}s");
        }

        // Rimuovi dalla lista
        RemovePlatformFromList(platformData.platform);
    }

    // ✅ NUOVO: Coroutine individuale per i RotatingObject
    private IEnumerator DeactivateRotatorAfterDuration(RotatorData rotatorData)
    {
        float blinkDuration = 3f;
        float effectiveDuration = rotatorData.customDuration - blinkDuration;
        
        if (effectiveDuration > 0)
        {
            Debug.Log($"[SlowdownAbility] Rotator {rotatorData.rotator.name} attivo per {effectiveDuration}s, poi {blinkDuration}s di blinking");
            yield return new WaitForSeconds(effectiveDuration);
        }

        // Blinking
        float elapsed = 0f;
        float blinkRate = 0.2f;
        bool blinkState = false;

        while (elapsed < blinkDuration && rotatorData.rotator != null)
        {
            rotatorData.rotator.SetOverlayActive(blinkState);
            blinkState = !blinkState;
            yield return new WaitForSeconds(blinkRate);
            elapsed += blinkRate;
        }

        // Disattiva il rotatore
        if (rotatorData.rotator != null)
        {
            rotatorData.rotator.SetSlowdownState(false, 0f);
            rotatorData.rotator.SetOverlayActive(false);
            
            // Backup se SetSlowdownState non funziona
            if (rotatorData.rotator.IsInSlowdown)
            {
                rotatorData.rotator.RestoreOriginalSpeed();
            }
            Debug.Log($"→ Rotator {rotatorData.rotator.name} disattivato dopo {rotatorData.customDuration}s");
        }

        // Rimuovi dalla lista
        RemoveRotatorFromList(rotatorData.rotator);
    }

    // ✅ NUOVO: Coroutine individuale per le TurtleShell
    private IEnumerator DeactivateTurtleShellAfterDuration(TurtleShellData turtleData)
    {
        float blinkDuration = 3f;
        float effectiveDuration = turtleData.customDuration - blinkDuration;
        
        if (effectiveDuration > 0)
        {
            Debug.Log($"[SlowdownAbility] TurtleShell {turtleData.turtleShell.name} attiva per {effectiveDuration}s, poi {blinkDuration}s di blinking");
            yield return new WaitForSeconds(effectiveDuration);
        }

        // Blinking
        float elapsed = 0f;
        float blinkRate = 0.2f;
        bool blinkState = false;

        while (elapsed < blinkDuration && turtleData.turtleShell != null)
        {
            turtleData.turtleShell.SetOverlayActive(blinkState);
            blinkState = !blinkState;
            yield return new WaitForSeconds(blinkRate);
            elapsed += blinkRate;
        }

        // Disattiva la turtle shell
        if (turtleData.turtleShell != null)
        {
            turtleData.turtleShell.SetSlow(false);
            turtleData.turtleShell.SetOverlayActive(false);
            turtleData.turtleShell.activeSlowdownAbility = null;
            Debug.Log($"→ TurtleShell {turtleData.turtleShell.name} disattivata dopo {turtleData.customDuration}s");
        }

        // Rimuovi dalla lista
        RemoveTurtleShellFromList(turtleData.turtleShell);
    }

    // ✅ NUOVO: Metodi helper per rimuovere oggetti dalle liste
    private void RemovePlatformFromList(MovingPlatform platform)
    {
        for (int i = affectedPlatforms.Count - 1; i >= 0; i--)
        {
            if (affectedPlatforms[i].platform == platform)
            {
                affectedPlatforms.RemoveAt(i);
                break;
            }
        }
        
        CheckIfAllObjectsDeactivated();
    }

    private void RemoveRotatorFromList(RotatingObject rotator)
    {
        for (int i = affectedRotators.Count - 1; i >= 0; i--)
        {
            if (affectedRotators[i].rotator == rotator)
            {
                affectedRotators.RemoveAt(i);
                break;
            }
        }
        
        CheckIfAllObjectsDeactivated();
    }

    private void RemoveTurtleShellFromList(TurtleShell turtleShell)
    {
        for (int i = affectedTurtleShells.Count - 1; i >= 0; i--)
        {
            if (affectedTurtleShells[i].turtleShell == turtleShell)
            {
                affectedTurtleShells.RemoveAt(i);
                break;
            }
        }
        
        CheckIfAllObjectsDeactivated();
    }

    // ✅ NUOVO: Controlla se tutti gli oggetti sono stati disattivati
    private void CheckIfAllObjectsDeactivated()
    {
        if (affectedPlatforms.Count == 0 && affectedRotators.Count == 0 && affectedTurtleShells.Count == 0)
        {
            Debug.Log("[SlowdownAbility] Tutti gli oggetti sono stati disattivati - abilità completamente terminata");
            IsActive = false;
        }
    }

    public override void Deactivate()
    {
        Debug.Log("\n=== [SlowdownAbility] Deactivate() forzato chiamato ===");

        // ✅ NUOVO: Ferma tutte le coroutine individuali
        foreach (var platformData in affectedPlatforms)
        {
            if (platformData.deactivationCoroutine != null)
            {
                StopCoroutine(platformData.deactivationCoroutine);
            }
            
            if (platformData.platform != null)
            {
                platformData.platform.speed = platformData.originalSpeed;
                platformData.platform.SetOverlayActive(false);
                Debug.Log($"→ Ripristinata MovingPlatform: {platformData.platform.name} (velocità: {platformData.originalSpeed})");
            }
        }

        foreach (var rotatorData in affectedRotators)
        {
            if (rotatorData.deactivationCoroutine != null)
            {
                StopCoroutine(rotatorData.deactivationCoroutine);
            }
            
            if (rotatorData.rotator != null)
            {
                rotatorData.rotator.SetSlowdownState(false, 0f);
                rotatorData.rotator.SetOverlayActive(false);
                
                if (rotatorData.rotator.IsInSlowdown)
                {
                    rotatorData.rotator.RestoreOriginalSpeed();
                }
                Debug.Log($"→ Ripristinato RotatingPlatform: {rotatorData.rotator.name} (velocità: {rotatorData.originalSpeed})");
            }
        }

        foreach (var turtleData in affectedTurtleShells)
        {
            if (turtleData.deactivationCoroutine != null)
            {
                StopCoroutine(turtleData.deactivationCoroutine);
            }
            
            if (turtleData.turtleShell != null)
            {
                turtleData.turtleShell.SetSlow(false);
                turtleData.turtleShell.SetOverlayActive(false);
                turtleData.turtleShell.activeSlowdownAbility = null;
                Debug.Log($"→ Ripristinata TurtleShell: {turtleData.turtleShell.name}");
            }
        }

        // Non sbloccare mai gli NPC (rimangono fermi definitivamente)

        affectedPlatforms.Clear();
        affectedRotators.Clear();
        affectedTurtleShells.Clear();
        affectedNPCs.Clear();

        IsActive = false;

        Debug.Log("[SlowdownAbility] Slowdown disattivato forzatamente.");
        Debug.Log("=== [SlowdownAbility] Fine Deactivate() ===\n");
    }
}