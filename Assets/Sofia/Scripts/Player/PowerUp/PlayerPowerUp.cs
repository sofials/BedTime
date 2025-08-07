using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class PlayerPowerUp : MonoBehaviour
{
    public PlayerUI playerUI;

    [Header("Power Settings")]
    public float maxPower = 100f;
    public float currentPower = 0f;

    public float CurrentPower => currentPower;
    public float MaxPower => maxPower;

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
            powerUI.SetActive(true); // CAMBIATO: Attiva la UI del power
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

        // AGGIUNTO: Inizializza con un po' di power per test
        currentPower = 20f; // Valore di test
        
        Debug.Log("[PlayerPowerUp] Start: Imposto max power e aggiorno UI");
        Debug.Log($"[PlayerPowerUp] Valori iniziali - Current: {currentPower}, Max: {maxPower}");
        
        // Aspetta un frame prima di aggiornare l'UI per assicurarsi che tutto sia inizializzato
        StartCoroutine(DelayedUIUpdate());
    }

    private System.Collections.IEnumerator DelayedUIUpdate()
    {
        yield return new WaitForEndOfFrame();
        
        if (playerUI != null)
        {
            playerUI.SetMaxValues(100f, maxPower);
            playerUI.UpdatePower(currentPower);
            Debug.Log($"[PlayerPowerUp] UI aggiornata con power: {currentPower}/{maxPower}");
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

    // NUOVO METODO: Centralizza l'aggiornamento dell'UI
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

    // AGGIUNTO: Metodo per test manuale
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

    // AGGIUNTO: Metodo di debug per verificare lo stato
    void Update()
    {
        // Solo per debug - rimuovi in produzione
        if (Input.GetKeyDown(KeyCode.P))
        {
            Debug.Log($"[DEBUG] Current Power: {currentPower}, Max Power: {maxPower}");
            Debug.Log($"[DEBUG] PowerUI attivo: {powerUI != null && powerUI.activeInHierarchy}");
            Debug.Log($"[DEBUG] PlayerUI presente: {playerUI != null}");
            
            if (playerUI != null && playerUI.powerFill != null)
            {
                Debug.Log($"[DEBUG] PowerFill fillAmount: {playerUI.powerFill.fillAmount}");
            }
        }
    }
}