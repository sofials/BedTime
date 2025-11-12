using UnityEngine;
using System.Collections;
using CartoonFX;

[RequireComponent(typeof(CFXR_Effect))]
public class CFXR_LavaJetController : MonoBehaviour
{
    [Header("Riferimenti")]
    private CFXR_Effect effect;
    private ParticleSystem parentPS;
    private ParticleSystem[] childrenPS;

    [Header("Collider di Danno")]
    [SerializeField] private Collider damageCollider; // ← Assegna il collider figlio nell'Inspector
    [SerializeField] private bool autoFindDamageCollider = true;
    [SerializeField] private string damageColliderName = "LavaZone"; // Nome del GameObject figlio con il collider

    [Header("Impostazioni Intermittenza")]
    [Tooltip("Durata dell'emissione in secondi")]
    public float emissionDuration = 2f;
    
    [Tooltip("Durata della pausa tra le emissioni")]
    public float pauseDuration = 1.5f;
    
    [Tooltip("Avvia automaticamente all'attivazione")]
    public bool autoStart = true;

    [Header("Opzionale")]
    [Tooltip("Variazione casuale sulla durata (±)")]
    public float durationRandomness = 0.5f;

    [Header("Debug")]
    [SerializeField] private bool debugEmission = false;
    [SerializeField] private bool debugCollisions = false;

    private bool isEmitting = false;
    private Coroutine emissionCoroutine;

    private void Awake()
    {
        // Ottieni i componenti
        effect = GetComponent<CFXR_Effect>();
        parentPS = GetComponent<ParticleSystem>();
        childrenPS = GetComponentsInChildren<ParticleSystem>(true);

        if (parentPS == null)
        {
            Debug.LogError($"[LavaJet] {gameObject.name}: Nessun ParticleSystem trovato!");
            return;
        }

        // ✅ Auto-trova il collider figlio
        if (autoFindDamageCollider && damageCollider == null)
        {
            // Cerca per nome
            Transform child = transform.Find(damageColliderName);
            if (child != null)
            {
                damageCollider = child.GetComponent<Collider>();
            }

            // Se non trovato per nome, cerca il primo collider trigger nei figli
            if (damageCollider == null)
            {
                Collider[] childColliders = GetComponentsInChildren<Collider>(true);
                foreach (var col in childColliders)
                {
                    // Escludi il collider del ParticleSystem (se presente)
                    if (col.isTrigger && col.gameObject != gameObject)
                    {
                        damageCollider = col;
                        break;
                    }
                }
            }

            if (damageCollider != null && debugEmission)
            {
                Debug.Log($"[LavaJet] Damage collider auto-assegnato: {damageCollider.gameObject.name}");
            }
        }

        // ✅ Verifica che il collider sia un trigger
        if (damageCollider != null)
        {
            if (!damageCollider.isTrigger)
            {
                Debug.LogWarning($"[LavaJet] {damageCollider.gameObject.name} non è un Trigger! Impostando isTrigger = true");
                damageCollider.isTrigger = true;
            }

            // ✅ Disattiva il collider all'inizio
            damageCollider.enabled = false;

            if (debugEmission)
            {
                Debug.Log($"[LavaJet] Damage collider configurato: {damageCollider.gameObject.name} (Layer: {LayerMask.LayerToName(damageCollider.gameObject.layer)})");
            }
        }
        else
        {
            Debug.LogWarning($"[LavaJet] {gameObject.name}: NESSUN COLLIDER DI DANNO TROVATO! La lava non farà danno.");
        }

        // ✅ Ferma le particelle all'inizio (ma NON disattivare il GameObject)
        StopAllParticleSystems();
        
        if (effect != null)
        {
            effect.ResetState();
        }

        if (debugEmission)
        {
            Debug.Log($"[LavaJet] {gameObject.name} inizializzato (autoStart: {autoStart})");
        }
    }

    private void OnEnable()
    {
        if (autoStart && parentPS != null)
        {
            StartIntermittentEmission();
        }
    }

    private void OnDisable()
    {
        StopIntermittentEmission();
    }

    /// <summary>
    /// Avvia l'emissione intermittente del getto di lava
    /// </summary>
    public void StartIntermittentEmission()
    {
        if (emissionCoroutine != null)
        {
            StopCoroutine(emissionCoroutine);
        }
        
        emissionCoroutine = StartCoroutine(IntermittentEmissionRoutine());
        
        if (debugEmission)
        {
            Debug.Log($"[LavaJet] {gameObject.name} - Emissione intermittente AVVIATA");
        }
    }

    /// <summary>
    /// Ferma l'emissione intermittente
    /// </summary>
    public void StopIntermittentEmission()
    {
        if (emissionCoroutine != null)
        {
            StopCoroutine(emissionCoroutine);
            emissionCoroutine = null;
        }
        
        StopAllParticleSystems();
        
        // ✅ Disattiva il collider quando fermiamo
        if (damageCollider != null)
        {
            damageCollider.enabled = false;
        }
        
        isEmitting = false;
        
        if (debugEmission)
        {
            Debug.Log($"[LavaJet] {gameObject.name} - Emissione FERMATA");
        }
    }

    private IEnumerator IntermittentEmissionRoutine()
    {
        while (true)
        {
            // ✅ FASE DI EMISSIONE
            float actualEmissionDuration = emissionDuration + Random.Range(-durationRandomness, durationRandomness);
            actualEmissionDuration = Mathf.Max(0.1f, actualEmissionDuration);
            
            PlayAllParticleSystems();
            
            // ✅ ATTIVA IL COLLIDER durante l'emissione
            if (damageCollider != null)
            {
                damageCollider.enabled = true;
                
                if (debugEmission)
                {
                    Debug.Log($"[LavaJet] {gameObject.name} - Collider ATTIVATO");
                }
            }
            
            isEmitting = true;
            
            if (debugEmission)
            {
                Debug.Log($"[LavaJet] {gameObject.name} - EMISSIONE per {actualEmissionDuration:F2}s");
            }
            
            yield return new WaitForSeconds(actualEmissionDuration);
            
            // ✅ FASE DI PAUSA
            float actualPauseDuration = pauseDuration + Random.Range(-durationRandomness, durationRandomness);
            actualPauseDuration = Mathf.Max(0.1f, actualPauseDuration);
            
            StopAllParticleSystems();
            
            // ✅ DISATTIVA IL COLLIDER durante la pausa
            if (damageCollider != null)
            {
                damageCollider.enabled = false;
                
                if (debugEmission)
                {
                    Debug.Log($"[LavaJet] {gameObject.name} - Collider DISATTIVATO");
                }
            }
            
            isEmitting = false;
            
            if (debugEmission)
            {
                Debug.Log($"[LavaJet] {gameObject.name} - PAUSA per {actualPauseDuration:F2}s");
            }
            
            yield return new WaitForSeconds(actualPauseDuration);
        }
    }

    private void PlayAllParticleSystems()
    {
        if (effect != null)
        {
            effect.ResetState();
        }

        if (parentPS != null)
        {
            parentPS.Play(true);
        }

        foreach (var ps in childrenPS)
        {
            if (ps != null && ps != parentPS)
            {
                ps.Play();
            }
        }
    }

    private void StopAllParticleSystems()
    {
        if (parentPS != null)
        {
            parentPS.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        foreach (var ps in childrenPS)
        {
            if (ps != null && ps != parentPS)
            {
                ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            }
        }
    }

    // ✅ GESTIONE COLLISIONI TRIGGER - Gestisce tutto internamente!
    private void OnTriggerEnter(Collider other)
    {
        // Controlla solo se il collider è abilitato (cioè durante l'emissione)
        if (damageCollider == null || !damageCollider.enabled)
            return;

        if (debugCollisions)
        {
            Debug.Log($"[LavaJet] Trigger Enter: {other.gameObject.name} (Tag: {other.tag}, Layer: {LayerMask.LayerToName(other.gameObject.layer)})");
        }

        // Verifica se è il player
        ThirdPersonController player = other.GetComponent<ThirdPersonController>();
        if (player != null)
        {
            if (debugCollisions)
            {
                Debug.Log($"[LavaJet] 🔥 PLAYER RILEVATO nella lava: {gameObject.name}");
            }

            // Uccidi il player istantaneamente
            player.TakeDamage(player.CurrentHealth);
        }
    }

    /// <summary>
    /// Verifica se il getto sta attualmente emettendo
    /// </summary>
    public bool IsEmitting()
    {
        return isEmitting;
    }

    /// <summary>
    /// Cambia i parametri di intermittenza durante il runtime
    /// </summary>
    public void SetIntermittenceParameters(float newEmissionDuration, float newPauseDuration)
    {
        emissionDuration = newEmissionDuration;
        pauseDuration = newPauseDuration;
        
        if (debugEmission)
        {
            Debug.Log($"[LavaJet] Parametri aggiornati: Emission={emissionDuration}s, Pause={pauseDuration}s");
        }
    }

    /// <summary>
    /// Forza l'attivazione/disattivazione del collider (per debug)
    /// </summary>
    public void SetColliderEnabled(bool enabled)
    {
        if (damageCollider != null)
        {
            damageCollider.enabled = enabled;
            
            if (debugEmission)
            {
                Debug.Log($"[LavaJet] Collider forzato a: {(enabled ? "ATTIVO" : "DISATTIVO")}");
            }
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        emissionDuration = Mathf.Max(0.1f, emissionDuration);
        pauseDuration = Mathf.Max(0.1f, pauseDuration);
        durationRandomness = Mathf.Max(0f, durationRandomness);
    }

    // ✅ Visualizza il collider di danno nell'editor
    private void OnDrawGizmosSelected()
    {
        if (damageCollider == null) return;

        Gizmos.color = damageCollider.enabled 
            ? new Color(1f, 0.3f, 0f, 0.5f)  // Rosso-arancio quando attivo
            : new Color(0.5f, 0.5f, 0.5f, 0.2f); // Grigio quando disattivo

        if (damageCollider is BoxCollider box)
        {
            Matrix4x4 rotationMatrix = Matrix4x4.TRS(damageCollider.transform.position, damageCollider.transform.rotation, damageCollider.transform.lossyScale);
            Gizmos.matrix = rotationMatrix;
            Gizmos.DrawCube(box.center, box.size);
        }
        else if (damageCollider is SphereCollider sphere)
        {
            Gizmos.DrawSphere(damageCollider.transform.position + sphere.center, sphere.radius * damageCollider.transform.lossyScale.x);
        }
        else if (damageCollider is CapsuleCollider capsule)
        {
            Vector3 worldCenter = damageCollider.transform.TransformPoint(capsule.center);
            Gizmos.DrawSphere(worldCenter, capsule.radius * damageCollider.transform.lossyScale.x);
        }

        Gizmos.matrix = Matrix4x4.identity;
    }
#endif
}