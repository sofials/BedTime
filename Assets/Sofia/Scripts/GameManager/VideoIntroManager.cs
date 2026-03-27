using UnityEngine;
using UnityEngine.Video;
using UnityEngine.UI;
using System.Collections;

public class VideoIntroManager : MonoBehaviour
{
    [Header("Video Setup")]
    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private RenderTexture videoRenderTexture;
    [SerializeField] private RawImage videoDisplayImage; // UI RawImage per mostrare il video
    [SerializeField] private GameObject videoPanel; // Panel che contiene il video
    
    [Header("Video Settings")]
    [SerializeField] private VideoClip introVideoClip;
    [SerializeField] private bool allowSkip = true;
    [SerializeField] private float fadeInDuration = 0.5f;
    [SerializeField] private float fadeOutDuration = 0.5f;
    
    [Header("UI References")]
    [SerializeField] private GameObject mainMenuUI; // Il menu principale
    [SerializeField] private CanvasGroup videoCanvasGroup; // Per fade in/out
    [SerializeField] private Text skipText; // Testo "Premi ESC per saltare" (opzionale)
    
    [Header("Scene Loading")]
    [SerializeField] private SceneController sceneController;
    [SerializeField] private string targetSceneName = "00 - Landing in the Dreamworld";
    
    private bool isVideoPlaying = false;
    private bool isVideoComplete = false;
    private bool isSkipping = false;
    
    public System.Action OnVideoComplete; // Evento per quando il video finisce

    private void Awake()
    {
        // Setup iniziale
        if (videoPlayer == null)
            videoPlayer = GetComponent<VideoPlayer>();
            
        if (videoCanvasGroup == null && videoPanel != null)
            videoCanvasGroup = videoPanel.GetComponent<CanvasGroup>();
    }

    private void Start()
    {
        // Nascondi il video panel all'inizio
        if (videoPanel != null)
            videoPanel.SetActive(false);
            
        SetVideoCanvasAlpha(0f);
    }

    private void Update()
    {
        // Gestione input per saltare il video
        if (isVideoPlaying && allowSkip && !isSkipping)
        {
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0))
            {
                SkipVideo();
            }
        }
    }

    /// <summary>
    /// Avvia la riproduzione del video intro
    /// </summary>
    public void StartVideoIntro()
    {
        if (introVideoClip == null)
        {
            Debug.LogWarning("[VideoIntroManager] Video clip non assegnato!");
            LoadTargetScene();
            return;
        }

        Debug.Log("[VideoIntroManager] Avvio video intro...");
        
        isVideoPlaying = true;
        isVideoComplete = false;
        isSkipping = false;
        
        // Nascondi il menu principale
        if (mainMenuUI != null)
            mainMenuUI.SetActive(false);
        
        // Mostra il panel del video
        if (videoPanel != null)
            videoPanel.SetActive(true);
        
        // Setup del video player
        SetupVideoPlayer();
        
        // Avvia la sequenza di riproduzione
        StartCoroutine(PlayVideoSequence());
    }

    /// <summary>
    /// Setup del Video Player
    /// </summary>
    private void SetupVideoPlayer()
    {
        if (videoPlayer == null) return;
        
        // Configurazione base
        videoPlayer.clip = introVideoClip;
        videoPlayer.playOnAwake = false;
        videoPlayer.isLooping = false;
        videoPlayer.skipOnDrop = true;
        
        // Configurazione render texture
        if (videoRenderTexture != null)
        {
            videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            videoPlayer.targetTexture = videoRenderTexture;
            
            // Assegna la render texture alla UI
            if (videoDisplayImage != null)
                videoDisplayImage.texture = videoRenderTexture;
        }
        else
        {
            // Fallback: render diretto su camera
            videoPlayer.renderMode = VideoRenderMode.CameraFarPlane;
        }
        
        // Eventi
        videoPlayer.loopPointReached += OnVideoFinished;
        videoPlayer.prepareCompleted += OnVideoPrepared;
        
        // Prepara il video
        videoPlayer.Prepare();
    }

    /// <summary>
    /// Sequenza completa di riproduzione video con fade
    /// </summary>
    private IEnumerator PlayVideoSequence()
    {
        // Fade in
        yield return StartCoroutine(FadeVideoCanvas(0f, 1f, fadeInDuration));
        
        // Mostra testo skip se abilitato
        if (skipText != null && allowSkip)
        {
            skipText.gameObject.SetActive(true);
            skipText.text = "Premi ESC, SPAZIO o clicca per saltare";
        }
        
        // Aspetta che il video sia pronto e poi riproduci
        yield return new WaitUntil(() => videoPlayer.isPrepared);
        
        Debug.Log("[VideoIntroManager] Video preparato, inizio riproduzione...");
        videoPlayer.Play();
        
        // Aspetta che il video finisca o venga saltato
        yield return new WaitUntil(() => isVideoComplete || isSkipping);
        
        // Nascondi testo skip
        if (skipText != null)
            skipText.gameObject.SetActive(false);
        
        // Fade out
        yield return StartCoroutine(FadeVideoCanvas(1f, 0f, fadeOutDuration));
        
        // Nascondi il panel del video
        if (videoPanel != null)
            videoPanel.SetActive(false);
        
        // Carica la scena target
        LoadTargetScene();
    }

    /// <summary>
    /// Fade del canvas del video
    /// </summary>
    private IEnumerator FadeVideoCanvas(float from, float to, float duration)
    {
        if (videoCanvasGroup == null) yield break;
        
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime; // Usa unscaled time
            float alpha = Mathf.Lerp(from, to, elapsed / duration);
            SetVideoCanvasAlpha(alpha);
            yield return null;
        }
        
        SetVideoCanvasAlpha(to);
    }

    /// <summary>
    /// Imposta l'alpha del canvas del video
    /// </summary>
    private void SetVideoCanvasAlpha(float alpha)
    {
        if (videoCanvasGroup != null)
            videoCanvasGroup.alpha = alpha;
    }

    /// <summary>
    /// Salta il video
    /// </summary>
    public void SkipVideo()
    {
        if (!isVideoPlaying || isSkipping) return;
        
        Debug.Log("[VideoIntroManager] Video saltato dall'utente");
        
        isSkipping = true;
        
        if (videoPlayer != null && videoPlayer.isPlaying)
            videoPlayer.Stop();
    }

    /// <summary>
    /// Callback quando il video è preparato
    /// </summary>
    private void OnVideoPrepared(VideoPlayer vp)
    {
        Debug.Log("[VideoIntroManager] Video preparato per la riproduzione");
    }

    /// <summary>
    /// Callback quando il video finisce naturalmente
    /// </summary>
    private void OnVideoFinished(VideoPlayer vp)
    {
        Debug.Log("[VideoIntroManager] Video completato naturalmente");
        isVideoComplete = true;
    }

    /// <summary>
    /// Carica la scena target
    /// </summary>
    private void LoadTargetScene()
    {
        Debug.Log($"[VideoIntroManager] Caricamento scena: {targetSceneName}");
        
        isVideoPlaying = false;
        
        // Cleanup
        if (videoPlayer != null)
        {
            videoPlayer.loopPointReached -= OnVideoFinished;
            videoPlayer.prepareCompleted -= OnVideoPrepared;
        }
        
        // Notifica evento
        OnVideoComplete?.Invoke();
        
        // Carica la scena
        if (sceneController != null)
        {
            sceneController.LoadScene(targetSceneName);
        }
        else
        {
            // Fallback
            UnityEngine.SceneManagement.SceneManager.LoadScene(targetSceneName);
        }
    }

    /// <summary>
    /// Metodo pubblico per avviare il video (da chiamare dal bottone Play)
    /// </summary>
    public void StartIntro()
    {
        StartVideoIntro();
    }

    /// <summary>
    /// Imposta la scena target (utile per configurazione dinamica)
    /// </summary>
    public void SetTargetScene(string sceneName)
    {
        targetSceneName = sceneName;
    }

    /// <summary>
    /// Cleanup quando l'oggetto viene distrutto
    /// </summary>
    private void OnDestroy()
    {
        if (videoPlayer != null)
        {
            videoPlayer.loopPointReached -= OnVideoFinished;
            videoPlayer.prepareCompleted -= OnVideoPrepared;
        }
    }

    // ========== DEBUG ==========
    
    [ContextMenu("Test - Start Video")]
    public void DebugStartVideo()
    {
        StartVideoIntro();
    }

    [ContextMenu("Test - Skip Video")]
    public void DebugSkipVideo()
    {
        SkipVideo();
    }
}