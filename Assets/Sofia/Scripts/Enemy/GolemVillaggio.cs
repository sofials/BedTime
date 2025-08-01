using UnityEngine;
using UnityEngine.AI;

public class GolemVillaggio : MonoBehaviour
{
    public NavMeshAgent agent;
    public Animator animator;

    [Header("Walk Settings")]
    public float walkSpeed = 6f;
    public Transform destinationWaypoint;

    private bool isWalking = false;
    private bool hasArrived = false;

    private void Start()
    {
        if (agent != null)
        {
            agent.speed = walkSpeed;
            agent.isStopped = true;
        }
    }

    private void Update()
    {
        if (isWalking && agent != null && destinationWaypoint != null && !hasArrived)
        {
            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
            {
                ArrivedAtDestination();
            }
        }
    }

    public void ActivateWalk()
    {
        if (destinationWaypoint == null || agent == null) return;

        agent.isStopped = false;
        agent.SetDestination(destinationWaypoint.position);
        isWalking = true;

        if (animator != null)
            animator.SetBool("isWalking", true);
    }

    private void ArrivedAtDestination()
    {
        if (hasArrived) return; // previene doppie chiamate

        hasArrived = true;

        if (animator != null)
            animator.SetBool("isWalking", false);

        // Distrugge il golem completamente
        Destroy(gameObject);
    }
}
