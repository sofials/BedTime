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

    [Header("Damage Settings")]
    [SerializeField] private bool canDamagePlayer = false;
    [Tooltip("Se disabilitato, la piattaforma non farà danno al player")]
    [SerializeField] private float damageAmount = 10f;
    [Tooltip("Se true, fa solo trigger Hit senza danno quando canDamagePlayer è false")]
    [SerializeField] private bool triggerHitWhenNoDamage = false;

    // CAMBIATO: Array di MeshRenderer invece di uno singolo
    private MeshRenderer[] meshRenderers;
    private bool patinaActive = false;

    // Per salvare i colori originali dei materiali - ora per ogni renderer
    private Dictionary<MeshRenderer, Dictionary<Material, Material>> rendererMaterialInstances = new Dictionary<MeshRenderer, Dictionary<Material, Material>>();
    private Dictionary<MeshRenderer, Dictionary<Material, Color>> rendererOriginalBaseColors = new Dictionary<MeshRenderer, Dictionary<Material, Color>>();
    private Dictionary<MeshRenderer, Dictionary<Material, Color>> rendererOriginalEmissionColors = new Dictionary<MeshRenderer, Dictionary<Material, Color>>();
    
    // NUOVO: Salva i materiali originali per ogni renderer
    private Dictionary<MeshRenderer, Material[]> rendererOriginalMaterials = new Dictionary<MeshRenderer, Material[]>();
    private bool materialsInitialized = false;

    void Awake()
    {
        // CAMBIATO: Ottieni TUTTI i MeshRenderer (oggetto corrente + figli)
        meshRenderers = GetComponentsInChildren<MeshRenderer>();
        
        if (meshRenderers.Length == 0)
        {
            Debug.LogWarning($"[RotatingObject] Nessun MeshRenderer trovato su {gameObject.name} o sui suoi figli");
        }
        else
        {
            Debug.Log($"[RotatingObject] Trovati {meshRenderers.Length} MeshRenderer su {gameObject.name}");
            
            // Inizializza le strutture dati per ogni renderer
            foreach (MeshRenderer renderer in meshRenderers)
            {
                rendererMaterialInstances[renderer] = new Dictionary<Material, Material>();
                rendererOriginalBaseColors[renderer] = new Dictionary<Material, Color>();
                rendererOriginalEmissionColors[renderer] = new Dictionary<Material, Color>();
                
                // Salva i colori originali dei materiali SHARED per questo renderer
                foreach (Material mat in renderer.sharedMaterials)
                {
                    if (mat != null)
                    {
                        if (mat.HasProperty("_BaseColor"))
                            rendererOriginalBaseColors[renderer][mat] = mat.GetColor("_BaseColor");
                        if (mat.HasProperty("_EmissionColor"))
                            rendererOriginalEmissionColors[renderer][mat] = mat.GetColor("_EmissionColor");
                    }
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

    // NUOVO: Metodi pubblici per controllare il danno
    public void SetCanDamagePlayer(bool canDamage)
    {
        canDamagePlayer = canDamage;
        Debug.Log($"[RotatingObject] {gameObject.name} - danno al player: {(canDamage ? "ABILITATO" : "DISABILITATO")}");
    }

    public void ToggleDamage()
    {
        SetCanDamagePlayer(!canDamagePlayer);
    }

    public bool CanDamagePlayer => canDamagePlayer;

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

    // OVERLAY EMISSIVO LUMINOSO per URP Simple Lit - ORA PER TUTTI I RENDERER
    public void SetOverlayActive(bool active)
    {
        Debug.Log($"[RotatingObject] *** SetOverlayActive({active}) chiamato su {gameObject.name} ***");
        
        if (meshRenderers == null || meshRenderers.Length == 0) 
        {
            Debug.LogWarning($"[RotatingObject] Nessun MeshRenderer disponibile su {gameObject.name}");
            return;
        }
        
        Debug.Log($"[RotatingObject] Applicando overlay a {meshRenderers.Length} MeshRenderer");
        
        // CAMBIATO: Applica l'overlay a TUTTI i renderer
        foreach (MeshRenderer renderer in meshRenderers)
        {
            SetEmissiveOverlay(renderer, active);
        }
    }

    private void SetEmissiveOverlay(MeshRenderer meshRenderer, bool active)
    {
        Debug.Log($"[RotatingObject] SetEmissiveOverlay({active}) su renderer {meshRenderer.gameObject.name}");
        
        // INIZIALIZZA i materiali originali solo la prima volta per questo renderer
        if (!rendererOriginalMaterials.ContainsKey(meshRenderer))
        {
            rendererOriginalMaterials[meshRenderer] = meshRenderer.sharedMaterials;
            Debug.Log($"[RotatingObject] Materiali originali salvati per {meshRenderer.gameObject.name}: {rendererOriginalMaterials[meshRenderer].Length}");
        }
        
        Material[] originalMaterials = rendererOriginalMaterials[meshRenderer];
        Material[] currentMaterials = meshRenderer.materials;
        bool materialsChanged = false;

        Debug.Log($"[RotatingObject] Materiali da processare: {currentMaterials.Length}");

        for (int i = 0; i < originalMaterials.Length; i++)
        {
            Material originalMat = originalMaterials[i];
            if (originalMat == null) continue;

            Debug.Log($"[RotatingObject] Processando materiale {i}: {originalMat.name}");

            Material instanceMat;

            // Crea istanza del materiale SOLO se non esiste ancora per questo renderer
            if (!rendererMaterialInstances[meshRenderer].ContainsKey(originalMat))
            {
                Material newInstance = new Material(originalMat);
                rendererMaterialInstances[meshRenderer][originalMat] = newInstance;
                currentMaterials[i] = newInstance;
                materialsChanged = true;
                instanceMat = newInstance;
                Debug.Log($"[RotatingObject] Creata PRIMA istanza per materiale {originalMat.name} su {meshRenderer.gameObject.name}");
            }
            else
            {
                // Usa l'istanza esistente
                instanceMat = rendererMaterialInstances[meshRenderer][originalMat];
                if (currentMaterials[i] != instanceMat)
                {
                    currentMaterials[i] = instanceMat;
                    materialsChanged = true;
                }
                Debug.Log($"[RotatingObject] Usando istanza ESISTENTE per materiale {originalMat.name} su {meshRenderer.gameObject.name}");
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
                    if (rendererOriginalBaseColors[meshRenderer].ContainsKey(originalMat))
                    {
                        Color originalColor = rendererOriginalBaseColors[meshRenderer][originalMat];
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
                if (instanceMat.HasProperty("_EmissionColor") && rendererOriginalEmissionColors[meshRenderer].ContainsKey(originalMat))
                {
                    Color originalEmission = rendererOriginalEmissionColors[meshRenderer][originalMat];
                    instanceMat.SetColor("_EmissionColor", originalEmission);
                    
                    // Se l'originale non aveva emission, disabilitalo
                    if (originalEmission == Color.black || originalEmission.maxColorComponent <= 0.01f)
                    {
                        instanceMat.DisableKeyword("_EMISSION");
                        instanceMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                    }
                    
                    Debug.Log($"[RotatingObject] Emission disattivata, ripristinato colore originale {originalEmission}");
                }

                if (instanceMat.HasProperty("_BaseColor") && rendererOriginalBaseColors[meshRenderer].ContainsKey(originalMat))
                {
                    instanceMat.SetColor("_BaseColor", rendererOriginalBaseColors[meshRenderer][originalMat]);
                    Debug.Log($"[RotatingObject] BaseColor ripristinato");
                }
            }
        }

        if (materialsChanged)
        {
            meshRenderer.materials = currentMaterials;
            Debug.Log($"[RotatingObject] Materiali aggiornati nel renderer {meshRenderer.gameObject.name}");
        }
        
        patinaActive = active;
        Debug.Log($"[RotatingObject] SetEmissiveOverlay completato per {meshRenderer.gameObject.name} - patinaActive = {patinaActive}");
    }

    private void OnTriggerEnter(Collider other)
    {
        var player = other.GetComponent<ThirdPersonController>();
        if (player != null && speedMultiplier > 0.99f)
        {
            // NUOVO: Controllo del toggle danno
            if (canDamagePlayer)
            {
                // Comportamento originale: fa danno
                player.TakeDamage(damageAmount);
                Debug.Log($"[RotatingObject] {gameObject.name} ha fatto {damageAmount} danni al player");
            }
            else if (triggerHitWhenNoDamage)
            {
                // Nuovo comportamento: solo trigger Hit senza danno
                Animator playerAnimator = player.GetComponentInChildren<Animator>();
                if (playerAnimator != null)
                {
                    playerAnimator.SetTrigger("Hit");
                    Debug.Log($"[RotatingObject] {gameObject.name} ha triggerato Hit senza danno");
                }
            }
            else
            {
                Debug.Log($"[RotatingObject] {gameObject.name} - danno disabilitato, nessun effetto sul player");
                return; // Esci senza push se il danno è disabilitato e non si vuole il trigger
            }

            // Push del player (sempre attivo se c'è stato damage o hit)
            Rigidbody playerRb = player.GetComponent<Rigidbody>();
            if (playerRb != null)
            {
                Vector3 pushDirection = (player.transform.position - transform.position).normalized;
                float pushForce = 5f;
                playerRb.AddForce(pushDirection * pushForce, ForceMode.Impulse);
                Debug.Log($"[RotatingObject] Push applicato al player con forza {pushForce}");
            }
        }
    }
}