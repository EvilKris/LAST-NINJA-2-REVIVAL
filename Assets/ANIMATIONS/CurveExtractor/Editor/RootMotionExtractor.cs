#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class RootMotionExtractor
{
    public static AnimationCurve ExtractForwardMotion(AnimationClip clip)
    {
        AnimationCurve resultCurve = new();

        // 1. Get both X and Z curves to handle rotation offsets/diagonal movement
        EditorCurveBinding xBinding = EditorCurveBinding.FloatCurve("", typeof(Animator), "RootT.x");
        EditorCurveBinding zBinding = EditorCurveBinding.FloatCurve("", typeof(Animator), "RootT.z");

        AnimationCurve xSource = AnimationUtility.GetEditorCurve(clip, xBinding);
        AnimationCurve zSource = AnimationUtility.GetEditorCurve(clip, zBinding);

        // Fallback for non-humanoid or generic rigs without RootT
        if (zSource == null)
        {
            zBinding = EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalPosition.z");
            zSource = AnimationUtility.GetEditorCurve(clip, zBinding);
        }

        if (zSource == null) return resultCurve;

        // 2. Sample the curves to get absolute distance traveled (Magnitude)
        // This ignores the rotation offset (like your 15 degrees) and just measures "How far did he go?"
        float totalDistance = 0f;
        Vector2 lastPos = new(
            xSource?.Evaluate(0) ?? 0,
            zSource.Evaluate(0)
        );

        int sampleCount = Mathf.Max(30, (int)(clip.length * clip.frameRate));
        for (int i = 0; i <= sampleCount; i++)
        {
            float time = (i / (float)sampleCount) * clip.length;

            float curX = xSource?.Evaluate(time) ?? 0;
            float curZ = zSource.Evaluate(time);
            Vector2 currentPos = new(curX, curZ);

            // Accumulate the distance between frames
            totalDistance += Vector2.Distance(currentPos, lastPos);

            resultCurve.AddKey(time / clip.length, totalDistance); // Normalized time for the curve
            lastPos = currentPos;
        }

        return resultCurve;
    }
}
#endif