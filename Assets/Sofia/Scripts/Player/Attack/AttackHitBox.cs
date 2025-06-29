using UnityEngine;

public class AttackHitBox : MonoBehaviour
{
    [SerializeField] private Collider hitboxCollider;

    void Awake()
    {
        if (hitboxCollider == null)
            hitboxCollider = GetComponent<Collider>();

        if (hitboxCollider != null)
            hitboxCollider.enabled = false;
    }
}