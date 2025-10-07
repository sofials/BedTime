using UnityEngine;
using UnityEngine.Events;
using System.Collections;

public class SimpleTriggerZone : MonoBehaviour
{
    [Header("Trigger Settings")]
    [SerializeField] private bool oneShot = false;
    [SerializeField] private bool alreadyEntered = false;
    [SerializeField] private bool alreadyExited = false;
    [SerializeField] private bool requireGroundedPlayer = false;
    
    [Header("Filtering")]
    [SerializeField] private string collisionTag = "Player";
    [SerializeField] private LayerMask triggerLayerMask = -1;
    
    [Header("Camera Integration")]
    [SerializeField] private bool triggerCameraSwitch = false;
    [SerializeField] private string targetCameraName = "";
    [SerializeField] private Unity.Cinemachine.CinemachineCamera targetCamera;
    [SerializeField] private bool restorePreviousCamera = false;
    
    [Header("Timing & Delays")]
    [SerializeField] private float enterDelay = 0f;
    [SerializeField] private float exitDelay = 0f;
    [SerializeField] private float cooldownTime = 0f;
    private float lastTriggerTime = 0f;
    
    [Header("Scene Transition")]
    [SerializeField] private bool triggerSceneTransition = false;
    [SerializeField] private string targetSceneName = "";
    [SerializeField] private float sceneTransitionDelay = 1f;
    
    [Header("Checkpoint")]
    [SerializeField] private bool isCheckpoint = false;
    [SerializeField] private string checkpointName = "";
    
    [Header("Visual Feedback")]
    [SerializeField] private bool showDebugGizmos = true;
    [SerializeField] private Color gizmoColor = Color.yellow;
    [SerializeField] private Color activeGizmoColor = Color.red;
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;
    [SerializeField] private bool logAllCollisions = false;
    
    [Header("Events")]
    public UnityEvent<Collider> onTriggerEnter;
    public UnityEvent<Collider> onTriggerExit;
    public UnityEvent<Collider> onTriggerStay;
    
    [Header("Advanced Events")]
    public UnityEvent<string> onPlayerEnterWithName;
    public UnityEvent<string> onPlayerExitWithName;
    public UnityEvent onCameraSwitched;
    public UnityEvent onCheckpointTriggered;
    public UnityEvent onSceneTransitionStarted;
    
    private bool isPlayerInside = false;
    private Collider currentPlayerCollider = null;
    private Unity.Cinemachine.CinemachineCamera previousCamera = null;
    private Collider triggerCollider;
    
    private CameraManager cameraManager;
    
    private void Awake()
    {
        triggerCollider = GetComponent<Collider>();
        if (triggerCollider == null)
        {
            DebugLog("Nessun Collider trovato! Aggiungendo BoxCollider...");
            triggerCollider = gameObject.AddComponent<BoxCollider>();
        }
        
        if (!triggerCollider.isTrigger)
        {
            DebugLog("Collider non impostato come Trigger! Correggendo...");
            triggerCollider.isTrigger = true;
        }
    }
    
    private void Start()
    {
        StartCoroutine(FindManagerReferences());
    }
    
    private IEnumerator FindManagerReferences()
    {
        yield return new WaitForSeconds(0.2f);
        
        var currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        
        // FIX: Cerca solo nella scena corrente
        var managers = FindObjectsByType<CameraManager>(FindObjectsSortMode.None);
        cameraManager = null;
        
        foreach (var manager in managers)
        {
            if (manager.gameObject.scene == currentScene)
            {
                cameraManager = manager;
                break;
            }
        }
        
        if (cameraManager == null)
        {
            DebugLog("SimpleCameraManager non trovato nella scena corrente");
        }
        else
        {
            DebugLog($"SimpleCameraManager trovato: {cameraManager.name}");
        }
        
        if (triggerCameraSwitch && !string.IsNullOrEmpty(targetCameraName) && targetCamera == null)
        {
            yield return new WaitForSeconds(0.5f);
            
            if (cameraManager != null)
            {
                targetCamera = cameraManager.GetCameraByName(targetCameraName);
                if (targetCamera == null)
                {
                    DebugLog($"Camera '{targetCameraName}' non trovata!");
                }
                else
                {
                    DebugLog($"Camera target trovata: {targetCamera.name}");
                }
            }
        }
        
        DebugLog($"SimpleTriggerZone inizializzato - Camera: {triggerCameraSwitch}, Checkpoint: {isCheckpoint}");
    }

    private void OnTriggerEnter(Collider other)
    {
        if (logAllCollisions)
        {
            DebugLog($"Collision detected with: {other.name} (Tag: {other.tag})");
        }
        
        // FIX: Controlla oneShot PRIMA di tutto
        if (oneShot && alreadyEntered)
        {
            DebugLog("OneShot già attivato, ignorando enter");
            return;
        }
        
        if (!ShouldProcessCollision(other))
            return;
            
        if (IsCooldownActive())
        {
            DebugLog("Trigger in cooldown, ignorando...");
            return;
        }
        
        if (requireGroundedPlayer && !IsPlayerGrounded(other))
        {
            DebugLog("Player non a terra, trigger ignorato");
            return;
        }
        
        DebugLog($"Trigger ENTER: {other.name}");
        
        isPlayerInside = true;
        currentPlayerCollider = other;
        lastTriggerTime = Time.time;
        
        if (enterDelay > 0f)
        {
            StartCoroutine(DelayedTriggerEnter(other));
        }
        else
        {
            ExecuteTriggerEnter(other);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        // FIX: Controlla oneShot PRIMA di tutto
        if (oneShot && alreadyExited)
        {
            DebugLog("OneShot già attivato, ignorando exit");
            return;
        }
        
        if (!ShouldProcessCollision(other))
            return;
            
        DebugLog($"Trigger EXIT: {other.name}");
        
        // FIX: Imposta SEMPRE lo stato interno, anche con oneShot
        isPlayerInside = false;
        currentPlayerCollider = null;
        
        if (exitDelay > 0f)
        {
            StartCoroutine(DelayedTriggerExit(other));
        }
        else
        {
            ExecuteTriggerExit(other);
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (!ShouldProcessCollision(other))
            return;

        onTriggerStay?.Invoke(other);
    }
    
    private bool ShouldProcessCollision(Collider other)
    {
        if (!string.IsNullOrEmpty(collisionTag) && !other.CompareTag(collisionTag))
            return false;
        
        if (triggerLayerMask != -1 && ((1 << other.gameObject.layer) & triggerLayerMask) == 0)
            return false;

        return true;
    }
    
    private bool IsPlayerGrounded(Collider playerCollider)
    {
        var playerController = playerCollider.GetComponent<CharacterController>();
        if (playerController != null)
        {
            return playerController.isGrounded;
        }
        
        return Physics.Raycast(playerCollider.transform.position, Vector3.down, 1.1f);
    }
    
    private bool IsCooldownActive()
    {
        return cooldownTime > 0f && (Time.time - lastTriggerTime) < cooldownTime;
    }
    
    private IEnumerator DelayedTriggerEnter(Collider other)
    {
        yield return new WaitForSeconds(enterDelay);
        
        if (isPlayerInside && currentPlayerCollider == other)
        {
            ExecuteTriggerEnter(other);
        }
    }
    
    private IEnumerator DelayedTriggerExit(Collider other)
    {
        yield return new WaitForSeconds(exitDelay);
        
        if (!isPlayerInside)
        {
            ExecuteTriggerExit(other);
        }
    }
    
    private void ExecuteTriggerEnter(Collider other)
    {
        DebugLog($"Executing Trigger ENTER for: {other.name}");
        
        onTriggerEnter?.Invoke(other);
        onPlayerEnterWithName?.Invoke(gameObject.name);
        
        if (triggerCameraSwitch && HandleCameraSwitch())
        {
            onCameraSwitched?.Invoke();
        }
        
        if (isCheckpoint && HandleCheckpoint())
        {
            onCheckpointTriggered?.Invoke();
        }
        
        if (triggerSceneTransition && !string.IsNullOrEmpty(targetSceneName))
        {
            HandleSceneTransition();
        }
        
        if (oneShot)
            alreadyEntered = true;
    }
    
    private void ExecuteTriggerExit(Collider other)
    {
        DebugLog($"Executing Trigger EXIT for: {other.name}");
        
        onTriggerExit?.Invoke(other);
        onPlayerExitWithName?.Invoke(gameObject.name);
        
        if (restorePreviousCamera && previousCamera != null && cameraManager != null)
        {
            cameraManager.SwitchCamera(previousCamera);
            DebugLog($"Camera ripristinata: {previousCamera.name}");
            previousCamera = null;
        }
        
        if (oneShot)
            alreadyExited = true;
    }
    
    private bool HandleCameraSwitch()
    {
        if (cameraManager == null || targetCamera == null)
        {
            DebugLog("SimpleCameraManager o target camera non disponibili");
            return false;
        }
        
        if (restorePreviousCamera)
        {
            previousCamera = cameraManager.GetActiveCamera();
            DebugLog($"Camera precedente salvata: {previousCamera?.name}");
        }
        
        cameraManager.SwitchCamera(targetCamera);
        DebugLog($"Camera cambiata a: {targetCamera.name}");
        
        return true;
    }
    
    private bool HandleCheckpoint()
    {
        if (string.IsNullOrEmpty(checkpointName))
        {
            checkpointName = gameObject.name;
        }
        
        DebugLog($"Checkpoint attivato: {checkpointName}");
        
        SaveCheckpointData();
        
        return true;
    }
    
    private void SaveCheckpointData()
    {
        if (currentPlayerCollider != null)
        {
            Vector3 playerPosition = currentPlayerCollider.transform.position;
            PlayerPrefs.SetFloat($"Checkpoint_{checkpointName}_X", playerPosition.x);
            PlayerPrefs.SetFloat($"Checkpoint_{checkpointName}_Y", playerPosition.y);
            PlayerPrefs.SetFloat($"Checkpoint_{checkpointName}_Z", playerPosition.z);
            PlayerPrefs.SetString("LastCheckpoint", checkpointName);
            PlayerPrefs.Save();
            
            DebugLog($"Checkpoint data salvato per: {checkpointName} at {playerPosition}");
        }
    }
    
    private void HandleSceneTransition()
    {
        DebugLog($"Avviando transizione verso: {targetSceneName}");
        onSceneTransitionStarted?.Invoke();
        
        StartCoroutine(DelayedSceneTransition());
    }
    
    private IEnumerator DelayedSceneTransition()
    {
        if (sceneTransitionDelay > 0f)
        {
            yield return new WaitForSeconds(sceneTransitionDelay);
        }
        
        UnityEngine.SceneManagement.SceneManager.LoadScene(targetSceneName);
    }
    
    public static Vector3 GetLastCheckpointPosition()
    {
        string lastCheckpoint = PlayerPrefs.GetString("LastCheckpoint", "");
        if (!string.IsNullOrEmpty(lastCheckpoint))
        {
            float x = PlayerPrefs.GetFloat($"Checkpoint_{lastCheckpoint}_X", 0f);
            float y = PlayerPrefs.GetFloat($"Checkpoint_{lastCheckpoint}_Y", 0f);
            float z = PlayerPrefs.GetFloat($"Checkpoint_{lastCheckpoint}_Z", 0f);
            return new Vector3(x, y, z);
        }
        
        return Vector3.zero;
    }
    
    public static string GetLastCheckpointName()
    {
        return PlayerPrefs.GetString("LastCheckpoint", "");
    }
    
    public void ResetTrigger()
    {
        alreadyEntered = false;
        alreadyExited = false;
        isPlayerInside = false;
        currentPlayerCollider = null;
        previousCamera = null;
        lastTriggerTime = 0f;
        
        DebugLog("Trigger resettato");
    }
    
    public void ForceTrigger()
    {
        if (currentPlayerCollider != null)
        {
            ExecuteTriggerEnter(currentPlayerCollider);
        }
        else
        {
            var player = GameObject.FindWithTag(collisionTag);
            if (player != null)
            {
                var playerCollider = player.GetComponent<Collider>();
                if (playerCollider != null)
                {
                    ExecuteTriggerEnter(playerCollider);
                }
            }
        }
    }
    
    public void SetTargetCamera(Unity.Cinemachine.CinemachineCamera camera)
    {
        targetCamera = camera;
        if (camera != null)
        {
            targetCameraName = camera.name;
            triggerCameraSwitch = true;
            DebugLog($"Camera target impostata: {camera.name}");
        }
    }
    
    public void SetCheckpointName(string checkpoint)
    {
        checkpointName = checkpoint;
        isCheckpoint = true;
        DebugLog($"Checkpoint impostato: {checkpoint}");
    }
    
    public void SetTargetScene(string sceneName)
    {
        targetSceneName = sceneName;
        triggerSceneTransition = true;
        DebugLog($"Scena target impostata: {sceneName}");
    }
    
    public bool IsPlayerInside() => isPlayerInside;
    public Collider GetCurrentPlayer() => currentPlayerCollider;
    public bool IsOneShot() => oneShot;
    public bool HasTriggeredEnter() => alreadyEntered;
    public bool HasTriggeredExit() => alreadyExited;
    public float GetTimeSinceLastTrigger() => Time.time - lastTriggerTime;
    public CameraManager GetCameraManager() => cameraManager;
    
    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[SimpleTriggerZone:{gameObject.name}] {message}");
        }
    }
    
    [ContextMenu("Debug Trigger State")]
    public void DebugTriggerState()
    {
        Debug.Log($"=== SimpleTriggerZone State ({gameObject.name}) ===\n" +
                  $"Player Inside: {isPlayerInside}\n" +
                  $"Current Player: {(currentPlayerCollider != null ? currentPlayerCollider.name : "null")}\n" +
                  $"One Shot: {oneShot}\n" +
                  $"Already Entered: {alreadyEntered}\n" +
                  $"Already Exited: {alreadyExited}\n" +
                  $"Cooldown Active: {IsCooldownActive()}\n" +
                  $"Time Since Last Trigger: {GetTimeSinceLastTrigger():F1}s\n" +
                  $"Camera Switch: {triggerCameraSwitch}\n" +
                  $"Target Camera: {(targetCamera != null ? targetCamera.name : "null")}\n" +
                  $"Is Checkpoint: {isCheckpoint}\n" +
                  $"Checkpoint Name: {checkpointName}\n" +
                  $"Scene Transition: {triggerSceneTransition} -> {targetSceneName}\n" +
                  $"SimpleCameraManager: {(cameraManager != null ? "Found" : "Not Found")}");
    }
    
    [ContextMenu("Test - Force Trigger")]
    public void DebugForceTrigger()
    {
        ForceTrigger();
    }
    
    [ContextMenu("Test - Reset Trigger")]
    public void DebugResetTrigger()
    {
        ResetTrigger();
    }
    
    [ContextMenu("Test - Find Manager")]
    public void DebugFindManager()
    {
        StartCoroutine(FindManagerReferences());
    }
    
    [ContextMenu("Test - Save Checkpoint")]
    public void DebugSaveCheckpoint()
    {
        SaveCheckpointData();
    }
    
    [ContextMenu("Test - Load Last Checkpoint")]
    public void DebugLoadLastCheckpoint()
    {
        Vector3 pos = GetLastCheckpointPosition();
        string name = GetLastCheckpointName();
        Debug.Log($"Last Checkpoint: {name} at {pos}");
    }
    
    private void OnDrawGizmos()
    {
        if (!showDebugGizmos) return;
        
        Gizmos.color = isPlayerInside ? activeGizmoColor : gizmoColor;
        
        if (triggerCollider != null)
        {
            if (triggerCollider is BoxCollider box)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawWireCube(box.center, box.size);
            }
            else if (triggerCollider is SphereCollider sphere)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawWireSphere(sphere.center, sphere.radius);
            }
            else if (triggerCollider is CapsuleCollider capsule)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Vector3 center = capsule.center;
                float radius = capsule.radius;
                float height = capsule.height;
                
                Gizmos.DrawWireSphere(center + Vector3.up * (height/2 - radius), radius);
                Gizmos.DrawWireSphere(center + Vector3.down * (height/2 - radius), radius);
            }
        }
        else
        {
            Gizmos.DrawWireCube(transform.position, Vector3.one);
        }
        
        if (triggerCameraSwitch && targetCamera != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(transform.position, targetCamera.transform.position);
        }
        
        if (isCheckpoint)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(transform.position + Vector3.up * 2f, 0.5f);
        }
        
        if (triggerSceneTransition)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawCube(transform.position + Vector3.up * 3f, Vector3.one * 0.5f);
        }
    }
}