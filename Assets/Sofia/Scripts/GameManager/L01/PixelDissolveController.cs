using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class PixelDissolveController : MonoBehaviour
{
    [Header("Target Objects")]
    [SerializeField] private List<GameObject> targetObjects = new List<GameObject>();
    
    [Header("Dissolve Settings")]
    [SerializeField] private Material dissolveMaterial;
    [SerializeField] private float dissolveDuration = 3f;
    [SerializeField] private AnimationCurve dissolveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    
    [Header("Pixelation Settings")]
    [SerializeField] private float initialPixelSize = 4f;
    [SerializeField] private float maxPixelSize = 64f;
    [SerializeField] private float pixelationDuration = 1f;
    [SerializeField] private AnimationCurve pixelationCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    
    [Header("Wave Effect")]
    [SerializeField] private Vector2 dissolveDirection = new Vector2(1f, 0.5f);
    [SerializeField] private float scrollSpeed = 2f;
    
    [Header("Timing")]
    [SerializeField] private bool sequentialDissolve = false;
    [SerializeField] private float delayBetweenObjects = 0.2f;
    [SerializeField] private bool randomizeDelay = true;
    [SerializeField] private Vector2 randomDelayRange = new Vector2(0f, 0.5f);
    
    [Header("Trigger Settings")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool triggerOnce = true;
    
    [Header("Audio (Optional)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip triggerSound;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugGizmos = true;
    
    private List<PixelDissolveTarget> dissolveTargets = new List<PixelDissolveTarget>();
    private bool hasTriggered = false;
    private Collider triggerCollider;
    
    private void Start()
    {
        SetupTrigger();
        SetupTargets();
    }
    
    private void SetupTrigger()
    {
        triggerCollider = GetComponent<Collider>();
        triggerCollider.isTrigger = true;
        
        if (triggerCollider == null)
        {
            Debug.LogError("PixelDissolveTrigger requires a Collider component!");
            return;
        }
        
        Debug.Log($"PixelDissolveTrigger setup on {gameObject.name}. Waiting for player with tag '{playerTag}'");
    }
    
    private void SetupTargets()
    {
        dissolveTargets.Clear();
        
        foreach (GameObject target in targetObjects)
        {
            if (target != null)
            {
                PixelDissolveTarget dissolveTarget = target.GetComponent<PixelDissolveTarget>();
                if (dissolveTarget == null)
                {
                    dissolveTarget = target.AddComponent<PixelDissolveTarget>();
                }
                
                // Configure the target
                dissolveTarget.SetupTarget(
                    dissolveMaterial, 
                    dissolveDuration, 
                    pixelationDuration,
                    pixelationCurve,
                    dissolveCurve,
                    initialPixelSize, 
                    maxPixelSize, 
                    dissolveDirection, 
                    scrollSpeed
                );
                
                dissolveTargets.Add(dissolveTarget);
            }
        }
        
        Debug.Log($"PixelDissolveTrigger: Setup {dissolveTargets.Count} target objects.");
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (hasTriggered && triggerOnce) return;
        
        if (other.CompareTag(playerTag))
        {
            Debug.Log($"Player entered trigger zone. Starting dissolve effect on {dissolveTargets.Count} objects.");
            TriggerDissolve();
        }
    }
    
    private void TriggerDissolve()
    {
        if (triggerOnce)
        {
            hasTriggered = true;
        }
        
        // Play sound effect
        if (audioSource != null && triggerSound != null)
        {
            audioSource.PlayOneShot(triggerSound);
        }
        
        if (sequentialDissolve)
        {
            StartCoroutine(SequentialDissolve());
        }
        else
        {
            StartCoroutine(SimultaneousDissolve());
        }
    }
    
    [ContextMenu("Test Trigger")]
    public void TestTrigger()
    {
        hasTriggered = false; // Reset for testing
        TriggerDissolve();
    }
    
    [ContextMenu("Reset All Targets")]
    public void ResetAllTargets()
    {
        hasTriggered = false;
        
        foreach (PixelDissolveTarget target in dissolveTargets)
        {
            if (target != null)
            {
                target.ResetEffect();
            }
        }
        
        Debug.Log("All dissolve targets have been reset.");
    }
    
    private IEnumerator SequentialDissolve()
    {
        for (int i = 0; i < dissolveTargets.Count; i++)
        {
            if (dissolveTargets[i] != null)
            {
                dissolveTargets[i].StartDissolve();
                
                float delay = delayBetweenObjects;
                if (randomizeDelay)
                {
                    delay = Random.Range(randomDelayRange.x, randomDelayRange.y);
                }
                
                yield return new WaitForSeconds(delay);
            }
        }
    }
    
    private IEnumerator SimultaneousDissolve()
    {
        // Start all dissolves simultaneously (with optional random delays)
        for (int i = 0; i < dissolveTargets.Count; i++)
        {
            if (dissolveTargets[i] != null)
            {
                float delay = 0f;
                if (randomizeDelay)
                {
                    delay = Random.Range(randomDelayRange.x, randomDelayRange.y);
                }
                
                StartCoroutine(DelayedDissolveStart(dissolveTargets[i], delay));
            }
        }
        
        yield return null;
    }
    
    private IEnumerator DelayedDissolveStart(PixelDissolveTarget target, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (target != null)
        {
            target.StartDissolve();
        }
    }
    
    private void OnDrawGizmos()
    {
        if (!showDebugGizmos) return;
        
        // Draw trigger zone
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            Gizmos.color = hasTriggered ? Color.red : Color.green;
            Gizmos.matrix = transform.localToWorldMatrix;
            
            if (col is BoxCollider box)
            {
                Gizmos.DrawWireCube(box.center, box.size);
            }
            else if (col is SphereCollider sphere)
            {
                Gizmos.DrawWireSphere(sphere.center, sphere.radius);
            }
            else if (col is CapsuleCollider capsule)
            {
                // Simplified capsule representation
                Gizmos.DrawWireCube(capsule.center, new Vector3(capsule.radius * 2, capsule.height, capsule.radius * 2));
            }
            
            Gizmos.matrix = Matrix4x4.identity;
        }
        
        // Draw connections to target objects
        Gizmos.color = Color.cyan;
        foreach (GameObject target in targetObjects)
        {
            if (target != null)
            {
                Gizmos.DrawLine(transform.position, target.transform.position);
                Gizmos.DrawWireCube(target.transform.position, Vector3.one * 0.3f);
            }
        }
    }
    
    private void OnValidate()
    {
        // Remove null references from the list
        for (int i = targetObjects.Count - 1; i >= 0; i--)
        {
            if (targetObjects[i] == null)
            {
                targetObjects.RemoveAt(i);
            }
        }
        
        // Ensure we have a trigger collider
        Collider col = GetComponent<Collider>();
        if (col != null && !col.isTrigger)
        {
            col.isTrigger = true;
        }
    }
}

// Componente per gestire ogni singolo oggetto target
public class PixelDissolveTarget : MonoBehaviour
{
    private Material originalMaterial;
    private Material materialInstance;
    private Renderer objectRenderer;
    private Coroutine dissolveCoroutine;
    
    // Shader properties
    private Material dissolveMaterial;
    private float dissolveDuration = 3f;
    private float pixelationDuration = 1f;
    private AnimationCurve pixelationCurve;
    private AnimationCurve dissolveCurve;
    private float initialPixelSize = 4f;
    private float maxPixelSize = 64f;
    private Vector2 dissolveDirection = Vector2.right;
    private float scrollSpeed = 2f;
    
    // Property IDs for performance
    private static readonly int PixelSizeID = Shader.PropertyToID("_PixelSize");
    private static readonly int PixelationStrengthID = Shader.PropertyToID("_PixelationStrength");
    private static readonly int DissolveProgressID = Shader.PropertyToID("_DissolveProgress");
    private static readonly int DissolveDirectionID = Shader.PropertyToID("_DissolveDirection");
    private static readonly int ScrollSpeedID = Shader.PropertyToID("_ScrollSpeed");
    private static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");
    
    public void SetupTarget(Material dissolveMat, float dissolveDur, float pixelDur,
                           AnimationCurve pixelCurve, AnimationCurve dissolveCurve,
                           float minPixel, float maxPixel, Vector2 direction, float speed)
    {
        dissolveMaterial = dissolveMat;
        dissolveDuration = dissolveDur;
        pixelationDuration = pixelDur;
        pixelationCurve = pixelCurve;
        this.dissolveCurve = dissolveCurve;
        initialPixelSize = minPixel;
        maxPixelSize = maxPixel;
        dissolveDirection = direction;
        scrollSpeed = speed;
        
        objectRenderer = GetComponent<Renderer>();
        if (objectRenderer != null)
        {
            originalMaterial = objectRenderer.material;
        }
        else
        {
            Debug.LogError($"No Renderer found on {gameObject.name}! PixelDissolve effect requires a Renderer component.");
        }
    }
    
    public void StartDissolve()
    {
        if (dissolveCoroutine != null)
        {
            StopCoroutine(dissolveCoroutine);
        }
        
        if (SetupMaterial())
        {
            dissolveCoroutine = StartCoroutine(DissolveSequence());
        }
    }
    
    public void ResetEffect()
    {
        if (dissolveCoroutine != null)
        {
            StopCoroutine(dissolveCoroutine);
            dissolveCoroutine = null;
        }
        
        if (objectRenderer != null && originalMaterial != null)
        {
            objectRenderer.material = originalMaterial;
        }
        
        if (materialInstance != null && materialInstance != dissolveMaterial)
        {
            DestroyImmediate(materialInstance);
            materialInstance = null;
        }
    }
    
    private bool SetupMaterial()
    {
        if (objectRenderer == null || dissolveMaterial == null)
        {
            Debug.LogError($"Cannot setup material for {gameObject.name}. Missing Renderer or Dissolve Material.");
            return false;
        }
        
        // Create material instance
        materialInstance = new Material(dissolveMaterial);
        
        // Copy base color from original material
        if (originalMaterial.HasProperty(BaseColorID))
        {
            materialInstance.SetColor(BaseColorID, originalMaterial.GetColor(BaseColorID));
        }
        else if (originalMaterial.HasProperty("_Color"))
        {
            materialInstance.SetColor(BaseColorID, originalMaterial.GetColor("_Color"));
        }
        
        objectRenderer.material = materialInstance;
        
        // Initialize shader properties
        materialInstance.SetFloat(PixelSizeID, initialPixelSize);
        materialInstance.SetFloat(PixelationStrengthID, 0f);
        materialInstance.SetFloat(DissolveProgressID, 0f);
        materialInstance.SetVector(DissolveDirectionID, new Vector4(dissolveDirection.x, dissolveDirection.y, 0f, 0f));
        materialInstance.SetFloat(ScrollSpeedID, scrollSpeed);
        
        return true;
    }
    
    private IEnumerator DissolveSequence()
    {
        if (materialInstance == null) yield break;
        
        // Phase 1: Pixelation
        yield return StartCoroutine(PixelationPhase());
        
        // Phase 2: Dissolve with wave effect
        yield return StartCoroutine(DissolvePhase());
        
        // Phase 3: Complete fade
        yield return StartCoroutine(FadePhase());
        
        Debug.Log($"Dissolve effect completed on {gameObject.name}");
    }
    
    private IEnumerator PixelationPhase()
    {
        float elapsedTime = 0f;
        
        while (elapsedTime < pixelationDuration)
        {
            elapsedTime += Time.deltaTime;
            float normalizedTime = elapsedTime / pixelationDuration;
            float curveValue = pixelationCurve != null ? pixelationCurve.Evaluate(normalizedTime) : normalizedTime;
            
            materialInstance.SetFloat(PixelationStrengthID, curveValue);
            materialInstance.SetFloat(PixelSizeID, Mathf.Lerp(initialPixelSize, maxPixelSize, curveValue));
            
            yield return null;
        }
        
        // Ensure final values are set
        materialInstance.SetFloat(PixelationStrengthID, 1f);
        materialInstance.SetFloat(PixelSizeID, maxPixelSize);
    }
    
    private IEnumerator DissolvePhase()
    {
        float elapsedTime = 0f;
        float dissolvePhaseDuration = dissolveDuration * 0.8f; // 80% of total duration for dissolve
        
        while (elapsedTime < dissolvePhaseDuration)
        {
            elapsedTime += Time.deltaTime;
            float normalizedTime = elapsedTime / dissolvePhaseDuration;
            float curveValue = dissolveCurve != null ? dissolveCurve.Evaluate(normalizedTime) : normalizedTime;
            
            // Don't go to full dissolve yet, leave room for fade phase
            materialInstance.SetFloat(DissolveProgressID, curveValue * 0.9f);
            
            yield return null;
        }
    }
    
    private IEnumerator FadePhase()
    {
        float elapsedTime = 0f;
        float fadePhaseDuration = dissolveDuration * 0.2f; // 20% of total duration for fade
        float startDissolve = 0.9f;
        
        while (elapsedTime < fadePhaseDuration)
        {
            elapsedTime += Time.deltaTime;
            float normalizedTime = elapsedTime / fadePhaseDuration;
            
            // Complete the dissolve effect
            materialInstance.SetFloat(DissolveProgressID, Mathf.Lerp(startDissolve, 1.1f, normalizedTime));
            
            yield return null;
        }
        
        // Optionally disable the object after dissolve
        // gameObject.SetActive(false);
    }
    
    private void OnDestroy()
    {
        // Clean up material instance
        if (materialInstance != null && materialInstance != dissolveMaterial)
        {
            DestroyImmediate(materialInstance);
        }
    }
}