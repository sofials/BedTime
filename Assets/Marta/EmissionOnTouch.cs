using UnityEngine;

public class EmissionOnTouch : MonoBehaviour
{
    public Color emissionColor = Color.cyan;
    public float emissionIntensity = 3f;

    private Renderer rend;
    private Material mat;
    private Color originalEmission;

    void Start()
    {
        rend = GetComponent<Renderer>();
        mat = rend.material;

        // Salva emissione originale
        originalEmission = mat.GetColor("_EmissionColor");

        // Emissione spenta all'inizio
        mat.SetColor("_EmissionColor", Color.black);
        mat.DisableKeyword("_EMISSION");
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", emissionColor * emissionIntensity);
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            mat.SetColor("_EmissionColor", Color.black);
            mat.DisableKeyword("_EMISSION");
        }
    }
}

