using UnityEngine;
using System.Collections;

public class CloudMover : MonoBehaviour
{
    [Header("Configurazione Movimento")]
    [SerializeField] private float movementSpeed = 6f; // Velocità ottimizzata per movimento visibile ma rapido
    [SerializeField] private AnimationCurve easeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    
    [Header("Destinazione Specifica - OBBLIGATORIA")]
    [SerializeField] private Transform specificTarget; // Target obbligatorio
    [SerializeField] private Vector3 specificPosition; // Oppure coordinate specifiche
    [SerializeField] private bool useTransformTarget = true; // true = usa Transform, false = usa Vector3
    
    [Header("Particle Systems")]
    [SerializeField] private ParticleSystem[] particleSystems; // PS da spegnere all'arrivo
    [SerializeField] private bool autoFindParticleSystems = true; // Trova automaticamente i PS figli
    
    private Vector3 startPosition;
    private bool isMoving = false;
    
    private void Start()
    {
        startPosition = transform.position;
        
        // Trova automaticamente tutti i particle systems figli se abilitato
        if (autoFindParticleSystems)
        {
            particleSystems = GetComponentsInChildren<ParticleSystem>();
        }
        
        // Valida le impostazioni
        ValidateSettings();
    }
    
    /// <summary>
    /// METODO PRINCIPALE - Inizia IMMEDIATAMENTE il movimento verso la destinazione specifica
    /// </summary>
    public void RevealLevelSection()
    {
        MoveToSpecificDestination();
    }
    
    /// <summary>
    /// Muove IMMEDIATAMENTE verso la destinazione specifica configurata
    /// </summary>
    public void MoveToSpecificDestination()
    {
        // Ferma qualsiasi movimento in corso
        if (isMoving)
        {
            StopMovement();
        }
        
        // Ottieni la destinazione
        Vector3 targetPosition = GetTargetPosition();
        
        // Valida la destinazione
        if (targetPosition == startPosition)
        {
            Debug.LogWarning($"Destinazione non configurata per {gameObject.name}! Configura specificTarget o specificPosition.");
            return;
        }
        
        // INIZIA IMMEDIATAMENTE il movimento
        StartCoroutine(MoveToDestinationCoroutine(targetPosition));
        
        Debug.Log($"Nuvola {gameObject.name} inizia IMMEDIATAMENTE il movimento verso {targetPosition}");
    }
    
    /// <summary>
    /// Ottiene la posizione target basata sulle impostazioni
    /// </summary>
    private Vector3 GetTargetPosition()
    {
        if (useTransformTarget && specificTarget != null)
        {
            return specificTarget.position;
        }
        else if (!useTransformTarget && specificPosition != Vector3.zero)
        {
            return specificPosition;
        }
        else
        {
            Debug.LogError($"Destinazione non configurata correttamente per {gameObject.name}!");
            return startPosition; // Fallback per evitare errori
        }
    }
    
    /// <summary>
    /// Coroutine ottimizzata per movimento immediato e fluido
    /// </summary>
    private IEnumerator MoveToDestinationCoroutine(Vector3 targetPosition)
    {
        isMoving = true;
        Vector3 initialPosition = transform.position;
        
        // Calcola durata basata sulla distanza e velocità
        float totalDistance = Vector3.Distance(initialPosition, targetPosition);
        float duration = totalDistance / movementSpeed;
        
        float elapsedTime = 0f;
        
        // Loop di movimento - inizia IMMEDIATAMENTE
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsedTime / duration);
            
            // Applica curva di easing per movimento fluido
            float easedProgress = easeCurve.Evaluate(progress);
            
            // Aggiorna posizione
            transform.position = Vector3.Lerp(initialPosition, targetPosition, easedProgress);
            
            yield return null; // Aspetta il prossimo frame
        }
        
        // Assicura posizione finale esatta
        transform.position = targetPosition;
        isMoving = false;
        
        // Spegni particle systems all'arrivo
        DisableParticleSystems();
        
        Debug.Log($"Nuvola {gameObject.name} è arrivata a destinazione: {targetPosition}");
    }
    
    /// <summary>
    /// Riporta la nuvola alla posizione iniziale
    /// </summary>
    public void ResetToStartPosition()
    {
        if (isMoving)
        {
            StopMovement();
        }
        
        StartCoroutine(ResetCoroutine());
    }
    
    private IEnumerator ResetCoroutine()
    {
        isMoving = true;
        Vector3 initialPosition = transform.position;
        float totalDistance = Vector3.Distance(initialPosition, startPosition);
        float duration = totalDistance / movementSpeed;
        float elapsedTime = 0f;
        
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsedTime / duration);
            float easedProgress = easeCurve.Evaluate(progress);
            
            transform.position = Vector3.Lerp(initialPosition, startPosition, easedProgress);
            yield return null;
        }
        
        transform.position = startPosition;
        isMoving = false;
        
        // Riaccendi particle systems quando torna all'inizio
        EnableParticleSystems();
        
        Debug.Log($"Nuvola {gameObject.name} è tornata alla posizione iniziale");
    }
    
    /// <summary>
    /// Spegne tutti i particle systems
    /// </summary>
    private void DisableParticleSystems()
    {
        if (particleSystems == null || particleSystems.Length == 0) return;
        
        foreach (ParticleSystem ps in particleSystems)
        {
            if (ps != null)
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.gameObject.SetActive(false);
            }
        }
        
        Debug.Log($"Spenti {particleSystems.Length} particle systems per {gameObject.name}");
    }
    
    /// <summary>
    /// Accende tutti i particle systems
    /// </summary>
    private void EnableParticleSystems()
    {
        if (particleSystems == null || particleSystems.Length == 0) return;
        
        foreach (ParticleSystem ps in particleSystems)
        {
            if (ps != null)
            {
                ps.gameObject.SetActive(true);
                ps.Play();
            }
        }
        
        Debug.Log($"Accesi {particleSystems.Length} particle systems per {gameObject.name}");
    }
    
    /// <summary>
    /// UTILITY: Imposta una destinazione specifica tramite coordinate
    /// </summary>
    /// <param name="targetPos">Posizione di destinazione</param>
    public void SetDestination(Vector3 targetPos)
    {
        specificPosition = targetPos;
        useTransformTarget = false;
    }
    
    /// <summary>
    /// UTILITY: Imposta una destinazione specifica tramite Transform
    /// </summary>
    /// <param name="target">Transform di destinazione</param>
    public void SetDestination(Transform target)
    {
        specificTarget = target;
        useTransformTarget = true;
    }
    
    /// <summary>
    /// Ferma immediatamente qualsiasi movimento in corso
    /// </summary>
    public void StopMovement()
    {
        StopAllCoroutines();
        isMoving = false;
    }
    
    /// <summary>
    /// Valida le impostazioni all'avvio
    /// </summary>
    private void ValidateSettings()
    {
        bool hasValidTarget = (useTransformTarget && specificTarget != null) || 
                              (!useTransformTarget && specificPosition != Vector3.zero);
        
        if (!hasValidTarget)
        {
            Debug.LogWarning($"⚠️ {gameObject.name}: Nessuna destinazione configurata! " +
                           $"Imposta 'Specific Target' (Transform) o 'Specific Position' (Vector3).");
        }
    }
    
    // Proprietà pubbliche per controllo esterno
    public bool IsMoving => isMoving;
    public Vector3 StartPosition => startPosition;
    public Vector3 CurrentTarget => GetTargetPosition();
}