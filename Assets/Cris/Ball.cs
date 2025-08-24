using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
public class Ball : MonoBehaviour
{
    Rigidbody rb;

    [Header("Impostazioni fisiche")]
    public float mass = 0.45f;          // Peso simile a un pallone da calcio
    public float drag = 0.05f;          // Resistenza lineare
    public float angularDrag = 0.1f;    // Resistenza rotazione
    public PhysicsMaterial ballMaterial; // Per il rimbalzo/attrito

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.mass = mass;
        rb.linearDamping = drag;
        rb.angularDamping = angularDrag;
        rb.interpolation = RigidbodyInterpolation.Interpolate; // rotolamento pi� fluido
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        // Applico il materiale fisico al collider se specificato
        SphereCollider col = GetComponent<SphereCollider>();
        if (ballMaterial != null)
            col.material = ballMaterial;
    }
}
