using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

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
    
    [Header("Typing Effect (Optional)")]
    public bool useTypingEffect = true;
    public float typingSpeed = 0.05f; // Velocità dell'effetto macchina da scrivere
    
    [Header("Input Settings")]
    public KeyCode nextLineKey = KeyCode.Space; // Tasto per passare alla battuta successiva
    public KeyCode cancelDialogueKey = KeyCode.Escape; // Tasto per chiudere manualmente il dialogo
    
    [Header("Player Control")]
    public ThirdPersonController playerController; // Reference al ThirdPersonController
    
    private int currentLineIndex = 0;
    private bool isDialogueActive = false;
    private bool isTyping = false;
    private bool isPlayingAudio = false;
    private bool hasBeenTriggered = false; // Per evitare ripetizioni
    private Coroutine typingCoroutine;
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
                if (isTyping)
                {
                    // Se il testo sta ancora apparendo, completa immediatamente la linea
                    CompleteCurrentLine();
                }
                else
                {
                    // Sempre passa alla prossima battuta, interrompendo l'audio se necessario
                    if (isPlayingAudio)
                    {
                        StopCurrentAudio();
                    }
                    NextLine();
                }
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
        if (dialogueLines.Length == 0) return;
        
        isDialogueActive = true;
        hasBeenTriggered = true; // Segna come già triggerato
        currentLineIndex = 0;
        
        // Attiva l'UI del dialogo
        if (dialogueUI != null)
            dialogueUI.SetActive(true);
        
        // Disabilita SOLO il salto, mantiene movimento
        DisablePlayerJumpOnly();
        
        // Mostra la prima battuta
        DisplayLine();
        
        Debug.Log("[DialogueSystem] Dialogo iniziato. Usa " + nextLineKey + " per continuare, " + cancelDialogueKey + " per chiudere.");
    }
    
    void DisplayLine()
    {
        if (currentLineIndex < dialogueLines.Length)
        {
            DialogueLine currentLine = dialogueLines[currentLineIndex];
            string lineToShow = currentLine.text;
            
            // Riproduci l'audio se presente
            if (currentLine.audioClip != null && audioSource != null)
            {
                PlayAudioForLine(currentLine.audioClip);
            }
            else
            {
                isPlayingAudio = false;
                // Se non c'è audio, la linea resta fino a quando non premi Space
            }
            
            if (useTypingEffect)
            {
                // Avvia l'effetto macchina da scrivere
                if (typingCoroutine != null)
                    StopCoroutine(typingCoroutine);
                
                typingCoroutine = StartCoroutine(TypeLine(lineToShow));
            }
            else
            {
                // Mostra il testo immediatamente
                dialogueText.text = lineToShow;
                isTyping = false;
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
    
    IEnumerator WaitForAudioToEnd(float audioDuration)
    {
        yield return new WaitForSeconds(audioDuration);
        isPlayingAudio = false;
        
        // Quando l'audio finisce, passa automaticamente alla linea successiva
        if (isDialogueActive) // Controlla che il dialogo sia ancora attivo
        {
            NextLine();
        }
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
    
    IEnumerator TypeLine(string line)
    {
        isTyping = true;
        dialogueText.text = "";
        
        foreach (char letter in line.ToCharArray())
        {
            dialogueText.text += letter;
            yield return new WaitForSeconds(typingSpeed);
        }
        
        isTyping = false;
    }
    
    void CompleteCurrentLine()
    {
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            dialogueText.text = dialogueLines[currentLineIndex].text;
            isTyping = false;
        }
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
        isDialogueActive = false;
        currentLineIndex = 0;
        isPlayingAudio = false;
        
        // Ferma l'audio se in riproduzione
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
        }
        
        // Nascondi l'UI del dialogo
        if (dialogueUI != null)
            dialogueUI.SetActive(false);
        
        // Ferma tutte le coroutine
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }
        
        if (audioCoroutine != null)
        {
            StopCoroutine(audioCoroutine);
            audioCoroutine = null;
        }
        
        // Riabilita il salto
        EnablePlayerJumpOnly();
        
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
    
    // Metodo per forzare la chiusura del dialogo da altri script
    public void ForceEndDialogue()
    {
        EndDialogue();
    }
    
    // ✅ NUOVO: Metodo per controllare se stiamo riproducendo audio
    public bool IsPlayingAudio()
    {
        return isPlayingAudio;
    }
    
    // ✅ NUOVO: Metodo per ottenere la linea di dialogo corrente
    public DialogueLine GetCurrentLine()
    {
        if (currentLineIndex < dialogueLines.Length)
            return dialogueLines[currentLineIndex];
        return null;
    }
}