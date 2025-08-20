using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class PlayerPowerUp : MonoBehaviour
{
    public PlayerUI playerUI;

    [Header("Power Settings")]
    public float maxPower = 200f;
    public float currentPower = 200f;

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

        currentPower = maxPower;
        Debug.Log("[PlayerPowerUp] Start: Imposto max power e aggiorno UI");
        Debug.Log($"[PlayerPowerUp] Valori iniziali - Current: {currentPower}, Max: {maxPower}");
        
        // Aspetta un frame prima di aggiornare l'UI per assicurarsi che tutto sia inizializzato
        StartCoroutine(DelayedUIUpdate());
    }

    // 🔧 FIX: DelayedUIUpdate corretto - Non modifica più maxHealth!
    private System.Collections.IEnumerator DelayedUIUpdate()
    {
        yield return new WaitForEndOfFrame();
        
        if (playerUI != null)
        {
            // ❌ VECCHIO CODICE CHE CAUSAVA IL PROBLEMA:
            // playerUI.SetMaxValues(100f, maxPower); // Questo impostava maxHealth a 100!
            
            // ✅ NUOVO CODICE SICURO:
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

    private void HandleAbility(AbilityBase ability)
    {
        if (ability == null) return;

        if (ability == SlowdownAbility)
        {
            if (playerAnimator != null)
            {
                playerAnimator.SetTrigger("SlowdownEffect");
                Debug.Log("[PlayerPowerUp] Trigger animazione SlowdownEffect inviato.");
            }
            else
            {
                Debug.LogWarning("[PlayerPowerUp] playerAnimator non assegnato!");
            }
        }
        else
        {
            if (ability.IsActive)
                ability.Deactivate();
            else
                ability.TryActivate();
        }
    }

    public void OnMagicEffectStart()
    {
        if (SlowdownAbility != null && !SlowdownAbility.IsActive)
        {
            SlowdownAbility.TryActivate();
            Debug.Log("[PlayerPowerUp] SlowdownAbility attivata tramite Animation Event.");
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

    // 🆕 NUOVO: Metodi di debug migliorati
    [ContextMenu("🔍 Debug - Show Power Status")]
    public void DebugShowPowerStatus()
    {
        Debug.Log($"=== POWER STATUS ===");
        Debug.Log($"Current Power: {currentPower}");
        Debug.Log($"Max Power: {maxPower}");
        Debug.Log($"PowerUI attivo: {powerUI != null && powerUI.activeInHierarchy}");
        Debug.Log($"PlayerUI presente: {playerUI != null}");
        
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
    }
}