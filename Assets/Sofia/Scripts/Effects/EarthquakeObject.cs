using System.Collections;
using UnityEngine;

public class EarthquakeObject : MonoBehaviour
{
    [Header("Earthquake Settings")]
    [SerializeField] private float shakeDuration = 3f;
    [SerializeField] private float shakeIntensity = 0.1f;
    [SerializeField] private AnimationCurve shakeCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);
    
    [Header("Collapse Settings")]
    [SerializeField] private float collapseDelay = 2f;
    [SerializeField] private float fallForce = 500f;
    [SerializeField] private float torqueForce = 100f;
    
    [Header("Testing")]
    [SerializeField] private bool triggerEarthquake = false;
    
    private Vector3 originalPosition;
    private Quaternion originalRotation;
    private Rigidbody rb;
    private bool isEarthquakeActive = false;
    private bool hasCollapsed = false;
    
    void Start()
    {
        // Salva posizione e rotazione originali
        originalPosition = transform.position;
        originalRotation = transform.rotation;
        
        // Ottieni componenti
        rb = GetComponent<Rigidbody>();
        
        // Assicurati che il Rigidbody sia kinematic all'inizio
        if (rb != null)
        {
            rb.isKinematic = true;
        }
    }
    
    void Update()
    {
        // Tasto per testare dall'inspector
        if (triggerEarthquake)
        {
            triggerEarthquake = false; // Reset del toggle
            TriggerEarthquake();
        }
    }
    
    /// <summary>
    /// Metodo pubblico per attivare il terremoto dall'esterno
    /// </summary>
    public void TriggerEarthquake()
    {
        if (!isEarthquakeActive && !hasCollapsed)
        {
            StartCoroutine(EarthquakeSequence());
        }
    }
    
    /// <summary>
    /// Metodo per resettare l'oggetto allo stato iniziale
    /// </summary>
    public void ResetObject()
    {
        StopAllCoroutines();
        
        transform.position = originalPosition;
        transform.rotation = originalRotation;
        
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        
        isEarthquakeActive = false;
        hasCollapsed = false;
    }
    
    private IEnumerator EarthquakeSequence()
    {
        isEarthquakeActive = true;
        
        // Avvia tremore e crollo contemporaneamente
        StartCoroutine(ShakeObject());
        StartCoroutine(CollapseAfterDelay());
        
        yield return new WaitForSeconds(shakeDuration + collapseDelay + 1f);
        
        isEarthquakeActive = false;
    }
    
    private IEnumerator ShakeObject()
    {
        float elapsed = 0f;
        
        while (elapsed < shakeDuration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / shakeDuration;
            
            // Usa la curva per controllare l'intensità nel tempo
            float currentIntensity = shakeCurve.Evaluate(progress) * shakeIntensity;
            
            // Calcola offset casuale per il tremore
            Vector3 randomOffset = new Vector3(
                Random.Range(-currentIntensity, currentIntensity),
                Random.Range(-currentIntensity * 0.5f, currentIntensity * 0.5f), // Meno movimento verticale
                Random.Range(-currentIntensity, currentIntensity)
            );
            
            // Applica tremore
            transform.position = originalPosition + randomOffset;
            
            // Piccole rotazioni casuali
            float rotationShake = Random.Range(-currentIntensity * 100f, currentIntensity * 100f);
            transform.rotation = originalRotation * Quaternion.Euler(0, 0, rotationShake);
            
            yield return null;
        }
        
        // Ritorna alla posizione originale dopo il tremore
        transform.position = originalPosition;
        transform.rotation = originalRotation;
    }
    
    private IEnumerator CollapseAfterDelay()
    {
        yield return new WaitForSeconds(collapseDelay);
        
        if (!hasCollapsed)
        {
            Collapse();
        }
    }
    
    private void Collapse()
    {
        hasCollapsed = true;
        
        // Attiva la fisica per il crollo
        if (rb != null)
        {
            rb.isKinematic = false;
            
            // Applica forze casuali per simulare il crollo
            Vector3 randomForce = new Vector3(
                Random.Range(-fallForce, fallForce),
                Random.Range(-fallForce * 0.5f, 0), // Forza verso il basso
                Random.Range(-fallForce, fallForce)
            );
            
            Vector3 randomTorque = new Vector3(
                Random.Range(-torqueForce, torqueForce),
                Random.Range(-torqueForce, torqueForce),
                Random.Range(-torqueForce, torqueForce)
            );
            
            rb.AddForce(randomForce);
            rb.AddTorque(randomTorque);
        }
    }
    
    // Metodo per chiamare il terremoto da altri script
    public bool IsEarthquakeActive => isEarthquakeActive;
    public bool HasCollapsed => hasCollapsed;
}