using JSAM;
using UnityEngine;

// This Enum defines all possible "Last Ninja 2" weapon types and body parts.
// Rekkyō-gata (列挙型)
public enum HitboxType
{
    Fist,       // For punches (like your fast-punch-left)
    Foot,       // For kicks
    Katana,     // Standard sword
    Nunchaku,   // Chain weapon
    Staff,      // Long reach Bo Staff
    Shuriken,   // Projectiles
    Utility     // Smoke bombs or special items
}


[System.Serializable]
public struct AnimationAudioEvent
{
    [Range(0f, 1f)] public float triggerTime; // The frame 'slider'
    public JSAM.SoundFileObject sound;        // The JSAM object

    [System.NonSerialized] public bool hasPlayed;
}


[CreateAssetMenu(fileName = "NewMove", menuName = "Combat/Move")]
public class CombatMove : ScriptableObject
{
    [Header("Visuals")]
    [Tooltip("Display name for this combat move (auto-filled from animation clip name if empty).")]
    public string moveName;
    
    [Tooltip("Animation clip to play when this move is executed.")]
    public AnimationClip animationClip;

    [Header("Combat Stats")]
    [Tooltip("Amount of damage this move deals to the target.")]
    public float damage = 10f;
    
    [Tooltip("If true, this move is considered a heavy attack (may reset light combo chain).")]
    public bool isHeavy;

    [Header("Hitbox Logic")]
    [Tooltip("Select which physical hitbox this move should activate (e.g., Fist, Foot, Katana).")]
    public HitboxType hitboxType;

    [Header("SFX")]
    [Tooltip("Audio events that trigger at specific times during the animation (e.g., whoosh sounds, impact sounds). Use JSAM library to add randomized instances.")]
    public AnimationAudioEvent[] audioEvents;

    [Header("Hitbox Window (Normalized)")]
    [Tooltip("Normalized time (0-1) when the hitbox becomes active. 0 = animation start, 1 = animation end.")]
    [Range(0f, 1f)] public float hitStart = 0.25f;
    
    [Tooltip("Normalized time (0-1) when the hitbox deactivates. Must be >= hitStart.")]
    [Range(0f, 1f)] public float hitEnd = 0.45f;

    [Header("Combo Window (Normalized)")]
    [Tooltip("If false, this move cannot chain into another move (combo window disabled).")]
    public bool canCombo = true;
    
    [Tooltip("Normalized time (0-1) when the combo input window opens (player can input next move).")]
    [Range(0f, 1f)] public float comboStart = 0.55f;
    
    [Tooltip("Normalized time (0-1) when the combo input window closes. Set both to 1.0 to disable combos.")]
    [Range(0f, 1f)] public float comboEnd = 0.85f;

    // Cached values to avoid recalculating during runtime
    private float? _cachedAnimationDuration;
    
    /// <summary>
    /// Gets the animation clip duration (cached for performance).
    /// </summary>
    public float AnimationDuration
    {
        get
        {
            if (!_cachedAnimationDuration.HasValue && animationClip != null)
            {
                _cachedAnimationDuration = animationClip.length;
            }
            return _cachedAnimationDuration ?? 0f;
        }
    }

    /// <summary>
    /// Returns the absolute time (in seconds) when the hitbox should open.
    /// </summary>
    public float GetHitStartTime() => hitStart * AnimationDuration;

    /// <summary>
    /// Returns the absolute time (in seconds) when the hitbox should close.
    /// </summary>
    public float GetHitEndTime() => hitEnd * AnimationDuration;

    /// <summary>
    /// Returns the absolute time (in seconds) when combo window opens.
    /// </summary>
    public float GetComboStartTime() => comboStart * AnimationDuration;

    /// <summary>
    /// Returns the absolute time (in seconds) when combo window closes.
    /// </summary>
    public float GetComboEndTime() => comboEnd * AnimationDuration;

    /// <summary>
    /// Checks if the given normalized time is within the hitbox window.
    /// </summary>
    public bool IsInHitWindow(float normalizedTime) => normalizedTime >= hitStart && normalizedTime <= hitEnd;

    /// <summary>
    /// Checks if the given normalized time is within the combo window.
    /// </summary>
    public bool IsInComboWindow(float normalizedTime) => canCombo && normalizedTime >= comboStart && normalizedTime <= comboEnd;


#if UNITY_EDITOR
    private void OnValidate()
    {
        // Automatically set moveName based on the animation clip name
        if (animationClip != null && string.IsNullOrEmpty(moveName))
        {
            moveName = animationClip.name;
        }

        // Clear cached duration when clip changes
        _cachedAnimationDuration = null;

        // Safety Guard: Ensure End times are never before Start times
        hitStart = Mathf.Clamp01(hitStart);
        hitEnd = Mathf.Clamp(hitEnd, hitStart, 1f);

        comboStart = Mathf.Clamp01(comboStart);
        comboEnd = Mathf.Clamp(comboEnd, comboStart, 1f);

        // If canCombo is false, force combo window to end of animation
        if (!canCombo)
        {
            comboStart = 1f;
            comboEnd = 1f;
        }
        // If both are 0, disable combo window by pushing to end
        else if (comboStart == 0f && comboEnd == 0f)
        {
            comboStart = 1f;
            comboEnd = 1f;
        }
    }
#endif
}
