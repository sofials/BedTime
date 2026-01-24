using UnityEngine;

/// <summary>
/// Traccia la velocità del player calcolandola dalla differenza di posizione.
/// Necessario per il sistema di mira predittiva dei nemici ranged.
/// Aggiungere al GameObject del Player.
/// </summary>
public class PlayerVelocityTracker : MonoBehaviour
{
    /// <summary>
    /// Velocità attuale del player (calcolata, non fisica)
    /// </summary>
    public Vector3 Velocity { get; private set; }
    
    /// <summary>
    /// Velocità smoothata per predizioni più stabili
    /// </summary>
    public Vector3 SmoothedVelocity { get; private set; }
    
    [Header("Settings")]
    [Tooltip("Fattore di smoothing per la velocità (0-1, più basso = più smooth)")]
    [Range(0.1f, 1f)]
    public float smoothingFactor = 0.5f;
    
    [Header("Debug")]
    public bool showDebug = false;
    
    private Vector3 lastPosition;
    
    private void Start()
    {
        lastPosition = transform.position;
        Velocity = Vector3.zero;
        SmoothedVelocity = Vector3.zero;
    }
    
    private void Update()
    {
        // Calcola velocità istantanea
        if (Time.deltaTime > 0)
        {
            Velocity = (transform.position - lastPosition) / Time.deltaTime;
        }
        
        // Smoothing per predizioni più stabili
        SmoothedVelocity = Vector3.Lerp(SmoothedVelocity, Velocity, smoothingFactor);
        
        lastPosition = transform.position;
        
        if (showDebug && Velocity.magnitude > 0.1f)
        {
            Debug.DrawRay(transform.position, Velocity.normalized * 3f, Color.blue);
            Debug.DrawRay(transform.position, SmoothedVelocity.normalized * 3f, Color.cyan);
        }
    }
    
    /// <summary>
    /// Restituisce true se il player si sta muovendo
    /// </summary>
    public bool IsMoving(float threshold = 0.5f)
    {
        return Velocity.magnitude > threshold;
    }
    
    /// <summary>
    /// Restituisce la direzione di movimento normalizzata
    /// </summary>
    public Vector3 GetMoveDirection()
    {
        return Velocity.magnitude > 0.1f ? Velocity.normalized : Vector3.zero;
    }
}