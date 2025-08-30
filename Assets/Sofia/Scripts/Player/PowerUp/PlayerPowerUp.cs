using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerPowerUp : MonoBehaviour
{
    public PlayerUI playerUI;

    [Header("Power Settings")]
    public float maxPower = 200f;
    public float currentPower = 200f;

    // 🆕 NUOVO: Toggle semplice per iniziare con zero power
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

        // 🆕 NUOVO: Logica semplice per il potere iniziale
        if (startWithZeroPower)
        {
            currentPower = 0f;
            Debug.Log($"[PlayerPowerUp] 🔋 Iniziando con ZERO potere (Toggle attivo)");
        }
        else
        {
            currentPower = maxPower;
            Debug.Log($"[PlayerPowerUp] 🔋 Iniziando con potere MASSIMO ({currentPower})");
        }
        
        Debug.Log($"[PlayerPowerUp] Start: Potere iniziale impostato a {currentPower}/{maxPower}");
        
        // Aspetta un frame prima di aggiornare l'UI per assicurarsi che tutto sia inizializzato
        StartCoroutine(DelayedUIUpdate());
    }

    // 🔧 FIX: DelayedUIUpdate corretto - Non modifica più maxHealth!
    private System.Collections.IEnumerator DelayedUIUpdate()
    {
        yield return new WaitForEndOfFrame();
        
        if (playerUI != null)
        {
            Debug.Log($"[PlayerPowerUp] 🔧 Aggiornamento UI sicuro - NON modifico maxHealth");
            
            // Opzione 1: Usa il valore corretto di maxHealth dal playerController
            if (playerUI.playerController != null)
            {
                float correctMaxHealth = playerUI.playerController.MaxHealth;
                Debug.Log($"[PlayerPowerUp] Usando maxHealth corretto: {correctMaxHealth}");
                playerUI.SetMaxValues(correctMaxHealth, maxPower);
            }
            else
            {
                // Opzione 2: Aggiorna solo il power senza toccare la salute
                Debug.LogWarning("[PlayerPowerUp] PlayerController non trovato, aggiorno solo power");
                UpdatePowerUIOnly();
            }
            
            // Aggiorna il power corrente
            playerUI.UpdatePower(currentPower);
            Debug.Log($"[PlayerPowerUp] ✅ UI aggiornata con power: {currentPower}/{maxPower}");
        }
        else
        {
            Debug.LogError("[PlayerPowerUp] ❌ PlayerUI non trovato durante DelayedUIUpdate!");
        }
    }

    // 🆕 NUOVO METODO: Aggiorna solo la UI del power senza toccare maxHealth
    private void UpdatePowerUIOnly()
    {
        if (playerUI != null && playerUI.playerPowerUp != null)
        {
            // Aggiorna direttamente il maxPower nel playerPowerUp referenziato da playerUI
            playerUI.playerPowerUp.maxPower = maxPower;
            Debug.Log($"[PlayerPowerUp] 🔧 Aggiornato solo maxPower: {maxPower}");
        }
        else
        {
            Debug.LogWarning("[PlayerPowerUp] ⚠️ Impossibile aggiornare power - riferimenti mancanti");
        }
    }

    private void OnEnable() => controls.Gameplay.Enable();
    private void OnDisable() => controls.Gameplay.Disable();

    // 🔧 FIX PRINCIPALE: Logica corretta per SlowdownAbility
    private void HandleAbility(AbilityBase ability)
    {
        if (ability == null) return;

        // 🆕 NUOVO: Gestione speciale per SlowdownAbility
        if (ability == SlowdownAbility)
        {
            // 🔧 CONTROLLO FONDAMENTALE: Verifica se l'abilità è abilitata nel livello tramite AbilitiesManager
            if (!SlowdownAbility.IsEnabled)
            {
                Debug.Log("[PlayerPowerUp] ❌ SlowdownAbility NON abilitata nel livello - Nessun suono o animazione.");
                return;
            }
            
            if (SlowdownAbility.IsActive)
            {
                // Se è già attiva, può essere disattivata
                SlowdownAbility.Deactivate();
                Debug.Log("[PlayerPowerUp] SlowdownAbility disattivata.");
                return; // Esci senza fare altro
            }
            else
            {
                // Controlla se ha abbastanza power PRIMA di attivare
                bool hasEnoughPower = HasEnoughPower(SlowdownAbility.powerCost);
                
                if (hasEnoughPower)
                {
                    // Ha abbastanza power - attiva l'abilità e riproduci effetti
                    SlowdownAbility.TryActivate();
                    
                    // 🔧 DOPPIO CONTROLLO: Verifica che l'abilità sia stata effettivamente attivata
                    if (SlowdownAbility.IsActive)
                    {
                        if (playerAnimator != null)
                        {
                            playerAnimator.SetTrigger("SlowdownEffect");
                            Debug.Log("[PlayerPowerUp] ✅ SlowdownAbility attivata - Trigger animazione SlowdownEffect inviato.");
                        }
                        else
                        {
                            Debug.LogWarning("[PlayerPowerUp] playerAnimator non assegnato!");
                        }
                    }
                    else
                    {
                        Debug.Log("[PlayerPowerUp] ⚠️ SlowdownAbility non si è attivata dopo TryActivate - Nessun suono o animazione.");
                    }
                }
                else
                {
                    Debug.Log("[PlayerPowerUp] ❌ Power insufficiente per SlowdownAbility - Nessun suono o animazione.");
                }
            }
        }
        else
        {
            // Gestione standard per le altre abilità
            if (ability.IsActive)
                ability.Deactivate();
            else
                ability.TryActivate();
        }
    }

    // 🔧 MODIFICATO: Ora questo metodo viene chiamato solo dall'Animation Event quando l'animazione è effettivamente partita
    public void OnMagicEffectStart()
    {
        Debug.Log("[PlayerPowerUp] OnMagicEffectStart chiamato dall'Animation Event.");
        
        // Questo metodo ora viene chiamato solo quando l'animazione è partita,
        // quindi l'abilità dovrebbe già essere attiva
        if (SlowdownAbility != null && SlowdownAbility.IsActive)
        {
            Debug.Log("[PlayerPowerUp] ✅ SlowdownAbility confermata attiva durante animazione.");
        }
        else
        {
            Debug.LogWarning("[PlayerPowerUp] ⚠️ OnMagicEffectStart chiamato ma SlowdownAbility non è attiva!");
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

    // METODO SICURO: Centralizza l'aggiornamento dell'UI
    private void UpdateUI()
    {
        if (playerUI != null)
        {
            Debug.Log($"[PlayerPowerUp] 🔄 Aggiornando UI con power: {currentPower}/{maxPower}");
            playerUI.UpdatePower(currentPower);
        }
        else
        {
            Debug.LogError("[PlayerPowerUp] ❌ playerUI è null!");
        }
    }

    // 🆕 NUOVO METODO PUBBLICO: Per controllo runtime del toggle
    
    /// <summary>
    /// Attiva/disattiva l'inizio con zero power
    /// </summary>
    public void SetStartWithZeroPower(bool enable)
    {
        startWithZeroPower = enable;
        Debug.Log($"[PlayerPowerUp] 🔄 Start With Zero Power impostato a: {enable}");
    }
    
    /// <summary>
    /// Ottieni lo stato attuale del toggle
    /// </summary>
    public bool GetStartWithZeroPower()
    {
        return startWithZeroPower;
    }
    
    /// <summary>
    /// Resetta il power a zero immediatamente (utile per testing)
    /// </summary>
    public void ResetPowerToZero()
    {
        currentPower = 0f;
        UpdateUI();
        Debug.Log("[PlayerPowerUp] 🔄 Power resettato a zero");
    }

    // Test methods
    [ContextMenu("Test Add Power")]
    public void TestAddPower()
    {
        AddPower(10f);
    }

    [ContextMenu("Test Spend Power")]
    public void TestSpendPower()
    {
        SpendPower(10f);
    }

    // 🆕 NUOVO METODO DI TEST: Testa SlowdownAbility senza power
    [ContextMenu("🧪 Test - Slowdown Without Power")]
    public void TestSlowdownWithoutPower()
    {
        Debug.Log("[PlayerPowerUp] 🧪 Test: Tentativo SlowdownAbility senza power...");
        
        // Salva il power attuale
        float originalPower = currentPower;
        
        // Imposta power a zero temporaneamente
        currentPower = 0f;
        UpdateUI();
        
        // Tenta di usare SlowdownAbility
        HandleAbility(SlowdownAbility);
        
        // Ripristina il power originale
        currentPower = originalPower;
        UpdateUI();
        
        Debug.Log("[PlayerPowerUp] ✅ Test completato - controlla che non ci siano stati suoni o animazioni");
    }

    // 🆕 NUOVO METODO DI TEST: Testa SlowdownAbility disabilitata nel livello
    [ContextMenu("🧪 Test - Slowdown Disabled In Level")]
    public void TestSlowdownDisabledInLevel()
    {
        Debug.Log("[PlayerPowerUp] 🧪 Test: Tentativo SlowdownAbility disabilitata nel livello...");
        
        if (SlowdownAbility != null)
        {
            Debug.Log($"[PlayerPowerUp] Stato prima del test:");
            Debug.Log($"- IsEnabled: {SlowdownAbility.IsEnabled}");
            Debug.Log($"- IsActive: {SlowdownAbility.IsActive}");
            Debug.Log($"- Power disponibile: {currentPower}");
            Debug.Log($"- Power richiesto: {SlowdownAbility.powerCost}");
            
            // Tenta di usare l'abilità (dovrebbe essere bloccata se disabilitata)
            HandleAbility(SlowdownAbility);
            
            Debug.Log($"[PlayerPowerUp] Stato dopo il tentativo:");
            Debug.Log($"- IsEnabled: {SlowdownAbility.IsEnabled}");
            Debug.Log($"- IsActive: {SlowdownAbility.IsActive}");
        }
        else
        {
            Debug.LogError("[PlayerPowerUp] SlowdownAbility è null!");
        }
        
        Debug.Log("[PlayerPowerUp] ✅ Test completato - se l'abilità era disabilitata, non dovrebbero esserci stati suoni o animazioni");
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
                
                // Prima raccogli la gem
                gem.Collect();
                
                // POI aggiungi il power
                AddPower(gem.GetGemValue());
            }
            else
            {
                Debug.LogWarning("[PlayerPowerUp] Oggetto con tag Gem ma senza componente Gem");
            }
        }
    }

    // 🆕 METODI DI DEBUG SEMPLIFICATI
    [ContextMenu("🔍 Debug - Show Power Status")]
    public void DebugShowPowerStatus()
    {
        Debug.Log($"=== POWER STATUS ===");
        Debug.Log($"Current Power: {currentPower}");
        Debug.Log($"Max Power: {maxPower}");
        Debug.Log($"Start With Zero Power: {startWithZeroPower}");
        Debug.Log($"PowerUI attivo: {powerUI != null && powerUI.activeInHierarchy}");
        Debug.Log($"PlayerUI presente: {playerUI != null}");
        
        // 🆕 DEBUG per SlowdownAbility
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
        
        if (playerUI != null)
        {
            Debug.Log($"PlayerUI.playerController: {playerUI.playerController?.name ?? "NULL"}");
            Debug.Log($"PlayerUI.playerPowerUp: {playerUI.playerPowerUp?.name ?? "NULL"}");
            
            if (playerUI.powerFill != null)
            {
                Debug.Log($"PowerFill fillAmount: {playerUI.powerFill.fillAmount}");
                Debug.Log($"PowerFill attivo: {playerUI.powerFill.gameObject.activeInHierarchy}");
            }
            else
            {
                Debug.LogError("PowerFill è NULL!");
            }
        }
    }

    [ContextMenu("🔧 Debug - Force UI Update")]
    public void DebugForceUIUpdate()
    {
        Debug.Log("[PlayerPowerUp] 🔧 Forza aggiornamento UI...");
        UpdateUI();
        
        if (playerUI != null)
        {
            // Verifica che playerUI abbia il riferimento corretto a questo script
            if (playerUI.playerPowerUp != this)
            {
                Debug.LogWarning($"⚠️ PlayerUI riferisce a un PlayerPowerUp diverso: {playerUI.playerPowerUp?.name ?? "NULL"}");
                Debug.Log("Assegnando il riferimento corretto...");
                playerUI.playerPowerUp = this;
            }
        }
    }

    [ContextMenu("🧪 Debug - Test Safe SetMaxValues")]
    public void DebugTestSafeSetMaxValues()
    {
        if (playerUI != null)
        {
            Debug.Log("[PlayerPowerUp] 🧪 Test SetMaxValues sicuro...");
            
            // Ottieni maxHealth corretto dal playerController
            float correctMaxHealth = playerUI.playerController?.MaxHealth ?? 300f;
            Debug.Log($"MaxHealth da usare: {correctMaxHealth}");
            
            // Chiama SetMaxValues con il valore corretto
            playerUI.SetMaxValues(correctMaxHealth, maxPower);
            
            Debug.Log("✅ Test completato - controlla che maxHealth non sia cambiato!");
        }
        else
        {
            Debug.LogError("❌ PlayerUI non assegnato!");
        }
    }

    // 🆕 METODI DI TESTING SEMPLIFICATI
    [ContextMenu("🔋 Test - Toggle Zero Power Start")]
    public void DebugToggleZeroPowerStart()
    {
        startWithZeroPower = !startWithZeroPower;
        Debug.Log($"[PlayerPowerUp] 🔄 Toggle Zero Power Start: {startWithZeroPower}");
    }

    [ContextMenu("⚡ Test - Simulate Level Restart")]
    public void DebugSimulateLevelRestart()
    {
        Debug.Log("[PlayerPowerUp] 🔄 Simulando restart livello...");
        
        if (startWithZeroPower)
        {
            currentPower = 0f;
            Debug.Log($"[PlayerPowerUp] 🔋 Power dopo restart: ZERO");
        }
        else
        {
            currentPower = maxPower;
            Debug.Log($"[PlayerPowerUp] 🔋 Power dopo restart: MASSIMO ({currentPower})");
        }
        
        UpdateUI();
    }

            // Debug key - rimuovi in produzione se non serve
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.P))
        {
            DebugShowPowerStatus();
        }
        if (Input.GetKeyDown(KeyCode.M))
        {
            Debug.Log($"[PlayerPowerUp] 🔋 Tasto M premuto - Aggiungendo {debugPowerAmount} power");
            AddPower(debugPowerAmount);
        }
        // 🆕 NUOVO: Tasto per testare zero power
        if (Input.GetKeyDown(KeyCode.Z))
        {
            DebugToggleZeroPowerStart();
            DebugSimulateLevelRestart();
        }
        // 🆕 NUOVO: Tasto per testare SlowdownAbility senza power
        if (Input.GetKeyDown(KeyCode.N))
        {
            TestSlowdownWithoutPower();
        }
        // 🆕 NUOVO: Tasto per testare SlowdownAbility disabilitata nel livello
        if (Input.GetKeyDown(KeyCode.B))
        {
            TestSlowdownDisabledInLevel();
        }
    }
}