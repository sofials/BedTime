using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class Gem : MonoBehaviour
{
    [Header("Gameplay")]
    [SerializeField] private int gemValue = 10;
    [SerializeField] private float respawnSec  = 10f;

    // Cache per efficienza
    private Collider  _coll;
    private Renderer[] _renderers;

    private void Awake()
    {
        _coll       = GetComponent<Collider>();
        _coll.isTrigger = true;           // Assicuriamoci che sia trigger
        _renderers  = GetComponentsInChildren<Renderer>(includeInactive: true);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        // ➜ Qui puoi aggiungere punteggio, suoni, particelle, ecc.
        Collect();
    }

    public int GetGemValue() => gemValue;

    public void Collect()
    {
        // Disattiviamo visivamente e collisioni
        SetActiveVisual(false);
        _coll.enabled = false;

        // Avviamo il timer di respawn
        StartCoroutine(RespawnCoroutine());
    }

    private IEnumerator RespawnCoroutine()
    {
        yield return new WaitForSeconds(respawnSec);

        // Riabilitiamo
        _coll.enabled = true;
        SetActiveVisual(true);
    }

    private void SetActiveVisual(bool state)
    {
        foreach (var r in _renderers)
            r.enabled = state;
    }
}
