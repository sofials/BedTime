using UnityEngine;
using UnityEngine.AI;

public class GolemVillaggio : MonoBehaviour
{
    public NavMeshAgent agent;
    public Animator animator;

    [Header("Walk Settings")]
    public float walkSpeed = 6f;
    public Transform destinationWaypoint;
    public float arrivalDistance = 2f; // Distanza per considerare arrivato al waypoint
    
    [Header("Rotation Settings")]
    public float rotationSpeed = 120f; // Velocità di rotazione in gradi/secondo

    private bool isWalking = false;
    private bool hasArrived = false;
    private bool isActivated = false;
    private bool isEnabled = false; // Nuovo flag per tracciare lo stato di enable

    private void Start()
    {
        if (agent != null)
        {
            agent.speed = walkSpeed;
            agent.isStopped = true;
            
            // IMPORTANTE: Assicurati che il NavMeshAgent possa ruotare
            agent.angularSpeed = rotationSpeed;
            agent.updateRotation = true; // Permetti al NavMeshAgent di gestire la rotazione
        }
    }

    private void Update()
    {
        if (isWalking && agent != null && destinationWaypoint != null && !hasArrived)
        {
            // Debug per vedere se il path è valido
            if (agent.hasPath)
            {
                Debug.DrawLine(transform.position, agent.destination, Color.red);
            }
            
            // Controlla la distanza diretta al waypoint per un arrivo più preciso
            float distanceToWaypoint = Vector3.Distance(transform.position, destinationWaypoint.position);
            
            if (distanceToWaypoint <= arrivalDistance)
            {
                ArrivedAtDestination();
            }
        }
    }

    /// <summary>
    /// Abilita il Golem e lo rende visibile, ma rimane in idle
    /// Chiamare questo quando inizia il dialogo
    /// </summary>
    public void ShowGolem()
    {
        // Abilita l'oggetto se è disabilitato
        if (!gameObject.activeInHierarchy)
            gameObject.SetActive(true);

        isEnabled = true;
        
        // Assicurati che sia fermo in idle
        if (agent != null)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }
        
        if (animator != null)
            animator.SetBool("isWalking", false);
            
        Debug.Log("Golem is now visible and waiting in idle state");
    }

    /// <summary>
    /// Attiva il movimento del Golem verso la destinazione
    /// Chiamare questo quando viene raggiunta l'ultima linea del dialogo
    /// </summary>
    public void StartWalking()
    {
        if (isActivated) return; // Previene attivazioni multiple
        
        // Auto-abilita se non è stato fatto prima
        if (!isEnabled)
        {
            Debug.Log("Golem wasn't shown yet, showing it first...");
            ShowGolem();
        }

        isActivated = true;
        
        // Debug per verificare la posizione del waypoint
        if (destinationWaypoint != null)
        {
            Debug.Log($"Golem position: {transform.position}, Waypoint position: {destinationWaypoint.position}");
            Debug.Log($"Distance to waypoint: {Vector3.Distance(transform.position, destinationWaypoint.position)}");
        }
        
        ActivateWalk();
    }

    /// <summary>
    /// Metodo pubblico per fermare il Golem se necessario
    /// </summary>
    public void StopGolem()
    {
        if (agent != null)
        {
            agent.isStopped = true;
            agent.ResetPath(); // Cancella il path corrente
        }

        isWalking = false;

        if (animator != null)
            animator.SetBool("isWalking", false);
    }

    /// <summary>
    /// Verifica se il Golem è visibile (mostrato ma potrebbe essere in idle o camminando)
    /// </summary>
    public bool IsVisible()
    {
        return isEnabled && gameObject.activeInHierarchy;
    }

    /// <summary>
    /// Verifica se il Golem sta camminando
    /// </summary>
    public bool IsWalking()
    {
        return isActivated && isWalking && !hasArrived;
    }

    private void ActivateWalk()
    {
        if (destinationWaypoint == null || agent == null) 
        {
            Debug.LogError("Waypoint o NavMeshAgent mancanti!");
            return;
        }

        // Verifica che la destinazione sia raggiungibile
        NavMeshPath path = new NavMeshPath();
        if (agent.CalculatePath(destinationWaypoint.position, path))
        {
            if (path.status == NavMeshPathStatus.PathComplete)
            {
                agent.isStopped = false;
                agent.SetDestination(destinationWaypoint.position);
                isWalking = true;

                if (animator != null)
                    animator.SetBool("isWalking", true);
                
                Debug.Log("Golem activated and walking to destination!");
            }
            else
            {
                Debug.LogError("Path to waypoint is not complete! Check NavMesh.");
            }
        }
        else
        {
            Debug.LogError("Cannot calculate path to waypoint!");
        }
    }

    private void ArrivedAtDestination()
    {
        if (hasArrived) return; // previene doppie chiamate

        hasArrived = true;
        Debug.Log("Golem arrived at destination!");

        if (animator != null)
            animator.SetBool("isWalking", false);

        // Distrugge il golem completamente
        Destroy(gameObject);
    }

    // Metodo per debug - visualizza informazioni utili nell'inspector
    private void OnDrawGizmos()
    {
        if (destinationWaypoint != null)
        {
            // Disegna una linea verso il waypoint
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(transform.position, destinationWaypoint.position);
            
            // Disegna una sfera al waypoint
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(destinationWaypoint.position, 1f);
        }

        if (agent != null && agent.hasPath)
        {
            // Disegna il path del NavMeshAgent
            Gizmos.color = Color.red;
            Vector3[] corners = agent.path.corners;
            for (int i = 0; i < corners.Length - 1; i++)
            {
                Gizmos.DrawLine(corners[i], corners[i + 1]);
            }
        }
    }
}