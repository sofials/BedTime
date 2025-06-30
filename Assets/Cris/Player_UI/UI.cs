using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class PlayerUI : MonoBehaviour
{
    [Header("Health UI")]
    public Image healthFill;

    [Header("Mana/Power UI")]
    public Image powerFill;

    [Header("Input Layouts")]
    public GameObject UIKeyboard;
    public GameObject UIController;

    [Header("Player References")]
    public ThirdPersonController playerController;
    public PlayerPowerUp playerPowerUp;

    [Header("Ability Icons")]
    public UIEffectHandler[] keyboardEffectIcons;   // es. 4 icone tastiera
    public UIEffectHandler[] controllerEffectIcons; // es. 4 icone controller

    private bool useGamepad = false;

    public static PlayerUI Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }


    private void OnEnable()
    {
        InputSystem.onActionChange += OnInputActionChange;
        UpdateInputLayout();
    }

    private void OnDisable()
    {
        InputSystem.onActionChange -= OnInputActionChange;
    }

    private void Update()
    {
        UpdateHealth(playerController.currentHealth);
        UpdatePower(playerPowerUp.currentPower);
    }

    private void OnInputActionChange(object obj, InputActionChange change)
    {
        if (change == InputActionChange.ActionPerformed)
        {
            UpdateInputLayout();
        }
    }

    private void UpdateInputLayout()
    {
        useGamepad = Gamepad.current != null && Gamepad.current.wasUpdatedThisFrame;

        if (UIKeyboard != null)
            UIKeyboard.SetActive(!useGamepad);

        if (UIController != null)
            UIController.SetActive(useGamepad);

        Debug.Log($"[PlayerUI] Layout attivo: {(useGamepad ? "Gamepad" : "Keyboard")}");
    }

    public void PulseIconAt(int index)
    {
        if (useGamepad)
        {
            if (controllerEffectIcons != null && index >= 0 && index < controllerEffectIcons.Length)
            {
                controllerEffectIcons[index]?.PulseIcon();
            }
        }
        else
        {
            if (keyboardEffectIcons != null && index >= 0 && index < keyboardEffectIcons.Length)
            {
                keyboardEffectIcons[index]?.PulseIcon();
            }
        }
    }

    public void UpdateHealth(float currentHealth)
    {
        if (healthFill == null || playerController == null)
        {
            Debug.LogWarning("[PlayerUI] healthFill o playerController non assegnato!");
            return;
        }

        float fillAmount = playerController.CurrentHealth / playerController.MaxHealth;
        healthFill.fillAmount = fillAmount;
    }

    public void UpdatePower(float currentPower)
    {
        if (powerFill == null || playerPowerUp == null)
        {
            Debug.LogWarning("[PlayerUI] powerFill o playerPowerUp non assegnato!");
            return;
        }

        float fillAmount = playerPowerUp.CurrentPower / playerPowerUp.MaxPower;
        powerFill.fillAmount = fillAmount;
    }

    public void SetMaxValues(float maxHealth, float maxPower)
    {
        if (playerController != null)
        {
            playerController.maxHealth = maxHealth;
            UpdateHealth(playerController.CurrentHealth);
        }
        if (playerPowerUp != null)
        {
            playerPowerUp.maxPower = maxPower;
            UpdatePower(playerPowerUp.CurrentPower);
        }
    }
}
