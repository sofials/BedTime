using UnityEngine;
using UnityEngine.AI;

public class GolemVillaggio : MonoBehaviour
{
    public Transform player;
    public NavMeshAgent agent;
    public Animator animator;

    [Header("Walk Settings")]
    public float walkSpeed = 6f; // camminata veloce
    private bool isChasing = false;

    private void Start()
    {
        if (agent != null)
        {
            agent.speed = walkSpeed;
        }
    }

    private void Update()
    {
        if (!isChasing || player == null) return;

        Vector3 dir = (player.position - transform.position).normalized;
        dir.y = 0;

        if (dir != Vector3.zero)
            transform.rotation = Quaternion.LookRotation(dir);

        MoveTowardsPlayer();
    }

    private void MoveTowardsPlayer()
    {
        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.speed = walkSpeed;
            agent.SetDestination(player.position);
        }

        if (animator != null)
            animator.SetBool("isWalking", true);
    }

    public void ActivateChase()
    {
        isChasing = true;
    }
}
