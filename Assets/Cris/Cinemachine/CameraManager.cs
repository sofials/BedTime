using System.Collections.Generic;
using System.Linq;
using Unity.Cinemachine;
using UnityEngine;

[System.Serializable]
public class CameraTransition
{
    public float blendDuration = 1f;
    public CinemachineBlendDefinition.Styles blendStyle = CinemachineBlendDefinition.Styles.EaseInOut;
}

public class CameraManager : MonoBehaviour
{
    [Header("Camera System")]
    static List<CinemachineCamera> cameras = new List<CinemachineCamera>();
    static Dictionary<CinemachineCamera, int> originalPriorities = new Dictionary<CinemachineCamera, int>();

    public static CinemachineCamera ActiveCamera = null;
    [SerializeField] private static int basePriority = 10;
    [SerializeField] private static int activePriority = 100;

    [Header("Transition Settings")]
    public CameraTransition defaultTransition = new CameraTransition();
    private static CinemachineBrain brain;
    private static CameraManager instance;

    void Start()
    {
        instance = this;
        if (brain == null)
            brain = Camera.main?.GetComponent<CinemachineBrain>();

        // Debug: Mostra tutte le camere registrate
        LogRegisteredCameras();

        // Forza la registrazione di tutte le camere Cinemachine nella scena
        RegisterAllCamerasInScene();
    }

    public static void LogRegisteredCameras()
    {
        for (int i = 0; i < cameras.Count; i++)
        {
            var cam = cameras[i];
        }
    }

    public static void RegisterAllCamerasInScene()
    {
        var allCameras = FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.InstanceID);

        foreach (var cam in allCameras)
        {
            Register(cam);
        }
    }

    public static bool IsActiveCamera(CinemachineCamera camera)
    {
        return camera == ActiveCamera;
    }

    public static void SwitchCamera(CinemachineCamera newCamera)
    {
        if (newCamera == null)
        {
            Debug.LogError("SwitchCamera: newCamera is null!");
            return;
        }

        if (newCamera == ActiveCamera)
        {
            return;
        }

        // Log del cambio camera
        Debug.Log($"[CameraManager] Cambio camera: da {(ActiveCamera != null ? ActiveCamera.name : "nessuna")} a {newCamera.name}");

        // Verifica se la camera è registrata
        if (!cameras.Contains(newCamera))
        {
            Register(newCamera);
        }

        if (instance?.defaultTransition != null && brain != null)
        {
            brain.DefaultBlend.Time = instance.defaultTransition.blendDuration;
            brain.DefaultBlend.Style = instance.defaultTransition.blendStyle;
        }

        newCamera.Priority = activePriority;
        ActiveCamera = newCamera;

        foreach (CinemachineCamera cam in cameras.Where(c => c != newCamera))
        {
            int priority = originalPriorities.ContainsKey(cam) ? originalPriorities[cam] : basePriority;
            cam.Priority = priority;
        }

        LogRegisteredCameras();
         ThirdPersonController.NotifyAllControllersOfCameraChange();
    }

    public static void SwitchCameraByName(string cameraName)
    {
        var camera = GetCameraByName(cameraName);
        if (camera != null)
            SwitchCamera(camera);
        else
            Debug.LogError($"Camera with name '{cameraName}' not found!");
    }

    public static void Register(CinemachineCamera camera)
    {
        if (camera == null)
        {
            return;
        }

        if (cameras.Contains(camera))
        {
            return;
        }

        cameras.Add(camera);

        if (!originalPriorities.ContainsKey(camera))
            originalPriorities[camera] = camera.Priority;

        if (ActiveCamera == null)
        {
            SwitchCamera(camera);
        }
        else
            camera.Priority = basePriority;
    }

    public static void Unregister(CinemachineCamera camera)
    {
        if (camera == null) return;

        cameras.Remove(camera);
        originalPriorities.Remove(camera);

        if (ActiveCamera == camera)
        {
            ActiveCamera = null;
            if (cameras.Count > 0)
            {
                SwitchCamera(cameras[0]);
            }
        }
    }

    public static CinemachineCamera GetCameraByName(string name)
    {
        return cameras.FirstOrDefault(cam => cam.name == name);
    }

    public static List<CinemachineCamera> GetAllCameras()
    {
        return new List<CinemachineCamera>(cameras);
    }

    public static void SetCameraPriority(CinemachineCamera camera, int priority)
    {
        if (camera != null && cameras.Contains(camera))
        {
            originalPriorities[camera] = priority;
            if (camera != ActiveCamera)
                camera.Priority = priority;
        }
    }
}