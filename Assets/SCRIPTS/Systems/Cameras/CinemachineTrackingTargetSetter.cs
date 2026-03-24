using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Automatically assigns a tracking target to the CinemachineCamera on this GameObject.
/// Attach this alongside a CinemachineCamera and assign the target in the Inspector.
/// </summary>
[RequireComponent(typeof(CinemachineCamera))]
public class CinemachineTrackingTargetSetter : MonoBehaviour
{
    [Tooltip("The Transform (empty GameObject) used as the Cinemachine Follow and Look At target.")]
    [SerializeField] private Transform trackingTarget;

    private void Awake()
    {
        CinemachineCamera cam = GetComponent<CinemachineCamera>();

        // Guard: warn and bail early if no target was assigned in the Inspector
        if (trackingTarget == null)
        {
            Debug.LogWarning($"[CinemachineTrackingTargetSetter] No tracking target assigned on '{gameObject.name}'.", this);
            return;
        }

        // Point both Follow and LookAt at the same target so the camera
        // physically tracks and rotates towards the assigned Transform
        cam.Follow = trackingTarget;
        cam.LookAt = trackingTarget;
    }
}
