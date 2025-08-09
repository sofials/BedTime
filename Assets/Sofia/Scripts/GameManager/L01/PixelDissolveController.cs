using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class PixelMaterialSettings
{
    [Header("Material Settings")]
    public Material material;
    
    [Header("Animation Settings")]
    [Range(0f, 5f)]
    public float animationDuration = 2f;
    
    [Range(0f, 2f)]
    public float delayBeforeStart = 0f;
    
    [Header("Wind Effect")]
    [Range(0f, 10f)]
    public float windStrength = 3f;
    
    public Vector3 windDirection = new Vector3(-1, 0.5f, 0.3f);
}

public class PixelDissolveController : MonoBehaviour
{
    [Header("Trigger Settings")]
    [SerializeField] private string triggerTag = "Player";
    
    [Header("Materials to Animate")]
    [SerializeField] private List<PixelMaterialSettings> pixelMaterials = new List<PixelMaterialSettings>();
    
    [Header("Global Settings")]
    [SerializeField] private bool triggerOnce = true; // Default true per effetto polvere
    [SerializeField] private bool debugMode = false;
    
    // Property IDs per performance
    private static readonly int EnablePixellationID = Shader.PropertyToID("_EnablePixellation");
    private static readonly int DissolveAmountID = Shader.PropertyToID("_DissolveAmount");
    private static readonly int DisplaceAmountID = Shader.PropertyToID("_DisplaceAmount");
    private static readonly int WindDirectionID = Shader.PropertyToID("_WindDirection");
    
    private bool hasTriggered = false;
    private List<Coroutine> activeCoroutines = new List<Coroutine>();
    
    void Start()
    {
        // Verifica che il collider sia configurato correttamente
        BoxCollider boxCollider = GetComponent<BoxCollider>();
        if (boxCollider == null)
        {
            Debug.LogError("PixelWindTrigger: BoxCollider component not found!");
            return;
        }
        
        if (!boxCollider.isTrigger)
        {
            Debug.LogWarning("PixelWindTrigger: BoxCollider should be set as 'Is Trigger'");
            boxCollider.isTrigger = true;
        }
        
        // Inizializza tutti i materiali
        InitializeMaterials();
        
        if (debugMode)
        {
            Debug.Log($"PixelWindTrigger initialized with {pixelMaterials.Count} materials");
        }
    }
    
    void InitializeMaterials()
    {
        foreach (var materialSetting in pixelMaterials)
        {
            if (materialSetting.material != null)
            {
                // Assicurati che la pixellation sia disabilitata all'inizio
                materialSetting.material.SetFloat(EnablePixellationID, 0f);
                materialSetting.material.SetFloat(DissolveAmountID, 0f);
                materialSetting.material.SetFloat(DisplaceAmountID, materialSetting.windStrength);
                materialSetting.material.SetVector(WindDirectionID, materialSetting.windDirection);
            }
        }
    }
    
    void OnTriggerEnter(Collider other)
    {
        // Verifica se è il giusto oggetto che ha triggerato
        if (!string.IsNullOrEmpty(triggerTag) && !other.CompareTag(triggerTag))
            return;
            
        // Se trigger once è attivo e già triggerato, ignora
        if (triggerOnce && hasTriggered)
            return;
        
        if (debugMode)
        {
            Debug.Log($"PixelWindTrigger: Triggered by {other.name}");
        }
        
        hasTriggered = true;
        StartPixelAnimation();
    }
    
    void OnTriggerExit(Collider other)
    {
        // Per effetto polvere, non facciamo nulla quando si esce dal trigger
        // I pixel rimangono dissolti/spazzati via
        if (debugMode)
        {
            Debug.Log($"PixelWindTrigger: {other.name} exited trigger (no reset for dust effect)");
        }
    }
    
    void StartPixelAnimation()
    {
        // Ferma tutte le coroutine attive
        StopAllActiveCoroutines();
        
        // Avvia nuove animazioni per ogni materiale
        foreach (var materialSetting in pixelMaterials)
        {
            if (materialSetting.material != null)
            {
                Coroutine coroutine = StartCoroutine(AnimatePixelMaterial(materialSetting, true));
                activeCoroutines.Add(coroutine);
            }
        }
    }
    
    void ResetPixelAnimation()
    {
        // Ferma tutte le coroutine attive
        StopAllActiveCoroutines();
        
        // Reset manuale - riporta tutti i materiali allo stato iniziale
        foreach (var materialSetting in pixelMaterials)
        {
            if (materialSetting.material != null)
            {
                Coroutine coroutine = StartCoroutine(ResetPixelMaterial(materialSetting));
                activeCoroutines.Add(coroutine);
            }
        }
        
        hasTriggered = false;
    }
    
    IEnumerator ResetPixelMaterial(PixelMaterialSettings settings)
    {
        if (settings.material == null) yield break;
        
        // Animazione di reset da dissolto a normale
        float startDissolve = settings.material.GetFloat(DissolveAmountID);
        float endDissolve = 0f;
        float duration = 1f; // Durata fissa per il reset
        
        float elapsedTime = 0f;
        
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / duration;
            float easedProgress = EaseInQuart(progress);
                
            float currentDissolve = Mathf.Lerp(startDissolve, endDissolve, easedProgress);
            settings.material.SetFloat(DissolveAmountID, currentDissolve);
            
            yield return null;
        }
        
        // Valori finali
        settings.material.SetFloat(DissolveAmountID, 0f);
        settings.material.SetFloat(EnablePixellationID, 0f);
        
        if (debugMode)
        {
            Debug.Log($"PixelWindTrigger: Reset completed for {settings.material.name}");
        }
    }
    
    IEnumerator AnimatePixelMaterial(PixelMaterialSettings settings, bool animate)
    {
        if (settings.material == null) yield break;
        
        // Attesa iniziale solo per l'animazione di dissolve
        if (animate && settings.delayBeforeStart > 0)
        {
            yield return new WaitForSeconds(settings.delayBeforeStart);
        }
        
        // Abilita la pixellation
        settings.material.SetFloat(EnablePixellationID, 1f);
        
        // Per effetto polvere, animiamo sempre da 0 a 1 (dissolve completo)
        float startDissolve = 0f;
        float endDissolve = 1f;
        float duration = settings.animationDuration;
        
        // Aggiorna le impostazioni del vento
        settings.material.SetFloat(DisplaceAmountID, settings.windStrength);
        settings.material.SetVector(WindDirectionID, settings.windDirection);
        
        // Animazione
        float elapsedTime = 0f;
        
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / duration;
            
            // Curva di easing per accelerazione (effetto vento che si intensifica)
            float easedProgress = EaseOutQuart(progress);
                
            float currentDissolve = Mathf.Lerp(startDissolve, endDissolve, easedProgress);
            settings.material.SetFloat(DissolveAmountID, currentDissolve);
            
            yield return null;
        }
        
        // Assicura il valore finale (completamente dissolto)
        settings.material.SetFloat(DissolveAmountID, endDissolve);
        
        if (debugMode)
        {
            Debug.Log($"PixelWindTrigger: Dust dissolve completed for {settings.material.name}");
        }
    }
    
    void StopAllActiveCoroutines()
    {
        foreach (var coroutine in activeCoroutines)
        {
            if (coroutine != null)
            {
                StopCoroutine(coroutine);
            }
        }
        activeCoroutines.Clear();
    }
    
    // Funzioni di easing per animazioni più fluide
    float EaseOutQuart(float t)
    {
        return 1f - Mathf.Pow(1f - t, 4f);
    }
    
    float EaseInQuart(float t)
    {
        return t * t * t * t;
    }
    
    // Metodi pubblici per controllo esterno
    [ContextMenu("Trigger Animation")]
    public void TriggerAnimationManually()
    {
        if (debugMode)
        {
            Debug.Log("PixelWindTrigger: Manual trigger activated");
        }
        
        hasTriggered = true;
        StartPixelAnimation();
    }
    
    [ContextMenu("Reset Animation")]
    public void ResetAnimationManually()
    {
        if (debugMode)
        {
            Debug.Log("PixelWindTrigger: Manual reset activated");
        }
        
        ResetPixelAnimation();
    }
    
    // Metodo per aggiungere materiali via script
    public void AddMaterial(Material material, float duration = 2f, float windStrength = 3f, Vector3 windDirection = default)
    {
        if (windDirection == default)
            windDirection = new Vector3(-1, 0.5f, 0.3f);
            
        PixelMaterialSettings newSetting = new PixelMaterialSettings
        {
            material = material,
            animationDuration = duration,
            windStrength = windStrength,
            windDirection = windDirection
        };
        
        pixelMaterials.Add(newSetting);
        
        // Inizializza il nuovo materiale
        if (material != null)
        {
            material.SetFloat(EnablePixellationID, 0f);
            material.SetFloat(DissolveAmountID, 0f);
            material.SetFloat(DisplaceAmountID, windStrength);
            material.SetVector(WindDirectionID, windDirection);
        }
    }
    
    void OnDrawGizmosSelected()
    {
        // Visualizza il trigger area nell'editor
        BoxCollider boxCollider = GetComponent<BoxCollider>();
        if (boxCollider != null)
        {
            Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(boxCollider.center, boxCollider.size);
            
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(boxCollider.center, boxCollider.size);
        }
        
        // Visualizza le direzioni del vento per ogni materiale
        foreach (var materialSetting in pixelMaterials)
        {
            if (materialSetting.material != null)
            {
                Gizmos.color = Color.cyan;
                Vector3 startPos = transform.position;
                Vector3 windDir = materialSetting.windDirection.normalized * materialSetting.windStrength;
                Gizmos.DrawRay(startPos, windDir);
                Gizmos.DrawSphere(startPos + windDir, 0.1f);
            }
        }
    }
}