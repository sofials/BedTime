using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class PlayerUI : MonoBehaviour
{
    [Header("Health UI")]
    public Image healthFill;

    [Header("Mana/Power UI")]
    public Image powerFill;

    [Header("Player References")]
    public ThirdPersonController playerController;
    public PlayerPowerUp playerPowerUp;

    [Header("Ability Icons")]
    public UIEffectHandler[] abilityIcons;

    [Header("Optional Collectibles UI")]
    public PlayerCollectiblesUI collectiblesUI; // Riferimento opzionale al sistema collectibles

    private bool useGamepad = false;

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
        // Auto-trova PlayerCollectiblesUI se non assegnato
        if (collectiblesUI == null)
        {
            collectiblesUI = PlayerCollectiblesUI.Instance;
            if (collectiblesUI == null)
            {
                collectiblesUI = Object.FindFirstObjectByType<PlayerCollectiblesUI>();
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

    // ========== ABILITY ICONS METHODS ==========

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

    // ========== HEALTH UI METHODS ==========

  public void UpdateHealth(float currentHealth)
{
    if (healthFill == null) return;
    
    if (playerController == null)
    {
        playerController = FindFirstObjectByType<ThirdPersonController>();
        if (playerController == null) return;
    }

    float maxHealth = playerController.MaxHealth;
    if (maxHealth <= 0f) return;

    float fillAmount = Mathf.Clamp01(currentHealth / maxHealth);
    healthFill.fillAmount = fillAmount;
    healthFill.SetAllDirty();
}


    // ========== POWER UI METHODS ==========

    public void UpdatePower(float currentPower)
{
    if (powerFill == null) return;

    if (playerPowerUp == null)
    {
        playerPowerUp = Object.FindFirstObjectByType<PlayerPowerUp>();
        if (playerPowerUp == null) return;
    }

    float maxPower = playerPowerUp.MaxPower;
    if (maxPower <= 0f)
    {
        powerFill.fillAmount = 0f;
        return;
    }

    float fillAmount = Mathf.Clamp01(currentPower / maxPower);
    powerFill.fillAmount = fillAmount;
    powerFill.SetAllDirty();
}
  
    // 🆕 NUOVO: Metodo sicuro per aggiornare maxHealth se necessario
   public void SafeSetMaxHealth(float newMaxHealth)
{
    if (playerController != null)
    {
        playerController.MaxHealth = newMaxHealth;
        UpdateHealth(playerController.currentHealth);
    }
}
    
public void SetMaxValues(float maxHealth, float maxPower)
{
    // ⚠️ Non modifica maxHealth per sicurezza
    if (playerController != null)
        UpdateHealth(playerController.currentHealth);
        
    if (playerPowerUp != null)
    {
        playerPowerUp.maxPower = maxPower;
        UpdatePower(playerPowerUp.currentPower);
    }
}

    // ========== COLLECTIBLES UI BRIDGE METHODS ==========
    // Questi metodi forniscono un ponte per accedere ai dati dei collectibles se necessario

    /// <summary>
    /// Ottieni il numero di memories raccolte nella scena corrente
    /// </summary>
    public int GetCurrentSceneMemories()
    {
        return collectiblesUI != null ? collectiblesUI.GetCurrentSceneMemories() : 0;
    }

    /// <summary>
    /// Ottieni il numero totale di memories nella scena corrente
    /// </summary>
    public int GetTotalSceneMemories()
    {
        return collectiblesUI != null ? collectiblesUI.GetTotalSceneMemories() : 0;
    }

    /// <summary>
    /// Ottieni il numero di presents raccolti nella scena corrente
    /// </summary>
    public int GetCurrentScenePresents()
    {
        return collectiblesUI != null ? collectiblesUI.GetCurrentScenePresents() : 0;
    }

    /// <summary>
    /// Ottieni il numero totale di presents nella scena corrente
    /// </summary>
    public int GetTotalScenePresents()
    {
        return collectiblesUI != null ? collectiblesUI.GetTotalScenePresents() : 0;
    }

    /// <summary>
    /// Ottieni il numero totale di collectibles raccolti nella scena corrente
    /// </summary>
    public int GetCurrentSceneCollectibles()
    {
        return collectiblesUI != null ? collectiblesUI.GetCurrentSceneCollectibles() : 0;
    }

    /// <summary>
    /// Ottieni il numero totale di collectibles nella scena corrente
    /// </summary>
    public int GetTotalSceneCollectibles()
    {
        return collectiblesUI != null ? collectiblesUI.GetTotalSceneCollectibles() : 0;
    }

    /// <summary>
    /// Verifica se il sistema collectibles è connesso a un tracker
    /// </summary>
    public bool IsConnectedToCollectiblesSystem()
    {
        return collectiblesUI != null ? collectiblesUI.IsConnectedToTrackingSystem() : false;
    }

    /// <summary>
    /// Ottieni informazioni sul sistema collectibles connesso
    /// </summary>
    public string GetCollectiblesSystemInfo()
    {
        return collectiblesUI != null ? collectiblesUI.GetConnectedSystemInfo() : "PlayerCollectiblesUI non trovato";
    }

    /// <summary>
    /// Forza la riconnessione del sistema collectibles (utile per debug)
    /// </summary>
    public void ForceReconnectCollectiblesSystem()
    {
        if (collectiblesUI != null)
        {
            collectiblesUI.DebugForceReconnect();
        }
        else
        {
            Debug.LogWarning("[PlayerUI] PlayerCollectiblesUI non trovato - impossibile forzare riconnessione");
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}