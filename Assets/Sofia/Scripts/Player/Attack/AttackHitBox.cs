using UnityEngine;
using System.Collections.Generic;

public class AttackHitbox : MonoBehaviour
{
    [Tooltip("Danno inflitto ai nemici colpiti")]
    public int damage = 25;

    private HashSet<Collider> alreadyHit = new(); // Evita colpi multipli

    private void OnEnable()
    {
        alreadyHit.Clear(); // Pulisce ogni volta che viene riattivata
    }

    private void OnTriggerEnter(Collider other)
    {
        if (alreadyHit.Contains(other)) return;

        // Cerca un Enemy nello stesso oggetto o nei suoi genitori (es. armature o collider secondari)
        Enemy enemy = other.GetComponentInParent<Enemy>();
        if (enemy != null)
        {
            Vector3 direction = (enemy.transform.position - transform.position).normalized;
            enemy.TakeDamage(damage, direction);
            alreadyHit.Add(other);
            Debug.Log($"✅ Nemico colpito: {enemy.name}");
        }
    }
}
