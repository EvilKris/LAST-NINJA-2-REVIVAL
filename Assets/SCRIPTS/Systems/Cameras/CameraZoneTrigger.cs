using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// Trigger zone that activates a Cinemachine camera and makes it follow the player.
/// Sets the camera's Follow target when the player enters and clears it when exiting.
/// </summary>
public class CameraZoneTrigger : MonoBehaviour
{
    [Tooltip("The Cinemachine camera to activate when player enters this zone.")]
    [SerializeField] private CinemachineCamera cinemachineCamera;

    /// <summary>
    /// Called when player enters the trigger zone.
    /// Sets the camera to follow the player and activates it.
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            // Set player as the camera's follow target
            //cinemachineCamera.Follow = other.transform;
            
            // Activate this camera through the zone manager
            CameraZoneManager.Instance.ActivateCamera(cinemachineCamera);
        }
    }

    /// <summary>
    /// Called when player exits the trigger zone.
    /// Removes the player as the camera's follow target.
    /// </summary>
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            // Clear the follow target
            //cinemachineCamera.Follow = null;
        }
    }
}