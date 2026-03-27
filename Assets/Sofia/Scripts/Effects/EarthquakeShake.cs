using UnityEngine;
using Unity.Cinemachine;
using System.Collections;

/// <summary>
/// Controller per camera shake durante terremoti
/// Richiede CinemachineImpulseSource configurato manualmente nell'inspector
/// </summary>
public class EarthquakeShake : MonoBehaviour
{
    [Header("Impulse Source (Assegna dall'Inspector)")]
    [SerializeField] private CinemachineImpulseSource impulseSource;
    [Tooltip("Se null, cerca CinemachineImpulseSource su questo GameObject")]
    
    [Header("Target Cameras (Opzionale)")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private CinemachineCamera targetVirtualCamera;
    [SerializeField] private bool autoFindCameras = true;
    
    [Header("Shake Settings")]
    [SerializeField] private float shakeIntensity = 2f;
    [SerializeField] private float shakeDuration = 5f;
    [SerializeField] private AnimationCurve shakeIntensityCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0.5f);
    [SerializeField] private float shakeFrequency = 5f; // Impulsi per secondo
    [SerializeField] private bool autoStop = true;
    
    [Header("Shake Pattern")]
    [SerializeField] private Vector3 shakeDirection = new Vector3(1f, 0.8f, 0.5f);
    [SerializeField] private bool randomizeDirection = true;
    [SerializeField] private float directionVariance = 0.3f;
    
    [Header("Advanced")]
    [SerializeField] private bool useRealtimeImpulse = false;
    [SerializeField] private float fadeOutDuration = 1f;
    
    [Header("Debug")]
    [SerializeField] private bool debugShake = false;
    [SerializeField] private bool testOnStart = false;
    [SerializeField] private KeyCode testKey = KeyCode.T;
    [SerializeField] private KeyCode startShakeKey = KeyCode.G;
    [SerializeField] private KeyCode stopShakeKey = KeyCode.H;
    
    // Stati
    private bool isShaking = false;
    private bool isFadingOut = false;
    private Coroutine shakeCoroutine;
    private float shakeStartTime;
    private float currentIntensityMultiplier = 1f;
    
    // Debug stats
    private int impulsesGenerated = 0;

    private void Start()
    {
        ValidateImpulseSource();
        
        if (autoFindCameras)
        {
            FindCameras();
        }
        
        if (debugShake)
        {
            DebugSystemInfo();
        }
        
        if (testOnStart)
        {
            Invoke(nameof(StartShake), 2f);
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(testKey))
        {
            TestImpulse();
        }
        
        if (Input.GetKeyDown(startShakeKey))
        {
            StartShake();
        }
        
        if (Input.GetKeyDown(stopShakeKey))
        {
            StopShake();
        }
        
        if (debugShake && Input.GetKeyDown(KeyCode.I))
        {
            DebugSystemInfo();
        }
    }

    private void ValidateImpulseSource()
    {
        if (impulseSource == null)
        {
            impulseSource = GetComponent<CinemachineImpulseSource>();
            
            if (impulseSource == null)
            {
                Debug.LogError($"[EarthquakeShake] CinemachineImpulseSource non trovato! Aggiungilo manualmente a '{name}' e assegnalo nell'inspector.");
                return;
            }
            else if (debugShake)
            {
                Debug.Log($"[EarthquakeShake] CinemachineImpulseSource trovato automaticamente su '{name}'");
            }
        }
        
        if (debugShake)
        {
            Debug.Log($"[EarthquakeShake] ImpulseSource configurato e pronto");
        }
    }

    private void FindCameras()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
            if (debugShake && targetCamera != null)
            {
                Debug.Log($"[EarthquakeShake] Main Camera trovata: '{targetCamera.name}'");
            }
        }
        
        if (targetVirtualCamera == null)
        {
            var vcams = FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None);
            foreach (var vcam in vcams)
            {
                var listener = vcam.GetComponent<CinemachineImpulseListener>();
                if (listener != null)
                {
                    targetVirtualCamera = vcam;
                    if (debugShake)
                    {
                        Debug.Log($"[EarthquakeShake] Virtual Camera con Listener trovata: '{vcam.name}'");
                    }
                    break;
                }
            }
        }
    }

    private void DebugSystemInfo()
    {
        Debug.Log("🔍 [EarthquakeShake] === DIAGNOSI SISTEMA ===");
        
        // Check Impulse Source
        if (impulseSource == null)
        {
            Debug.LogError("❌ [EarthquakeShake] CinemachineImpulseSource NON ASSEGNATO!");
            return;
        }
        
        Debug.Log($"✅ [EarthquakeShake] ImpulseSource OK e configurato");
        
        // Check Target Virtual Camera
        if (targetVirtualCamera != null)
        {
            var listener = targetVirtualCamera.GetComponent<CinemachineImpulseListener>();
            if (listener != null)
            {
                Debug.Log($"✅ [EarthquakeShake] Target VCam: '{targetVirtualCamera.name}' - Gain: {listener.Gain}");
                
                // Analisi dettagliata listener
                if (listener.Gain < 0.5f)
                {
                    Debug.LogWarning($"⚠️ [EarthquakeShake] Gain basso ({listener.Gain}) - Consiglio >= 1.0");
                }
                
                if (!listener.enabled)
                {
                    Debug.LogError("❌ [EarthquakeShake] ImpulseListener DISABILITATO!");
                }
            }
            else
            {
                Debug.LogError($"❌ [EarthquakeShake] NESSUN ImpulseListener su '{targetVirtualCamera.name}'!");
            }
        }
        else
        {
            Debug.LogWarning("⚠️ [EarthquakeShake] Nessuna Virtual Camera target assegnata");
        }
        
        // Check Target Camera
        if (targetCamera != null)
        {
            Debug.Log($"✅ [EarthquakeShake] Target Camera: '{targetCamera.name}' - MainCamera: {targetCamera.CompareTag("MainCamera")}");
        }
        else
        {
            Debug.LogWarning("⚠️ [EarthquakeShake] Nessuna Camera fisica assegnata");
        }
        
        Debug.Log($"🎛️ [EarthquakeShake] Config: Intensità={shakeIntensity}, Freq={shakeFrequency}Hz, Durata={shakeDuration}s");
        Debug.Log($"🎮 [EarthquakeShake] Controlli: T=Test, G=Start, H=Stop, I=Info");
    }

    /// <summary>
    /// Avvia il camera shake
    /// </summary>
    public void StartShake()
    {
        if (impulseSource == null)
        {
            Debug.LogError("[EarthquakeShake] Impossibile avviare shake: ImpulseSource non configurato!");
            return;
        }
        
        if (isShaking)
        {
            if (debugShake)
                Debug.LogWarning("[EarthquakeShake] Shake già in corso, riavvio...");
            StopShake(true);
        }
        
        isShaking = true;
        isFadingOut = false;
        currentIntensityMultiplier = 1f;
        shakeStartTime = Time.time;
        impulsesGenerated = 0;
        
        shakeCoroutine = StartCoroutine(ShakeSequence());
        
        if (debugShake)
            Debug.Log($"🚀 [EarthquakeShake] Shake avviato! Intensità: {shakeIntensity}, Durata: {shakeDuration}s");
    }

    /// <summary>
    /// Ferma il camera shake
    /// </summary>
    public void StopShake(bool immediate = false)
    {
        if (!isShaking) return;
        
        if (immediate || fadeOutDuration <= 0f)
        {
            if (shakeCoroutine != null)
            {
                StopCoroutine(shakeCoroutine);
                shakeCoroutine = null;
            }
            
            isShaking = false;
            isFadingOut = false;
            
            if (debugShake)
                Debug.Log($"🛑 [EarthquakeShake] Shake fermato. Impulsi generati: {impulsesGenerated}");
        }
        else
        {
            if (!isFadingOut)
            {
                isFadingOut = true;
                StartCoroutine(FadeOutShake());
                
                if (debugShake)
                    Debug.Log($"🌅 [EarthquakeShake] Fade out avviato ({fadeOutDuration}s)");
            }
        }
    }

    private IEnumerator ShakeSequence()
    {
        float elapsedTime = 0f;
        float impulseInterval = shakeFrequency > 0 ? 1f / shakeFrequency : 0.1f;
        float lastImpulseTime = 0f;
        
        if (debugShake)
            Debug.Log($"🎬 [EarthquakeShake] Sequenza iniziata - Impulso ogni {impulseInterval:F3}s");
        
        while (isShaking && elapsedTime < shakeDuration)
        {
            float deltaTime = useRealtimeImpulse ? Time.unscaledDeltaTime : Time.deltaTime;
            
            // Calcola intensità corrente
            float normalizedTime = elapsedTime / shakeDuration;
            float curveIntensity = shakeIntensityCurve.Evaluate(normalizedTime);
            float currentIntensity = shakeIntensity * curveIntensity * currentIntensityMultiplier;
            
            // Genera impulsi a intervalli regolari
            if (elapsedTime - lastImpulseTime >= impulseInterval)
            {
                GenerateShakeImpulse(currentIntensity);
                lastImpulseTime = elapsedTime;
            }
            
            elapsedTime += deltaTime;
            yield return null;
        }
        
        if (autoStop && isShaking)
        {
            if (debugShake)
                Debug.Log($"⏰ [EarthquakeShake] Auto-stop. Impulsi totali: {impulsesGenerated}");
            StopShake(true);
        }
        
        shakeCoroutine = null;
    }

    private IEnumerator FadeOutShake()
    {
        float fadeElapsed = 0f;
        float initialMultiplier = currentIntensityMultiplier;
        
        while (isFadingOut && fadeElapsed < fadeOutDuration)
        {
            float fadeProgress = fadeElapsed / fadeOutDuration;
            currentIntensityMultiplier = Mathf.Lerp(initialMultiplier, 0f, fadeProgress);
            
            fadeElapsed += Time.deltaTime;
            yield return null;
        }
        
        StopShake(true);
    }

    private void GenerateShakeImpulse(float intensity)
    {
        if (impulseSource == null || intensity <= 0f) return;
        
        Vector3 baseDirection = shakeDirection.normalized;
        
        if (randomizeDirection)
        {
            Vector3 randomVariation = new Vector3(
                Random.Range(-directionVariance, directionVariance),
                Random.Range(-directionVariance, directionVariance),
                Random.Range(-directionVariance, directionVariance)
            );
            baseDirection += randomVariation;
        }
        
        Vector3 impulseVelocity = new Vector3(
            baseDirection.x * intensity * Random.Range(0.8f, 1.2f),
            baseDirection.y * intensity * Random.Range(0.8f, 1.2f),
            baseDirection.z * intensity * Random.Range(0.8f, 1.2f)
        );
        
        // Genera l'impulso sul channel configurato
        impulseSource.GenerateImpulse(impulseVelocity);
        impulsesGenerated++;
        
        if (debugShake && impulsesGenerated % 15 == 1)
        {
            Debug.Log($"💥 [EarthquakeShake] Impulso #{impulsesGenerated}: intensità={intensity:F2}, velocity={impulseVelocity}");
        }
    }

    // ========================
    // METODI PUBBLICI
    // ========================
    
    /// <summary>
    /// Genera un singolo impulso di test
    /// </summary>
    public void TestImpulse(float testIntensity = -1f)
    {
        if (impulseSource == null)
        {
            Debug.LogError("[EarthquakeShake] Impossibile testare: ImpulseSource non configurato!");
            return;
        }
        
        if (testIntensity < 0) testIntensity = shakeIntensity;
        
        if (debugShake)
            Debug.Log($"🧪 [EarthquakeShake] Test impulse - Intensità: {testIntensity}");
        
        GenerateShakeImpulse(testIntensity);
        
        // Feedback immediato
        if (debugShake)
        {
            StartCoroutine(TestFeedback());
        }
    }
    
    private IEnumerator TestFeedback()
    {
        yield return new WaitForSeconds(0.2f);
        
        if (targetVirtualCamera != null)
        {
            var listener = targetVirtualCamera.GetComponent<CinemachineImpulseListener>();
            if (listener != null)
            {
                Debug.Log($"📹 [EarthquakeShake] Test completato su '{targetVirtualCamera.name}' - Listener enabled: {listener.enabled}");
            }
        }
    }
    
    /// <summary>
    /// Cambia l'intensità durante lo shake
    /// </summary>
    public void SetIntensity(float newIntensity)
    {
        shakeIntensity = Mathf.Max(0f, newIntensity);
        if (debugShake)
            Debug.Log($"🎛️ [EarthquakeShake] Intensità aggiornata: {shakeIntensity}");
    }
    
    /// <summary>
    /// Pausa/riprende lo shake
    /// </summary>
    public void PauseShake(bool pause)
    {
        currentIntensityMultiplier = pause ? 0f : 1f;
        if (debugShake)
            Debug.Log($"⏸️ [EarthquakeShake] Shake {(pause ? "pausato" : "ripreso")}");
    }
    
    // Proprietà pubbliche
    public bool IsShaking => isShaking;
    public bool IsFadingOut => isFadingOut;
    public float CurrentIntensity => shakeIntensity * currentIntensityMultiplier;
    public float ShakeProgress => isShaking ? (Time.time - shakeStartTime) / shakeDuration : 0f;
    public int ImpulsesGenerated => impulsesGenerated;
    public CinemachineImpulseSource ImpulseSource => impulseSource;
    
    // ========================
    // CLEANUP E VALIDAZIONE
    // ========================
    
    private void OnDestroy()
    {
        StopShake(true);
    }
    
    private void OnDisable()
    {
        StopShake(true);
    }
    
    private void OnValidate()
    {
        shakeIntensity = Mathf.Max(0f, shakeIntensity);
        shakeDuration = Mathf.Max(0.1f, shakeDuration);
        shakeFrequency = Mathf.Max(0.1f, shakeFrequency);
        fadeOutDuration = Mathf.Max(0f, fadeOutDuration);
        directionVariance = Mathf.Clamp01(directionVariance);
        
        if (shakeIntensityCurve == null || shakeIntensityCurve.keys.Length == 0)
        {
            shakeIntensityCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0.5f);
        }
    }

    // ========================
    // DEBUG GIZMOS
    // ========================
    
    private void OnDrawGizmosSelected()
    {
        if (isShaking)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, 2f);
            
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(transform.position, shakeDirection.normalized * shakeIntensity);
        }
        
        // Visualizza connessioni
        if (impulseSource != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(transform.position, Vector3.one * 0.5f);
        }
        
        if (targetVirtualCamera != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, targetVirtualCamera.transform.position);
        }
    }
}