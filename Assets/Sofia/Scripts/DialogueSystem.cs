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
    public UnityEvent OnLastLineFinished; // Quando finisce l'ultima battuta (QUESTO È QUELLO CHE SERVE AGLI NPC!)
    
    // Eventi statici per comunicazione globale con gli NPC
    public static event Action<DialogueSystem> OnAnyDialogueStarted;
    public static event Action<DialogueSystem> OnAnyDialogueEnded; 
    public static event Action<DialogueSystem> OnAnyLastLineFinished; // 🎯 EVENTO CHIAVE PER GLI NPC
    
    private int currentLineIndex = 0;
    private bool isDialogueActive = false;
    private bool isPlayingAudio = false;
    private bool hasBeenTriggered = false; // Per evitare ripetizioni
    private bool isOnLastLine = false; // Flag per tracciare se siamo sull'ultima battuta
    private Coroutine audioCoroutine;
    
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
                
                // ✅ Se siamo sull'ultima linea, notifica la fine PRIMA di NextLine()
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
        
        // 🎯 NOTIFICA INIZIO DIALOGO
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
            
            // 🎯 CONTROLLA SE SIAMO SULL'ULTIMA BATTUTA
            isOnLastLine = (currentLineIndex == dialogueLines.Length - 1);
            if (isOnLastLine)
            {
                Debug.Log($"[DialogueSystem] 🎯 ULTIMA BATTUTA RAGGIUNTA: '{lineToShow}' - ASPETTO SPACE PER FINIRE");
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
                isPlayingAudio = false;
                // ✅ Se non c'è audio E siamo sull'ultima linea, 
                // il dialogo resta attivo aspettando Space
                Debug.Log($"[DialogueSystem] Nessun audio per questa linea. Ultima linea: {isOnLastLine}");
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
    /// ✅ CORRETTO: Gestisce la fine dell'audio senza far avanzare automaticamente l'ultima battuta
    /// </summary>
    IEnumerator WaitForAudioToEnd(float audioDuration)
    {
        yield return new WaitForSeconds(audioDuration);
        isPlayingAudio = false;
        
        // ✅ CORRETTO: Se siamo sull'ultima linea, NON fare nulla automaticamente
        if (isDialogueActive && isOnLastLine)
        {
            Debug.Log("[DialogueSystem] 🎯 Audio ultima battuta finito - ASPETTO che il player prema Space per finire!");
            // NON chiamare NotifyLastLineFinished() qui!
            // NON chiamare NextLine() qui!
            yield break; // Esci e aspetta input del player
        }
        
        // Solo se NON siamo sull'ultima linea, passa automaticamente
        if (isDialogueActive && !isOnLastLine)
        {
            Debug.Log("[DialogueSystem] Audio finito, passo automaticamente alla linea successiva");
            NextLine();
        }
    }
    
    /// <summary>
    /// 🎯 METODO CHIAVE: Notifica che l'ultima battuta è finita
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
        
        // Ferma la coroutine dell'audio se attiva
        if (audioCoroutine != null)
        {
            StopCoroutine(audioCoroutine);
            audioCoroutine = null;
        }
        
        // Riabilita il salto
        EnablePlayerJumpOnly();
        
        // 🎯 NOTIFICA FINE DIALOGO
        OnDialogueEnded?.Invoke();
        OnAnyDialogueEnded?.Invoke(this);
        
        Debug.Log("[DialogueSystem] Dialogo terminato.");
    }
    
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
    }
    
    // Metodo pubblico per controllare se il dialogo è attivo
    public bool IsDialogueActive()
    {
        return isDialogueActive;
    }
    
    // 🎯 NUOVO: Metodo per sapere se siamo sull'ultima battuta
    public bool IsOnLastLine()
    {
        return isOnLastLine;
    }
    
    // 🎯 NUOVO: Metodo per sapere quante battute ci sono in totale
    public int GetTotalLines()
    {
        return dialogueLines.Length;
    }
    
    // 🎯 NUOVO: Metodo per sapere su che battuta siamo attualmente
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
    
    // ✅ AGGIUNTA: Metodo per forzare la fine dell'ultima battuta (per testing)
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
    
    [ContextMenu("Debug - Stato Attuale")]
    public void DebugCurrentState()
    {
        Debug.Log($"[DialogueSystem] 📊 Stato attuale:\n" +
                 $"- Attivo: {isDialogueActive}\n" +
                 $"- Linea corrente: {currentLineIndex}/{dialogueLines.Length}\n" +
                 $"- Ultima linea: {isOnLastLine}\n" +
                 $"- Audio in riproduzione: {isPlayingAudio}\n" +
                 $"- Già triggerato: {hasBeenTriggered}");
    }
    
    /// <summary>
    /// ✅ METODO DI DEBUG: Traccia gli eventi
    /// </summary>
    [ContextMenu("Debug - Traccia Eventi")]
    public void DebugTrackEvents()
    {
        Debug.Log($"[DialogueSystem] 📋 Stato eventi:\n" +
                 $"- OnLastLineReached listeners: {OnLastLineReached.GetPersistentEventCount()}\n" +
                 $"- OnLastLineFinished listeners: {OnLastLineFinished.GetPersistentEventCount()}\n" +
                 $"- OnAnyLastLineFinished subscribers: {(OnAnyLastLineFinished?.GetInvocationList()?.Length ?? 0)}");
    }
}