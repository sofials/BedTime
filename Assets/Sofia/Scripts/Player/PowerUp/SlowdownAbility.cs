using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class SlowdownAbility : AbilityBase
{
    [Header("Slowdown Settings")]
    public float slowdownRadius = 50f;
    public float slowdownFactor = 0.5f;
    public float customDuration = 10f;
    public Color overlayColor = new Color(0.47f, 0.57f, 1f, 1f); // 7791FF

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

    // Override CanActivate per permettere riutilizzo immediato
    public override bool CanActivate()
    {
        return IsEnabled && powerUpScript != null && powerUpScript.HasEnoughPower(powerCost);
    }

    // Verifica se ci sono oggetti validi nel raggio d'azione
    private bool HasValidTargetsInRange()
    {
        Collider[] colliders = Physics.OverlapSphere(powerUpScript.transform.position, slowdownRadius);
        
        foreach (Collider col in colliders)
        {
            if (col.CompareTag("MovingPlatform") && col.TryGetComponent(out MovingPlatform mp))
            {
                if (!mp.canBeSlowed) continue;
                
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
            
            if (col.CompareTag("RotatingPlatform") && col.TryGetComponent(out RotatingObject ro))
            {
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
            
            if (col.CompareTag("TurtleShellHurtbox"))
            {
                TurtleShell ts = col.GetComponentInParent<TurtleShell>();
                if (ts != null && !ts.isSlow)
                {
                    return true;
                }
            }
            
            if (col.CompareTag("Chibi"))
            {
                Npc_village npc = col.GetComponent<Npc_village>();
                if (npc != null && !npc.IsStopped)
                {
                    return true;
                }
            }
            
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

    // Override TryActivate per controllare i bersagli validi
  public override void TryActivate()
{
    Debug.Log("\n=== [SlowdownAbility] TryActivate() DEBUG START ===");
    Debug.Log($"IsEnabled: {IsEnabled}");
    Debug.Log($"GetDisableReason(): {GetDisableReason()}");
    Debug.Log($"powerUpScript null: {powerUpScript == null}");
    if (powerUpScript != null)
        Debug.Log($"HasEnoughPower({powerCost}): {powerUpScript.HasEnoughPower(powerCost)}");
    // Prima controlla se l'abilità è abilitata a livello di sistema
        if (!IsEnabled)
        {
            string reason = GetDisableReason();
            Debug.LogWarning($"[SlowdownAbility] Abilità disabilitata: {reason}");

            // SE L'ABILITÀ NON È PERMESSA NEL LIVELLO, NON FARE ASSOLUTAMENTE NIENTE
            if (reason.Contains("non permessa in questo livello"))
            {
                Debug.Log("[SlowdownAbility] Abilità non permessa nel livello - nessun feedback, nessuna UI");
                   Debug.Log("=== [SlowdownAbility] TryActivate() DEBUG END (silent exit) ===\n");
                return; // Esce silenziosamente - NO suoni, NO UI, NO coroutines
            }
            Debug.Log("[SlowdownAbility] Altri tipi di disabilitazione - riproduce failure sound");

            // Solo per altri tipi di disabilitazione (abilità disabilitata manualmente)
            if (failureSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(failureSound);
                Debug.Log("[SlowdownAbility] Audio di fallimento per abilità disabilitata");
            }

            // UI pulse solo per disabilitazioni manuali, NON per restrizioni di livello
            if (PlayerUI.Instance != null)
            {
                PlayerUI.Instance.PulseIconAt(effectIconIndex);
            }

            return;
        }

    // Controlla energia
    if (powerUpScript == null || !powerUpScript.HasEnoughPower(powerCost))
    {
        Debug.LogWarning("[SlowdownAbility] Energia insufficiente o PowerUp script mancante");
        
        // Suona failure sound per energia insufficiente
        if (failureSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(failureSound);
            Debug.Log("[SlowdownAbility] Audio di fallimento per energia insufficiente");
        }
        
        if (PlayerUI.Instance != null)
        {
            PlayerUI.Instance.PulseIconAt(effectIconIndex);
        }
        
        return;
    }

    // Controlla se ci sono bersagli validi
    if (!HasValidTargetsInRange())
    {
        Debug.LogWarning("[SlowdownAbility] Nessun oggetto valido nel raggio d'azione (tutti già rallentati).");
        
        // Suona failure sound per nessun bersaglio SENZA consumare energia
        if (failureSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(failureSound);
            Debug.Log("[SlowdownAbility] Audio di fallimento riprodotto per nessun bersaglio valido (energia NON consumata)");
        }
        
        if (PlayerUI.Instance != null)
        {
            PlayerUI.Instance.PulseIconAt(effectIconIndex);
        }
        
        return;
    }

    // Se arriviamo qui, l'abilità può essere attivata e ci sono bersagli validi
    Activate();
    
    // Suona activation sound
    if (activationSound != null && audioSource != null)
    {
        audioSource.PlayOneShot(activationSound);
    }

    if (PlayerUI.Instance != null)
        PlayerUI.Instance.PulseIconAt(effectIconIndex);
}

    public override void Activate()
    {
        Debug.Log("\n=== [SlowdownAbility] Activate() chiamato ===");

        // Consuma energia SOLO quando l'abilità viene effettivamente attivata
        powerUpScript.SpendPower(powerCost);

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
                if (!mp.canBeSlowed)
                {
                    Debug.Log($"→ MovingPlatform {col.name} ha canBeSlowed = false, skip.");
                    continue;
                }
                
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

        // Suona l'effect audio solo se ci sono nuovi oggetti rallentati
        if (newPlatformsCount > 0 || newRotatorsCount > 0 || newTurtlesCount > 0 || newNPCsCount > 0)
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
        
        // Mantieni IsActive = false per permettere riutilizzo immediato
        IsActive = false;
        
        Debug.Log($"[SlowdownAbility] IsActive mantenuto a FALSE per permettere riutilizzo immediato");
        Debug.Log("=== [SlowdownAbility] Fine Activate() ===\n");
    }

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

        RemovePlatformFromList(platformData.platform);
    }

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
            
            if (rotatorData.rotator.IsInSlowdown)
            {
                rotatorData.rotator.RestoreOriginalSpeed();
            }
            Debug.Log($"→ Rotator {rotatorData.rotator.name} disattivato dopo {rotatorData.customDuration}s");
        }

        RemoveRotatorFromList(rotatorData.rotator);
    }

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

        RemoveTurtleShellFromList(turtleData.turtleShell);
    }

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
    }

    public override void Deactivate()
    {
        Debug.Log("\n=== [SlowdownAbility] Deactivate() forzato chiamato ===");

        // Ferma tutte le coroutine individuali
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