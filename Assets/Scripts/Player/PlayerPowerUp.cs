using UnityEngine;
using UnityEngine.UI;

public class PlayerPowerUp : MonoBehaviour
{
    [Header("Barra Power-Up")]
    [SerializeField] private Slider powerUpSlider;
    [SerializeField] private int maxPower = 100;

    private int currentPower = 0;
    private bool powerUpReady = false; // Flag per sapere se la barra è piena

    private void Start()
    {
        powerUpSlider.maxValue = maxPower;
        powerUpSlider.value = currentPower;
    }

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("Entrato in contatto con: " + other.name);

        if (powerUpReady) return; // Se il power-up è pronto, non aggiungere più potere

        Gem gem = other.GetComponent<Gem>();
        if (gem != null)
        {
            AddPower(gem.GetGemValue());
            gem.Collect();
        }
    }

    private void AddPower(int amount)
    {
        currentPower += amount;
        currentPower = Mathf.Clamp(currentPower, 0, maxPower);
        powerUpSlider.value = currentPower;

        if (currentPower >= maxPower)
        {
            currentPower = maxPower; // Assicuriamoci che non vada oltre
            powerUpReady = true; // Blocca ulteriori incrementi
            ActivatePowerUp();
        }
    }

    private void ActivatePowerUp()
    {
        Debug.Log("Power-Up pronto! La barra è al massimo.");
        // Qui puoi attivare il potenziamento (ma la barra resta piena finché non la svuoti)
        // Se vuoi azzerarla manualmente, potresti aggiungere un tasto o un trigger separato.
    }
}
