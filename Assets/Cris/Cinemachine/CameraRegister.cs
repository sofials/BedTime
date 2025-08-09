using Unity.Cinemachine;
using UnityEngine;

public class CameraRegister : MonoBehaviour
{
    [Header("Auto Registration")]
    [SerializeField] private bool autoRegisterOnEnable = true;
    [SerializeField] private bool autoUnregisterOnDisable = true;
    [SerializeField] private bool forceRegisterOnStart = true; // Nuovo flag

    private CinemachineCamera virtualCamera;

    void Awake()
    {
        virtualCamera = GetComponent<CinemachineCamera>();
        if (virtualCamera == null)
        {
        }
    }

    void Start()
    {
        if (forceRegisterOnStart && virtualCamera != null)
        {
            CameraManager.Register(virtualCamera);
        }
    }

    private void OnEnable()
    {
        if (autoRegisterOnEnable && virtualCamera != null)
        {
            CameraManager.Register(virtualCamera);
        }
    }

    private void OnDisable()
    {
        if (autoUnregisterOnDisable && virtualCamera != null)
        {
            CameraManager.Unregister(virtualCamera);
        }
    }

    public void ManualRegister()
    {
        if (virtualCamera != null)
        {
            CameraManager.Register(virtualCamera);
        }
    }

    public void ManualUnregister()
    {
        if (virtualCamera != null)
        {
            CameraManager.Unregister(virtualCamera);
        }
    }
}