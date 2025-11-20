using UnityEngine;
using CartoonFX;

[RequireComponent(typeof(CFXR_Effect))]
public class LavaGeyser : MonoBehaviour
{
    [Header("Timing")]
    [Tooltip("Tempo di attesa prima del primo getto")]
    public float initialDelay = 2f;
    [Tooltip("Tempo tra un getto e l'altro")]
    public float intervalBetweenBursts = 5f;
    [Tooltip("Durata del getto di lava")]
    public float burstDuration = 3f;

    [Header("Damage")]
    [Tooltip("Riferimento alla sfera child che causa danno")]
    public LavaDamage lavaDamage;
    [Tooltip("Danno inflitto al player")]
    public float damage = 15f;
    [Tooltip("Forza del push sul player")]
    public float pushForce = 8f;

    private CFXR_Effect cfxrEffect;
    private ParticleSystem ps;
    private bool firstBurst = true;
    private float burstTimer = 0f;
    private bool isBursting = false;

    private void Awake()
    {
        cfxrEffect = GetComponent<CFXR_Effect>();
        ps = GetComponent<ParticleSystem>();

        // IMPORTANTE: Imposta clearBehavior su None per evitare auto-disattivazione
        if (cfxrEffect != null)
        {
            cfxrEffect.clearBehavior = CFXR_Effect.ClearBehavior.None;
        }

        // Ferma il PS all'inizio
        if (ps != null)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        // Disabilita la sfera del danno all'inizio
        if (lavaDamage != null)
        {
            lavaDamage.gameObject.SetActive(false);
        }
        else
        {
            Debug.LogWarning("LavaGeyser: Nessuna LavaDamage assegnata!");
        }
    }

    private void Start()
    {
        // Avvia il ciclo dei getti dopo l'initial delay
        Invoke(nameof(StartBurst), initialDelay);
    }

    private void StartBurst()
    {
        // Resetta lo stato del CFXR_Effect
        if (cfxrEffect != null)
        {
            cfxrEffect.ResetState();
        }

        // Play del ParticleSystem
        if (ps != null)
        {
            if (firstBurst)
            {
                ps.Play();
                firstBurst = false;
            }
            else
            {
                ps.Clear();
                ps.Play();
            }
        }
        
        // Attiva e fa partire il movimento della sfera del danno
        if (lavaDamage != null)
        {
            lavaDamage.gameObject.SetActive(true);
            lavaDamage.StartMovement();
        }

        // Inizia il timer per la durata del burst
        burstTimer = 0f;
        isBursting = true;
    }

    private void Update()
    {
        if (isBursting)
        {
            burstTimer += Time.deltaTime;

            // Controlla se è finita la durata del getto
            if (burstTimer >= burstDuration)
            {
                EndBurst();
            }
        }
    }

    private void EndBurst()
    {
        // Ferma l'emissione del ParticleSystem
        if (ps != null)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
        
        // Ferma e disabilita la sfera del danno
        if (lavaDamage != null)
        {
            lavaDamage.StopMovement();
            lavaDamage.gameObject.SetActive(false);
        }

        isBursting = false;

        // Programma il prossimo getto
        Invoke(nameof(StartBurst), intervalBetweenBursts);
    }
}