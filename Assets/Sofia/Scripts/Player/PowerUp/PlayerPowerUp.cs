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
    public SlowdownAbility SlowdownAbility;  // meglio cast diretto
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
            powerUI.SetActive(false);
        }

        Debug.Log("[PlayerPowerUp] Start: Imposto max power e aggiorno UI");
        playerUI.SetMaxValues(100f, maxPower);
        playerUI.UpdatePower(currentPower);
    }

    private void OnEnable() => controls.Gameplay.Enable();
    private void OnDisable() => controls.Gameplay.Disable();

    private void HandleAbility(AbilityBase ability)
    {
        if (ability == null) return;

        if (ability == SlowdownAbility)
        {
            // NON attivare subito la slow, ma far partire animazione
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

    // Metodo pubblico chiamato da Animation Event nel clip "magic"
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
        currentPower = Mathf.Max(0f, currentPower - amount);
        Debug.Log($"[PlayerPowerUp] Energia consumata: {amount}. Rimasta: {currentPower}");
        playerUI.UpdatePower(currentPower);
    }

    public bool HasEnoughPower(float amount) => currentPower >= amount;

    public void AddPower(float amount)
    {
        Debug.Log($"[PlayerPowerUp] AddPower chiamato con amount: {amount}");
        if (currentPower < maxPower)
        {
            currentPower += amount;
            currentPower = Mathf.Min(currentPower, maxPower);
            Debug.Log($"[PlayerPowerUp] Energia aumentata di {amount}. Attuale: {currentPower}");
        }
        else
        {
            Debug.Log("[PlayerPowerUp] Power già al massimo");
        }
        playerUI.UpdatePower(currentPower); // aggiorna la UI subito
    }

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"[PlayerPowerUp] OnTriggerEnter con: {other.name} tag: {other.tag}");

        if (other.CompareTag("Gem"))
        {
            Gem gem = other.GetComponent<Gem>();
            if (gem != null)
            {
                Debug.Log("[PlayerPowerUp] Gemma trovata, aggiungo energia");
                AddPower(gem.GetGemValue());
                gem.Collect();
            }
            else
            {
                Debug.LogWarning("[PlayerPowerUp] Oggetto con tag Gem ma senza componente Gem");
            }
        }
    }
}
