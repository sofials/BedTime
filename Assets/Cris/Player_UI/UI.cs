using UnityEngine;
using UnityEngine.UI;

public class PlayerUI : MonoBehaviour
{
    [Header("Health UI")]
    public Image healthFill;

    [Header("Mana/Power UI")]
    public Image powerFill;

    private float maxHealth = 100f;
    private float maxPower = 100f;

    public void UpdateHealth(float currentHealth)
    {
        if (healthFill == null)
        {
            Debug.LogWarning("[PlayerUI] healthFill non assegnato!");
            return;
        }

        float fillAmount = currentHealth / maxHealth;
        healthFill.fillAmount = fillAmount;
        Debug.Log($"[PlayerUI] UpdateHealth chiamato. FillAmount: {fillAmount}");
    }

    public void UpdatePower(float currentPower)
    {
        if (powerFill == null)
        {
            Debug.LogWarning("[PlayerUI] powerFill non assegnato!");
            return;
        }

        float fillAmount = currentPower / maxPower;
        powerFill.fillAmount = fillAmount;
        Debug.Log($"[PlayerUI] UpdatePower chiamato. FillAmount: {fillAmount}");
    }

    public void SetMaxValues(float maxHealthVal, float maxPowerVal)
    {
        maxHealth = maxHealthVal;
        maxPower = maxPowerVal;
        Debug.Log($"[PlayerUI] SetMaxValues chiamato. maxHealth: {maxHealth}, maxPower: {maxPower}");
    }
}
