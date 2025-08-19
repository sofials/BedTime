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
    public UnityEvent OnObjectsActivated; // 🆕 Quando gli oggetti vengono attivati
    
    // Eventi statici per comunicazione globale con gli NPC
    public static event Action<DialogueSystem> OnAnyDialogueStarted;
    public static event Action<DialogueSystem> OnAnyDialogueEnded; 
    public static event Action<DialogueSystem> OnAnyLastLineFinished;
    public static event Action<DialogueSystem> OnAnyObjectsActivated; // 🆕 Evento globale per attivazione oggetti
    
    private int currentLineIndex = 0;
    private bool isDialogueActive = false;
    private bool isPlayingAudio = false;
    private bool hasBeenTriggered = false; // Per evitare ripetizioni
    private bool isOnLastLine = false; // Flag per tracciare se siamo sull'ultima battuta
    private bool objectsAlreadyActivated = false; // 🆕 Per evitare attivazioni multiple
    private Coroutine audioCoroutine;
    private Coroutine autoFinishCoroutine;
    
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
        objectsAlreadyActivated = false; // 🆕 Reset flag attivazione oggetti
        
        // Attiva l'UI del dialogo
        if (dialogueUI != null)
        {
            dialogueUI.SetActive(true);
            Debug.Log("[DEBUG] DialogueUI attivato");
        }
        
        // Disabilita SOLO il salto, mantiene movimento
        DisablePlayerJumpOnly();
        
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
        
        // Riabilita il salto
        EnablePlayerJumpOnly();
        
        // 🆕 ATTIVA OGGETTI SE ABILITATO
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
        
        // Notifica fine dialogo
        OnDialogueEnded?.Invoke();
        OnAnyDialogueEnded?.Invoke(this);
        
        Debug.Log("[DialogueSystem] Dialogo terminato.");
    }
    
    // ========== OBJECT ACTIVATION SYSTEM ==========
    
    /// <summary>
    /// 🆕 Attiva gli oggetti con ritardo se specificato
    /// </summary>
    IEnumerator ActivateObjectsWithDelay()
    {
        Debug.Log($"[DialogueSystem] Attivazione oggetti ritardata di {activationDelay} secondi...");
        yield return new WaitForSeconds(activationDelay);
        ActivateObjects();
    }
    
    /// <summary>
    /// 🆕 Attiva/disattiva gli oggetti configurati
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
    
    /// <summary>
    /// 🆕 Valida il setup degli oggetti all'avvio
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
    /// 🆕 Abilita/disabilita il sistema di attivazione oggetti
    /// </summary>
    public void SetObjectActivationEnabled(bool enabled)
    {
        enableObjectActivation = enabled;
        Debug.Log($"[DialogueSystem] Object Activation {(enabled ? "abilitato" : "disabilitato")}");
    }
    
    /// <summary>
    /// 🆕 Imposta l'oggetto da attivare
    /// </summary>
    public void SetObjectToEnable(GameObject obj)
    {
        objectToEnable = obj;
        Debug.Log($"[DialogueSystem] Oggetto da attivare impostato: {(obj != null ? obj.name : "NULL")}");
    }
    
    /// <summary>
    /// 🆕 Imposta l'oggetto da disattivare
    /// </summary>
    public void SetObjectToDisable(GameObject obj)
    {
        objectToDisable = obj;
        Debug.Log($"[DialogueSystem] Oggetto da disattivare impostato: {(obj != null ? obj.name : "NULL")}");
    }
    
    /// <summary>
    /// 🆕 Imposta il messaggio di attivazione
    /// </summary>
    public void SetActivationMessage(string message)
    {
        activationMessage = message;
        Debug.Log($"[DialogueSystem] Messaggio di attivazione impostato: {message}");
    }
    
    /// <summary>
    /// 🆕 Imposta il ritardo di attivazione
    /// </summary>
    public void SetActivationDelay(float delay)
    {
        activationDelay = delay;
        Debug.Log($"[DialogueSystem] Ritardo attivazione impostato: {delay}s");
    }
    
    /// <summary>
    /// 🆕 Forza l'attivazione degli oggetti (per test)
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
    /// 🆕 Reset del flag di attivazione (per dialoghi ripetibili)
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
    
    // Disabilita SOLO il salto, mantiene movimento e rotazione
    void DisablePlayerJumpOnly()
    {
        if (playerController != null)
        {
            playerController.SetJumpEnabled(false);
            Debug.Log("[DialogueSystem] Salto del player disabilitato durante dialogo");
        }
        else
        {
            // Fallback: cerca automaticamente il ThirdPersonController
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                ThirdPersonController tpc = player.GetComponent<ThirdPersonController>();
                if (tpc != null)
                {
                    tpc.SetJumpEnabled(false);
                    Debug.Log("[DialogueSystem] Salto disabilitato via auto-found ThirdPersonController");
                }
            }
        }
    }
    
    // Riabilita SOLO il salto
    void EnablePlayerJumpOnly()
    {
        if (playerController != null)
        {
            playerController.SetJumpEnabled(true);
            Debug.Log("[DialogueSystem] Salto del player riabilitato");
        }
        else
        {
            // Fallback: cerca automaticamente il ThirdPersonController
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                ThirdPersonController tpc = player.GetComponent<ThirdPersonController>();
                if (tpc != null)
                {
                    tpc.SetJumpEnabled(true);
                    Debug.Log("[DialogueSystem] Salto riabilitato via auto-found ThirdPersonController");
                }
            }
        }
    }
    
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
    
    // ========== DEBUG METHODS ==========
    
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
                 $"- Activation Message: {(string.IsNullOrEmpty(activationMessage) ? "DEFAULT" : activationMessage)}");
    }
    
    [ContextMenu("Debug - Traccia Eventi")]
    public void DebugTrackEvents()
    {
        Debug.Log($"[DialogueSystem] 📋 Stato eventi:\n" +
                 $"- OnLastLineReached listeners: {OnLastLineReached.GetPersistentEventCount()}\n" +
                 $"- OnLastLineFinished listeners: {OnLastLineFinished.GetPersistentEventCount()}\n" +
                 $"- OnObjectsActivated listeners: {OnObjectsActivated.GetPersistentEventCount()}\n" +
                 $"- OnAnyLastLineFinished subscribers: {(OnAnyLastLineFinished?.GetInvocationList()?.Length ?? 0)}\n" +
                 $"- OnAnyObjectsActivated subscribers: {(OnAnyObjectsActivated?.GetInvocationList()?.Length ?? 0)}");
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
}