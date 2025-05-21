using UnityEngine;

public class AttackHitbox : MonoBehaviour
{
    public int damage = 25;

    private void OnTriggerEnter(Collider other)
    {
        EnemyAI enemy = other.GetComponent<EnemyAI>();
        if (enemy != null)
        {
            enemy.TakeDamage(damage);
        }
    }
}
