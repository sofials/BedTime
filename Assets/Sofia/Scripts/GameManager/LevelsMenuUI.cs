using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Script per gestire la UI del menu dei livelli con navigazione e caricamento scene
/// Attaccare questo script al GameObject LEVELSUI
/// </summary>
public class LevelsMenuUI : MonoBehaviour
{
    [Header("Scene Names")]
    [SerializeField] private string level1SceneName = "00 - Landing in the Dreamworld";
    [SerializeField] private string level2SceneName = "01 - Party in Lukelandia"; 
    [SerializeField] private string level3SceneName = "Level3";
    
    [Header("Auto-Detection Settings")]
    [SerializeField] private bool debugMode = false;
    [SerializeField] private bool autoFindButtons = true;
    
    [Header("Button References (Opzionale - vengono trovati automaticamente)")]
    [SerializeField] private Button level1Button;
    [SerializeField] private Button level2Button;
    [SerializeField] private Button level3Button;
    [SerializeField] private Button backToPauseButton;
    
    [Header("UI References")]
    [SerializeField] private GameObject pauseMenuUI; // Riferimento al PauseMenuUI
    [SerializeField] private SceneController sceneController; // Riferimento al SceneController
    
    // Stati
    private bool isInitialized = false;
    private bool isLoadingScene = false;

    private void Awake()
    {
        // Trova automaticamente i riferimenti se non specificati
        if (pauseMenuUI == null)
        {
            pauseMenuUI = FindPauseMenuUI();
        }
        
        if (sceneController == null)
        {
            sceneController = FindFirstObjectByType<SceneController>();
        }
    }

    private void Start()
    {
        StartCoroutine(DelayedInitialization());
    }

    /// <summary>
    /// Trova automaticamente l'oggetto PauseMenuUI nella scena
    /// </summary>
    private GameObject FindPauseMenuUI()
    {
        // Prima cerca un MainMenu nella scena (per Title Screen)
        MainMenu mainMenuComponent = FindFirstObjectByType<MainMenu>();
        if (mainMenuComponent != null)
        {
            if (debugMode) Debug.Log($"[LevelsMenuUI] MainMenu trovato per Title Screen: {mainMenuComponent.gameObject.name}");
            return mainMenuComponent.gameObject;
        }
        
        // Poi cerca oggetti con possibili nomi
        string[] possibleNames = { 
            "PauseMenuUI", "Pause Menu", "PauseMenu", "Pause", 
            "MenuPausa", "Menu Pausa", "PAUSEMENU",
            "MainMenu", "Main Menu", "TitleMenu", "Title Menu"
        };
        
        foreach (string name in possibleNames)
        {
            GameObject found = GameObject.Find(name);
            if (found != null)
            {
                if (debugMode) Debug.Log($"[LevelsMenuUI] Menu trovato per nome: {found.name}");
                return found;
            }
        }
        
        // Cerca tramite componente PauseMenuUI
        PauseMenuUI pauseComponent = FindFirstObjectByType<PauseMenuUI>();
        if (pauseComponent != null)
        {
            if (debugMode) Debug.Log($"[LevelsMenuUI] PauseMenuUI trovato tramite componente: {pauseComponent.gameObject.name}");
            return pauseComponent.gameObject;
        }
        
        if (debugMode) Debug.LogWarning("[LevelsMenuUI] Nessun menu precedente trovato automaticamente.");
        return null;
    }

    /// <summary>
    /// Inizializzazione ritardata
    /// </summary>
    private IEnumerator DelayedInitialization()
    {
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();
        
        if (autoFindButtons)
        {
            FindAndConnectButtons();
        }
        else
        {
            ConnectAssignedButtons();
        }
        
        // Aggiungi effetti hover a tutti i bottoni
        AddHoverEffectsToButtons();
        
        // Assicurati che questo menu sia nascosto all'inizio
        gameObject.SetActive(false);
        
        isInitialized = true;
        
        if (debugMode)
        {
            Debug.Log($"[LevelsMenuUI] Inizializzazione completata per: {gameObject.name}");
            LogDebugInfo();
        }
    }

    /// <summary>
    /// Trova automaticamente i bottoni nel menu dei livelli
    /// </summary>
    private void FindAndConnectButtons()
    {
        if (debugMode) Debug.Log("[LevelsMenuUI] Ricerca automatica bottoni...");
        
        Button[] allButtons = GetComponentsInChildren<Button>(true);
        
        foreach (Button button in allButtons)
        {
            string buttonName = button.name.ToLower();
            
            if (IsLevel1Button(buttonName))
            {
                if (level1Button == null) level1Button = button;
                ConnectLevel1Button(button);
            }
            else if (IsLevel2Button(buttonName))
            {
                if (level2Button == null) level2Button = button;
                ConnectLevel2Button(button);
            }
            else if (IsLevel3Button(buttonName))
            {
                if (level3Button == null) level3Button = button;
                ConnectLevel3Button(button);
            }
            else if (IsBackButton(buttonName))
            {
                if (backToPauseButton == null) backToPauseButton = button;
                ConnectBackButton(button);
            }
            else if (debugMode)
            {
                Debug.Log($"[LevelsMenuUI] Bottone non riconosciuto: {button.name}");
            }
        }
        
        if (debugMode)
        {
            Debug.Log($"[LevelsMenuUI] Bottoni trovati - Level1: {level1Button != null}, Level2: {level2Button != null}, Level3: {level3Button != null}, Back: {backToPauseButton != null}");
        }
    }

    /// <summary>
    /// Collega i bottoni già assegnati manualmente
    /// </summary>
    private void ConnectAssignedButtons()
    {
        if (level1Button != null) ConnectLevel1Button(level1Button);
        if (level2Button != null) ConnectLevel2Button(level2Button);
        if (level3Button != null) ConnectLevel3Button(level3Button);
        if (backToPauseButton != null) ConnectBackButton(backToPauseButton);
        
        if (debugMode) Debug.Log("[LevelsMenuUI] Bottoni assegnati manualmente collegati");
    }

    // ========== METODI DI IDENTIFICAZIONE BOTTONI ==========

    private bool IsLevel1Button(string name)
    {
        return name.Contains("level1") || name.Contains("livello1") || name.Contains("1") && 
               (name.Contains("level") || name.Contains("livello"));
    }

    private bool IsLevel2Button(string name)
    {
        return name.Contains("level2") || name.Contains("livello2") || name.Contains("2") && 
               (name.Contains("level") || name.Contains("livello"));
    }

    private bool IsLevel3Button(string name)
    {
        return name.Contains("level3") || name.Contains("livello3") || name.Contains("3") && 
               (name.Contains("level") || name.Contains("livello"));
    }

    private bool IsBackButton(string name)
    {
        return name.Contains("back") || name.Contains("indietro") || name.Contains("return") || 
               name.Contains("ritorna") || name.Contains("pause") || name.Contains("pausa");
    }

    // ========== METODI DI COLLEGAMENTO BOTTONI ==========

    private void ConnectLevel1Button(Button button)
    {
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => LoadLevelDirect(level1SceneName));
        if (debugMode) Debug.Log($"[LevelsMenuUI] Bottone Level1 collegato (direct): {button.name}");
    }

    private void ConnectLevel2Button(Button button)
    {
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => LoadLevelDirect(level2SceneName));
        if (debugMode) Debug.Log($"[LevelsMenuUI] Bottone Level2 collegato (direct): {button.name}");
    }

    private void ConnectLevel3Button(Button button)
    {
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => LoadLevelDirect(level3SceneName));
        if (debugMode) Debug.Log($"[LevelsMenuUI] Bottone Level3 collegato (direct): {button.name}");
    }

    private void ConnectBackButton(Button button)
    {
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(GoBackToPauseMenu);
        if (debugMode) Debug.Log($"[LevelsMenuUI] Bottone Back collegato: {button.name}");
    }

    // ========== GESTORI EVENTI BOTTONI ==========

    /// <summary>
    /// Metodo principale per caricare un livello
    /// </summary>
    public void LoadLevel(string sceneName)
    {
        if (isLoadingScene)
        {
            if (debugMode) Debug.Log("[LevelsMenuUI] Caricamento già in corso, operazione ignorata");
            return;
        }

        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("[LevelsMenuUI] Nome della scena vuoto!");
            return;
        }

        if (debugMode) Debug.Log($"[LevelsMenuUI] Caricamento livello: {sceneName}");
        
        StartCoroutine(LoadLevelCoroutine(sceneName));
    }

    /// <summary>
    /// Coroutine per il caricamento del livello con fade
    /// </summary>
    private IEnumerator LoadLevelCoroutine(string sceneName)
    {
        isLoadingScene = true;
        
        // IMPORTANTE: Ripristina il time scale prima di caricare se siamo in pausa
        bool wasGamePaused = Mathf.Approximately(Time.timeScale, 0f);
        if (wasGamePaused)
        {
            Time.timeScale = 1f;
            if (debugMode) Debug.Log("[LevelsMenuUI] Time scale ripristinato per il caricamento della scena");
        }
        
        // Disabilita tutti i bottoni durante il caricamento
        SetButtonsInteractable(false);
        
        if (sceneController != null)
        {
            // Usa il SceneController esistente per il fade
            sceneController.LoadScene(sceneName);
        }
        else
        {
            // Fallback: carica direttamente senza fade
            Debug.LogWarning("[LevelsMenuUI] SceneController non trovato, caricamento senza fade");
            yield return UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(sceneName);
        }
        
        // Nota: isLoadingScene verrà resettato quando la scena si ricarica
    }

    /// <summary>
    /// Metodi pubblici per caricare livelli specifici (da usare negli eventi dei bottoni)
    /// </summary>
    public void LoadLevel1()
    {
        LoadLevel(level1SceneName);
    }

    public void LoadLevel2()
    {
        LoadLevel(level2SceneName);
    }

    public void LoadLevel3()
    {
        LoadLevel(level3SceneName);
    }
    
    /// <summary>
    /// Metodi alternativi che caricano direttamente senza coroutine (per situazioni di emergenza)
    /// </summary>
    public void LoadLevel1Direct()
    {
        LoadLevelDirect(level1SceneName);
    }

    public void LoadLevel2Direct()
    {
        LoadLevelDirect(level2SceneName);
    }

    public void LoadLevel3Direct()
    {
        LoadLevelDirect(level3SceneName);
    }
    
    /// <summary>
    /// Caricamento diretto della scena senza coroutine (più affidabile durante la pausa)
    /// </summary>
    public void LoadLevelDirect(string sceneName)
    {
        Debug.Log($"[LevelsMenuUI] LoadLevelDirect chiamato con sceneName: '{sceneName}'");
        
        if (isLoadingScene)
        {
            Debug.LogWarning("[LevelsMenuUI] Caricamento già in corso, operazione ignorata");
            return;
        }

        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("[LevelsMenuUI] Nome della scena vuoto!");
            return;
        }

        Debug.Log($"[LevelsMenuUI] Inizio caricamento DIRETTO livello: {sceneName}");
        
        isLoadingScene = true;
        
        // IMPORTANTE: Ripristina il time scale prima di caricare se siamo in pausa
        bool wasGamePaused = Mathf.Approximately(Time.timeScale, 0f);
        if (wasGamePaused)
        {
            Time.timeScale = 1f;
            Debug.Log("[LevelsMenuUI] Time scale ripristinato da 0 a 1");
        }
        else
        {
            Debug.Log($"[LevelsMenuUI] Time scale attuale: {Time.timeScale}");
        }
        
        // Disabilita tutti i bottoni durante il caricamento
        SetButtonsInteractable(false);
        Debug.Log("[LevelsMenuUI] Bottoni disabilitati per il caricamento");
        
        if (sceneController != null)
        {
            Debug.Log($"[LevelsMenuUI] Usando SceneController: {sceneController.name}");
            sceneController.LoadScene(sceneName);
        }
        else
        {
            Debug.LogWarning("[LevelsMenuUI] SceneController non trovato, caricamento diretto con SceneManager");
            
            // Verifica se la scena esiste nel Build Settings
            int sceneIndex = UnityEngine.SceneManagement.SceneUtility.GetBuildIndexByScenePath(sceneName);
            if (sceneIndex == -1)
            {
                Debug.LogError($"[LevelsMenuUI] Scena '{sceneName}' non trovata nel Build Settings!");
                isLoadingScene = false;
                SetButtonsInteractable(true);
                return;
            }
            
            Debug.Log($"[LevelsMenuUI] Scena trovata nel Build Settings (indice: {sceneIndex}), caricamento in corso...");
            UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
        }
    }

    /// <summary>
    /// Torna al menu precedente (PauseMenuUI o MainMenu)
    /// </summary>
    public void GoBackToPauseMenu()
    {
        Debug.Log("[LevelsMenuUI] GoBackToPauseMenu chiamato");
        
        // Nasconde questo menu
        gameObject.SetActive(false);
        Debug.Log("[LevelsMenuUI] LevelsUI nascosto");
        
        // Riattiva il menu precedente
        if (pauseMenuUI != null)
        {
            Debug.Log($"[LevelsMenuUI] Tentativo di riattivare menu precedente: {pauseMenuUI.name}");
            pauseMenuUI.SetActive(true);
            
            // Controlla se ha un componente PauseMenuUI
            PauseMenuUI pauseComponent = pauseMenuUI.GetComponent<PauseMenuUI>();
            if (pauseComponent != null)
            {
                Debug.Log("[LevelsMenuUI] Trovato componente PauseMenuUI, chiamata Show()");
                pauseComponent.Show();
            }
            else
            {
                // Controlla se ha un componente MainMenu
                MainMenu mainMenuComponent = pauseMenuUI.GetComponent<MainMenu>();
                if (mainMenuComponent != null)
                {
                    Debug.Log("[LevelsMenuUI] Trovato componente MainMenu, chiamata Show()");
                    mainMenuComponent.Show();
                }
                else
                {
                    Debug.Log("[LevelsMenuUI] Nessun componente specifico trovato, menu generico riattivato");
                }
            }
            Debug.Log($"[LevelsMenuUI] Menu precedente riattivato: {pauseMenuUI.name}");
        }
        else
        {
            Debug.LogError("[LevelsMenuUI] Riferimento al menu precedente è NULL!");
        }
    }

    // ========== UTILITY METHODS ==========

    /// <summary>
    /// Aggiunge automaticamente gli effetti hover a tutti i bottoni
    /// </summary>
    private void AddHoverEffectsToButtons()
    {
        Button[] allButtons = { level1Button, level2Button, level3Button, backToPauseButton };
        
        foreach (Button button in allButtons)
        {
            if (button != null)
            {
                // Trova l'oggetto background
                Transform backgroundTransform = button.transform.Find("background");
                if (backgroundTransform == null)
                {
                    // Cerca con nomi alternativi
                    string[] possibleNames = { "Background", "bg", "BG", "image", "Image" };
                    foreach (string name in possibleNames)
                    {
                        backgroundTransform = button.transform.Find(name);
                        if (backgroundTransform != null) break;
                    }
                }
                
                if (backgroundTransform != null)
                {
                    Image backgroundImage = backgroundTransform.GetComponent<Image>();
                    if (backgroundImage != null)
                    {
                        // Imposta alpha iniziale a 0
                        Color initialColor = new Color(0.66f, 0.51f, 0.91f, 0f); // A981E9 con alpha 0
                        backgroundImage.color = initialColor;
                        
                        // Aggiungi i trigger per hover e click usando Unity Events
                        AddHoverEvents(button, backgroundImage);
                        
                        if (debugMode) Debug.Log($"[LevelsMenuUI] Effetto hover configurato per: {button.name}");
                    }
                }
                else if (debugMode)
                {
                    Debug.LogWarning($"[LevelsMenuUI] Oggetto background non trovato per: {button.name}");
                }
            }
        }
        
        if (debugMode) Debug.Log("[LevelsMenuUI] Effetti hover applicati a tutti i bottoni");
    }
    
    /// <summary>
    /// Aggiunge gli eventi hover a un bottone specifico
    /// </summary>
    private void AddHoverEvents(Button button, Image backgroundImage)
    {
        // Usa EventTrigger per gestire hover e click
        UnityEngine.EventSystems.EventTrigger trigger = button.GetComponent<UnityEngine.EventSystems.EventTrigger>();
        if (trigger == null)
        {
            trigger = button.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
        }
        
        // Colori
        Color normalColor = new Color(0.66f, 0.51f, 0.91f, 0f);        // A981E9 con alpha 0
        Color hoverColor = new Color(0.66f, 0.51f, 0.91f, 0.2392157f); // A981E9 con alpha hover
        Color clickColor = new Color(0.87f, 0.78f, 1f, 0.2392157f);    // DDC7FF con alpha hover
        
        // Mouse Enter
        UnityEngine.EventSystems.EventTrigger.Entry pointerEnter = new UnityEngine.EventSystems.EventTrigger.Entry();
        pointerEnter.eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter;
        pointerEnter.callback.AddListener((data) => { backgroundImage.color = hoverColor; });
        trigger.triggers.Add(pointerEnter);
        
        // Mouse Exit  
        UnityEngine.EventSystems.EventTrigger.Entry pointerExit = new UnityEngine.EventSystems.EventTrigger.Entry();
        pointerExit.eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit;
        pointerExit.callback.AddListener((data) => { backgroundImage.color = normalColor; });
        trigger.triggers.Add(pointerExit);
        
        // Mouse Down
        UnityEngine.EventSystems.EventTrigger.Entry pointerDown = new UnityEngine.EventSystems.EventTrigger.Entry();
        pointerDown.eventID = UnityEngine.EventSystems.EventTriggerType.PointerDown;
        pointerDown.callback.AddListener((data) => { backgroundImage.color = clickColor; });
        trigger.triggers.Add(pointerDown);
        
        // Mouse Up
        UnityEngine.EventSystems.EventTrigger.Entry pointerUp = new UnityEngine.EventSystems.EventTrigger.Entry();
        pointerUp.eventID = UnityEngine.EventSystems.EventTriggerType.PointerUp;
        pointerUp.callback.AddListener((data) => { 
            // Torna a hover se il mouse è ancora sopra, altrimenti normale
            backgroundImage.color = hoverColor; // Per semplicità, sempre hover dopo click
        });
        trigger.triggers.Add(pointerUp);
    }

    /// <summary>
    /// Abilita/disabilita l'interazione con tutti i bottoni
    /// </summary>
    private void SetButtonsInteractable(bool interactable)
    {
        Button[] buttons = { level1Button, level2Button, level3Button, backToPauseButton };
        
        foreach (Button button in buttons)
        {
            if (button != null)
            {
                button.interactable = interactable;
            }
        }
        
        if (debugMode) Debug.Log($"[LevelsMenuUI] Bottoni impostati come interagibili: {interactable}");
    }

    /// <summary>
    /// Imposta i nomi delle scene per i livelli
    /// </summary>
    public void SetSceneNames(string level1, string level2, string level3)
    {
        level1SceneName = level1;
        level2SceneName = level2;
        level3SceneName = level3;
        
        if (debugMode) 
        {
            Debug.Log($"[LevelsMenuUI] Scene names aggiornati: L1={level1}, L2={level2}, L3={level3}");
        }
    }

    /// <summary>
    /// Imposta il riferimento al PauseMenuUI
    /// </summary>
    public void SetPauseMenuUI(GameObject pauseMenu)
    {
        pauseMenuUI = pauseMenu;
        if (debugMode) Debug.Log($"[LevelsMenuUI] PauseMenuUI impostato: {pauseMenu.name}");
    }

    /// <summary>
    /// Imposta il riferimento al SceneController
    /// </summary>
    public void SetSceneController(SceneController controller)
    {
        sceneController = controller;
        if (debugMode) Debug.Log($"[LevelsMenuUI] SceneController impostato: {controller.name}");
    }

    // ========== DEBUG E CONTEXT MENU ==========

    [ContextMenu("Test Load Level 1")]
    public void TestLoadLevel1()
    {
        LoadLevel1();
    }

    [ContextMenu("Test Load Level 2")] 
    public void TestLoadLevel2()
    {
        LoadLevel2();
    }

    [ContextMenu("Test Load Level 3")]
    public void TestLoadLevel3()
    {
        LoadLevel3();
    }

    [ContextMenu("Test Back to Pause")]
    public void TestBackToPause()
    {
        GoBackToPauseMenu();
    }

    [ContextMenu("Force Find References")]
    public void ForceFindReferences()
    {
        pauseMenuUI = FindPauseMenuUI();
        sceneController = FindFirstObjectByType<SceneController>();
        Debug.Log($"[LevelsMenuUI] Riferimenti aggiornati - PauseMenu: {pauseMenuUI != null}, SceneController: {sceneController != null}");
    }

    [ContextMenu("Add Hover Effects")]
    public void ForceAddHoverEffects()
    {
        AddHoverEffectsToButtons();
        Debug.Log("[LevelsMenuUI] Effetti hover applicati forzatamente");
    }

    [ContextMenu("Debug Info")]
    public void LogDebugInfo()
    {
        Debug.Log($"=== LevelsMenuUI Debug Info ===\n" +
                  $"GameObject: {gameObject.name}\n" +
                  $"GameObject Active: {gameObject.activeSelf}\n" +
                  $"Initialized: {isInitialized}\n" +
                  $"Loading Scene: {isLoadingScene}\n" +
                  $"Auto Find Buttons: {autoFindButtons}\n" +
                  $"Time.timeScale: {Time.timeScale}\n" +
                  $"Scene Names: L1={level1SceneName}, L2={level2SceneName}, L3={level3SceneName}\n" +
                  $"Level1 Button: {(level1Button != null ? level1Button.name + " (Active: " + level1Button.gameObject.activeSelf + ", Interactable: " + level1Button.interactable + ")" : "❌")}\n" +
                  $"Level2 Button: {(level2Button != null ? level2Button.name + " (Active: " + level2Button.gameObject.activeSelf + ", Interactable: " + level2Button.interactable + ")" : "❌")}\n" +
                  $"Level3 Button: {(level3Button != null ? level3Button.name + " (Active: " + level3Button.gameObject.activeSelf + ", Interactable: " + level3Button.interactable + ")" : "❌")}\n" +
                  $"Back Button: {(backToPauseButton != null ? backToPauseButton.name + " (Active: " + backToPauseButton.gameObject.activeSelf + ", Interactable: " + backToPauseButton.interactable + ")" : "❌")}\n" +
                  $"Pause Menu UI: {(pauseMenuUI != null ? pauseMenuUI.name + " (Active: " + pauseMenuUI.activeSelf + ")" : "❌")}\n" +
                  $"Scene Controller: {(sceneController != null ? sceneController.name : "❌")}");
    }

    // ========== CLEANUP ==========

    private void OnDestroy()
    {
        // Reset dello stato se necessario
        isLoadingScene = false;
    }
}