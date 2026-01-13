using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Ensures only one CinemachineBrain and one AudioListener are active across all scenes.
/// This component should be attached to the master camera in the MasterSingleton to prevent
/// conflicts when multiple scenes are loaded additively.
/// </summary>
public class MasterCameraAndAudioManager : MonoBehaviour
{
    /// <summary>
    /// Called automatically after scene load to ensure the master components are prioritized.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureMasterCheck()
    {
        // Ensure the MasterSingleton is loaded and trigger a check
        if (MasterSingleton.Main != null)
        {
            MasterCameraAndAudioManager manager = MasterSingleton.Main.GetComponentInChildren<MasterCameraAndAudioManager>();
            if (manager != null)
            {
                manager.DisableOtherComponents();
            }
        }
    }

    private void Awake()
    {
        // Subscribe to scene loaded event
        SceneManager.sceneLoaded += OnSceneLoaded;

        // Perform initial check for existing components
        if (Application.isPlaying)
        {
            DisableOtherComponents();
        }
    }

    private void OnDestroy()
    {
        // Unsubscribe to prevent memory leaks
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    /// <summary>
    /// Called whenever a new scene is loaded. Ensures all duplicate components are disabled.
    /// </summary>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Disable other components in play mode
        if (Application.isPlaying)
        {
            DisableOtherComponents();
        }
    }

    /// <summary>
    /// Disables all CinemachineBrain and AudioListener components except the master ones.
    /// </summary>
    public void DisableOtherComponents()
    {
        DisableOtherCinemachineBrains();
        DisableOtherAudioListeners();
    }

    /// <summary>
    /// Finds and disables all CinemachineBrain components except the one on this GameObject.
    /// Also disables the GameObjects containing those brains to prevent duplicate cameras.
    /// </summary>
    private void DisableOtherCinemachineBrains()
    {
        // Find all CinemachineBrain components in all loaded scenes
        CinemachineBrain[] brains = FindObjectsByType<CinemachineBrain>(FindObjectsSortMode.None);
        CinemachineBrain thisBrain = GetComponent<CinemachineBrain>();

        // Disable all other brains and their GameObjects except the master brain
        foreach (CinemachineBrain brain in brains)
        {
            // Skip the master brain itself
            if (brain == thisBrain) continue;
            
            // Disable the entire GameObject (disables both the brain and any camera components)
            if (brain.gameObject.activeSelf)
            {
                brain.gameObject.SetActive(false);
                Debug.Log($"[MasterCameraAndAudioManager] Disabled GameObject with CinemachineBrain: {brain.gameObject.name}");
            }
        }
    }

    /// <summary>
    /// Finds and disables all AudioListener components except the one on this GameObject.
    /// Unity requires exactly one active AudioListener to prevent audio warnings.
    /// </summary>
    private void DisableOtherAudioListeners()
    {
        // Find all AudioListener components in all loaded scenes
        AudioListener[] listeners = FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
        AudioListener thisListener = GetComponent<AudioListener>();

        // If no listener on this object, skip processing
        if (thisListener == null)
        {
            Debug.LogWarning("[MasterCameraAndAudioManager] No AudioListener found on master object.");
            return;
        }

        // Disable all other listeners except the master listener
        foreach (AudioListener listener in listeners)
        {
            // Skip the master listener itself
            if (listener == thisListener) continue;
            
            // Disable the listener component
            if (listener.enabled)
            {
                listener.enabled = false;
                Debug.Log($"[MasterCameraAndAudioManager] Disabled AudioListener on {listener.gameObject.name}");
            }
        }
    }
}
