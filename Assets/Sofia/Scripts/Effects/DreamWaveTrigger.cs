using UnityEngine;
using Unity.Cinemachine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Trigger principale che coordina audio, dissolve e camera shake
/// Supporta multipli oggetti e materiali per il dissolve
/// Può essere usato sia con trigger che manualmente tramite chiamata di metodo
/// </summary>
public class DreamWaveTrigger : MonoBehaviour
{
    [Header("Trigger Settings")]
    [SerializeField] private bool useTriggerCollider = true; // Nuovo campo per abilitare/disabilitare il trigger
    [SerializeField] private bool onlyTriggerOnce = true;
    
    [Header("Earthquake Settings")]
    [SerializeField] private float earthquakeDuration = 3f;
    [SerializeField] private bool disablePlayerMovement = true;
    
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
    [SerializeField] private GameObject[] objectsToDissolve;
    [SerializeField] private Material[] materialsToDissolve;
    [SerializeField] private string dissolvePropertyName = "_Dissolve";
    [SerializeField] private float dissolveDuration = 3f;
    [SerializeField] private AnimationCurve dissolveCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    [SerializeField] private bool debugDissolve = false;
    
    [Header("Object Activation/Deactivation")]
    [SerializeField] private GameObject[] objectsToActivate;
    [SerializeField] private float activationDelay = 0f; // Ritardo prima di attivare gli oggetti
    [SerializeField] private GameObject[] objectsToDeactivate;
    [SerializeField] private float deactivationDelay = 0f; // Ritardo prima di disattivare gli oggetti
    
    [Header("Debug")]
    [SerializeField] private bool debugMode = false;
    
    // Componenti e stati
    private ThirdPersonController playerController;
    private bool hasTriggered = false;
    private bool isAudioPlaying = false;
    private bool isDissolving = false;
    private Coroutine mainCoroutine;
    private Coroutine dissolveCoroutine;
    private List<Material> validMaterials = new List<Material>();

    private void Awake()
    {
        SetupTrigger();
        SetupAudioSource();
        SetupDissolveMaterials();
        FindShakeController();
    }

    private void SetupTrigger()
    {
        // Setup del trigger solo se abilitato
        if (useTriggerCollider)
        {
            BoxCollider boxCollider = GetComponent<BoxCollider>();
            if (boxCollider == null)
            {
                boxCollider = gameObject.AddComponent<BoxCollider>();
                Debug.LogWarning($"[DreamWaveTrigger] BoxCollider mancante su {name}. Aggiunto automaticamente.");
            }
            boxCollider.isTrigger = true;
        }
        else
        {
            // Se il trigger è disabilitato, rimuovi o disabilita il BoxCollider se presente
            BoxCollider boxCollider = GetComponent<BoxCollider>();
            if (boxCollider != null && boxCollider.isTrigger)
            {
                if (debugMode)
                    Debug.Log($"[DreamWaveTrigger] Trigger disabilitato - BoxCollider rimosso da {name}");
                DestroyImmediate(boxCollider);
            }
        }
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
        validMaterials.Clear();
        
        if (materialsToDissolve != null)
        {
            for (int i = 0; i < materialsToDissolve.Length; i++)
            {
                Material material = materialsToDissolve[i];
                if (material != null)
                {
                    if (material.HasProperty(dissolvePropertyName))
                    {
                        material.SetFloat(dissolvePropertyName, 0f);
                        validMaterials.Add(material);
                        if (debugDissolve)
                            Debug.Log($"[DreamWaveTrigger] Materiale '{material.name}' inizializzato (indice {i})");
                    }
                    else
                    {
                        Debug.LogWarning($"[DreamWaveTrigger] Il materiale '{material.name}' (indice {i}) non ha la proprietà '{dissolvePropertyName}'");
                    }
                }
            }
        }
        
        if (debugDissolve)
            Debug.Log($"[DreamWaveTrigger] {validMaterials.Count} materiali validi trovati per il dissolve");
    }

    private void OnTriggerEnter(Collider other)
    {
        // Funziona solo se il trigger è abilitato
        if (!useTriggerCollider) return;
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

    // ========================
    // METODI PUBBLICI PER ATTIVAZIONE MANUALE
    // ========================
    
    /// <summary>
    /// Attiva la sequenza manualmente senza bisogno del trigger
    /// Utile per eventi come la raccolta di un regalo
    /// </summary>
    public void TriggerSequenceManually()
    {
        // Verifica se può essere attivato
        if (onlyTriggerOnce && hasTriggered)
        {
            if (debugMode)
                Debug.LogWarning($"[DreamWaveTrigger] Tentativo di attivare {name} che può essere attivato solo una volta");
            return;
        }
        
        if (isAudioPlaying)
        {
            if (debugMode)
                Debug.LogWarning($"[DreamWaveTrigger] Tentativo di attivare {name} ma è già in esecuzione");
            return;
        }
        
        // Trova automaticamente il player controller se non è già impostato
        if (playerController == null)
        {
            ThirdPersonController controller = FindFirstObjectByType<ThirdPersonController>();
            if (controller != null)
            {
                playerController = controller;
            }
            else
            {
                Debug.LogError($"[DreamWaveTrigger] ThirdPersonController non trovato nella scena!");
                return;
            }
        }
        
        if (debugMode)
            Debug.Log($"[DreamWaveTrigger] Sequenza attivata manualmente: {name}");
        
        StartSequence();
    }
    
    /// <summary>
    /// Versione alternativa che accetta un riferimento specifico al player controller
    /// </summary>
    public void TriggerSequenceManually(ThirdPersonController specificPlayerController)
    {
        // Verifica se può essere attivato
        if (onlyTriggerOnce && hasTriggered)
        {
            if (debugMode)
                Debug.LogWarning($"[DreamWaveTrigger] Tentativo di attivare {name} che può essere attivato solo una volta");
            return;
        }
        
        if (isAudioPlaying)
        {
            if (debugMode)
                Debug.LogWarning($"[DreamWaveTrigger] Tentativo di attivare {name} ma è già in esecuzione");
            return;
        }
        
        if (specificPlayerController == null)
        {
            Debug.LogError($"[DreamWaveTrigger] PlayerController fornito è null!");
            return;
        }
        
        playerController = specificPlayerController;
        
        if (debugMode)
            Debug.Log($"[DreamWaveTrigger] Sequenza attivata manualmente con player specifico: {name}");
        
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
        if ((validMaterials.Count > 0) && (objectsToDissolve != null && objectsToDissolve.Length > 0))
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
            Debug.Log($"[DreamWaveTrigger] Avvio dissolve per {validMaterials.Count} materiali e {objectsToDissolve.Length} oggetti");
        
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
            
            // ✅ APPLICA DISSOLVE A TUTTI I MATERIALI VALIDI
            foreach (Material material in validMaterials)
            {
                if (material != null)
                {
                    material.SetFloat(dissolvePropertyName, dissolveValue);
                }
            }
            
            // ✅ DISATTIVA OGGETTI QUANDO DISSOLVE RAGGIUNGE ~95% (più istantaneo)
            if (!objectsDeactivated && dissolveValue >= 0.95f)
            {
                // Disattiva tutti gli oggetti da dissolvere
                if (objectsToDissolve != null)
                {
                    foreach (GameObject obj in objectsToDissolve)
                    {
                        if (obj != null)
                        {
                            obj.SetActive(false);
                            if (debugDissolve) Debug.Log($"[DreamWaveTrigger] Oggetto '{obj.name}' disattivato");
                        }
                    }
                }
                
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
                
                // ✅ DISATTIVA OGGETTI SPECIFICATI (con eventuale delay)
                if (objectsToDeactivate != null && objectsToDeactivate.Length > 0)
                {
                    if (deactivationDelay > 0)
                    {
                        StartCoroutine(DeactivateObjectsWithDelay());
                    }
                    else
                    {
                        DeactivateObjects();
                    }
                }
                
                // ✅ ATTIVA OGGETTI SPECIFICATI (con eventuale delay)
                if (objectsToActivate != null && objectsToActivate.Length > 0)
                {
                    if (activationDelay > 0)
                    {
                        StartCoroutine(ActivateObjectsWithDelay());
                    }
                    else
                    {
                        ActivateObjects();
                    }
                }
                
                objectsDeactivated = true;
                if (debugDissolve) Debug.Log("[DreamWaveTrigger] Oggetti gestiti al 95% del dissolve");
            }
            
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        
        // Dissolve completato - assicura valori finali
        foreach (Material material in validMaterials)
        {
            if (material != null)
            {
                material.SetFloat(dissolvePropertyName, 1f);
            }
        }
        
        // ✅ FALLBACK: Se gli oggetti non sono ancora stati gestiti, fallo ora
        if (!objectsDeactivated)
        {
            // Disattiva oggetti
            if (objectsToDissolve != null)
            {
                foreach (GameObject obj in objectsToDissolve)
                {
                    if (obj != null)
                    {
                        obj.SetActive(false);
                        if (debugDissolve) Debug.Log($"[DreamWaveTrigger] Oggetto '{obj.name}' disattivato (fallback)");
                    }
                }
            }
            
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
            
            // ✅ DISATTIVA OGGETTI COME FALLBACK
            if (objectsToDeactivate != null && objectsToDeactivate.Length > 0)
            {
                if (deactivationDelay > 0)
                {
                    StartCoroutine(DeactivateObjectsWithDelay());
                }
                else
                {
                    DeactivateObjects();
                }
            }
            
            // ✅ ATTIVA OGGETTI COME FALLBACK
            if (objectsToActivate != null && objectsToActivate.Length > 0)
            {
                if (activationDelay > 0)
                {
                    StartCoroutine(ActivateObjectsWithDelay());
                }
                else
                {
                    ActivateObjects();
                }
            }
            
            if (debugDissolve) Debug.Log("[DreamWaveTrigger] Oggetti gestiti (fallback finale)");
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

    // ✅ METODI HELPER PER ATTIVAZIONE/DISATTIVAZIONE OGGETTI
    private void ActivateObjects()
    {
        if (objectsToActivate != null)
        {
            foreach (GameObject obj in objectsToActivate)
            {
                if (obj != null)
                {
                    obj.SetActive(true);
                    if (debugMode) Debug.Log($"[DreamWaveTrigger] Oggetto '{obj.name}' attivato");
                }
            }
        }
    }
    
    private void DeactivateObjects()
    {
        if (objectsToDeactivate != null)
        {
            foreach (GameObject obj in objectsToDeactivate)
            {
                if (obj != null)
                {
                    obj.SetActive(false);
                    if (debugMode) Debug.Log($"[DreamWaveTrigger] Oggetto '{obj.name}' disattivato");
                }
            }
        }
    }
    
    private IEnumerator ActivateObjectsWithDelay()
    {
        yield return new WaitForSeconds(activationDelay);
        
        ActivateObjects();
        
        if (debugMode) 
            Debug.Log($"[DreamWaveTrigger] {objectsToActivate?.Length ?? 0} oggetti attivati dopo {activationDelay}s di delay");
    }
    
    private IEnumerator DeactivateObjectsWithDelay()
    {
        yield return new WaitForSeconds(deactivationDelay);
        
        DeactivateObjects();
        
        if (debugMode) 
            Debug.Log($"[DreamWaveTrigger] {objectsToDeactivate?.Length ?? 0} oggetti disattivati dopo {deactivationDelay}s di delay");
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
    // METODI PUBBLICI LEGACY (mantenuti per compatibilità)
    // ========================
    
    [System.Obsolete("Usa TriggerSequenceManually() invece")]
    public void TriggerSequence()
    {
        TriggerSequenceManually();
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
        // Reset materiali
        foreach (Material material in validMaterials)
        {
            if (material != null)
            {
                material.SetFloat(dissolvePropertyName, 0f);
            }
        }
        
        // Riattiva oggetti da dissolvere
        if (objectsToDissolve != null)
        {
            foreach (GameObject obj in objectsToDissolve)
            {
                if (obj != null)
                {
                    obj.SetActive(true);
                }
            }
        }
        
        // ✅ RESET ANCHE GLI OGGETTI DA ATTIVARE E DISATTIVARE
        if (objectsToActivate != null)
        {
            foreach (GameObject obj in objectsToActivate)
            {
                if (obj != null)
                {
                    obj.SetActive(false);
                }
            }
        }
        
        if (objectsToDeactivate != null)
        {
            foreach (GameObject obj in objectsToDeactivate)
            {
                if (obj != null)
                {
                    obj.SetActive(true);
                }
            }
        }
        
        if (debugDissolve)
            Debug.Log("[DreamWaveTrigger] Valori dissolve resettati e oggetti ripristinati");
    }
    
    // ✅ METODI DI UTILITÀ PER GESTIRE ARRAY DINAMICAMENTE
    public void AddObjectToDissolve(GameObject obj)
    {
        if (obj == null) return;
        
        var list = new System.Collections.Generic.List<GameObject>();
        if (objectsToDissolve != null)
            list.AddRange(objectsToDissolve);
        
        if (!list.Contains(obj))
        {
            list.Add(obj);
            objectsToDissolve = list.ToArray();
            if (debugMode) Debug.Log($"[DreamWaveTrigger] Oggetto '{obj.name}' aggiunto alla lista dissolve");
        }
    }
    
    public void AddMaterialToDissolve(Material material)
    {
        if (material == null) return;
        
        var list = new System.Collections.Generic.List<Material>();
        if (materialsToDissolve != null)
            list.AddRange(materialsToDissolve);
        
        if (!list.Contains(material))
        {
            list.Add(material);
            materialsToDissolve = list.ToArray();
            
            // Aggiorna anche la lista dei materiali validi
            if (material.HasProperty(dissolvePropertyName))
            {
                material.SetFloat(dissolvePropertyName, 0f);
                if (!validMaterials.Contains(material))
                {
                    validMaterials.Add(material);
                }
                if (debugMode) Debug.Log($"[DreamWaveTrigger] Materiale '{material.name}' aggiunto alla lista dissolve");
            }
            else
            {
                Debug.LogWarning($"[DreamWaveTrigger] Il materiale '{material.name}' non ha la proprietà '{dissolvePropertyName}'");
            }
        }
    }
    
    public void AddObjectToActivate(GameObject obj)
    {
        if (obj == null) return;
        
        var list = new System.Collections.Generic.List<GameObject>();
        if (objectsToActivate != null)
            list.AddRange(objectsToActivate);
        
        if (!list.Contains(obj))
        {
            list.Add(obj);
            objectsToActivate = list.ToArray();
            if (debugMode) Debug.Log($"[DreamWaveTrigger] Oggetto '{obj.name}' aggiunto alla lista attivazione");
        }
    }
    
    public void AddObjectToDeactivate(GameObject obj)
    {
        if (obj == null) return;
        
        var list = new System.Collections.Generic.List<GameObject>();
        if (objectsToDeactivate != null)
            list.AddRange(objectsToDeactivate);
        
        if (!list.Contains(obj))
        {
            list.Add(obj);
            objectsToDeactivate = list.ToArray();
            if (debugMode) Debug.Log($"[DreamWaveTrigger] Oggetto '{obj.name}' aggiunto alla lista disattivazione");
        }
    }
    
    // Proprietà pubbliche
    public bool IsAudioPlaying => isAudioPlaying;
    public bool IsDissolving => isDissolving;
    public bool HasTriggered => hasTriggered;
    public bool UseTriggerCollider => useTriggerCollider;
    public int DissolveObjectsCount => objectsToDissolve?.Length ?? 0;
    public int DissolveMaterialsCount => materialsToDissolve?.Length ?? 0;
    public int ValidMaterialsCount => validMaterials.Count;
    public int ActivateObjectsCount => objectsToActivate?.Length ?? 0;
    public int DeactivateObjectsCount => objectsToDeactivate?.Length ?? 0;
    
    private void OnDestroy() => StopSequence();
    private void OnDisable() => StopSequence();
    
    private void OnValidate()
    {
        earthquakeDuration = Mathf.Max(0.1f, earthquakeDuration);
        audioVolume = Mathf.Clamp01(audioVolume);
        dissolveDuration = Mathf.Max(0.1f, dissolveDuration);
        shakeDelay = Mathf.Max(0f, shakeDelay);
        shakeStopDelay = Mathf.Max(0f, shakeStopDelay);
        activationDelay = Mathf.Max(0f, activationDelay);
        deactivationDelay = Mathf.Max(0f, deactivationDelay);
        
        if (dissolveCurve == null || dissolveCurve.keys.Length == 0)
        {
            dissolveCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        }
        
        // Riconvalida i materiali quando si modificano nell'inspector
        if (Application.isPlaying)
        {
            SetupDissolveMaterials();
        }
        
        // Aggiorna il setup del trigger quando cambia l'impostazione
        if (Application.isPlaying)
        {
            SetupTrigger();
        }
    }
}