using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine.Splines;
using System;

[RequireComponent(typeof(NavMeshAgent))]
public class Npc_village : MonoBehaviour
{
    [Header("Waypoints")]
    public Transform[] waypoints;

    [Header("Animator")]
    [SerializeField] private Animator animator;

    [Header("Spline per posizionamento")]
    [SerializeField] private SplineContainer splineContainer; // La spline su cui posizionarsi

    [Header("Slowdown Effect")]
    [SerializeField] private CFXR_EffectController slowdownEffect; // Effetto quando viene fermato dal power-up

    [Header("Debug")]
    [SerializeField] private bool debugMode = true;

    private int currentIndex = -1;
    private NavMeshAgent agent;
    private bool isStopped = false;

    // Lista statica per tenere traccia degli NPC fermati
    private static List<Npc_village> stoppedNPCs = new List<Npc_village>();
    
    // Spline condivisa (viene impostata dal primo NPC)
    private static SplineContainer sharedSpline;
    
    // EVENTO STATICO per comunicare il primo slowdown
    public static event Action OnFirstSlowdownUsed;
    
    // Flag statico per tracciare se il slowdown è già stato usato
    private static bool slowdownAlreadyUsed = false;

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
        }

        // Imposta la spline condivisa se non è già stata impostata
        if (sharedSpline == null && splineContainer != null)
        {
            sharedSpline = splineContainer;
        }

        if (debugMode) Debug.Log($"NPC {gameObject.name}: Inizializzato e pronto.");
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
    /// Chiamato da SlowdownAbility per fermare l'NPC definitivamente
    /// </summary>
    public void StopNPC()
    {
        if (debugMode) Debug.Log($"StopNPC chiamato su: {gameObject.name}");
        
        // CONTROLLO: Se il slowdown è già stato usato, ferma comunque questo NPC
        // ma non scatenare di nuovo l'evento globale
        if (slowdownAlreadyUsed)
        {
            if (debugMode) Debug.Log($"Slowdown già usato in precedenza. NPC {gameObject.name} viene fermato senza evento globale.");
            StopThisNPCOnly();
            return;
        }
        
        // PRIMA VOLTA: Attiva l'evento globale
        if (!slowdownAlreadyUsed)
        {
            slowdownAlreadyUsed = true;
            if (debugMode) Debug.Log("PRIMO SLOWDOWN USATO! Attivo evento globale...");
            OnFirstSlowdownUsed?.Invoke();
        }

        StopThisNPCOnly();
    }

    /// <summary>
    /// Ferma solo questo specifico NPC senza scatenare eventi globali
    /// </summary>
    private void StopThisNPCOnly()
    {
        if (isStopped)
        {
            if (debugMode) Debug.Log($"NPC {gameObject.name} già fermo, ignoro.");
            return;
        }

        isStopped = true;
        agent.isStopped = true;

        // Aggiungi questo NPC alla lista statica
        if (!stoppedNPCs.Contains(this))
        {
            stoppedNPCs.Add(this);
            if (debugMode) Debug.Log($"NPC {gameObject.name} aggiunto alla lista. Totale NPC fermati: {stoppedNPCs.Count}");
        }

        // Avvia l'effetto sprint breve e poi posizionamento istantaneo
        StartCoroutine(SprintEffectThenTeleport());
    }

    /// <summary>
    /// Mostra l'effetto slowdown e poi teleporta
    /// </summary>
    System.Collections.IEnumerator SprintEffectThenTeleport()
    {
        if (sharedSpline == null)
        {
            Debug.LogWarning($"Spline non assegnata per NPC {gameObject.name}!");
            
            // Fallback: ferma l'NPC dove si trova
            agent.ResetPath();
            agent.isStopped = true;
            
            // Attiva comunque l'effetto slowdown se disponibile
            if (slowdownEffect != null)
            {
                slowdownEffect.PlayEffect();
                if (debugMode) Debug.Log($"NPC {gameObject.name}: Effetto slowdown attivato (fallback)");
            }
            
            animator.SetBool("IsRunning", false);
            animator.SetBool("Slow", true);
            animator.SetBool("isIdle", true);
            yield break;
        }

        // Calcola dove deve andare
        Vector3 targetPosition = CalculateSplinePosition();
        
        if (debugMode) Debug.Log($"NPC {gameObject.name}: Inizio effetto slowdown e teleport verso {targetPosition}");
        
        // ATTIVA L'EFFETTO SLOWDOWN
        if (slowdownEffect != null)
        {
            slowdownEffect.PlayEffect();
            if (debugMode) Debug.Log($"NPC {gameObject.name}: Effetto slowdown attivato");
        }
        else
        {
            if (debugMode) Debug.LogWarning($"NPC {gameObject.name}: Nessun slowdown effect assegnato!");
        }
        
        // BREVE PAUSA PER MOSTRARE L'EFFETTO
        yield return new WaitForSeconds(0.2f);
        
        // TELEPORT ISTANTANEO
        if (debugMode) Debug.Log($"NPC {gameObject.name}: Teleport a {targetPosition}");
        
        // Ferma l'effetto slowdown se ancora attivo
        if (slowdownEffect != null)
        {
            slowdownEffect.StopEffect();
        }
        
        // Teleporta istantaneamente alla posizione corretta
        transform.position = targetPosition;
        
        // Ferma completamente l'agent
        agent.ResetPath();
        agent.isStopped = true;
        
        // Imposta animazione idle
        animator.SetBool("IsRunning", false);
        animator.SetBool("Slow", true);
        animator.SetBool("isIdle", true);
        
        // Guarda il player
        LookAtPlayer();
        
        if (debugMode) Debug.Log($"NPC {gameObject.name}: Posizionamento completato");
    }

    /// <summary>
    /// Calcola la posizione dell'NPC sulla spline (un NPC per knot)
    /// </summary>
    Vector3 CalculateSplinePosition()
    {
        int myIndex = stoppedNPCs.IndexOf(this);
        
        // Ottieni il numero di knots nella spline
        int knotCount = sharedSpline.Spline.Count;
        
        if (knotCount == 0)
        {
            Debug.LogWarning($"Spline {sharedSpline.name} non ha knots!");
            return transform.position; // Rimani dove sei
        }
        
        // Se ci sono più NPC che knots, alcuni condivideranno la posizione
        int targetKnotIndex = myIndex % knotCount;
        
        // Ottieni la posizione del knot specifico
        BezierKnot knot = sharedSpline.Spline[targetKnotIndex];
        float3 knotPosition = knot.Position;
        
        // Converti la posizione locale della spline in posizione world
        Vector3 worldPosition = sharedSpline.transform.TransformPoint(new Vector3(knotPosition.x, knotPosition.y, knotPosition.z));
        
        if (debugMode) Debug.Log($"NPC {gameObject.name}: Posizione calcolata - Index: {myIndex}, Knot: {targetKnotIndex}, Pos: {worldPosition}");
        
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
                if (debugMode) Debug.Log($"NPC {gameObject.name}: Guardando verso il player");
            }
        }
        else
        {
            Debug.LogWarning($"Player non trovato per NPC {gameObject.name}");
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
            if (debugMode) Debug.Log($"NPC {gameObject.name}: Rimosso dalla lista al destroy");
        }
    }

    /// <summary>
    /// Riavvia il movimento dell'NPC (per testing o restart level)
    /// </summary>
    public void RestartMovement()
    {
        if (debugMode) Debug.Log($"NPC {gameObject.name}: Riavvio movimento");
        
        isStopped = false;
        agent.isStopped = false;
        
        // Rimuovi dalla lista degli NPC fermati
        if (stoppedNPCs.Contains(this))
        {
            stoppedNPCs.Remove(this);
        }
        
        // Ripristina animazione di corsa
        animator.SetBool("IsRunning", true);
        animator.SetBool("Slow", false);
        animator.SetBool("isIdle", false);
        
        // Riprendi il movimento verso waypoint
        if (waypoints.Length > 0)
        {
            GoToRandomWaypoint();
        }
    }

    /// <summary>
    /// Metodo per resettare il sistema (utile per testing)
    /// </summary>
    [ContextMenu("Reset Slowdown System")]
    public static void ResetSlowdownSystem()
    {
        slowdownAlreadyUsed = false;
        
        // Riavvia tutti gli NPC fermati
        for (int i = stoppedNPCs.Count - 1; i >= 0; i--)
        {
            if (stoppedNPCs[i] != null)
            {
                stoppedNPCs[i].RestartMovement();
            }
        }
        
        stoppedNPCs.Clear();
        Debug.Log("Sistema slowdown resettato e tutti gli NPC riavviati!");
    }

    /// <summary>
    /// Metodo per testare manualmente lo stop dell'NPC
    /// </summary>
    [ContextMenu("Test Stop This NPC")]
    public void TestStopThisNPC()
    {
        StopNPC();
    }

    /// <summary>
    /// Metodo per testare manualmente il restart dell'NPC
    /// </summary>
    [ContextMenu("Test Restart This NPC")]
    public void TestRestartThisNPC()
    {
        RestartMovement();
    }

    // Proprietà per debug e controllo esterno
    public bool IsStopped => isStopped;
    public static int TotalStoppedNPCs => stoppedNPCs.Count;
    public static bool SlowdownWasUsed => slowdownAlreadyUsed;
}