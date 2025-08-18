using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

public class TurtleShell : MonoBehaviour
{
    [Header("Movement & Patrol")]
    public NavMeshAgent agent;
    public float waitTimeAtPoint = 3f;
    public float rotateTime = 2f;
    public float walkSpeed = 4f;
    public float runSpeed = 6f;
    public Transform[] waypoints;
    [Header("Slowdown Custom Duration")]
[Tooltip("Durata personalizzata per lo slowdown (0 = usa durata default dell'abilità)")]
public float customSlowdownDuration = 15f;

    [Header("Vision & Attack")]
    public float viewRadius = 10f;
    public float viewAngle = 90f;
    public float attackRange = 2f;
    public LayerMask playerMask;
    public LayerMask obstacleMask;
    public float pushForce = 60f;

    [Header("Slowdown Settings")]
    public bool isSlow = false;
    public float slowFactor = 0.5f;

    [Header("Attack Settings")]
    public float attackDamage = 10f;
    public float slowedAttackDamage = 5f;
    public float attackCooldown = 1.5f;

    [Header("Health")]
    public float maxHealth = 100f;
    public float currentHealth;

    [Header("VFX")]
    public Renderer Renderer;
    public CFXR_EffectController deathEffectController;
    [SerializeField] private Material patinaMaterial;
    [SerializeField] private CFXR_EffectController slowdownEffect;
    public SlowdownAbility activeSlowdownAbility;


    private bool isDead = false;
    private bool isStunned = false;
    public bool hasBeenHitWhileSlow = false;

    private int currentWaypoint = 0;
    private float waitTimer;
    private Transform player;
    private bool playerVisible = false;
    private bool isPatrolling = true;
    private bool isAttacking = false;
    private float attackTimer = 0f;
    private Animator animator;

    private bool patinaActive = false;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
    }

    private void Start()
    {
        waitTimer = waitTimeAtPoint;
        if (waypoints != null && waypoints.Length > 0)
            agent.SetDestination(waypoints[currentWaypoint].position);
        agent.speed = walkSpeed;

        currentHealth = maxHealth;

        if (deathEffectController != null)
            deathEffectController.StopEffect();

        if (slowdownEffect != null)
            slowdownEffect.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (isDead || isStunned)
        {
            agent.isStopped = true;
            return;
        }

        attackTimer -= Time.deltaTime;
        UpdatePlayerVisibility();

        if (playerVisible)
        {
            isPatrolling = false;
            ChasePlayer();
        }
        else
        {
            if (!isPatrolling)
                ResetToPatrol();
            Patrol();
        }
    }

    private void UpdatePlayerVisibility()
    {
        playerVisible = false;
        Collider[] hits = Physics.OverlapSphere(transform.position, viewRadius, playerMask);
        foreach (var hit in hits)
        {
            Vector3 dir = (hit.transform.position - transform.position).normalized;
            if (Vector3.Angle(transform.forward, dir) < viewAngle / 2f)
            {
                float dist = Vector3.Distance(transform.position, hit.transform.position);
                if (!Physics.Raycast(transform.position, dir, dist, obstacleMask))
                {
                    player = hit.transform;
                    playerVisible = true;
                    return;
                }
            }
        }
        player = null;
    }

    private void ChasePlayer()
    {
        if (isStunned || player == null) return;

        RotateTowards(player.position);
        float dist = Vector3.Distance(transform.position, player.position);

        if (dist <= attackRange)
        {
            if (isSlow)
            {
                InflictPushToPlayer();
                agent.isStopped = false;
                agent.speed = walkSpeed * slowFactor;
                agent.SetDestination(player.position);

                if (isAttacking)
                {
                    isAttacking = false;
                    animator.SetBool("isAttacking", false);
                }

                return;
            }

            if (attackTimer <= 0f)
            {
                if (!isAttacking)
                {
                    isAttacking = true;
                    animator.SetBool("isAttacking", true);
                }

                animator.SetTrigger("Attack");
                agent.isStopped = true;
                attackTimer = attackCooldown;
            }

            return;
        }

        if (isAttacking)
        {
            isAttacking = false;
            animator.SetBool("isAttacking", false);
        }

        agent.isStopped = false;
        agent.speed = isSlow ? walkSpeed * slowFactor : runSpeed;
        agent.SetDestination(player.position);
    }

    public void EnemyAttackHitbox()
    {
        if (isStunned || player == null) return;

        HurtBox playerHurtBox = player.GetComponentInChildren<HurtBox>();
        if (playerHurtBox != null)
        {
            Vector3 pushDir = (player.position - transform.position).normalized;
            float damageToApply = isSlow ? 0f : slowedAttackDamage;
            playerHurtBox.OnHit(pushDir, pushForce, damageToApply);
        }
        else
        {
            Debug.LogWarning("Player hurtbox non trovata!");
        }
    }

    private void InflictPushToPlayer()
    {
        if (player == null) return;

        HurtBox playerHurtBox = player.GetComponentInChildren<HurtBox>();
        if (playerHurtBox != null)
        {
            Vector3 pushDir = (player.position - transform.position).normalized;
            playerHurtBox.OnHit(pushDir, pushForce, 0f);
        }
    }

    private void Patrol()
    {
        if (!agent.isOnNavMesh || isStunned) return;

        agent.speed = walkSpeed;

        if (!agent.hasPath || agent.remainingDistance < agent.stoppingDistance + 0.1f)
        {
            if (waitTimer <= 0f)
            {
                GoToNextWaypoint();
                waitTimer = waitTimeAtPoint;
            }
            else
            {
                agent.isStopped = true;
                waitTimer -= Time.deltaTime;
            }
        }
        else
        {
            agent.isStopped = false;
        }
    }

    private void GoToNextWaypoint()
    {
        if (waypoints == null || waypoints.Length == 0) return;
        currentWaypoint = (currentWaypoint + 1) % waypoints.Length;
        agent.SetDestination(waypoints[currentWaypoint].position);
    }

    private void ResetToPatrol()
    {
        isPatrolling = true;
        isAttacking = false;
        waitTimer = waitTimeAtPoint;
        animator.SetBool("isAttacking", false);
        agent.isStopped = false;
        agent.speed = walkSpeed;
        FindClosestWaypoint();
    }

    private void FindClosestWaypoint()
    {
        if (waypoints == null || waypoints.Length == 0) return;
        float minDist = float.MaxValue;
        for (int i = 0; i < waypoints.Length; i++)
        {
            float dist = Vector3.Distance(transform.position, waypoints[i].position);
            if (dist < minDist)
            {
                minDist = dist;
                currentWaypoint = i;
            }
        }
        agent.SetDestination(waypoints[currentWaypoint].position);
    }

    public void SetSlow(bool slow)
    {
        Debug.Log($"SetSlow chiamato con valore: {slow}");
        isSlow = slow;
        agent.speed = slow ? walkSpeed * slowFactor : walkSpeed;
        animator.SetBool("isSlow", slow);

        if (slow)
        {
            SetOverlayActive(true);
            PlaySlowdownEffect(1f);
        }
        else
        {
            SetOverlayActive(false);
        }

        if (!slow)
            hasBeenHitWhileSlow = false;
    }

    public void StartBlinkingOverlay(float duration)
    {
        if (Renderer == null || patinaMaterial == null) return;
        StartCoroutine(BlinkOverlay(duration));
    }

    private IEnumerator BlinkOverlay(float duration)
    {
        float elapsed = 0f;
        float blinkRate = 0.2f;
        bool state = true;

        while (elapsed < duration)
        {
            SetOverlayActive(state);
            state = !state;
            yield return new WaitForSeconds(blinkRate);
            elapsed += blinkRate;
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

    public void PlaySlowdownEffect(float duration = 1f)
    {
        if (slowdownEffect == null) return;

        StartCoroutine(PlayEffectRoutine(duration));
    }

    private IEnumerator PlayEffectRoutine(float duration)
    {
        slowdownEffect.gameObject.SetActive(true);
        slowdownEffect.PlayEffect();

        yield return new WaitForSeconds(duration);

        slowdownEffect.StopEffect();
        slowdownEffect.gameObject.SetActive(false);
    }

    public void TakeDamage(float damage)
{
    if (isDead) return;

    if (isSlow)
    {
        if (!hasBeenHitWhileSlow)
        {
            hasBeenHitWhileSlow = true;
            animator.SetTrigger("GetHitReal");

            SetSlow(false);
            agent.isStopped = true;
            isStunned = true;

            // 👉 Disattiva l'effetto slowdown globale
            if (activeSlowdownAbility != null && activeSlowdownAbility.IsActive)
            {
                activeSlowdownAbility.Deactivate();
            }
        }
        return;
    }

    currentHealth -= damage;
    animator.SetTrigger("GetHit");

    if (currentHealth <= 0f)
    {
        currentHealth = 0f;
        isDead = true;
        StartCoroutine(HandleDeath());
    }
}


    private IEnumerator HandleDeath()
    {
        agent.isStopped = true;
        yield return null;
    }

    public void TriggerDie()
    {
        if (isDead) return;
        isDead = true;
        isStunned = true;
        agent.isStopped = true;
        animator.SetTrigger("Die");
    }

    public void OnDeathAnimationFinished()
    {
        if (deathEffectController != null)
            deathEffectController.PlayEffect();

        if (Renderer != null)
            Renderer.enabled = false;

        StartCoroutine(DelayedDestroy());
    }

    private IEnumerator DelayedDestroy()
    {
        if (deathEffectController != null)
        {
            ParticleSystem ps = deathEffectController.GetComponent<ParticleSystem>();
            if (ps != null)
                yield return new WaitUntil(() => !ps.isPlaying);
        }
        else
        {
            yield return new WaitForSeconds(1.5f);
        }

        GemManager.Instance?.SpawnLifeGem(transform.position);
        Destroy(gameObject);
    }

    private void RotateTowards(Vector3 target)
    {
        Vector3 dir = (target - transform.position).normalized;
        dir.y = 0;
        if (dir == Vector3.zero) return;
        Quaternion look = Quaternion.LookRotation(dir);
        transform.rotation = Quaternion.Slerp(transform.rotation, look, Time.deltaTime * 5f);
    }
}
