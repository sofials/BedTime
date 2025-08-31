using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerPowerUp : MonoBehaviour
{
    public PlayerUI playerUI;

    [Header("Power Settings")]
    public float maxPower = 200f;
    public float currentPower = 200f;

    [Header("Level Start Settings")]
    [SerializeField] private bool startWithZeroPower = false;
    [Tooltip("Se attivato, il player inizierà il livello con 0 potere invece del massimo")]

    public float CurrentPower => currentPower;
    public float MaxPower => maxPower;
    
    [Header("Debug")]
    public float debugPowerAmount = 10f;

    [Header("UI")]
    public GameObject powerUI;

    [Header("Abilities")]
    public AbilityBase PlatformSpawnerForwardAbility;
    public SlowdownAbility SlowdownAbility;
    public AbilityBase TeleportAbility;

    private PlayerControls controls;

    [Header("References")]
    public Animator playerAnimator;

    void Awake()
    {
        controls = new PlayerControls();

        controls.Gameplay.Create.performed += ctx => HandleAbility(PlatformSpawnerForwardAbility);
        controls.Gameplay.Time.performed += ctx => HandleAbility(SlowdownAbility);
        controls.Gameplay.Teleport.performed += ctx => HandleAbility(TeleportAbility);

        if (playerAnimator == null)
            playerAnimator = GetComponent<Animator>();
    }

    void Start()
    {
        if (powerUI != null)
        {
            powerUI.SetActive(true);
        }

        // VERIFICA che playerUI sia assegnata
        if (playerUI == null)
        {
            Debug.LogError("[PlayerPowerUp] playerUI NON è assegnata! Cerca PlayerUI in scena...");
            playerUI = Object.FindAnyObjectByType<PlayerUI>();
           
            if (playerUI != null)
            {
                Debug.Log("[PlayerPowerUp] PlayerUI trovata automaticamente!");
            }
            else
            {
                Debug.LogError("[PlayerPowerUp] PlayerUI non trovata nemmeno in scena!");
                return;
            }
        }

        // Logica per il potere iniziale
        if (startWithZeroPower)
        {
            currentPower = 0f;
            Debug.Log($"[PlayerPowerUp] Iniziando con ZERO potere (Toggle attivo)");
        }
        else
        {
            currentPower = maxPower;
            Debug.Log($"[PlayerPowerUp] Iniziando con potere MASSIMO ({currentPower})");
        }
        
        Debug.Log($"[PlayerPowerUp] Start: Potere iniziale impostato a {currentPower}/{maxPower}");
        
        StartCoroutine(DelayedUIUpdate());
    }

    private System.Collections.IEnumerator DelayedUIUpdate()
    {
        yield return new WaitForEndOfFrame();
        
        if (playerUI != null)
        {
            Debug.Log($"[PlayerPowerUp] Aggiornamento UI sicuro - NON modifico maxHealth");
            
            if (playerUI.playerController != null)
            {
                float correctMaxHealth = playerUI.playerController.MaxHealth;
                Debug.Log($"[PlayerPowerUp] Usando maxHealth corretto: {correctMaxHealth}");
                playerUI.SetMaxValues(correctMaxHealth, maxPower);
            }
            else
            {
                Debug.LogWarning("[PlayerPowerUp] PlayerController non trovato, aggiorno solo power");
                UpdatePowerUIOnly();
            }
            
            playerUI.UpdatePower(currentPower);
            Debug.Log($"[PlayerPowerUp] UI aggiornata con power: {currentPower}/{maxPower}");
        }
        else
        {
            Debug.LogError("[PlayerPowerUp] PlayerUI non trovato durante DelayedUIUpdate!");
        }
    }

    private void UpdatePowerUIOnly()
    {
        if (playerUI != null && playerUI.playerPowerUp != null)
        {
            playerUI.playerPowerUp.maxPower = maxPower;
            Debug.Log($"[PlayerPowerUp] Aggiornato solo maxPower: {maxPower}");
        }
        else
        {
            Debug.LogWarning("[PlayerPowerUp] Impossibile aggiornare power - riferimenti mancanti");
        }
    }

    private void OnEnable() => controls.Gameplay.Enable();
    private void OnDisable() => controls.Gameplay.Disable();


private void HandleAbility(AbilityBase ability)
{
    if (ability == null) 
    {
        Debug.Log("[PlayerPowerUp] Ability is null");
        return;
    }

    // UNICO controllo: se il GameObject/componente è attivo
    if (!ability.gameObject.activeInHierarchy || !ability.enabled)
    {
        Debug.Log($"[PlayerPowerUp] GameObject '{ability.name}' inactive or component disabled - SILENT EXIT");
        return;
    }

    Debug.Log($"[PlayerPowerUp] Processing ability '{ability.name}'...");

    // Delega alla classe base PRIMA di qualsiasi altra azione
    bool actionPerformed = false;
    
    if (ability.IsActive)
    {
        Debug.Log($"[PlayerPowerUp] Deactivating ability '{ability.name}'");
        ability.Deactivate();
        actionPerformed = true;
    }
    else
    {
        // Controlla se può essere attivata
        if (ability.CanActivate())
        {
            Debug.Log($"[PlayerPowerUp] Ability '{ability.name}' can be activated, proceeding...");
            ability.TryActivate();
            actionPerformed = true;
        }
        else
        {
            Debug.Log($"[PlayerPowerUp] Ability '{ability.name}' cannot be activated: {ability.GetDisableReason()}, delegating for feedback only");
            ability.TryActivate(); // Gestisce feedback appropriato
            return; // Non riprodurre animazione
        }
    }

    // Riproduce animazione SOLO se l'azione è stata eseguita con successo
    if (actionPerformed && ability == SlowdownAbility)
    {
        if (playerAnimator != null)
        {
            playerAnimator.SetTrigger("SlowdownEffect");
            Debug.Log("[PlayerPowerUp] Animazione SlowdownEffect attivata dopo successo");
        }
    }
}
    public void SpendPower(float amount)
    {
        Debug.Log($"[PlayerPowerUp] SpendPower chiamato con amount: {amount}");
        float oldPower = currentPower;
        currentPower = Mathf.Max(0f, currentPower - amount);
        Debug.Log($"[PlayerPowerUp] Energia consumata: {amount}. Da {oldPower} a {currentPower}");
        
        UpdateUI();
    }

    public bool HasEnoughPower(float amount) => currentPower >= amount;

    public void AddPower(float amount)
    {
        Debug.Log($"[PlayerPowerUp] AddPower chiamato con amount: {amount}");
        
        float oldPower = currentPower;
        currentPower = Mathf.Min(currentPower + amount, maxPower);
        
        Debug.Log($"[PlayerPowerUp] Energia aumentata da {oldPower} a {currentPower} (+{amount})");
        
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (playerUI != null)
        {
            Debug.Log($"[PlayerPowerUp] Aggiornando UI con power: {currentPower}/{maxPower}");
            playerUI.UpdatePower(currentPower);
        }
        else
        {
            Debug.LogError("[PlayerPowerUp] playerUI è null!");
        }
    }

    // Metodi di utilità
    public void SetStartWithZeroPower(bool enable)
    {
        startWithZeroPower = enable;
        Debug.Log($"[PlayerPowerUp] Start With Zero Power impostato a: {enable}");
    }
    
    public bool GetStartWithZeroPower()
    {
        return startWithZeroPower;
    }
    
    public void ResetPowerToZero()
    {
        currentPower = 0f;
        UpdateUI();
        Debug.Log("[PlayerPowerUp] Power resettato a zero");
    }

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"[PlayerPowerUp] OnTriggerEnter con: {other.name} tag: {other.tag}");

        if (other.CompareTag("Gem"))
        {
            Gem gem = other.GetComponent<Gem>();
            if (gem != null)
            {
                Debug.Log($"[PlayerPowerUp] Gemma trovata con valore: {gem.GetGemValue()}");
                
                gem.Collect();
                AddPower(gem.GetGemValue());
            }
            else
            {
                Debug.LogWarning("[PlayerPowerUp] Oggetto con tag Gem ma senza componente Gem");
            }
        }
    }

    // Debug methods
    [ContextMenu("Debug - Show Power Status")]
    public void DebugShowPowerStatus()
    {
        Debug.Log($"=== POWER STATUS ===");
        Debug.Log($"Current Power: {currentPower}");
        Debug.Log($"Max Power: {maxPower}");
        Debug.Log($"Start With Zero Power: {startWithZeroPower}");
        Debug.Log($"PowerUI attivo: {powerUI != null && powerUI.activeInHierarchy}");
        Debug.Log($"PlayerUI presente: {playerUI != null}");
        
        if (SlowdownAbility != null)
        {
            Debug.Log($"SlowdownAbility presente: {SlowdownAbility.name}");
            Debug.Log($"SlowdownAbility attiva: {SlowdownAbility.IsActive}");
            Debug.Log($"SlowdownAbility power cost: {SlowdownAbility.powerCost}");
            Debug.Log($"Può attivare SlowdownAbility: {HasEnoughPower(SlowdownAbility.powerCost)}");
        }
        else
        {
            Debug.LogError("SlowdownAbility è NULL!");
        }
    }

    // Debug key controls
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.P))
        {
            DebugShowPowerStatus();
        }
        if (Input.GetKeyDown(KeyCode.M))
        {
            Debug.Log($"[PlayerPowerUp] Tasto M premuto - Aggiungendo {debugPowerAmount} power");
            AddPower(debugPowerAmount);
        }
    }
}