using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerUIAbilities : MonoBehaviour
{
    [Header("Ability Icons")]
    public UIEffectHandler[] abilityIcons;

    private bool useGamepad = false;

    public static PlayerUIAbilities Instance { get; private set; }

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

    private void OnInputActionChange(object obj, InputActionChange change)
    {
        if (change == InputActionChange.ActionPerformed)
        {
            UpdateInputLayout();
        }
    }

    private void UpdateInputLayout()
    {
        bool wasGamepad = useGamepad;
        useGamepad = Gamepad.current != null && Gamepad.current.wasUpdatedThisFrame;

        if (wasGamepad != useGamepad && abilityIcons != null)
        {
            for (int i = 0; i < abilityIcons.Length; i++)
            {
                if (abilityIcons[i] != null)
                {
                    abilityIcons[i].UpdateInputText(useGamepad);
                }
            }
        }
    }

    // ========== ABILITY ICONS METHODS ==========

    public void UpdateAbilityIconState(int index, bool canActivate)
    {
        if (abilityIcons != null && index >= 0 && index < abilityIcons.Length && abilityIcons[index] != null)
        {
            abilityIcons[index].SetGrayscale(!canActivate);
            abilityIcons[index].UpdateInputText(useGamepad);
        }
    }

    public void PulseIconAt(int index)
    {
        if (abilityIcons != null && index >= 0 && index < abilityIcons.Length)
        {
            abilityIcons[index]?.PulseIcon();
        }
    }

    // ========== DEBUG METHODS ==========

    [ContextMenu("🔥 Debug - Test Ability Icons")]
    public void DebugTestAbilityIcons()
    {
        if (abilityIcons != null && abilityIcons.Length > 0)
        {
            for (int i = 0; i < abilityIcons.Length; i++)
            {
                if (abilityIcons[i] != null)
                {
                    // Test pulse effect
                    PulseIconAt(i);
                    Debug.Log($"[PlayerUIAbilities] Test pulse icona abilità {i}");
                }
            }
        }
        else
        {
            Debug.LogWarning("[PlayerUIAbilities] Nessuna icona abilità trovata");
        }
    }

    [ContextMenu("🌟 Debug - Show Abilities Info")]
    public void DebugShowAbilitiesInfo()
    {
        Debug.Log($"=== PlayerUIAbilities Debug Info ===\n" +
                  $"Ability Icons: {(abilityIcons != null ? abilityIcons.Length.ToString() : "❌")}\n" +
                  $"Use Gamepad: {useGamepad}");

        if (abilityIcons != null)
        {
            for (int i = 0; i < abilityIcons.Length; i++)
            {
                Debug.Log($"Icon {i}: {(abilityIcons[i] != null ? "✅" : "❌")}");
            }
        }
    }

    [ContextMenu("🔄 Debug - Update Input Layout")]
    public void DebugUpdateInputLayout()
    {
        UpdateInputLayout();
        Debug.Log($"[PlayerUIAbilities] Input layout aggiornato - Gamepad: {useGamepad}");
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}