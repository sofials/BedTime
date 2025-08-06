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
    public PlayerMemoryCollector memoryCollector; // Nuovo riferimento

    [Header("Ability Icons")]
    public UIEffectHandler[] keyboardEffectIcons;   // es. 4 icone tastiera
    public UIEffectHandler[] controllerEffectIcons; // es. 4 icone controller

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
    // Trova automaticamente il memory collector se non assegnato
    if (memoryCollector == null)
    {
        memoryCollector = Object.FindFirstObjectByType<PlayerMemoryCollector>();
    }
    
    // Collegati agli eventi del memory collector
    if (memoryCollector != null)
    {
        memoryCollector.OnMemoryCollected += UpdateMemoryCounter;
        memoryCollector.OnMemoriesInitialized += InitializeMemoryCounter;
        
        // AGGIUNTO: Se il collector ha già il totale, inizializza subito
        if (memoryCollector.GetTotalMemories() > 0)
        {
            InitializeMemoryCounter(memoryCollector.GetTotalMemories());
        }
    }
    else
    {
        Debug.LogWarning("[PlayerUI] PlayerMemoryCollector non trovato!");
    }
    
    // AGGIUNTO: Fallback se tutto il resto fallisce
    if (totalMemories == 0)
    {
        StartCoroutine(LateInitializeCounter());
    }
}

// Nuovo metodo di fallback
private System.Collections.IEnumerator LateInitializeCounter()
{
    yield return new WaitForEndOfFrame();
    
    if (totalMemories == 0)
    {
        // Conta direttamente dalla scena come backup
        GameObject[] memories = GameObject.FindGameObjectsWithTag("Memories");
        if (memories.Length > 0)
        {
            InitializeMemoryCounter(memories.Length);
            Debug.Log($"[PlayerUI] Fallback: Inizializzato counter con {memories.Length} memorie");
        }
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
        
        // Disconnetti gli eventi per evitare memory leaks
        if (memoryCollector != null)
        {
            memoryCollector.OnMemoryCollected -= UpdateMemoryCounter;
            memoryCollector.OnMemoriesInitialized -= InitializeMemoryCounter;
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
    
    // MODIFICA QUI: Mostra subito il totale corretto
    if (memoryCounterText != null)
    {
        memoryCounterText.text = $"Memories collected 0/{total}";
        memoryCounterText.color = Color.white;
    }
    
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
        // Usa sempre i valori correnti
        memoryCounterText.text = $"Memories collected {currentMemories}/{totalMemories}";
        
        // Cambia colore se tutte raccolte
        if (currentMemories >= totalMemories && totalMemories > 0)
        {
            memoryCounterText.color = Color.green;
        }
        else
        {
            memoryCounterText.color = Color.white;
        }
    }
}
    
    private void AnimateMemoryCounter()
    {
        if (memoryCounterText != null)
        {
            // Simple scale animation con LeanTween (se disponibile)
            // Altrimenti puoi usare un'animazione semplice
            Transform textTransform = memoryCounterText.transform;
            Vector3 originalScale = textTransform.localScale;
            
            // Se hai LeanTween:
            /*
            LeanTween.cancel(memoryCounterText.gameObject);
            LeanTween.scale(memoryCounterText.gameObject, originalScale * memoryPunchScale, memoryAnimationDuration * 0.5f)
                .setEaseOutBack()
                .setOnComplete(() => {
                    LeanTween.scale(memoryCounterText.gameObject, originalScale, memoryAnimationDuration * 0.5f)
                        .setEaseInBack();
                });
            */
            
            // Versione senza LeanTween (semplice):
            StartCoroutine(SimpleScaleAnimation(textTransform, originalScale));
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
        if (powerFill == null || playerPowerUp == null)
        {
            Debug.LogWarning("[PlayerUI] powerFill o playerPowerUp non assegnato!");
            return;
        }

        float fillAmount = currentPower / playerPowerUp.MaxPower;
        fillAmount = Mathf.Clamp(fillAmount, 0f, 1f);

        if (currentPower > 0f && fillAmount < 0.01f)
        {
            fillAmount = 0.01f;
        }

        powerFill.fillAmount = fillAmount;
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
}