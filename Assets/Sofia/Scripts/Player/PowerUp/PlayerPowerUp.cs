using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using UnityEngine.InputSystem;

public class PlayerPowerUp : MonoBehaviour
{
    [Header("Power Settings")]
    private float maxPower = 100f;
    private float currentPower = 0f;

    [Header("UI")]
    public Slider powerSlider;  // Cambiato da Image a Slider

    [Header("Abilities")]
    public AbilityBase PlatformSpawnerForwardAbility;
    public AbilityBase SlowdownAbility;
    public AbilityBase TeleportAbility;

    private Dictionary<KeyCode, AbilityBase> abilityKeyMap;

    private PlayerControls controls;

    void Start()
    {
        if (powerSlider != null)
        {
            powerSlider.maxValue = maxPower;
            powerSlider.value = currentPower;
        }
    }

    private void Awake()
    {
        controls = new PlayerControls();

        controls.Gameplay.Create.performed += ctx => HandleAbility(PlatformSpawnerForwardAbility);
        controls.Gameplay.Time.performed += ctx => HandleAbility(SlowdownAbility);
        controls.Gameplay.Teleport.performed += ctx => HandleAbility(TeleportAbility);

    }

    private void OnEnable() => controls.Gameplay.Enable();
    private void OnDisable() => controls.Gameplay.Disable();

    private void HandleAbility(AbilityBase ability)
    {
        if (ability == null) return;

        if (ability.IsActive)
            ability.Deactivate();
        else
            ability.TryActivate();
    }


    void Update()
    {
    }

    public void SpendPower(float amount)
    {
        currentPower = Mathf.Max(0f, currentPower - amount);
        UpdatePowerBar();

        Debug.Log($"Energia consumata: {amount}. Rimasta: {currentPower}");
    }

    public bool HasEnoughPower(float amount)
    {
        return currentPower >= amount;
    }

    public void AddPower(float amount)
    {
        if (currentPower < maxPower)
        {
            currentPower += amount;
            currentPower = Mathf.Min(currentPower, maxPower);
            UpdatePowerBar();

            Debug.Log($"Energia aumentata di {amount}. Attuale: {currentPower}");
        }
    }

    public void UpdatePowerBar()
    {
        if (powerSlider != null)
            powerSlider.value = currentPower;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Gem"))
        {
            Gem gem = other.GetComponent<Gem>();
            if (gem != null)
            {
                AddPower(gem.GetGemValue());
                gem.Collect();
            }
        }
    }
}
