using UnityEngine;
using UnityEngine.InputSystem;

public class InputSchemeSwitcher : MonoBehaviour
{
    private PlayerInput playerInput;
    private string forcedScheme = "Keyboard&Mouse"; // default iniziale
    private bool lockSwitching = true;

    void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
        playerInput.onControlsChanged += OnControlsChanged;
        ApplyScheme(forcedScheme);
    }

    void Update()
    {
        // Premi TAB per passare a tastiera/mouse
        if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
        {
            SwitchTo("Keyboard&Mouse");
        }

        // Premi START per passare a gamepad
        if (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame)
        {
            SwitchTo("Gamepad");
        }
    }

    private void OnControlsChanged(PlayerInput input)
    {
        // Se è bloccato lo switch automatico, annulla i cambi
        if (lockSwitching && input.currentControlScheme != forcedScheme)
        {
            Debug.Log($"Still: {forcedScheme}");
            ApplyScheme(forcedScheme);
        }
    }

    private void SwitchTo(string scheme)
    {
        forcedScheme = scheme;
        Debug.Log($"Switched to: {forcedScheme}");
        ApplyScheme(forcedScheme);
    }

    private void ApplyScheme(string scheme)
    {
        if (scheme == "Gamepad")
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else // Keyboard&Mouse
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
