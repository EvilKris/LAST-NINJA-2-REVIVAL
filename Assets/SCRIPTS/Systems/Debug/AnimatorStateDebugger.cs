using UnityEngine;

/// <summary>
/// Debug utility to track and log animator state changes in real-time.
/// Attach this to any GameObject with an Animator to monitor its state transitions.
/// </summary>
public class AnimatorStateDebugger : MonoBehaviour
{
    [Header("Debug Settings")]
    [Tooltip("Enable to pause the game when animator state changes")]
    public bool breakOnStateChange = false;
    
    [Tooltip("Enable to log state changes to console")]
    public bool logStateChanges = true;
    
    [Tooltip("Which animator layer to monitor (0 = base layer)")]
    public int layerToMonitor = 0;

    private Animator _animator;
    private int _currentStateHash;
    private string _currentStateName;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        if (_animator == null)
        {
            Debug.LogError($"[AnimatorStateDebugger] No Animator found on {gameObject.name}!");
            enabled = false;
        }
    }

    private void Update()
    {
        if (_animator == null) return;

        // Get current animator state info
        AnimatorStateInfo stateInfo = _animator.GetCurrentAnimatorStateInfo(layerToMonitor);
        
        // Check if state has changed
        if (stateInfo.fullPathHash != _currentStateHash)
        {
            // State changed - get the new state name
            string newStateName = GetStateName(stateInfo);
            
            if (logStateChanges)
            {
                Debug.Log($"[{gameObject.name}] Animation State Changed: <color=yellow>{_currentStateName}</color> ? <color=cyan>{newStateName}</color> (Hash: {stateInfo.fullPathHash})");
            }

            if (breakOnStateChange)
            {
                Debug.Log($"<color=red>[BREAK]</color> Animation state changed to: {newStateName}");
                Debug.Break();
            }

            // Update cached values
            _currentStateHash = stateInfo.fullPathHash;
            _currentStateName = newStateName;
        }
    }

    /// <summary>
    /// Attempts to get a readable state name from the AnimatorStateInfo.
    /// Falls back to hash if name cannot be retrieved.
    /// </summary>
    private string GetStateName(AnimatorStateInfo stateInfo)
    {
        // Try to get the clip name from the animator
        AnimatorClipInfo[] clipInfo = _animator.GetCurrentAnimatorClipInfo(layerToMonitor);
        
        if (clipInfo.Length > 0 && clipInfo[0].clip != null)
        {
            return clipInfo[0].clip.name;
        }
        
        // Fallback to hash
        return $"State_{stateInfo.fullPathHash}";
    }

    /// <summary>
    /// Logs all currently playing animation clips on all layers.
    /// Call this from console or another script for debugging.
    /// </summary>
    [ContextMenu("Log All Current Clips")]
    public void LogAllCurrentClips()
    {
        if (_animator == null) return;

        Debug.Log($"=== Current Animation Clips on {gameObject.name} ===");
        
        for (int layer = 0; layer < _animator.layerCount; layer++)
        {
            AnimatorClipInfo[] clips = _animator.GetCurrentAnimatorClipInfo(layer);
            AnimatorStateInfo stateInfo = _animator.GetCurrentAnimatorStateInfo(layer);
            
            Debug.Log($"Layer {layer}: State Hash = {stateInfo.fullPathHash}, Normalized Time = {stateInfo.normalizedTime:F2}");
            
            foreach (var clipInfo in clips)
            {
                Debug.Log($"  - Clip: {clipInfo.clip.name} (Weight: {clipInfo.weight:F2})");
            }
        }
    }
}
