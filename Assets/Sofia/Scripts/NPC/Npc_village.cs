using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine.Splines;

[RequireComponent(typeof(NavMeshAgent))]
public class Npc_village : MonoBehaviour
{
    [Header("Waypoints")]
    public Transform[] waypoints;

    [Header("Animator")]
    [SerializeField] private Animator animator;

    [Header("Effetto particelle")]
    [SerializeField] private CFXR_EffectController effectController;

    [Header("Spline per posizionamento")]
    [SerializeField] private SplineContainer splineContainer; // La spline su cui posizionarsi

    private int currentIndex = -1;
    private NavMeshAgent agent;
    private bool isStopped = false;

    // Lista statica per tenere traccia degli NPC fermati
    private static List<Npc_village> stoppedNPCs = new List<Npc_village>();
    
    // Spline condivisa (viene impostata dal primo NPC)
    private static SplineContainer sharedSpline;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.updateRotation = false;
        agent.stoppingDistance = 0.3f;

        if (animator == null)
            animator = GetComponent<Animator>();
        
        animator.applyRootMotion = false;

        // Impostazioni movimento
        agent.speed = 15f;
        agent.angularSpeed = 120f;
        agent.acceleration = 8f;
        agent.avoidancePriority = UnityEngine.Random.Range(10, 90); // Evitamento tra NPC

        // Inizia il movimento tra waypoint (animator inizia già in running come default)
        if (waypoints.Length > 0)
        {
            GoToRandomWaypoint();
            // Non serve impostare IsRunning=true perché è già il default
        }

        // Attiva l'effetto all'avvio
        if (effectController != null)
        {
            effectController.PlayEffect();
        }

        // Imposta la spline condivisa se non è già stata impostata
        if (sharedSpline == null && splineContainer != null)
        {
            sharedSpline = splineContainer;
        }
    }

    void Update()
    {
        // Se è fermo, non fare nulla
        if (isStopped) return;

        // Se ha raggiunto il waypoint, vai al prossimo
        if (!agent.pathPending && agent.remainingDistance < 0.5f)
        {
            GoToRandomWaypoint();
        }

        // Aggiorna rotazione durante il movimento
        if (agent.velocity.sqrMagnitude > 0.1f)
        {
            Vector3 direction = agent.velocity.normalized;
            direction.y = 0;
            transform.rotation = Quaternion.LookRotation(direction);
        }
    }

    /// <summary>
    /// Chiamato quando si usa il power-up per fermare l'NPC definitivamente
    /// </summary>
    public void StopNPC()
    {
        isStopped = true;
        agent.isStopped = true;
        
        // Animazioni corrette: IsRunning=false + Slow=true = Idle
        animator.SetBool("IsRunning", false);
        animator.SetBool("Slow", true);

        // Aggiungi questo NPC alla lista statica
        if (!stoppedNPCs.Contains(this))
        {
            stoppedNPCs.Add(this);
        }

        // Posiziona lungo la spline
        StartCoroutine(MoveToSplinePosition());

        // Spegne l'effetto definitivamente
        if (effectController != null)
        {
            effectController.StopEffect();
        }
    }

    /// <summary>
    /// Muove l'NPC verso la sua posizione sulla spline
    /// </summary>
    System.Collections.IEnumerator MoveToSplinePosition()
    {
        if (sharedSpline == null)
        {
            Debug.LogWarning("Spline non assegnata!");
            yield break;
        }

        // Calcola la posizione sulla spline
        Vector3 targetPosition = CalculateSplinePosition();
        
        // Aumenta velocità per movimento veloce
        float originalSpeed = agent.speed;
        agent.speed = 50f; // Velocità molto alta per posizionamento rapidissimo
        
        // Riattiva temporaneamente l'agent per il movimento
        agent.isStopped = false;
        agent.SetDestination(targetPosition);

        // Aspetta che raggiunga la posizione
        while (agent.pathPending || agent.remainingDistance > 1f)
        {
            // Durante il movimento, orienta verso la direzione di movimento
            if (agent.velocity.sqrMagnitude > 0.1f)
            {
                Vector3 direction = agent.velocity.normalized;
                direction.y = 0;
                transform.rotation = Quaternion.LookRotation(direction);
            }
            yield return null;
        }

        // Ripristina velocità originale e ferma definitivamente
        agent.speed = originalSpeed;
        agent.isStopped = true;
        LookAtPlayer(); // Guarda il player quando è fermo
    }

    /// <summary>
    /// Calcola la posizione dell'NPC sulla spline (un NPC per knot)
    /// </summary>
    Vector3 CalculateSplinePosition()
    {
        int myIndex = stoppedNPCs.IndexOf(this);
        
        // Ottieni il numero di knots nella spline
        int knotCount = sharedSpline.Spline.Count;
        
        // Se ci sono più NPC che knots, alcuni condivideranno la posizione
        int targetKnotIndex = myIndex % knotCount;
        
        // Ottieni la posizione del knot specifico
        BezierKnot knot = sharedSpline.Spline[targetKnotIndex];
        float3 knotPosition = knot.Position;
        
        // Converti la posizione locale della spline in posizione world
        Vector3 worldPosition = sharedSpline.transform.TransformPoint(new Vector3(knotPosition.x, knotPosition.y, knotPosition.z));
        
        return worldPosition;
    }

    /// <summary>
    /// Ruota l'NPC verso il player quando è fermo ai knot
    /// </summary>
    void LookAtPlayer()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            Vector3 direction = player.transform.position - transform.position;
            direction.y = 0; // Mantieni solo rotazione orizzontale
            
            if (direction.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.LookRotation(direction);
            }
        }
    }

    void GoToRandomWaypoint()
    {
        if (waypoints.Length <= 1)
        {
            if (waypoints.Length == 1)
                agent.SetDestination(waypoints[0].position);
            return;
        }

        int newIndex;
        do
        {
            newIndex = UnityEngine.Random.Range(0, waypoints.Length);
        } while (newIndex == currentIndex);

        currentIndex = newIndex;
        agent.SetDestination(waypoints[currentIndex].position);
    }

    void OnDestroy()
    {
        // Rimuovi dalla lista statica quando l'oggetto viene distrutto
        if (stoppedNPCs.Contains(this))
        {
            stoppedNPCs.Remove(this);
        }
    }
}