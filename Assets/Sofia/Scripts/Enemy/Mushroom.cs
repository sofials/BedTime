using UnityEngine;
using System.Collections;

public class Mushroom : EnemyAI
{
    protected override void Awake()
    {
        base.Awake();

        // Valori personalizzati per il funghetto
        moveSpeed = 4f;
        maxHealth = 80f;
        timeBetweenAttacks = 1.5f;
        dizzyDuration = 1.8f;
        pushForce = 50f; // Forza spinta specifica per Mushroom

        currentHealth = maxHealth;

        if (agent != null)
            agent.speed = moveSpeed;
    }

    protected override void AttackPlayer()
    {
        // Logica attacco Mushroom:
        // Es. danno al player, effetti, spinta ecc.

        Debug.Log("Mushroom attacca il player!");

        // Qui puoi chiamare PushPlayer se vuoi applicare la spinta
        PushPlayer();

        // Altre logiche di attacco specifiche qui
    }
}
