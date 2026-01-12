#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Extracts root motion from an AnimationClip and creates a curve representing
/// forward displacement over time. The curve uses ABSOLUTE TIME (seconds) as the X-axis,
/// not normalized time, to match how Unity's Animator reports time.
/// </summary>
public static class RootMotionExtractor
{
    /// <summary>
    /// Extracts forward motion from an animation clip and returns a curve with
    /// ABSOLUTE TIME (0 to clip.length) on the X-axis and cumulative distance on the Y-axis.
    /// </summary>
    public static AnimationCurve ExtractForwardMotion(AnimationClip clip)
    {
        AnimationCurve resultCurve = new();

        // 1. Bind to Root Transform X and Z
        EditorCurveBinding xBinding = EditorCurveBinding.FloatCurve("", typeof(Animator), "RootT.x");
        EditorCurveBinding zBinding = EditorCurveBinding.FloatCurve("", typeof(Animator), "RootT.z");

        AnimationCurve xSource = AnimationUtility.GetEditorCurve(clip, xBinding);
        AnimationCurve zSource = AnimationUtility.GetEditorCurve(clip, zBinding);

        // Fallback for Generic rigs
        if (zSource == null)
        {
            zBinding = EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalPosition.z");
            zSource = AnimationUtility.GetEditorCurve(clip, zBinding);
        }

        if (zSource == null) return resultCurve;

        // 2. High-Density Sampling
        float totalDistance = 0f;
        Vector2 lastPos = new(xSource?.Evaluate(0) ?? 0, zSource.Evaluate(0));

        // Use a fixed time step (e.g., 0.01s) to ensure high resolution regardless of length
        float timeStep = 0.01f;
        float currentTime = 0f;

        while (currentTime <= clip.length)
        {
            float curX = xSource?.Evaluate(currentTime) ?? 0;
            float curZ = zSource.Evaluate(currentTime);
            Vector2 currentPos = new(curX, curZ);

            // Calculate distance from previous sample point
            totalDistance += Vector2.Distance(currentPos, lastPos);

            // CRITICAL: Store with ABSOLUTE TIME (seconds), not normalized time
            // This matches how Unity's Animator.GetCurrentAnimatorStateInfo().normalizedTime works
            resultCurve.AddKey(currentTime, totalDistance);

            lastPos = currentPos;
            currentTime += timeStep;
        }

        // Ensure the very last frame is captured exactly at clip.length
        float finalX = xSource?.Evaluate(clip.length) ?? 0;
        float finalZ = zSource.Evaluate(clip.length);
        totalDistance += Vector2.Distance(new Vector2(finalX, finalZ), lastPos);
        resultCurve.AddKey(clip.length, totalDistance);

        return resultCurve;
    }
}
#endif