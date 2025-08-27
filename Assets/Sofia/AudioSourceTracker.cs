using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class AudioSourceTracker : MonoBehaviour
{
    [Header("Tracking Settings")]
    public bool trackOnStart = true;
    public bool trackOnPlay = true;
    public bool trackOnStop = false;
    public float scanInterval = 0.5f; // Secondi tra ogni scan
    
    private Dictionary<AudioSource, bool> trackedSources = new Dictionary<AudioSource, bool>();
    private Coroutine trackingCoroutine;
    
    void Start()
    {
        if (trackOnStart)
        {
            StartTracking();
        }
    }
    
    public void StartTracking()
    {
        if (trackingCoroutine != null)
        {
            StopCoroutine(trackingCoroutine);
        }
        
        trackingCoroutine = StartCoroutine(TrackAudioSources());
        Debug.Log("[AudioTracker] Tracking iniziato");
    }
    
    public void StopTracking()
    {
        if (trackingCoroutine != null)
        {
            StopCoroutine(trackingCoroutine);
            trackingCoroutine = null;
        }
        
        Debug.Log("[AudioTracker] Tracking fermato");
    }
    
    private IEnumerator TrackAudioSources()
    {
        while (true)
        {
            // Trova tutti gli AudioSource nella scena
            AudioSource[] allAudioSources = FindObjectsOfType<AudioSource>();
            
            foreach (AudioSource audioSource in allAudioSources)
            {
                if (audioSource == null) continue;
                
                bool wasPlaying = trackedSources.ContainsKey(audioSource) ? trackedSources[audioSource] : false;
                bool isPlayingNow = audioSource.isPlaying;
                
                // Aggiorna lo stato
                trackedSources[audioSource] = isPlayingNow;
                
                // Log quando inizia a suonare
                if (!wasPlaying && isPlayingNow && trackOnPlay)
                {
                    LogAudioStart(audioSource);
                }
                
                // Log quando smette di suonare
                if (wasPlaying && !isPlayingNow && trackOnStop)
                {
                    LogAudioStop(audioSource);
                }
            }
            
            yield return new WaitForSeconds(scanInterval);
        }
    }
    
    private void LogAudioStart(AudioSource audioSource)
    {
        string objectName = audioSource.gameObject.name;
        string clipName = audioSource.clip != null ? audioSource.clip.name : "null";
        string parentName = audioSource.transform.parent != null ? audioSource.transform.parent.name : "none";
        
        Debug.Log($"[AudioTracker] 🔊 AUDIO START - Oggetto: '{objectName}' | Parent: '{parentName}' | Clip: '{clipName}' | Volume: {audioSource.volume:F3} | Loop: {audioSource.loop}");
        
        // Log posizione se è 3D
        if (audioSource.spatialBlend > 0)
        {
            Vector3 pos = audioSource.transform.position;
            Debug.Log($"[AudioTracker] 📍 Posizione 3D: ({pos.x:F2}, {pos.y:F2}, {pos.z:F2}) | Spatial: {audioSource.spatialBlend:F2} | MaxDistance: {audioSource.maxDistance}");
        }
        
        // Log gerarchia completa
        LogObjectHierarchy(audioSource.transform);
    }
    
    private void LogAudioStop(AudioSource audioSource)
    {
        string objectName = audioSource.gameObject.name;
        string clipName = audioSource.clip != null ? audioSource.clip.name : "null";
        
        Debug.Log($"[AudioTracker] 🔇 AUDIO STOP - Oggetto: '{objectName}' | Clip: '{clipName}'");
    }
    
    private void LogObjectHierarchy(Transform target)
    {
        List<string> hierarchy = new List<string>();
        Transform current = target;
        
        while (current != null)
        {
            hierarchy.Insert(0, current.name);
            current = current.parent;
        }
        
        string hierarchyPath = string.Join(" > ", hierarchy);
        Debug.Log($"[AudioTracker] 📂 Gerarchia: {hierarchyPath}");
    }
    
    [ContextMenu("Log All Active Audio Sources")]
    public void LogAllActiveAudioSources()
    {
        AudioSource[] allAudioSources = FindObjectsOfType<AudioSource>();
        int activeCount = 0;
        
        Debug.Log("[AudioTracker] === SCAN MANUALE AUDIO SOURCES ===");
        
        foreach (AudioSource audioSource in allAudioSources)
        {
            if (audioSource == null) continue;
            
            if (audioSource.isPlaying)
            {
                activeCount++;
                LogAudioStart(audioSource);
            }
            else if (audioSource.clip != null)
            {
                // Log anche quelli con clip ma non attivi
                Debug.Log($"[AudioTracker] 💤 INACTIVE - Oggetto: '{audioSource.gameObject.name}' | Clip: '{audioSource.clip.name}'");
            }
        }
        
        Debug.Log($"[AudioTracker] Totale AudioSources trovati: {allAudioSources.Length} | Attivi: {activeCount}");
    }
    
    void OnDestroy()
    {
        StopTracking();
    }
}