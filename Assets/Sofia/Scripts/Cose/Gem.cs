using UnityEngine;

public class Gem : MonoBehaviour
{
    [SerializeField] private int gemValue = 5; // Valore della gemma

    // Metodo per ottenere il valore della gemma
    public int GetGemValue()
    {
        return gemValue;
    }

    // Metodo per distruggere la gemma quando viene raccolta
    public void Collect()
    {
        // Puoi aggiungere effetti qui (particelle, suoni...)
        Destroy(gameObject);
    }
}
