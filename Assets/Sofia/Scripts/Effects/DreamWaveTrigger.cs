using UnityEngine;
using Unity.Cinemachine;
using System.Collections;

/// <summary>
/// Trigger principale che coordina audio, dissolve e camera shake
/// </summary>
public class DreamWaveTrigger : MonoBehaviour
{
    [Header("Earthquake Settings")]
    [SerializeField] private float earthquakeDuration = 3f;
    [SerializeField] private bool disablePlayerMovement = true;
    [SerializeField] private bool onlyTriggerOnce = true;
    
    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip mainAudio;
    [SerializeField] private float audioVolume = 0.8f;
    [SerializeField] private bool loopAudio = false;
    
    [Header("Camera Shake Integration")]
    [SerializeField] private EarthquakeShake earthquakeShakeController;
    [SerializeField] private bool enableCameraShake = true;
    [SerializeField] private float shakeDelay = 0f; // Ritardo prima di iniziare shake
    [SerializeField] private float shakeStopDelay = 1f; // Ritardo dopo dissolve per fermare shake
    
    [Header("Material Dissolve Effect")]
    [SerializeField] private GameObject firstObject;
    [SerializeField] private GameObject secondObject;
    [SerializeField] private Material firstMaterial;
    [SerializeField] private Material secondMaterial;
    [SerializeField] private string dissolvePropertyName = "_Dissolve";
    [SerializeField] private float dissolveDuration = 3f;
    [SerializeField] private AnimationCurve dissolveCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    [SerializeField] private bool debugDissolve = false;
    
    [Header("Object Activation")]
    [SerializeField] private GameObject objectToActivate;
    [SerializeField] private float activationDelay = 0f; // Ritardo prima di attivare l'oggetto
    
    [Header("Debug")]
    [SerializeField] private bool debugMode = false;
    
    // Componenti e stati
    private ThirdPersonController playerController;
    private bool hasTriggered = false;
    private bool isAudioPlaying = false;
    private bool isDissolving = false;
    private Coroutine mainCoroutine;
    private Coroutine dissolveCoroutine;

    private void Awake()
    {
        SetupTrigger();
        SetupAudioSource();
        SetupDissolveMaterials();
        FindShakeController();
    }

    private void SetupTrigger()
    {
        BoxCollider boxCollider = GetComponent<BoxCollider>();
        if (boxCollider == null)
        {
            boxCollider = gameObject.AddComponent<BoxCollider>();
            Debug.LogWarning($"[DreamWaveTrigger] BoxCollider mancante su {name}. Aggiunto automaticamente.");
        }
        boxCollider.isTrigger = true;
    }

    private void SetupAudioSource()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                GameObject audioGO = new GameObject("TriggerAudio");
                audioGO.transform.SetParent(transform);
                audioGO.transform.localPosition = Vector3.zero;
                audioSource = audioGO.AddComponent<AudioSource>();
            }
        }
        
        audioSource.clip = mainAudio;
        audioSource.volume = 0f;
        audioSource.loop = loopAudio;
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0.7f;
        audioSource.rolloffMode = AudioRolloffMode.Linear;
        audioSource.maxDistance = 50f;
    }

    private void FindShakeController()
    {
        if (earthquakeShakeController == null)
        {
            earthquakeShakeController = FindFirstObjectByType<EarthquakeShake>();
            if (earthquakeShakeController == null && enableCameraShake)
            {
                Debug.LogWarning("[DreamWaveTrigger] EarthquakeShake controller non trovato! Camera shake disabilitato.");
                enableCameraShake = false;
            }
        }
    }

    private void SetupDissolveMaterials()
    {
        if (firstMaterial != null)
        {
            if (firstMaterial.HasProperty(dissolvePropertyName))
            {
                firstMaterial.SetFloat(dissolvePropertyName, 0f);
                if (debugDissolve)
                    Debug.Log($"[DreamWaveTrigger] Primo materiale '{firstMaterial.name}' inizializzato");
            }
            else
            {
                Debug.LogWarning($"[DreamWaveTrigger] Il primo materiale '{firstMaterial.name}' non ha la proprietà '{dissolvePropertyName}'");
            }
        }
        
        if (secondMaterial != null)
        {
            if (secondMaterial.HasProperty(dissolvePropertyName))
            {
                secondMaterial.SetFloat(dissolvePropertyName, 0f);
                if (debugDissolve)
                    Debug.Log($"[DreamWaveTrigger] Secondo materiale '{secondMaterial.name}' inizializzato");
            }
            else
            {
                Debug.LogWarning($"[DreamWaveTrigger] Il secondo materiale '{secondMaterial.name}' non ha la proprietà '{dissolvePropertyName}'");
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (onlyTriggerOnce && hasTriggered) return;
        if (isAudioPlaying) return;
        
        if (debugMode)
            Debug.Log($"[DreamWaveTrigger] Player entrato nel trigger: {name}");
        
        playerController = other.GetComponent<ThirdPersonController>();
        if (playerController == null)
        {
            Debug.LogError($"[DreamWaveTrigger] ThirdPersonController non trovato sul player!");
            return;
        }
        
        StartSequence();
    }

    private void StartSequence()
    {
        if (isAudioPlaying) return;
        
        hasTriggered = true;
        isAudioPlaying = true;
        
        if (debugMode)
            Debug.Log($"[DreamWaveTrigger] Avvio sequenza terremoto per {earthquakeDuration} secondi");
        
        mainCoroutine = StartCoroutine(MainSequence());
    }

    private IEnumerator MainSequence()
    {
        // ========================
        // FASE 1: AVVIO
        // ========================
        
        // Disabilita movimento del player
        if (disablePlayerMovement && playerController != null)
        {
            playerController.IsMovementLocked = true;
            if (debugMode) Debug.Log("[DreamWaveTrigger] Movimento player disabilitato");
        }
        
        // Avvia audio principale
        if (audioSource != null && mainAudio != null)
        {
            audioSource.clip = mainAudio;
            audioSource.loop = loopAudio;
            audioSource.volume = audioVolume;
            audioSource.Play();
            if (debugMode) Debug.Log("[DreamWaveTrigger] Audio principale avviato");
        }
        
        // ✅ AVVIA CAMERA SHAKE (con eventuale ritardo)
        if (enableCameraShake && earthquakeShakeController != null)
        {
            if (shakeDelay > 0)
            {
                yield return new WaitForSeconds(shakeDelay);
            }
            earthquakeShakeController.StartShake();
            if (debugMode) Debug.Log("[DreamWaveTrigger] Camera shake avviato");
        }
        
        // ✅ AVVIA DISSOLVE
        if ((firstMaterial != null || secondMaterial != null) && (firstObject != null || secondObject != null))
        {
            dissolveCoroutine = StartCoroutine(DissolveObjects());
        }
        
        // ========================
        // FASE 2: ATTESA
        // ========================
        
        float totalDuration = earthquakeDuration;
        if (!loopAudio && mainAudio != null)
        {
            totalDuration = Mathf.Min(earthquakeDuration, mainAudio.length);
        }
        
        yield return new WaitForSeconds(totalDuration - shakeDelay);
        
        // ========================
        // FASE 3: CONCLUSIONE
        // ========================
        
        EndSequence();
    }

    private IEnumerator DissolveObjects()
    {
        if (debugDissolve)
            Debug.Log("[DreamWaveTrigger] Avvio dissolve objects");
        
        isDissolving = true;
        float elapsedTime = 0f;
        bool objectsDeactivated = false;
        
        float duration = dissolveDuration;
        if (mainAudio != null && !loopAudio)
        {
            duration = Mathf.Min(dissolveDuration, mainAudio.length);
        }
        
        // Loop di dissolve
        while (elapsedTime < duration)
        {
            float normalizedTime = elapsedTime / duration;
            float dissolveValue = dissolveCurve.Evaluate(normalizedTime);
            
            if (firstMaterial != null && firstMaterial.HasProperty(dissolvePropertyName))
            {
                firstMaterial.SetFloat(dissolvePropertyName, dissolveValue);
            }
            
            if (secondMaterial != null && secondMaterial.HasProperty(dissolvePropertyName))
            {
                secondMaterial.SetFloat(dissolvePropertyName, dissolveValue);
            }
            
            // ✅ DISATTIVA OGGETTI QUANDO DISSOLVE RAGGIUNGE ~95% (più istantaneo)
            if (!objectsDeactivated && dissolveValue >= 0.95f)
            {
                if (firstObject != null) firstObject.SetActive(false);
                if (secondObject != null) secondObject.SetActive(false);
                
                // ✅ ATTIVA TRIGGER FALLING IMMEDIATAMENTE
                if (playerController != null)
                {
                    Animator playerAnimator = playerController.GetComponent<Animator>();
                    if (playerAnimator != null)
                    {
                        playerAnimator.SetTrigger("Falling");
                        if (debugMode) Debug.Log("[DreamWaveTrigger] Trigger 'Falling' attivato sul player");
                    }
                    else
                    {
                        Debug.LogWarning("[DreamWaveTrigger] Animator non trovato sul player per attivare trigger 'Falling'");
                    }
                }
                
                // ✅ ATTIVA OGGETTO SPECIFICATO (con eventuale delay)
                if (objectToActivate != null)
                {
                    if (activationDelay > 0)
                    {
                        StartCoroutine(ActivateObjectWithDelay());
                    }
                    else
                    {
                        objectToActivate.SetActive(true);
                        if (debugMode) Debug.Log($"[DreamWaveTrigger] Oggetto '{objectToActivate.name}' attivato");
                    }
                }
                
                objectsDeactivated = true;
                if (debugDissolve) Debug.Log("[DreamWaveTrigger] Oggetti disattivati al 95% del dissolve");
            }
            
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        
        // Dissolve completato - assicura valori finali
        if (firstMaterial != null && firstMaterial.HasProperty(dissolvePropertyName))
        {
            firstMaterial.SetFloat(dissolvePropertyName, 1f);
        }
        
        if (secondMaterial != null && secondMaterial.HasProperty(dissolvePropertyName))
        {
            secondMaterial.SetFloat(dissolvePropertyName, 1f);
        }
        
        // ✅ FALLBACK: Se gli oggetti non sono ancora stati disattivati, fallo ora
        if (!objectsDeactivated)
        {
            if (firstObject != null) firstObject.SetActive(false);
            if (secondObject != null) secondObject.SetActive(false);
            
            // Attiva trigger falling come fallback
            if (playerController != null)
            {
                Animator playerAnimator = playerController.GetComponent<Animator>();
                if (playerAnimator != null)
                {
                    playerAnimator.SetTrigger("Falling");
                    if (debugMode) Debug.Log("[DreamWaveTrigger] Trigger 'Falling' attivato sul player (fallback)");
                }
            }
            
            // ✅ ATTIVA OGGETTO COME FALLBACK
            if (objectToActivate != null && !objectToActivate.activeInHierarchy)
            {
                if (activationDelay > 0)
                {
                    StartCoroutine(ActivateObjectWithDelay());
                }
                else
                {
                    objectToActivate.SetActive(true);
                    if (debugMode) Debug.Log($"[DreamWaveTrigger] Oggetto '{objectToActivate.name}' attivato (fallback)");
                }
            }
            
            if (debugDissolve) Debug.Log("[DreamWaveTrigger] Oggetti disattivati (fallback finale)");
        }
        
        // ✅ FERMA SHAKE DOPO IL DISSOLVE (con ritardo)
        if (enableCameraShake && earthquakeShakeController != null)
        {
            if (shakeStopDelay > 0)
            {
                yield return new WaitForSeconds(shakeStopDelay);
            }
            earthquakeShakeController.StopShake();
            if (debugMode) Debug.Log("[DreamWaveTrigger] Camera shake fermato dopo dissolve");
        }
        
        isDissolving = false;
        dissolveCoroutine = null;
        
        if (debugDissolve)
            Debug.Log("[DreamWaveTrigger] Dissolve completato e shake fermato");
    }

    // ✅ METODO HELPER PER ATTIVAZIONE CON DELAY
    private IEnumerator ActivateObjectWithDelay()
    {
        yield return new WaitForSeconds(activationDelay);
        
        if (objectToActivate != null)
        {
            objectToActivate.SetActive(true);
            if (debugMode) Debug.Log($"[DreamWaveTrigger] Oggetto '{objectToActivate.name}' attivato dopo {activationDelay}s di delay");
        }
    }

    private void EndSequence()
    {
        if (debugMode)
            Debug.Log("[DreamWaveTrigger] Sequenza terminata");
        
        // Riabilita movimento del player
        if (disablePlayerMovement && playerController != null)
        {
            playerController.IsMovementLocked = false;
            if (debugMode) Debug.Log("[DreamWaveTrigger] Movimento player riabilitato");
        }
        
        // Ferma audio
        if (audioSource != null)
        {
            audioSource.Stop();
        }
        
        // Assicura che lo shake sia fermato
        if (enableCameraShake && earthquakeShakeController != null && earthquakeShakeController.IsShaking)
        {
            earthquakeShakeController.StopShake();
        }
        
        // Reset stati
        isAudioPlaying = false;
        mainCoroutine = null;
        playerController = null;
    }

    // ========================
    // METODI PUBBLICI
    // ========================
    
    public void TriggerSequence()
    {
        if (playerController == null)
        {
            ThirdPersonController controller = FindFirstObjectByType<ThirdPersonController>();
            if (controller != null)
            {
                playerController = controller;
            }
        }
        StartSequence();
    }
    
    public void StopSequence()
    {
        if (mainCoroutine != null)
        {
            StopCoroutine(mainCoroutine);
            EndSequence();
        }
        
        if (dissolveCoroutine != null)
        {
            StopCoroutine(dissolveCoroutine);
            dissolveCoroutine = null;
        }
        
        if (isAudioPlaying)
        {
            isAudioPlaying = false;
            if (audioSource != null) audioSource.Stop();
        }
        
        if (isDissolving)
        {
            isDissolving = false;
            ResetDissolveValues();
        }
    }
    
    public void ResetTrigger()
    {
        hasTriggered = false;
        ResetDissolveValues();
        if (debugMode)
            Debug.Log("[DreamWaveTrigger] Trigger resettato");
    }
    
    public void ResetDissolveValues()
    {
        if (firstMaterial != null && firstMaterial.HasProperty(dissolvePropertyName))
        {
            firstMaterial.SetFloat(dissolvePropertyName, 0f);
        }
        
        if (secondMaterial != null && secondMaterial.HasProperty(dissolvePropertyName))
        {
            secondMaterial.SetFloat(dissolvePropertyName, 0f);
        }
        
        if (firstObject != null) firstObject.SetActive(true);
        if (secondObject != null) secondObject.SetActive(true);
        
        // ✅ RESET ANCHE L'OGGETTO DA ATTIVARE
        if (objectToActivate != null) objectToActivate.SetActive(false);
        
        if (debugDissolve)
            Debug.Log("[DreamWaveTrigger] Valori dissolve resettati e oggetti riattivati");
    }
    
    // Proprietà pubbliche
    public bool IsAudioPlaying => isAudioPlaying;
    public bool IsDissolving => isDissolving;
    
    private void OnDestroy() => StopSequence();
    private void OnDisable() => StopSequence();
    
    private void OnValidate()
    {
        earthquakeDuration = Mathf.Max(0.1f, earthquakeDuration);
        audioVolume = Mathf.Clamp01(audioVolume);
        dissolveDuration = Mathf.Max(0.1f, dissolveDuration);
        shakeDelay = Mathf.Max(0f, shakeDelay);
        shakeStopDelay = Mathf.Max(0f, shakeStopDelay);
        activationDelay = Mathf.Max(0f, activationDelay); // ✅ VALIDAZIONE NUOVO CAMPO
        
        if (dissolveCurve == null || dissolveCurve.keys.Length == 0)
        {
            dissolveCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        }
    }
}