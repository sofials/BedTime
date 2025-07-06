using UnityEngine;

public class RotatingObject : MonoBehaviour
{
    public Vector3 rotationAxis = Vector3.up;     // Direzione della rotazione
    public float rotationSpeed = 360f;            // Velocità in gradi al secondo
    private float speedMultiplier = 1f;           // Moltiplicatore (per slowdown)

    void Update()
    {
        transform.Rotate(rotationAxis.normalized, rotationSpeed * speedMultiplier * Time.deltaTime);
    }

    public void SetSpeedMultiplier(float multiplier)
    {
        speedMultiplier = multiplier;
        Debug.Log($"[RotatingObject] {gameObject.name} speed multiplier impostato a {multiplier}");
    }
}
