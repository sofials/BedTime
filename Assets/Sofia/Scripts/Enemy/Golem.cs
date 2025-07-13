using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

public class Golem : MonoBehaviour
{
    public Transform player;
    public NavMeshAgent agent;

    [Header("Vision Settings")]
    public float viewRadius = 300f;
    public float viewAngle = 360f;
    public LayerMask playerMask;
    public LayerMask obstacleMask;

    [Header("Attack Settings")]
    public float meleeRange = 20f;
    public float rangedRange = 280f;
    public float meleeCooldown = 2f;
    public float rangedCooldown = 3f;

    private float meleeTimer = 0f;
    private float rangedTimer = 0f;

    [Header("Attack Effects")]
    public float damage = 20f;
    public float pushForce = 5f;

    [Header("Projectile Settings")]
    public GameObject projectilePrefab;
    public Transform projectileSpawnPoint;

    [Header("Animation")]
    public Animator animator;

    [Header("Stats")]
    public float maxHealth = 30f;
    private float currentHealth;
    private bool isDead = false;

    private bool playerVisible = false;

    private bool isMeleeAttacking = false;
    private bool isRangedAttacking = false;

    [Header("Slowdown")]
    public bool isSlow = false;
    public float slowFactor = 0.5f;
    public float animationSlowFactor = 0.3f;
    public SlowdownAbility activeSlowdownAbility;

    [Header("FX & Patina")]
    public Renderer Renderer;
    [SerializeField] private Material patinaMaterial;
    [SerializeField] private CFXR_EffectController slowdownEffect;

    private bool patinaActive = false;
    private Coroutine fxCoroutine;
    private Coroutine slowCoroutine;

    private Coroutine blinkCoroutine;
    [SerializeField] private float blinkDurationBeforeEnd = 2f; // Durata blinking prima che lo slow finisca

    private int deadLayer;

    private void Start()
    {
        currentHealth = maxHealth;
        deadLayer = LayerMask.NameToLayer("DeadEnemy");

        if (slowdownEffect != null)
            slowdownEffect.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (isDead || player == null) return;

        meleeTimer -= Time.deltaTime;
        rangedTimer -= Time.deltaTime;

        UpdatePlayerVisibility();

        float dist = Vector3.Distance(transform.position, player.position);

        Vector3 dir = (player.position - transform.position).normalized;
        dir.y = 0;
        if (dir != Vector3.zero)
            transform.rotation = Quaternion.LookRotation(dir);

        if (!playerVisible)
        {
            MoveTowardsPlayer();
            return;
        }

        if (isMeleeAttacking || isRangedAttacking)
            return;

        if (dist <= meleeRange && meleeTimer <= 0f)
            DoMeleeAttack();
        else if (dist <= rangedRange && rangedTimer <= 0f)
            DoRangedAttack();
        else
            MoveTowardsPlayer();
    }

    private void DoMeleeAttack()
    {
        StopAndFacePlayer();
        isMeleeAttacking = true;
        meleeTimer = meleeCooldown;
        animator.SetTrigger("AttackMelee");
    }

    private void DoRangedAttack()
    {
        StopAndFacePlayer();
        isRangedAttacking = true;
        rangedTimer = rangedCooldown;
        animator.SetTrigger("AttackRanged");
    }

    private void StopAndFacePlayer()
    {
        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
            agent.isStopped = true;

        animator.SetBool("isWalking", false);
    }

    private void MoveTowardsPlayer()
    {
        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.speed = isSlow ? 3f * slowFactor : 3f;
            agent.SetDestination(player.position);
        }

        animator.SetBool("isWalking", true);
    }

    private void UpdatePlayerVisibility()
    {
        playerVisible = false;
        Collider[] hits = Physics.OverlapSphere(transform.position, viewRadius, playerMask);
        foreach (var hit in hits)
        {
            Vector3 dirToPlayer = (hit.transform.position - transform.position).normalized;
            if (Vector3.Angle(transform.forward, dirToPlayer) < viewAngle / 2f)
            {
                float distanceToPlayer = Vector3.Distance(transform.position, hit.transform.position);
                if (!Physics.Raycast(transform.position, dirToPlayer, distanceToPlayer, obstacleMask))
                {
                    playerVisible = true;
                    return;
                }
            }
        }
    }

    public void EnemyAttackHitbox()
    {
        if (isDead) return;

        Collider[] hits = Physics.OverlapBox(
            transform.position + transform.forward * (meleeRange * 0.5f),
            new Vector3(10f, 10f, 10f),
            transform.rotation,
            LayerMask.GetMask("PlayerHurtbox")
        );

        foreach (var hit in hits)
        {
            if (hit.gameObject.CompareTag("PlayerHurtbox"))
            {
                var hurtbox = hit.GetComponent<HurtBox>();
                if (hurtbox != null)
                {
                    Vector3 pushDir = (hurtbox.transform.position - transform.position).normalized;
                    hurtbox.OnHit(pushDir, pushForce, damage);
                }
            }
        }

        isMeleeAttacking = false;
    }

    public void SpawnProjectile()
    {
        if (isDead || projectilePrefab == null || projectileSpawnPoint == null) return;

        GameObject proj = Instantiate(projectilePrefab, projectileSpawnPoint.position, Quaternion.identity);
        Golem_Projectile projectile = proj.GetComponent<Golem_Projectile>();

        if (projectile != null)
            projectile.Initialize(player.position);
    }

    public void EndRangedAttack()
    {
        isRangedAttacking = false;
    }

    public void TakeDamage(float amount)
    {
        if (isDead) return;

        currentHealth -= amount;
        Debug.Log($"Golem ha subito {amount} danni. Vita rimanente: {currentHealth}");

        isMeleeAttacking = false;
        isRangedAttacking = false;

        if (currentHealth <= 0)
            Die();
        else
            animator.SetTrigger("Hit");
    }

    public void StartSlow(float duration, SlowdownAbility sourceAbility)
    {
        if (isSlow)
        {
            if (slowCoroutine != null)
                StopCoroutine(slowCoroutine);
        }
        else
        {
            SetSlow(true);
        }

        activeSlowdownAbility = sourceAbility;
        slowCoroutine = StartCoroutine(SlowDurationRoutine(duration));
    }

    private IEnumerator SlowDurationRoutine(float duration)
    {
        float normalDuration = duration - blinkDurationBeforeEnd;

        if (normalDuration > 0)
            yield return new WaitForSeconds(normalDuration);

        // Avvia blinking negli ultimi secondi
        if (blinkCoroutine != null)
            StopCoroutine(blinkCoroutine);
        blinkCoroutine = StartCoroutine(BlinkOverlayWhileSlow());

        yield return new WaitForSeconds(blinkDurationBeforeEnd);

        SetSlow(false);
        activeSlowdownAbility = null;
        slowCoroutine = null;

        if (blinkCoroutine != null)
        {
            StopCoroutine(blinkCoroutine);
            blinkCoroutine = null;
        }
    }

    private bool slowdownEffectPlayedThisCycle = false;

    public void SetSlow(bool value)
    {
        if (value == isSlow)
            return;

        isSlow = value;

        if (isSlow)
        {
            SetOverlayActive(true);

            if (!slowdownEffectPlayedThisCycle && slowdownEffect != null)
            {
                if (fxCoroutine != null)
                    StopCoroutine(fxCoroutine);
                fxCoroutine = StartCoroutine(PlayEffectOnce());
                slowdownEffectPlayedThisCycle = true;
            }

            if (agent != null)
                agent.speed = 3f * slowFactor;

            animator.speed = animationSlowFactor;  // animazioni più lente
        }
        else
        {
            SetOverlayActive(false);

            if (slowdownEffect != null)
            {
                slowdownEffect.StopEffect();
                slowdownEffect.gameObject.SetActive(false);
            }

            if (agent != null)
                agent.speed = 3f;

            animator.speed = 1f;  // velocità animazioni normale

            slowdownEffectPlayedThisCycle = false;
        }
    }

    private IEnumerator PlayEffectOnce()
    {
        slowdownEffect.gameObject.SetActive(true);
        slowdownEffect.PlayEffect();

        yield return null; // aspetta un frame

        slowdownEffect.StopEffect();
        slowdownEffect.gameObject.SetActive(false);

        fxCoroutine = null;
    }

    private IEnumerator BlinkOverlayWhileSlow()
    {
        if (Renderer == null || patinaMaterial == null) yield break;

        bool state = true;
        float blinkRate = 0.2f;

        while (isSlow)
        {
            SetOverlayActive(state);
            state = !state;
            yield return new WaitForSeconds(blinkRate);
        }

        SetOverlayActive(false);
    }

    public void SetOverlayActive(bool active)
    {
        if (Renderer == null || patinaMaterial == null) return;

        var materials = new List<Material>(Renderer.sharedMaterials);

        if (active && !patinaActive)
        {
            if (!materials.Contains(patinaMaterial))
            {
                materials.Add(patinaMaterial);
                Renderer.materials = materials.ToArray();
                patinaActive = true;
            }
        }
        else if (!active && patinaActive)
        {
            materials.Remove(patinaMaterial);
            Renderer.materials = materials.ToArray();
            patinaActive = false;
        }
    }

    public void EndHit()
    {
        isMeleeAttacking = false;
        isRangedAttacking = false;
    }

    private void Die()
    {
        isDead = true;
        animator.SetTrigger("Die");

        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
            agent.isStopped = true;

        Collider[] colliders = GetComponentsInChildren<Collider>();
        foreach (var col in colliders)
        {
            if (col.CompareTag("GolemHurtbox"))
            {
                col.isTrigger = false;
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, meleeRange);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, rangedRange);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, viewRadius);

        Vector3 viewAngleA = DirFromAngle(-viewAngle / 2);
        Vector3 viewAngleB = DirFromAngle(viewAngle / 2);
        Gizmos.color = Color.white;
        Gizmos.DrawLine(transform.position, transform.position + viewAngleA * viewRadius);
        Gizmos.DrawLine(transform.position, transform.position + viewAngleB * viewRadius);
    }

    private Vector3 DirFromAngle(float angleDegrees)
    {
        angleDegrees += transform.eulerAngles.y;
        return new Vector3(Mathf.Sin(angleDegrees * Mathf.Deg2Rad), 0, Mathf.Cos(angleDegrees * Mathf.Deg2Rad));
    }
}
