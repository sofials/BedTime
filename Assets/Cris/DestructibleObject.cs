using UnityEngine;

public class DestructibleObject : MonoBehaviour
{
    [Header("Health Settings")]
    public float maxHealth = 50f;
    private float currentHealth;
    
    [Header("Physics Settings")]
    public float forceMultiplier = 5f;
    public float upwardForce = 2f;
    public bool enablePhysicsOnHit = true;
    
    [Header("Damage Detection")]
    public float damageAmount = 25f;
    
    public CFXR_EffectController deathEffect;
    
    private Rigidbody rb;
    private Collider physicsCollider;
    private Collider triggerCollider;
    private bool wasKinematic;
    private int lastAttackId = -1;

    private void Start()
    {
        currentHealth = maxHealth;
        SetupComponents();
        
        if (deathEffect != null)
            deathEffect.StopEffect();
    }
    
    private void SetupComponents()
    {
        // Setup Rigidbody
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }
        wasKinematic = rb.isKinematic;
        
        // IMPORTANTE: Se l'oggetto deve essere solido fin dall'inizio, disabilita kinematic
        if (rb.isKinematic)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            // Congela la posizione finché non viene colpito
            rb.constraints = RigidbodyConstraints.FreezeAll;
        }
        
        // Setup Colliders
        Collider[] colliders = GetComponents<Collider>();
        
        if (colliders.Length == 0)
        {
            // Crea entrambi i collider se non esistono
            physicsCollider = gameObject.AddComponent<BoxCollider>();
            physicsCollider.isTrigger = false;
            
            triggerCollider = gameObject.AddComponent<BoxCollider>();
            triggerCollider.isTrigger = true;
        }
        else
        {
            // Trova o crea i collider necessari
            foreach (var col in colliders)
            {
                if (col.isTrigger && triggerCollider == null)
                    triggerCollider = col;
                else if (!col.isTrigger && physicsCollider == null)
                    physicsCollider = col;
            }
            
            // Crea quelli mancanti
            if (physicsCollider == null)
            {
                physicsCollider = gameObject.AddComponent<BoxCollider>();
                physicsCollider.isTrigger = false;
            }
            
            if (triggerCollider == null)
            {
                triggerCollider = gameObject.AddComponent<BoxCollider>();
                triggerCollider.isTrigger = true;
                // Rendi il trigger leggermente più grande per detection migliore
                if (triggerCollider is BoxCollider triggerBox && physicsCollider is BoxCollider physicsBox)
                {
                    triggerBox.size = physicsBox.size * 1.1f;
                }
            }
        }
    }

    #region Damage Detection via Trigger (compatibile con PlayerAttack)
    
    private void OnTriggerEnter(Collider other)
    {
        TryApplyDamage(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryApplyDamage(other);
    }

    private void TryApplyDamage(Collider other)
    {
        // Controlla se è l'hitbox del player (stesso sistema del tuo HurtBox_Enemy)
        if (other.gameObject.layer == LayerMask.NameToLayer("PlayerAttackHitbox") &&
            other.CompareTag("PlayerAttackHitbox"))
        {
            var playerAttack = other.GetComponentInParent<PlayerAttack>();
            if (playerAttack != null && playerAttack.isAttacking)
            {
                // Evita danni multipli dallo stesso attacco
                if (playerAttack.AttackId == lastAttackId)
                    return;

                lastAttackId = playerAttack.AttackId;

                // Calcola punto e direzione del colpo
                Vector3 hitPoint = other.ClosestPoint(transform.position);
                Vector3 hitDirection = (transform.position - playerAttack.transform.position).normalized;

                // Applica danno
                TakeDamage(damageAmount, hitPoint, hitDirection);

                // Notifica al PlayerAttack che il colpo è andato a segno
                playerAttack.RegisterSuccessfulHit();

                Debug.Log($"DestructibleObject colpito da PlayerAttack - Health: {currentHealth}/{maxHealth}");
            }
        }
    }
    
    #endregion

    #region Health and Damage

    public void TakeDamage(float amount)
    {
        TakeDamage(amount, Vector3.zero, Vector3.zero);
    }

    public void TakeDamage(float amount, Vector3 hitPoint, Vector3 hitDirection)
    {
        currentHealth -= amount;
        
        // Attiva la fisica quando viene colpito
        if (enablePhysicsOnHit && rb != null)
        {
            ActivatePhysics(hitPoint, hitDirection);
        }
        
        if (currentHealth <= 0f)
            Die();
    }
    
    #endregion
    
    #region Physics
    
    private void ActivatePhysics(Vector3 hitPoint, Vector3 hitDirection)
    {
        // Sblocca completamente il rigidbody quando viene colpito
        if (rb.constraints != RigidbodyConstraints.None)
        {
            rb.constraints = RigidbodyConstraints.None;
        }
        
        if (rb.isKinematic)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
        }
        
        // Applica forza se abbiamo informazioni sul colpo
        if (hitDirection != Vector3.zero)
        {
            Vector3 force = hitDirection.normalized * forceMultiplier;
            force.y += upwardForce;
            
            // BOOST EXTRA per più drammaticità
            force *= 2f; // Raddoppia la forza!
            
            if (hitPoint != Vector3.zero)
            {
                rb.AddForceAtPosition(force, hitPoint, ForceMode.Impulse);
            }
            else
            {
                rb.AddForce(force, ForceMode.Impulse);
            }
            
            // Forza aggiuntiva verso l'alto per effetto più spettacolare
            rb.AddForce(Vector3.up * upwardForce * 1.5f, ForceMode.Impulse);
        }
        else
        {
            // Forza casuale MOLTO più forte
            Vector3 randomForce = new Vector3(
                Random.Range(-2f, 2f),    // Range più ampio
                Random.Range(1f, 3f),     // Più forza verso l'alto
                Random.Range(-2f, 2f)
            ) * forceMultiplier * 1.5f;   // Moltiplicatore extra
            
            rb.AddForce(randomForce, ForceMode.Impulse);
        }
        
        // Aggiungi rotazione più drammatica
        Vector3 torque = new Vector3(
            Random.Range(-3f, 3f),    // Rotazione più intensa
            Random.Range(-3f, 3f),
            Random.Range(-3f, 3f)
        ) * forceMultiplier * 1.5f;   // Torque più forte
        
        rb.AddTorque(torque, ForceMode.Impulse);
    }

    public void EnablePhysics()
    {
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.constraints = RigidbodyConstraints.None;
        }
    }
    
    public void DisablePhysics()
    {
        if (rb != null)
        {
            rb.isKinematic = wasKinematic;
            rb.useGravity = false;
            if (wasKinematic)
                rb.constraints = RigidbodyConstraints.FreezeAll;
        }
    }
    
    #endregion

    #region Death and Destruction

    private void Die()
    {
        if (deathEffect != null)
            deathEffect.PlayEffect();

        var renderer = GetComponentInChildren<Renderer>();
        if (renderer != null)
            renderer.enabled = false;

        // Mantieni la fisica anche dopo la morte
        if (rb != null && !rb.isKinematic)
        {
            rb.mass *= 0.5f; // Rendi più leggero per un effetto migliore
        }
        
        // Disabilita il trigger per evitare ulteriori danni
        if (triggerCollider != null)
            triggerCollider.enabled = false;

        Destroy(gameObject, 2f);
    }

    #endregion

    #region Public Methods

    public float GetHealthPercentage()
    {
        return currentHealth / maxHealth;
    }

    public bool IsAlive()
    {
        return currentHealth > 0f;
    }

    public void Heal(float amount)
    {
        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
    }

    public void SetHealth(float health)
    {
        currentHealth = Mathf.Clamp(health, 0f, maxHealth);
        if (currentHealth <= 0f)
            Die();
    }

    #endregion
}