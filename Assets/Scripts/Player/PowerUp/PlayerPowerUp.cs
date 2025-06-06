using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class PlayerPowerUp : MonoBehaviour
{
    [Header("Power Settings")]
    private float maxPower = 100f;
    private float currentPower = 0f;

    [Header("UI")]
    public Slider powerSlider;  // Cambiato da Image a Slider

    [Header("Abilities")]
    public AbilityBase PlatformAbility;
    public AbilityBase SlowdownAbility;
    public AbilityBase TeleportAbility;

    private Dictionary<KeyCode, AbilityBase> abilityKeyMap;

    void Start()
    {
        abilityKeyMap = new Dictionary<KeyCode, AbilityBase>()
        {
            { KeyCode.F, PlatformAbility },
            { KeyCode.Q, SlowdownAbility },
            { KeyCode.E, TeleportAbility }
        };

        foreach (var ability in abilityKeyMap.Values)
        {
            if (ability != null)
                ability.powerUpScript = this;
        }

        if (powerSlider != null)
        {
            powerSlider.maxValue = maxPower;
            powerSlider.value = currentPower;
        }
    }

    void Update()
    {
        foreach (var kvp in abilityKeyMap)
        {
            KeyCode key = kvp.Key;
            AbilityBase ability = kvp.Value;

            if (ability == null) continue;

            if (Input.GetKeyDown(key))
            {
                if (ability.IsActive)
                    ability.Deactivate();
                else
                    ability.TryActivate();

                break; // uscita dal ciclo dopo gestione input
            }
        }
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
