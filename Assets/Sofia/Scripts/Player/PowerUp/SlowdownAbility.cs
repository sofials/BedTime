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

    public override int powerCost => 25;
    protected override bool HasFixedDuration => true;

    private struct PlatformData
    {
        public MovingPlatform platform;
        public float originalSpeed;
        public float customDuration;
        public Coroutine deactivationCoroutine;
    }

    private struct RotatorData
    {
        public RotatingObject rotator;
        public float originalSpeed;
        public float customDuration;
        public Coroutine deactivationCoroutine;
    }

    private struct TurtleShellData
    {
        public TurtleShell turtleShell;
        public float customDuration;
        public Coroutine deactivationCoroutine;
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
    /// Override di CanActivate per rimuovere il controllo su IsActive
    /// Permette il riutilizzo immediato su nuovi bersagli
    /// </summary>
    public override bool CanActivate()
    {
        // ✅ RIMOSSO il controllo !IsActive per permettere riutilizzo immediato
        return powerUpScript != null && powerUpScript.HasEnoughPower(powerCost);
    }

    /// <summary>
    /// Verifica se ci sono oggetti validi nel raggio d'azione PRIMA di consumare energia
    /// </summary>
    
/// <summary>
/// Verifica se ci sono oggetti validi nel raggio d'azione PRIMA di consumare energia
/// </summary>
private bool HasValidTargetsInRange()
{
    Collider[] colliders = Physics.OverlapSphere(powerUpScript.transform.position, slowdownRadius);
    
    foreach (Collider col in colliders)
    {
        // MovingPlatform - verifica se NON è già rallentata E se può essere rallentata
        if (col.CompareTag("MovingPlatform") && col.TryGetComponent(out MovingPlatform mp))
        {
            // ✅ FIX: Controlla canBeSlowed prima di tutto
            if (!mp.canBeSlowed)
            {
                continue; // Salta questa piattaforma se non può essere rallentata
            }
            
            // Controlla se questa piattaforma NON è già nella nostra lista
            bool alreadyAffected = false;
            foreach (var data in affectedPlatforms)
            {
                if (data.platform == mp)
                {
                    alreadyAffected = true;
                    break;
                }
            }
            if (!alreadyAffected) return true;
        }
        
        // RotatingPlatform - verifica se NON è già rallentata
        if (col.CompareTag("RotatingPlatform") && col.TryGetComponent(out RotatingObject ro))
        {
            // Controlla se questo rotatore NON è già nella nostra lista
            bool alreadyAffected = false;
            foreach (var data in affectedRotators)
            {
                if (data.rotator == ro)
                {
                    alreadyAffected = true;
                    break;
                }
            }
            if (!alreadyAffected) return true;
        }
        
        // TurtleShell - verifica se NON è già rallentata
        if (col.CompareTag("TurtleShellHurtbox"))
        {
            TurtleShell ts = col.GetComponentInParent<TurtleShell>();
            if (ts != null && !ts.isSlow)
            {
                return true;
            }
        }
        
        // NPC - verifica se NON è già fermato
        if (col.CompareTag("Chibi"))
        {
            Npc_village npc = col.GetComponent<Npc_village>();
            if (npc != null && !npc.IsStopped)
            {
                return true;
            }
        }
        
        // Golem - verifica se NON è già rallentato
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
         if (powerUpScript == null)
    {
        Debug.Log("[SlowdownAbility] PowerUpScript non disponibile.");
        // ❌ NESSUN AUDIO/ANIMAZIONE quando power up non abilitato
        return;
    }

        // ✅ STEP 1: Verifica energia disponibile
        if (!powerUpScript.HasEnoughPower(powerCost))
        {
            Debug.Log("[SlowdownAbility] Energia insufficiente per attivare l'abilità.");

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
            
            if (failureSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(failureSound);
            }
            
            if (PlayerUI.Instance != null)
            {
                PlayerUI.Instance.PulseIconAt(effectIconIndex);
            }
            
            return;
        }

        // ✅ STEP 3: Se arriviamo qui, tutto è OK - consuma energia e attiva
        Debug.Log($"[SlowdownAbility] Condizioni soddisfatte. Consumo {powerCost} energia.");
        
        powerUpScript.SpendPower(powerCost);
        
        // ✅ NON settiamo IsActive = true per permettere riutilizzo immediato
        Debug.Log($"[SlowdownAbility] Oggetti attualmente rallentati: {affectedPlatforms.Count + affectedRotators.Count + affectedTurtleShells.Count}");
        
        // Forza aggiornamento UI
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

        // Attiva l'abilità (aggiungerà SOLO nuovi oggetti alle liste esistenti)
        Activate();
    }

    public override void Activate()
    {
        Debug.Log("\n=== [SlowdownAbility] Activate() chiamato ===");

        // ✅ IMPORTANTE: NON svuotiamo le liste!
        // Questo permette di mantenere traccia degli oggetti già rallentati

        int newPlatformsCount = 0;
        int newRotatorsCount = 0;
        int newTurtlesCount = 0;
        int newNPCsCount = 0;

        Collider[] colliders = Physics.OverlapSphere(powerUpScript.transform.position, slowdownRadius);
        Debug.Log($"[SlowdownAbility] Collider trovati: {colliders.Length}");

        foreach (Collider col in colliders)
        {
            if (col.CompareTag("MovingPlatform") && col.TryGetComponent(out MovingPlatform mp))
            {
                // ✅ FIX: Controlla canBeSlowed prima di processare
    if (!mp.canBeSlowed)
    {
        Debug.Log($"→ MovingPlatform {col.name} ha canBeSlowed = false, skip.");
        continue; // Salta questa piattaforma
    }
    
                // ✅ Controlla se questa piattaforma è già rallentata
                bool alreadyAffected = false;
                foreach (var data in affectedPlatforms)
                {
                    if (data.platform == mp)
                    {
                         alreadyAffected = true;
            Debug.Log($"→ MovingPlatform {col.name} già rallentata, skip.");
            break;
                    }
                }
                
                if (!alreadyAffected)
                {
                    float platformDuration = mp.customSlowdownDuration > 0 ? mp.customSlowdownDuration : customDuration;
                    
                    var platformData = new PlatformData
                    {
                        platform = mp,
                        originalSpeed = mp.speed,
                        customDuration = platformDuration,
                        deactivationCoroutine = null
                    };
                    
                    float originalSpeed = mp.speed;
                    
                    if (mp.useCustomSlowdown)
                    {
                        mp.speed = mp.customSlowdownFactor;
                        Debug.Log($"→ MovingPlatform {col.name} rallentata da {originalSpeed} a {mp.speed} (custom) per {platformDuration}s.");
                    }
                    else
                    {
                        mp.speed *= slowdownFactor;
                        Debug.Log($"→ MovingPlatform {col.name} rallentata da {originalSpeed} a {mp.speed} (factor) per {platformDuration}s.");
                    }
                    
                    mp.SetOverlayActive(true);
                    mp.PlaySlowdownEffect(1f);

                    platformData.deactivationCoroutine = StartCoroutine(DeactivatePlatformAfterDuration(platformData));
                    
                    affectedPlatforms.Add(platformData);
                    newPlatformsCount++;
                }
            }
            else if (col.CompareTag("RotatingPlatform") && col.TryGetComponent(out RotatingObject ro))
            {
                // ✅ Controlla se questo rotatore è già rallentato
                bool alreadyAffected = false;
                foreach (var data in affectedRotators)
                {
                    if (data.rotator == ro)
                    {
                        alreadyAffected = true;
                        Debug.Log($"→ RotatingPlatform {col.name} già rallentata, skip.");
                        break;
                    }
                }
                
                if (!alreadyAffected)
                {
                    float rotatorDuration = ro.customSlowdownDuration > 0 ? ro.customSlowdownDuration : customDuration;
                    
                    var rotatorData = new RotatorData
                    {
                        rotator = ro,
                        originalSpeed = ro.rotationSpeed,
                        customDuration = rotatorDuration,
                        deactivationCoroutine = null
                    };
                    
                    float originalSpeed = ro.rotationSpeed;
                    float newSpeed;
                    
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
                    
                    ro.SetSlowdownState(true, newSpeed);
                    ro.SetOverlayActive(true);
                    ro.PlaySlowdownEffect(1f);

                    rotatorData.deactivationCoroutine = StartCoroutine(DeactivateRotatorAfterDuration(rotatorData));
                    
                    affectedRotators.Add(rotatorData);
                    newRotatorsCount++;
                }
            }
            else if (col.CompareTag("TurtleShellHurtbox"))
            {
                TurtleShell ts = col.GetComponentInParent<TurtleShell>();
                if (ts != null && !ts.isSlow)
                {
                    // ✅ Controlla se questa turtle shell è già nella lista
                    bool alreadyInList = false;
                    foreach (var existingTurtle in affectedTurtleShells)
                    {
                        if (existingTurtle.turtleShell == ts)
                        {
                            alreadyInList = true;
                            Debug.Log($"→ TurtleShell {ts.name} già rallentata, skip.");
                            break;
                        }
                    }
                    
                    if (!alreadyInList)
                    {
                        float turtleDuration = ts.customSlowdownDuration > 0 ? ts.customSlowdownDuration : customDuration;
                        
                        var turtleData = new TurtleShellData
                        {
                            turtleShell = ts,
                            customDuration = turtleDuration,
                            deactivationCoroutine = null
                        };
                        
                        ts.slowFactor = slowdownFactor;
                        ts.SetSlow(true);
                        ts.SetOverlayActive(true);
                        ts.PlaySlowdownEffect(1f);
                        ts.activeSlowdownAbility = this;

                        turtleData.deactivationCoroutine = StartCoroutine(DeactivateTurtleShellAfterDuration(turtleData));
                        
                        affectedTurtleShells.Add(turtleData);
                        newTurtlesCount++;
                        Debug.Log($"→ TurtleShell {ts.name} rallentata per {turtleDuration}s.");
                    }
                }
            }
            else if (col.CompareTag("Chibi"))
            {
                Npc_village npc = col.GetComponent<Npc_village>();
                if (npc != null && !npc.IsStopped)
                {
                    // ✅ Controlla se questo NPC è già nella lista
                    bool alreadyInList = affectedNPCs.Contains(npc);
                    
                    if (!alreadyInList)
                    {
                        npc.StopNPC();
                        affectedNPCs.Add(npc);
                        newNPCsCount++;
                        Debug.Log($"→ NPC Village {npc.name} fermato definitivamente.");
                    }
                }
            }
            else if (col.CompareTag("GolemHurtbox"))
            {
                Golem golem = col.GetComponentInParent<Golem>();
                if (golem != null && !golem.isSlow)
                {
                    float golemDuration = golem.customSlowdownDuration > 0 ? golem.customSlowdownDuration : customDuration;
                    golem.StartSlow(golemDuration, this);
                    Debug.Log($"→ Golem {golem.name} rallentato da Slowdown per {golemDuration}s.");
                }
            }
        }

        // ✅ Verifica solo i NUOVI oggetti aggiunti
        if (newPlatformsCount == 0 && newRotatorsCount == 0 && newTurtlesCount == 0 && newNPCsCount == 0)
        {
            Debug.LogWarning("[SlowdownAbility] ⚠️ Nessun NUOVO oggetto rallentato!");
        }
        else
        {
            if (effectAudioSource != null && effectAudioClip != null)
            {
                effectAudioSource.Play();
                Debug.Log("[SlowdownAbility] Audio effetto slowdown riprodotto.");
            }

            Debug.Log($"[SlowdownAbility] NUOVI oggetti rallentati: {newPlatformsCount} piattaforme, " +
                      $"{newRotatorsCount} rotatori, {newTurtlesCount} TurtleShell, {newNPCsCount} NPC.");
            Debug.Log($"[SlowdownAbility] TOTALE oggetti rallentati: {affectedPlatforms.Count} piattaforme, " +
                      $"{affectedRotators.Count} rotatori, {affectedTurtleShells.Count} TurtleShell, {affectedNPCs.Count} NPC.");
        }
        
        // ✅ FIX: Non settiamo mai IsActive = true per evitare blocchi dalla classe base
        // IsActive rimane sempre false per permettere riutilizzo immediato
        IsActive = false;
        
        Debug.Log($"[SlowdownAbility] IsActive mantenuto a FALSE per permettere riutilizzo immediato");
        Debug.Log($"[SlowdownAbility] Oggetti attualmente rallentati: {affectedPlatforms.Count} piattaforme, {affectedRotators.Count} rotatori, {affectedTurtleShells.Count} turtle");
        Debug.Log("=== [SlowdownAbility] Fine Activate() ===\n");
    }

    // ✅ Coroutine individuale per le MovingPlatform
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

    // ✅ Coroutine individuale per i RotatingObject
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

    // ✅ Coroutine individuale per le TurtleShell
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

    // ✅ Metodi helper per rimuovere oggetti dalle liste
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

    // ✅ FIX: Non modifichiamo mai IsActive per evitare blocchi
    // Questo metodo ora serve solo per logging, non per cambiare IsActive
    private void CheckIfAllObjectsDeactivated()
    {
        bool hasActiveEffects = affectedPlatforms.Count > 0 || 
                               affectedRotators.Count > 0 || 
                               affectedTurtleShells.Count > 0;
        
        if (!hasActiveEffects)
        {
            Debug.Log("[SlowdownAbility] Tutti gli oggetti temporanei sono stati disattivati");
        }
        
        // NON modifichiamo IsActive - rimane sempre false
    }

    public override void Deactivate()
    {
        Debug.Log("\n=== [SlowdownAbility] Deactivate() forzato chiamato ===");

        // ✅ Ferma tutte le coroutine individuali
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

        // Gli NPC rimangono fermi definitivamente
        Debug.Log($"[SlowdownAbility] {affectedNPCs.Count} NPCs rimangono fermi definitivamente (non vengono ripristinati)");

        affectedPlatforms.Clear();
        affectedRotators.Clear();
        affectedTurtleShells.Clear();
        // NON fare affectedNPCs.Clear() - gli NPC rimangono fermi per sempre

        IsActive = false;

        Debug.Log("[SlowdownAbility] Slowdown disattivato forzatamente.");
        Debug.Log("=== [SlowdownAbility] Fine Deactivate() ===\n");
    }
}