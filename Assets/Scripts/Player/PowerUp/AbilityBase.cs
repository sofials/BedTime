using UnityEngine;

public abstract class AbilityBase : MonoBehaviour
{
    [Header("Ability Settings")]
    [Tooltip("Se vuoto, l'abilità resta attiva finché non viene disattivata manualmente.")]
    public float? duration = null;

    public int powerCost = 20;
    public PlayerPowerUp powerUpScript;

    public bool IsActive { get; protected set; } = false;

    public virtual bool CanActivate()
    {
        return !IsActive && powerUpScript != null && powerUpScript.HasEnoughPower(powerCost);
    }

    public void TryActivate()
    {
        if (CanActivate())
        {
            Activate();
            IsActive = true;

            // Se la durata è assegnata, disattiva automaticamente dopo il tempo
            if (duration.HasValue && duration.Value > 0f)
            {
                Invoke(nameof(Deactivate), duration.Value);
            }
        }
        else
        {
            Debug.Log("Impossibile attivare l'abilità.");
        }
    }

    public abstract void Activate();
    public abstract void Deactivate();
}
