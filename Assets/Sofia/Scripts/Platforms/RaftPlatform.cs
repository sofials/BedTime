using UnityEngine;
using UnityEngine.Splines;
using System.Collections.Generic;

public class RaftPlatform : MonoBehaviour
{
    public SplineContainer splineContainer;
    public float speed = 10f;
    public int sampleResolution = 100;

    private List<Vector3> sampledPoints = new List<Vector3>();
    private List<float> cumulativeDistances = new List<float>();
    private float currentDistance = 0f;
    private float totalLength = 0f;
    private int direction = 1;

    private Vector3 lastPosition;
    private CharacterController playerController = null;

    private float speedMultiplier = 1f;

    private Vector3 deltaMovement;

    public Vector3 DeltaMovement => deltaMovement;

    private bool isMoving = false;
    private bool isWaitingAtEnd = false;
    private bool wasMovingWithPlayer = false;

    private float startDistance = 0f;
    private float endDistance = 0f;

    // Sistema per gestione respawn
    [Header("Respawn Settings")]
    [SerializeField] private float returnToTerminalRadius = 50f; // Raggio per considerare il player "vicino"
    [SerializeField] private bool debugRespawnSystem = true;
    
    private bool isReturningToTerminal = false;
    private Vector3 lastKnownPlayerPosition;
    private bool playerWasOnBoard = false;

    // Integrazione sistema checkpoint
    [Header("Checkpoint Integration")]
    [SerializeField] private bool useCheckpointSystem = true;
    [SerializeField] private CheckpointManager checkpointManager;

    private Vector3 lastKnownCheckpointPosition;
    private bool hasCheckpointPosition = false;

    // Sistema per gestire i salti del player
    [Header("Jump Detection")]
    [SerializeField] private float jumpIgnoreTime = 1.5f; // Tempo per ignorare uscite brevi
    private float playerExitTime = -1f;
    private bool playerJustExited = false;
    private int lastDirectionWithPlayer = 1; // Memorizza direzione precedente
    private bool wasMovingBeforeJump = false;

    void Start()
    {
        SampleSpline();
        
        // Calcola la distanza tra knot 0 e 1
        startDistance = 0f;
        endDistance = GetDistanceAtT(1f / (splineContainer.Spline.Count - 1));
        
        // ✅ SOLUZIONE: Forza sempre la zattera all'inizio della spline
        currentDistance = startDistance; // Sempre 0
        
        // ✅ POSIZIONA FISICAMENTE la zattera all'inizio della spline
        Vector3 startPosition = GetPositionAtDistance(startDistance);
        transform.position = startPosition;
        lastPosition = startPosition;

        isWaitingAtEnd = true;
        
        // Collegamento al CheckpointManager
        SetupCheckpointIntegration();
        
        // Salva la posizione iniziale come ultima posizione conosciuta del player
        lastKnownPlayerPosition = startPosition;
        
        if (debugRespawnSystem)
        {
            Debug.Log($"[RaftPlatform] Zattera posizionata automaticamente all'inizio della spline: {startPosition}");
            Debug.Log($"[RaftPlatform] currentDistance={currentDistance}, startDistance={startDistance}, endDistance={endDistance}");
        }
    }

    void Update()
    {
        // Controlla se il player è respawnato lontano
        CheckForPlayerRespawn();
        
        // Gestisci le uscite definitive del player dopo il tempo di grazia
        if (playerJustExited && (Time.time - playerExitTime) >= jumpIgnoreTime)
        {
            // Il player è davvero sceso definitivamente
            if (debugRespawnSystem)
            {
                Debug.Log("[RaftPlatform] Player sceso definitivamente, fermo la piattaforma.");
            }
            
            playerController = null;
            isMoving = false;
            wasMovingWithPlayer = false;
            playerJustExited = false;
            wasMovingBeforeJump = false;
        }

        if (isMoving)
        {
            // Salva la direzione e lo stato quando si muove con il player
            if (playerController != null)
            {
                lastDirectionWithPlayer = direction;
                wasMovingBeforeJump = true;
            }
            
            currentDistance += speed * speedMultiplier * direction * Time.deltaTime;

            if (direction == 1 && currentDistance >= endDistance)
            {
                currentDistance = endDistance;
                isMoving = false;
                isWaitingAtEnd = true;
                isReturningToTerminal = false;
                wasMovingBeforeJump = false;
            }
            else if (direction == -1 && currentDistance <= startDistance)
            {
                currentDistance = startDistance;
                isMoving = false;
                isWaitingAtEnd = true;
                isReturningToTerminal = false;
                wasMovingBeforeJump = false;
            }

            Vector3 newPosition = GetPositionAtDistance(currentDistance);
            deltaMovement = newPosition - lastPosition;
            transform.position = newPosition;
            lastPosition = newPosition;
        }
        else
        {
            deltaMovement = Vector3.zero;
            lastPosition = transform.position;

            // Logica originale semplificata - ma non attivare se il player ha appena saltato
            if (wasMovingWithPlayer && playerController == null && !isReturningToTerminal && !playerJustExited)
            {
                StartReturningToTerminalNearZattera();
            }
        }
    }

    // ✅ Setup integrazione con checkpoint system
    private void SetupCheckpointIntegration()
    {
        if (!useCheckpointSystem) return;
        
        // Trova il CheckpointManager se non assegnato
        if (checkpointManager == null)
        {
            checkpointManager = CheckpointManager.Instance;
            if (checkpointManager == null)
            {
                checkpointManager = FindFirstObjectByType<CheckpointManager>();
            }
        }
        
        // Collegati agli eventi del CheckpointManager
        if (checkpointManager != null)
        {
            checkpointManager.OnPlayerCheckpointChanged.AddListener(OnPlayerCheckpointUpdated);
            
            // Ottieni la posizione corrente del checkpoint se esiste
            if (checkpointManager.HasActiveCheckpoint())
            {
                lastKnownCheckpointPosition = checkpointManager.GetCurrentSpawnPosition();
                hasCheckpointPosition = true;
                
                if (debugRespawnSystem)
                {
                    Debug.Log($"[RaftPlatform] Checkpoint iniziale trovato: {lastKnownCheckpointPosition}");
                }
            }
        }
        else if (debugRespawnSystem)
        {
            Debug.LogWarning("[RaftPlatform] CheckpointManager non trovato, sistema checkpoint disabilitato");
        }
    }

    // ✅ Callback quando il player raggiunge un nuovo checkpoint
    private void OnPlayerCheckpointUpdated(Vector3 checkpointPosition)
    {
        lastKnownCheckpointPosition = checkpointPosition;
        hasCheckpointPosition = true;
        
        if (debugRespawnSystem)
        {
            Debug.Log($"[RaftPlatform] Nuovo checkpoint player ricevuto: {checkpointPosition}");
        }
        
        // Se la zattera sta tornando automaticamente e il player ha raggiunto un checkpoint,
        // potremmo voler ricalcolare la destinazione
        if (isReturningToTerminal && !isWaitingAtEnd)
        {
            if (debugRespawnSystem)
            {
                Debug.Log("[RaftPlatform] Player ha raggiunto checkpoint durante ritorno automatico, ricalcolo destinazione");
            }
            StartReturningToTerminalNearPlayer(checkpointPosition);
        }
    }

    // ✅ MODIFICATO: Usa la posizione del checkpoint invece di cercare il player
    private void CheckForPlayerRespawn()
    {
        if (playerController == null && !isReturningToTerminal && playerWasOnBoard)
        {
            Vector3 playerRespawnPosition;
            bool foundPlayerPosition = false;
            
            // ✅ PRIORITÀ 1: Usa il sistema checkpoint se disponibile e attivo
            if (useCheckpointSystem && hasCheckpointPosition)
            {
                playerRespawnPosition = lastKnownCheckpointPosition;
                foundPlayerPosition = true;
                
                if (debugRespawnSystem)
                {
                    Debug.Log($"[RaftPlatform] Usando posizione checkpoint per respawn: {playerRespawnPosition}");
                }
            }
            else
            {
                // ✅ FALLBACK: Cerca il player nella scena (sistema originale)
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    playerRespawnPosition = player.transform.position;
                    foundPlayerPosition = true;
                    
                    if (debugRespawnSystem)
                    {
                        Debug.Log($"[RaftPlatform] Player trovato nella scena: {playerRespawnPosition}");
                    }
                }
                else
                {
                    playerRespawnPosition = Vector3.zero;
                }
            }
            
            if (foundPlayerPosition)
            {
                float distanceToPlayer = Vector3.Distance(transform.position, playerRespawnPosition);
                
                if (debugRespawnSystem)
                {
                    Debug.Log($"[RaftPlatform] Distanza zattera-player: {distanceToPlayer:F1}m");
                }

                // Se il player è lontano, probabilmente è respawnato
                if (distanceToPlayer > returnToTerminalRadius)
                {
                    if (debugRespawnSystem)
                    {
                        Debug.Log($"[RaftPlatform] Player respawnato lontano ({distanceToPlayer:F1}m > {returnToTerminalRadius}m), torno al capolinea più vicino AL PLAYER");
                    }
                    
                    StartReturningToTerminalNearPlayer(playerRespawnPosition);
                    playerWasOnBoard = false; // Reset flag
                }
            }
        }
    }

    // ✅ Avvia il ritorno al capolinea più vicino AL PLAYER
    private void StartReturningToTerminalNearPlayer(Vector3 playerPosition)
    {
        isReturningToTerminal = true;
        isMoving = true;
        isWaitingAtEnd = false;
        
        // Calcola quale capolinea è più vicino alla POSIZIONE DEL PLAYER
        Vector3 startPos = GetPositionAtDistance(startDistance);
        Vector3 endPos = GetPositionAtDistance(endDistance);
        
        float distancePlayerToStart = Vector3.Distance(playerPosition, startPos);
        float distancePlayerToEnd = Vector3.Distance(playerPosition, endPos);
        
        // Vai verso il capolinea più vicino al player
        direction = (distancePlayerToStart < distancePlayerToEnd) ? -1 : 1;
        
        if (debugRespawnSystem)
        {
            string targetTerminal = (direction == -1) ? "START" : "END";
            float targetDistance = (direction == -1) ? distancePlayerToStart : distancePlayerToEnd;
            Debug.Log($"[RaftPlatform] Ritorno al capolinea {targetTerminal} più vicino al PLAYER (distanza player-capolinea: {targetDistance:F1})");
            Debug.Log($"[RaftPlatform] Player position: {playerPosition}, Start: {startPos}, End: {endPos}");
        }
        
        wasMovingWithPlayer = false;
    }

    // ✅ Metodo per tornare al capolinea più vicino alla zattera (logica originale)
    private void StartReturningToTerminalNearZattera()
    {
        isReturningToTerminal = true;
        isMoving = true;
        isWaitingAtEnd = false;
        
        // Calcola quale capolinea è più vicino alla posizione ATTUALE DELLA ZATTERA
        float distanceToStart = Mathf.Abs(currentDistance - startDistance);
        float distanceToEnd = Mathf.Abs(currentDistance - endDistance);
        
        direction = (distanceToStart < distanceToEnd) ? -1 : 1;
        
        if (debugRespawnSystem)
        {
            string targetTerminal = (direction == -1) ? "START" : "END";
            Debug.Log($"[RaftPlatform] Ritorno al capolinea {targetTerminal} più vicino alla ZATTERA (distanza: {(direction == -1 ? distanceToStart : distanceToEnd):F1})");
        }
        
        wasMovingWithPlayer = false;
    }

    // ✅ AGGIORNATO: Metodo pubblico che ora considera la posizione del player
    public void ForceReturnToNearestTerminal()
    {
        // Prima prova con il sistema checkpoint
        if (useCheckpointSystem && hasCheckpointPosition)
        {
            if (debugRespawnSystem)
            {
                Debug.Log("[RaftPlatform] Forzato ritorno al capolinea più vicino al CHECKPOINT");
            }
            StartReturningToTerminalNearPlayer(lastKnownCheckpointPosition);
            return;
        }
        
        // Fallback al player nella scena
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            if (debugRespawnSystem)
            {
                Debug.Log("[RaftPlatform] Forzato ritorno al capolinea più vicino al PLAYER");
            }
            StartReturningToTerminalNearPlayer(player.transform.position);
        }
        else
        {
            if (debugRespawnSystem)
            {
                Debug.Log("[RaftPlatform] Player non trovato, forzato ritorno al capolinea più vicino alla ZATTERA");
            }
            StartReturningToTerminalNearZattera();
        }
    }

    // ✅ NUOVO: Metodo per aggiornare manualmente la posizione del checkpoint
    public void UpdatePlayerCheckpointPosition(Vector3 checkpointPosition)
    {
        lastKnownCheckpointPosition = checkpointPosition;
        hasCheckpointPosition = true;
        
        if (debugRespawnSystem)
        {
            Debug.Log($"[RaftPlatform] Posizione checkpoint aggiornata manualmente: {checkpointPosition}");
        }
    }

    // ✅ NUOVO: Metodo per disabilitare il sistema checkpoint
    public void DisableCheckpointSystem()
    {
        useCheckpointSystem = false;
        hasCheckpointPosition = false;
        
        if (checkpointManager != null)
        {
            checkpointManager.OnPlayerCheckpointChanged.RemoveListener(OnPlayerCheckpointUpdated);
        }
        
        if (debugRespawnSystem)
        {
            Debug.Log("[RaftPlatform] Sistema checkpoint disabilitato");
        }
    }

    // Metodo per ottenere la distanza dai capolinea (zattera)
    public float GetDistanceToNearestTerminal()
    {
        float distanceToStart = Mathf.Abs(currentDistance - startDistance);
        float distanceToEnd = Mathf.Abs(currentDistance - endDistance);
        return Mathf.Min(distanceToStart, distanceToEnd);
    }

    // ✅ AGGIORNATO: Metodo per ottenere la distanza del player dai capolinea
    public float GetPlayerDistanceToNearestTerminal()
    {
        Vector3 playerPos;
        
        // Usa checkpoint se disponibile
        if (useCheckpointSystem && hasCheckpointPosition)
        {
            playerPos = lastKnownCheckpointPosition;
        }
        else
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) return float.MaxValue;
            playerPos = player.transform.position;
        }
        
        Vector3 startPos = GetPositionAtDistance(startDistance);
        Vector3 endPos = GetPositionAtDistance(endDistance);
        
        float distanceToStart = Vector3.Distance(playerPos, startPos);
        float distanceToEnd = Vector3.Distance(playerPos, endPos);
        
        return Mathf.Min(distanceToStart, distanceToEnd);
    }

    // Verifica se la zattera è al capolinea
    public bool IsAtTerminal()
    {
        float tolerance = 0.1f;
        return Mathf.Abs(currentDistance - startDistance) < tolerance || 
               Mathf.Abs(currentDistance - endDistance) < tolerance;
    }

    // ✅ NUOVO: Metodo pubblico per riposizionare manualmente all'inizio
    [ContextMenu("Reset to Start Position")]
    public void ResetToStartPosition()
    {
        if (sampledPoints.Count == 0)
        {
            SampleSpline();
        }
        
        currentDistance = startDistance;
        Vector3 startPosition = GetPositionAtDistance(startDistance);
        transform.position = startPosition;
        lastPosition = startPosition;
        
        isMoving = false;
        isWaitingAtEnd = true;
        isReturningToTerminal = false;
        
        if (debugRespawnSystem)
        {
            Debug.Log($"[RaftPlatform] Zattera riposizionata manualmente all'inizio: {startPosition}");
        }
    }

    void SampleSpline()
    {
        sampledPoints.Clear();
        cumulativeDistances.Clear();

        totalLength = 0f;
        Vector3 prevPoint = splineContainer.EvaluatePosition(0f);
        sampledPoints.Add(prevPoint);
        cumulativeDistances.Add(0f);

        for (int i = 1; i <= sampleResolution; i++)
        {
            float t = (float)i / sampleResolution;
            Vector3 point = splineContainer.EvaluatePosition(t);
            float dist = Vector3.Distance(prevPoint, point);
            totalLength += dist;

            sampledPoints.Add(point);
            cumulativeDistances.Add(totalLength);
            prevPoint = point;
        }
    }

    float GetDistanceAtT(float t)
    {
        float targetDistance = t * totalLength;
        for (int i = 1; i < cumulativeDistances.Count; i++)
        {
            if (cumulativeDistances[i] >= targetDistance)
            {
                return cumulativeDistances[i];
            }
        }
        return totalLength;
    }

    Vector3 GetPositionAtDistance(float distance)
    {
        if (distance <= 0f) return sampledPoints[0];
        if (distance >= totalLength) return sampledPoints[sampledPoints.Count - 1];

        for (int i = 1; i < cumulativeDistances.Count; i++)
        {
            if (cumulativeDistances[i] >= distance)
            {
                float prevDist = cumulativeDistances[i - 1];
                float nextDist = cumulativeDistances[i];
                float segmentT = Mathf.InverseLerp(prevDist, nextDist, distance);
                return Vector3.Lerp(sampledPoints[i - 1], sampledPoints[i], segmentT);
            }
        }

        return sampledPoints[sampledPoints.Count - 1];
    }

    public void SetSpeedMultiplier(float multiplier)
    {
        speedMultiplier = multiplier;
        Debug.Log($"[RaftPlatform] {gameObject.name} speed multiplier impostato a {multiplier}");
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            // Se il player è rientrato poco dopo essere uscito, era solo un salto
            if (playerJustExited && (Time.time - playerExitTime) < jumpIgnoreTime)
            {
                if (debugRespawnSystem)
                {
                    Debug.Log($"[RaftPlatform] Player atterrato dopo salto ({(Time.time - playerExitTime):F2}s), continuo movimento.");
                }
                
                playerJustExited = false;
                playerController = other.GetComponent<CharacterController>();
                
                // Riprendi il movimento se era in corso prima del salto
                if (wasMovingBeforeJump && !isMoving)
                {
                    isMoving = true;
                    direction = lastDirectionWithPlayer; // Mantieni direzione precedente
                    
                    if (debugRespawnSystem)
                    {
                        Debug.Log($"[RaftPlatform] Riprendo movimento nella direzione {direction}");
                    }
                }
                
                return; // Exit early, non fare altro processing
            }
            
            // È un vero ingresso (prima volta o dopo molto tempo)
            playerController = other.GetComponent<CharacterController>();
            playerWasOnBoard = true;
            playerJustExited = false;
            wasMovingBeforeJump = false;
            
            // Salva posizione del player
            lastKnownPlayerPosition = other.transform.position;

            // Riparte quando il player sale, anche se era ferma
            if (isWaitingAtEnd || !isMoving)
            {
                isMoving = true;
                isWaitingAtEnd = false;
                isReturningToTerminal = false;

                // Logica migliorata per la direzione
                if (Mathf.Approximately(currentDistance, startDistance))
                {
                    direction = 1; // Verso end
                }
                else if (Mathf.Approximately(currentDistance, endDistance))
                {
                    direction = -1; // Torna indietro
                }
                else
                {
                    // Se abbiamo una direzione precedente valida, usala
                    direction = lastDirectionWithPlayer;
                }

                wasMovingWithPlayer = true;
                
                if (debugRespawnSystem)
                {
                    Debug.Log($"[RaftPlatform] Player salito, riparto in direzione {direction}");
                }
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (playerController == other.GetComponent<CharacterController>())
            {
                // Salva ultima posizione nota del player prima che scenda
                lastKnownPlayerPosition = other.transform.position;
                
                // Marca il tempo di uscita per detectare i salti
                playerExitTime = Time.time;
                playerJustExited = true;
                wasMovingBeforeJump = isMoving; // Salva se stava muovendo
                
                if (debugRespawnSystem)
                {
                    Debug.Log($"[RaftPlatform] Player uscito dal trigger, timer salto avviato. Era in movimento: {wasMovingBeforeJump}");
                }
                
                // NON fermare immediatamente la piattaforma
                // Lascia che il timer nel Update gestisca la fermata definitiva
            }
        }
    }

    // ✅ CLEANUP: Disconnetti dagli eventi quando l'oggetto viene distrutto
    private void OnDestroy()
    {
        if (checkpointManager != null)
        {
            checkpointManager.OnPlayerCheckpointChanged.RemoveListener(OnPlayerCheckpointUpdated);
        }
    }

    // ✅ AGGIORNAMENTO GIZMOS: Mostra anche la posizione del checkpoint
    #if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) return;

        // Disegna il raggio di rilevamento respawn
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, returnToTerminalRadius);
        
        // Disegna i capolinea
        Gizmos.color = Color.green;
        Vector3 startPos = GetPositionAtDistance(startDistance);
        Vector3 endPos = GetPositionAtDistance(endDistance);
        
        Gizmos.DrawWireSphere(startPos, 2f);
        Gizmos.DrawWireSphere(endPos, 2f);
        
        // Etichette
        UnityEditor.Handles.Label(startPos + Vector3.up * 3, "START");
        UnityEditor.Handles.Label(endPos + Vector3.up * 3, "END");
        
        if (isReturningToTerminal)
        {
            Gizmos.color = Color.red;
            Vector3 targetPos = (direction == -1) ? startPos : endPos;
            Gizmos.DrawLine(transform.position, targetPos);
        }
        
        // ✅ NUOVO: Mostra posizione del checkpoint se disponibile
        if (hasCheckpointPosition)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(lastKnownCheckpointPosition, 1.5f);
            UnityEditor.Handles.Label(lastKnownCheckpointPosition + Vector3.up * 4, "CHECKPOINT");
            
            // Linee verso i capolinea dal checkpoint
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(lastKnownCheckpointPosition, startPos);
            Gizmos.DrawLine(lastKnownCheckpointPosition, endPos);
        }
        
        // Fallback: mostra posizione del player se presente
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null && !hasCheckpointPosition)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(player.transform.position, 1f);
            UnityEditor.Handles.Label(player.transform.position + Vector3.up * 4, "PLAYER");
            
            // Linee verso i capolinea per visualizzare le distanze
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(player.transform.position, startPos);
            Gizmos.DrawLine(player.transform.position, endPos);
        }

        // ✅ NUOVO: Mostra informazioni sui salti
        if (playerJustExited)
        {
            float timeRemaining = jumpIgnoreTime - (Time.time - playerExitTime);
            UnityEditor.Handles.Label(transform.position + Vector3.up * 6, $"Jump Timer: {timeRemaining:F1}s");
        }
    }
    #endif
}