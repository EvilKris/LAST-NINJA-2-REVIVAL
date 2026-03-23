using UnityEngine;
using Unity.Cinemachine;
using DG.Tweening;

/// <summary>
/// Manages Cinemachine camera zone transitions. When the brain is mid-blend,
/// incoming camera activations are queued and applied once the current
/// transition finishes, preventing jarring mid-ease camera cuts.
/// </summary>
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

    /// <summary>Cached brain reference used to detect active blends.</summary>
    private CinemachineBrain _brain;

    /// <summary>Camera queued while the brain is mid-blend or clinch-locked; applied when the lock clears.</summary>
    private CinemachineCamera _pendingCamera;

    /// <summary>True from clinch ease-in start until ease-out completes. Camera changes are blocked during this window.</summary>
    private bool _clinchLocked;

    private void Awake()
    {
        Instance = this;
        _brain = FindAnyObjectByType<CinemachineBrain>();
    }

    private void LateUpdate()
    {
        // Flush the queued camera once both the blend and clinch lock are clear
        if (_pendingCamera != null && !_clinchLocked
            && (_brain == null || !_brain.IsBlending))
        {
            CinemachineCamera cam = _pendingCamera;
            _pendingCamera = null;
            ActivateCameraImmediate(cam);
        }
    }

    public CinemachineCamera GetCurrentCamera()
    {
        return currentCamera;
    }

    /// <summary>
    /// Returns the effective camera yaw for movement calculations.
    /// During a Cinemachine blend the brain smoothly interpolates between
    /// the outgoing and incoming cameras, so we return the brain's
    /// (main-camera) yaw which tracks the blend progress exactly.
    /// Outside of a blend we fall back to the virtual camera's yaw.
    /// </summary>
    public float GetBlendedCameraYaw()
    {
        // While the brain is blending, its output (the main camera) already
        // interpolates the rotation at the same speed as the visual blend.
        if (_brain != null && _brain.IsBlending)
        {
            return _brain.transform.eulerAngles.y;
        }

        if (currentCamera != null)
        {
            return currentCamera.transform.eulerAngles.y;
        }

        return _brain != null ? _brain.transform.eulerAngles.y : 0f;
    }

    /// <summary>
    /// Requests a camera activation. If the brain is currently blending,
    /// the request is queued and will be applied once the blend finishes.
    /// Only the most recent queued request is kept (last-writer-wins).
    /// </summary>
    public void ActivateCamera(CinemachineCamera newCamera)
    {
        if (newCamera == currentCamera) return;

        // Queue during an active blend or while the clinch camera is locked
        if (_clinchLocked || (_brain != null && _brain.IsBlending))
        {
            _pendingCamera = newCamera;
            return;
        }

        ActivateCameraImmediate(newCamera);
    }

    /// <summary>
    /// Immediately swaps priorities so Cinemachine begins blending to <paramref name="newCamera"/>.
    /// </summary>
    private void ActivateCameraImmediate(CinemachineCamera newCamera)
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

        // Lock camera changes for the entire clinch (ease-in ? hold ? ease-out).
        // The lock is set on ease-in and only cleared when the ease-out tween finishes.
        if (easeIn)
            _clinchLocked = true;

        float targetFov = currentCamera.Lens.FieldOfView + (easeIn ? clinchFovOffset : -clinchFovOffset);
        float duration = easeIn ? clinchEaseInDuration : clinchEaseOutDuration;

        _clinchZoomTween = DOTween
            .To(() => currentCamera.Lens.FieldOfView, x => currentCamera.Lens.FieldOfView = x, targetFov, duration)
            .SetEase(Ease.InOutSine)
            .OnComplete(() =>
            {
                _clinchZoomTween = null;

                // Only release the lock when the ease-out completes
                if (!easeIn)
                    _clinchLocked = false;
            });
    }
}