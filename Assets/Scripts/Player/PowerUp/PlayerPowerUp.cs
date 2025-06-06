using UnityEngine;
using UnityEngine.UI;

public class PlayerPowerUp : MonoBehaviour
{
    [Header("Power Settings")]
    private float maxPower = 100f;
    private float currentPower = 0f;

    [Header("UI")]
    public Slider powerSlider;  // Cambiato da Image a Slider

    [Header("Abilities")]
    public AbilityBase platformAbility;
    public AbilityBase SlowdownAbility;
    public AbilityBase TeleportAbility;

    void Start()
    {
        if (platformAbility != null) platformAbility.powerUpScript = this;
        if (SlowdownAbility != null) SlowdownAbility.powerUpScript = this;
        if (TeleportAbility != null) TeleportAbility.powerUpScript = this;

        if (powerSlider != null)
        {
            powerSlider.maxValue = maxPower;
            powerSlider.value = currentPower;
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F) && platformAbility != null)
        {
            platformAbility.TryActivate();
        }
        else if (Input.GetKeyDown(KeyCode.Q) && SlowdownAbility != null)
        {
            SlowdownAbility.TryActivate();
        }
        else if (Input.GetKeyDown(KeyCode.E) && TeleportAbility != null)
        {
            TeleportAbility.TryActivate();
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
