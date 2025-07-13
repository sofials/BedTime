using UnityEngine;
using UnityEngine.AI;
using System.Collections;

[RequireComponent(typeof(NavMeshAgent))]
public class Npc_village : MonoBehaviour
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
        agent.updateRotation = false;
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
            animator.SetBool("IsRunning", false);
            animator.SetBool("IsWalking", false);

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

            return;
        }

        if (movingToPlayerAfterSlow)
        {
            if (playerTransform != null)
            {
                Vector3 dir = playerTransform.position - transform.position;
                dir.y = 0;
                if (dir.sqrMagnitude > 0.01f)
                    targetRotation = Quaternion.LookRotation(dir);
            }
            return;
        }

        if (playerTransform != null)
        {
            float dist = Vector3.Distance(transform.position, playerTransform.position);

            if (dist <= 600f)
            {
                animator.SetBool("IsRunning", true);
                animator.SetBool("Slow", false);
                animator.SetBool("IsWalking", false);

                if (footDustEffect != null && !footDustActive)
                {
                    footDustEffect.PlayEffect();
                    footDustActive = true;
                }
            }
            else
            {
                animator.SetBool("IsRunning", false);

                if (footDustActive && footDustEffect != null)
                {
                    footDustEffect.StopEffect();
                    footDustActive = false;
                }
            }
        }

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
        if (slowEffect != null)
            slowEffect.StopEffect();

        movingToPlayerAfterSlow = true;
        animator.SetBool("Slow", false);
        StartCoroutine(MoveTowardsPlayerThenStop(3.5f));
    }

    public void MoveNearPlayer(float stopDistance = 3.5f)
    {
        StartCoroutine(MoveTowardsPlayerThenStop(stopDistance));
    }

    private IEnumerator MoveTowardsPlayerThenStop(float stopDistance)
    {
        if (playerTransform == null)
            yield break;

        animator.SetBool("IsWalking", true);
        agent.isStopped = false;
        agent.stoppingDistance = stopDistance;

        while (true)
        {
            Vector3 direction = (playerTransform.position - transform.position).normalized;
            Vector3 targetPos = playerTransform.position - direction * stopDistance;

            float distToTarget = Vector3.Distance(transform.position, targetPos);

            if (distToTarget <= 0.1f)
                break;

            agent.SetDestination(targetPos);
            yield return null;
        }

        agent.isStopped = true;
        animator.SetBool("IsWalking", false);
        movingToPlayerAfterSlow = false;

        agent.stoppingDistance = 0.3f;
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
