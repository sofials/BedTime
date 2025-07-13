using UnityEngine;
using UnityEngine.AI;
using System.Collections;

[RequireComponent(typeof(NavMeshAgent))]
public class Npc_verde : MonoBehaviour
{
    [Header("Waypoints (random)")]
    public Transform[] waypoints;

    [Header("Effetto polvere ai piedi")]
    [SerializeField] private CFXR_EffectController footDustEffect;

    [Header("Effetto slow (una sola volta)")]
    [SerializeField] private CFXR_EffectController slowEffect;

    [Header("Durata effetto slow in secondi")]
    [SerializeField] private float slowEffectDuration = 0.5f;

    [Header("Animator (sul padre)")]
    [SerializeField] private Animator animator;

    [Header("Riferimento Player")]
    public Transform playerTransform;

    private int currentIndex = -1;
    private NavMeshAgent agent;

    private bool footDustActive = false;
    private bool isSlowed = false;
    private bool slowEffectPlayed = false;
    private bool movingToPlayerAfterSlow = false;

    private Quaternion targetRotation;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.updateRotation = false;  // rotazione manuale
        agent.stoppingDistance = 0.3f;

        if (animator == null)
            animator = GetComponent<Animator>();
        animator.applyRootMotion = false;

        agent.speed = 15f;
        agent.angularSpeed = 120f;
        agent.acceleration = 8f;
        agent.avoidancePriority = Random.Range(10, 90);

        if (waypoints.Length == 0)
        {
            Debug.LogWarning("Nessun waypoint assegnato a " + gameObject.name);
            enabled = false;
            return;
        }

        GoToRandomWaypoint();

        if (footDustEffect != null)
        {
            footDustEffect.PlayEffect();
            footDustActive = true;
        }
    }

    void Update()
{
    if (playerTransform == null)
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            playerTransform = player.transform;
    }

    if (isSlowed && !movingToPlayerAfterSlow)
    {
        agent.isStopped = true;
        animator.SetBool("Slow", true);

        if (footDustActive)
        {
            footDustEffect.StopEffect();
            footDustActive = false;
        }

        if (playerTransform != null)
        {
            Vector3 dir = playerTransform.position - transform.position;
            dir.y = 0;
            if (dir.sqrMagnitude > 0.01f)
                targetRotation = Quaternion.LookRotation(dir);
        }

        if (slowEffect != null && !slowEffectPlayed)
        {
            slowEffect.PlayEffect();
            slowEffectPlayed = true;
            StartCoroutine(StopSlowEffectAfterDelay());
        }

        // Non return più qui per permettere check successivi
    }

    if (movingToPlayerAfterSlow)
    {
        // continua a muoverti verso player (gestito dalla coroutine)
        if (playerTransform != null)
        {
            Vector3 dir = playerTransform.position - transform.position;
            dir.y = 0;
            if (dir.sqrMagnitude > 0.01f)
                targetRotation = Quaternion.LookRotation(dir);
        }
        return; // blocco il resto perché sto muovendo verso player
    }

    // Movimento normale tra waypoint
    if (agent.enabled && !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
    {
        if (!agent.hasPath || agent.velocity.sqrMagnitude < 0.1f)
            GoToRandomWaypoint();
    }

    if (agent.velocity.sqrMagnitude > 0.1f)
    {
        Vector3 direction = agent.velocity.normalized;
        direction.y = 0;
        targetRotation = Quaternion.LookRotation(direction);

        animator.SetBool("IsWalking", true);

        if (!footDustActive && footDustEffect != null)
        {
            footDustEffect.PlayEffect();
            footDustActive = true;
        }
    }
    else
    {
        animator.SetBool("IsWalking", false);

        if (footDustActive && footDustEffect != null)
        {
            footDustEffect.StopEffect();
            footDustActive = false;
        }
    }
}

    void LateUpdate()
    {
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 5f);
    }

    public void SetSlow(bool slow)
    {
        if (slow && !isSlowed)
        {
            isSlowed = true;
            slowEffectPlayed = false;
            movingToPlayerAfterSlow = false;
        }
    }

    private IEnumerator StopSlowEffectAfterDelay()
    {
        yield return new WaitForSeconds(slowEffectDuration);

        slowEffect.StopEffect();

        // Parto con movimento verso player
        movingToPlayerAfterSlow = true;
        animator.SetBool("Slow", false);
        StartCoroutine(MoveTowardsPlayerThenStop());
    }

    private IEnumerator MoveTowardsPlayerThenStop()
    {
        if (playerTransform == null)
            yield break;

        animator.SetBool("IsWalking", true);
        agent.isStopped = false;

        while (Vector3.Distance(transform.position, playerTransform.position) > 1.5f)
        {
            agent.SetDestination(playerTransform.position);
            yield return null;
        }

        agent.isStopped = true;
        animator.SetBool("IsWalking", false);

        movingToPlayerAfterSlow = false;

        // Qui puoi mettere azioni successive come dialoghi
    }

    void GoToRandomWaypoint()
    {
        if (waypoints.Length <= 1)
        {
            SetDestinationWithOffset(waypoints[0].position);
            return;
        }

        int newIndex;
        do
        {
            newIndex = Random.Range(0, waypoints.Length);
        } while (newIndex == currentIndex);

        currentIndex = newIndex;
        SetDestinationWithOffset(waypoints[currentIndex].position);
    }

    void SetDestinationWithOffset(Vector3 basePosition)
    {
        Vector2 randomCircle = Random.insideUnitCircle * 0.5f;
        Vector3 offsetPosition = basePosition + new Vector3(randomCircle.x, 0, randomCircle.y);
        agent.SetDestination(offsetPosition);
    }
}
