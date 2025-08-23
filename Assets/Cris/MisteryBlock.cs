using UnityEngine;
using System.Collections;

public class MysteryBlock : MonoBehaviour
{
    public float bounceHeight = 0.3f;
    public float bounceSpeed = 4f;
    public GameObject rewardPrefab;
    public Transform spawnPoint;
    public GameObject usedBlockPrefab;

    private Vector3 originalPosition;
    private bool isUsed = false;
    private Collider blockCollider;

    private void Awake()
    {
        originalPosition = transform.position;
        blockCollider = GetComponent<Collider>();
        if (blockCollider == null)
            blockCollider = gameObject.AddComponent<BoxCollider>();
    }

    private void Update()
    {
        if (isUsed) return;

        // Controlla se qualche hitbox del player sta toccando il blocco
        Collider[] hits = Physics.OverlapBox(blockCollider.bounds.center, blockCollider.bounds.extents, transform.rotation);
        foreach (Collider hit in hits)
        {
            if (hit.CompareTag("PlayerAttackHitbox"))
            {
                var playerAttack = hit.GetComponentInParent<PlayerAttack>();
                if (playerAttack != null && playerAttack.isAttacking)
                {
                    isUsed = true;
                    ActivateBlock();
                    playerAttack.RegisterSuccessfulHit();
                    break;
                }
            }
        }
    }

    private void ActivateBlock()
    {
        StartCoroutine(BounceAnimation());
    }

    private IEnumerator BounceAnimation()
    {
        Vector3 target = originalPosition + Vector3.up * bounceHeight;
        float t = 0f;

        while (t < 1f)
        {
            transform.position = Vector3.Lerp(originalPosition, target, t);
            t += Time.deltaTime * bounceSpeed;
            yield return null;
        }

        t = 0f;
        while (t < 1f)
        {
            transform.position = Vector3.Lerp(target, originalPosition, t);
            t += Time.deltaTime * bounceSpeed;
            yield return null;
        }

        transform.position = originalPosition;

        // Spawna la ricompensa qui, dopo l'animazione e il delay
        yield return new WaitForSeconds(1f);
        if (rewardPrefab && spawnPoint)
            Instantiate(rewardPrefab, spawnPoint.position, Quaternion.identity);

        if (usedBlockPrefab)
        {
            Instantiate(usedBlockPrefab, transform.position, transform.rotation);
            Destroy(gameObject);
        }
    }
}
