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

    [Header("Animator")]
    [SerializeField] private Animator animator;

    [Header("Riferimento Player")]
    public Transform playerTransform;

    private int currentIndex = -1;
    private NavMeshAgent agent;

    private bool footDustActive = false;
    private bool isSlowed = false;
    private bool slowEffectPlayed = false;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();

        if (animator == null)
            animator = GetComponent<Animator>();

        agent.speed = 15f;
        agent.angularSpeed = 120f;
        agent.acceleration = 8f;
        agent.avoidancePriority = Random.Range(10, 90);
        agent.stoppingDistance = 0.3f;

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
        if (isSlowed)
        {
            if (agent.enabled)
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
                if (dir.sqrMagnitude > 0.001f)
                {
                    transform.rotation = Quaternion.LookRotation(dir);
                }
            }

            if (slowEffect != null && !slowEffectPlayed)
            {
                slowEffect.PlayEffect();
                slowEffectPlayed = true;
                StartCoroutine(StopSlowEffectAfterDelay());
            }

            return;
        }
    }

    public void SetSlow(bool slow)
    {
        if (slow && !isSlowed)
        {
            isSlowed = true;
        }
    }

    private IEnumerator StopSlowEffectAfterDelay()
    {
        yield return new WaitForSeconds(slowEffectDuration);
        slowEffect.StopEffect();
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
