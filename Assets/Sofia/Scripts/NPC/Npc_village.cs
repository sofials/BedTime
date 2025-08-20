using UnityEngine;
using UnityEngine.AI;
using System.Collections;
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
    [SerializeField] private SplineContainer splineContainer;

    [Header("Slowdown Effect")]
    [SerializeField] private CFXR_EffectController slowdownEffect;

    [Header("Dialogo Post-Slowdown")]
    [SerializeField] private DialogueSystem dialogueSystem;
    [SerializeField] private float tempoCheckDialogo = 0.5f;

    [Header("Comportamento Post-Dialogo")]
    [SerializeField] private bool usaPuntoFisso = false;
    [SerializeField] private Transform puntoDestinazione;
    [SerializeField] private int regaliRichiestiPerLiberazione = 2;
    [SerializeField] private float tempoIdleMin = 2f;
    [SerializeField] private float tempoIdleMax = 5f;
    [SerializeField] private float tempoWalkMin = 3f;
    [SerializeField] private float tempoWalkMax = 8f;
    [SerializeField] private float velocitaPostDialogo = 5f;
    [SerializeField] private float raggioMovimento = 10f;
    [SerializeField] private LayerMask layerOstacoli = -1;
    [SerializeField] private float raggioControlloOstacoli = 1f;

    [Header("Oggetti da Disattivare")]
    [SerializeField] private GameObject[] oggettiDaDisattivare = new GameObject[2];
    [SerializeField] private bool disattivaOggettiAllaLiberazione = true;

    [Header("Debug")]
    [SerializeField] private bool debugMode = true;

    private int currentIndex = -1;
    private NavMeshAgent agent;
    private bool isStopped = false;
    private bool dialogoFinito = false;
    private bool staControllandoDialogo = false;
    private Vector3 posizioneSpawn;
    private bool inAttesaRegali = false;
    private CollectiblesManager collectiblesManager;
    private int regaliIniziali = 0;
    
    private enum StatoPostDialogo { Idle, Walking }
    private StatoPostDialogo statoAttuale = StatoPostDialogo.Idle;
    private Coroutine comportamentoRoutine;

    private static List<Npc_village> stoppedNPCs = new List<Npc_village>();
    private static SplineContainer sharedSpline;
    public static event Action OnFirstSlowdownUsed;
    private static bool slowdownAlreadyUsed = false;

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
        agent.avoidancePriority = UnityEngine.Random.Range(10, 90);

        DialogueSystem.OnAnyLastLineFinished += OnDialogueLastLineFinished;

        if (waypoints.Length > 0)
        {
            GoToRandomWaypoint();
        }

        if (sharedSpline == null && splineContainer != null)
        {
            sharedSpline = splineContainer;
        }

        if (debugMode) Debug.Log($"NPC {gameObject.name}: Inizializzato e pronto.");
    }

    void Update()
    {
        if (isStopped && !dialogoFinito) return;
        if (dialogoFinito) return;

        if (!agent.pathPending && agent.remainingDistance < 0.5f)
        {
            GoToRandomWaypoint();
        }

        if (agent.velocity.sqrMagnitude > 0.1f)
        {
            Vector3 direction = agent.velocity.normalized;
            direction.y = 0;
            transform.rotation = Quaternion.LookRotation(direction);
        }
    }

    public void StopNPC()
    {
        if (debugMode) Debug.Log($"StopNPC chiamato su: {gameObject.name}");
        
        if (slowdownAlreadyUsed)
        {
            if (debugMode) Debug.Log($"Slowdown già usato in precedenza. NPC {gameObject.name} viene fermato senza evento globale.");
            StopThisNPCOnly();
            return;
        }
        
        if (!slowdownAlreadyUsed)
        {
            slowdownAlreadyUsed = true;
            if (debugMode) Debug.Log("PRIMO SLOWDOWN USATO! Attivo evento globale...");
            OnFirstSlowdownUsed?.Invoke();
        }

        StopThisNPCOnly();
    }

    private void StopThisNPCOnly()
    {
        if (isStopped)
        {
            if (debugMode) Debug.Log($"NPC {gameObject.name} già fermo, ignoro.");
            return;
        }

        isStopped = true;
        
        agent.ResetPath();
        agent.isStopped = true;
        agent.velocity = Vector3.zero;

        SetSlowAnimationImmediate();

        if (debugMode) Debug.Log($"NPC {gameObject.name}: Fermato e animazione impostata");

        if (!stoppedNPCs.Contains(this))
        {
            stoppedNPCs.Add(this);
            if (debugMode) Debug.Log($"NPC {gameObject.name} aggiunto alla lista. Totale NPC fermati: {stoppedNPCs.Count}");
        }

        StartCoroutine(SprintEffectThenTeleport());
    }

    private void SetSlowAnimationImmediate()
    {
        if (animator == null) return;
        
        if (debugMode) Debug.Log($"NPC {gameObject.name}: SetSlowAnimationImmediate chiamato");
        
        animator.SetBool("IsRunning", false);
        animator.SetBool("isIdle", true);
        animator.SetBool("isWalking", false);
        animator.SetBool("Slow", true);
        
        animator.Update(0f);
        
        if (debugMode) 
        {
            Debug.Log($"NPC {gameObject.name}: Parametri animator impostati:" +
                     $"\n- IsRunning: {animator.GetBool("IsRunning")}" +
                     $"\n- isIdle: {animator.GetBool("isIdle")}" +
                     $"\n- isWalking: {animator.GetBool("isWalking")}" +
                     $"\n- Slow: {animator.GetBool("Slow")}");
        }
    }

    System.Collections.IEnumerator SprintEffectThenTeleport()
    {
        if (sharedSpline == null)
        {
            Debug.LogWarning($"Spline non assegnata per NPC {gameObject.name}!");
            
            agent.ResetPath();
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
            posizioneSpawn = transform.position;
            
            SetSlowAnimationImmediate();
            
            if (slowdownEffect != null)
            {
                slowdownEffect.PlayEffect();
            }
            
            if (debugMode) Debug.Log($"NPC {gameObject.name}: Fallback completato - Avvio controllo dialogo");
            IniciaControlloDialogo();
            yield break;
        }

        Vector3 targetPosition = CalculateSplinePosition();
        posizioneSpawn = targetPosition;
        
        if (debugMode) Debug.Log($"NPC {gameObject.name}: Inizio teleport istantaneo verso {targetPosition}");
        
        agent.ResetPath();
        agent.isStopped = true;
        agent.velocity = Vector3.zero;
        
        SetSlowAnimationImmediate();
        
        yield return null;
        yield return null;
        
        if (debugMode) 
        {
            CheckCurrentAnimationState("Dopo 2 frame");
        }
        
        if (slowdownEffect != null)
        {
            slowdownEffect.PlayEffect();
            if (debugMode) Debug.Log($"NPC {gameObject.name}: Effetto slowdown attivato");
            
            yield return new WaitForSeconds(0.2f);
            
            slowdownEffect.StopEffect();
        }
        
        if (debugMode) Debug.Log($"NPC {gameObject.name}: Teleport a {targetPosition}");
        
        transform.position = targetPosition;
        
        agent.ResetPath();
        agent.isStopped = true;
        agent.velocity = Vector3.zero;
        
        SetSlowAnimationImmediate();
        
        LookAtPlayer();
        
        yield return new WaitForSeconds(0.1f);
        
        if (debugMode) Debug.Log($"NPC {gameObject.name}: Posizionamento completato - Avvio controllo dialogo per {dialogueSystem?.name}");
        IniciaControlloDialogo();
    }

    private void CheckCurrentAnimationState(string context)
    {
        if (animator == null) return;
        
        AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(0);
        Debug.Log($"NPC {gameObject.name} - {context}:" +
                 $"\n- State Hash: {currentState.shortNameHash}" +
                 $"\n- State Name Check - Slow: {currentState.IsName("Slow")}" +
                 $"\n- State Name Check - Idle: {currentState.IsName("Idle")}" +
                 $"\n- State Name Check - Running: {currentState.IsName("Running")}" +
                 $"\n- Normalized Time: {currentState.normalizedTime}" +
                 $"\n- Parameters - IsRunning: {animator.GetBool("IsRunning")}" +
                 $"\n- Parameters - isIdle: {animator.GetBool("isIdle")}" +
                 $"\n- Parameters - Slow: {animator.GetBool("Slow")}");
    }

    void IniciaControlloDialogo()
    {
        if (dialogueSystem == null)
        {
            if (debugMode) Debug.LogWarning($"NPC {gameObject.name}: Nessun DialogueSystem assegnato, salto comportamento post-dialogo");
            return;
        }

        if (debugMode) Debug.Log($"NPC {gameObject.name}: DialogueSystem assegnato: {dialogueSystem.name}. Aspetto che il dialogo inizi...");
        
        staControllandoDialogo = true;
        StartCoroutine(AspettaInizioDialogo());
    }

    IEnumerator AspettaInizioDialogo()
    {
        if (debugMode) Debug.Log($"NPC {gameObject.name}: Aspetto che il dialogo {dialogueSystem.name} inizi...");
        
        float timeoutInizio = 30f;
        float tempoInizio = Time.time;
        
        while (staControllandoDialogo && !dialogoFinito)
        {
            if (Time.time - tempoInizio > timeoutInizio)
            {
                if (debugMode) Debug.LogWarning($"NPC {gameObject.name}: TIMEOUT - Il dialogo non è mai iniziato dopo {timeoutInizio}s. Avvio comportamento di fallback.");
                
                dialogoFinito = true;
                staControllandoDialogo = false;
                AvviaComportamentoPostDialogo();
                yield break;
            }
            
            if (dialogueSystem != null && dialogueSystem.IsDialogueActive())
            {
                if (debugMode) Debug.Log($"NPC {gameObject.name}: ✅ Il dialogo {dialogueSystem.name} è iniziato! Ora aspetto che finisca...");
                
                StartCoroutine(ControllaDialogoBackup());
                yield break;
            }
            
            yield return new WaitForSeconds(0.2f);
        }
    }

    IEnumerator ControllaDialogoBackup()
    {
        if (debugMode) Debug.Log($"NPC {gameObject.name}: Il dialogo è attivo - ora controllo periodicamente se finisce (ogni {tempoCheckDialogo}s)");
        
        while (staControllandoDialogo && !dialogoFinito)
        {
            if (dialogueSystem != null && !dialogueSystem.IsDialogueActive())
            {
                if (debugMode) Debug.Log($"NPC {gameObject.name}: BACKUP - DialogueSystem indica che il dialogo è finito! Avvio comportamento post-dialogo");
                
                dialogoFinito = true;
                staControllandoDialogo = false;
                
                StartCoroutine(AvviaComportamentoConDelay());
                yield break;
            }
            
            yield return new WaitForSeconds(tempoCheckDialogo);
        }
        
        if (debugMode) Debug.Log($"NPC {gameObject.name}: Uscito dal loop di controllo dialogo. DialogoFinito: {dialogoFinito}");
    }

    void OnDialogueLastLineFinished(DialogueSystem finishedDialogue)
    {
        if (debugMode) Debug.Log($"NPC {gameObject.name}: 📢 Ricevuto evento OnDialogueLastLineFinished da {finishedDialogue?.name}");
        
        if (dialogueSystem != null && finishedDialogue == dialogueSystem)
        {
            if (debugMode) Debug.Log($"NPC {gameObject.name}: 🚀 IL MIO DIALOGO HA FINITO L'ULTIMA BATTUTA! Avvio comportamento post-dialogo");
            
            if (isStopped && !dialogoFinito)
            {
                dialogoFinito = true;
                staControllandoDialogo = false;
                
                if (comportamentoRoutine != null)
                {
                    StopCoroutine(comportamentoRoutine);
                }
                
                StartCoroutine(AvviaComportamentoConDelay());
            }
            else
            {
                if (debugMode) Debug.LogWarning($"NPC {gameObject.name}: Evento ricevuto ma stato non corretto - Stopped: {isStopped}, DialogoFinito: {dialogoFinito}");
            }
        }
        else
        {
            if (debugMode) Debug.Log($"NPC {gameObject.name}: Dialogo finito, ma non è il mio (Il mio: {dialogueSystem?.name}, Finito: {finishedDialogue?.name})");
        }
    }

    IEnumerator AvviaComportamentoConDelay()
    {
        yield return new WaitForSeconds(0.1f);
        
        if (dialogoFinito && isStopped)
        {
            AvviaComportamentoPostDialogo();
        }
    }

    void AvviaComportamentoPostDialogo()
    {
        agent.speed = velocitaPostDialogo;
        agent.isStopped = false;
        
        if (debugMode) Debug.Log($"NPC {gameObject.name}: Reset animazioni per comportamento post-dialogo");
        
        animator.SetBool("Slow", false);
        animator.SetBool("IsRunning", false);
        animator.Update(0f);
        
        if (debugMode) CheckCurrentAnimationState("Dopo reset per post-dialogo");
        
        if (usaPuntoFisso)
        {
            collectiblesManager = FindCollectiblesManager();
            if (collectiblesManager != null)
            {
                regaliIniziali = collectiblesManager.GetCollectedPresents();
                if (debugMode) Debug.Log($"NPC {gameObject.name}: Regali iniziali rilevati: {regaliIniziali}");
            }
            else
            {
                if (debugMode) Debug.LogWarning($"NPC {gameObject.name}: CollectiblesManager non trovato! Disabilito punto fisso.");
                usaPuntoFisso = false;
            }
        }
        
        if (usaPuntoFisso && puntoDestinazione != null)
        {
            AvviaComportamentoPuntoFisso();
        }
        else
        {
            AvviaComportamentoCasuale();
        }
        
        if (debugMode) Debug.Log($"NPC {gameObject.name}: Comportamento post-dialogo avviato (Punto fisso: {usaPuntoFisso})");
    }

    void AvviaComportamentoPuntoFisso()
    {
        inAttesaRegali = true;
        
        statoAttuale = StatoPostDialogo.Walking;
        animator.SetBool("isIdle", false);
        animator.SetBool("isWalking", true);
        
        agent.SetDestination(puntoDestinazione.position);
        
        if (comportamentoRoutine != null)
        {
            StopCoroutine(comportamentoRoutine);
        }
        comportamentoRoutine = StartCoroutine(ComportamentoPuntoFisso());
        
        if (debugMode) Debug.Log($"NPC {gameObject.name}: Avviato comportamento punto fisso verso {puntoDestinazione.position}");
    }

    void AvviaComportamentoCasuale()
    {
        statoAttuale = StatoPostDialogo.Idle;
        animator.SetBool("isIdle", true);
        animator.SetBool("isWalking", false);
        
        if (comportamentoRoutine != null)
        {
            StopCoroutine(comportamentoRoutine);
        }
        comportamentoRoutine = StartCoroutine(ComportamentoPostDialogo());
        
        if (debugMode) Debug.Log($"NPC {gameObject.name}: Avviato comportamento casuale POST-DIALOGO");
    }

    IEnumerator ComportamentoPuntoFisso()
    {
        if (debugMode) Debug.Log($"NPC {gameObject.name}: Camminando verso punto fisso...");
        
        while (inAttesaRegali && dialogoFinito)
        {
            if (!agent.pathPending && agent.remainingDistance < 1f)
            {
                agent.ResetPath();
                statoAttuale = StatoPostDialogo.Idle;
                animator.SetBool("isIdle", true);
                animator.SetBool("isWalking", false);
                
                LookAtPlayer();
                
                if (debugMode) Debug.Log($"NPC {gameObject.name}: Punto fisso raggiunto, aspetto {regaliRichiestiPerLiberazione} regali...");
                break;
            }
            
            if (agent.velocity.sqrMagnitude > 0.1f)
            {
                Vector3 direction = agent.velocity.normalized;
                direction.y = 0;
                transform.rotation = Quaternion.LookRotation(direction);
            }
            
            yield return new WaitForSeconds(0.1f);
        }
        
        while (inAttesaRegali && dialogoFinito)
        {
            if (Time.frameCount % 60 == 0)
            {
                LookAtPlayer();
            }
            
            if (collectiblesManager != null)
            {
                int regaliAttuali = collectiblesManager.GetCollectedPresents();
                int regaliRaccoltiDaInizio = regaliAttuali - regaliIniziali;
                
                if (debugMode && Time.frameCount % 300 == 0)
                {
                    Debug.Log($"NPC {gameObject.name}: Regali raccolti dall'inizio: {regaliRaccoltiDaInizio}/{regaliRichiestiPerLiberazione}");
                }
                
                if (regaliRaccoltiDaInizio >= regaliRichiestiPerLiberazione)
                {
                    inAttesaRegali = false;
                    
                    // NUOVA FUNZIONALITÀ: Disattiva gli oggetti quando i regali sono stati raccolti
                    if (disattivaOggettiAllaLiberazione)
                    {
                        DisattivaOggetti();
                    }
                    
                    if (debugMode) Debug.Log($"NPC {gameObject.name}: {regaliRichiestiPerLiberazione} regali raccolti! NPC liberato, avvio comportamento casuale.");
                    
                    AvviaComportamentoCasuale();
                    yield break;
                }
            }
            else
            {
                if (debugMode) Debug.LogWarning($"NPC {gameObject.name}: CollectiblesManager perso! Libero l'NPC.");
                inAttesaRegali = false;
                AvviaComportamentoCasuale();
                yield break;
            }
            
            yield return new WaitForSeconds(0.5f);
        }
    }

    private void DisattivaOggetti()
    {
        if (oggettiDaDisattivare == null || oggettiDaDisattivare.Length == 0)
        {
            if (debugMode) Debug.LogWarning($"NPC {gameObject.name}: Nessun oggetto configurato per la disattivazione.");
            return;
        }
        
        int oggettiDisattivati = 0;
        
        for (int i = 0; i < oggettiDaDisattivare.Length; i++)
        {
            if (oggettiDaDisattivare[i] != null)
            {
                if (oggettiDaDisattivare[i].activeInHierarchy)
                {
                    oggettiDaDisattivare[i].SetActive(false);
                    oggettiDisattivati++;
                    
                    if (debugMode) 
                    {
                        Debug.Log($"NPC {gameObject.name}: Oggetto #{i} '{oggettiDaDisattivare[i].name}' disattivato con successo!");
                    }
                }
                else
                {
                    if (debugMode) 
                    {
                        Debug.Log($"NPC {gameObject.name}: Oggetto #{i} '{oggettiDaDisattivare[i].name}' era già disattivato.");
                    }
                }
            }
            else
            {
                if (debugMode) 
                {
                    Debug.LogWarning($"NPC {gameObject.name}: Oggetto #{i} nell'array è NULL!");
                }
            }
        }
        
        if (debugMode) 
        {
            Debug.Log($"NPC {gameObject.name}: Disattivazione completata. {oggettiDisattivati}/{oggettiDaDisattivare.Length} oggetti disattivati.");
        }
    }

    private void RiattivaOggetti()
    {
        if (oggettiDaDisattivare == null || oggettiDaDisattivare.Length == 0)
        {
            return;
        }
        
        int oggettiRiattivati = 0;
        
        for (int i = 0; i < oggettiDaDisattivare.Length; i++)
        {
            if (oggettiDaDisattivare[i] != null)
            {
                if (!oggettiDaDisattivare[i].activeInHierarchy)
                {
                    oggettiDaDisattivare[i].SetActive(true);
                    oggettiRiattivati++;
                    
                    if (debugMode) 
                    {
                        Debug.Log($"NPC {gameObject.name}: Oggetto #{i} '{oggettiDaDisattivare[i].name}' riattivato!");
                    }
                }
            }
        }
        
        if (debugMode) 
        {
            Debug.Log($"NPC {gameObject.name}: Riattivazione completata. {oggettiRiattivati}/{oggettiDaDisattivare.Length} oggetti riattivati.");
        }
    }

    IEnumerator ComportamentoPostDialogo()
    {
        while (dialogoFinito && !inAttesaRegali)
        {
            if (statoAttuale == StatoPostDialogo.Idle)
            {
                float personalityFactor = Mathf.Sin(transform.position.x + transform.position.z) * 0.5f + 0.5f;
                float tempoIdleBase = Mathf.Lerp(tempoIdleMin, tempoIdleMax, personalityFactor);
                float variazione = UnityEngine.Random.Range(-1f, 1f);
                float tempoIdle = Mathf.Max(1f, tempoIdleBase + variazione);
                
                if (debugMode) Debug.Log($"NPC {gameObject.name}: Idle per {tempoIdle:F1} secondi (personalità: {personalityFactor:F2})");
                
                if (UnityEngine.Random.Range(0f, 1f) < 0.3f)
                {
                    yield return new WaitForSeconds(tempoIdle * 0.3f);
                    
                    Vector3 randomDirection = new Vector3(
                        UnityEngine.Random.Range(-1f, 1f), 
                        0, 
                        UnityEngine.Random.Range(-1f, 1f)
                    ).normalized;
                    
                    transform.rotation = Quaternion.LookRotation(randomDirection);
                    if (debugMode) Debug.Log($"NPC {gameObject.name}: Guardo in direzione casuale durante idle");
                    
                    yield return new WaitForSeconds(tempoIdle * 0.7f);
                }
                else
                {
                    yield return new WaitForSeconds(tempoIdle);
                }
                
                if (inAttesaRegali) continue;
                
                statoAttuale = StatoPostDialogo.Walking;
                animator.SetBool("isIdle", false);
                animator.SetBool("isWalking", true);
                
                Vector3 posizioneTarget = TrovaPosizioneRandomIntornoAllaSpawn();
                agent.SetDestination(posizioneTarget);
                
                if (debugMode) Debug.Log($"NPC {gameObject.name}: Inizio camminata verso {posizioneTarget}");
            }
            else if (statoAttuale == StatoPostDialogo.Walking)
            {
                float personalityFactor = Mathf.Cos(transform.position.x - transform.position.z) * 0.5f + 0.5f;
                float tempoWalkBase = Mathf.Lerp(tempoWalkMin, tempoWalkMax, personalityFactor);
                float tempoWalk = tempoWalkBase + UnityEngine.Random.Range(-1f, 2f);
                tempoWalk = Mathf.Max(2f, tempoWalk);
                
                float tempoInizio = Time.time;
                
                while (Time.time - tempoInizio < tempoWalk && dialogoFinito && !inAttesaRegali)
                {
                    if (!agent.pathPending && agent.remainingDistance < 1f)
                    {
                        if (debugMode) Debug.Log($"NPC {gameObject.name}: Destinazione raggiunta");
                        break;
                    }
                    
                    if (agent.velocity.sqrMagnitude > 0.1f)
                    {
                        Vector3 direction = agent.velocity.normalized;
                        direction.y = 0;
                        transform.rotation = Quaternion.LookRotation(direction);
                    }
                    
                    if (UnityEngine.Random.Range(0f, 1f) < 0.05f)
                    {
                        Vector3 currentDestination = agent.destination;
                        Vector3 slightVariation = new Vector3(
                            UnityEngine.Random.Range(-2f, 2f), 
                            0, 
                            UnityEngine.Random.Range(-2f, 2f)
                        );
                        Vector3 newDestination = currentDestination + slightVariation;
                        
                        NavMeshHit hit;
                        if (NavMesh.SamplePosition(newDestination, out hit, 3f, NavMesh.AllAreas))
                        {
                            agent.SetDestination(hit.position);
                            if (debugMode) Debug.Log($"NPC {gameObject.name}: Leggera correzione di rotta");
                        }
                    }
                    
                    yield return new WaitForSeconds(0.1f);
                }
                
                if (inAttesaRegali) continue;
                
                agent.ResetPath();
                agent.isStopped = false;
                
                statoAttuale = StatoPostDialogo.Idle;
                animator.SetBool("isIdle", true);
                animator.SetBool("isWalking", false);
                
                if (debugMode) Debug.Log($"NPC {gameObject.name}: Fine camminata, torno in idle");
            }
            
            yield return null;
        }
    }

    CollectiblesManager FindCollectiblesManager()
    {
        if (CollectiblesManager.Instance != null)
        {
            return CollectiblesManager.Instance;
        }
        
        CollectiblesManager manager = UnityEngine.Object.FindFirstObjectByType<CollectiblesManager>();
        if (manager != null)
        {
            if (debugMode) Debug.Log($"NPC {gameObject.name}: CollectiblesManager trovato: {manager.name}");
            return manager;
        }
        
        if (debugMode) Debug.LogWarning($"NPC {gameObject.name}: Nessun CollectiblesManager trovato nella scena!");
        return null;
    }

    Vector3 TrovaPosizioneRandomIntornoAllaSpawn()
    {
        for (int i = 0; i < 30; i++)
        {
            Vector3 targetPos;
            
            if (i < 10)
            {
                Vector2 randomDir = UnityEngine.Random.insideUnitCircle * raggioMovimento;
                targetPos = posizioneSpawn + new Vector3(randomDir.x, 0, randomDir.y);
            }
            else if (i < 20)
            {
                Vector3[] direzioniCardinali = {
                    Vector3.forward, Vector3.back, Vector3.left, Vector3.right,
                    new Vector3(1, 0, 1).normalized, new Vector3(-1, 0, 1).normalized,
                    new Vector3(1, 0, -1).normalized, new Vector3(-1, 0, -1).normalized
                };
                
                Vector3 direzioneBase = direzioniCardinali[UnityEngine.Random.Range(0, direzioniCardinali.Length)];
                float distanza = UnityEngine.Random.Range(raggioMovimento * 0.3f, raggioMovimento);
                float variazione = UnityEngine.Random.Range(-45f, 45f);
                
                Vector3 direzioneVariata = Quaternion.AngleAxis(variazione, Vector3.up) * direzioneBase;
                targetPos = posizioneSpawn + direzioneVariata * distanza;
            }
            else
            {
                float angolo = UnityEngine.Random.Range(0f, 360f);
                float distanza = UnityEngine.Random.Range(raggioMovimento * 0.5f, raggioMovimento * 1.2f);
                
                Vector3 direzione = new Vector3(Mathf.Cos(angolo * Mathf.Deg2Rad), 0, Mathf.Sin(angolo * Mathf.Deg2Rad));
                targetPos = posizioneSpawn + direzione * distanza;
            }
            
            NavMeshHit navHit;
            if (NavMesh.SamplePosition(targetPos, out navHit, 3f, NavMesh.AllAreas))
            {
                if (!CiSonoOstacoliNellaPosizione(navHit.position))
                {
                    float distanzaDallaPosizioneAttuale = Vector3.Distance(transform.position, navHit.position);
                    if (distanzaDallaPosizioneAttuale > 2f)
                    {
                        if (debugMode) Debug.Log($"NPC {gameObject.name}: Posizione casuale trovata al tentativo {i + 1}: {navHit.position} (distanza: {distanzaDallaPosizioneAttuale:F1}m)");
                        return navHit.position;
                    }
                    else
                    {
                        if (debugMode) Debug.Log($"NPC {gameObject.name}: Tentativo {i + 1} - Troppo vicino alla posizione attuale ({distanzaDallaPosizioneAttuale:F1}m)");
                    }
                }
                else
                {
                    if (debugMode) Debug.Log($"NPC {gameObject.name}: Tentativo {i + 1} - Ostacoli rilevati in {navHit.position}");
                }
            }
            else
            {
                if (debugMode) Debug.Log($"NPC {gameObject.name}: Tentativo {i + 1} - Posizione non valida sulla NavMesh: {targetPos}");
            }
        }
        
        Vector2 fallbackDir = UnityEngine.Random.insideUnitCircle.normalized * 3f;
        Vector3 fallbackPos = transform.position + new Vector3(fallbackDir.x, 0, fallbackDir.y);
        
        NavMeshHit fallbackHit;
        if (NavMesh.SamplePosition(fallbackPos, out fallbackHit, 5f, NavMesh.AllAreas))
        {
            if (debugMode) Debug.Log($"NPC {gameObject.name}: Usando posizione fallback: {fallbackHit.position}");
            return fallbackHit.position;
        }
        
        if (debugMode) Debug.LogWarning($"NPC {gameObject.name}: Non riesco a trovare posizione valida dopo 30 tentativi, resto fermo");
        return transform.position;
    }

    bool CiSonoOstacoliNellaPosizione(Vector3 posizione)
    {
        Collider[] ostacoli = Physics.OverlapSphere(posizione, raggioControlloOstacoli, layerOstacoli);
        
        int ostacoliSignificativi = 0;
        foreach (Collider ostacolo in ostacoli)
        {
            if (ostacolo.bounds.size.magnitude < 1f) continue;
            if (ostacolo.isTrigger) continue;
            
            if (ostacolo.GetComponent<Npc_village>() != null) continue;
            
            ostacoliSignificativi++;
        }
        
        if (ostacoliSignificativi > 0)
        {
            if (debugMode)
            {
                string nomiOstacoli = "";
                foreach (var ostacolo in ostacoli)
                {
                    if (!ostacolo.isTrigger && ostacolo.bounds.size.magnitude >= 1f)
                    {
                        nomiOstacoli += ostacolo.name + ", ";
                    }
                }
                if (nomiOstacoli.Length > 0)
                {
                    Debug.Log($"NPC {gameObject.name}: Ostacoli significativi in {posizione}: {nomiOstacoli}");
                }
            }
            return true;
        }
        
        RaycastHit groundHit;
        if (!Physics.Raycast(posizione + Vector3.up * 2f, Vector3.down, out groundHit, 5f))
        {
            if (debugMode) Debug.Log($"NPC {gameObject.name}: Nessun terreno rilevato sotto {posizione}");
            return true;
        }
        
        return false;
    }

    Vector3 CalculateSplinePosition()
    {
        int myIndex = stoppedNPCs.IndexOf(this);
        
        int knotCount = sharedSpline.Spline.Count;
        
        if (knotCount == 0)
        {
            Debug.LogWarning($"Spline {sharedSpline.name} non ha knots!");
            return transform.position;
        }
        
        int targetKnotIndex = myIndex % knotCount;
        
        BezierKnot knot = sharedSpline.Spline[targetKnotIndex];
        float3 knotPosition = knot.Position;
        
        Vector3 worldPosition = sharedSpline.transform.TransformPoint(new Vector3(knotPosition.x, knotPosition.y, knotPosition.z));
        
        if (debugMode) Debug.Log($"NPC {gameObject.name}: Posizione calcolata - Index: {myIndex}, Knot: {targetKnotIndex}, Pos: {worldPosition}");
        
        return worldPosition;
    }

    void LookAtPlayer()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            Vector3 direction = player.transform.position - transform.position;
            direction.y = 0;
            
            if (direction.sqrMagnitude > 0.01f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                
                if (agent.isStopped || agent.velocity.sqrMagnitude < 0.1f)
                {
                    StartCoroutine(RotateTowardsTarget(targetRotation, 1f));
                }
                
                if (debugMode) Debug.Log($"NPC {gameObject.name}: Guardando verso il player");
            }
        }
        else
        {
            Debug.LogWarning($"Player non trovato per NPC {gameObject.name}");
        }
    }

    IEnumerator RotateTowardsTarget(Quaternion targetRotation, float duration)
    {
        Quaternion startRotation = transform.rotation;
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            
            t = Mathf.SmoothStep(0f, 1f, t);
            
            transform.rotation = Quaternion.Slerp(startRotation, targetRotation, t);
            yield return null;
        }
        
        transform.rotation = targetRotation;
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
        DialogueSystem.OnAnyLastLineFinished -= OnDialogueLastLineFinished;
        
        if (comportamentoRoutine != null)
        {
            StopCoroutine(comportamentoRoutine);
        }
        
        if (stoppedNPCs.Contains(this))
        {
            stoppedNPCs.Remove(this);
            if (debugMode) Debug.Log($"NPC {gameObject.name}: Rimosso dalla lista al destroy");
        }
    }

    public void RestartMovement()
    {
        if (debugMode) Debug.Log($"NPC {gameObject.name}: Riavvio movimento");
        
        isStopped = false;
        dialogoFinito = false;
        staControllandoDialogo = false;
        inAttesaRegali = false;
        regaliIniziali = 0;
        
        // Riattiva gli oggetti quando l'NPC viene riavviato
        if (disattivaOggettiAllaLiberazione)
        {
            RiattivaOggetti();
        }
        
        if (comportamentoRoutine != null)
        {
            StopCoroutine(comportamentoRoutine);
            comportamentoRoutine = null;
        }
        
        agent.isStopped = false;
        agent.speed = 15f;
        
        if (stoppedNPCs.Contains(this))
        {
            stoppedNPCs.Remove(this);
        }
        
        animator.SetBool("IsRunning", true);
        animator.SetBool("Slow", false);
        animator.SetBool("isIdle", false);
        animator.SetBool("isWalking", false);
        
        if (waypoints.Length > 0)
        {
            GoToRandomWaypoint();
        }
    }

    [ContextMenu("Reset Slowdown System")]
    public static void ResetSlowdownSystem()
    {
        slowdownAlreadyUsed = false;
        
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

    [ContextMenu("Test Stop This NPC")]
    public void TestStopThisNPC()
    {
        StopNPC();
    }

    [ContextMenu("Test Restart This NPC")]
    public void TestRestartThisNPC()
    {
        RestartMovement();
    }

    [ContextMenu("Test - Animazione Slow Istantanea")]
    public void TestSlowAnimationInstant()
    {
        StartCoroutine(TestSlowAnimation());
    }

    private IEnumerator TestSlowAnimation()
    {
        Debug.Log($"NPC {gameObject.name}: TEST - Stato prima del cambio");
        CheckCurrentAnimationState("Prima del cambio");
        
        agent.ResetPath();
        agent.isStopped = true;
        agent.velocity = Vector3.zero;
        
        SetSlowAnimationImmediate();
        
        Debug.Log($"NPC {gameObject.name}: TEST - Stato subito dopo SetSlowAnimationImmediate");
        CheckCurrentAnimationState("Subito dopo SetSlowAnimationImmediate");
        
        yield return null;
        
        Debug.Log($"NPC {gameObject.name}: TEST - Stato dopo 1 frame");
        CheckCurrentAnimationState("Dopo 1 frame");
        
        yield return null;
        
        Debug.Log($"NPC {gameObject.name}: TEST - Stato dopo 2 frame");
        CheckCurrentAnimationState("Dopo 2 frame");
    }

    [ContextMenu("Test - Simula Attesa Dialogo")]
    public void TestSimulaAttesaDialogo()
    {
        if (isStopped && !dialogoFinito)
        {
            if (debugMode) Debug.Log($"NPC {gameObject.name}: Test - Simulando attesa dialogo...");
            StopAllCoroutines();
            staControllandoDialogo = false;
            IniciaControlloDialogo();
        }
        else
        {
            if (debugMode) Debug.LogWarning($"NPC {gameObject.name}: Test fallito - Stato: Stopped={isStopped}, DialogoFinito={dialogoFinito}");
        }
    }

    [ContextMenu("Test - Forza Inizio Dialogo")]
    public void TestForzaInizioDialogo()
    {
        if (dialogueSystem != null)
        {
            dialogueSystem.TriggerDialogue();
            if (debugMode) Debug.Log($"NPC {gameObject.name}: Test - Forzato inizio dialogo {dialogueSystem.name}");
        }
        else
        {
            if (debugMode) Debug.LogWarning($"NPC {gameObject.name}: Nessun DialogueSystem assegnato per il test");
        }
    }

    [ContextMenu("Test Connessione DialogueSystem")]
    public void TestConnessioneDialogueSystem()
    {
        if (dialogueSystem != null)
        {
            bool isActive = dialogueSystem.IsDialogueActive();
            if (debugMode) Debug.Log($"NPC {gameObject.name}: DialogueSystem connesso - Attivo: {isActive}");
        }
        else
        {
            if (debugMode) Debug.LogWarning($"NPC {gameObject.name}: Nessun DialogueSystem assegnato!");
        }
    }

    [ContextMenu("Test Fine Dialogo")]
    public void TestFineDialogo()
    {
        if (isStopped && !dialogoFinito)
        {
            dialogoFinito = true;
            staControllandoDialogo = false;
            AvviaComportamentoPostDialogo();
        }
    }

    [ContextMenu("Test Toggle Punto Fisso")]
    public void TestTogglePuntoFisso()
    {
        usaPuntoFisso = !usaPuntoFisso;
        if (debugMode) Debug.Log($"NPC {gameObject.name}: Punto fisso {(usaPuntoFisso ? "ATTIVATO" : "DISATTIVATO")}");
        
        if (dialogoFinito)
        {
            if (comportamentoRoutine != null)
            {
                StopCoroutine(comportamentoRoutine);
            }
            AvviaComportamentoPostDialogo();
        }
    }

    [ContextMenu("Test Simula Raccolta Regali")]
    public void TestSimulaRaccoltaRegali()
    {
        if (collectiblesManager != null)
        {
            for (int i = 0; i < regaliRichiestiPerLiberazione; i++)
            {
                collectiblesManager.NotifyPresentCollected($"TestPresent_{i}_{Time.time}");
            }
            if (debugMode) Debug.Log($"NPC {gameObject.name}: Simulati {regaliRichiestiPerLiberazione} regali raccolti");
        }
        else
        {
            if (debugMode) Debug.LogWarning($"NPC {gameObject.name}: CollectiblesManager non trovato per test");
        }
    }

    [ContextMenu("Test Forza Liberazione")]
    public void TestForzaLiberazione()
    {
        if (inAttesaRegali)
        {
            inAttesaRegali = false;
            if (debugMode) Debug.Log($"NPC {gameObject.name}: Liberazione forzata dall'attesa regali");
            AvviaComportamentoCasuale();
        }
    }

    // NUOVI METODI DI TEST PER GLI OGGETTI
    [ContextMenu("Test - Disattiva Oggetti Manualmente")]
    public void TestDisattivaOggetti()
    {
        DisattivaOggetti();
    }

    [ContextMenu("Test - Riattiva Oggetti Manualmente")]
    public void TestRiattivaOggetti()
    {
        RiattivaOggetti();
    }

    [ContextMenu("Test - Verifica Configurazione Oggetti")]
    public void TestVerificaConfigurazioneOggetti()
    {
        if (oggettiDaDisattivare == null)
        {
            Debug.LogError($"NPC {gameObject.name}: Array oggetti è NULL!");
            return;
        }
        
        Debug.Log($"NPC {gameObject.name}: Configurazione oggetti da disattivare:");
        Debug.Log($"- Array size: {oggettiDaDisattivare.Length}");
        Debug.Log($"- Disattivazione abilitata: {disattivaOggettiAllaLiberazione}");
        
        for (int i = 0; i < oggettiDaDisattivare.Length; i++)
        {
            if (oggettiDaDisattivare[i] != null)
            {
                Debug.Log($"- Slot {i}: '{oggettiDaDisattivare[i].name}' (Attivo: {oggettiDaDisattivare[i].activeInHierarchy})");
            }
            else
            {
                Debug.LogWarning($"- Slot {i}: VUOTO!");
            }
        }
    }

    [ContextMenu("Debug - Stato Animator")]
    public void DebugAnimatorState()
    {
        if (animator != null)
        {
            Debug.Log($"NPC {gameObject.name} Animator State:\n" +
                     $"- IsRunning: {animator.GetBool("IsRunning")}\n" +
                     $"- Slow: {animator.GetBool("Slow")}\n" +
                     $"- isIdle: {animator.GetBool("isIdle")}\n" +
                     $"- isWalking: {animator.GetBool("isWalking")}\n" +
                     $"- Current State: {animator.GetCurrentAnimatorStateInfo(0).IsName("Slow")}\n" +
                     $"- Agent Stopped: {agent.isStopped}\n" +
                     $"- Agent Velocity: {agent.velocity.magnitude}");
        }
    }

    [ContextMenu("Test - Verifica Configurazione Animator")]
    public void TestAnimatorConfiguration()
    {
        if (animator == null)
        {
            Debug.LogError($"NPC {gameObject.name}: Animator è NULL!");
            return;
        }

        if (animator.runtimeAnimatorController == null)
        {
            Debug.LogError($"NPC {gameObject.name}: Nessun Animator Controller assegnato!");
            return;
        }

        Debug.Log($"NPC {gameObject.name}: Animator Controller: {animator.runtimeAnimatorController.name}");
        
        var parameters = animator.parameters;
        Debug.Log($"Parametri trovati nell'Animator Controller:");
        foreach (var param in parameters)
        {
            Debug.Log($"- {param.name} (Type: {param.type})");
        }
        
        bool hasIsRunning = System.Array.Exists(parameters, p => p.name == "IsRunning");
        bool hasIsIdle = System.Array.Exists(parameters, p => p.name == "isIdle");
        bool hasIsWalking = System.Array.Exists(parameters, p => p.name == "isWalking");
        bool hasSlow = System.Array.Exists(parameters, p => p.name == "Slow");
        
        Debug.Log($"Parametri richiesti:" +
                 $"\n- IsRunning: {(hasIsRunning ? "✅" : "❌")}" +
                 $"\n- isIdle: {(hasIsIdle ? "✅" : "❌")}" +
                 $"\n- isWalking: {(hasIsWalking ? "✅" : "❌")}" +
                 $"\n- Slow: {(hasSlow ? "✅" : "❌")}");
    }

    void OnDrawGizmosSelected()
    {
        if (!dialogoFinito || !Application.isPlaying) return;
        
        Gizmos.color = Color.green;
        DrawWireCircle(posizioneSpawn, raggioMovimento);
        
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(posizioneSpawn, 0.5f);
        
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, raggioControlloOstacoli);
        
        if (usaPuntoFisso && puntoDestinazione != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(puntoDestinazione.position, 1f);
            Gizmos.DrawLine(transform.position, puntoDestinazione.position);
            
            if (inAttesaRegali)
            {
                Gizmos.color = new Color(1f, 0.5f, 0f);
                Gizmos.DrawWireCube(puntoDestinazione.position + Vector3.up * 2f, Vector3.one * 0.5f);
            }
        }
        
        if (statoAttuale == StatoPostDialogo.Walking && agent.hasPath)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(agent.destination, 0.3f);
            Gizmos.DrawLine(transform.position, agent.destination);
        }
    }

    void DrawWireCircle(Vector3 center, float radius)
    {
        const int segments = 32;
        float angleStep = 360f / segments;
        Vector3 prevPoint = center + new Vector3(radius, 0, 0);
        
        for (int i = 1; i <= segments; i++)
        {
            float angle = i * angleStep * Mathf.Deg2Rad;
            Vector3 newPoint = center + new Vector3(
                Mathf.Cos(angle) * radius, 
                0, 
                Mathf.Sin(angle) * radius
            );
            Gizmos.DrawLine(prevPoint, newPoint);
            prevPoint = newPoint;
        }
    }

    // Proprietà per debug e controllo esterno
    public bool IsStopped => isStopped;
    public bool DialogoFinito => dialogoFinito;
    public bool InAttesaRegali => inAttesaRegali;
    public bool UsaPuntoFisso => usaPuntoFisso;
    public int RegaliRichiestiPerLiberazione => regaliRichiestiPerLiberazione;
    public GameObject[] OggettiDaDisattivare => oggettiDaDisattivare;
    public bool DisattivaOggettiAllaLiberazione => disattivaOggettiAllaLiberazione;
    public static int TotalStoppedNPCs => stoppedNPCs.Count;
    public static bool SlowdownWasUsed => slowdownAlreadyUsed;
}