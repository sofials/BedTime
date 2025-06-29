using UnityEngine;

public class AttackHitBox : MonoBehaviour
{
    [SerializeField] private Collider hitboxCollider;

    void Awake()
    {
        if (hitboxCollider == null)
            hitboxCollider = GetComponent<Collider>();

        if (hitboxCollider != null)
            hitboxCollider.enabled = false;
    }

    public void EnableHitbox()
    {
        if (hitboxCollider != null)
        {
            hitboxCollider.enabled = true;
            Debug.Log("Hitbox abilitata");
        }
    }

    public void DisableHitbox()
    {
        if (hitboxCollider != null)
        {
            hitboxCollider.enabled = false;
            Debug.Log("Hitbox disabilitata");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("Hitbox triggerata con: " + other.name);

        Enemy enemy = other.GetComponent<Enemy>();
        if (enemy != null)
        {
            Debug.Log("Nemico colpito direttamente. Applico danno.");
            enemy.TakeDamage(25f);

            // Setta il trigger Dizzy sull'animator del nemico
            Animator enemyAnim = enemy.GetComponentInChildren<Animator>();
            if (enemyAnim != null)
            {
                enemyAnim.SetTrigger("Dizzy");
                Debug.Log("Trigger Dizzy settato sull'animator del nemico!");
            }
        }
        else
        {
            Debug.Log("Collider non è un Enemy.");
        }
    }
}
