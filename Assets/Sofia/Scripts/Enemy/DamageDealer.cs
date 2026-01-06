using UnityEngine;

public class DamageDealer : MonoBehaviour
{
    [Header("Damage Settings")]
    public float damage = 10f;
    public float pushForce = 3f;
    
    [Header("Optional Settings")]
    [Tooltip("Se true, l'oggetto si distrugge dopo aver fatto danno")]
    public bool destroyOnHit = false;
    
    [Header("Cooldown")]
    [Tooltip("Tempo minimo tra un danno e l'altro")]
    public float damageCooldown = 0.5f;
    
    private float lastDamageTime = -999f;
    
    private void OnTriggerEnter(Collider other)
    {
        // Cooldown check
        if (Time.time < lastDamageTime + damageCooldown) return;
        
        // Verifica se è il player
        if (other.GetComponent<ThirdPersonController>() != null || 
            other.GetComponentInParent<ThirdPersonController>() != null)
        {
            lastDamageTime = Time.time;
            
            if (destroyOnHit)
            {
                Destroy(gameObject);
            }
            
            Debug.Log($"[DamageDealer] {gameObject.name} ha colpito il player!");
        }
    }
}