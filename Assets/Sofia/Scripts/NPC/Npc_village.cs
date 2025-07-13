using UnityEngine;
using UnityEngine.AI;
using System.Collections;

[RequireComponent(typeof(NavMeshAgent))]
public class Npc_village : MonoBehaviour
{
    [Header("Waypoints iniziali (prima del dialogo)")]
    public Transform[] initialWaypoints;

    [Header("Effetto polvere ai piedi")]
    [SerializeField] private CFXR_EffectController footDustEffect;

    [Header("Effetto slow (una sola volta)")]
    [SerializeField] private CFXR_EffectController slowEffect;

    [Header("Durata effetto slow in secondi")]
    [SerializeField] private float slowEffectDuration = 0.5f;

    [Header("Distanza di stop vicino al player (regolabile in inspector)")]
    public float stopDistanceFromPlayer = 6.5f;

    [Header("Animator (sul padre)")]
    [SerializeField] private Animator animator;

    [Header("Riferimento Player")]
    public Transform playerTransform;

    private int currentIndex = -1;
    private NavMeshAgent agent;
    private Quaternion targetRotation;

    private bool footDustActive = false;
    private bool isSlowed = false;
    private bool slowEffectPlayed = false;
    private bool movingToPlayerAfterSlow = false;
    private bool dialogFinished = false;

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

        if (initialWaypoints.Length > 0)
        {
            GoToRandomWaypoint(initialWaypoints);
            animator.SetBool("IsWalking", true);
            agent.isStopped = false;
        }

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

            LookAtPlayer();

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
            LookAtPlayer();
            return;
        }

        // Aggiorna rotazione solo se non fermo e non rallentato
        if (agent.isStopped == false && agent.velocity.sqrMagnitude > 0.1f)
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

    void LookAtPlayer()
    {
        if (playerTransform != null)
        {
            Vector3 dir = playerTransform.position - transform.position;
            dir.y = 0;
            if (dir.sqrMagnitude > 0.01f)
                targetRotation = Quaternion.LookRotation(dir);
        }
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

        movingToPlayerAfterSlow = false;
        animator.SetBool("Slow", false);

        // Se il dialogo è finito, solo stop e idle, niente routine post-dialogo
        if (dialogFinished)
        {
            StopAndIdle();
        }
    }

    /// <summary>
    /// Chiamato dal Dialogo per indicare che è finito
    /// </summary>
    public void OnDialogFinished()
    {
        if (!dialogFinished)
        {
            dialogFinished = true;

            // Ferma l'agente e imposta idle, senza avviare routine post-dialogo
            StopAndIdle();
        }
    }

    private void StopAndIdle()
    {
        agent.isStopped = true;
        animator.SetBool("IsWalking", false);
        animator.SetBool("IsRunning", false);
        animator.SetBool("Slow", false);

        if (footDustActive && footDustEffect != null)
        {
            footDustEffect.StopEffect();
            footDustActive = false;
        }
    }

    void GoToRandomWaypoint(Transform[] waypoints)
    {
        if (waypoints.Length <= 1)
        {
            currentIndex = 0;
            agent.SetDestination(waypoints[0].position);
            return;
        }

        int newIndex;
        do
        {
            newIndex = Random.Range(0, waypoints.Length);
        } while (newIndex == currentIndex);

        currentIndex = newIndex;
        agent.SetDestination(waypoints[currentIndex].position);
    }
}
