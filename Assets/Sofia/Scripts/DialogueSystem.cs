using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using TMPro;
using System;

[System.Serializable]
public class DialogueLine
{
    [TextArea(2, 5)]
    public string text;
    public AudioClip audioClip; // Audio opzionale per questa linea
}

[System.Serializable]
public class DissolveObject
{
    [Header("Oggetto da Dissolvere")]
    [Tooltip("L'oggetto che avrà l'effetto dissolve")]
    public GameObject targetObject;
    
    [Header("Materiali con Dissolve")]
    [Tooltip("Lista dei materiali che hanno la proprietà '_Dissolve' o simile")]
    public Material[] dissolveMaterials;
    
    [Header("Proprietà Dissolve")]
    [Tooltip("Nome della proprietà dissolve nei materiali (es: '_Dissolve', '_DissolveAmount')")]
    public string dissolvePropertyName = "_Dissolve";
    
    [Header("Timing")]
    [Tooltip("Durata dell'effetto dissolve in secondi")]
    [Range(0.1f, 10f)]
    public float dissolveDuration = 2f;
    
    [Tooltip("Ritardo prima di iniziare l'effetto dissolve")]
    [Range(0f, 5f)]
    public float dissolveDelay = 0f;
    
    [Header("Mesh Collider")]
    [Tooltip("Se true, attiverà il MeshCollider alla fine dell'effetto")]
    public bool enableMeshColliderAfterDissolve = true;
    
    [Tooltip("Ritardo aggiuntivo prima di attivare il collider (dopo fine dissolve)")]
    [Range(0f, 2f)]
    public float colliderActivationDelay = 0f;
}

public class DialogueSystem : MonoBehaviour
{
    [Header("UI Components")]
    public GameObject dialogueUI; // L'elemento UI che contiene il dialogo
    public TextMeshProUGUI dialogueText; // Il TextMeshPro che mostrerà il testo
    
    [Header("Dialogue Settings")]
    public DialogueLine[] dialogueLines; // Array di linee con testo e audio opzionale
    public bool canRepeatDialogue = false; // Se può essere attivato più volte
    public bool autoCloseOnExit = false; // Se true, chiude il dialogo quando esci dalla zona
    public bool autoFinishLastLine = true; // Se true, l'ultima battuta finisce automaticamente
    public float autoFinishDelay = 2f; // Tempo di attesa per ultima battuta senza audio
    
    [Header("🆕 Sottodialogo System")]
    [SerializeField] private bool enableSubDialogue = false;
    [Tooltip("DialogueSystem che verrà attivato alla fine di questo dialogo")]
    [SerializeField] private DialogueSystem subDialogueSystem;
    [Tooltip("Ritardo prima di attivare il sottodialogo (in secondi)")]
    [SerializeField] private float subDialogueDelay = 0.5f;
    [Tooltip("Messaggio da loggare quando viene attivato il sottodialogo")]
    [SerializeField] private string subDialogueMessage = "Attivando sottodialogo...";
    
    [Header("🆕 Dissolve System")]
    [SerializeField] private bool enableDissolveEffect = false;
    [Tooltip("Lista degli oggetti da dissolvere alla fine del dialogo")]
    [SerializeField] private DissolveObject[] dissolveObjects;
    [Tooltip("Se true, avvia il dissolve insieme al sottodialogo")]
    [SerializeField] private bool dissolveWithSubDialogue = true;
    [Header("🆕 Dissolve Audio")]
[SerializeField] private bool enableDissolveAudio = false;
[Tooltip("AudioSource che verrà riprodotto durante l'effetto dissolve")]
[SerializeField] private AudioSource dissolveAudioSource;
[Tooltip("AudioClip da riprodurre durante il dissolve")]
[SerializeField] private AudioClip dissolveAudioClip;
[Tooltip("Se true, ferma l'audio quando tutti i dissolve sono completati")]
[SerializeField] private bool stopAudioOnDissolveComplete = true;
    [Tooltip("Tempo di fade out per l'audio dissolve (secondi)")]
    [Range(0f, 2f)]
    [SerializeField] private float dissolveAudioFadeTime = 0.5f;
[Header("🎮 Player Movement Control")]
[SerializeField] private bool lockPlayerMovement = false;
[Tooltip("Se true, blocca completamente i controlli del player durante questo dialogo")]
[SerializeField] private bool lockMovementDuringDialogue = false;
[Tooltip("Ritardo prima di bloccare il movimento (utile per transizioni fluide)")]
[Range(0f, 2f)]
[SerializeField] private float movementLockDelay = 0f;
[Tooltip("Se true, blocca anche la rotazione della camera")]
[SerializeField] private bool lockCameraRotation = false;
[Tooltip("Player Controller da controllare (se null, verrà cercato automaticamente)")]
[SerializeField] private ThirdPersonController targetPlayerController;

// Variabili private per gestire l'audio dissolve
private Coroutine dissolveAudioCoroutine;
private bool dissolveAudioPlaying = false;
    
    [Header("🆕 Dissolve Initialization")]
    [Tooltip("Se true, forza tutti i materiali dissolve a valore 1 all'avvio (oggetti completamente visibili)")]
    [SerializeField] private bool forceInitializeDissolveValues = true;
    [Tooltip("Valore di dissolve da impostare all'avvio (1 = completamente visibile, 0 = completamente dissolto)")]
    [Range(0f, 1f)]
    [SerializeField] private float initialDissolveValue = 1f;
    
    [Header("Object Activation System")]
    [SerializeField] private bool enableObjectActivation = false;
    [Tooltip("Oggetto che verrà attivato alla fine del dialogo")]
    [SerializeField] private GameObject objectToEnable;
    [Tooltip("Oggetto che verrà disattivato alla fine del dialogo")]
    [SerializeField] private GameObject objectToDisable;
    [Tooltip("Lista di oggetti da attivare alla fine del dialogo")]
    [SerializeField] private GameObject[] objectsToEnable;
    [Tooltip("Lista di oggetti da disattivare alla fine del dialogo")]
    [SerializeField] private GameObject[] objectsToDisable;
    [Tooltip("Messaggio da loggare quando gli oggetti vengono attivati")]
    [SerializeField] private string activationMessage = "Oggetti attivati dal dialogo!";
    [Tooltip("Ritardo prima di attivare gli oggetti (in secondi)")]
    [SerializeField] private float activationDelay = 0f;
    private bool wasMovementLocked = false;
private bool movementLockApplied = false;
private Coroutine movementLockCoroutine = null;
    
    [Header("Audio Settings")]
    public AudioSource audioSource; // AudioSource per riprodurre i suoni del dialogo
    public float audioFadeOutTime = 0.2f; // Tempo per il fade out dell'audio quando si skippa
    
    [Header("Input Settings")]
    public KeyCode nextLineKey = KeyCode.Space; // Tasto per passare alla battuta successiva
    public KeyCode cancelDialogueKey = KeyCode.Escape; // Tasto per chiudere manualmente il dialogo
    
    [Header("Player Control")]
    public ThirdPersonController playerController; // Reference al ThirdPersonController
    
    [Header("Events")]
    public UnityEvent OnDialogueStarted; // Quando inizia il dialogo
    public UnityEvent OnDialogueEnded; // Quando finisce il dialogo
    public UnityEvent OnLastLineReached; // Quando viene mostrata l'ultima battuta
    public UnityEvent OnLastLineFinished; // Quando finisce l'ultima battuta
    public UnityEvent OnObjectsActivated; // Quando gli oggetti vengono attivati
    public UnityEvent OnSubDialogueTriggered; // 🆕 Quando viene attivato un sottodialogo
    public UnityEvent OnDissolveStarted; // 🆕 Quando inizia l'effetto dissolve
    public UnityEvent OnDissolveCompleted; // 🆕 Quando finisce l'effetto dissolve
    
    // Eventi statici per comunicazione globale con gli NPC
    public static event Action<DialogueSystem> OnAnyDialogueStarted;
    public static event Action<DialogueSystem> OnAnyDialogueEnded; 
    public static event Action<DialogueSystem> OnAnyLastLineFinished;
    public static event Action<DialogueSystem> OnAnyObjectsActivated;
    public static event Action<DialogueSystem> OnAnySubDialogueTriggered; // 🆕
    public static event Action<DialogueSystem> OnAnyDissolveStarted; // 🆕
    public static event Action<DialogueSystem> OnAnyDissolveCompleted; // 🆕
    
    private int currentLineIndex = 0;
    private bool isDialogueActive = false;
    private bool isPlayingAudio = false;
    private bool hasBeenTriggered = false; // Per evitare ripetizioni
    private bool isOnLastLine = false; // Flag per tracciare se siamo sull'ultima battuta
    private bool objectsAlreadyActivated = false; // Per evitare attivazioni multiple
    private bool subDialogueTriggered = false; // 🆕 Per evitare attivazioni multiple del sottodialogo
    private bool dissolveTriggered = false; // 🆕 Per evitare attivazioni multiple del dissolve
    private Coroutine audioCoroutine;
    private Coroutine autoFinishCoroutine;
    private List<Coroutine> dissolveCoroutines = new List<Coroutine>(); // 🆕 Lista delle coroutine di dissolve attive
    
    void Start()
    {
        // Assicurati che l'UI sia nascosta all'inizio
        if (dialogueUI != null)
            dialogueUI.SetActive(false);
            
        // Crea un AudioSource se non è assegnato
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }
        
        // Valida il setup degli oggetti
        ValidateObjectActivationSetup();
        ValidateSubDialogueSetup(); // 🆕
        ValidateDissolveSetup(); // 🆕
        ValidateDissolveAudioSetup();
        ValidateMovementControlSetup();
        
        // 🆕 INIZIALIZZA I VALORI DISSOLVE ALL'AVVIO
        if (enableDissolveEffect && forceInitializeDissolveValues)
        {
            InitializeDissolveValues();
        }
    }
    /// <summary>
/// 🆕 Abilita/disabilita l'audio dissolve
/// </summary>
public void SetDissolveAudioEnabled(bool enabled)
{
    enableDissolveAudio = enabled;
    Debug.Log($"[DialogueSystem] Audio Dissolve {(enabled ? "abilitato" : "disabilitato")}");
}

/// <summary>
/// 🆕 Imposta l'AudioClip per il dissolve
/// </summary>
public void SetDissolveAudioClip(AudioClip clip)
{
    dissolveAudioClip = clip;
    Debug.Log($"[DialogueSystem] AudioClip dissolve impostato: {(clip != null ? clip.name : "NULL")}");
}

/// <summary>
/// 🆕 Imposta l'AudioSource per il dissolve
/// </summary>
public void SetDissolveAudioSource(AudioSource source)
{
    dissolveAudioSource = source;
    Debug.Log($"[DialogueSystem] AudioSource dissolve impostato: {(source != null ? source.name : "NULL")}");
}

/// <summary>
/// 🆕 Forza stop dell'audio dissolve
/// </summary>
public void ForceStopDissolveAudio()
{
    StopDissolveAudio();
}
    void ValidateMovementControlSetup()
    {
        if (!lockMovementDuringDialogue)
        {
            Debug.Log("[DialogueSystem] Player Movement Lock disabilitato");
            return;
        }

        // Cerca automaticamente il ThirdPersonController se non assegnato
        if (targetPlayerController == null)
        {
            targetPlayerController = FindFirstObjectByType<ThirdPersonController>();

            if (targetPlayerController == null)
            {
                Debug.LogWarning("[DialogueSystem] ⚠️ Movement Lock abilitato ma nessun ThirdPersonController trovato!");
                lockMovementDuringDialogue = false;
                return;
            }
            else
            {
                Debug.Log($"[DialogueSystem] 🎮 ThirdPersonController trovato automaticamente: {targetPlayerController.name}");
            }
        }

        Debug.Log($"[DialogueSystem] ✅ Player Movement Lock configurato per: {targetPlayerController.name}");
    }
    void LockPlayerMovement()
    {
        if (!lockMovementDuringDialogue || targetPlayerController == null || movementLockApplied)
        {
            return;
        }

        Debug.Log("[DialogueSystem] 🔒 Bloccando movimento del player...");

        // Salva lo stato precedente
        wasMovementLocked = targetPlayerController.IsMovementLocked;

        // Blocca il movimento
        targetPlayerController.IsMovementLocked = true;
        movementLockApplied = true;

        // TODO: Se implementato in futuro, blocca anche la rotazione camera
        if (lockCameraRotation)
        {
            Debug.Log("[DialogueSystem] 📷 Camera rotation lock non ancora implementato");
            // Qui potresti aggiungere codice per bloccare la rotazione della camera
        }

        Debug.Log($"[DialogueSystem] ✅ Movimento bloccato! (Era già bloccato: {wasMovementLocked})");
    }
    void UnlockPlayerMovement()
    {
        if (!lockMovementDuringDialogue || targetPlayerController == null || !movementLockApplied)
        {
            return;
        }

        Debug.Log("[DialogueSystem] 🔓 Sbloccando movimento del player...");

        // Ripristina lo stato precedente solo se non era già bloccato prima
        if (!wasMovementLocked)
        {
            targetPlayerController.IsMovementLocked = false;
        }

        movementLockApplied = false;

        // Sblocca camera se era bloccata
        if (lockCameraRotation)
        {
            Debug.Log("[DialogueSystem] 📷 Camera rotation unlock non ancora implementato");
            // Qui potresti aggiungere codice per sbloccare la rotazione della camera
        }

        Debug.Log($"[DialogueSystem] ✅ Movimento sbloccato! (Stato precedente: {(wasMovementLocked ? "bloccato" : "libero")})");
    }
IEnumerator LockPlayerMovementWithDelay()
{
    if (movementLockDelay > 0f)
    {
        Debug.Log($"[DialogueSystem] ⏳ Aspettando {movementLockDelay}s prima di bloccare movimento...");
        yield return new WaitForSeconds(movementLockDelay);
    }
    
    LockPlayerMovement();
    movementLockCoroutine = null;
}

/// <summary>
/// 🆕 Controlla se l'audio dissolve è in riproduzione
/// </summary>
public bool IsDissolveAudioPlaying() => dissolveAudioPlaying;
    // 🆕 NUOVO METODO: Inizializza i valori dissolve all'avvio
    /// <summary>
    /// 🆕 Inizializza tutti i materiali dissolve al valore specificato all'avvio della scena
    /// </summary>
    void InitializeDissolveValues()
    {
        if (dissolveObjects == null || dissolveObjects.Length == 0)
        {
            Debug.LogWarning("[DialogueSystem] ⚠️ Nessun oggetto dissolve configurato per l'inizializzazione!");
            return;
        }
        
        Debug.Log($"[DialogueSystem] 🔧 INIZIALIZZAZIONE VALORI DISSOLVE (valore: {initialDissolveValue})");
        
        int totalMaterialsProcessed = 0;
        int totalMaterialsInitialized = 0;
        
        for (int i = 0; i < dissolveObjects.Length; i++)
        {
            DissolveObject dissolveObj = dissolveObjects[i];
            
            if (dissolveObj.targetObject == null)
            {
                Debug.LogWarning($"[DialogueSystem] ⚠️ Target Object NULL per dissolve #{i} - skip inizializzazione");
                continue;
            }
            
            if (dissolveObj.dissolveMaterials == null || dissolveObj.dissolveMaterials.Length == 0)
            {
                Debug.LogWarning($"[DialogueSystem] ⚠️ Nessun materiale configurato per '{dissolveObj.targetObject.name}' - skip inizializzazione");
                continue;
            }
            
            Debug.Log($"[DialogueSystem] 🔧 Inizializzando dissolve per '{dissolveObj.targetObject.name}'...");
            
            foreach (Material mat in dissolveObj.dissolveMaterials)
            {
                totalMaterialsProcessed++;
                
                if (mat != null && mat.HasProperty(dissolveObj.dissolvePropertyName))
                {
                    try
                    {
                        float currentValue = mat.GetFloat(dissolveObj.dissolvePropertyName);
                        mat.SetFloat(dissolveObj.dissolvePropertyName, initialDissolveValue);
                        totalMaterialsInitialized++;
                        
                        Debug.Log($"[DialogueSystem] ✅ Materiale '{mat.name}': {currentValue} → {initialDissolveValue}");
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"[DialogueSystem] ❌ Errore nell'inizializzare materiale '{mat.name}': {e.Message}");
                    }
                }
                else if (mat != null)
                {
                    Debug.LogWarning($"[DialogueSystem] ⚠️ Materiale '{mat.name}' NON ha proprietà '{dissolveObj.dissolvePropertyName}'!");
                }
                else
                {
                    Debug.LogWarning($"[DialogueSystem] ⚠️ Materiale NULL nell'oggetto '{dissolveObj.targetObject.name}'!");
                }
            }
        }
        
        Debug.Log($"[DialogueSystem] 🎉 Inizializzazione dissolve completata: {totalMaterialsInitialized}/{totalMaterialsProcessed} materiali processati");
    }
    
    void Update()
    {
        // Gestione input durante il dialogo
        if (isDialogueActive)
        {
            // Avanza il dialogo con Space
            if (Input.GetKeyDown(nextLineKey))
            {
                Debug.Log($"[DialogueSystem] Space premuto. Ultima linea: {isOnLastLine}");
                
                // Se siamo sull'ultima linea, notifica la fine PRIMA di NextLine()
                if (isOnLastLine)
                {
                    Debug.Log("[DialogueSystem] 🚀 SPACE premuto sull'ultima battuta - notificare fine dialogo!");
                    NotifyLastLineFinished();
                }
                
                // Ferma audio se in riproduzione
                if (isPlayingAudio)
                {
                    StopCurrentAudio();
                }
                
                // Ferma auto-finish se attivo
                if (autoFinishCoroutine != null)
                {
                    StopCoroutine(autoFinishCoroutine);
                    autoFinishCoroutine = null;
                }
                
                // Passa alla linea successiva (che chiuderà il dialogo se era l'ultima)
                NextLine();
            }
            
            // Permette di chiudere manualmente il dialogo con Escape
            if (Input.GetKeyDown(cancelDialogueKey))
            {
                EndDialogue();
            }
        }
    }
    void OnApplicationPause(bool pauseStatus)
{
    if (pauseStatus && enableDissolveEffect)
    {
        ResetDissolveToInitialState();
    }
}

void OnApplicationFocus(bool hasFocus)
{
    if (!hasFocus && enableDissolveEffect)
    {
        ResetDissolveToInitialState();
    }
}

void ResetDissolveToInitialState()
{
    SetAllDissolveValues(1f);
    Debug.Log("[DialogueSystem] 🔄 Materiali dissolve ripristinati allo stato iniziale");
}
    
    void OnTriggerEnter(Collider other)
    {
        // Controlla se l'oggetto che entra nel trigger è il player
        if (other.CompareTag("Player") && (!hasBeenTriggered || canRepeatDialogue))
        {
            StartDialogue();
        }
    }
    
    void OnTriggerExit(Collider other)
    {
        // Solo se autoCloseOnExit è attivo, chiude il dialogo
        if (other.CompareTag("Player") && autoCloseOnExit)
        {
            EndDialogue();
        }
        
        // Log per debug
        if (other.CompareTag("Player") && !autoCloseOnExit)
        {
            Debug.Log("[DialogueSystem] Player uscito dalla zona, ma il dialogo continua...");
        }
    }
    
   public void StartDialogue()
{
    Debug.Log($"[DEBUG] StartDialogue chiamato. DialogueLines.Length = {dialogueLines.Length}");
    
    if (dialogueLines.Length == 0) 
    {
        Debug.LogError("[DEBUG] Nessuna DialogueLine configurata!");
        return;
    }
    
    Debug.Log($"[DEBUG] Prima linea: '{dialogueLines[0].text}'");
    Debug.Log($"[DEBUG] DialogueUI assigned: {dialogueUI != null}");
    Debug.Log($"[DEBUG] DialogueText assigned: {dialogueText != null}");
    
    isDialogueActive = true;
    hasBeenTriggered = true;
    currentLineIndex = 0;
    isOnLastLine = false;
    objectsAlreadyActivated = false;
    subDialogueTriggered = false;
    dissolveTriggered = false;
    movementLockApplied = false; // 🎮 RESET STATO LOCK
    
    // Attiva l'UI del dialogo
    if (dialogueUI != null)
    {
        dialogueUI.SetActive(true);
        Debug.Log("[DEBUG] DialogueUI attivato");
    }
    
    // 🎮 BLOCCA MOVIMENTO DEL PLAYER SE ABILITATO
    if (lockMovementDuringDialogue)
    {
        if (movementLockDelay > 0f)
        {
            movementLockCoroutine = StartCoroutine(LockPlayerMovementWithDelay());
        }
        else
        {
            LockPlayerMovement();
        }
    }
    
    // Mostra la prima battuta
    DisplayLine();
    
    // Notifica inizio dialogo
    OnDialogueStarted?.Invoke();
    OnAnyDialogueStarted?.Invoke(this);
    
    Debug.Log("[DialogueSystem] Dialogo iniziato. Usa " + nextLineKey + " per continuare, " + cancelDialogueKey + " per chiudere.");
}

    void DisplayLine()
    {
        Debug.Log($"[DEBUG] DisplayLine chiamato. CurrentLineIndex = {currentLineIndex}");
        
        if (currentLineIndex < dialogueLines.Length)
        {
            DialogueLine currentLine = dialogueLines[currentLineIndex];
            string lineToShow = currentLine.text;
            
            // Controlla se siamo sull'ultima battuta
            isOnLastLine = (currentLineIndex == dialogueLines.Length - 1);
            if (isOnLastLine)
            {
                Debug.Log($"[DialogueSystem] 🎯 ULTIMA BATTUTA RAGGIUNTA: '{lineToShow}' - Finirà automaticamente se autoFinishLastLine = {autoFinishLastLine}");
                OnLastLineReached?.Invoke();
            }
            
            Debug.Log($"[DEBUG] Testo da mostrare: '{lineToShow}' (Ultima linea: {isOnLastLine})");
            
            // Mostra il testo immediatamente
            if (dialogueText != null)
            {
                dialogueText.text = lineToShow;
                Debug.Log($"[DEBUG] Testo assegnato al TextMeshPro");
            }
            else
            {
                Debug.LogError("[DEBUG] DialogueText è NULL!");
            }
            
            // Riproduci l'audio se presente
            if (currentLine.audioClip != null && audioSource != null)
            {
                PlayAudioForLine(currentLine.audioClip);
            }
            else
            {
                // Se non c'è audio E siamo sull'ultima battuta E autoFinishLastLine è attivo
                isPlayingAudio = false;
                if (isOnLastLine && autoFinishLastLine)
                {
                    Debug.Log($"[DialogueSystem] 🎯 Ultima battuta senza audio - AVVIO AUTO-FINISH tra {autoFinishDelay} secondi!");
                    autoFinishCoroutine = StartCoroutine(AutoFinishLastLineWithoutAudio());
                }
                else if (isOnLastLine && !autoFinishLastLine)
                {
                    Debug.Log($"[DialogueSystem] 🎯 Ultima battuta senza audio - ASPETTO Space (autoFinishLastLine = false)");
                }
                else
                {
                    Debug.Log($"[DialogueSystem] Battuta normale senza audio - ASPETTO Space");
                }
            }
        }
    }
    
    void PlayAudioForLine(AudioClip clip)
    {
        if (audioCoroutine != null)
            StopCoroutine(audioCoroutine);
            
        audioSource.clip = clip;
        audioSource.Play();
        isPlayingAudio = true;
        
        // Avvia sempre la coroutine per aspettare la fine dell'audio
        audioCoroutine = StartCoroutine(WaitForAudioToEnd(clip.length));
        
        Debug.Log($"[DialogueSystem] Riproduco audio: {clip.name} (durata: {clip.length:F2}s)");
    }
    
    /// <summary>
    /// Gestisce la fine dell'audio - anche per l'ultima battuta se autoFinishLastLine è attivo
    /// </summary>
    IEnumerator WaitForAudioToEnd(float audioDuration)
    {
        yield return new WaitForSeconds(audioDuration);
        isPlayingAudio = false;
        
        // Se siamo sull'ultima linea E autoFinishLastLine è attivo, chiudi automaticamente
        if (isDialogueActive && isOnLastLine && autoFinishLastLine)
        {
            Debug.Log("[DialogueSystem] 🎯 Audio ultima battuta finito - CHIUDO AUTOMATICAMENTE!");
            NotifyLastLineFinished(); // Notifica agli NPC
            yield return new WaitForSeconds(0.1f); // Piccola pausa per sicurezza
            NextLine(); // Questo chiuderà il dialogo
            yield break;
        }
        
        // Se siamo sull'ultima linea MA autoFinishLastLine è disattivo, aspetta Space
        if (isDialogueActive && isOnLastLine && !autoFinishLastLine)
        {
            Debug.Log("[DialogueSystem] 🎯 Audio ultima battuta finito - ASPETTO Space (autoFinishLastLine = false)!");
            yield break; // Esci e aspetta input del player
        }
        
        // Per tutte le altre battute, passa automaticamente
        if (isDialogueActive && !isOnLastLine)
        {
            Debug.Log("[DialogueSystem] Audio finito, passo automaticamente alla linea successiva");
            NextLine();
        }
    }
    
    /// <summary>
    /// Gestisce auto-finish per ultima battuta senza audio
    /// </summary>
    IEnumerator AutoFinishLastLineWithoutAudio()
    {
        // Pausa per dare tempo di leggere
        yield return new WaitForSeconds(autoFinishDelay);
        
        if (isDialogueActive && isOnLastLine)
        {
            Debug.Log($"[DialogueSystem] 🎯 Tempo scaduto per ultima battuta senza audio ({autoFinishDelay}s) - CHIUDO!");
            NotifyLastLineFinished(); // Notifica agli NPC
            NextLine(); // Chiude il dialogo
        }
        
        autoFinishCoroutine = null;
    }
    
    /// <summary>
    /// Notifica che l'ultima battuta è finita
    /// </summary>
    void NotifyLastLineFinished()
    {
        Debug.Log("[DialogueSystem] 🚀 NOTIFICANDO CHE L'ULTIMA BATTUTA È FINITA!");
        OnLastLineFinished?.Invoke();
        OnAnyLastLineFinished?.Invoke(this);
    }
    
    void StopCurrentAudio()
    {
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
            isPlayingAudio = false;
            
            // Ferma anche la coroutine dell'audio
            if (audioCoroutine != null)
            {
                StopCoroutine(audioCoroutine);
                audioCoroutine = null;
            }
            
            Debug.Log("[DialogueSystem] Audio interrotto per passare alla linea successiva.");
        }
    }
    
    IEnumerator FadeOutAudio()
    {
        float startVolume = audioSource.volume;
        float timer = 0f;
        
        while (timer < audioFadeOutTime && audioSource.isPlaying)
        {
            timer += Time.deltaTime;
            audioSource.volume = Mathf.Lerp(startVolume, 0f, timer / audioFadeOutTime);
            yield return null;
        }
        
        audioSource.Stop();
        audioSource.volume = startVolume; // Ripristina il volume originale
        isPlayingAudio = false;
        
        Debug.Log("[DialogueSystem] Audio skippato.");
    }
    
    void NextLine()
    {
        currentLineIndex++;
        
        if (currentLineIndex < dialogueLines.Length)
        {
            // C'è ancora una battuta da mostrare
            DisplayLine();
        }
        else
        {
            // Fine del dialogo
            EndDialogue();
        }
    }
    
    public void EndDialogue()
{
    Debug.Log("[DialogueSystem] 🏁 EndDialogue chiamato");
    
    isDialogueActive = false;
    currentLineIndex = 0;
    isPlayingAudio = false;
    isOnLastLine = false;
    
    // Ferma l'audio se in riproduzione
    if (audioSource != null && audioSource.isPlaying)
    {
        audioSource.Stop();
    }
    
    // 🎮 SBLOCCA MOVIMENTO DEL PLAYER
    if (lockMovementDuringDialogue)
    {
        // Ferma la coroutine del lock se attiva
        if (movementLockCoroutine != null)
        {
            StopCoroutine(movementLockCoroutine);
            movementLockCoroutine = null;
        }
        
        UnlockPlayerMovement();
    }
    
    // Nascondi l'UI del dialogo
    if (dialogueUI != null)
        dialogueUI.SetActive(false);
    
    // Ferma le coroutine se attive
    if (audioCoroutine != null)
    {
        StopCoroutine(audioCoroutine);
        audioCoroutine = null;
    }
    
    if (autoFinishCoroutine != null)
    {
        StopCoroutine(autoFinishCoroutine);
        autoFinishCoroutine = null;
    }
    
    // ATTIVA OGGETTI SE ABILITATO
    if (enableObjectActivation && !objectsAlreadyActivated)
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
    
    // ATTIVA SOTTODIALOGO SE ABILITATO
    if (enableSubDialogue && !subDialogueTriggered)
    {
        if (subDialogueDelay > 0)
        {
            StartCoroutine(TriggerSubDialogueWithDelay());
        }
        else
        {
            TriggerSubDialogue();
        }
    }
    
    // AVVIA DISSOLVE SE ABILITATO E NON DEVE ESSERE INSIEME AL SOTTODIALOGO
    if (enableDissolveEffect && !dissolveTriggered && !dissolveWithSubDialogue)
    {
        StartDissolveEffect();
    }
    
    // Notifica fine dialogo
    OnDialogueEnded?.Invoke();
    OnAnyDialogueEnded?.Invoke(this);
    
    Debug.Log("[DialogueSystem] Dialogo terminato.");
}

    
    // ========== OBJECT ACTIVATION SYSTEM ==========
    
    /// <summary>
    /// Attiva gli oggetti con ritardo se specificato
    /// </summary>
    IEnumerator ActivateObjectsWithDelay()
    {
        Debug.Log($"[DialogueSystem] Attivazione oggetti ritardata di {activationDelay} secondi...");
        yield return new WaitForSeconds(activationDelay);
        ActivateObjects();
    }
    
    /// <summary>
    /// Attiva/disattiva gli oggetti configurati
    /// </summary>
    void ActivateObjects()
    {
        if (objectsAlreadyActivated)
        {
            Debug.Log("[DialogueSystem] Oggetti già attivati, skip.");
            return;
        }
        
        Debug.Log("[DialogueSystem] 🎯 ATTIVAZIONE OGGETTI INIZIATA!");
        
        int objectsActivated = 0;
        int objectsDeactivated = 0;
        
        // Attiva oggetto singolo
        if (objectToEnable != null)
        {
            try
            {
                bool wasActive = objectToEnable.activeInHierarchy;
                objectToEnable.SetActive(true);
                objectsActivated++;
                Debug.Log($"[DialogueSystem] ✅ Oggetto attivato: {objectToEnable.name} (era attivo: {wasActive})");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[DialogueSystem] ❌ Errore nell'attivare {objectToEnable.name}: {e.Message}");
            }
        }
        
        // Disattiva oggetto singolo
        if (objectToDisable != null)
        {
            try
            {
                bool wasActive = objectToDisable.activeInHierarchy;
                objectToDisable.SetActive(false);
                objectsDeactivated++;
                Debug.Log($"[DialogueSystem] ❌ Oggetto disattivato: {objectToDisable.name} (era attivo: {wasActive})");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[DialogueSystem] ❌ Errore nel disattivare {objectToDisable.name}: {e.Message}");
            }
        }
        
        // Attiva oggetti multipli
        if (objectsToEnable != null && objectsToEnable.Length > 0)
        {
            foreach (GameObject obj in objectsToEnable)
            {
                if (obj != null)
                {
                    try
                    {
                        bool wasActive = obj.activeInHierarchy;
                        obj.SetActive(true);
                        objectsActivated++;
                        Debug.Log($"[DialogueSystem] ✅ Oggetto attivato: {obj.name} (era attivo: {wasActive})");
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"[DialogueSystem] ❌ Errore nell'attivare {obj.name}: {e.Message}");
                    }
                }
            }
        }
        
        // Disattiva oggetti multipli
        if (objectsToDisable != null && objectsToDisable.Length > 0)
        {
            foreach (GameObject obj in objectsToDisable)
            {
                if (obj != null)
                {
                    try
                    {
                        bool wasActive = obj.activeInHierarchy;
                        obj.SetActive(false);
                        objectsDeactivated++;
                        Debug.Log($"[DialogueSystem] ❌ Oggetto disattivato: {obj.name} (era attivo: {wasActive})");
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"[DialogueSystem] ❌ Errore nel disattivare {obj.name}: {e.Message}");
                    }
                }
            }
        }
        
        objectsAlreadyActivated = true;
        
        // Log finale e messaggio personalizzato
        string finalMessage = !string.IsNullOrEmpty(activationMessage) ? activationMessage : 
                             $"Dialogo completato: {objectsActivated} oggetti attivati, {objectsDeactivated} disattivati";
        
        Debug.Log($"[DialogueSystem] 🎉 {finalMessage}");
        
        // Invoca eventi
        OnObjectsActivated?.Invoke();
        OnAnyObjectsActivated?.Invoke(this);
    }
    
    // ========== 🆕 SUB-DIALOGUE SYSTEM ==========
    
    /// <summary>
    /// 🆕 Attiva il sottodialogo con ritardo se specificato
    /// </summary>
    IEnumerator TriggerSubDialogueWithDelay()
    {
        Debug.Log($"[DialogueSystem] Attivazione sottodialogo ritardata di {subDialogueDelay} secondi...");
        yield return new WaitForSeconds(subDialogueDelay);
        TriggerSubDialogue();
    }
    
    /// <summary>
    /// 🆕 Attiva il sottodialogo
    /// </summary>
    void TriggerSubDialogue()
    {
        if (subDialogueTriggered)
        {
            Debug.Log("[DialogueSystem] Sottodialogo già attivato, skip.");
            return;
        }
        
        if (subDialogueSystem == null)
        {
            Debug.LogWarning("[DialogueSystem] ⚠️ Sottodialogo abilitato ma nessun DialogueSystem assegnato!");
            return;
        }
        
        Debug.Log("[DialogueSystem] 🎭 ATTIVAZIONE SOTTODIALOGO!");
        
        subDialogueTriggered = true;
        
        // Log messaggio personalizzato
        if (!string.IsNullOrEmpty(subDialogueMessage))
        {
            Debug.Log($"[DialogueSystem] 📢 {subDialogueMessage}");
        }
        
        // 🆕 AVVIA DISSOLVE SE ABILITATO E DEVE ESSERE INSIEME AL SOTTODIALOGO
        if (enableDissolveEffect && !dissolveTriggered && dissolveWithSubDialogue)
        {
            StartDissolveEffect();
        }
        
        // Attiva il sottodialogo
        try
        {
            subDialogueSystem.TriggerDialogue();
            Debug.Log($"[DialogueSystem] ✅ Sottodialogo '{subDialogueSystem.name}' attivato con successo!");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[DialogueSystem] ❌ Errore nell'attivare sottodialogo: {e.Message}");
        }
        
        // Invoca eventi
        OnSubDialogueTriggered?.Invoke();
        OnAnySubDialogueTriggered?.Invoke(this);
    }
    
    /// <summary>
    /// 🆕 Valida il setup del sottodialogo all'avvio
    /// </summary>
    void ValidateSubDialogueSetup()
    {
        if (!enableSubDialogue)
        {
            Debug.Log("[DialogueSystem] Sub-Dialogue disabilitato");
            return;
        }
        
        if (subDialogueSystem == null)
        {
            Debug.LogWarning("[DialogueSystem] ⚠️ Sub-Dialogue abilitato ma nessun DialogueSystem assegnato!");
        }
        else if (subDialogueSystem == this)
        {
            Debug.LogError("[DialogueSystem] ❌ ERRORE: Sub-Dialogue non può essere se stesso! Rischio loop infinito!");
            enableSubDialogue = false;
        }
        else
        {
            Debug.Log($"[DialogueSystem] ✅ Sub-Dialogue setup: '{subDialogueSystem.name}'");
        }
    }

    // ========== 🆕 DISSOLVE SYSTEM ==========

    /// <summary>
    /// 🆕 Avvia l'effetto dissolve per tutti gli oggetti configurati
    /// </summary>
    /// <summary>
    /// 🆕 Avvia l'effetto dissolve per tutti gli oggetti configurati
    /// </summary>
    void StartDissolveEffect()
    {
        if (dissolveTriggered)
        {
            Debug.Log("[DialogueSystem] Dissolve già attivato, skip.");
            return;
        }

        if (dissolveObjects == null || dissolveObjects.Length == 0)
        {
            Debug.LogWarning("[DialogueSystem] ⚠️ Dissolve abilitato ma nessun oggetto configurato!");
            return;
        }

        Debug.Log("[DialogueSystem] 🌀 AVVIO EFFETTO DISSOLVE!");

        dissolveTriggered = true;

        // 🆕 AVVIA AUDIO DISSOLVE SE ABILITATO
        if (enableDissolveAudio)
        {
            StartDissolveAudio();
        }

        // Avvia dissolve per ogni oggetto
        for (int i = 0; i < dissolveObjects.Length; i++)
        {
            DissolveObject dissolveObj = dissolveObjects[i];
            if (ValidateDissolveObject(dissolveObj, i))
            {
                Coroutine dissolveCoroutine = StartCoroutine(DissolveObjectCoroutine(dissolveObj, i));
                dissolveCoroutines.Add(dissolveCoroutine);
            }
        }

        // Invoca eventi
        OnDissolveStarted?.Invoke();
        OnAnyDissolveStarted?.Invoke(this);
    }
/// <summary>
/// 🆕 Avvia l'audio durante il dissolve
/// </summary>
void StartDissolveAudio()
{
    if (dissolveAudioSource == null && dissolveAudioClip == null)
    {
        Debug.LogWarning("[DialogueSystem] ⚠️ Audio dissolve abilitato ma nessun AudioSource o AudioClip configurato!");
        return;
    }
    
    // Se non c'è AudioSource ma c'è il clip, usa l'AudioSource principale
    if (dissolveAudioSource == null && dissolveAudioClip != null)
    {
        dissolveAudioSource = audioSource;
        Debug.Log("[DialogueSystem] 🔊 Usando AudioSource principale per dissolve audio");
    }
    
    if (dissolveAudioSource != null && dissolveAudioClip != null)
    {
        Debug.Log("[DialogueSystem] 🔊 AVVIO AUDIO DISSOLVE");
        
        dissolveAudioSource.clip = dissolveAudioClip;
        dissolveAudioSource.loop = false; 
        dissolveAudioSource.Play();
        dissolveAudioPlaying = true;
        
        Debug.Log($"[DialogueSystem] 🎵 Audio dissolve avviato: {dissolveAudioClip.name}");
    }
    else
    {
        Debug.LogWarning("[DialogueSystem] ⚠️ Impossibile avviare audio dissolve - AudioSource o AudioClip mancante!");
    }
}

/// <summary>
/// 🆕 Ferma l'audio dissolve con fade out
/// </summary>
void StopDissolveAudio()
{
    if (!dissolveAudioPlaying || dissolveAudioSource == null)
    {
        return;
    }
    
    Debug.Log("[DialogueSystem] 🔇 FERMANDO AUDIO DISSOLVE");
    
    if (dissolveAudioFadeTime > 0f)
    {
        // Ferma con fade out
        if (dissolveAudioCoroutine != null)
        {
            StopCoroutine(dissolveAudioCoroutine);
        }
        dissolveAudioCoroutine = StartCoroutine(FadeOutDissolveAudio());
    }
    else
    {
        // Ferma immediatamente
        dissolveAudioSource.Stop();
        dissolveAudioPlaying = false;
        Debug.Log("[DialogueSystem] ⏹️ Audio dissolve fermato immediatamente");
    }
}

/// <summary>
/// 🆕 Coroutine per fade out dell'audio dissolve
/// </summary>
IEnumerator FadeOutDissolveAudio()
{
    if (dissolveAudioSource == null)
    {
        yield break;
    }
    
    float startVolume = dissolveAudioSource.volume;
    float elapsedTime = 0f;
    
    Debug.Log($"[DialogueSystem] 🎵 Fade out audio dissolve (durata: {dissolveAudioFadeTime}s)");
    
    while (elapsedTime < dissolveAudioFadeTime && dissolveAudioSource.isPlaying)
    {
        elapsedTime += Time.deltaTime;
        float progress = elapsedTime / dissolveAudioFadeTime;
        dissolveAudioSource.volume = Mathf.Lerp(startVolume, 0f, progress);
        yield return null;
    }
    
    // Ferma l'audio e ripristina il volume
    dissolveAudioSource.Stop();
    dissolveAudioSource.volume = startVolume;
    dissolveAudioPlaying = false;
    dissolveAudioCoroutine = null;
    
    Debug.Log("[DialogueSystem] ✅ Fade out audio dissolve completato");
}
    
    /// <summary>
    /// 🆕 Coroutine che gestisce l'effetto dissolve per un singolo oggetto
    /// </summary>
    IEnumerator DissolveObjectCoroutine(DissolveObject dissolveObj, int index)
    {
        Debug.Log($"[DialogueSystem] 🌀 Iniziando dissolve per '{dissolveObj.targetObject.name}' (#{index})");
        
        // Ritardo iniziale se specificato
        if (dissolveObj.dissolveDelay > 0f)
        {
            Debug.Log($"[DialogueSystem] ⏳ Aspettando {dissolveObj.dissolveDelay}s prima del dissolve...");
            yield return new WaitForSeconds(dissolveObj.dissolveDelay);
        }
        
        // Verifica che i materiali siano ancora validi
        if (dissolveObj.dissolveMaterials == null || dissolveObj.dissolveMaterials.Length == 0)
        {
            Debug.LogError($"[DialogueSystem] ❌ Nessun materiale configurato per '{dissolveObj.targetObject.name}'!");
            yield break;
        }
        
        // Verifica che tutti i materiali abbiano la proprietà dissolve
        List<Material> validMaterials = new List<Material>();
        foreach (Material mat in dissolveObj.dissolveMaterials)
        {
            if (mat != null && mat.HasProperty(dissolveObj.dissolvePropertyName))
            {
                validMaterials.Add(mat);
                Debug.Log($"[DialogueSystem] ✅ Materiale '{mat.name}' ha proprietà '{dissolveObj.dissolvePropertyName}'");
            }
            else if (mat != null)
            {
                Debug.LogWarning($"[DialogueSystem] ⚠️ Materiale '{mat.name}' NON ha proprietà '{dissolveObj.dissolvePropertyName}'!");
            }
        }
        
        if (validMaterials.Count == 0)
        {
            Debug.LogError($"[DialogueSystem] ❌ Nessun materiale valido per dissolve di '{dissolveObj.targetObject.name}'!");
            yield break;
        }
        
        // Salva i valori iniziali di dissolve (dovrebbero essere 1)
        float[] initialDissolveValues = new float[validMaterials.Count];
        for (int i = 0; i < validMaterials.Count; i++)
        {
            initialDissolveValues[i] = validMaterials[i].GetFloat(dissolveObj.dissolvePropertyName);
            Debug.Log($"[DialogueSystem] 📊 Valore iniziale dissolve per '{validMaterials[i].name}': {initialDissolveValues[i]}");
        }
        
        // Animazione dissolve da 1 a 0
        float elapsedTime = 0f;
        Debug.Log($"[DialogueSystem] 🎬 Avviando animazione dissolve (durata: {dissolveObj.dissolveDuration}s)");
        
        while (elapsedTime < dissolveObj.dissolveDuration)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / dissolveObj.dissolveDuration;
            
            // Interpola da 1 a 0
            float currentDissolveValue = Mathf.Lerp(1f, 0f, progress);
            
            // Applica il valore a tutti i materiali validi
            foreach (Material mat in validMaterials)
            {
                if (mat != null) // Double check per sicurezza
                {
                    mat.SetFloat(dissolveObj.dissolvePropertyName, currentDissolveValue);
                }
            }
            
            yield return null;
        }
        
        // Assicurati che il valore finale sia esattamente 0
        foreach (Material mat in validMaterials)
        {
            if (mat != null)
            {
                mat.SetFloat(dissolveObj.dissolvePropertyName, 0f);
            }
        }
        
        Debug.Log($"[DialogueSystem] ✅ Dissolve completato per '{dissolveObj.targetObject.name}'!");
        
        // Attiva il MeshCollider se richiesto
        if (dissolveObj.enableMeshColliderAfterDissolve)
        {
            if (dissolveObj.colliderActivationDelay > 0f)
            {
                Debug.Log($"[DialogueSystem] ⏳ Aspettando {dissolveObj.colliderActivationDelay}s prima di attivare collider...");
                yield return new WaitForSeconds(dissolveObj.colliderActivationDelay);
            }
            
            EnableMeshCollider(dissolveObj.targetObject);
        }
        
        // Controlla se tutte le coroutine di dissolve sono finite
        CheckDissolveCompletion();
    }

    /// <summary>
    /// 🆕 Controlla se tutti i dissolve sono completati
    /// </summary>
    /// <summary>
    /// 🆕 Controlla se tutti i dissolve sono completati
    /// </summary>
    void CheckDissolveCompletion()
    {
        // Rimuovi le coroutine completate dalla lista
        dissolveCoroutines.RemoveAll(coroutine => coroutine == null);

        // Se tutte le coroutine di dissolve sono finite, notifica completamento
        if (dissolveCoroutines.Count == 0 && dissolveTriggered)
        {
            Debug.Log("[DialogueSystem] 🎉 TUTTI I DISSOLVE COMPLETATI!");

            // 🆕 FERMA AUDIO DISSOLVE SE ABILITATO
            if (enableDissolveAudio && stopAudioOnDissolveComplete)
            {
                StopDissolveAudio();
            }

            OnDissolveCompleted?.Invoke();
            OnAnyDissolveCompleted?.Invoke(this);
        }
    }
/// <summary>
/// 🆕 Valida il setup audio dissolve
/// </summary>
void ValidateDissolveAudioSetup()
{
    if (!enableDissolveAudio)
    {
        Debug.Log("[DialogueSystem] Audio Dissolve disabilitato");
        return;
    }
    
    if (dissolveAudioClip == null)
    {
        Debug.LogWarning("[DialogueSystem] ⚠️ Audio Dissolve abilitato ma nessun AudioClip assegnato!");
        return;
    }
    
    if (dissolveAudioSource == null && audioSource == null)
    {
        Debug.LogWarning("[DialogueSystem] ⚠️ Nessun AudioSource disponibile per audio dissolve!");
        return;
    }
    
    Debug.Log($"[DialogueSystem] ✅ Audio Dissolve setup: '{dissolveAudioClip.name}'");
    if (dissolveAudioSource == null)
    {
        Debug.Log("[DialogueSystem] 📢 Userà AudioSource principale per dissolve");
    }
}
    
    /// <summary>
    /// 🆕 Attiva il MeshCollider di un oggetto
    /// </summary>
    void EnableMeshCollider(GameObject targetObject)
    {
        MeshCollider meshCollider = targetObject.GetComponent<MeshCollider>();
        if (meshCollider != null)
        {
            bool wasEnabled = meshCollider.enabled;
            meshCollider.enabled = true;
            Debug.Log($"[DialogueSystem] 🔲 MeshCollider attivato per '{targetObject.name}' (era abilitato: {wasEnabled})");
        }
        else
        {
            Debug.LogWarning($"[DialogueSystem] ⚠️ Nessun MeshCollider trovato su '{targetObject.name}'!");
        }
    }
    
    /// <summary>
    /// 🆕 Valida un oggetto dissolve
    /// </summary>
    bool ValidateDissolveObject(DissolveObject dissolveObj, int index)
    {
        if (dissolveObj.targetObject == null)
        {
            Debug.LogError($"[DialogueSystem] ❌ Target Object NULL per dissolve #{index}!");
            return false;
        }
        
        if (!dissolveObj.targetObject.activeInHierarchy)
        {
            Debug.LogWarning($"[DialogueSystem] ⚠️ Target Object '{dissolveObj.targetObject.name}' non è attivo!");
            return false;
        }
        
        if (dissolveObj.dissolveMaterials == null || dissolveObj.dissolveMaterials.Length == 0)
        {
            Debug.LogError($"[DialogueSystem] ❌ Nessun materiale configurato per dissolve #{index}!");
            return false;
        }
        
        if (string.IsNullOrEmpty(dissolveObj.dissolvePropertyName))
        {
            Debug.LogError($"[DialogueSystem] ❌ Nome proprietà dissolve vuoto per #{index}!");
            return false;
        }
        
        if (dissolveObj.dissolveDuration <= 0f)
        {
            Debug.LogError($"[DialogueSystem] ❌ Durata dissolve invalida per #{index}: {dissolveObj.dissolveDuration}");
            return false;
        }
        
        Debug.Log($"[DialogueSystem] ✅ Dissolve Object #{index} validato: '{dissolveObj.targetObject.name}'");
        return true;
    }
    
    /// <summary>
    /// 🆕 Valida il setup del dissolve all'avvio (VERSIONE AGGIORNATA)
    /// </summary>
    void ValidateDissolveSetup()
    {
        if (!enableDissolveEffect)
        {
            Debug.Log("[DialogueSystem] Dissolve Effect disabilitato");
            return;
        }
        
        if (dissolveObjects == null || dissolveObjects.Length == 0)
        {
            Debug.LogWarning("[DialogueSystem] ⚠️ Dissolve Effect abilitato ma nessun oggetto configurato!");
            return;
        }
        
        int validObjects = 0;
        int totalMaterials = 0;
        int validMaterials = 0;
        
        for (int i = 0; i < dissolveObjects.Length; i++)
        {
            DissolveObject dissolveObj = dissolveObjects[i];
            if (dissolveObj.targetObject != null && 
                dissolveObj.dissolveMaterials != null && 
                dissolveObj.dissolveMaterials.Length > 0)
            {
                validObjects++;
                
                // Controlla se i materiali hanno la proprietà dissolve
                int objValidMaterials = 0;
                foreach (Material mat in dissolveObj.dissolveMaterials)
                {
                    totalMaterials++;
                    if (mat != null && mat.HasProperty(dissolveObj.dissolvePropertyName))
                    {
                        objValidMaterials++;
                        validMaterials++;
                        
                        // 🆕 Mostra il valore attuale se forceInitializeDissolveValues è disabilitato
                        if (!forceInitializeDissolveValues)
                        {
                            float currentValue = mat.GetFloat(dissolveObj.dissolvePropertyName);
                            if (currentValue != 1f)
                            {
                                Debug.LogWarning($"[DialogueSystem] ⚠️ Materiale '{mat.name}' ha valore dissolve {currentValue} (non 1). Considera di abilitare forceInitializeDissolveValues!");
                            }
                        }
                    }
                }
                
                Debug.Log($"[DialogueSystem] 🔍 Dissolve Object #{i}: '{dissolveObj.targetObject.name}' - {objValidMaterials}/{dissolveObj.dissolveMaterials.Length} materiali validi");
            }
        }
        
        Debug.Log($"[DialogueSystem] ✅ Dissolve Effect setup: {validObjects}/{dissolveObjects.Length} oggetti configurati correttamente");
        Debug.Log($"[DialogueSystem] 📊 Materiali: {validMaterials}/{totalMaterials} validi");
        
        if (forceInitializeDissolveValues)
        {
            Debug.Log($"[DialogueSystem] 🔧 Inizializzazione automatica ABILITATA (valore: {initialDissolveValue})");
        }
        else
        {
            Debug.Log($"[DialogueSystem] ⚠️ Inizializzazione automatica DISABILITATA - verifica manualmente i valori!");
        }
    }
    
    /// <summary>
    /// 🆕 Ferma tutte le coroutine di dissolve attive
    /// </summary>
    void StopAllDissolveCoroutines()
    {
        foreach (Coroutine coroutine in dissolveCoroutines)
        {
            if (coroutine != null)
            {
                StopCoroutine(coroutine);
            }
        }
        dissolveCoroutines.Clear();
        Debug.Log("[DialogueSystem] 🛑 Tutte le coroutine dissolve fermate");
    }
    // ========== GETTERS E SETTERS PER PLAYER MOVEMENT CONTROL ==========

/// <summary>
/// 🎮 Abilita/disabilita il blocco movimento durante il dialogo
/// </summary>
public void SetMovementLockEnabled(bool enabled)
{
    lockMovementDuringDialogue = enabled;
    Debug.Log($"[DialogueSystem] 🎮 Player Movement Lock {(enabled ? "abilitato" : "disabilitato")}");
}

/// <summary>
/// 🎮 Imposta il ritardo per il blocco movimento
/// </summary>
public void SetMovementLockDelay(float delay)
{
    movementLockDelay = delay;
    Debug.Log($"[DialogueSystem] 🎮 Movement Lock Delay impostato: {delay}s");
}

/// <summary>
/// 🎮 Imposta il ThirdPersonController target
/// </summary>
public void SetTargetPlayerController(ThirdPersonController controller)
{
    targetPlayerController = controller;
    Debug.Log($"[DialogueSystem] 🎮 Target Player Controller impostato: {(controller != null ? controller.name : "NULL")}");
}

/// <summary>
/// 🎮 Abilita/disabilita il blocco rotazione camera
/// </summary>
public void SetCameraRotationLock(bool enabled)
{
    lockCameraRotation = enabled;
    Debug.Log($"[DialogueSystem] 📷 Camera Rotation Lock {(enabled ? "abilitato (non implementato)" : "disabilitato")}");
}

    /// <summary>
    /// 🎮 Forza il blocco del movimento (per test)
    /// </summary>
    public void ForceLockPlayerMovement()
    {
        if (targetPlayerController != null)
        {
            LockPlayerMovement();
        }
        else
        {
            Debug.LogWarning("[DialogueSystem] Impossibile forzare lock - nessun controller assegnato");
        }
    }
// ========== GETTERS PUBBLICI ==========

public bool IsMovementLockEnabled() => lockMovementDuringDialogue;
public bool IsCameraRotationLockEnabled() => lockCameraRotation;
public float GetMovementLockDelay() => movementLockDelay;
public ThirdPersonController GetTargetPlayerController() => targetPlayerController;
public bool IsPlayerMovementCurrentlyLocked() => movementLockApplied;


/// <summary>
/// 🎮 Forza lo sblocco del movimento (per test)
/// </summary>
public void ForceUnlockPlayerMovement()
{
    if (targetPlayerController != null)
    {
        UnlockPlayerMovement();
    }
    else
    {
        Debug.LogWarning("[DialogueSystem] Impossibile forzare unlock - nessun controller assegnato");
    }
}
    // ========== 🆕 DISSOLVE GETTERS/SETTERS ==========
    
    /// <summary>
    /// 🆕 Abilita/disabilita il sistema di dissolve
    /// </summary>
    public void SetDissolveEffectEnabled(bool enabled)
    {
        enableDissolveEffect = enabled;
        Debug.Log($"[DialogueSystem] Dissolve Effect {(enabled ? "abilitato" : "disabilitato")}");
    }
    
    /// <summary>
    /// 🆕 Imposta se il dissolve deve avvenire con il sottodialogo
    /// </summary>
    public void SetDissolveWithSubDialogue(bool withSubDialogue)
    {
        dissolveWithSubDialogue = withSubDialogue;
        Debug.Log($"[DialogueSystem] Dissolve con sottodialogo: {withSubDialogue}");
    }
    
    /// <summary>
    /// 🆕 Forza l'avvio dell'effetto dissolve (per test)
    /// </summary>
    public void ForceStartDissolve()
    {
        if (enableDissolveEffect)
        {
            dissolveTriggered = false; // Reset flag
            StartDissolveEffect();
        }
        else
        {
            Debug.LogWarning("[DialogueSystem] Dissolve Effect disabilitato - impossibile forzare avvio");
        }
    }
    
    /// <summary>
    /// 🆕 Ferma immediatamente tutti i dissolve
    /// </summary>
    public void ForceStopDissolve()
    {
        StopAllDissolveCoroutines();
        dissolveTriggered = false;
        Debug.Log("[DialogueSystem] Dissolve forzatamente fermato");
    }
    
    /// <summary>
    /// 🆕 Reset del flag di dissolve (per dialoghi ripetibili)
    /// </summary>
    public void ResetDissolve()
    {
        dissolveTriggered = false;
        StopAllDissolveCoroutines();
        Debug.Log("[DialogueSystem] Flag dissolve resettato");
    }
    
    // ========== 🆕 DISSOLVE INITIALIZATION METHODS ==========
    
    /// <summary>
    /// 🆕 Forza la re-inizializzazione dei valori dissolve (utile per testing)
    /// </summary>
    [ContextMenu("🆕 Test - Forza Re-Inizializzazione Dissolve")]
    public void ForceReinitializeDissolve()
    {
        if (enableDissolveEffect)
        {
            Debug.Log("[DialogueSystem] 🧪 Forzando re-inizializzazione dissolve...");
            InitializeDissolveValues();
        }
        else
        {
            Debug.LogWarning("[DialogueSystem] Dissolve Effect disabilitato - impossibile re-inizializzare");
        }
    }
    
    /// <summary>
    /// 🆕 Imposta un valore dissolve personalizzato per tutti i materiali configurati
    /// </summary>
    public void SetAllDissolveValues(float value)
    {
        if (dissolveObjects == null || dissolveObjects.Length == 0)
        {
            Debug.LogWarning("[DialogueSystem] ⚠️ Nessun oggetto dissolve configurato!");
            return;
        }
        
        value = Mathf.Clamp01(value); // Clamp tra 0 e 1
        Debug.Log($"[DialogueSystem] 🎨 Impostando valore dissolve {value} per tutti i materiali...");
        
        int materialsSet = 0;
        
        foreach (DissolveObject dissolveObj in dissolveObjects)
        {
            if (dissolveObj.targetObject != null && dissolveObj.dissolveMaterials != null)
            {
                foreach (Material mat in dissolveObj.dissolveMaterials)
                {
                    if (mat != null && mat.HasProperty(dissolveObj.dissolvePropertyName))
                    {
                        try
                        {
                            mat.SetFloat(dissolveObj.dissolvePropertyName, value);
                            materialsSet++;
                        }
                        catch (System.Exception e)
                        {
                            Debug.LogError($"[DialogueSystem] ❌ Errore nell'impostare valore per '{mat.name}': {e.Message}");
                        }
                    }
                }
            }
        }
        
        Debug.Log($"[DialogueSystem] ✅ Valore {value} impostato su {materialsSet} materiali");
    }
    
    /// <summary>
    /// 🆕 Abilita/disabilita l'inizializzazione automatica dei valori dissolve
    /// </summary>
    public void SetForceInitializeDissolveValues(bool force)
    {
        forceInitializeDissolveValues = force;
        Debug.Log($"[DialogueSystem] Inizializzazione automatica dissolve: {(force ? "ABILITATA" : "DISABILITATA")}");
    }
    
    /// <summary>
    /// 🆕 Imposta il valore di dissolve iniziale
    /// </summary>
    public void SetInitialDissolveValue(float value)
    {
        initialDissolveValue = Mathf.Clamp01(value);
        Debug.Log($"[DialogueSystem] Valore dissolve iniziale impostato: {initialDissolveValue}");
    }
    
    /// <summary>
    /// 🆕 Ottieni se l'inizializzazione automatica è abilitata
    /// </summary>
    public bool GetForceInitializeDissolveValues() => forceInitializeDissolveValues;
    
    /// <summary>
    /// 🆕 Ottieni il valore di dissolve iniziale
    /// </summary>
    public float GetInitialDissolveValue() => initialDissolveValue;
    
    // ========== 🆕 SUB-DIALOGUE GETTERS/SETTERS ==========
    
    /// <summary>
    /// 🆕 Abilita/disabilita il sistema di sottodialogo
    /// </summary>
    public void SetSubDialogueEnabled(bool enabled)
    {
        enableSubDialogue = enabled;
        Debug.Log($"[DialogueSystem] Sub-Dialogue {(enabled ? "abilitato" : "disabilitato")}");
    }
    
    /// <summary>
    /// 🆕 Imposta il DialogueSystem da usare come sottodialogo
    /// </summary>
    public void SetSubDialogueSystem(DialogueSystem subDialogue)
    {
        if (subDialogue == this)
        {
            Debug.LogError("[DialogueSystem] ❌ ERRORE: Sub-Dialogue non può essere se stesso!");
            return;
        }
        
        subDialogueSystem = subDialogue;
        Debug.Log($"[DialogueSystem] Sub-Dialogue impostato: {(subDialogue != null ? subDialogue.name : "NULL")}");
    }
    
    /// <summary>
    /// 🆕 Imposta il ritardo del sottodialogo
    /// </summary>
    public void SetSubDialogueDelay(float delay)
    {
        subDialogueDelay = delay;
        Debug.Log($"[DialogueSystem] Ritardo sottodialogo impostato: {delay}s");
    }
    
    /// <summary>
    /// 🆕 Imposta il messaggio del sottodialogo
    /// </summary>
    public void SetSubDialogueMessage(string message)
    {
        subDialogueMessage = message;
        Debug.Log($"[DialogueSystem] Messaggio sottodialogo impostato: {message}");
    }
    
    /// <summary>
    /// 🆕 Forza l'attivazione del sottodialogo (per test)
    /// </summary>
    public void ForceTriggerSubDialogue()
    {
        if (enableSubDialogue)
        {
            subDialogueTriggered = false; // Reset flag
            TriggerSubDialogue();
        }
        else
        {
            Debug.LogWarning("[DialogueSystem] Sub-Dialogue disabilitato - impossibile forzare attivazione");
        }
    }
    
    /// <summary>
    /// 🆕 Reset del flag di sottodialogo (per dialoghi ripetibili)
    /// </summary>
    public void ResetSubDialogue()
    {
        subDialogueTriggered = false;
        Debug.Log("[DialogueSystem] Flag sottodialogo resettato");
    }
    
    // ========== 🆕 GETTERS NUOVE FUNZIONALITÀ ==========
    
    public bool IsSubDialogueEnabled() => enableSubDialogue;
    public DialogueSystem GetSubDialogueSystem() => subDialogueSystem;
    public float GetSubDialogueDelay() => subDialogueDelay;
    public string GetSubDialogueMessage() => subDialogueMessage;
    public bool WasSubDialogueTriggered() => subDialogueTriggered;
    
    public bool IsDissolveEffectEnabled() => enableDissolveEffect;
    public DissolveObject[] GetDissolveObjects() => dissolveObjects;
    public bool GetDissolveWithSubDialogue() => dissolveWithSubDialogue;
    public bool WasDissolveTriggered() => dissolveTriggered;
    public int GetActiveDissolveCoroutines() => dissolveCoroutines.Count;
    
    /// <summary>
    /// Valida il setup degli oggetti all'avvio
    /// </summary>
    void ValidateObjectActivationSetup()
    {
        if (!enableObjectActivation)
        {
            Debug.Log("[DialogueSystem] Object Activation disabilitato");
            return;
        }
        
        int totalObjects = 0;
        
        if (objectToEnable != null) totalObjects++;
        if (objectToDisable != null) totalObjects++;
        if (objectsToEnable != null) totalObjects += objectsToEnable.Length;
        if (objectsToDisable != null) totalObjects += objectsToDisable.Length;
        
        if (totalObjects == 0)
        {
            Debug.LogWarning("[DialogueSystem] ⚠️ Object Activation abilitato ma nessun oggetto assegnato!");
        }
        else
        {
            Debug.Log($"[DialogueSystem] ✅ Object Activation setup: {totalObjects} oggetti configurati");
        }
    }
    
    // ========== OBJECT ACTIVATION GETTERS/SETTERS ==========
    
    /// <summary>
    /// Abilita/disabilita il sistema di attivazione oggetti
    /// </summary>
    public void SetObjectActivationEnabled(bool enabled)
    {
        enableObjectActivation = enabled;
        Debug.Log($"[DialogueSystem] Object Activation {(enabled ? "abilitato" : "disabilitato")}");
    }
    
    /// <summary>
    /// Imposta l'oggetto da attivare
    /// </summary>
    public void SetObjectToEnable(GameObject obj)
    {
        objectToEnable = obj;
        Debug.Log($"[DialogueSystem] Oggetto da attivare impostato: {(obj != null ? obj.name : "NULL")}");
    }
    
    /// <summary>
    /// Imposta l'oggetto da disattivare
    /// </summary>
    public void SetObjectToDisable(GameObject obj)
    {
        objectToDisable = obj;
        Debug.Log($"[DialogueSystem] Oggetto da disattivare impostato: {(obj != null ? obj.name : "NULL")}");
    }
    
    /// <summary>
    /// Imposta il messaggio di attivazione
    /// </summary>
    public void SetActivationMessage(string message)
    {
        activationMessage = message;
        Debug.Log($"[DialogueSystem] Messaggio di attivazione impostato: {message}");
    }
    
    /// <summary>
    /// Imposta il ritardo di attivazione
    /// </summary>
    public void SetActivationDelay(float delay)
    {
        activationDelay = delay;
        Debug.Log($"[DialogueSystem] Ritardo attivazione impostato: {delay}s");
    }
    
    /// <summary>
    /// Forza l'attivazione degli oggetti (per test)
    /// </summary>
    public void ForceActivateObjects()
    {
        if (enableObjectActivation)
        {
            objectsAlreadyActivated = false; // Reset flag
            ActivateObjects();
        }
        else
        {
            Debug.LogWarning("[DialogueSystem] Object Activation disabilitato - impossibile forzare attivazione");
        }
    }
    
    /// <summary>
    /// Reset del flag di attivazione (per dialoghi ripetibili)
    /// </summary>
    public void ResetObjectActivation()
    {
        objectsAlreadyActivated = false;
        Debug.Log("[DialogueSystem] Flag attivazione oggetti resettato");
    }
    
    // ========== GETTERS OBJECT ACTIVATION ==========
    
    public bool IsObjectActivationEnabled() => enableObjectActivation;
    public GameObject GetObjectToEnable() => objectToEnable;
    public GameObject GetObjectToDisable() => objectToDisable;
    public GameObject[] GetObjectsToEnable() => objectsToEnable;
    public GameObject[] GetObjectsToDisable() => objectsToDisable;
    public string GetActivationMessage() => activationMessage;
    public float GetActivationDelay() => activationDelay;
    public bool AreObjectsActivated() => objectsAlreadyActivated;
    

    
    // Metodo pubblico per iniziare il dialogo da altri script
    public void TriggerDialogue()
    {
        StartDialogue();
    }
    
    // Metodo per resettare il trigger (utile per testing o per dialoghi ripetibili)
    public void ResetTrigger()
    {
        hasBeenTriggered = false;
        ResetObjectActivation(); // Reset anche gli oggetti se il dialogo è ripetibile
        ResetSubDialogue(); // 🆕 Reset sottodialogo
        ResetDissolve(); // 🆕 Reset dissolve
    }
    
    // Metodo pubblico per controllare se il dialogo è attivo
    public bool IsDialogueActive()
    {
        return isDialogueActive;
    }
    
    // Metodo per sapere se siamo sull'ultima battuta
    public bool IsOnLastLine()
    {
        return isOnLastLine;
    }
    
    // Metodo per sapere quante battute ci sono in totale
    public int GetTotalLines()
    {
        return dialogueLines.Length;
    }
    
    // Metodo per sapere su che battuta siamo attualmente
    public int GetCurrentLineIndex()
    {
        return currentLineIndex;
    }
    
    // Metodo per forzare la chiusura del dialogo da altri script
    public void ForceEndDialogue()
    {
        EndDialogue();
    }
    
    // Metodo per controllare se stiamo riproducendo audio
    public bool IsPlayingAudio()
    {
        return isPlayingAudio;
    }
    
    // Metodo per ottenere la linea di dialogo corrente
    public DialogueLine GetCurrentLine()
    {
        if (currentLineIndex < dialogueLines.Length)
            return dialogueLines[currentLineIndex];
        return null;
    }
    
    // ========== 🆕 DEBUG METHODS (AGGIORNATI) ==========
    
    [ContextMenu("Test - Forza Fine Ultima Battuta")]
    public void TestForceLastLineFinished()
    {
        if (isDialogueActive && isOnLastLine)
        {
            Debug.Log("[DialogueSystem] 🧪 Test: Forzando fine ultima battuta");
            NotifyLastLineFinished();
            NextLine(); // Chiude il dialogo
        }
        else
        {
            Debug.LogWarning($"[DialogueSystem] 🧪 Test fallito: Attivo={isDialogueActive}, Ultima={isOnLastLine}");
        }
    }
    
    [ContextMenu("Test - Forza Attivazione Oggetti")]
    public void TestForceActivateObjects()
    {
        ForceActivateObjects();
    }
    
    [ContextMenu("Test - Reset Object Activation")]
    public void TestResetObjectActivation()
    {
        ResetObjectActivation();
    }
    
    [ContextMenu("🆕 Test - Forza Sottodialogo")]
    public void TestForceSubDialogue()
    {
        ForceTriggerSubDialogue();
    }
    
    [ContextMenu("🆕 Test - Reset Sub-Dialogue")]
    public void TestResetSubDialogue()
    {
        ResetSubDialogue();
    }
    
    [ContextMenu("🆕 Test - Forza Dissolve")]
    public void TestForceDissolve()
    {
        ForceStartDissolve();
    }
    
    [ContextMenu("🆕 Test - Ferma Dissolve")]
    public void TestStopDissolve()
    {
        ForceStopDissolve();
    }
    
    [ContextMenu("🆕 Test - Reset Dissolve")]
    public void TestResetDissolve()
    {
        ResetDissolve();
    }
    
    [ContextMenu("🆕 Test - Imposta Tutti Dissolve a 1")]
    public void TestSetAllDissolveToOne()
    {
        SetAllDissolveValues(1f);
    }
    
    [ContextMenu("🆕 Test - Imposta Tutti Dissolve a 0")]
    public void TestSetAllDissolveToZero()
    {
        SetAllDissolveValues(0f);
    }
    
    [ContextMenu("🆕 Test - Imposta Tutti Dissolve a 0.5")]
    public void TestSetAllDissolveToHalf()
    {
        SetAllDissolveValues(0.5f);
    }
    
    [ContextMenu("Debug - Stato Attuale")]
    public void DebugCurrentState()
    {
        Debug.Log($"[DialogueSystem] 📊 Stato attuale:\n" +
                 $"- Attivo: {isDialogueActive}\n" +
                 $"- Linea corrente: {currentLineIndex}/{dialogueLines.Length}\n" +
                 $"- Ultima linea: {isOnLastLine}\n" +
                 $"- Audio in riproduzione: {isPlayingAudio}\n" +
                 $"- Auto finish: {autoFinishLastLine}\n" +
                 $"- Auto finish delay: {autoFinishDelay}s\n" +
                 $"- Già triggerato: {hasBeenTriggered}\n" +
                 $"🎯 Object Activation:\n" +
                 $"- Abilitato: {enableObjectActivation}\n" +
                 $"- Oggetti già attivati: {objectsAlreadyActivated}\n" +
                 $"- Object To Enable: {(objectToEnable != null ? objectToEnable.name : "NULL")}\n" +
                 $"- Object To Disable: {(objectToDisable != null ? objectToDisable.name : "NULL")}\n" +
                 $"- Objects To Enable Count: {(objectsToEnable != null ? objectsToEnable.Length : 0)}\n" +
                 $"- Objects To Disable Count: {(objectsToDisable != null ? objectsToDisable.Length : 0)}\n" +
                 $"- Activation Delay: {activationDelay}s\n" +
                 $"- Activation Message: {(string.IsNullOrEmpty(activationMessage) ? "DEFAULT" : activationMessage)}\n" +
                 $"🎭 Sub-Dialogue:\n" +
                 $"- Abilitato: {enableSubDialogue}\n" +
                 $"- Sistema: {(subDialogueSystem != null ? subDialogueSystem.name : "NULL")}\n" +
                 $"- Già triggerato: {subDialogueTriggered}\n" +
                 $"- Delay: {subDialogueDelay}s\n" +
                 $"- Messaggio: {(string.IsNullOrEmpty(subDialogueMessage) ? "DEFAULT" : subDialogueMessage)}\n" +
                 $"🌀 Dissolve Effect:\n" +
                 $"- Abilitato: {enableDissolveEffect}\n" +
                 $"- Oggetti configurati: {(dissolveObjects != null ? dissolveObjects.Length : 0)}\n" +
                 $"- Già triggerato: {dissolveTriggered}\n" +
                 $"- Con sottodialogo: {dissolveWithSubDialogue}\n" +
                 $"- Coroutine attive: {dissolveCoroutines.Count}\n" +
                 $"🔧 Dissolve Initialization:\n" +
                 $"- Forza inizializzazione: {forceInitializeDissolveValues}\n" +
                 $"- Valore iniziale: {initialDissolveValue}");
    }
    
    [ContextMenu("Debug - Traccia Eventi")]
    public void DebugTrackEvents()
    {
        Debug.Log($"[DialogueSystem] 📋 Stato eventi:\n" +
                 $"- OnLastLineReached listeners: {OnLastLineReached.GetPersistentEventCount()}\n" +
                 $"- OnLastLineFinished listeners: {OnLastLineFinished.GetPersistentEventCount()}\n" +
                 $"- OnObjectsActivated listeners: {OnObjectsActivated.GetPersistentEventCount()}\n" +
                 $"- OnSubDialogueTriggered listeners: {OnSubDialogueTriggered.GetPersistentEventCount()}\n" +
                 $"- OnDissolveStarted listeners: {OnDissolveStarted.GetPersistentEventCount()}\n" +
                 $"- OnDissolveCompleted listeners: {OnDissolveCompleted.GetPersistentEventCount()}\n" +
                 $"- OnAnyLastLineFinished subscribers: {(OnAnyLastLineFinished?.GetInvocationList()?.Length ?? 0)}\n" +
                 $"- OnAnyObjectsActivated subscribers: {(OnAnyObjectsActivated?.GetInvocationList()?.Length ?? 0)}\n" +
                 $"- OnAnySubDialogueTriggered subscribers: {(OnAnySubDialogueTriggered?.GetInvocationList()?.Length ?? 0)}\n" +
                 $"- OnAnyDissolveStarted subscribers: {(OnAnyDissolveStarted?.GetInvocationList()?.Length ?? 0)}\n" +
                 $"- OnAnyDissolveCompleted subscribers: {(OnAnyDissolveCompleted?.GetInvocationList()?.Length ?? 0)}");
    }
    
    [ContextMenu("Debug - Valida Setup Oggetti")]
    public void DebugValidateObjectSetup()
    {
        ValidateObjectActivationSetup();
        
        if (enableObjectActivation)
        {
            Debug.Log($"[DialogueSystem] 🔍 Dettagli oggetti:\n" +
                     $"- Object To Enable: {(objectToEnable != null ? $"{objectToEnable.name} (Active: {objectToEnable.activeInHierarchy})" : "NULL")}\n" +
                     $"- Object To Disable: {(objectToDisable != null ? $"{objectToDisable.name} (Active: {objectToDisable.activeInHierarchy})" : "NULL")}");
            
            if (objectsToEnable != null && objectsToEnable.Length > 0)
            {
                Debug.Log("[DialogueSystem] 📋 Objects To Enable:");
                for (int i = 0; i < objectsToEnable.Length; i++)
                {
                    var obj = objectsToEnable[i];
                    Debug.Log($"  [{i}] {(obj != null ? $"{obj.name} (Active: {obj.activeInHierarchy})" : "NULL")}");
                }
            }
            
            if (objectsToDisable != null && objectsToDisable.Length > 0)
            {
                Debug.Log("[DialogueSystem] 📋 Objects To Disable:");
                for (int i = 0; i < objectsToDisable.Length; i++)
                {
                    var obj = objectsToDisable[i];
                    Debug.Log($"  [{i}] {(obj != null ? $"{obj.name} (Active: {obj.activeInHierarchy})" : "NULL")}");
                }
            }
        }
    }
    
    [ContextMenu("🆕 Debug - Valida Setup Sub-Dialogue")]
    public void DebugValidateSubDialogueSetup()
    {
        ValidateSubDialogueSetup();
        
        if (enableSubDialogue && subDialogueSystem != null)
        {
            Debug.Log($"[DialogueSystem] 🎭 Dettagli Sub-Dialogue:\n" +
                     $"- Sistema: {subDialogueSystem.name}\n" +
                     $"- Attivo: {subDialogueSystem.gameObject.activeInHierarchy}\n" +
                     $"- Enabled: {subDialogueSystem.enabled}\n" +
                     $"- Già usato: {subDialogueTriggered}\n" +
                     $"- Delay: {subDialogueDelay}s\n" +
                     $"- Messaggio: {subDialogueMessage}");
        }
    }
    
    [ContextMenu("🆕 Debug - Valida Setup Dissolve (AGGIORNATO)")]
    public void DebugValidateDissolveSetup()
    {
        ValidateDissolveSetup();
        
        Debug.Log($"[DialogueSystem] 🔧 Impostazioni Inizializzazione:\n" +
                 $"- Forza inizializzazione: {forceInitializeDissolveValues}\n" +
                 $"- Valore iniziale: {initialDissolveValue}");
        
        if (enableDissolveEffect && dissolveObjects != null && dissolveObjects.Length > 0)
        {
            Debug.Log("[DialogueSystem] 🌀 Dettagli Dissolve Objects (con valori attuali):");
            for (int i = 0; i < dissolveObjects.Length; i++)
            {
                DissolveObject dissolveObj = dissolveObjects[i];
                if (dissolveObj.targetObject != null)
                {
                    Debug.Log($"  [{i}] {dissolveObj.targetObject.name}:\n" +
                             $"    - Active: {dissolveObj.targetObject.activeInHierarchy}\n" +
                             $"    - Materiali: {(dissolveObj.dissolveMaterials != null ? dissolveObj.dissolveMaterials.Length : 0)}\n" +
                             $"    - Proprietà: {dissolveObj.dissolvePropertyName}\n" +
                             $"    - Durata: {dissolveObj.dissolveDuration}s\n" +
                             $"    - Delay: {dissolveObj.dissolveDelay}s\n" +
                             $"    - Enable Collider: {dissolveObj.enableMeshColliderAfterDissolve}\n" +
                             $"    - Collider Delay: {dissolveObj.colliderActivationDelay}s");
                    
                    // Controlla i materiali e i loro valori attuali
                    if (dissolveObj.dissolveMaterials != null)
                    {
                        for (int j = 0; j < dissolveObj.dissolveMaterials.Length; j++)
                        {
                            Material mat = dissolveObj.dissolveMaterials[j];
                            if (mat != null)
                            {
                                bool hasProperty = mat.HasProperty(dissolveObj.dissolvePropertyName);
                                float currentValue = hasProperty ? mat.GetFloat(dissolveObj.dissolvePropertyName) : -1f;
                                string status = hasProperty ? (currentValue == 1f ? "✅ OK" : "⚠️ NON 1") : "❌ NO PROP";
                                Debug.Log($"      Material[{j}] {mat.name}: HasProperty={hasProperty}, Value={currentValue} {status}");
                            }
                            else
                            {
                                Debug.Log($"      Material[{j}] NULL");
                            }
                        }
                    }
                }
                else
                {
                    Debug.Log($"  [{i}] NULL TARGET");
                }
            }
        }
    }
    
    [ContextMenu("Test - Simula Dialogo Completo")]
    public void TestCompleteDialogue()
    {
        if (!isDialogueActive)
        {
            Debug.Log("[DialogueSystem] 🧪 Simulazione dialogo completo...");
            StartDialogue();
            
            // Simula completamento dopo un breve delay
            StartCoroutine(SimulateDialogueCompletion());
        }
        else
        {
            Debug.LogWarning("[DialogueSystem] Dialogo già attivo!");
        }
    }
    
    IEnumerator SimulateDialogueCompletion()
    {
        yield return new WaitForSeconds(1f);
        
        // Salta direttamente alla fine
        currentLineIndex = dialogueLines.Length - 1;
        isOnLastLine = true;
        DisplayLine();
        
        yield return new WaitForSeconds(1f);
        
        // Forza la fine
        NotifyLastLineFinished();
        NextLine();
        
        Debug.Log("[DialogueSystem] 🧪 Simulazione dialogo completata!");
    }
    
    [ContextMenu("🆕 Test - Simula Tutto")]
    public void TestSimulateEverything()
    {
        Debug.Log("[DialogueSystem] 🧪 Simulazione completa di tutte le funzionalità...");
        StartCoroutine(SimulateEverything());
    }
    
    IEnumerator SimulateEverything()
    {
        // 1. Avvia dialogo
        if (!isDialogueActive)
        {
            StartDialogue();
            yield return new WaitForSeconds(2f);
        }
        
        // 2. Completa dialogo
        if (isDialogueActive)
        {
            currentLineIndex = dialogueLines.Length - 1;
            isOnLastLine = true;
            DisplayLine();
            yield return new WaitForSeconds(1f);
            
            NotifyLastLineFinished();
            NextLine();
            yield return new WaitForSeconds(1f);
        }
        
        Debug.Log("[DialogueSystem] 🧪 Simulazione completa terminata!");
        Debug.Log($"[DialogueSystem] 📊 Risultati:\n" +
                 $"- Oggetti attivati: {objectsAlreadyActivated}\n" +
                 $"- Sottodialogo triggerato: {subDialogueTriggered}\n" +
                 $"- Dissolve triggerato: {dissolveTriggered}\n" +
                 $"- Coroutine dissolve attive: {dissolveCoroutines.Count}");
    }
}