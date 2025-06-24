using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

public class PlayerAttack : MonoBehaviour
{
    public GameObject attackHitbox;
    private Animator animator;

    private PlayerControls controls;
    private bool attackInput;

    private int ignoreFrames = 0;

    private void Awake()
    {
        controls = new PlayerControls();

        // Collegamento all'input Attack, con controllo UI
        controls.Gameplay.Attack.performed += ctx =>
        {
            // Ignora il click se il puntatore è sopra la UI
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            attackInput = true;
        };
    }

    private void OnEnable()
    {
        controls.Gameplay.Enable();
    }

    private void OnDisable()
    {
        controls.Gameplay.Disable();
    }

    private void Start()
    {
        animator = GetComponentInChildren<Animator>();
        DeactivateHitbox();
    }

    private void Update()
    {
        if (ignoreFrames > 0)
        {
            ignoreFrames--;
            return;
        }

        // Blocca input se il gioco è in pausa
        if (Time.timeScale == 0f)
        {
            return;
        }

        // Failsafe extra: ignora se il cursore è sulla UI
        if (attackInput)
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                attackInput = false;
                return;
            }

            animator.SetTrigger("Attack");
            attackInput = false;
        }
    }

    public void IgnoreNextClick()
    {
        ignoreFrames = 2;
    }

    public void ActivateHitbox()
    {
        attackHitbox.SetActive(true);
        Debug.Log("Hitbox ATTIVA");
    }

    public void DeactivateHitbox()
    {
        attackHitbox.SetActive(false);
        Debug.Log("Hitbox DISATTIVATA");
    }
}