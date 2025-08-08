using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using System.Text.RegularExpressions;

public class PlayerUI : MonoBehaviour
{
    [Header("Health UI")]
    public Image healthFill;

    [Header("Mana/Power UI")]
    public Image powerFill;

    [Header("Scene Collectibles UI")]
    public TextMeshProUGUI memoryCounterText;
    public TextMeshProUGUI presentCounterText;
    public Image memoryIcon; // opzionale, icona della memoria
    public Image presentIcon; // opzionale, icona del present
    
    [Header("Collectible Panels Animation")]
    public GameObject memoryPanel; // Panel che contiene memoryCounterText e memoryIcon
    public GameObject presentPanel; // Panel che contiene presentCounterText e presentIcon
    
    [Header("Panel Animation Settings")]
    public bool showPanelsOnCollect = true;
    public float panelShowDuration = 3f; // Quanto tempo mostrare il panel
    public float panelAnimationSpeed = 0.5f; // Velocità animazione entrata/uscita
    public AnimationCurve panelEaseInOut = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    
    [Header("Collectibles Animation")]
    public bool animateOnCollect = true;
    public float punchScale = 1.2f;
    public float animationDuration = 0.3f;

    [Header("Player References")]
    public ThirdPersonController playerController;
    public PlayerPowerUp playerPowerUp;

    [Header("Ability Icons")]
    public UIEffectHandler[] abilityIcons;

    private bool useGamepad = false;
    
    // Scene collectibles tracking
    private int currentSceneMemories = 0;
    private int totalSceneMemories = 0;
    private int currentScenePresents = 0;
    private int totalScenePresents = 0;
    
    // Template strings per preservare la formattazione
    private string memoryTextTemplate = "";
    private string presentTextTemplate = "";
    private Color originalMemoryColor;
    private Color originalPresentColor;

    public static PlayerUI Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        // Salva i template di testo e colori originali PRIMA di inizializzare
        SaveOriginalTextFormats();
        
        // Nascondi i pannelli all'inizio
        HidePanelsAtStart();
        
        InitializeCollectibleSystem();
    }
    
    private void HidePanelsAtStart()
    {
        // Nascondi i pannelli dei collectibles all'inizio del gioco
        if (memoryPanel != null)
        {
            memoryPanel.SetActive(false);
        }
        else if (memoryCounterText != null)
        {
            memoryCounterText.gameObject.SetActive(false);
        }
        
        if (presentPanel != null)
        {
            presentPanel.SetActive(false);
        }
        else if (presentCounterText != null)
        {
            presentCounterText.gameObject.SetActive(false);
        }
        
        Debug.Log("[PlayerUI] Pannelli collectibles nascosti all'avvio");
    }
    
    private void SaveOriginalTextFormats()
    {
        // Salva il template per le memories
        if (memoryCounterText != null)
        {
            memoryTextTemplate = memoryCounterText.text;
            originalMemoryColor = memoryCounterText.color;
            
            // Se il testo è vuoto o non contiene numeri, usa un template di default
            if (string.IsNullOrEmpty(memoryTextTemplate) || !memoryTextTemplate.Contains("/"))
            {
                memoryTextTemplate = "Memories: 0/0";
            }
            
            Debug.Log($"[PlayerUI] Memory template salvato: '{memoryTextTemplate}' - Colore: {originalMemoryColor}");
        }
        
        // Salva il template per i presents
        if (presentCounterText != null)
        {
            presentTextTemplate = presentCounterText.text;
            originalPresentColor = presentCounterText.color;
            
            // Se il testo è vuoto o non contiene numeri, usa un template di default
            if (string.IsNullOrEmpty(presentTextTemplate) || !presentTextTemplate.Contains("/"))
            {
                presentTextTemplate = "Presents: 0/0";
            }
            
            Debug.Log($"[PlayerUI] Present template salvato: '{presentTextTemplate}' - Colore: {originalPresentColor}");
        }
    }

    private void InitializeCollectibleSystem()
    {
        // Collegati al SceneManager01 per i dati della scena corrente
        if (SceneManager01.Instance != null)
        {
            // Disconnetti eventuali vecchi eventi
            CleanupSceneManagerEvents();
            
            // Connetti ai nuovi eventi
            SceneManager01.Instance.OnMemoryCountChanged.AddListener(UpdateSceneMemoryCounter);
            SceneManager01.Instance.OnPresentCountChanged.AddListener(UpdateScenePresentCounter);
            SceneManager01.Instance.OnAllMemoriesCollected.AddListener(OnAllSceneMemoriesCompleted);
            SceneManager01.Instance.OnAllPresentsCollected.AddListener(OnAllScenePresentsCompleted);
            SceneManager01.Instance.OnAllCollectiblesCompleted.AddListener(OnAllSceneCollectiblesCompleted);
            
            // Inizializza subito se i dati sono già disponibili
            currentSceneMemories = SceneManager01.Instance.GetCollectedMemories();
            totalSceneMemories = SceneManager01.Instance.GetTotalMemories();
            currentScenePresents = SceneManager01.Instance.GetCollectedPresents();
            totalScenePresents = SceneManager01.Instance.GetTotalPresents();
            
            UpdateAllCounterDisplays();
            
            Debug.Log($"[PlayerUI] Inizializzato da SceneManager - Memories: {currentSceneMemories}/{totalSceneMemories}, Presents: {currentScenePresents}/{totalScenePresents}");
        }
        else
        {
            Debug.LogWarning("[PlayerUI] SceneManager01 non trovato! Provo con PlayerCollectibleTracker come fallback...");
            TryInitializeFromTracker();
        }
    }
    
    private void TryInitializeFromTracker()
    {
        // Fallback con PlayerCollectibleTracker
        PlayerCollectibleTracker tracker = PlayerCollectibleTracker.Instance;
        if (tracker == null)
        {
            tracker = Object.FindFirstObjectByType<PlayerCollectibleTracker>();
        }
        
        if (tracker != null)
        {
            // Disconnetti eventuali vecchi eventi
            CleanupTrackerEvents(tracker);
            
            // Connetti agli eventi del tracker
            tracker.OnMemoryCollected += UpdateSceneMemoryCounter;
            tracker.OnPresentCollected += UpdateScenePresentCounter;
            tracker.OnAllMemoriesCollected += OnAllSceneMemoriesCompleted;
            tracker.OnAllPresentsCollected += OnAllScenePresentsCompleted;
            tracker.OnAllCollectiblesCompleted += OnAllSceneCollectiblesCompleted;
            
            // Inizializza i valori
            currentSceneMemories = tracker.GetCollectedMemories();
            totalSceneMemories = tracker.GetTotalMemories();
            currentScenePresents = tracker.GetCollectedPresents();
            totalScenePresents = tracker.GetTotalPresents();
            
            UpdateAllCounterDisplays();
            
            Debug.Log($"[PlayerUI] Fallback con Tracker - Memories: {currentSceneMemories}/{totalSceneMemories}, Presents: {currentScenePresents}/{totalScenePresents}");
        }
        else
        {
            Debug.LogError("[PlayerUI] Nessun sistema di tracking collectibles trovato!");
        }
    }

    private void OnEnable()
    {
        InputSystem.onActionChange += OnInputActionChange;
        UpdateInputLayout();
    }

    private void OnDisable()
    {
        InputSystem.onActionChange -= OnInputActionChange;
        CleanupEvents();
    }
    
    private void CleanupEvents()
    {
        CleanupSceneManagerEvents();
        
        // Cleanup Tracker events
        PlayerCollectibleTracker tracker = PlayerCollectibleTracker.Instance;
        if (tracker != null)
        {
            CleanupTrackerEvents(tracker);
        }
    }
    
    private void CleanupSceneManagerEvents()
    {
        if (SceneManager01.Instance != null)
        {
            SceneManager01.Instance.OnMemoryCountChanged.RemoveListener(UpdateSceneMemoryCounter);
            SceneManager01.Instance.OnPresentCountChanged.RemoveListener(UpdateScenePresentCounter);
            SceneManager01.Instance.OnAllMemoriesCollected.RemoveListener(OnAllSceneMemoriesCompleted);
            SceneManager01.Instance.OnAllPresentsCollected.RemoveListener(OnAllScenePresentsCompleted);
            SceneManager01.Instance.OnAllCollectiblesCompleted.RemoveListener(OnAllSceneCollectiblesCompleted);
        }
    }
    
    private void CleanupTrackerEvents(PlayerCollectibleTracker tracker)
    {
        if (tracker != null)
        {
            tracker.OnMemoryCollected -= UpdateSceneMemoryCounter;
            tracker.OnPresentCollected -= UpdateScenePresentCounter;
            tracker.OnAllMemoriesCollected -= OnAllSceneMemoriesCompleted;
            tracker.OnAllPresentsCollected -= OnAllScenePresentsCompleted;
            tracker.OnAllCollectiblesCompleted -= OnAllSceneCollectiblesCompleted;
        }
    }

    private void OnInputActionChange(object obj, InputActionChange change)
    {
        if (change == InputActionChange.ActionPerformed)
        {
            UpdateInputLayout();
        }
    }

    private void UpdateInputLayout()
    {
        bool wasGamepad = useGamepad;
        useGamepad = Gamepad.current != null && Gamepad.current.wasUpdatedThisFrame;

        if (wasGamepad != useGamepad && abilityIcons != null)
        {
            for (int i = 0; i < abilityIcons.Length; i++)
            {
                if (abilityIcons[i] != null)
                {
                    abilityIcons[i].UpdateInputText(useGamepad);
                }
            }
        }
    }

    // ========== SCENE COLLECTIBLES COUNTER METHODS ==========
    
    public void UpdateSceneMemoryCounter(int collected, int total)
    {
        currentSceneMemories = collected;
        totalSceneMemories = total;
        UpdateMemoryCounterDisplay();
        
        // Mostra il pannello con animazione quando viene raccolta una memory
        if (showPanelsOnCollect)
        {
            ShowMemoryPanel();
        }
        
        if (animateOnCollect)
        {
            AnimateMemoryCounter();
        }
        
        Debug.Log($"[PlayerUI] Scene memory counter aggiornato: {currentSceneMemories}/{totalSceneMemories}");
    }
    
    public void UpdateScenePresentCounter(int collected, int total)
    {
        currentScenePresents = collected;
        totalScenePresents = total;
        UpdatePresentCounterDisplay();
        
        // Mostra il pannello con animazione quando viene raccolto un present
        if (showPanelsOnCollect)
        {
            ShowPresentPanel();
        }
        
        if (animateOnCollect)
        {
            AnimatePresentCounter();
        }
        
        Debug.Log($"[PlayerUI] Scene present counter aggiornato: {currentScenePresents}/{totalScenePresents}");
    }
    
    private void UpdateMemoryCounterDisplay()
    {
        if (memoryCounterText != null && totalSceneMemories > 0)
        {
            // Usa il template salvato e sostituisci solo i numeri, preservando la formattazione
            string updatedText = UpdateNumbersInTemplate(memoryTextTemplate, currentSceneMemories, totalSceneMemories);
            memoryCounterText.text = updatedText;
            
            // Cambia colore se tutte raccolte, altrimenti usa il colore originale
            if (currentSceneMemories >= totalSceneMemories)
            {
                memoryCounterText.color = Color.green;
            }
            else
            {
                memoryCounterText.color = originalMemoryColor;
            }
            
            Debug.Log($"[PlayerUI] Memory text aggiornato: '{updatedText}'");
        }
    }
    
    private void UpdatePresentCounterDisplay()
    {
        if (presentCounterText != null && totalScenePresents > 0)
        {
            // Usa il template salvato e sostituisci solo i numeri, preservando la formattazione
            string updatedText = UpdateNumbersInTemplate(presentTextTemplate, currentScenePresents, totalScenePresents);
            presentCounterText.text = updatedText;
            
            // Cambia colore se tutti raccolti, altrimenti usa il colore originale
            if (currentScenePresents >= totalScenePresents)
            {
                presentCounterText.color = Color.green;
            }
            else
            {
                presentCounterText.color = originalPresentColor;
            }
            
            Debug.Log($"[PlayerUI] Present text aggiornato: '{updatedText}'");
        }
    }
    
    // Metodo helper per aggiornare solo i numeri nel template, preservando tutta la formattazione TMP
    private string UpdateNumbersInTemplate(string template, int current, int total)
    {
        // Metodo semplice: cerca il pattern X/Y e sostituiscilo
        if (template.Contains("/"))
        {
            // Usa Regex per trovare e sostituire numeri nel formato X/Y
            string result = Regex.Replace(template, @"\d+/\d+", $"{current}/{total}");
            Debug.Log($"[PlayerUI] Template aggiornato: '{template}' -> '{result}'");
            return result;
        }
        
        // Se non trova il pattern, aggiungi alla fine
        string fallbackResult = template.TrimEnd() + $" {current}/{total}";
        Debug.Log($"[PlayerUI] Fallback: '{template}' -> '{fallbackResult}'");
        return fallbackResult;
    }
    
    // ========== PANEL ANIMATION METHODS ==========
    
    private void ShowMemoryPanel()
    {
        GameObject targetPanel = memoryPanel != null ? memoryPanel : memoryCounterText?.gameObject;
        if (targetPanel != null)
        {
            StartCoroutine(ShowPanelWithAnimation(targetPanel));
        }
    }
    
    private void ShowPresentPanel()
    {
        GameObject targetPanel = presentPanel != null ? presentPanel : presentCounterText?.gameObject;
        if (targetPanel != null)
        {
            StartCoroutine(ShowPanelWithAnimation(targetPanel));
        }
    }
    
    private System.Collections.IEnumerator ShowPanelWithAnimation(GameObject panel)
    {
        if (panel == null) yield break;
        
        // Ferma eventuali animazioni precedenti su questo pannello
        StopCoroutine(nameof(HidePanelWithAnimation));
        
        // Attiva il pannello e prepara l'animazione
        panel.SetActive(true);
        Transform panelTransform = panel.transform;
        Vector3 originalScale = panelTransform.localScale;
        CanvasGroup canvasGroup = panel.GetComponent<CanvasGroup>();
        
        // Se non ha un CanvasGroup, aggiungilo per l'animazione di fade
        if (canvasGroup == null)
        {
            canvasGroup = panel.AddComponent<CanvasGroup>();
        }
        
        // Inizia l'animazione di entrata (scale + fade)
        float elapsed = 0f;
        panelTransform.localScale = Vector3.zero;
        canvasGroup.alpha = 0f;
        
        Debug.Log($"[PlayerUI] Showing panel with animation: {panel.name}");
        
        // Animazione di entrata
        while (elapsed < panelAnimationSpeed)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / panelAnimationSpeed;
            float easedProgress = panelEaseInOut.Evaluate(progress);
            
            panelTransform.localScale = Vector3.Lerp(Vector3.zero, originalScale, easedProgress);
            canvasGroup.alpha = Mathf.Lerp(0f, 1f, easedProgress);
            
            yield return null;
        }
        
        // Assicura che sia completamente visibile
        panelTransform.localScale = originalScale;
        canvasGroup.alpha = 1f;
        
        // Aspetta il tempo di visualizzazione
        yield return new WaitForSeconds(panelShowDuration);
        
        // Inizia l'animazione di uscita
        StartCoroutine(HidePanelWithAnimation(panel));
    }
    
    private System.Collections.IEnumerator HidePanelWithAnimation(GameObject panel)
    {
        if (panel == null || !panel.activeInHierarchy) yield break;
        
        Transform panelTransform = panel.transform;
        Vector3 originalScale = panelTransform.localScale;
        CanvasGroup canvasGroup = panel.GetComponent<CanvasGroup>();
        
        if (canvasGroup == null) yield break;
        
        Debug.Log($"[PlayerUI] Hiding panel with animation: {panel.name}");
        
        // Animazione di uscita
        float elapsed = 0f;
        
        while (elapsed < panelAnimationSpeed)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / panelAnimationSpeed;
            float easedProgress = panelEaseInOut.Evaluate(progress);
            
            panelTransform.localScale = Vector3.Lerp(originalScale, Vector3.zero, easedProgress);
            canvasGroup.alpha = Mathf.Lerp(1f, 0f, easedProgress);
            
            yield return null;
        }
        
        // Nascondi completamente il pannello
        panelTransform.localScale = Vector3.zero;
        canvasGroup.alpha = 0f;
        panel.SetActive(false);
        
        // Ripristina la scala originale per la prossima volta
        panelTransform.localScale = originalScale;
        
        Debug.Log($"[PlayerUI] Panel hidden: {panel.name}");
    }
    
    private void UpdateAllCounterDisplays()
    {
        UpdateMemoryCounterDisplay();
        UpdatePresentCounterDisplay();
    }
    
    
    // Metodo per forzare la visualizzazione di un pannello (per debug)
    [ContextMenu("Show Memory Panel")]
    public void DebugShowMemoryPanel()
    {
        ShowMemoryPanel();
    }
    
    [ContextMenu("Show Present Panel")]  
    public void DebugShowPresentPanel()
    {
        ShowPresentPanel();
    }
    
    private void OnAllSceneMemoriesCompleted()
    {
        Debug.Log("[PlayerUI] Tutte le memorie della scena completate!");
        
        if (memoryCounterText != null)
        {
            memoryCounterText.color = new Color(1f, 0.84f, 0f, 1f); // Colore oro
            StartCoroutine(CelebrationEffect(memoryCounterText.transform, memoryIcon?.transform));
        }
    }
    
    private void OnAllScenePresentsCompleted()
    {
        Debug.Log("[PlayerUI] Tutti i presents della scena completati!");
        
        if (presentCounterText != null)
        {
            presentCounterText.color = new Color(1f, 0.84f, 0f, 1f); // Colore oro
            StartCoroutine(CelebrationEffect(presentCounterText.transform, presentIcon?.transform));
        }
    }
    
    private void OnAllSceneCollectiblesCompleted()
    {
        Debug.Log("[PlayerUI] TUTTI i collectibles della scena completati! 🎉");
        
        // Celebrazione completa
        StartCoroutine(FullSceneCelebration());
    }
    
    private void AnimateMemoryCounter()
    {
        if (memoryCounterText != null)
        {
            StartCoroutine(SimpleScaleAnimation(memoryCounterText.transform));
        }
        
        if (memoryIcon != null)
        {
            StartCoroutine(SimpleScaleAnimation(memoryIcon.transform));
        }
    }
    
    private void AnimatePresentCounter()
    {
        if (presentCounterText != null)
        {
            StartCoroutine(SimpleScaleAnimation(presentCounterText.transform));
        }
        
        if (presentIcon != null)
        {
            StartCoroutine(SimpleScaleAnimation(presentIcon.transform));
        }
    }
    
    private System.Collections.IEnumerator SimpleScaleAnimation(Transform target)
    {
        if (target == null) yield break;
        
        Vector3 originalScale = target.localScale;
        float elapsed = 0f;
        float halfDuration = animationDuration * 0.5f;
        
        // Scale up
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / halfDuration;
            target.localScale = Vector3.Lerp(originalScale, originalScale * punchScale, t);
            yield return null;
        }
        
        elapsed = 0f;
        
        // Scale down
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / halfDuration;
            target.localScale = Vector3.Lerp(originalScale * punchScale, originalScale, t);
            yield return null;
        }
        
        target.localScale = originalScale;
    }
    
    private System.Collections.IEnumerator CelebrationEffect(Transform textTransform, Transform iconTransform)
    {
        // Effetto di celebrazione multiplo
        for (int i = 0; i < 3; i++)
        {
            if (textTransform != null)
                StartCoroutine(SimpleScaleAnimation(textTransform));
                
            if (iconTransform != null)
                StartCoroutine(RotationEffect(iconTransform));
                
            yield return new WaitForSeconds(0.2f);
        }
    }
    
    private System.Collections.IEnumerator FullSceneCelebration()
    {
        // Celebrazione completa per tutti i collectibles
        if (memoryCounterText != null && memoryCounterText.gameObject.activeInHierarchy)
        {
            StartCoroutine(CelebrationEffect(memoryCounterText.transform, memoryIcon?.transform));
        }
        
        yield return new WaitForSeconds(0.1f);
        
        if (presentCounterText != null && presentCounterText.gameObject.activeInHierarchy)
        {
            StartCoroutine(CelebrationEffect(presentCounterText.transform, presentIcon?.transform));
        }
    }
    
    private System.Collections.IEnumerator RotationEffect(Transform target)
    {
        if (target == null) yield break;
        
        float elapsed = 0f;
        float duration = 0.5f;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float rotation = Mathf.Lerp(0f, 360f, elapsed / duration);
            target.rotation = Quaternion.Euler(0, 0, rotation);
            yield return null;
        }
        
        target.rotation = Quaternion.identity;
    }
    
    // Metodo per reinizializzare quando cambi scena
    public void OnSceneChanged()
    {
        currentSceneMemories = 0;
        totalSceneMemories = 0;
        currentScenePresents = 0;
        totalScenePresents = 0;
        
        // Risalva i template per la nuova scena
        SaveOriginalTextFormats();
        
        InitializeCollectibleSystem();
        
        Debug.Log("[PlayerUI] Reinizializzato per nuova scena");
    }
    
    // ========== GETTERS PUBBLICI ==========
    
    public int GetCurrentSceneMemories() => currentSceneMemories;
    public int GetTotalSceneMemories() => totalSceneMemories;
    public int GetCurrentScenePresents() => currentScenePresents;
    public int GetTotalScenePresents() => totalScenePresents;
    public int GetCurrentSceneCollectibles() => currentSceneMemories + currentScenePresents;
    public int GetTotalSceneCollectibles() => totalSceneMemories + totalScenePresents;
    
    // ========== EXISTING METHODS ==========

    public void UpdateAbilityIconState(int index, bool canActivate)
    {
        if (abilityIcons != null && index >= 0 && index < abilityIcons.Length && abilityIcons[index] != null)
        {
            abilityIcons[index].SetGrayscale(!canActivate);
            abilityIcons[index].UpdateInputText(useGamepad);
        }
    }

    public void PulseIconAt(int index)
    {
        if (abilityIcons != null && index >= 0 && index < abilityIcons.Length)
        {
            abilityIcons[index]?.PulseIcon();
        }
    }

    public void UpdateHealth(float currentHealth)
    {
        if (healthFill == null || playerController == null)
        {
            Debug.LogWarning("[PlayerUI] healthFill o playerController non assegnato!");
            return;
        }

        float fillAmount = currentHealth / playerController.MaxHealth;
        healthFill.fillAmount = fillAmount;
    }

    public void UpdatePower(float currentPower)
    {
        Debug.Log($"[PlayerUI] UpdatePower chiamato con: {currentPower}");
        
        if (powerFill == null)
        {
            Debug.LogError("[PlayerUI] powerFill non assegnato!");
            return;
        }

        if (playerPowerUp == null)
        {
            Debug.LogError("[PlayerUI] playerPowerUp non assegnato!");
            playerPowerUp = Object.FindFirstObjectByType<PlayerPowerUp>();
            
            if (playerPowerUp == null)
            {
                Debug.LogError("[PlayerUI] PlayerPowerUp non trovato nemmeno in scena!");
                return;
            }
            Debug.Log("[PlayerUI] PlayerPowerUp trovato automaticamente!");
        }

        float maxPower = playerPowerUp.MaxPower;
        Debug.Log($"[PlayerUI] MaxPower: {maxPower}, CurrentPower: {currentPower}");
        
        if (maxPower <= 0f)
        {
            Debug.LogWarning("[PlayerUI] MaxPower è 0 o negativo!");
            powerFill.fillAmount = 0f;
            return;
        }

        float fillAmount = currentPower / maxPower;
        fillAmount = Mathf.Clamp01(fillAmount);

        Debug.Log($"[PlayerUI] Settando fillAmount a: {fillAmount}");
        
        if (!powerFill.gameObject.activeInHierarchy)
        {
            Debug.LogWarning("[PlayerUI] powerFill non è attivo nella gerarchia!");
            powerFill.gameObject.SetActive(true);
        }
        
        powerFill.fillAmount = fillAmount;
        powerFill.SetAllDirty();
        
        Debug.Log($"[PlayerUI] Verificando fillAmount settato: {powerFill.fillAmount}");
    }

    public void SetMaxValues(float maxHealth, float maxPower)
    {
        if (playerController != null)
        {
            playerController.maxHealth = maxHealth;
            UpdateHealth(playerController.currentHealth);
        }
        if (playerPowerUp != null)
        {
            playerPowerUp.maxPower = maxPower;
            UpdatePower(playerPowerUp.currentPower);
        }
    }
    
    private void OnDestroy()
    {
        CleanupEvents();
        
        if (Instance == this)
        {
            Instance = null;
        }
    }
}