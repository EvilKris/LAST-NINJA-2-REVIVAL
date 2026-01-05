using UnityEngine;
using JSAM;

public enum HitboxType
{
    Fist,
    Foot,
    Katana,
    Nunchaku,
    Staff,
    Shuriken,
    Utility
}

[System.Serializable]
public struct AnimationAudioEvent
{
    [Range(0f, 1f)] public float triggerTime;
    public SoundFileObject sound;

    [System.NonSerialized] public bool hasPlayed;
}

[CreateAssetMenu(fileName = "NewMove", menuName = "Combat/Move")]
public class CombatMove : ScriptableObject
{
    // ─────────────────────────────────────────────
    // VISUALS
    // ─────────────────────────────────────────────

    [Header("Visuals")]
    public string moveName;
    public AnimationClip animationClip;

    // ─────────────────────────────────────────────
    // MOTION (ROOT-MOTION REPLACEMENT)
    // ─────────────────────────────────────────────

    //When using Animation Curves to replicate root motion, we can store the forward displacement over time here.
    //Apply Root Motion on the Animator component must be disabled for this to work properly!   
    [Header("Motion")]
    [Tooltip("Forward displacement curve extracted from animation root motion. Captures the root motion movement of the animation for storage")]
    public AnimationCurve motionCurve;

    [Tooltip("Scales how far this move travels without touching the curve shape.")]
    public float motionScale = 1f;

    // ─────────────────────────────────────────────
    // COMBAT STATS
    // ─────────────────────────────────────────────

    [Header("Combat Stats")]
    public float damage = 10f;
    public bool isHeavy;

    // ─────────────────────────────────────────────
    // HITBOX
    // ─────────────────────────────────────────────

    [Header("Hitbox Logic")]
    public HitboxType hitboxType;

    // ─────────────────────────────────────────────
    // AUDIO
    // ─────────────────────────────────────────────

    [Header("SFX")]
    public AnimationAudioEvent[] audioEvents;

    // ─────────────────────────────────────────────
    // HIT WINDOW
    // ─────────────────────────────────────────────

    [Header("Hitbox Window (Normalized)")]
    [Range(0f, 1f)] public float hitStart = 0.25f;
    [Range(0f, 1f)] public float hitEnd = 0.45f;

    // ─────────────────────────────────────────────
    // COMBO WINDOW
    // ─────────────────────────────────────────────

    [Header("Combo Window (Normalized)")]
    public bool canCombo = true;

    [Range(0f, 1f)] public float comboStart = 0.55f;
    [Range(0f, 1f)] public float comboEnd = 0.85f;

    // ─────────────────────────────────────────────
    // CACHED DATA
    // ─────────────────────────────────────────────

    private float? _cachedDuration;

    public float AnimationDuration
    {
        get
        {
            if (!_cachedDuration.HasValue && animationClip != null)
                _cachedDuration = animationClip.length;

            return _cachedDuration ?? 0f;
        }
    }

    // ─────────────────────────────────────────────
    // MOTION SAMPLING (RUNTIME)
    // ─────────────────────────────────────────────

    /// <summary>
    /// Returns forward displacement (delta) between two normalized times.
    /// </summary>
    public float EvaluateMotionDelta(float fromNormalized, float toNormalized)
    {
        if (motionCurve == null || motionCurve.length == 0)
            return 0f;

        float fromT = Mathf.Clamp01(fromNormalized) * AnimationDuration;
        float toT = Mathf.Clamp01(toNormalized) * AnimationDuration;

        float from = motionCurve.Evaluate(fromT);
        float to = motionCurve.Evaluate(toT);

        return (to - from) * motionScale;
    }

    // ─────────────────────────────────────────────
    // WINDOW HELPERS
    // ─────────────────────────────────────────────

    public bool IsInHitWindow(float normalizedTime)
        => normalizedTime >= hitStart && normalizedTime <= hitEnd;

    public bool IsInComboWindow(float normalizedTime)
        => canCombo && normalizedTime >= comboStart && normalizedTime <= comboEnd;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (animationClip != null && string.IsNullOrEmpty(moveName))
            moveName = animationClip.name;

        _cachedDuration = null;

        hitStart = Mathf.Clamp01(hitStart);
        hitEnd = Mathf.Clamp(hitEnd, hitStart, 1f);

        comboStart = Mathf.Clamp01(comboStart);
        comboEnd = Mathf.Clamp(comboEnd, comboStart, 1f);

        if (!canCombo)
        {
            comboStart = 1f;
            comboEnd = 1f;
        }
    }
#endif
}
