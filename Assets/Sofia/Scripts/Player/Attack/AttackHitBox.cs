using UnityEngine;

public class AttackHitBox : MonoBehaviour
{
    private Collider hitboxCollider;

    void Awake()
    {
        hitboxCollider = GetComponent<Collider>();
        if (hitboxCollider != null)
            hitboxCollider.enabled = false; // Disattiva di default
    }

    // Da chiamare tramite Animation Event all'inizio del colpo
    public void EnableHitbox()
    {
        if (hitboxCollider != null)
            hitboxCollider.enabled = true;
    }

    // Da chiamare tramite Animation Event alla fine del colpo
    public void DisableHitbox()
    {
        if (hitboxCollider != null)
            hitboxCollider.enabled = false;
    }

    // Gestione collisione con la hurtbox del nemico
    private void OnTriggerEnter(Collider other)
    {
        EnemyHurtBox enemyHurtBox = other.GetComponent<EnemyHurtBox>();
        if (enemyHurtBox != null)
        {
            enemyHurtBox.OnHit(25f); // oppure passa un valore variabile di danno
            Debug.Log("Nemico colpito dalla hitbox del player!");
        }
    }
}