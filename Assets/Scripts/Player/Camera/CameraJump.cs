using UnityEngine;
using Unity.Cinemachine;

public class CameraJumpAdjust : MonoBehaviour
{
    public CinemachineCamera cineCamera;
    public Transform player;
    public float jumpYOffset = 0.5f;
    public float lerpSpeed = 2f;

    private Vector3 defaultOffset;
    private Cinemachine3rdPersonFollow thirdPersonFollow;

    void Start()
    {
        // Assicurati che il componente Cinemachine3rdPersonFollow sia presente
        thirdPersonFollow = cineCamera.GetComponentInChildren<Cinemachine3rdPersonFollow>();
        if (thirdPersonFollow != null)
        {
            defaultOffset = thirdPersonFollow.ShoulderOffset;
        }
        else
        {
            Debug.LogError("Cinemachine3rdPersonFollow non trovato nel CinemachineCamera.");
        }
    }

    void Update()
    {
        if (thirdPersonFollow == null) return;

        bool isJumping = !IsGrounded();
        Vector3 targetOffset = defaultOffset;

        if (isJumping)
            targetOffset.y += jumpYOffset;

        thirdPersonFollow.ShoulderOffset = Vector3.Lerp(
            thirdPersonFollow.ShoulderOffset,
            targetOffset,
            Time.deltaTime * lerpSpeed
        );
    }

    bool IsGrounded()
    {
        // Sostituisci con il tuo controllo effettivo!
        return Physics.Raycast(player.position, Vector3.down, 0.2f);
    }
}
