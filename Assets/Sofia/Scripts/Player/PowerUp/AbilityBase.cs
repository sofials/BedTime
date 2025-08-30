using UnityEngine;

public abstract class AbilityBase : MonoBehaviour
{
    [Header("Ability Settings")]
    public float duration = 5f;
    public virtual int powerCost => 20;

    public PlayerPowerUp powerUpScript;

    [Header("UI Effect")]
    public int effectIconIndex;

    [Header("Ability Control")]
    [SerializeField] private bool isEnabled = true; // Controllo base dell'abilità
    private bool levelAllowed = true; // Controllo per livello

    public bool IsActive { get; protected set; } = false;

    // Proprietà per verificare se l'abilità è abilitata
    public bool IsEnabled 
    { 
        get 
        { 
            return isEnabled && levelAllowed;
        }
    }

    protected virtual bool HasFixedDuration => false;

    [Header("Audio")]
    public AudioClip activationSound;
    protected AudioSource audioSource;

    public AudioClip failureSound;

    protected virtual void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
    }

    protected virtual void Update()
    {
        if (PlayerUI.Instance != null)
        {
            bool canUse = CanActivate();
            PlayerUI.Instance.UpdateAbilityIconState(effectIconIndex, canUse);
        }
    }

    public virtual bool CanActivate()
    {
        return IsEnabled && !IsActive && powerUpScript != null && powerUpScript.HasEnoughPower(powerCost);
    }

    public virtual void TryActivate()
    {
        if (CanActivate())
        {
            Activate();
            IsActive = true;

            if (activationSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(activationSound);
            }

            if (PlayerUI.Instance != null)
                PlayerUI.Instance.PulseIconAt(effectIconIndex);
        }
        else
        {
            string reason = GetDisableReason();
            Debug.Log($"Impossibile attivare l'abilità {gameObject.name}: {reason}");
            
             if (IsEnabled && failureSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(failureSound);
        }
        }
    }

    // METODI PER IL CONTROLLO DELL'ABILITÀ

    /// <summary>
    /// Imposta se l'abilità è abilitata o disabilitata
    /// </summary>
    public void SetAbilityEnabled(bool enabled)
    {
        isEnabled = enabled;
        Debug.Log($"Abilità {gameObject.name} {(enabled ? "abilitata" : "disabilitata")}");
        
        // Se l'abilità viene disabilitata mentre è attiva, la disattiva
        if (!enabled && IsActive)
        {
            Deactivate();
            IsActive = false;
        }
    }

    /// <summary>
    /// Controllo specifico per livello (chiamato dal LevelAbilityManager)
    /// </summary>
    public void SetLevelAllowed(bool allowed)
    {
        levelAllowed = allowed;
        Debug.Log($"Abilità {gameObject.name} {(allowed ? "permessa" : "vietata")} in questo livello");
        
        if (!allowed && IsActive)
        {
            Deactivate();
            IsActive = false;
        }
    }

    /// <summary>
    /// Forza l'attivazione dell'abilità (da script esterni)
    /// Bypassa tutti i controlli tranne IsEnabled
    /// </summary>
    public virtual bool ForceActivate()
    {
        if (!IsEnabled)
        {
            Debug.Log($"Impossibile forzare l'attivazione di {gameObject.name}: abilità disabilitata");
            return false;
        }

        if (IsActive)
        {
            Debug.Log($"Abilità {gameObject.name} già attiva");
            return false;
        }

        // Forza l'attivazione senza controlli di energia
        Activate();
        IsActive = true;

        if (activationSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(activationSound);
        }

        if (PlayerUI.Instance != null)
            PlayerUI.Instance.PulseIconAt(effectIconIndex);

        Debug.Log($"Abilità {gameObject.name} attivata forzatamente");
        return true;
    }

    /// <summary>
    /// Restituisce il motivo per cui l'abilità è disabilitata
    /// </summary>
    public string GetDisableReason()
    {
        if (!isEnabled) return "abilità disabilitata";
        if (!levelAllowed) return "non permessa in questo livello";
        if (IsActive) return "già attiva";
        if (powerUpScript == null) return "PowerUp script mancante";
        if (!powerUpScript.HasEnoughPower(powerCost)) return "energia insufficiente";
        
        return "motivo sconosciuto";
    }

    public abstract void Activate();
    public abstract void Deactivate();
}