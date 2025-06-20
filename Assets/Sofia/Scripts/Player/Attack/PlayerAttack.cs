using UnityEngine;
using UnityEngine.InputSystem;

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

        // Collegamento all'input Attack
        controls.Gameplay.Attack.performed += ctx => attackInput = true;
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

        if (Time.timeScale == 0f)
        {
            return;
        }

        // Nuovo sistema di input
        if (attackInput)
        {
            animator.SetTrigger("Attack");
            attackInput = false;  // reset per il prossimo frame
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
