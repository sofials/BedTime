using UnityEngine;
using UnityEngine.AI;
using System.Collections;

[RequireComponent(typeof(NavMeshAgent))]
public class Npc_verde : MonoBehaviour
{
    [Header("Waypoints (random)")]
    public Transform[] waypoints;

    [Header("Effetti visivi")]
    [SerializeField] private CFXR_EffectController footDustEffect;
    [SerializeField] private CFXR_EffectController slowEffect;

    [Header("Config")]
    [SerializeField] private float slowEffectDuration = 0.5f;
    [SerializeField] private Animator animator;
    [SerializeField] private Transform playerTransform;
    [SerializeField] private Dialog_verde dialogController;

    private NavMeshAgent agent;
    private int currentIndex = -1;
    private Quaternion targetRotation;

    private bool isSlowed = false;
    private bool slowEffectPlayed = false;
    private bool movingToPlayerAfterSlow = false;
    private bool dialogStarted = false;
    private bool dialogActive = false;
    private bool hasUsedSlow = false;
    private bool ignoreRunLogic = false;
    private bool footDustActive = false;

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

        if (waypoints.Length > 0)
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
            if (player != null) playerTransform = player.transform;
        }

        if (ignoreRunLogic)
        {
            ForzaIdle();
            return;
        }

        if (dialogActive)
        {
            ForzaIdle();
            return;
        }

        if (isSlowed && !movingToPlayerAfterSlow)
        {
            ForzaIdle();
            animator.SetBool("Slow", true);
            LookAtPlayer();

            if (!slowEffectPlayed && slowEffect != null)
            {
                slowEffect.PlayEffect();
                slowEffectPlayed = true;
                StartCoroutine(StopSlowEffectAfterDelay());
            }

            return;
        }

        if (movingToPlayerAfterSlow)
        {
            LookAtPlayer();
            return;
        }

        if (!hasUsedSlow && playerTransform != null)
        {
            float dist = Vector3.Distance(transform.position, playerTransform.position);

            if (dist <= 600f)
            {
                animator.SetBool("IsRunning", true);
                animator.SetBool("Slow", false);
                animator.SetBool("IsWalking", false);

                if (!footDustActive && footDustEffect != null)
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

        if (agent.enabled && !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance && (!agent.hasPath || agent.velocity.sqrMagnitude < 0.1f))
            GoToRandomWaypoint();

        if (agent.velocity.sqrMagnitude > 0.1f)
        {
            Vector3 dir = agent.velocity.normalized;
            dir.y = 0;
            targetRotation = Quaternion.LookRotation(dir);
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
            dialogStarted = false;
            dialogActive = false;
            hasUsedSlow = true;
        }
    }

    private IEnumerator StopSlowEffectAfterDelay()
    {
        yield return new WaitForSeconds(slowEffectDuration);
        if (slowEffect != null) slowEffect.StopEffect();

        movingToPlayerAfterSlow = true;
        animator.SetBool("Slow", false);
        StartCoroutine(MoveTowardsPlayerThenStop());
    }

    private IEnumerator MoveTowardsPlayerThenStop()
    {
        if (playerTransform == null) yield break;

        animator.SetBool("IsWalking", true);
        agent.isStopped = false;
        agent.stoppingDistance = 3.5f;

        while (true)
        {
            Vector3 direction = (playerTransform.position - transform.position).normalized;
            Vector3 target = playerTransform.position - direction * agent.stoppingDistance;

            if (Vector3.Distance(transform.position, target) <= 0.1f)
                break;

            agent.SetDestination(target);
            yield return null;
        }

        agent.isStopped = true;
        animator.SetBool("IsWalking", false);
        movingToPlayerAfterSlow = false;
        agent.stoppingDistance = 0.3f;

        if (!dialogStarted)
        {
            dialogStarted = true;
            StartDialogue();
        }
    }

    private void StartDialogue()
    {
        dialogActive = true;
        if (dialogController != null)
            StartCoroutine(DialogCoroutine());
        else
            Debug.LogWarning($"{gameObject.name} - dialogController non assegnato");
    }

    private IEnumerator DialogCoroutine()
    {
        yield return StartCoroutine(dialogController.PlayEntireDialog());

        dialogActive = false;
        ignoreRunLogic = true;
        ForzaIdle();
        StopAllAudioSources();
    }

    private void ForzaIdle()
    {
        animator.SetBool("IsRunning", false);
        animator.SetBool("IsWalking", false);
        animator.SetBool("Slow", false);

        if (footDustEffect != null && footDustActive)
        {
            footDustEffect.StopEffect();
            footDustActive = false;
        }

        agent.isStopped = true;
    }

    public void OnDialogFinished()
    {
        ignoreRunLogic = true;
        dialogActive = false;
        ForzaIdle();
    }

    private void StopAllAudioSources()
    {
        foreach (var audio in GetComponentsInChildren<AudioSource>())
        {
            if (audio.isPlaying)
                audio.Stop();
        }
    }

    private void GoToRandomWaypoint()
    {
        if (waypoints.Length == 0) return;

        int newIndex;
        do
        {
            newIndex = Random.Range(0, waypoints.Length);
        } while (waypoints.Length > 1 && newIndex == currentIndex);

        currentIndex = newIndex;
        SetDestinationWithOffset(waypoints[currentIndex].position);
    }

    private void SetDestinationWithOffset(Vector3 basePos)
    {
        Vector2 offset2D = Random.insideUnitCircle * 0.5f;
        Vector3 finalPos = basePos + new Vector3(offset2D.x, 0, offset2D.y);
        agent.SetDestination(finalPos);
    }

    private void LookAtPlayer()
    {
        if (playerTransform != null)
        {
            Vector3 dir = playerTransform.position - transform.position;
            dir.y = 0;
            if (dir.sqrMagnitude > 0.01f)
                targetRotation = Quaternion.LookRotation(dir);
        }
    }
}
