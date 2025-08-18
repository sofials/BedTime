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
    }

    private struct RotatorData
    {
        public RotatingObject rotator;
        public float originalSpeed;
    }

    private List<PlatformData> affectedPlatforms = new();
    private List<RotatorData> affectedRotators = new();
    private List<TurtleShell> affectedTurtleShells = new();
    private List<Npc_village> affectedNPCs = new(); // Lista per tracciare gli NPC fermati

    private Coroutine deactivateCoroutine;

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
        affectedNPCs.Clear(); // Pulisci anche la lista NPC

        Collider[] colliders = Physics.OverlapSphere(powerUpScript.transform.position, slowdownRadius);
        Debug.Log($"[SlowdownAbility] Collider trovati: {colliders.Length}");

        foreach (Collider col in colliders)
        {
            if (col.CompareTag("MovingPlatform") && col.TryGetComponent(out MovingPlatform mp))
            {
                // Salva i dati originali della piattaforma
                affectedPlatforms.Add(new PlatformData
                {
                    platform = mp,
                    originalSpeed = mp.speed
                });
                
                float originalSpeed = mp.speed;
                
                // Applica il rallentamento in base alla modalità
                if (mp.useCustomSlowdown)
                {
                    // Usa il valore custom dell'oggetto
                    mp.speed = mp.customSlowdownFactor;
                    Debug.Log($"→ MovingPlatform {col.name} rallentata da {originalSpeed} a {mp.speed} (custom).");
                }
                else
                {
                    // Usa il moltiplicatore dell'abilità
                    mp.speed *= slowdownFactor;
                    Debug.Log($"→ MovingPlatform {col.name} rallentata da {originalSpeed} a {mp.speed} (factor).");
                }
                
                // Attiva l'overlay emissivo
                mp.SetOverlayActive(true);
                
                // Riproduce l'effetto di rallentamento se disponibile
                mp.PlaySlowdownEffect(1f);
            }
            else if (col.CompareTag("RotatingPlatform") && col.TryGetComponent(out RotatingObject ro))
            {
                // Salva i dati originali del rotatore (come per MovingPlatform)
                affectedRotators.Add(new RotatorData
                {
                    rotator = ro,
                    originalSpeed = ro.rotationSpeed
                });
                
                float originalSpeed = ro.rotationSpeed;
                
                // Applica il rallentamento in base alla modalità
                if (ro.useCustomSlowdown)
                {
                    // Usa il valore custom dell'oggetto
                    ro.rotationSpeed = ro.customSlowdownFactor;
                    Debug.Log($"→ RotatingPlatform {col.name} rallentata da {originalSpeed} a {ro.rotationSpeed} (custom).");
                }
                else
                {
                    // Usa il moltiplicatore dell'abilità
                    ro.rotationSpeed *= slowdownFactor;
                    Debug.Log($"→ RotatingPlatform {col.name} rallentata da {originalSpeed} a {ro.rotationSpeed} (factor).");
                }
                
                // Attiva l'overlay emissivo
                ro.SetOverlayActive(true);
                
                // Riproduce l'effetto di rallentamento se disponibile
                ro.PlaySlowdownEffect(1f);
            }
            else if (col.CompareTag("TurtleShellHurtbox"))
            {
                TurtleShell ts = col.GetComponentInParent<TurtleShell>();
                if (ts != null && !affectedTurtleShells.Contains(ts) && !ts.isSlow)
                {
                    ts.slowFactor = slowdownFactor;
                    ts.SetSlow(true);
                    ts.SetOverlayActive(true);
                    ts.PlaySlowdownEffect(1f);
                    ts.activeSlowdownAbility = this;
                    affectedTurtleShells.Add(ts);
                    Debug.Log($"→ TurtleShell {ts.name} rallentata.");
                }
            }
            else if (col.CompareTag("Chibi"))
            {
                Npc_village npc = col.GetComponent<Npc_village>();
                if (npc != null)
                {
                    npc.StopNPC();
                    affectedNPCs.Add(npc); // Aggiungi alla lista per il controllo
                    Debug.Log($"→ NPC Village {npc.name} fermato definitivamente.");
                }
            }
            else if (col.CompareTag("GolemHurtbox"))
            {
                Golem golem = col.GetComponentInParent<Golem>();
                if (golem != null && !golem.isSlow)
                {
                    golem.StartSlow(duration, this); // usa durata globale
                    Debug.Log($"→ Golem {golem.name} rallentato da Slowdown.");
                }
            }
        }

        // ⚠️ NOTA: A questo punto dovremmo sempre avere almeno un oggetto 
        // perché abbiamo controllato prima con HasValidTargetsInRange()
        // Ma aggiungiamo un controllo di sicurezza per casi edge
        if (affectedPlatforms.Count == 0 && affectedRotators.Count == 0 && affectedTurtleShells.Count == 0 && affectedNPCs.Count == 0)
        {
            Debug.LogWarning("[SlowdownAbility] ⚠️ CASO EDGE: Nessun oggetto rallentato dopo HasValidTargetsInRange() ha restituito true!");
            
            // In questo caso eccezionale, disattiva e magari restituisci l'energia?
            IsActive = false;
            // powerUpScript.AddPower(powerCost); // Opzionale: restituisci energia
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

        if (HasFixedDuration)
        {
            if (deactivateCoroutine != null)
                StopCoroutine(deactivateCoroutine);

            deactivateCoroutine = StartCoroutine(DeactivateAfterDuration());
        }
    }

    private IEnumerator DeactivateAfterDuration()
    {
        float blinkDuration = 3f;
        float blinkRate = 0.2f;

        // Aspetta fino a 3 secondi prima della fine
        yield return new WaitForSeconds(duration - blinkDuration);

        Debug.Log($"[SlowdownAbility] *** INIZIO BLINKING *** - Durata: {blinkDuration}s");
        Debug.Log($"[SlowdownAbility] Oggetti da far blinkare: {affectedPlatforms.Count} piattaforme, {affectedRotators.Count} rotatori, {affectedTurtleShells.Count} tartarughe");

        // Gestisce il blinking direttamente qui per migliore controllo
        float elapsed = 0f;
        bool blinkState = false;

        while (elapsed < blinkDuration)
        {
            Debug.Log($"[SlowdownAbility] >>> Blinking frame: {blinkState}, elapsed: {elapsed:F1}s");

            // MovingPlatform
            foreach (var data in affectedPlatforms)
            {
                if (data.platform != null)
                {
                    Debug.Log($"[SlowdownAbility] Settando overlay {blinkState} per MovingPlatform {data.platform.name}");
                    data.platform.SetOverlayActive(blinkState);
                }
            }

            // RotatingObject
            foreach (var data in affectedRotators)
            {
                if (data.rotator != null)
                {
                    Debug.Log($"[SlowdownAbility] Settando overlay {blinkState} per RotatingObject {data.rotator.name}");
                    data.rotator.SetOverlayActive(blinkState);
                }
            }

            // TurtleShell (se hanno il metodo SetOverlayActive)
            foreach (var ts in affectedTurtleShells)
            {
                if (ts != null)
                {
                    Debug.Log($"[SlowdownAbility] Settando overlay {blinkState} per TurtleShell {ts.name}");
                    ts.SetOverlayActive(blinkState);
                }
            }

            blinkState = !blinkState;
            yield return new WaitForSeconds(blinkRate);
            elapsed += blinkRate;
        }

        Debug.Log("[SlowdownAbility] *** FINE BLINKING - SPEGNIMENTO DEFINITIVO ***");

        // Spegni definitivamente tutti gli overlay
        foreach (var data in affectedPlatforms)
        {
            if (data.platform != null)
            {
                Debug.Log($"[SlowdownAbility] Spegnendo definitivamente overlay per MovingPlatform {data.platform.name}");
                data.platform.SetOverlayActive(false);
            }
        }

        foreach (var data in affectedRotators)
        {
            if (data.rotator != null)
            {
                Debug.Log($"[SlowdownAbility] Spegnendo definitivamente overlay per RotatingObject {data.rotator.name}");
                data.rotator.SetOverlayActive(false);
            }
        }

        foreach (var ts in affectedTurtleShells)
        {
            if (ts != null)
            {
                Debug.Log($"[SlowdownAbility] Spegnendo definitivamente overlay per TurtleShell {ts.name}");
                ts.SetOverlayActive(false);
            }
        }

        // Ora disattiva l'abilità (ripristina velocità, ecc.)
        Debug.Log("[SlowdownAbility] Chiamando Deactivate()...");
        Deactivate();
        deactivateCoroutine = null;
    }

    public override void Deactivate()
    {
        Debug.Log("\n=== [SlowdownAbility] Deactivate() chiamato ===");

        // Ripristina le MovingPlatform
        foreach (var data in affectedPlatforms)
        {
            if (data.platform != null)
            {
                // Ripristina la velocità originale
                data.platform.speed = data.originalSpeed;
                
                // NON toccare l'overlay qui - è già stato spento dal blinking
                
                Debug.Log($"→ Ripristinata MovingPlatform: {data.platform.name} (velocità: {data.originalSpeed})");
            }
        }

        // Ripristina i RotatingObject
        foreach (var data in affectedRotators)
        {
            if (data.rotator != null)
            {
                // Ripristina la velocità originale (come MovingPlatform)
                data.rotator.rotationSpeed = data.originalSpeed;
                
                // NON toccare l'overlay qui - è già stato spento dal blinking
                
                Debug.Log($"→ Ripristinato RotatingPlatform: {data.rotator.name} (velocità: {data.originalSpeed})");
            }
        }

        // Ripristina le TurtleShell
        foreach (var ts in affectedTurtleShells)
        {
            if (ts != null)
            {
                ts.SetSlow(false);
                // NON toccare l'overlay qui - è già stato spento dal blinking
                ts.activeSlowdownAbility = null;
                Debug.Log($"→ Ripristinata TurtleShell: {ts.name}");
            }
        }

        // Non sbloccare mai gli NPC (rimangono fermi definitivamente)

        affectedPlatforms.Clear();
        affectedRotators.Clear();
        affectedTurtleShells.Clear();
        affectedNPCs.Clear(); // Pulisci anche la lista NPC

        IsActive = false;

        Debug.Log("[SlowdownAbility] Slowdown disattivato.");
        Debug.Log("=== [SlowdownAbility] Fine Deactivate() ===\n");
    }
}