using UnityEngine;
using Unity.Cinemachine;
using DG.Tweening;

public class CameraZoneManager : MonoBehaviour
{
    public static CameraZoneManager Instance { get; private set; }

    [SerializeField] private int activePriority = 20;
    [SerializeField] private int inactivePriority = 10;

    [Header("Clinch Zoom")]
    [SerializeField] private float clinchFovOffset = -10f;
    [SerializeField] private float clinchEaseInDuration = 0.4f;
    [SerializeField] private float clinchEaseOutDuration = 0.6f;

    private CinemachineCamera currentCamera;
    private Tween _clinchZoomTween;

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

    public void SetClinchZoom(bool easeIn)
    {
        if (currentCamera == null) return;

        _clinchZoomTween?.Kill();

        float targetFov = currentCamera.Lens.FieldOfView + (easeIn ? clinchFovOffset : -clinchFovOffset);
        float duration = easeIn ? clinchEaseInDuration : clinchEaseOutDuration;

        _clinchZoomTween = DOTween
            .To(() => currentCamera.Lens.FieldOfView, x => currentCamera.Lens.FieldOfView = x, targetFov, duration)
            .SetEase(Ease.InOutSine)
            .OnComplete(() => _clinchZoomTween = null);
    }
}