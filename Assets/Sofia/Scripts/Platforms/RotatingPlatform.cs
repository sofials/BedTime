using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class RotatingObject : MonoBehaviour
{
    public Vector3 rotationAxis = Vector3.up;
    public float rotationSpeed = 360f;
    private float speedMultiplier = 1f;
    private float originalRotationSpeed;

    [Header("Overlay Emission")]
    [SerializeField] private Color overlayColor = Color.red;
    [SerializeField] private float overlayIntensity = 2f; // Aumentato per più luminosità
    [Tooltip("Moltiplicatore aggiuntivo per HDR emission (valori alti = più luce)")]
    [SerializeField] private float hdrMultiplier = 3f;
    [Tooltip("Se true, mantiene anche il tint del Base Color oltre all'emission")]
    [SerializeField] private bool applyColorTint = true;

    [Header("Slowdown FX")]
    [SerializeField] private CFXR_EffectController slowdownEffect;

    [Header("Modalità Girandola")]
    public bool usePinwheelMode = false;
    public Transform visualToRotate;

    [Header("Rotazione attorno a oggetto")]
    public bool rotateAroundObject = false;
    public Transform targetObject;
    public bool maintainOrientation = true;

    [Header("Slowdown Custom Settings")]
    public bool useCustomSlowdown = false;
    [Tooltip("Velocità assoluta temporanea durante lo slowdown.")]
    public float customSlowdownFactor = 90f;

    private MeshRenderer meshRenderer;
    private bool patinaActive = false;

    // Per salvare i colori originali dei materiali
    private Dictionary<Material, Material> materialInstances = new Dictionary<Material, Material>();
    private Dictionary<Material, Color> originalBaseColors = new Dictionary<Material, Color>();
    private Dictionary<Material, Color> originalEmissionColors = new Dictionary<Material, Color>();
    
    // NUOVO: Salva i materiali originali al primo accesso
    private Material[] originalMaterials = null;
    private bool materialsInitialized = false;

    private const float DAMAGE_AMOUNT = 10f;

    void Awake()
    {
        meshRenderer = GetComponentInChildren<MeshRenderer>();
        if (meshRenderer == null)
        {
            Debug.LogWarning($"[RotatingObject] Nessun MeshRenderer trovato su {gameObject.name}");
        }
        else
        {
            // Salva i colori originali dei materiali SHARED (non istanze)
            foreach (Material mat in meshRenderer.sharedMaterials)
            {
                if (mat != null)
                {
                    if (mat.HasProperty("_BaseColor"))
                        originalBaseColors[mat] = mat.GetColor("_BaseColor");
                    if (mat.HasProperty("_EmissionColor"))
                        originalEmissionColors[mat] = mat.GetColor("_EmissionColor");
                }
            }
        }

        if (slowdownEffect != null)
        {
            slowdownEffect.gameObject.SetActive(false);
        }

        if ((rotateAroundObject || usePinwheelMode) && targetObject == null)
        {
            Debug.LogWarning($"[RotatingObject] Modalità attivata ma targetObject non assegnato su {gameObject.name}");
        }

        if (usePinwheelMode && visualToRotate == null)
        {
            Debug.LogWarning($"[RotatingObject] usePinwheelMode attivo ma nessun visualToRotate assegnato su {gameObject.name}");
        }
    }

    void Update()
    {
        float rotationThisFrame = rotationSpeed * speedMultiplier * Time.deltaTime;

        if (usePinwheelMode && visualToRotate != null)
        {
            visualToRotate.Rotate(rotationAxis.normalized, rotationThisFrame, Space.Self);
        }
        else if (rotateAroundObject && targetObject != null)
        {
            transform.RotateAround(targetObject.position, rotationAxis.normalized, rotationThisFrame);

            if (maintainOrientation)
            {
                transform.rotation = Quaternion.Euler(0, transform.rotation.eulerAngles.y, 0);
            }
        }
        else
        {
            transform.Rotate(rotationAxis.normalized, rotationThisFrame, Space.Self);
        }
    }


public void SetSpeedMultiplier(float multiplier)
{
    // NOTA: Questo metodo ora è usato solo per altri scopi, 
    // SlowdownAbility modifica direttamente rotationSpeed
    
    if (useCustomSlowdown)
    {
        // Salva la velocità originale solo se non già salvata
        if (originalRotationSpeed == 0f)
        {
            originalRotationSpeed = rotationSpeed;
        }
        
        // Applica la velocità custom direttamente
        rotationSpeed = customSlowdownFactor;
        Debug.Log($"[RotatingObject] {gameObject.name} - velocità custom impostata a {rotationSpeed} (originale: {originalRotationSpeed})");
    }
    else
    {
        speedMultiplier = multiplier;
        Debug.Log($"[RotatingObject] {gameObject.name} - speed multiplier impostato a {speedMultiplier}");
    }
}
public void RestoreOriginalSpeed()
{
    if (useCustomSlowdown)
    {
        if (originalRotationSpeed != 0f)
        {
            rotationSpeed = originalRotationSpeed;
            Debug.Log($"[RotatingObject] {gameObject.name} - velocità ripristinata a {rotationSpeed}");
        }
        else
        {
            Debug.LogWarning($"[RotatingObject] {gameObject.name} - originalRotationSpeed non salvata!");
        }
    }
    else
    {
        speedMultiplier = 1f;
        Debug.Log($"[RotatingObject] {gameObject.name} - speed multiplier ripristinato a 1.0");
    }
}

    public void PlaySlowdownEffect(float duration = 1f)
    {
        if (slowdownEffect != null)
        {
            StartCoroutine(SlowdownWithFxRoutine(duration));
        }
        else
        {
            StartCoroutine(SlowdownRoutine(duration));
        }
    }

    private IEnumerator SlowdownRoutine(float duration)
    {
        SetSpeedMultiplier(1f);
        yield return new WaitForSeconds(duration);
        RestoreOriginalSpeed();
    }

    private IEnumerator SlowdownWithFxRoutine(float duration)
    {
        slowdownEffect.gameObject.SetActive(true);
        slowdownEffect.PlayEffect();

        SetSpeedMultiplier(1f);
        yield return new WaitForSeconds(duration);

        RestoreOriginalSpeed();
        slowdownEffect.StopEffect();
        slowdownEffect.gameObject.SetActive(false);
    }

    // OVERLAY EMISSIVO LUMINOSO per URP Simple Lit
    public void SetOverlayActive(bool active)
    {
        Debug.Log($"[RotatingObject] *** SetOverlayActive({active}) chiamato su {gameObject.name} ***");
        
        if (meshRenderer == null) 
        {
            Debug.LogWarning($"[RotatingObject] MeshRenderer nullo su {gameObject.name}");
            return;
        }
        
        Debug.Log($"[RotatingObject] MeshRenderer OK, chiamando SetEmissiveOverlay({active})");
        SetEmissiveOverlay(active);
    }

    private void SetEmissiveOverlay(bool active)
    {
        Debug.Log($"[RotatingObject] SetEmissiveOverlay({active}) - inizio processing su {gameObject.name}");
        
        // INIZIALIZZA i materiali originali solo la prima volta
        if (!materialsInitialized)
        {
            originalMaterials = meshRenderer.sharedMaterials; // USA sharedMaterials per ottenere gli originali
            materialsInitialized = true;
            Debug.Log($"[RotatingObject] Materiali originali salvati: {originalMaterials.Length}");
        }
        
        Material[] currentMaterials = meshRenderer.materials; // Questi possono essere istanze
        bool materialsChanged = false;

        Debug.Log($"[RotatingObject] Materiali da processare: {currentMaterials.Length}");

        for (int i = 0; i < originalMaterials.Length; i++)
        {
            Material originalMat = originalMaterials[i];
            if (originalMat == null) continue;

            Debug.Log($"[RotatingObject] Processando materiale {i}: {originalMat.name}");

            Material instanceMat;

            // Crea istanza del materiale SOLO se non esiste ancora
            if (!materialInstances.ContainsKey(originalMat))
            {
                Material newInstance = new Material(originalMat);
                materialInstances[originalMat] = newInstance;
                currentMaterials[i] = newInstance;
                materialsChanged = true;
                instanceMat = newInstance;
                Debug.Log($"[RotatingObject] Creata PRIMA istanza per materiale {originalMat.name}");
            }
            else
            {
                // Usa l'istanza esistente
                instanceMat = materialInstances[originalMat];
                if (currentMaterials[i] != instanceMat)
                {
                    currentMaterials[i] = instanceMat;
                    materialsChanged = true;
                }
                Debug.Log($"[RotatingObject] Usando istanza ESISTENTE per materiale {originalMat.name}");
            }

            if (active)
            {
                Debug.Log($"[RotatingObject] ATTIVANDO overlay per materiale {instanceMat.name}");
                
                // EMISSION LUMINOSO (principale)
                if (instanceMat.HasProperty("_EmissionColor"))
                {
                    // Calcola colore emission HDR per massima luminosità
                    Color hdrEmission = overlayColor * overlayIntensity * hdrMultiplier;
                    instanceMat.SetColor("_EmissionColor", hdrEmission);
                    
                    // Abilita emission
                    instanceMat.EnableKeyword("_EMISSION");
                    
                    // Forza il material a essere emission-enabled
                    instanceMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                    
                    Debug.Log($"[RotatingObject] Emission attivata con colore {hdrEmission}");
                }
                else
                {
                    Debug.LogWarning($"[RotatingObject] Materiale {instanceMat.name} non ha _EmissionColor");
                }

                // BASE COLOR TINT (opzionale, per colorare anche la texture)
                if (applyColorTint && instanceMat.HasProperty("_BaseColor"))
                {
                    if (originalBaseColors.ContainsKey(originalMat))
                    {
                        Color originalColor = originalBaseColors[originalMat];
                        // Mescola il colore originale con l'overlay
                        Color tintedColor = Color.Lerp(originalColor, originalColor * overlayColor, 0.3f);
                        tintedColor.a = originalColor.a;
                        instanceMat.SetColor("_BaseColor", tintedColor);
                        Debug.Log($"[RotatingObject] BaseColor tint applicato");
                    }
                }
            }
            else
            {
                Debug.Log($"[RotatingObject] DISATTIVANDO overlay per materiale {instanceMat.name}");
                
                // Ripristina colori originali
                if (instanceMat.HasProperty("_EmissionColor") && originalEmissionColors.ContainsKey(originalMat))
                {
                    Color originalEmission = originalEmissionColors[originalMat];
                    instanceMat.SetColor("_EmissionColor", originalEmission);
                    
                    // Se l'originale non aveva emission, disabilitalo
                    if (originalEmission == Color.black || originalEmission.maxColorComponent <= 0.01f)
                    {
                        instanceMat.DisableKeyword("_EMISSION");
                        instanceMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                    }
                    
                    Debug.Log($"[RotatingObject] Emission disattivata, ripristinato colore originale {originalEmission}");
                }

                if (instanceMat.HasProperty("_BaseColor") && originalBaseColors.ContainsKey(originalMat))
                {
                    instanceMat.SetColor("_BaseColor", originalBaseColors[originalMat]);
                    Debug.Log($"[RotatingObject] BaseColor ripristinato");
                }
            }
        }

        if (materialsChanged)
        {
            meshRenderer.materials = currentMaterials;
            Debug.Log($"[RotatingObject] Materiali aggiornati nel renderer");
        }
        
        patinaActive = active;
        Debug.Log($"[RotatingObject] SetEmissiveOverlay completato - patinaActive = {patinaActive}");
    }

    private void OnTriggerEnter(Collider other)
    {
        var player = other.GetComponent<ThirdPersonController>();
        if (player != null && speedMultiplier > 0.99f)
        {
            player.TakeDamage(DAMAGE_AMOUNT);

            Rigidbody playerRb = player.GetComponent<Rigidbody>();
            if (playerRb != null)
            {
                Vector3 pushDirection = (player.transform.position - transform.position).normalized;
                float pushForce = 5f;
                playerRb.AddForce(pushDirection * pushForce, ForceMode.Impulse);
            }
        }
    }
}