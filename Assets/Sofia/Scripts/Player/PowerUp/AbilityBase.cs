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

    [Header("Audio")]
    public AudioClip activationSound;
    protected AudioSource audioSource;

    public AudioClip failureSound;  // nuovo

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
        return !IsActive && powerUpScript != null && powerUpScript.HasEnoughPower(powerCost);
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
        Debug.Log("Impossibile attivare l'abilità: energia insufficiente o già attiva.");
        
        if (failureSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(failureSound);
        }
    }
}

    public abstract void Activate();
    public abstract void Deactivate();
}
