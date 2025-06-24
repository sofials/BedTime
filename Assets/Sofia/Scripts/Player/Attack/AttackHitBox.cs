using UnityEngine;

public class AttackHitbox : MonoBehaviour
{
    public int damage = 25;

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("Trigger con: " + other.name); // 👈 stampa tutto ciò che tocca la hitbox

        EnemyAI enemy = other.GetComponent<EnemyAI>();
        if (enemy != null)
        {
            Debug.Log("Colpito nemico! Danno inflitto: " + damage);
            enemy.TakeDamage(damage);
        }
    }
}