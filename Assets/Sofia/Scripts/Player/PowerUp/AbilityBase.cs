using UnityEngine;

public abstract class AbilityBase : MonoBehaviour
{
    [Header("Ability Settings")]
    public float duration = 5f;

    // Proprietà virtuale per il costo di energia (power cost)
    public virtual int powerCost => 20;

    public PlayerPowerUp powerUpScript;

    public bool IsActive { get; protected set; } = false;

    // Override this in derived classes to disable auto-deactivation
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
