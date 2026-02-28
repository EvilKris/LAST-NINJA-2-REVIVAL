using UnityEngine;
using Unity.Cinemachine;

public class CameraZoneManager : MonoBehaviour
{
    public static CameraZoneManager Instance { get; private set; }

    [SerializeField] private int activePriority = 20;
    [SerializeField] private int inactivePriority = 10;

    private CinemachineCamera currentCamera;

    private void Awake()
    {
        Instance = this;
    }

    public CinemachineCamera GetCurrentCamera()
    {
        return currentCamera;
    }

    public void ActivateCamera(CinemachineCamera newCamera)
    {
        if (currentCamera != null && currentCamera != newCamera)
        {
            currentCamera.Priority = inactivePriority;
        }

        newCamera.Priority = activePriority;
        currentCamera = newCamera;

      
    }
}