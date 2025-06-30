using UnityEngine;

public abstract class AbilityBase : MonoBehaviour
{
    [Header("Ability Settings")]
    public float duration = 5f;
    public virtual int powerCost => 20;

    public PlayerPowerUp powerUpScript;

    [Header("UI Effect")]
    public int effectIconIndex;

    public bool IsActive { get; protected set; } = false;

    protected virtual bool HasFixedDuration => false;

    public virtual bool CanActivate()
    {
        return !IsActive && powerUpScript != null && powerUpScript.HasEnoughPower(powerCost);
    }

    public virtual void TryActivate()
    {
        if (CanActivate())
        {
            Activate();
            IsActive = true;

            // Mostra effetto sull’icona UI
            if (PlayerUI.Instance != null)
                PlayerUI.Instance.PulseIconAt(effectIconIndex);

            if (HasFixedDuration)
                Invoke(nameof(Deactivate), duration);
        }
        else
        {
            Debug.Log("Impossibile attivare l'abilità.");
        }
    }

    public abstract void Activate();
    public abstract void Deactivate();
}
