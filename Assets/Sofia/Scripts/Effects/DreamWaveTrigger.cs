using UnityEngine;
using Unity.Cinemachine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using UnityEngine.Events;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Definisce come un oggetto deve muoversi durante il terremoto
/// </summary>
[System.Serializable]
public class ObjectMovement
{
    public GameObject targetObject;
    
    [Header("Tipo di Movimento")]
    [Tooltip("Se true, usa la posizione finale assoluta. Se false, usa l'offset relativo.")]
    public bool useAbsolutePosition = false;
    
    [Header("Offset Relativo (se useAbsolutePosition = false)")]
    [Tooltip("Quanto spostare l'oggetto rispetto alla posizione attuale")]
    public Vector3 movementOffset = Vector3.zero;
    
    [Header("Posizione Finale (se useAbsolutePosition = true)")]
    [Tooltip("La posizione finale dove l'oggetto deve arrivare")]
    public Vector3 targetPosition = Vector3.zero;
    
    [Header("Timing")]
    [Tooltip("Ritardo prima di iniziare il movimento (in secondi)")]
    public float startDelay = 0f;
    [Tooltip("Durata del movimento (in secondi). Se 0, usa la durata del terremoto")]
    public float duration = 0f;
    
    [Header("Easing")]
    [Tooltip("Curva di animazione per il movimento")]
    public AnimationCurve movementCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    
    [Header("Spazio")]
    [Tooltip("Se true, il movimento è in world space. Se false, in local space.")]
    public bool useWorldSpace = true;
    
    [Header("Gestione Figli")]
    [Tooltip("Se true, forza i Rigidbody dei figli a kinematic durante il movimento")]
    public bool forceChildrenFollow = false;
    [Tooltip("Se true, include anche i figli dei figli (ricorsivo)")]
    public bool includeNestedChildren = true;
    
    [Header("Gestione Speciale Spline")]
    [Tooltip("Se true, ri-campiona le spline dopo il movimento")]
    public bool resampleSplineAfterMove = false;
    [Tooltip("MovingPlatform da ri-campionare dopo il movimento (assegna direttamente)")]
    public MonoBehaviour[] movingPlatformsToResample;
    [Tooltip("GemSpawnerSpline da reinizializzare dopo il movimento (assegna direttamente)")]
    public MonoBehaviour[] gemSpawnersToReinitialize;
    
    [Header("Oggetti Spawnati da Muovere")]
    [Tooltip("Se true, sposta anche le gemme/oggetti spawnati insieme")]
    public bool moveSpawnedObjectsWithSpline = false;
    [Tooltip("Tag degli oggetti spawnati da muovere insieme (es. 'Gem')")]
    public string spawnedObjectsTag = "Gem";
    [Tooltip("Raggio di ricerca per oggetti spawnati vicino alla spline")]
    public float spawnedObjectsSearchRadius = 50f;

    [Header("GemSpawnerSpline Integration")]
    [Tooltip("Se true, distrugge le gemme prima del movimento e le respawna dopo")]
    public bool destroyAndRespawnGems = true;

    // Stato interno (non visibile nell'inspector)
    [HideInInspector] public Vector3 startPosition;
    [HideInInspector] public Vector3 calculatedEndPosition;
    [HideInInspector] public bool isMoving = false;
    [HideInInspector] public List<ChildTransformData> savedChildData = new List<ChildTransformData>();
    [HideInInspector] public List<SpawnedObjectData> spawnedObjectsData = new List<SpawnedObjectData>();
    [HideInInspector] public List<GemSpawnerSpline> gemSpawnersToRespawn = new List<GemSpawnerSpline>();
}

/// <summary>
/// Salva i dati di un transform figlio per il ripristino
/// </summary>
[System.Serializable]
public class ChildTransformData
{
    public Transform child;
    public Transform originalParent;
    public Vector3 originalLocalPosition;
    public Quaternion originalLocalRotation;
    public Vector3 originalLocalScale;
    public Rigidbody rigidbody;
    public bool wasKinematic;
    public Animator animator;
    public bool animatorWasEnabled;
}

/// <summary>
/// Salva i dati degli oggetti spawnati per muoverli insieme
/// </summary>
[System.Serializable]
public class SpawnedObjectData
{
    public Transform spawnedObject;
    public Vector3 offsetFromParent; // Offset rispetto al parent della spline
}

/// <summary>
/// Definisce gli oggetti da attivare al termine di tutti i movimenti, con audio opzionale
/// </summary>
[System.Serializable]
public class PostMovementActivation
{
    [Header("Oggetti da Attivare")]
    public GameObject[] objectsToActivate;

    [Header("Nemici da Nascondere")]
    [Tooltip("Nemici da nascondere IMMEDIATAMENTE quando gli oggetti vengono attivati (prima del rebake)")]
    public GameObject[] enemiesToHide;

    [Header("Audio")]
    [Tooltip("Audio da riprodurre quando gli oggetti vengono attivati")]
    public AudioClip activationAudio;
    [Range(0f, 1f)]
    public float audioVolume = 1f;
    [Tooltip("Se true, l'audio è spazializzato 3D. Se false, è 2D.")]
    public bool spatializedAudio = false;
    [Tooltip("Posizione dove riprodurre l'audio 3D (se vuoto, usa la posizione del primo oggetto attivato)")]
    public Transform audioSourcePosition;

    [Header("Timing")]
    [Tooltip("Ritardo dopo la fine dei movimenti prima di attivare gli oggetti")]
    public float activationDelay = 0f;

    // ✅ NavMesh Rebaking
    [Header("NavMesh Rebaking")]
    [Tooltip("Se true, effettua il rebake della NavMesh dopo il movimento")]
    public bool rebakeNavMesh = false;
    [Tooltip("NavMeshSurface da rebakare (assegna da Inspector). Supporta array multipli.")]
    public NavMeshSurface[] navMeshSurfaces;
    [Tooltip("Ritardo prima del rebake della NavMesh")]
    public float rebakeDelay = 0.1f;
}

/// <summary>
/// Trigger principale che coordina audio, dissolve, camera shake e movimento oggetti
/// Supporta multipli oggetti e materiali per il dissolve
/// Può essere usato sia con trigger che manualmente tramite chiamata di metodo
/// </summary>
public class DreamWaveTrigger : MonoBehaviour
{
    [Header("Trigger Settings")]
    [SerializeField] private bool useTriggerCollider = true;
    [SerializeField] private bool onlyTriggerOnce = true;
    
    [Header("Earthquake Settings")]
    [SerializeField] private float earthquakeDuration = 3f;
    [SerializeField] private bool disablePlayerMovement = true;
    
    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip mainAudio;
    [SerializeField] private float audioVolume = 0.8f;
    [SerializeField] private bool loopAudio = false;
    
    [Header("Camera Shake Integration")]
    [SerializeField] private EarthquakeShake earthquakeShakeController;
    [SerializeField] private bool enableCameraShake = true;
    [SerializeField] private float shakeDelay = 0f;
    [SerializeField] private float shakeStopDelay = 1f;
    
    [Header("Material Dissolve Effect")]
    [SerializeField] private GameObject[] objectsToDissolve;
    [SerializeField] private Material[] materialsToDissolve;
    [SerializeField] private string dissolvePropertyName = "_Dissolve";
    [SerializeField] private float dissolveDuration = 3f;
    [SerializeField] private AnimationCurve dissolveCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    [SerializeField] private bool debugDissolve = false;
    
    [Header("Object Movement During Earthquake")]
    [SerializeField] private bool enableObjectMovement = false;
    [SerializeField] private ObjectMovement[] objectsToMove;
    [SerializeField] private bool debugMovement = false;
    
    [Header("Post-Movement Activation")]
    [Tooltip("Oggetti da attivare al termine di TUTTI i movimenti")]
    [SerializeField] private PostMovementActivation postMovementActivation;
    
    [Header("Object Activation/Deactivation")]
    [SerializeField] private GameObject[] objectsToActivate;
    [SerializeField] private float activationDelay = 0f;
    [SerializeField] private GameObject[] objectsToDeactivate;
    [SerializeField] private float deactivationDelay = 0f;
    
    [Header("Events")]
    [Tooltip("Evento chiamato all'inizio dell'ondata onirica (per cambio camera, etc.)")]
    public UnityEvent OnDreamWaveStarted;

    [Header("Debug")]
    [SerializeField] private bool debugMode = false;

    // Componenti e stati
    private ThirdPersonController playerController;
    private bool hasTriggered = false;
    private bool isAudioPlaying = false;
    private bool isDissolving = false;
    private bool isMovingObjects = false;
    private Coroutine mainCoroutine;
    private Coroutine dissolveCoroutine;
    private List<Coroutine> movementCoroutines = new List<Coroutine>();
    private List<Material> validMaterials = new List<Material>();
    private int activeMovementsCount = 0;
    private Coroutine postMovementCoroutine;

    private void Awake()
    {
        SetupTrigger();
        SetupAudioSource();
        SetupDissolveMaterials();
        FindShakeController();
        InitializeMovementCurves();
    }

    private void SetupTrigger()
    {
        if (useTriggerCollider)
        {
            BoxCollider boxCollider = GetComponent<BoxCollider>();
            if (boxCollider == null)
            {
                boxCollider = gameObject.AddComponent<BoxCollider>();
                Debug.LogWarning($"[DreamWaveTrigger] BoxCollider mancante su {name}. Aggiunto automaticamente.");
            }
            boxCollider.isTrigger = true;
        }
        else
        {
            BoxCollider boxCollider = GetComponent<BoxCollider>();
            if (boxCollider != null && boxCollider.isTrigger)
            {
                if (debugMode)
                    Debug.Log($"[DreamWaveTrigger] Trigger disabilitato - BoxCollider rimosso da {name}");
                DestroyImmediate(boxCollider);
            }
        }
    }

    private void SetupAudioSource()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                GameObject audioGO = new GameObject("TriggerAudio");
                audioGO.transform.SetParent(transform);
                audioGO.transform.localPosition = Vector3.zero;
                audioSource = audioGO.AddComponent<AudioSource>();
            }
        }
        
        audioSource.clip = mainAudio;
        audioSource.volume = 0f;
        audioSource.loop = loopAudio;
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0.7f;
        audioSource.rolloffMode = AudioRolloffMode.Linear;
        audioSource.maxDistance = 50f;
    }

    private void FindShakeController()
    {
        if (earthquakeShakeController == null)
        {
            earthquakeShakeController = FindFirstObjectByType<EarthquakeShake>();
            if (earthquakeShakeController == null && enableCameraShake)
            {
                Debug.LogWarning("[DreamWaveTrigger] EarthquakeShake controller non trovato! Camera shake disabilitato.");
                enableCameraShake = false;
            }
        }
    }

    private void SetupDissolveMaterials()
    {
        validMaterials.Clear();
        
        if (materialsToDissolve != null)
        {
            for (int i = 0; i < materialsToDissolve.Length; i++)
            {
                Material material = materialsToDissolve[i];
                if (material != null)
                {
                    if (material.HasProperty(dissolvePropertyName))
                    {
                        material.SetFloat(dissolvePropertyName, 0f);
                        validMaterials.Add(material);
                        if (debugDissolve)
                            Debug.Log($"[DreamWaveTrigger] Materiale '{material.name}' inizializzato (indice {i})");
                    }
                    else
                    {
                        Debug.LogWarning($"[DreamWaveTrigger] Il materiale '{material.name}' (indice {i}) non ha la proprietà '{dissolvePropertyName}'");
                    }
                }
            }
        }
        
        if (debugDissolve)
            Debug.Log($"[DreamWaveTrigger] {validMaterials.Count} materiali validi trovati per il dissolve");
    }
    
    private void InitializeMovementCurves()
    {
        if (objectsToMove == null) return;
        
        foreach (var movement in objectsToMove)
        {
            if (movement.movementCurve == null || movement.movementCurve.keys.Length == 0)
            {
                movement.movementCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!useTriggerCollider) return;
        if (!other.CompareTag("Player")) return;
        if (onlyTriggerOnce && hasTriggered) return;
        if (isAudioPlaying) return;
        
        if (debugMode)
            Debug.Log($"[DreamWaveTrigger] Player entrato nel trigger: {name}");
        
        playerController = other.GetComponent<ThirdPersonController>();
        if (playerController == null)
        {
            Debug.LogError($"[DreamWaveTrigger] ThirdPersonController non trovato sul player!");
            return;
        }
        
        StartSequence();
    }

    // ========================
    // METODI PUBBLICI PER ATTIVAZIONE MANUALE
    // ========================
    
    public void TriggerSequenceManually()
    {
        if (onlyTriggerOnce && hasTriggered)
        {
            if (debugMode)
                Debug.LogWarning($"[DreamWaveTrigger] Tentativo di attivare {name} che può essere attivato solo una volta");
            return;
        }
        
        if (isAudioPlaying)
        {
            if (debugMode)
                Debug.LogWarning($"[DreamWaveTrigger] Tentativo di attivare {name} ma è già in esecuzione");
            return;
        }
        
        if (playerController == null)
        {
            ThirdPersonController controller = FindFirstObjectByType<ThirdPersonController>();
            if (controller != null)
            {
                playerController = controller;
            }
            else
            {
                Debug.LogError($"[DreamWaveTrigger] ThirdPersonController non trovato nella scena!");
                return;
            }
        }
        
        if (debugMode)
            Debug.Log($"[DreamWaveTrigger] Sequenza attivata manualmente: {name}");
        
        StartSequence();
    }
    
    public void TriggerSequenceManually(ThirdPersonController specificPlayerController)
    {
        if (onlyTriggerOnce && hasTriggered)
        {
            if (debugMode)
                Debug.LogWarning($"[DreamWaveTrigger] Tentativo di attivare {name} che può essere attivato solo una volta");
            return;
        }
        
        if (isAudioPlaying)
        {
            if (debugMode)
                Debug.LogWarning($"[DreamWaveTrigger] Tentativo di attivare {name} ma è già in esecuzione");
            return;
        }
        
        if (specificPlayerController == null)
        {
            Debug.LogError($"[DreamWaveTrigger] PlayerController fornito è null!");
            return;
        }
        
        playerController = specificPlayerController;
        
        if (debugMode)
            Debug.Log($"[DreamWaveTrigger] Sequenza attivata manualmente con player specifico: {name}");
        
        StartSequence();
    }

    private void StartSequence()
    {
        if (isAudioPlaying) return;

        hasTriggered = true;
        isAudioPlaying = true;

        if (debugMode)
            Debug.Log($"[DreamWaveTrigger] Avvio sequenza terremoto per {earthquakeDuration} secondi");

        // ⭐ Invoca l'evento OnDreamWaveStarted SUBITO all'inizio
        OnDreamWaveStarted?.Invoke();

        if (debugMode)
            Debug.Log($"[DreamWaveTrigger] Evento OnDreamWaveStarted invocato");

        mainCoroutine = StartCoroutine(MainSequence());
    }

    private IEnumerator MainSequence()
    {
        // ========================
        // FASE 1: AVVIO
        // ========================
        
        if (disablePlayerMovement && playerController != null)
        {
            playerController.IsMovementLocked = true;
            if (debugMode) Debug.Log("[DreamWaveTrigger] Movimento player disabilitato");
        }
        
        if (audioSource != null && mainAudio != null)
        {
            audioSource.clip = mainAudio;
            audioSource.loop = loopAudio;
            audioSource.volume = audioVolume;
            audioSource.Play();
            if (debugMode) Debug.Log("[DreamWaveTrigger] Audio principale avviato");
        }
        
        // ✅ AVVIA CAMERA SHAKE
        if (enableCameraShake && earthquakeShakeController != null)
        {
            if (shakeDelay > 0)
            {
                yield return new WaitForSeconds(shakeDelay);
            }
            earthquakeShakeController.StartShake();
            if (debugMode) Debug.Log("[DreamWaveTrigger] Camera shake avviato");
        }
        
        // ✅ AVVIA MOVIMENTO OGGETTI
        if (enableObjectMovement && objectsToMove != null && objectsToMove.Length > 0)
        {
            StartObjectMovements();
        }
        
        // ✅ AVVIA DISSOLVE
        if ((validMaterials.Count > 0) && (objectsToDissolve != null && objectsToDissolve.Length > 0))
        {
            dissolveCoroutine = StartCoroutine(DissolveObjects());
        }
        
        // ========================
        // FASE 2: ATTESA
        // ========================
        
        float totalDuration = earthquakeDuration;
        if (!loopAudio && mainAudio != null)
        {
            totalDuration = Mathf.Min(earthquakeDuration, mainAudio.length);
        }
        
        yield return new WaitForSeconds(totalDuration - shakeDelay);
        
        // ========================
        // FASE 3: CONCLUSIONE
        // ========================
        
        EndSequence();
    }
    
    // ========================
    // SISTEMA DI MOVIMENTO OGGETTI
    // ========================
    
    private void StartObjectMovements()
    {
        if (objectsToMove == null) return;
        
        isMovingObjects = true;
        movementCoroutines.Clear();
        activeMovementsCount = 0;
        
        foreach (var movement in objectsToMove)
        {
            if (movement.targetObject != null)
            {
                activeMovementsCount++;
                Coroutine coroutine = StartCoroutine(MoveObject(movement));
                movementCoroutines.Add(coroutine);
            }
        }
        
        if (debugMovement)
            Debug.Log($"[DreamWaveTrigger] Avviato movimento per {activeMovementsCount} oggetti");
    }
    
    private void OnMovementCompleted()
    {
        activeMovementsCount--;
        
        if (debugMovement)
            Debug.Log($"[DreamWaveTrigger] Movimento completato. Rimanenti: {activeMovementsCount}");
        
        // Quando tutti i movimenti sono completati
        if (activeMovementsCount <= 0)
        {
            isMovingObjects = false;
            
            if (debugMovement)
                Debug.Log("[DreamWaveTrigger] Tutti i movimenti completati!");
            
            // Avvia l'attivazione post-movimento
            if (postMovementActivation != null && 
                postMovementActivation.objectsToActivate != null && 
                postMovementActivation.objectsToActivate.Length > 0)
            {
                postMovementCoroutine = StartCoroutine(ExecutePostMovementActivation());
            }
        }
    }
    
    private IEnumerator ExecutePostMovementActivation()
    {
        // Attendi il delay se specificato
        if (postMovementActivation.activationDelay > 0)
        {
            yield return new WaitForSeconds(postMovementActivation.activationDelay);
        }

        // ✅ NASCONDI IMMEDIATAMENTE I NEMICI (prima di tutto il resto)
        if (postMovementActivation.enemiesToHide != null)
        {
            foreach (GameObject enemy in postMovementActivation.enemiesToHide)
            {
                if (enemy != null)
                {
                    enemy.SetActive(false);
                    if (debugMovement)
                        Debug.Log($"[DreamWaveTrigger] Post-movimento: Nemico '{enemy.name}' nascosto immediatamente");
                }
            }
        }

        // Attiva tutti gli oggetti
        foreach (GameObject obj in postMovementActivation.objectsToActivate)
        {
            if (obj != null)
            {
                obj.SetActive(true);
                if (debugMovement)
                    Debug.Log($"[DreamWaveTrigger] Post-movimento: Oggetto '{obj.name}' attivato");
            }
        }
        
        // ✅ REBAKE NAVMESH SE RICHIESTO
        if (postMovementActivation.rebakeNavMesh && 
            postMovementActivation.navMeshSurfaces != null && 
            postMovementActivation.navMeshSurfaces.Length > 0)
        {
            if (postMovementActivation.rebakeDelay > 0)
            {
                yield return new WaitForSeconds(postMovementActivation.rebakeDelay);
            }
            
            RebakeNavMeshSurfaces();
        }
        
        // Riproduci audio se presente
        if (postMovementActivation.activationAudio != null)
        {
            PlayPostMovementAudio();
        }
        
        postMovementCoroutine = null;
        
        if (debugMovement)
            Debug.Log("[DreamWaveTrigger] Attivazione post-movimento completata");
    }
    
    /// <summary>
    /// Esegue il rebake di tutte le NavMeshSurface assegnate
    /// </summary>
    private void RebakeNavMeshSurfaces()
    {
        if (postMovementActivation.navMeshSurfaces == null ||
            postMovementActivation.navMeshSurfaces.Length == 0)
        {
            Debug.LogWarning("[DreamWaveTrigger] Nessuna NavMeshSurface assegnata per il rebake!");
            return;
        }

        int successCount = 0;
        int failCount = 0;

        foreach (NavMeshSurface surface in postMovementActivation.navMeshSurfaces)
        {
            if (surface == null)
            {
                Debug.LogWarning("[DreamWaveTrigger] NavMeshSurface null nell'array, skip...");
                failCount++;
                continue;
            }

            if (!surface.gameObject.activeInHierarchy)
            {
                Debug.LogWarning($"[DreamWaveTrigger] NavMeshSurface '{surface.name}' non è attiva, skip...");
                failCount++;
                continue;
            }

            try
            {
                if (debugMovement)
                    Debug.Log($"[DreamWaveTrigger] Rebake NavMesh su '{surface.name}'...");

                // Usa UpdateNavMesh invece di BuildNavMesh per evitare problemi con mesh non leggibili
                if (surface.navMeshData != null)
                {
                    surface.UpdateNavMesh(surface.navMeshData);
                }
                else
                {
                    surface.BuildNavMesh();
                }

                successCount++;

                if (debugMovement)
                    Debug.Log($"[DreamWaveTrigger] ✅ NavMesh '{surface.name}' rebakata con successo");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[DreamWaveTrigger] ⚠️ Errore rebake NavMesh '{surface.name}': {e.Message}");
                Debug.LogWarning($"[DreamWaveTrigger] Tento rebuild completo...");

                // Fallback: prova a ricostruire da zero
                try
                {
                    surface.BuildNavMesh();
                    successCount++;
                    if (debugMovement)
                        Debug.Log($"[DreamWaveTrigger] ✅ NavMesh '{surface.name}' ricostruita con fallback");
                }
                catch
                {
                    Debug.LogError($"[DreamWaveTrigger] ❌ Impossibile rebakare NavMesh '{surface.name}'");
                    failCount++;
                }
            }
        }

        if (debugMovement)
            Debug.Log($"[DreamWaveTrigger] Rebake completato: {successCount} successi, {failCount} fallimenti");

        // ✅ RESPAWN ENEMY AGENTS: Distrugge e rispawna i nemici vicino ai loro waypoint dopo il rebake
       StartCoroutine(RespawnEnemiesAfterRebake());
    }

    private IEnumerator RespawnEnemiesAfterRebake()
{
    // Attendi un frame per permettere alla NavMesh di stabilizzarsi
    yield return new WaitForEndOfFrame();
    yield return new WaitForSeconds(0.3f);

    if (debugMovement)
        Debug.Log("[DreamWaveTrigger] Inizio respawn enemy agents...");

    List<EnemyRespawnData> enemiesToRespawn = new List<EnemyRespawnData>();

    NavMeshAgent[] allAgents = FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None);

    foreach (NavMeshAgent agent in allAgents)
    {
        if (agent == null || agent.gameObject == null)
            continue;

        var slime = agent.GetComponent<Slime>();
        if (slime != null && slime.waypoints != null && slime.waypoints.Length > 0)
        {
            enemiesToRespawn.Add(new EnemyRespawnData
            {
                enemyType = EnemyType.Slime,
                originalObject = agent.gameObject,
                waypoints = slime.waypoints,
                parent = agent.transform.parent
            });
            continue;
        }

        var turtle = agent.GetComponent<TurtleShell>();
        if (turtle != null && turtle.waypoints != null && turtle.waypoints.Length > 0)
        {
            enemiesToRespawn.Add(new EnemyRespawnData
            {
                enemyType = EnemyType.TurtleShell,
                originalObject = agent.gameObject,
                waypoints = turtle.waypoints,
                parent = agent.transform.parent
            });
            continue;
        }

        var mushroom = agent.GetComponent<Mushroom>();
        if (mushroom != null && mushroom.waypoints != null && mushroom.waypoints.Length > 0)
        {
            enemiesToRespawn.Add(new EnemyRespawnData
            {
                enemyType = EnemyType.Mushroom,
                originalObject = agent.gameObject,
                waypoints = mushroom.waypoints,
                parent = agent.transform.parent
            });
            continue;
        }
    }

    if (debugMovement)
        Debug.Log($"[DreamWaveTrigger] Trovati {enemiesToRespawn.Count} nemici da respawnare");

    int respawnedCount = 0;

    foreach (var enemyData in enemiesToRespawn)
    {
        if (enemyData.waypoints == null || enemyData.waypoints.Length == 0)
            continue;

        Transform closestWaypoint = FindClosestWaypoint(enemyData.originalObject.transform.position, enemyData.waypoints);

        if (closestWaypoint == null)
        {
            if (debugMovement)
                Debug.LogWarning($"[DreamWaveTrigger] ⚠️ Nessun waypoint trovato per '{enemyData.originalObject.name}'");
            continue;
        }

        // Trova posizione valida sulla NavMesh
        Vector3 spawnPosition = closestWaypoint.position;
        NavMeshHit navHit;
        bool foundValidPosition = false;

        float[] searchDistances = { 5f, 10f, 20f, 50f };
        foreach (float distance in searchDistances)
        {
            if (NavMesh.SamplePosition(closestWaypoint.position, out navHit, distance, NavMesh.AllAreas))
            {
                spawnPosition = navHit.position;
                foundValidPosition = true;
                break;
            }
        }

        if (!foundValidPosition)
        {
            if (debugMovement)
                Debug.LogWarning($"[DreamWaveTrigger] ⚠️ Nessuna NavMesh valida per '{enemyData.originalObject.name}'");
            continue;
        }

        string enemyName = enemyData.originalObject.name;
        Quaternion enemyRotation = enemyData.originalObject.transform.rotation;
        Transform enemyParent = enemyData.parent;

        // ✅ DISABILITA l'agent PRIMA di clonare per evitare che il clone parta con agent attivo
        NavMeshAgent originalAgent = enemyData.originalObject.GetComponent<NavMeshAgent>();
        if (originalAgent != null)
        {
            originalAgent.enabled = false;
        }

        // ✅ Crea il clone - sarà creato con NavMeshAgent DISABILITATO
        GameObject newEnemy = Instantiate(enemyData.originalObject, spawnPosition, enemyRotation, enemyParent);
        newEnemy.name = enemyName;

        // Distruggi il vecchio
        Destroy(enemyData.originalObject);

        // ✅ NON TOCCARE IL NAVMESHAGENT QUI!
        // Lo script del nemico (Slime/Mushroom/TurtleShell) ha già SetDestinationWhenReady()
        // che gestirà l'abilitazione quando sarà pronto
        
        // ✅ Invece, forza la riabilitazione dopo un delay più lungo
        StartCoroutine(EnableAgentDelayed(newEnemy, spawnPosition, enemyName));

        respawnedCount++;
    }

    if (debugMovement)
        Debug.Log($"[DreamWaveTrigger] Respawn completato: {respawnedCount}/{enemiesToRespawn.Count} nemici respawnati");
}
/// <summary>
/// Abilita il NavMeshAgent dopo un delay, assicurandosi che sia sulla NavMesh
/// </summary>
private IEnumerator EnableAgentDelayed(GameObject enemy, Vector3 targetPosition, string enemyName)
{
    if (enemy == null) yield break;
    
    // Attendi che la fisica si stabilizzi
    yield return new WaitForSeconds(0.5f);
    
    if (enemy == null) yield break;
    
    NavMeshAgent agent = enemy.GetComponent<NavMeshAgent>();
    if (agent == null) yield break;
    
    // Verifica posizione sulla NavMesh
    NavMeshHit hit;
    if (NavMesh.SamplePosition(enemy.transform.position, out hit, 5f, NavMesh.AllAreas))
    {
        // Sposta alla posizione esatta PRIMA di abilitare
        enemy.transform.position = hit.position;
        
        // Attendi un altro frame
        yield return null;
        
        // Ora abilita
        agent.enabled = true;
        
        // Warp se necessario
        if (agent.isOnNavMesh)
        {
            agent.Warp(hit.position);
            
            if (debugMovement)
                Debug.Log($"[DreamWaveTrigger] ✅ Agent '{enemyName}' abilitato con successo");
        }
        else
        {
            if (debugMovement)
                Debug.LogWarning($"[DreamWaveTrigger] ⚠️ Agent '{enemyName}' abilitato ma non su NavMesh");
        }
    }
    else
    {
        if (debugMovement)
            Debug.LogWarning($"[DreamWaveTrigger] ⚠️ Nessuna NavMesh vicina per '{enemyName}'");
    }
}

    /// <summary>
    /// Trova il waypoint più vicino a una posizione data
    /// </summary>
    private Transform FindClosestWaypoint(Vector3 position, Transform[] waypoints)
    {
        Transform closest = null;
        float minDistance = float.MaxValue;

        foreach (Transform waypoint in waypoints)
        {
            if (waypoint == null)
                continue;

            float distance = Vector3.Distance(position, waypoint.position);
            if (distance < minDistance)
            {
                minDistance = distance;
                closest = waypoint;
            }
        }

        return closest;
    }

    /// <summary>
    /// Tipo di nemico per il respawn
    /// </summary>
    private enum EnemyType
    {
        Slime,
        TurtleShell,
        Mushroom
    }

    /// <summary>
    /// Dati necessari per respawnare un nemico dopo il rebake
    /// </summary>
    private class EnemyRespawnData
    {
        public EnemyType enemyType;
        public GameObject originalObject;
        public Transform[] waypoints;
        public Transform parent;
    }

    private void PlayPostMovementAudio()
    {
        // Determina la posizione dell'audio
        Vector3 audioPosition = transform.position;
        
        if (postMovementActivation.audioSourcePosition != null)
        {
            audioPosition = postMovementActivation.audioSourcePosition.position;
        }
        else if (postMovementActivation.objectsToActivate != null && 
                 postMovementActivation.objectsToActivate.Length > 0 &&
                 postMovementActivation.objectsToActivate[0] != null)
        {
            audioPosition = postMovementActivation.objectsToActivate[0].transform.position;
        }
        
        if (postMovementActivation.spatializedAudio)
        {
            // Audio 3D spazializzato
            AudioSource.PlayClipAtPoint(
                postMovementActivation.activationAudio, 
                audioPosition, 
                postMovementActivation.audioVolume
            );
            
            if (debugMovement)
                Debug.Log($"[DreamWaveTrigger] Audio 3D riprodotto a {audioPosition}");
        }
        else
        {
            // Audio 2D - crea una sorgente temporanea
            GameObject tempAudio = new GameObject("PostMovementAudio");
            AudioSource tempSource = tempAudio.AddComponent<AudioSource>();
            tempSource.clip = postMovementActivation.activationAudio;
            tempSource.volume = postMovementActivation.audioVolume;
            tempSource.spatialBlend = 0f; // 2D
            tempSource.Play();
            
            // Distruggi dopo la riproduzione
            Destroy(tempAudio, postMovementActivation.activationAudio.length + 0.1f);
            
            if (debugMovement)
                Debug.Log("[DreamWaveTrigger] Audio 2D riprodotto");
        }
    }
    
    private IEnumerator MoveObject(ObjectMovement movement)
    {
        if (movement.targetObject == null) yield break;
        
        // Attendi il delay iniziale
        if (movement.startDelay > 0)
        {
            yield return new WaitForSeconds(movement.startDelay);
        }
        
        movement.isMoving = true;
        
        // ✅ PREPARA I FIGLI SE RICHIESTO
        if (movement.forceChildrenFollow)
        {
            PrepareChildrenForMovement(movement);
        }
        
        // Calcola posizioni
        if (movement.useWorldSpace)
        {
            movement.startPosition = movement.targetObject.transform.position;
            
            if (movement.useAbsolutePosition)
            {
                movement.calculatedEndPosition = movement.targetPosition;
            }
            else
            {
                movement.calculatedEndPosition = movement.startPosition + movement.movementOffset;
            }
        }
        else
        {
            movement.startPosition = movement.targetObject.transform.localPosition;
            
            if (movement.useAbsolutePosition)
            {
                movement.calculatedEndPosition = movement.targetPosition;
            }
            else
            {
                movement.calculatedEndPosition = movement.startPosition + movement.movementOffset;
            }
        }
        
        // Determina la durata
        float duration = movement.duration > 0 ? movement.duration : earthquakeDuration;
        
        if (debugMovement)
        {
            string spaceType = movement.useWorldSpace ? "World" : "Local";
            string posType = movement.useAbsolutePosition ? "Assoluta" : "Relativa";
            Debug.Log($"[DreamWaveTrigger] Movimento '{movement.targetObject.name}': " +
                      $"{movement.startPosition} -> {movement.calculatedEndPosition} " +
                      $"({spaceType} Space, Posizione {posType}, Durata: {duration}s)" +
                      (movement.forceChildrenFollow ? $" [+{movement.savedChildData.Count} figli forzati]" : ""));
        }
        
        // Esegui il movimento
        float elapsedTime = 0f;
        
        while (elapsedTime < duration)
        {
            if (movement.targetObject == null) yield break;
            
            float normalizedTime = elapsedTime / duration;
            float curveValue = movement.movementCurve.Evaluate(normalizedTime);
            
            Vector3 newPosition = Vector3.Lerp(movement.startPosition, movement.calculatedEndPosition, curveValue);
            
            // Calcola il delta per gli oggetti spawnati
            Vector3 previousPosition = movement.useWorldSpace 
                ? movement.targetObject.transform.position 
                : movement.targetObject.transform.localPosition;
            
            if (movement.useWorldSpace)
            {
                movement.targetObject.transform.position = newPosition;
            }
            else
            {
                movement.targetObject.transform.localPosition = newPosition;
            }
            
            // Calcola il delta del movimento
            Vector3 delta = newPosition - previousPosition;

            // ✅ MUOVI ANCHE GLI OGGETTI SPAWNATI (se abilitato)
            if (movement.moveSpawnedObjectsWithSpline && movement.spawnedObjectsData.Count > 0)
            {
                foreach (var spawnedData in movement.spawnedObjectsData)
                {
                    if (spawnedData.spawnedObject != null)
                    {
                        spawnedData.spawnedObject.position += delta;
                    }
                }
            }
            // NOTA: Le gemme vengono distrutte prima del movimento e respawnate dopo (destroyAndRespawnGems)
            
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        
        // Assicura posizione finale esatta
        if (movement.targetObject != null)
        {
            if (movement.useWorldSpace)
            {
                movement.targetObject.transform.position = movement.calculatedEndPosition;
            }
            else
            {
                movement.targetObject.transform.localPosition = movement.calculatedEndPosition;
            }
        }
        
        // ✅ RIPRISTINA I FIGLI SE ERANO STATI PREPARATI
        if (movement.forceChildrenFollow)
        {
            RestoreChildrenAfterMovement(movement);
        }
        
        movement.isMoving = false;
        
        if (debugMovement)
            Debug.Log($"[DreamWaveTrigger] Movimento completato per '{movement.targetObject?.name}'");
        
        // Notifica il completamento
        OnMovementCompleted();
    }
    
    /// <summary>
    /// Prepara i figli per il movimento forzando il kinematic sui rigidbody
    /// </summary>
    private void PrepareChildrenForMovement(ObjectMovement movement)
    {
        movement.savedChildData.Clear();
        movement.spawnedObjectsData.Clear();
        movement.gemSpawnersToRespawn.Clear();

        // ✅ DISTRUGGI LE GEMME PRIMA DEL MOVIMENTO (verranno respawnate dopo)
        if (movement.destroyAndRespawnGems)
        {
            DestroyGemsBeforeMovement(movement);
        }
        
        Transform[] children;
        
        if (movement.includeNestedChildren)
        {
            // Prendi tutti i figli ricorsivamente
            children = movement.targetObject.GetComponentsInChildren<Transform>(true);
        }
        else
        {
            // Prendi solo i figli diretti
            children = new Transform[movement.targetObject.transform.childCount];
            for (int i = 0; i < movement.targetObject.transform.childCount; i++)
            {
                children[i] = movement.targetObject.transform.GetChild(i);
            }
        }
        
        foreach (Transform child in children)
        {
            // Salta il parent stesso
            if (child == movement.targetObject.transform) continue;
            
            var data = new ChildTransformData
            {
                child = child,
                originalParent = child.parent,
                originalLocalPosition = child.localPosition,
                originalLocalRotation = child.localRotation,
                originalLocalScale = child.localScale
            };
            
            // Gestisci Rigidbody - forza kinematic per evitare problemi
            Rigidbody rb = child.GetComponent<Rigidbody>();
            if (rb != null)
            {
                data.rigidbody = rb;
                data.wasKinematic = rb.isKinematic;
                rb.isKinematic = true;
                
                if (debugMovement)
                    Debug.Log($"[DreamWaveTrigger] Rigidbody di '{child.name}' impostato a kinematic");
            }
            
            // Gestisci Animator - disabilita temporaneamente se applica root motion o modifica transform
            Animator anim = child.GetComponent<Animator>();
            if (anim != null)
            {
                data.animator = anim;
                data.animatorWasEnabled = anim.enabled;
                
                // Disabilita solo se l'animator potrebbe interferire
                if (anim.applyRootMotion || anim.updateMode == AnimatorUpdateMode.Fixed)
                {
                    anim.enabled = false;
                    if (debugMovement)
                        Debug.Log($"[DreamWaveTrigger] Animator di '{child.name}' disabilitato temporaneamente");
                }
            }
            
            movement.savedChildData.Add(data);
        }
        
        // ✅ DISABILITA FLOATING SU TUTTI I COLLECTIBLES durante il movimento
        DisableFloatingOnCollectibles(movement.targetObject);
        
        // ✅ GESTIONE OGGETTI SPAWNATI (gemme, etc.)
        if (movement.moveSpawnedObjectsWithSpline && !string.IsNullOrEmpty(movement.spawnedObjectsTag))
        {
            PrepareSpawnedObjects(movement);
        }
        
        if (debugMovement)
            Debug.Log($"[DreamWaveTrigger] Preparati {movement.savedChildData.Count} figli + {movement.spawnedObjectsData.Count} oggetti spawnati + {movement.gemSpawnersToRespawn.Count} GemSpawnerSpline per il movimento di '{movement.targetObject.name}'");
    }

    /// <summary>
    /// Distrugge le gemme prima del movimento e salva i GemSpawnerSpline per il respawn
    /// </summary>
    private void DestroyGemsBeforeMovement(ObjectMovement movement)
    {
        if (movement.targetObject == null) return;

        // Cerca tutti i GemSpawnerSpline nei figli (e nel target stesso)
        GemSpawnerSpline[] spawners = movement.targetObject.GetComponentsInChildren<GemSpawnerSpline>(true);

        foreach (GemSpawnerSpline spawner in spawners)
        {
            if (spawner == null) continue;

            // Salva il riferimento per il respawn dopo il movimento
            movement.gemSpawnersToRespawn.Add(spawner);

            // Distruggi immediatamente tutte le gemme
            spawner.ImmediateClearAllGems();

            if (debugMovement)
                Debug.Log($"[DreamWaveTrigger] 🗑️ Gemme distrutte per '{spawner.name}' - verranno respawnate dopo il movimento");
        }

        if (debugMovement && spawners.Length > 0)
            Debug.Log($"[DreamWaveTrigger] Distrutte gemme di {spawners.Length} GemSpawnerSpline");
    }
    
    /// <summary>
    /// Disabilita temporaneamente il floating sui Collectibles per permettere il movimento
    /// </summary>
    private void DisableFloatingOnCollectibles(GameObject target)
    {
        if (target == null) return;
        
        Collectibles[] allCollectibles = target.GetComponentsInChildren<Collectibles>(true);
        
        foreach (Collectibles collectible in allCollectibles)
        {
            if (collectible != null)
            {
                // Usa reflection per disabilitare enableFloating
                var field = collectible.GetType().GetField("enableFloating", 
                    System.Reflection.BindingFlags.NonPublic | 
                    System.Reflection.BindingFlags.Public | 
                    System.Reflection.BindingFlags.Instance);
                
                if (field == null)
                {
                    field = typeof(Collectibles).GetField("enableFloating", 
                        System.Reflection.BindingFlags.NonPublic | 
                        System.Reflection.BindingFlags.Public | 
                        System.Reflection.BindingFlags.Instance);
                }
                
                if (field != null)
                {
                    field.SetValue(collectible, false);
                    if (debugMovement)
                        Debug.Log($"[DreamWaveTrigger] ✅ Floating disabilitato per '{collectible.name}'");
                }
            }
        }
        
        // Fai lo stesso per le Gem
        Gem[] allGems = target.GetComponentsInChildren<Gem>(true);
        
        foreach (Gem gem in allGems)
        {
            if (gem != null)
            {
                // Le Gem non hanno enableFloating ma aggiornano sempre la Y in Update
                // Non possiamo disabilitarle facilmente, ma l'aggiornamento di startPos dovrebbe bastare
                if (debugMovement)
                    Debug.Log($"[DreamWaveTrigger] Gem '{gem.name}' trovata (startPos sarà aggiornato dopo movimento)");
            }
        }
    }
    
    /// <summary>
    /// Trova e prepara gli oggetti spawnati (es. gemme) per muoverli insieme alla spline
    /// </summary>
    private void PrepareSpawnedObjects(ObjectMovement movement)
    {
        // Trova tutti gli oggetti con il tag specificato
        GameObject[] taggedObjects = GameObject.FindGameObjectsWithTag(movement.spawnedObjectsTag);
        
        Vector3 parentPos = movement.targetObject.transform.position;
        
        foreach (GameObject obj in taggedObjects)
        {
            if (obj == null) continue;
            
            // Controlla se l'oggetto è nel raggio di ricerca
            float distance = Vector3.Distance(obj.transform.position, parentPos);
            if (distance <= movement.spawnedObjectsSearchRadius)
            {
                var data = new SpawnedObjectData
                {
                    spawnedObject = obj.transform,
                    offsetFromParent = obj.transform.position - parentPos
                };
                
                movement.spawnedObjectsData.Add(data);
                
                if (debugMovement)
                    Debug.Log($"[DreamWaveTrigger] Oggetto spawnato '{obj.name}' aggiunto al movimento (distanza: {distance:F1})");
            }
        }
    }
    
    /// <summary>
    /// Ripristina i figli dopo il movimento
    /// </summary>
    private void RestoreChildrenAfterMovement(ObjectMovement movement)
    {
        foreach (var data in movement.savedChildData)
        {
            if (data.child == null) continue;
            
            // Ripristina il Rigidbody allo stato originale
            if (data.rigidbody != null)
            {
                data.rigidbody.isKinematic = data.wasKinematic;
                
                if (debugMovement && !data.wasKinematic)
                    Debug.Log($"[DreamWaveTrigger] Rigidbody di '{data.child.name}' ripristinato a non-kinematic");
            }
            
            // Ripristina l'Animator allo stato originale
            if (data.animator != null && data.animatorWasEnabled && !data.animator.enabled)
            {
                data.animator.enabled = true;
                
                if (debugMovement)
                    Debug.Log($"[DreamWaveTrigger] Animator di '{data.child.name}' riabilitato");
            }
        }
        
        movement.savedChildData.Clear();

        // ✅ RESPAWN GEMME dopo il movimento
        if (movement.destroyAndRespawnGems && movement.gemSpawnersToRespawn.Count > 0)
        {
            RespawnGemsAfterMovement(movement);
        }
        movement.gemSpawnersToRespawn.Clear();

        // ✅ RI-CAMPIONA LA SPLINE SE RICHIESTO (per MovingPlatform)
        if (movement.resampleSplineAfterMove)
        {
            ResampleSplineOnTarget(movement.targetObject);
        }

        if (debugMovement)
            Debug.Log($"[DreamWaveTrigger] Figli ripristinati per '{movement.targetObject?.name}'");
    }

    /// <summary>
    /// Respawna le gemme dopo che il movimento è completato
    /// </summary>
    private void RespawnGemsAfterMovement(ObjectMovement movement)
    {
        foreach (GemSpawnerSpline spawner in movement.gemSpawnersToRespawn)
        {
            if (spawner == null) continue;

            // Forza la reinizializzazione per spawnare le gemme nella nuova posizione
            spawner.ForceReinitialize();

            if (debugMovement)
                Debug.Log($"[DreamWaveTrigger] ✅ Gemme respawnate per '{spawner.name}' nella nuova posizione");
        }

        if (debugMovement)
            Debug.Log($"[DreamWaveTrigger] Respawnate gemme per {movement.gemSpawnersToRespawn.Count} GemSpawnerSpline");
    }
    
    /// <summary>
    /// Ri-campiona tutte le spline su MovingPlatform o GemSpawnerSpline nei figli
    /// </summary>
    private void ResampleSplineOnTarget(GameObject target)
    {
        if (target == null) return;
        
        // ✅ TROVA TUTTE LE MovingPlatform nei figli (ricorsivo)
        MovingPlatform[] allPlatforms = target.GetComponentsInChildren<MovingPlatform>(true);
        
        foreach (MovingPlatform platform in allPlatforms)
        {
            if (platform != null)
            {
                platform.SendMessage("SampleSpline", SendMessageOptions.DontRequireReceiver);
                
                if (debugMovement)
                    Debug.Log($"[DreamWaveTrigger] Richiesto re-sample spline per MovingPlatform '{platform.name}'");
            }
        }
        
        if (debugMovement && allPlatforms.Length > 0)
            Debug.Log($"[DreamWaveTrigger] Ri-campionate {allPlatforms.Length} MovingPlatform");
        
        // ✅ TROVA TUTTI I GemSpawnerSpline nei figli (ricorsivo)
        GemSpawnerSpline[] allSpawners = target.GetComponentsInChildren<GemSpawnerSpline>(true);
        
        foreach (GemSpawnerSpline spawner in allSpawners)
        {
            if (spawner != null)
            {
                // NON chiamiamo più ForceReinitialize perché le gemme sono già state mosse
                // insieme al parent durante il movimento
                
                if (debugMovement)
                    Debug.Log($"[DreamWaveTrigger] GemSpawnerSpline '{spawner.name}' trovato (gemme già spostate con parent)");
            }
        }
        
        if (debugMovement && allSpawners.Length > 0)
            Debug.Log($"[DreamWaveTrigger] Trovati {allSpawners.Length} GemSpawnerSpline");
        
        // ✅ AGGIORNA startPos DI TUTTE LE GEM nei figli (ricorsivo)
        UpdateAllGemsInScene(target);
        
        // ✅ AGGIORNA startPos PER TUTTI I COLLECTIBLES (Memories, etc.) nei figli
        UpdateCollectiblesStartPosition(target);
    }
    
    /// <summary>
    /// Aggiorna le gemme che sono figli del target (già spostate col parent)
    /// </summary>
    private void UpdateAllGemsInScene(GameObject target)
    {
        if (target == null) return;
        
        // Cerca SOLO le gemme nei figli del target
        Gem[] gemsInChildren = target.GetComponentsInChildren<Gem>(true);
        
        foreach (Gem gem in gemsInChildren)
        {
            if (gem != null)
            {
                UpdateStartPositionViaReflection(gem, "startPos");
                
                if (debugMovement)
                    Debug.Log($"[DreamWaveTrigger] ✅ Aggiornato startPos per Gem '{gem.name}'");
            }
        }
        
        if (debugMovement && gemsInChildren.Length > 0)
            Debug.Log($"[DreamWaveTrigger] Aggiornate {gemsInChildren.Length} Gem nei figli di '{target.name}'");
    }
    
    /// <summary>
    /// Aggiorna la startPos di tutti i Collectibles (Memories, etc.) nei figli dopo il movimento
    /// </summary>
    private void UpdateCollectiblesStartPosition(GameObject target)
    {
        if (target == null) return;
        
        // Cerca Collectibles (classe base)
        Collectibles[] allCollectibles = target.GetComponentsInChildren<Collectibles>(true);
        
        foreach (Collectibles collectible in allCollectibles)
        {
            if (collectible != null)
            {
                UpdateStartPositionViaReflection(collectible, "startY");
                UpdateStartPositionViaReflection(collectible, "startPosition");
                UpdateStartPositionViaReflection(collectible, "startPos");
                UpdateStartPositionViaReflection(collectible, "originalPosition");
                
                if (debugMovement)
                    Debug.Log($"[DreamWaveTrigger] Aggiornata posizione per Collectible '{collectible.name}'");
            }
        }
        
        // Cerca anche Memories specificamente
        Memories[] allMemories = target.GetComponentsInChildren<Memories>(true);
        
        foreach (Memories memory in allMemories)
        {
            if (memory != null)
            {
                UpdateStartPositionViaReflection(memory, "startY");
                UpdateStartPositionViaReflection(memory, "startPosition");
                UpdateStartPositionViaReflection(memory, "startPos");
                UpdateStartPositionViaReflection(memory, "originalPosition");
                
                if (debugMovement)
                    Debug.Log($"[DreamWaveTrigger] Aggiornata posizione per Memory '{memory.name}'");
            }
        }
        
        if (debugMovement && (allCollectibles.Length > 0 || allMemories.Length > 0))
            Debug.Log($"[DreamWaveTrigger] Aggiornati {allCollectibles.Length} Collectibles e {allMemories.Length} Memories");
    }
    
    /// <summary>
    /// Usa reflection per aggiornare un campo Vector3 o float di posizione
    /// </summary>
    private void UpdateStartPositionViaReflection(MonoBehaviour target, string fieldName)
    {
        if (target == null) return;
        
        System.Type type = target.GetType();
        
        // Cerca in questa classe e nelle classi base
        while (type != null && type != typeof(MonoBehaviour))
        {
            // ✅ BINDING FLAGS COMPLETI per trovare campi protected, private e public
            System.Reflection.FieldInfo field = type.GetField(fieldName, 
                System.Reflection.BindingFlags.NonPublic | 
                System.Reflection.BindingFlags.Public | 
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.FlattenHierarchy);
            
            if (field != null)
            {
                if (field.FieldType == typeof(Vector3))
                {
                    Vector3 oldValue = (Vector3)field.GetValue(target);
                    Vector3 newValue = target.transform.position;
                    field.SetValue(target, newValue);
                    
                    if (debugMovement)
                        Debug.Log($"[DreamWaveTrigger] ✅ Campo '{fieldName}' (Vector3) aggiornato: {oldValue} -> {newValue} per '{target.name}'");
                    return;
                }
                else if (field.FieldType == typeof(float))
                {
                    // Per campi come startY, aggiorna con la Y corrente
                    float oldValue = (float)field.GetValue(target);
                    float newValue = target.transform.position.y;
                    field.SetValue(target, newValue);
                    
                    if (debugMovement)
                        Debug.Log($"[DreamWaveTrigger] ✅ Campo '{fieldName}' (float) aggiornato: {oldValue} -> {newValue} per '{target.name}'");
                    return;
                }
            }
            
            type = type.BaseType;
        }
        
        // ✅ FALLBACK: Prova anche con GetFields su tutta la gerarchia
        type = target.GetType();
        while (type != null && type != typeof(UnityEngine.Object))
        {
            var allFields = type.GetFields(
                System.Reflection.BindingFlags.NonPublic | 
                System.Reflection.BindingFlags.Public | 
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.DeclaredOnly);
            
            foreach (var field in allFields)
            {
                if (field.Name == fieldName)
                {
                    if (field.FieldType == typeof(Vector3))
                    {
                        Vector3 oldValue = (Vector3)field.GetValue(target);
                        Vector3 newValue = target.transform.position;
                        field.SetValue(target, newValue);
                        
                        if (debugMovement)
                            Debug.Log($"[DreamWaveTrigger] ✅ Campo '{fieldName}' (Vector3) trovato con fallback: {oldValue} -> {newValue} per '{target.name}'");
                        return;
                    }
                    else if (field.FieldType == typeof(float))
                    {
                        float oldValue = (float)field.GetValue(target);
                        float newValue = target.transform.position.y;
                        field.SetValue(target, newValue);
                        
                        if (debugMovement)
                            Debug.Log($"[DreamWaveTrigger] ✅ Campo '{fieldName}' (float) trovato con fallback: {oldValue} -> {newValue} per '{target.name}'");
                        return;
                    }
                }
            }
            
            type = type.BaseType;
        }
    }
    
    private void StopAllMovements()
    {
        foreach (var coroutine in movementCoroutines)
        {
            if (coroutine != null)
            {
                StopCoroutine(coroutine);
            }
        }
        movementCoroutines.Clear();
        
        if (postMovementCoroutine != null)
        {
            StopCoroutine(postMovementCoroutine);
            postMovementCoroutine = null;
        }
        
        if (objectsToMove != null)
        {
            foreach (var movement in objectsToMove)
            {
                movement.isMoving = false;
            }
        }
        
        activeMovementsCount = 0;
        isMovingObjects = false;
    }

    private IEnumerator DissolveObjects()
    {
        if (debugDissolve)
            Debug.Log($"[DreamWaveTrigger] Avvio dissolve per {validMaterials.Count} materiali e {objectsToDissolve.Length} oggetti");
        
        isDissolving = true;
        float elapsedTime = 0f;
        bool objectsDeactivated = false;
        
        float duration = dissolveDuration;
        if (mainAudio != null && !loopAudio)
        {
            duration = Mathf.Min(dissolveDuration, mainAudio.length);
        }
        
        while (elapsedTime < duration)
        {
            float normalizedTime = elapsedTime / duration;
            float dissolveValue = dissolveCurve.Evaluate(normalizedTime);
            
            foreach (Material material in validMaterials)
            {
                if (material != null)
                {
                    material.SetFloat(dissolvePropertyName, dissolveValue);
                }
            }
            
            if (!objectsDeactivated && dissolveValue >= 0.95f)
            {
                if (objectsToDissolve != null)
                {
                    foreach (GameObject obj in objectsToDissolve)
                    {
                        if (obj != null)
                        {
                            obj.SetActive(false);
                            if (debugDissolve) Debug.Log($"[DreamWaveTrigger] Oggetto '{obj.name}' disattivato");
                        }
                    }
                }
                
                if (playerController != null)
                {
                    Animator playerAnimator = playerController.GetComponent<Animator>();
                    if (playerAnimator != null)
                    {
                        playerAnimator.SetTrigger("Falling");
                        if (debugMode) Debug.Log("[DreamWaveTrigger] Trigger 'Falling' attivato sul player");
                    }
                    else
                    {
                        Debug.LogWarning("[DreamWaveTrigger] Animator non trovato sul player per attivare trigger 'Falling'");
                    }
                }
                
                if (objectsToDeactivate != null && objectsToDeactivate.Length > 0)
                {
                    if (deactivationDelay > 0)
                    {
                        StartCoroutine(DeactivateObjectsWithDelay());
                    }
                    else
                    {
                        DeactivateObjects();
                    }
                }
                
                if (objectsToActivate != null && objectsToActivate.Length > 0)
                {
                    if (activationDelay > 0)
                    {
                        StartCoroutine(ActivateObjectsWithDelay());
                    }
                    else
                    {
                        ActivateObjects();
                    }
                }
                
                objectsDeactivated = true;
                if (debugDissolve) Debug.Log("[DreamWaveTrigger] Oggetti gestiti al 95% del dissolve");
            }
            
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        
        foreach (Material material in validMaterials)
        {
            if (material != null)
            {
                material.SetFloat(dissolvePropertyName, 1f);
            }
        }
        
        if (!objectsDeactivated)
        {
            if (objectsToDissolve != null)
            {
                foreach (GameObject obj in objectsToDissolve)
                {
                    if (obj != null)
                    {
                        obj.SetActive(false);
                        if (debugDissolve) Debug.Log($"[DreamWaveTrigger] Oggetto '{obj.name}' disattivato (fallback)");
                    }
                }
            }
            
            if (playerController != null)
            {
                Animator playerAnimator = playerController.GetComponent<Animator>();
                if (playerAnimator != null)
                {
                    playerAnimator.SetTrigger("Falling");
                    if (debugMode) Debug.Log("[DreamWaveTrigger] Trigger 'Falling' attivato sul player (fallback)");
                }
            }
            
            if (objectsToDeactivate != null && objectsToDeactivate.Length > 0)
            {
                if (deactivationDelay > 0)
                {
                    StartCoroutine(DeactivateObjectsWithDelay());
                }
                else
                {
                    DeactivateObjects();
                }
            }
            
            if (objectsToActivate != null && objectsToActivate.Length > 0)
            {
                if (activationDelay > 0)
                {
                    StartCoroutine(ActivateObjectsWithDelay());
                }
                else
                {
                    ActivateObjects();
                }
            }
            
            if (debugDissolve) Debug.Log("[DreamWaveTrigger] Oggetti gestiti (fallback finale)");
        }
        
        if (enableCameraShake && earthquakeShakeController != null)
        {
            if (shakeStopDelay > 0)
            {
                yield return new WaitForSeconds(shakeStopDelay);
            }
            earthquakeShakeController.StopShake();
            if (debugMode) Debug.Log("[DreamWaveTrigger] Camera shake fermato dopo dissolve");
        }
        
        isDissolving = false;
        dissolveCoroutine = null;
        
        if (debugDissolve)
            Debug.Log("[DreamWaveTrigger] Dissolve completato e shake fermato");
    }

    private void ActivateObjects()
    {
        if (objectsToActivate != null)
        {
            foreach (GameObject obj in objectsToActivate)
            {
                if (obj != null)
                {
                    obj.SetActive(true);
                    if (debugMode) Debug.Log($"[DreamWaveTrigger] Oggetto '{obj.name}' attivato");
                }
            }
        }
    }
    
    private void DeactivateObjects()
    {
        if (objectsToDeactivate != null)
        {
            foreach (GameObject obj in objectsToDeactivate)
            {
                if (obj != null)
                {
                    obj.SetActive(false);
                    if (debugMode) Debug.Log($"[DreamWaveTrigger] Oggetto '{obj.name}' disattivato");
                }
            }
        }
    }
    
    private IEnumerator ActivateObjectsWithDelay()
    {
        yield return new WaitForSeconds(activationDelay);
        
        ActivateObjects();
        
        if (debugMode) 
            Debug.Log($"[DreamWaveTrigger] {objectsToActivate?.Length ?? 0} oggetti attivati dopo {activationDelay}s di delay");
    }
    
    private IEnumerator DeactivateObjectsWithDelay()
    {
        yield return new WaitForSeconds(deactivationDelay);
        
        DeactivateObjects();
        
        if (debugMode) 
            Debug.Log($"[DreamWaveTrigger] {objectsToDeactivate?.Length ?? 0} oggetti disattivati dopo {deactivationDelay}s di delay");
    }

    private void EndSequence()
    {
        if (debugMode)
            Debug.Log("[DreamWaveTrigger] Sequenza terminata");
        
        if (disablePlayerMovement && playerController != null)
        {
            playerController.IsMovementLocked = false;
            if (debugMode) Debug.Log("[DreamWaveTrigger] Movimento player riabilitato");
        }
        
        if (audioSource != null)
        {
            audioSource.Stop();
        }
        
        if (enableCameraShake && earthquakeShakeController != null && earthquakeShakeController.IsShaking)
        {
            earthquakeShakeController.StopShake();
        }
        
        isAudioPlaying = false;
        isMovingObjects = false;
        mainCoroutine = null;
        playerController = null;
    }

    // ========================
    // METODI PUBBLICI LEGACY
    // ========================
    
    [System.Obsolete("Usa TriggerSequenceManually() invece")]
    public void TriggerSequence()
    {
        TriggerSequenceManually();
    }
    
    public void StopSequence()
    {
        if (mainCoroutine != null)
        {
            StopCoroutine(mainCoroutine);
            EndSequence();
        }
        
        if (dissolveCoroutine != null)
        {
            StopCoroutine(dissolveCoroutine);
            dissolveCoroutine = null;
        }
        
        StopAllMovements();
        
        if (isAudioPlaying)
        {
            isAudioPlaying = false;
            if (audioSource != null) audioSource.Stop();
        }
        
        if (isDissolving)
        {
            isDissolving = false;
            ResetDissolveValues();
        }
    }
    
    public void ResetTrigger()
    {
        hasTriggered = false;
        ResetDissolveValues();
        ResetObjectPositions();
        if (debugMode)
            Debug.Log("[DreamWaveTrigger] Trigger resettato");
    }
    
    public void ResetDissolveValues()
    {
        foreach (Material material in validMaterials)
        {
            if (material != null)
            {
                material.SetFloat(dissolvePropertyName, 0f);
            }
        }
        
        if (objectsToDissolve != null)
        {
            foreach (GameObject obj in objectsToDissolve)
            {
                if (obj != null)
                {
                    obj.SetActive(true);
                }
            }
        }
        
        if (objectsToActivate != null)
        {
            foreach (GameObject obj in objectsToActivate)
            {
                if (obj != null)
                {
                    obj.SetActive(false);
                }
            }
        }
        
        if (objectsToDeactivate != null)
        {
            foreach (GameObject obj in objectsToDeactivate)
            {
                if (obj != null)
                {
                    obj.SetActive(true);
                }
            }
        }
        
        if (debugDissolve)
            Debug.Log("[DreamWaveTrigger] Valori dissolve resettati e oggetti ripristinati");
    }
    
    /// <summary>
    /// Resetta gli oggetti alle loro posizioni iniziali salvate
    /// </summary>
    public void ResetObjectPositions()
    {
        if (objectsToMove != null)
        {
            foreach (var movement in objectsToMove)
            {
                if (movement.targetObject != null && movement.startPosition != Vector3.zero)
                {
                    if (movement.useWorldSpace)
                    {
                        movement.targetObject.transform.position = movement.startPosition;
                    }
                    else
                    {
                        movement.targetObject.transform.localPosition = movement.startPosition;
                    }
                    
                    if (debugMovement)
                        Debug.Log($"[DreamWaveTrigger] Posizione resettata per '{movement.targetObject.name}'");
                }
            }
        }
        
        // Resetta anche gli oggetti attivati post-movimento
        if (postMovementActivation != null && postMovementActivation.objectsToActivate != null)
        {
            foreach (GameObject obj in postMovementActivation.objectsToActivate)
            {
                if (obj != null)
                {
                    obj.SetActive(false);
                    if (debugMovement)
                        Debug.Log($"[DreamWaveTrigger] Oggetto post-movimento '{obj.name}' disattivato");
                }
            }
        }
    }
    
    // ✅ METODI DI UTILITÀ PER GESTIRE ARRAY DINAMICAMENTE
    public void AddObjectToDissolve(GameObject obj)
    {
        if (obj == null) return;
        
        var list = new List<GameObject>();
        if (objectsToDissolve != null)
            list.AddRange(objectsToDissolve);
        
        if (!list.Contains(obj))
        {
            list.Add(obj);
            objectsToDissolve = list.ToArray();
            if (debugMode) Debug.Log($"[DreamWaveTrigger] Oggetto '{obj.name}' aggiunto alla lista dissolve");
        }
    }
    
    public void AddMaterialToDissolve(Material material)
    {
        if (material == null) return;
        
        var list = new List<Material>();
        if (materialsToDissolve != null)
            list.AddRange(materialsToDissolve);
        
        if (!list.Contains(material))
        {
            list.Add(material);
            materialsToDissolve = list.ToArray();
            
            if (material.HasProperty(dissolvePropertyName))
            {
                material.SetFloat(dissolvePropertyName, 0f);
                if (!validMaterials.Contains(material))
                {
                    validMaterials.Add(material);
                }
                if (debugMode) Debug.Log($"[DreamWaveTrigger] Materiale '{material.name}' aggiunto alla lista dissolve");
            }
            else
            {
                Debug.LogWarning($"[DreamWaveTrigger] Il materiale '{material.name}' non ha la proprietà '{dissolvePropertyName}'");
            }
        }
    }
    
    public void AddObjectToActivate(GameObject obj)
    {
        if (obj == null) return;
        
        var list = new List<GameObject>();
        if (objectsToActivate != null)
            list.AddRange(objectsToActivate);
        
        if (!list.Contains(obj))
        {
            list.Add(obj);
            objectsToActivate = list.ToArray();
            if (debugMode) Debug.Log($"[DreamWaveTrigger] Oggetto '{obj.name}' aggiunto alla lista attivazione");
        }
    }
    
    public void AddObjectToDeactivate(GameObject obj)
    {
        if (obj == null) return;
        
        var list = new List<GameObject>();
        if (objectsToDeactivate != null)
            list.AddRange(objectsToDeactivate);
        
        if (!list.Contains(obj))
        {
            list.Add(obj);
            objectsToDeactivate = list.ToArray();
            if (debugMode) Debug.Log($"[DreamWaveTrigger] Oggetto '{obj.name}' aggiunto alla lista disattivazione");
        }
    }
    
    /// <summary>
    /// Aggiunge un nuovo movimento oggetto alla lista
    /// </summary>
    public void AddObjectMovement(ObjectMovement movement)
    {
        if (movement == null || movement.targetObject == null) return;
        
        var list = new List<ObjectMovement>();
        if (objectsToMove != null)
            list.AddRange(objectsToMove);
        
        list.Add(movement);
        objectsToMove = list.ToArray();
        
        if (debugMovement) 
            Debug.Log($"[DreamWaveTrigger] Movimento per '{movement.targetObject.name}' aggiunto");
    }
    
    /// <summary>
    /// Crea e aggiunge un movimento con offset relativo
    /// </summary>
    public void AddRelativeMovement(GameObject obj, Vector3 offset, float duration = 0f, float delay = 0f)
    {
        var movement = new ObjectMovement
        {
            targetObject = obj,
            useAbsolutePosition = false,
            movementOffset = offset,
            duration = duration,
            startDelay = delay,
            movementCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f)
        };
        
        AddObjectMovement(movement);
    }
    
    /// <summary>
    /// Crea e aggiunge un movimento con posizione assoluta finale
    /// </summary>
    public void AddAbsoluteMovement(GameObject obj, Vector3 targetPos, float duration = 0f, float delay = 0f)
    {
        var movement = new ObjectMovement
        {
            targetObject = obj,
            useAbsolutePosition = true,
            targetPosition = targetPos,
            duration = duration,
            startDelay = delay,
            movementCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f)
        };
        
        AddObjectMovement(movement);
    }
    
    // Proprietà pubbliche
    public bool IsAudioPlaying => isAudioPlaying;
    public bool IsDissolving => isDissolving;
    public bool IsMovingObjects => isMovingObjects;
    public bool HasTriggered => hasTriggered;
    public bool UseTriggerCollider => useTriggerCollider;
    public int DissolveObjectsCount => objectsToDissolve?.Length ?? 0;
    public int DissolveMaterialsCount => materialsToDissolve?.Length ?? 0;
    public int ValidMaterialsCount => validMaterials.Count;
    public int ActivateObjectsCount => objectsToActivate?.Length ?? 0;
    public int DeactivateObjectsCount => objectsToDeactivate?.Length ?? 0;
    public int MovingObjectsCount => objectsToMove?.Length ?? 0;
    public int PostMovementActivationCount => postMovementActivation?.objectsToActivate?.Length ?? 0;
    
    private void OnDestroy() => StopSequence();
    private void OnDisable() => StopSequence();
    
    private void OnValidate()
    {
        earthquakeDuration = Mathf.Max(0.1f, earthquakeDuration);
        audioVolume = Mathf.Clamp01(audioVolume);
        dissolveDuration = Mathf.Max(0.1f, dissolveDuration);
        shakeDelay = Mathf.Max(0f, shakeDelay);
        shakeStopDelay = Mathf.Max(0f, shakeStopDelay);
        activationDelay = Mathf.Max(0f, activationDelay);
        deactivationDelay = Mathf.Max(0f, deactivationDelay);
        
        if (dissolveCurve == null || dissolveCurve.keys.Length == 0)
        {
            dissolveCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        }
        
        // Inizializza le curve di movimento se necessario
        if (objectsToMove != null)
        {
            foreach (var movement in objectsToMove)
            {
                if (movement != null)
                {
                    movement.startDelay = Mathf.Max(0f, movement.startDelay);
                    movement.duration = Mathf.Max(0f, movement.duration);
                    
                    if (movement.movementCurve == null || movement.movementCurve.keys.Length == 0)
                    {
                        movement.movementCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
                    }
                }
            }
        }
        
        if (Application.isPlaying)
        {
            SetupDissolveMaterials();
            SetupTrigger();
        }
    }
}