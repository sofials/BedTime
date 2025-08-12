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
        if (healthFill == null || playerController == null)
        {
            Debug.LogWarning("[PlayerUI] healthFill o playerController non assegnato!");
            return;
        }

        float fillAmount = currentHealth / playerController.MaxHealth;
        healthFill.fillAmount = fillAmount;
    }

    // ========== POWER UI METHODS ==========

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

    // ========== UTILITY METHODS ==========

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

    // ========== DEBUG METHODS ==========

    [ContextMenu("🌟 Debug - Show PlayerUI Info")]
    public void DebugShowPlayerUIInfo()
    {
        Debug.Log($"=== PlayerUI Debug Info ===\n" +
                  $"Health Fill: {(healthFill != null ? "✅" : "❌")}\n" +
                  $"Power Fill: {(powerFill != null ? "✅" : "❌")}\n" +
                  $"Player Controller: {(playerController != null ? "✅" : "❌")}\n" +
                  $"Player PowerUp: {(playerPowerUp != null ? "✅" : "❌")}\n" +
                  $"Ability Icons: {(abilityIcons != null ? abilityIcons.Length.ToString() : "❌")}\n" +
                  $"Collectibles UI: {(collectiblesUI != null ? "✅ Connected" : "❌ Not Found")}\n" +
                  $"Use Gamepad: {useGamepad}");
    }

    [ContextMenu("🔄 Debug - Auto-Find Components")]
    public void DebugAutoFindComponents()
    {
        Debug.Log("[PlayerUI] Ricerca automatica componenti...");

        // Auto-trova PlayerController se non assegnato
        if (playerController == null)
        {
            playerController = Object.FindFirstObjectByType<ThirdPersonController>();
            Debug.Log($"[PlayerUI] PlayerController: {(playerController != null ? "✅ Trovato" : "❌ Non trovato")}");
        }

        // Auto-trova PlayerPowerUp se non assegnato
        if (playerPowerUp == null)
        {
            playerPowerUp = Object.FindFirstObjectByType<PlayerPowerUp>();
            Debug.Log($"[PlayerUI] PlayerPowerUp: {(playerPowerUp != null ? "✅ Trovato" : "❌ Non trovato")}");
        }

        // Auto-trova PlayerCollectiblesUI se non assegnato
        if (collectiblesUI == null)
        {
            collectiblesUI = PlayerCollectiblesUI.Instance;
            if (collectiblesUI == null)
            {
                collectiblesUI = Object.FindFirstObjectByType<PlayerCollectiblesUI>();
            }
            Debug.Log($"[PlayerUI] PlayerCollectiblesUI: {(collectiblesUI != null ? "✅ Trovato" : "❌ Non trovato")}");
        }

        Debug.Log("[PlayerUI] Ricerca automatica completata!");
        DebugShowPlayerUIInfo();
    }

    [ContextMenu("🔥 Debug - Test Health Update")]
    public void DebugTestHealthUpdate()
    {
        if (playerController != null)
        {
            float testHealth = playerController.MaxHealth * 0.5f; // 50% della salute massima
            UpdateHealth(testHealth);
            Debug.Log($"[PlayerUI] Test aggiornamento salute: {testHealth}/{playerController.MaxHealth}");
        }
        else
        {
            Debug.LogWarning("[PlayerUI] PlayerController non assegnato - impossibile testare salute");
        }
    }

    [ContextMenu("🔥 Debug - Test Power Update")]
    public void DebugTestPowerUpdate()
    {
        if (playerPowerUp != null)
        {
            float testPower = playerPowerUp.MaxPower * 0.75f; // 75% del potere massimo
            UpdatePower(testPower);
            Debug.Log($"[PlayerUI] Test aggiornamento potere: {testPower}/{playerPowerUp.MaxPower}");
        }
        else
        {
            Debug.LogWarning("[PlayerUI] PlayerPowerUp non assegnato - impossibile testare potere");
        }
    }

    [ContextMenu("🔥 Debug - Test Ability Icons")]
    public void DebugTestAbilityIcons()
    {
        if (abilityIcons != null && abilityIcons.Length > 0)
        {
            for (int i = 0; i < abilityIcons.Length; i++)
            {
                if (abilityIcons[i] != null)
                {
                    // Test pulse effect
                    PulseIconAt(i);
                    Debug.Log($"[PlayerUI] Test pulse icona abilità {i}");
                }
            }
        }
        else
        {
            Debug.LogWarning("[PlayerUI] Nessuna icona abilità trovata");
        }
    }

    [ContextMenu("🌟 Debug - Show Collectibles Info")]
    public void DebugShowCollectiblesInfo()
    {
        if (collectiblesUI != null)
        {
            Debug.Log($"=== Collectibles Info ===\n" +
                      $"Sistema: {GetCollectiblesSystemInfo()}\n" +
                      $"Connesso: {IsConnectedToCollectiblesSystem()}\n" +
                      $"Memories: {GetCurrentSceneMemories()}/{GetTotalSceneMemories()}\n" +
                      $"Presents: {GetCurrentScenePresents()}/{GetTotalScenePresents()}\n" +
                      $"Totale: {GetCurrentSceneCollectibles()}/{GetTotalSceneCollectibles()}");
        }
        else
        {
            Debug.LogWarning("[PlayerUI] PlayerCollectiblesUI non trovato");
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