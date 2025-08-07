using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

public class PlayerUI : MonoBehaviour
{
    [Header("Health UI")]
    public Image healthFill;

    [Header("Mana/Power UI")]
    public Image powerFill;

    [Header("Memory Counter UI")]
    public TextMeshProUGUI memoryCounterText;
    public Image memoryIcon; // opzionale, icona della memoria
    
    [Header("Memory Animation (opzionale)")]
    public bool animateMemoryOnCollect = true;
    public float memoryPunchScale = 1.2f;
    public float memoryAnimationDuration = 0.3f;

    [Header("Input Layouts")]
    public GameObject UIKeyboard;
    public GameObject UIController;

    [Header("Player References")]
    public ThirdPersonController playerController;
    public PlayerPowerUp playerPowerUp;
    public PlayerCollectibleTracker memoryCollector;

    [Header("Ability Icons")]
    public UIEffectHandler[] keyboardEffectIcons;
    public UIEffectHandler[] controllerEffectIcons;

    private bool useGamepad = false;
    private int currentMemories = 0;
    private int totalMemories = 0;

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
        InitializeMemorySystem();
    }

    private void InitializeMemorySystem()
    {
        // Trova automaticamente il memory collector se non assegnato
        if (memoryCollector == null)
        {
            memoryCollector = PlayerCollectibleTracker.Instance;
            if (memoryCollector == null)
            {
                memoryCollector = Object.FindFirstObjectByType<PlayerCollectibleTracker>();
            }
        }
        
        // Collegati agli eventi del memory collector
        if (memoryCollector != null)
        {
            // Disconnetti eventuali vecchi eventi
            memoryCollector.OnMemoryCollected -= UpdateMemoryCounter;
            memoryCollector.OnMemoriesInitialized -= InitializeMemoryCounter;
            memoryCollector.OnAllMemoriesCollected -= OnAllMemoriesCompleted;
            
            // Connetti i nuovi eventi
            memoryCollector.OnMemoryCollected += UpdateMemoryCounter;
            memoryCollector.OnMemoriesInitialized += InitializeMemoryCounter;
            memoryCollector.OnAllMemoriesCollected += OnAllMemoriesCompleted;
            
            // Inizializza subito se i dati sono già disponibili
            if (memoryCollector.GetTotalMemories() > 0)
            {
                currentMemories = memoryCollector.GetCollectedMemories();
                totalMemories = memoryCollector.GetTotalMemories();
                UpdateMemoryCounterDisplay();
                Debug.Log($"[PlayerUI] Inizializzato da collector esistente: {currentMemories}/{totalMemories}");
            }
        }
        else
        {
            Debug.LogWarning("[PlayerUI] PlayerMemoryCollector non trovato!");
            // Fallback con SceneManager
            TryInitializeFromSceneManager();
        }
    }
    
    private void TryInitializeFromSceneManager()
    {
        if (SceneManager01.Instance != null)
        {
            // Collegati direttamente al SceneManager come fallback
            SceneManager01.Instance.OnMemoryCountChanged.AddListener(UpdateMemoryCounter);
            
            totalMemories = SceneManager01.Instance.GetTotalMemories();
            currentMemories = SceneManager01.Instance.GetCurrentMemories();
            UpdateMemoryCounterDisplay();
            
            Debug.Log($"[PlayerUI] Fallback: Inizializzato da SceneManager: {currentMemories}/{totalMemories}");
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
        // Disconnetti gli eventi per evitare memory leaks
        if (memoryCollector != null)
        {
            memoryCollector.OnMemoryCollected -= UpdateMemoryCounter;
            memoryCollector.OnMemoriesInitialized -= InitializeMemoryCounter;
            memoryCollector.OnAllMemoriesCollected -= OnAllMemoriesCompleted;
        }
        
        // Cleanup SceneManager events
        if (SceneManager01.Instance != null)
        {
            SceneManager01.Instance.OnMemoryCountChanged.RemoveListener(UpdateMemoryCounter);
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
        useGamepad = Gamepad.current != null && Gamepad.current.wasUpdatedThisFrame;

        if (UIKeyboard != null)
            UIKeyboard.SetActive(!useGamepad);

        if (UIController != null)
            UIController.SetActive(useGamepad);
    }

    // === MEMORY COUNTER METHODS ===
    
    public void InitializeMemoryCounter(int total)
    {
        totalMemories = total;
        currentMemories = 0;
        UpdateMemoryCounterDisplay();
        
        Debug.Log($"[PlayerUI] Memory counter inizializzato: 0/{total}");
    }
    
    public void UpdateMemoryCounter(int collected, int total)
    {
        currentMemories = collected;
        totalMemories = total;
        UpdateMemoryCounterDisplay();
        
        if (animateMemoryOnCollect)
        {
            AnimateMemoryCounter();
        }
        
        Debug.Log($"[PlayerUI] Memory counter aggiornato: {currentMemories}/{totalMemories}");
    }
    
    private void UpdateMemoryCounterDisplay()
    {
        if (memoryCounterText != null)
        {
            memoryCounterText.text = $"Memories collected {currentMemories}/{totalMemories}";
            
            // Cambia colore se tutte raccolte
            if (currentMemories >= totalMemories && totalMemories > 0)
            {
                memoryCounterText.color = Color.green;
                
                // Anima icona se presente
                if (memoryIcon != null)
                {
                    AnimateMemoryIcon();
                }
            }
            else
            {
                memoryCounterText.color = Color.white;
            }
        }
    }
    
    private void OnAllMemoriesCompleted()
    {
        Debug.Log("[PlayerUI] Tutte le memorie completate! Attivando celebrazione UI");
        
        // Effetti speciali quando tutte le memorie sono raccolte
        if (memoryCounterText != null)
        {
            memoryCounterText.color = new Color(1f, 0.84f, 0f, 1f); // Colore oro
            StartCoroutine(CelebrationTextEffect());
        }
        
        if (memoryIcon != null)
        {
            StartCoroutine(CelebrationIconEffect());
        }
    }
    
    private void AnimateMemoryCounter()
    {
        if (memoryCounterText != null)
        {
            Transform textTransform = memoryCounterText.transform;
            Vector3 originalScale = textTransform.localScale;
            StartCoroutine(SimpleScaleAnimation(textTransform, originalScale));
        }
    }
    
    private void AnimateMemoryIcon()
    {
        if (memoryIcon != null)
        {
            Transform iconTransform = memoryIcon.transform;
            Vector3 originalScale = iconTransform.localScale;
            StartCoroutine(SimpleScaleAnimation(iconTransform, originalScale));
        }
    }
    
    private System.Collections.IEnumerator SimpleScaleAnimation(Transform target, Vector3 originalScale)
    {
        float elapsed = 0f;
        float halfDuration = memoryAnimationDuration * 0.5f;
        
        // Scale up
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / halfDuration;
            target.localScale = Vector3.Lerp(originalScale, originalScale * memoryPunchScale, t);
            yield return null;
        }
        
        elapsed = 0f;
        
        // Scale down
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / halfDuration;
            target.localScale = Vector3.Lerp(originalScale * memoryPunchScale, originalScale, t);
            yield return null;
        }
        
        target.localScale = originalScale;
    }
    
    private System.Collections.IEnumerator CelebrationTextEffect()
    {
        if (memoryCounterText == null) yield break;
        
        Transform textTransform = memoryCounterText.transform;
        Vector3 originalScale = textTransform.localScale;
        
        // Effetto di celebrazione più lungo
        for (int i = 0; i < 3; i++)
        {
            yield return StartCoroutine(SimpleScaleAnimation(textTransform, originalScale));
            yield return new WaitForSeconds(0.1f);
        }
    }
    
    private System.Collections.IEnumerator CelebrationIconEffect()
    {
        if (memoryIcon == null) yield break;
        
        // Effetto di rotazione per l'icona
        Transform iconTransform = memoryIcon.transform;
        float elapsed = 0f;
        float duration = 1f;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float rotation = Mathf.Lerp(0f, 360f, elapsed / duration);
            iconTransform.rotation = Quaternion.Euler(0, 0, rotation);
            yield return null;
        }
        
        iconTransform.rotation = Quaternion.identity;
    }
    
    // Metodo per reinizializzare quando cambi scena
    public void OnSceneChanged()
    {
        currentMemories = 0;
        totalMemories = 0;
        InitializeMemorySystem();
    }

    // === EXISTING METHODS ===

    public void UpdateAbilityIconState(int index, bool canActivate)
    {
        if (useGamepad)
        {
            if (controllerEffectIcons != null && index >= 0 && index < controllerEffectIcons.Length && controllerEffectIcons[index] != null)
            {
                controllerEffectIcons[index].SetGrayscale(!canActivate);
            }
        }
        else
        {
            if (keyboardEffectIcons != null && index >= 0 && index < keyboardEffectIcons.Length && keyboardEffectIcons[index] != null)
            {
                keyboardEffectIcons[index].SetGrayscale(!canActivate);
            }
        }
    }

    public void PulseIconAt(int index)
    {
        if (useGamepad)
        {
            if (controllerEffectIcons != null && index >= 0 && index < controllerEffectIcons.Length)
            {
                controllerEffectIcons[index]?.PulseIcon();
            }
        }
        else
        {
            if (keyboardEffectIcons != null && index >= 0 && index < keyboardEffectIcons.Length)
            {
                keyboardEffectIcons[index]?.PulseIcon();
            }
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