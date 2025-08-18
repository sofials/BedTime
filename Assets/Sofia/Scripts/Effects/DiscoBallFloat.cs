using UnityEngine;

public class DiscoBallFloat : MonoBehaviour
{
    [Header("Rotazione")]
    public float velocitaRotazione = 30f; // Gradi per secondo
    public Vector3 asseRotazione = Vector3.up; // Asse Y di default
    
    [Header("Levitazione")]
    public float altezzaLevitazione = 0.5f; // Quanto si alza/abbassa
    public float velocitaLevitazione = 2f; // Velocità del movimento
    
    private Vector3 posizioneIniziale;
    private float tempoTrascorso;
    
    void Start()
    {
        // Salva la posizione iniziale
        posizioneIniziale = transform.position;
        
        // Inizializza con un offset casuale per variare il movimento
        tempoTrascorso = Random.Range(0f, Mathf.PI * 2f);
    }
    
    void Update()
    {
        // Rotazione continua
        transform.Rotate(asseRotazione * velocitaRotazione * Time.deltaTime);
        
        // Movimento di levitazione usando una funzione seno
        tempoTrascorso += Time.deltaTime * velocitaLevitazione;
        float offsetY = Mathf.Sin(tempoTrascorso) * altezzaLevitazione;
        
        // Applica la nuova posizione
        transform.position = posizioneIniziale + new Vector3(0f, offsetY, 0f);
    }
    
    // Funzione opzionale per cambiare i parametri a runtime
    public void ImpostaParametri(float nuovaVelocitaRotazione, float nuovaAltezzaLevitazione, float nuovaVelocitaLevitazione)
    {
        velocitaRotazione = nuovaVelocitaRotazione;
        altezzaLevitazione = nuovaAltezzaLevitazione;
        velocitaLevitazione = nuovaVelocitaLevitazione;
    }
}