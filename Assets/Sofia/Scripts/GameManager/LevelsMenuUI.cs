using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Script per gestire la UI del menu dei livelli - VERSIONE SENZA GLITCH
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
    
    [Header("Button References")]
    [SerializeField] private Button level1Button;
    [SerializeField] private Button level2Button;
    [SerializeField] private Button level3Button;
    [SerializeField] private Button backToPauseButton;
    
    [Header("UI References")]
    [SerializeField] private GameObject pauseMenuUI;
    [SerializeField] private SceneController sceneController;
    [SerializeField] private CanvasGroup canvasGroup; // Per fade smooth
    
    // Stati
    private bool isInitialized = false;
    private bool isLoadingScene = false;
    private bool hoverEffectsApplied = false; // Previene applicazione multipla

    private void Awake()
    {
        // Trova il CanvasGroup se non specificato (per fade smooth)
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }
        
        // IMPORTANTE: Inizia invisibile per prevenire flash
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        
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
    
    private void OnEnable()
    {
        // Se è già stato inizializzato, fai fade in direttamente
        if (isInitialized)
        {
            StartCoroutine(FadeIn());
        }
    }

    private GameObject FindPauseMenuUI()
    {
        MainMenu mainMenuComponent = FindFirstObjectByType<MainMenu>();
        if (mainMenuComponent != null)
        {
            if (debugMode) Debug.Log($"[LevelsMenuUI] MainMenu trovato: {mainMenuComponent.gameObject.name}");
            return mainMenuComponent.gameObject;
        }
        
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
                if (debugMode) Debug.Log($"[LevelsMenuUI] Menu trovato: {found.name}");
                return found;
            }
        }
        
        PauseMenuUI pauseComponent = FindFirstObjectByType<PauseMenuUI>();
        if (pauseComponent != null)
        {
            if (debugMode) Debug.Log($"[LevelsMenuUI] PauseMenuUI trovato: {pauseComponent.gameObject.name}");
            return pauseComponent.gameObject;
        }
        
        if (debugMode) Debug.LogWarning("[LevelsMenuUI] Nessun menu precedente trovato.");
        return null;
    }

    private IEnumerator DelayedInitialization()
    {
        // Attendi che tutto sia pronto
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
        
        // IMPORTANTE: Applica hover effects SOLO UNA VOLTA e DOPO che i bottoni sono collegati
        if (!hoverEffectsApplied)
        {
            AddHoverEffectsToButtons();
            hoverEffectsApplied = true;
        }
        
        isInitialized = true;
        
        // Se l'oggetto è attivo, fai il fade in
        if (gameObject.activeSelf)
        {
            StartCoroutine(FadeIn());
        }
        
        if (debugMode)
        {
            Debug.Log($"[LevelsMenuUI] Inizializzazione completata");
        }
    }

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
        }
        
        if (debugMode)
        {
            Debug.Log($"[LevelsMenuUI] Bottoni trovati - L1:{level1Button!=null}, L2:{level2Button!=null}, L3:{level3Button!=null}, Back:{backToPauseButton!=null}");
        }
    }

    private void ConnectAssignedButtons()
    {
        if (level1Button != null) ConnectLevel1Button(level1Button);
        if (level2Button != null) ConnectLevel2Button(level2Button);
        if (level3Button != null) ConnectLevel3Button(level3Button);
        if (backToPauseButton != null) ConnectBackButton(backToPauseButton);
        
        if (debugMode) Debug.Log("[LevelsMenuUI] Bottoni assegnati collegati");
    }

    // ========== IDENTIFICAZIONE BOTTONI ==========

    private bool IsLevel1Button(string name)
    {
        return (name.Contains("level1") || name.Contains("livello1")) || 
               (name.Contains("1") && (name.Contains("level") || name.Contains("livello")));
    }

    private bool IsLevel2Button(string name)
    {
        return (name.Contains("level2") || name.Contains("livello2")) || 
               (name.Contains("2") && (name.Contains("level") || name.Contains("livello")));
    }

    private bool IsLevel3Button(string name)
    {
        return (name.Contains("level3") || name.Contains("livello3")) || 
               (name.Contains("3") && (name.Contains("level") || name.Contains("livello")));
    }

    private bool IsBackButton(string name)
    {
        return name.Contains("back") || name.Contains("indietro") || name.Contains("return") || 
               name.Contains("ritorna") || name.Contains("pause") || name.Contains("pausa");
    }

    // ========== COLLEGAMENTO BOTTONI ==========

    private void ConnectLevel1Button(Button button)
    {
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => LoadLevelDirect(level1SceneName));
        if (debugMode) Debug.Log($"[LevelsMenuUI] Level1 collegato: {button.name}");
    }

    private void ConnectLevel2Button(Button button)
    {
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => LoadLevelDirect(level2SceneName));
        if (debugMode) Debug.Log($"[LevelsMenuUI] Level2 collegato: {button.name}");
    }

    private void ConnectLevel3Button(Button button)
    {
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => LoadLevelDirect(level3SceneName));
        if (debugMode) Debug.Log($"[LevelsMenuUI] Level3 collegato: {button.name}");
    }

    private void ConnectBackButton(Button button)
    {
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(GoBackToPauseMenu);
        if (debugMode) Debug.Log($"[LevelsMenuUI] Back collegato: {button.name}");
    }

    // ========== CARICAMENTO LIVELLI ==========

    public void LoadLevelDirect(string sceneName)
    {
        if (isLoadingScene || string.IsNullOrEmpty(sceneName)) return;

        if (debugMode) Debug.Log($"[LevelsMenuUI] Caricamento livello: {sceneName}");
        
        isLoadingScene = true;
        
        // Ripristina time scale
        if (Mathf.Approximately(Time.timeScale, 0f))
        {
            Time.timeScale = 1f;
        }
        
        SetButtonsInteractable(false);
        
        if (sceneController != null)
        {
            sceneController.LoadScene(sceneName);
        }
        else
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
        }
    }

    public void LoadLevel1() => LoadLevelDirect(level1SceneName);
    public void LoadLevel2() => LoadLevelDirect(level2SceneName);
    public void LoadLevel3() => LoadLevelDirect(level3SceneName);

    public void GoBackToPauseMenu()
    {
        if (debugMode) Debug.Log("[LevelsMenuUI] Ritorno al menu precedente");
        
        // Nascondi con fade
        StartCoroutine(FadeOutAndHide());
    }

    private IEnumerator FadeOutAndHide()
    {
        // Fade out
        float duration = 0.15f;
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / duration);
            yield return null;
        }
        
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        gameObject.SetActive(false);
        
        // Riattiva menu precedente
        if (pauseMenuUI != null)
        {
            pauseMenuUI.SetActive(true);
            
            PauseMenuUI pauseComponent = pauseMenuUI.GetComponent<PauseMenuUI>();
            if (pauseComponent != null)
            {
                pauseComponent.Show();
            }
            else
            {
                MainMenu mainMenuComponent = pauseMenuUI.GetComponent<MainMenu>();
                if (mainMenuComponent != null)
                {
                    mainMenuComponent.Show();
                }
            }
        }
    }

    // ========== FADE IN/OUT SMOOTH ==========

    private IEnumerator FadeIn()
    {
        float duration = 0.2f;
        float elapsed = 0f;
        
        canvasGroup.blocksRaycasts = true;
        canvasGroup.interactable = false;
        
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsed / duration);
            yield return null;
        }
        
        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
    }

    // ========== HOVER EFFECTS - OTTIMIZZATO ==========

    private void AddHoverEffectsToButtons()
    {
        Button[] allButtons = { level1Button, level2Button, level3Button, backToPauseButton };
        
        foreach (Button button in allButtons)
        {
            if (button == null) continue;
            
            // Cerca background
            Transform bgTransform = button.transform.Find("background");
            if (bgTransform == null)
            {
                string[] names = { "Background", "bg", "BG", "image", "Image" };
                foreach (string n in names)
                {
                    bgTransform = button.transform.Find(n);
                    if (bgTransform != null) break;
                }
            }
            
            if (bgTransform == null) continue;
            
            Image bgImage = bgTransform.GetComponent<Image>();
            if (bgImage == null) continue;
            
            // IMPORTANTE: Imposta colore iniziale PRIMA di aggiungere eventi
            Color initialColor = new Color(0.66f, 0.51f, 0.91f, 0f);
            bgImage.color = initialColor;
            
            // Aggiungi eventi hover
            AddHoverEvents(button, bgImage);
        }
    }
    
    private void AddHoverEvents(Button button, Image bgImage)
    {
        // Rimuovi EventTrigger esistente per evitare duplicati
        UnityEngine.EventSystems.EventTrigger oldTrigger = button.GetComponent<UnityEngine.EventSystems.EventTrigger>();
        if (oldTrigger != null)
        {
            Destroy(oldTrigger);
        }
        
        UnityEngine.EventSystems.EventTrigger trigger = button.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
        
        Color normalColor = new Color(0.66f, 0.51f, 0.91f, 0f);
        Color hoverColor = new Color(0.66f, 0.51f, 0.91f, 0.24f);
        Color clickColor = new Color(0.87f, 0.78f, 1f, 0.24f);
        
        // Mouse Enter
        var pointerEnter = new UnityEngine.EventSystems.EventTrigger.Entry();
        pointerEnter.eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter;
        pointerEnter.callback.AddListener((data) => { bgImage.color = hoverColor; });
        trigger.triggers.Add(pointerEnter);
        
        // Mouse Exit
        var pointerExit = new UnityEngine.EventSystems.EventTrigger.Entry();
        pointerExit.eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit;
        pointerExit.callback.AddListener((data) => { bgImage.color = normalColor; });
        trigger.triggers.Add(pointerExit);
        
        // Mouse Down
        var pointerDown = new UnityEngine.EventSystems.EventTrigger.Entry();
        pointerDown.eventID = UnityEngine.EventSystems.EventTriggerType.PointerDown;
        pointerDown.callback.AddListener((data) => { bgImage.color = clickColor; });
        trigger.triggers.Add(pointerDown);
        
        // Mouse Up
        var pointerUp = new UnityEngine.EventSystems.EventTrigger.Entry();
        pointerUp.eventID = UnityEngine.EventSystems.EventTriggerType.PointerUp;
        pointerUp.callback.AddListener((data) => { bgImage.color = hoverColor; });
        trigger.triggers.Add(pointerUp);
    }

    private void SetButtonsInteractable(bool interactable)
    {
        Button[] buttons = { level1Button, level2Button, level3Button, backToPauseButton };
        foreach (Button btn in buttons)
        {
            if (btn != null) btn.interactable = interactable;
        }
    }

    // ========== METODI PUBBLICI ==========

    public void SetPauseMenuUI(GameObject pauseMenu)
    {
        pauseMenuUI = pauseMenu;
    }

    public void SetSceneController(SceneController controller)
    {
        sceneController = controller;
    }

    public void ForceReconnectButtons()
    {
        if (autoFindButtons)
        {
            FindAndConnectButtons();
        }
        else
        {
            ConnectAssignedButtons();
        }
    }

    // Metodi legacy per compatibilità (non più necessari ma mantenuti)
    public void DisableAutoHide() { }
    public void EnableAutoHide() { }
    public void NotifyExternalActivation() { }

    [ContextMenu("Debug Info")]
    public void LogDebugInfo()
    {
        Debug.Log($"=== LevelsMenuUI Debug ===\n" +
                  $"Active: {gameObject.activeSelf}\n" +
                  $"Initialized: {isInitialized}\n" +
                  $"Alpha: {canvasGroup.alpha}\n" +
                  $"Buttons: L1={level1Button!=null}, L2={level2Button!=null}, L3={level3Button!=null}, Back={backToPauseButton!=null}\n" +
                  $"PauseMenuUI: {(pauseMenuUI!=null ? pauseMenuUI.name : "null")}\n" +
                  $"SceneController: {(sceneController!=null ? sceneController.name : "null")}");
    }

    private void OnDestroy()
    {
        isLoadingScene = false;
    }
}