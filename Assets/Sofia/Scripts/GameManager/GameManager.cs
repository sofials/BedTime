using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("UI References")]
    public GameObject startMenu; // Riferimento locale per Title Screen
    public GameObject pauseMenu; // Riferimento al menu di pausa (trovato automaticamente)
    
    // NUOVO: Riferimento al componente PauseMenuUI
    private PauseMenuUI pauseMenuUI;

    [Header("Scene Management")]
    [SerializeField] private SceneController sceneController;
    [SerializeField] private string mainMenuSceneName = "Title Screen"; // Nome della scena del menu principale
    
    [Header("Cursor Settings")]
    [SerializeField] private bool debugCursorState = false; // Per debugging

    // NUOVO: Stato di pausa
    private bool isPaused = false;

    // Eventi per compatibilità con SceneManager
    public System.Action<string> OnSceneReady;
    public System.Action<bool> OnPauseStateChanged; // NUOVO: Evento per notificare cambio stato pausa

    private void Awake()
    {
        // Singleton semplice
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        // FIX: Usa coroutine per dare tempo alla scena di inizializzarsi
        StartCoroutine(InitializeAfterSceneLoad());
    }

    /// <summary>
    /// NUOVO: Inizializzazione ritardata per dare tempo alla scena di caricarsi completamente
    /// </summary>
    private IEnumerator InitializeAfterSceneLoad()
    {
        // Aspetta qualche frame per permettere alla scena di inizializzarsi
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();
        
        string currentScene = SceneManager.GetActiveScene().name;
        Debug.Log($"[GameManager] Inizializzazione ritardata per scena: {currentScene}");
        
        // Trova il menu di pausa
        FindPauseMenuInScene();
        
        if (currentScene == "Title Screen")
        {
            HandleTitleScreen();
        }
        else
        {
            HandleGameScene();
        }
    }

    /// <summary>
    /// MODIFICATO: Trova automaticamente il menu di pausa nella scena corrente con più tentativi
    /// </summary>
    private void FindPauseMenuInScene()
    {
        // Reset riferimenti precedenti se stiamo cambiando scena
        if (pauseMenu != null && pauseMenu.scene != SceneManager.GetActiveScene())
        {
            Debug.Log("[GameManager] Reset riferimento pause menu (scena cambiata)");
            pauseMenu = null;
            pauseMenuUI = null;
        }
        
        if (pauseMenu == null)
        {
            // Lista di nomi possibili per il menu di pausa
            string[] possibleNames = { 
                "PauseMenu", 
                "Pause Menu", 
                "MenuPausa", 
                "Menu Pausa",
                "PauseCanvas",
                "CanvasMenuPausa",
                "UI_PauseMenu"
            };
            
            GameObject foundPauseMenu = null;
            
            // Cerca per nome
            foreach (string name in possibleNames)
            {
                foundPauseMenu = GameObject.Find(name);
                if (foundPauseMenu != null)
                {
                    Debug.Log($"[GameManager] Menu di pausa trovato con nome: {name}");
                    break;
                }
            }
            
            // Se non trovato per nome, cerca per tag
            if (foundPauseMenu == null)
            {
                foundPauseMenu = GameObject.FindGameObjectWithTag("PauseMenu");
                if (foundPauseMenu != null)
                {
                    Debug.Log("[GameManager] Menu di pausa trovato con tag 'PauseMenu'");
                }
            }
            
            // Se ancora non trovato, cerca tutti i Canvas e controlla i loro figli
            if (foundPauseMenu == null)
            {
                Canvas[] allCanvas = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
                foreach (Canvas canvas in allCanvas)
                {
                    // Controlla se il canvas stesso ha un nome che suggerisce sia il menu di pausa
                    foreach (string name in possibleNames)
                    {
                        if (canvas.name.Contains(name.Replace(" ", "")))
                        {
                            foundPauseMenu = canvas.gameObject;
                            Debug.Log($"[GameManager] Menu di pausa trovato in Canvas: {canvas.name}");
                            break;
                        }
                    }
                    
                    if (foundPauseMenu != null) break;
                    
                    // MODIFICATO: Controlla tutti i figli del canvas, anche quelli disattivati
                    Transform[] allChildren = canvas.GetComponentsInChildren<Transform>(true); // true = include inattivi
                    foreach (Transform child in allChildren)
                    {
                        if (child == canvas.transform) continue; // Salta il genitore stesso
                        
                        foreach (string name in possibleNames)
                        {
                            if (child.name.Contains(name.Replace(" ", "")))
                            {
                                foundPauseMenu = child.gameObject;
                                Debug.Log($"[GameManager] Menu di pausa trovato come figlio di Canvas: {child.name} (era {(child.gameObject.activeSelf ? "attivo" : "disattivato")})");
                                break;
                            }
                        }
                        if (foundPauseMenu != null) break;
                    }
                    
                    if (foundPauseMenu != null) break;
                }
            }
            
            // NUOVO: Se ancora non trovato, cerca oggetti disattivati usando Resources.FindObjectsOfTypeAll
            if (foundPauseMenu == null)
            {
                Debug.Log("[GameManager] Ricerca oggetti disattivati...");
                
                GameObject[] allGameObjects = Resources.FindObjectsOfTypeAll<GameObject>();
                foreach (GameObject obj in allGameObjects)
                {
                    // Escludi oggetti che non sono nella scena (prefab, asset, ecc.)
                    if (obj.scene.IsValid() && obj.scene == UnityEngine.SceneManagement.SceneManager.GetActiveScene())
                    {
                        foreach (string name in possibleNames)
                        {
                            if (obj.name.Contains(name.Replace(" ", "")))
                            {
                                foundPauseMenu = obj;
                                Debug.Log($"[GameManager] Menu di pausa trovato tra oggetti disattivati: {obj.name}");
                                break;
                            }
                        }
                        if (foundPauseMenu != null) break;
                    }
                }
            }
            
            // NUOVO: Cerca anche per componente PauseMenuUI
            if (foundPauseMenu == null)
            {
                Debug.Log("[GameManager] Ricerca tramite componente PauseMenuUI...");
                
                PauseMenuUI[] allPauseMenuUIs = Resources.FindObjectsOfTypeAll<PauseMenuUI>();
                foreach (PauseMenuUI pauseUI in allPauseMenuUIs)
                {
                    if (pauseUI.gameObject.scene.IsValid() && 
                        pauseUI.gameObject.scene == UnityEngine.SceneManagement.SceneManager.GetActiveScene())
                    {
                        foundPauseMenu = pauseUI.gameObject;
                        Debug.Log($"[GameManager] Menu di pausa trovato tramite componente PauseMenuUI: {pauseUI.gameObject.name}");
                        break;
                    }
                }
            }
            
            if (foundPauseMenu != null)
            {
                pauseMenu = foundPauseMenu;
                
                // NUOVO: Cerca anche il componente PauseMenuUI
                pauseMenuUI = pauseMenu.GetComponent<PauseMenuUI>();
                if (pauseMenuUI == null)
                {
                    // Se non trovato sul GameObject principale, cerca nei genitori/figli
                    pauseMenuUI = pauseMenu.GetComponentInParent<PauseMenuUI>();
                    if (pauseMenuUI == null)
                        pauseMenuUI = pauseMenu.GetComponentInChildren<PauseMenuUI>();
                }
                
                if (pauseMenuUI != null)
                {
                    Debug.Log($"[GameManager] PauseMenuUI trovato su: {pauseMenuUI.gameObject.name}");
                }
                else
                {
                    Debug.LogWarning($"[GameManager] PauseMenuUI non trovato! Assicurati che il componente PauseMenuUI sia attaccato a {pauseMenu.name} o a un oggetto correlato.");
                }
                
                Debug.Log($"[GameManager] Menu di pausa assegnato: {pauseMenu.name} nella scena {SceneManager.GetActiveScene().name}");
                
                // Assicurati che sia nascosto inizialmente
                HidePauseMenu();
            }
            else
            {
                Debug.LogWarning($"[GameManager] Menu di pausa NON trovato nella scena: {SceneManager.GetActiveScene().name}");
                Debug.LogWarning("[GameManager] Assicurati che ci sia un oggetto chiamato 'PauseMenu', 'Pause Menu' o con tag 'PauseMenu'");
                
                // Lista tutti gli oggetti nella scena per debug
                if (debugCursorState)
                {
                    Debug.Log("[GameManager] Oggetti nella scena corrente:");
                    GameObject[] allObjects = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
                    foreach (GameObject obj in allObjects)
                    {
                        if (obj.name.ToLower().Contains("pause") || obj.name.ToLower().Contains("menu"))
                        {
                            Debug.Log($"  - {obj.name} (attivo: {obj.activeSelf})");
                        }
                    }
                }
            }
        }
        else
        {
            Debug.Log($"[GameManager] Menu di pausa già assegnato: {pauseMenu.name}");
            
            // Assicurati che il componente PauseMenuUI sia trovato anche se il GameObject è già assegnato
            if (pauseMenuUI == null)
            {
                pauseMenuUI = pauseMenu.GetComponent<PauseMenuUI>();
                if (pauseMenuUI == null)
                {
                    pauseMenuUI = pauseMenu.GetComponentInParent<PauseMenuUI>();
                    if (pauseMenuUI == null)
                        pauseMenuUI = pauseMenu.GetComponentInChildren<PauseMenuUI>();
                }
                
                if (pauseMenuUI != null)
                {
                    Debug.Log($"[GameManager] PauseMenuUI trovato su: {pauseMenuUI.gameObject.name}");
                }
            }
        }
        
        // IMPORTANTE: Gestisci EventSystem duplicati
        HandleDuplicateEventSystems();
    }

    /// <summary>
    /// Rimuove EventSystem duplicati per evitare conflitti UI
    /// </summary>
    private void HandleDuplicateEventSystems()
    {
        var eventSystems = Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None);
        
        if (eventSystems.Length > 1)
        {
            Debug.LogWarning($"[GameManager] Trovati {eventSystems.Length} EventSystem! Rimuovo i duplicati...");
            
            // Mantieni solo il primo EventSystem attivo, distruggi gli altri
            for (int i = 1; i < eventSystems.Length; i++)
            {
                Debug.Log($"[GameManager] Distruggo EventSystem duplicato: {eventSystems[i].name}");
                Destroy(eventSystems[i].gameObject);
            }
        }
        else
        {
            Debug.Log($"[GameManager] EventSystem OK - trovato {eventSystems.Length} EventSystem");
        }
    }

    // MODIFICATO: Aggiunto controllo input ESC
    private void Update()
    {
        // FIX: Se il menu di pausa non è stato trovato, riprova a cercarlo
        if (pauseMenu == null && !IsInMainMenu())
        {
            FindPauseMenuInScene();
        }
        
        // Gestione ESC per menu di pausa (solo se non siamo nel menu principale)
        if (Input.GetKeyDown(KeyCode.Escape) && !IsInMainMenu())
        {
            TogglePause();
        }

        // Debug cursore (esistente)
        if (debugCursorState && !IsInMainMenu() && !isPaused)
        {
            // Forza lo stato del cursore se si "sblocca" accidentalmente (solo se non in pausa)
            if (Cursor.visible || Cursor.lockState != CursorLockMode.Locked)
            {
                Debug.LogWarning("[GameManager] Cursore si è sbloccato, lo riforzo nascosto");
                SetGameCursorState();
            }
        }
    }

    private void HandleTitleScreen()
    {
        // Nel menu principale, cerca il startMenu solo se siamo effettivamente nel Title Screen
        if (startMenu != null)
            startMenu.SetActive(true);
        else
        {
            // Prova a trovare il menu iniziale nella scena corrente
            GameObject foundStartMenu = GameObject.Find("StartMenu");
            if (foundStartMenu == null)
                foundStartMenu = GameObject.Find("Main Menu");
            if (foundStartMenu == null)
                foundStartMenu = GameObject.Find("MenuIniziale");
                
            if (foundStartMenu != null)
            {
                startMenu = foundStartMenu;
                startMenu.SetActive(true);
                Debug.Log($"[GameManager] Menu iniziale trovato automaticamente: {startMenu.name}");
            }
        }
            
        // Assicurati che il menu di pausa sia nascosto nel menu principale
        HidePauseMenu();
            
        // Configurazione cursore per menu
        isPaused = false;
        Time.timeScale = 1f;
        SetMenuCursorState();
        
        Debug.Log("[GameManager] Configurazione Title Screen - Cursore visibile");
    }

    private void HandleGameScene()
    {
        // Assicurati che il menu di pausa sia nascosto all'inizio
        HidePauseMenu();
            
        // Configurazione cursore per gioco
        isPaused = false;
        Time.timeScale = 1f;
        SetGameCursorState();
        
        Debug.Log("[GameManager] Configurazione Game Scene - Cursore nascosto");
        
        // Notifica lo SceneManager che il GameManager è pronto
        StartCoroutine(NotifySceneManagerAfterDelay());
    }

    // ========== GESTIONE MENU DI PAUSA ==========

    /// <summary>
    /// Alterna lo stato di pausa
    /// </summary>
    public void TogglePause()
    {
        if (IsInMainMenu()) return; // Non mettere in pausa nel menu principale
        
        // FIX: Se il menu di pausa non è stato trovato, riprova a cercarlo prima di procedere
        if (pauseMenu == null)
        {
            Debug.LogWarning("[GameManager] Menu di pausa non trovato, tentativo di ricerca...");
            FindPauseMenuInScene();
            
            if (pauseMenu == null)
            {
                Debug.LogError("[GameManager] Impossibile trovare il menu di pausa! Toggle pause annullato.");
                return;
            }
        }
        
        SetGamePaused(!isPaused);
    }

    /// <summary>
    /// Riprendi il gioco (chiudi menu di pausa) - METODO PUBBLICO PER I BOTTONI UI
    /// CORRETTO: Usa Invoke per evitare problemi di timeScale
    /// </summary>
    public void ResumeGame()
    {
        Debug.Log("[GameManager] Resume Game chiamato");
        if (isPaused)
        {
            // Ripristina tutto immediatamente
            isPaused = false;
            Time.timeScale = 1f;
            
            HidePauseMenu();
            
            Debug.Log("[GameManager] Gioco ripreso completamente dal menu di pausa");
            
            // Usa Invoke per sistemare il cursore nel prossimo frame
            Invoke(nameof(DoResumeGameCursor), 0.01f);
        }
    }
    
    private void DoResumeGameCursor()
    {
        // Sistema il cursore
        SetGameCursorState();
        
        // Notifica altri sistemi del cambio di stato
        OnPauseStateChanged?.Invoke(false);
        
        Debug.Log("[GameManager] Cursore sistemato dopo resume");
    }

    /// <summary>
    /// Pausa il gioco (apri menu di pausa)
    /// </summary>
    public void PauseGame()
    {
        if (!isPaused && !IsInMainMenu())
        {
            SetGamePaused(true);
        }
    }

    /// <summary>
    /// NUOVO: Torna al menu principale (per il bottone del menu di pausa)
    /// CORRETTO: Usa il nome della scena configurabile invece di riferimento diretto
    /// </summary>
    public void GoToMainMenu()
    {
        Debug.Log($"[GameManager] Tornando al menu principale ({mainMenuSceneName})...");
        
        // Ripristina immediatamente lo stato di gioco
        if (isPaused)
        {
            isPaused = false;
            Time.timeScale = 1f;
            
            HidePauseMenu();
            
            Debug.Log("[GameManager] Time.timeScale ripristinato a 1, menu nascosto");
        }
        
        // Usa Invoke per eseguire il caricamento nel prossimo frame
        Invoke(nameof(DoGoToMainMenu), 0.01f);
    }
    
    private void DoGoToMainMenu()
    {
        Debug.Log($"[GameManager] Eseguendo caricamento menu principale: {mainMenuSceneName}");
        LoadSceneWithFade(mainMenuSceneName);
    }

    /// <summary>
    /// NUOVO: Riavvia il livello corrente (per il bottone del menu di pausa)
    /// CORRETTO: Usa Invoke per eseguire dopo un frame, evitando problemi di timeScale
    /// </summary>
    public void RestartLevel()
    {
        Debug.Log("[GameManager] Riavviando il livello corrente...");
        
        string currentScene = SceneManager.GetActiveScene().name;
        Debug.Log($"[GameManager] Scena corrente da riavviare: {currentScene}");
        
        // Salva il nome della scena per l'Invoke
        currentSceneToRestart = currentScene;
        
        // Ripristina immediatamente lo stato di gioco
        if (isPaused)
        {
            isPaused = false;
            Time.timeScale = 1f;
            
            HidePauseMenu();
            
            Debug.Log("[GameManager] Time.timeScale ripristinato a 1, menu nascosto");
        }
        
        // Usa Invoke per eseguire il caricamento nel prossimo frame
        Invoke(nameof(DoRestartLevel), 0.01f);
    }
    
    private string currentSceneToRestart;
    private void DoRestartLevel()
    {
        Debug.Log($"[GameManager] Eseguendo riavvio livello: {currentSceneToRestart}");
        LoadSceneWithFade(currentSceneToRestart);
    }

    /// <summary>
    /// Ottieni lo stato di pausa corrente
    /// </summary>
    public bool IsPaused()
    {
        return isPaused;
    }
    
    /// <summary>
    /// NUOVO: Mostra il menu di pausa usando PauseMenuUI se disponibile, altrimenti fallback su GameObject
    /// </summary>
    private void ShowPauseMenu()
    {
        if (pauseMenuUI != null)
        {
            pauseMenuUI.Show();
            Debug.Log("[GameManager] Menu mostrato tramite PauseMenuUI");
        }
        else if (pauseMenu != null)
        {
            pauseMenu.SetActive(true);
            Debug.Log("[GameManager] Menu mostrato tramite GameObject (fallback)");
        }
        else
        {
            Debug.LogWarning("[GameManager] Nessun menu di pausa disponibile per essere mostrato!");
        }
    }
    
    /// <summary>
    /// NUOVO: Nascondi il menu di pausa usando PauseMenuUI se disponibile, altrimenti fallback su GameObject
    /// </summary>
    private void HidePauseMenu()
    {
        if (pauseMenuUI != null)
        {
            pauseMenuUI.Hide();
            Debug.Log("[GameManager] Menu nascosto tramite PauseMenuUI");
        }
        else if (pauseMenu != null)
        {
            pauseMenu.SetActive(false);
            Debug.Log("[GameManager] Menu nascosto tramite GameObject (fallback)");
        }
    }
    
    /// <summary>
    /// Imposta il cursore per il gameplay (nascosto e bloccato)
    /// </summary>
    private void SetGameCursorState()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
    
    /// <summary>
    /// Imposta il cursore per i menu (visibile e libero)
    /// </summary>
    private void SetMenuCursorState()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
    
    private IEnumerator NotifySceneManagerAfterDelay()
    {
        // Piccolo delay per assicurarsi che tutto sia inizializzato
        yield return new WaitForSeconds(0.1f);
        
        // Ri-forza lo stato del cursore dopo l'inizializzazione
        if (!IsInMainMenu() && !isPaused)
        {
            SetGameCursorState();
        }
        
        string currentScene = SceneManager.GetActiveScene().name;
        
        // Cerca lo SceneManager specifico della scena
        string[] possibleNames = { 
            "SceneManager00", "SceneManager01", "SceneManager02",
            "SceneManager", currentScene + "_Manager" 
        };
        
        GameObject sceneManagerObj = null;
        foreach (string name in possibleNames)
        {
            sceneManagerObj = GameObject.Find(name);
            if (sceneManagerObj != null) break;
        }
        
        if (sceneManagerObj != null)
        {
            // Notifica tramite SendMessage (compatibilità con SceneManager00)
            sceneManagerObj.SendMessage("OnGameManagerReady", SendMessageOptions.DontRequireReceiver);
        }
        
        // Notifica anche tramite evento
        OnSceneReady?.Invoke(currentScene);
    }

    // ========== METODI PUBBLICI PER CARICAMENTO SCENE ==========
    
    /// <summary>
    /// Carica una scena usando il SceneController per il fade
    /// </summary>
    public void LoadSceneWithFade(string sceneName)
    {
        Debug.Log($"[GameManager] Tentativo caricamento scena con fade: {sceneName}");
        
        // Se siamo in pausa, riprendi prima di caricare
        if (isPaused)
        {
            Debug.Log("[GameManager] Riprendendo dal menu di pausa prima del caricamento...");
            isPaused = false;
            Time.timeScale = 1f;
            
            HidePauseMenu();
        }
        
        // Trova il SceneController nella scena corrente
        if (sceneController == null)
        {
            sceneController = Object.FindFirstObjectByType<SceneController>();
        }
        
        if (sceneController != null)
        {
            Debug.Log($"[GameManager] Caricamento scena con fade: {sceneName}");
            sceneController.LoadScene(sceneName);
        }
        else
        {
            // Fallback senza fade
            Debug.LogWarning($"[GameManager] SceneController non trovato, caricamento senza fade: {sceneName}");
            SceneManager.LoadScene(sceneName);
        }
    }
    
    /// <summary>
    /// Carica una scena direttamente senza fade (per compatibilità)
    /// </summary>
    public void LoadScene(string sceneName)
    {
        // Se siamo in pausa, riprendi prima di caricare
        if (isPaused)
        {
            isPaused = false;
            Time.timeScale = 1f;
            
            HidePauseMenu();
        }
        
        Debug.Log($"[GameManager] Caricamento diretto scena: {sceneName}");
        SceneManager.LoadScene(sceneName);
    }

    /// <summary>
    /// Riavvia la scena corrente con fade (COMPATIBILITÀ - usa RestartLevel)
    /// </summary>
    public void RestartCurrentScene()
    {
        RestartLevel();
    }

    /// <summary>
    /// Torna al menu principale (COMPATIBILITÀ - usa GoToMainMenu)
    /// </summary>
    public void ReturnToMainMenu()
    {
        GoToMainMenu();
    }

    /// <summary>
    /// NUOVO: Imposta il nome della scena del menu principale
    /// </summary>
    public void SetMainMenuSceneName(string sceneName)
    {
        mainMenuSceneName = sceneName;
        Debug.Log($"[GameManager] Nome scena menu principale impostato a: {mainMenuSceneName}");
    }

    /// <summary>
    /// Ottieni il nome della scena del menu principale
    /// </summary>
    public string GetMainMenuSceneName()
    {
        return mainMenuSceneName;
    }

    /// <summary>
    /// Esci dal gioco
    /// </summary>
    public void ExitGame()
    {
        Debug.Log("[GameManager] Uscita dal gioco richiesta");
        
        #if UNITY_EDITOR
            // In editor, ferma il play mode
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            // In build, chiudi l'applicazione
            Application.Quit();
        #endif
    }

    // ========== GESTIONE EVENTI SCENA ==========
    
    /// <summary>
    /// MODIFICATO: Chiamato automaticamente quando una scena viene caricata
    /// </summary>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"[GameManager] Scena caricata: {scene.name}");
        
        // Reset stato pausa per nuova scena
        isPaused = false;
        Time.timeScale = 1f;
        
        // Reset riferimenti pause menu (verrà trovato di nuovo)
        pauseMenu = null;
        pauseMenuUI = null;
        
        // FIX: Usa coroutine per dare tempo alla scena di inizializzarsi prima di cercare il menu
        StartCoroutine(HandleNewSceneLoad(scene.name));
    }
    
    /// <summary>
    /// NUOVO: Gestisce il caricamento di una nuova scena con timing corretto
    /// </summary>
    private IEnumerator HandleNewSceneLoad(string sceneName)
    {
        // Aspetta che la scena sia completamente caricata
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();
        
        Debug.Log($"[GameManager] Inizializzazione ritardata per nuova scena: {sceneName}");
        
        // Trova il menu di pausa nella nuova scena
        FindPauseMenuInScene();
        
        // Aspetta un frame per permettere l'inizializzazione
        yield return ConfigureCursorForNewScene(sceneName);
    }
    
    private IEnumerator ConfigureCursorForNewScene(string sceneName)
    {
        yield return null; // Aspetta un frame
        
        if (sceneName == mainMenuSceneName)
        {
            SetMenuCursorState();
            Debug.Log("[GameManager] Nuovo caricamento - Cursore configurato per menu");
        }
        else
        {
            SetGameCursorState();
            Debug.Log("[GameManager] Nuovo caricamento - Cursore configurato per gameplay");
        }
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // ========== METODI UTILITY ==========

    /// <summary>
    /// Ottieni il nome della scena corrente
    /// </summary>
    public string GetCurrentSceneName()
    {
        return SceneManager.GetActiveScene().name;
    }

    /// <summary>
    /// Controlla se siamo nel menu principale
    /// </summary>
    public bool IsInMainMenu()
    {
        return GetCurrentSceneName() == mainMenuSceneName;
    }

    /// <summary>
    /// Pausa/Riprendi il gioco - MODIFICATO per gestire meglio il menu di pausa
    /// </summary>
    public void SetGamePaused(bool paused)
    {
        if (IsInMainMenu()) 
        {
            Debug.Log("[GameManager] Tentativo di pausa nel menu principale - ignorato");
            return; // Non permettere pausa nel menu principale
        }
        
        Debug.Log($"[GameManager] Impostando pausa a: {paused}");
        
        isPaused = paused;
        Time.timeScale = paused ? 0f : 1f;
        
        // MODIFICATO: Usa i nuovi metodi unificati
        if (paused)
        {
            ShowPauseMenu();
        }
        else
        {
            HidePauseMenu();
        }
        
        if (paused)
        {
            // Quando in pausa, mostra il cursore
            SetMenuCursorState();
            Debug.Log("[GameManager] Gioco in pausa - Cursore visibile");
        }
        else
        {
            // Quando riprendi, nascondi il cursore
            SetGameCursorState();
            Debug.Log("[GameManager] Gioco ripreso - Cursore nascosto");
        }
        
        // Notifica altri sistemi del cambio di stato
        OnPauseStateChanged?.Invoke(isPaused);
    }

    /// <summary>
    /// Forza lo stato del cursore per il gameplay
    /// </summary>
    public void ForceCursorForGameplay()
    {
        if (!IsInMainMenu() && !isPaused)
        {
            SetGameCursorState();
            Debug.Log("[GameManager] Cursore forzato per gameplay");
        }
    }

    /// <summary>
    /// Imposta la configurazione del cursore (per compatibilità)
    /// </summary>
    public void SetCursorState(bool visible, CursorLockMode lockMode)
    {
        Cursor.visible = visible;
        Cursor.lockState = lockMode;
        Debug.Log($"[GameManager] Cursore impostato manualmente - Visible: {visible}, Lock: {lockMode}");
    }

    // ========== DEBUG ==========

    [ContextMenu("Debug State")]
    public void DebugState()
    {
        Debug.Log($"=== GameManager State ===\n" +
                  $"Current Scene: {GetCurrentSceneName()}\n" +
                  $"Is Main Menu: {IsInMainMenu()}\n" +
                  $"Is Paused: {isPaused}\n" +
                  $"Time Scale: {Time.timeScale}\n" +
                  $"Cursor Visible: {Cursor.visible}\n" +
                  $"Cursor Lock: {Cursor.lockState}\n" +
                  $"SceneController: {(sceneController != null ? "✅" : "❌")}\n" +
                  $"Pause Menu: {(pauseMenu != null ? "✅" : "❌")}\n" +
                  $"Pause Menu Active: {(pauseMenu != null ? pauseMenu.activeSelf : false)}\n" +
                  $"Debug Mode: {debugCursorState}");
    }

    [ContextMenu("Force Find Pause Menu")]
    public void DebugForceFindPauseMenu()
    {
        pauseMenu = null; // Reset
        FindPauseMenuInScene();
    }

    [ContextMenu("Test - Toggle Pause")]
    public void DebugTogglePause()
    {
        TogglePause();
    }

    [ContextMenu("Test - Resume Game")]
    public void DebugResumeGame()
    {
        ResumeGame();
    }

    [ContextMenu("Test - Force Cursor for Gameplay")]
    public void DebugForceCursor()
    {
        ForceCursorForGameplay();
    }

    [ContextMenu("Test - Restart Level")]
    public void DebugRestartLevel()
    {
        RestartLevel();
    }

    [ContextMenu("Test - Go to Main Menu")]
    public void DebugGoToMainMenu()
    {
        GoToMainMenu();
    }

    [ContextMenu("Test - Exit Game")]
    public void DebugExitGame()
    {
        ExitGame();
    }

    // ========== CLEANUP ==========

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}