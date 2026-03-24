#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Extracts root motion from an AnimationClip and creates curves representing
/// forward displacement and vertical (Y) position over time.
/// Curves use ABSOLUTE TIME (seconds) on the X-axis, not normalized time,
/// to match how Unity's Animator reports time.
///
/// For Humanoid clips the root transform position is computed at runtime from
/// muscle data, so <see cref="AnimationUtility.GetEditorCurve"/> returns null
/// for RootT.x/y/z.  In that case we spin up a temporary hidden GameObject
/// with an Animator, sample the clip via <see cref="AnimationMode"/>, and read
/// the resulting root position directly from the transform hierarchy.
/// </summary>
public static class RootMotionExtractor
{
    // ------------------------------------------------------------------
    // Public API
    // ------------------------------------------------------------------

    /// <summary>
    /// Extracts forward (XZ) motion from an animation clip and returns a curve
    /// with ABSOLUTE TIME (0 ? clip.length) on the X-axis and cumulative
    /// forward distance on the Y-axis.
    /// </summary>
    public static AnimationCurve ExtractForwardMotion(AnimationClip clip)
    {
        // Try explicit editor curves first (works for Generic rigs and .anim clips
        // that already contain baked RootT curves).
        AnimationCurve result = ExtractForwardMotionFromCurves(clip);
        if (result.length > 0)
            return result;

        // Fallback: sample via Animator (required for Humanoid clips).
        return ExtractForwardMotionViaSampling(clip);
    }

    /// <summary>
    /// Extracts the vertical (Y) position from an animation clip and returns a
    /// curve with ABSOLUTE TIME on the X-axis and Y offset (relative to frame 0)
    /// on the Y-axis.  Used by acrobatic moves to drive the collider up and down.
    /// </summary>
    public static AnimationCurve ExtractVerticalMotion(AnimationClip clip)
    {
        // Try explicit editor curves first.
        AnimationCurve result = ExtractVerticalMotionFromCurves(clip);
        if (result.length > 0)
            return result;

        // Fallback: sample via Animator (required for Humanoid clips).
        return ExtractVerticalMotionViaSampling(clip);
    }

    // ------------------------------------------------------------------
    // Curve-based extraction (Generic / baked clips)
    // ------------------------------------------------------------------

    private static AnimationCurve ExtractForwardMotionFromCurves(AnimationClip clip)
    {
        AnimationCurve resultCurve = new();

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

        float totalDistance = 0f;
        Vector2 lastPos = new(xSource?.Evaluate(0) ?? 0, zSource.Evaluate(0));
        float timeStep = 0.01f;
        float currentTime = 0f;

        while (currentTime <= clip.length)
        {
            float curX = xSource?.Evaluate(currentTime) ?? 0;
            float curZ = zSource.Evaluate(currentTime);
            Vector2 currentPos = new(curX, curZ);
            totalDistance += Vector2.Distance(currentPos, lastPos);
            resultCurve.AddKey(currentTime, totalDistance);
            lastPos = currentPos;
            currentTime += timeStep;
        }

        float finalX = xSource?.Evaluate(clip.length) ?? 0;
        float finalZ = zSource.Evaluate(clip.length);
        totalDistance += Vector2.Distance(new Vector2(finalX, finalZ), lastPos);
        resultCurve.AddKey(clip.length, totalDistance);

        return resultCurve;
    }

    private static AnimationCurve ExtractVerticalMotionFromCurves(AnimationClip clip)
    {
        AnimationCurve resultCurve = new();

        EditorCurveBinding yBinding = EditorCurveBinding.FloatCurve("", typeof(Animator), "RootT.y");
        AnimationCurve ySource = AnimationUtility.GetEditorCurve(clip, yBinding);

        if (ySource == null)
        {
            yBinding = EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalPosition.y");
            ySource = AnimationUtility.GetEditorCurve(clip, yBinding);
        }

        if (ySource == null) return resultCurve;

        float baseY = ySource.Evaluate(0);
        float timeStep = 0.01f;
        float currentTime = 0f;

        while (currentTime <= clip.length)
        {
            float yValue = ySource.Evaluate(currentTime) - baseY;
            resultCurve.AddKey(currentTime, yValue);
            currentTime += timeStep;
        }

        float finalY = ySource.Evaluate(clip.length) - baseY;
        resultCurve.AddKey(clip.length, finalY);

        return resultCurve;
    }

    // ------------------------------------------------------------------
    // Sampling-based extraction (Humanoid clips)
    // ------------------------------------------------------------------
    // For Humanoid rigs, root transform data is computed from muscle curves at
    // runtime so there are no explicit RootT curves in the clip asset.
    // We create a temporary hidden GameObject with an Animator, use
    // AnimationMode to sample the clip at many time steps, and read the
    // resulting root position from the Animator component.
    // ------------------------------------------------------------------

    private static AnimationCurve ExtractForwardMotionViaSampling(AnimationClip clip)
    {
        AnimationCurve resultCurve = new();

        // Locate an avatar from the clip's model importer so the Animator can
        // evaluate Humanoid muscle curves correctly.
        Avatar avatar = FindAvatarForClip(clip);

        GameObject tempGO = new("_RootMotionExtractor_Temp") { hideFlags = HideFlags.HideAndDontSave };
        Animator animator = tempGO.AddComponent<Animator>();
        animator.avatar = avatar;

        try
        {
            AnimationMode.StartAnimationMode();
            AnimationMode.BeginSampling();
            AnimationMode.SampleAnimationClip(tempGO, clip, 0f);
            AnimationMode.EndSampling();

            Vector3 startPos = tempGO.transform.position;
            Vector3 lastPos = startPos;

            float totalDistance = 0f;
            float timeStep = 0.01f;
            float currentTime = 0f;

            while (currentTime <= clip.length)
            {
                AnimationMode.BeginSampling();
                AnimationMode.SampleAnimationClip(tempGO, clip, currentTime);
                AnimationMode.EndSampling();

                Vector3 pos = tempGO.transform.position;
                Vector2 xz = new(pos.x, pos.z);
                Vector2 lastXZ = new(lastPos.x, lastPos.z);
                totalDistance += Vector2.Distance(xz, lastXZ);

                resultCurve.AddKey(currentTime, totalDistance);
                lastPos = pos;
                currentTime += timeStep;
            }

            // Final frame
            AnimationMode.BeginSampling();
            AnimationMode.SampleAnimationClip(tempGO, clip, clip.length);
            AnimationMode.EndSampling();

            Vector3 finalPos = tempGO.transform.position;
            totalDistance += Vector2.Distance(
                new Vector2(finalPos.x, finalPos.z),
                new Vector2(lastPos.x, lastPos.z));
            resultCurve.AddKey(clip.length, totalDistance);
        }
        finally
        {
            AnimationMode.StopAnimationMode();
            Object.DestroyImmediate(tempGO);
        }

        return resultCurve;
    }

    private static AnimationCurve ExtractVerticalMotionViaSampling(AnimationClip clip)
    {
        AnimationCurve resultCurve = new();

        Avatar avatar = FindAvatarForClip(clip);

        GameObject tempGO = new("_RootMotionExtractor_Temp") { hideFlags = HideFlags.HideAndDontSave };
        Animator animator = tempGO.AddComponent<Animator>();
        animator.avatar = avatar;

        try
        {
            AnimationMode.StartAnimationMode();

            // Sample frame 0 to get the base Y
            AnimationMode.BeginSampling();
            AnimationMode.SampleAnimationClip(tempGO, clip, 0f);
            AnimationMode.EndSampling();

            float baseY = tempGO.transform.position.y;

            float timeStep = 0.01f;
            float currentTime = 0f;

            while (currentTime <= clip.length)
            {
                AnimationMode.BeginSampling();
                AnimationMode.SampleAnimationClip(tempGO, clip, currentTime);
                AnimationMode.EndSampling();

                float yOffset = tempGO.transform.position.y - baseY;
                resultCurve.AddKey(currentTime, yOffset);

                currentTime += timeStep;
            }

            // Final frame
            AnimationMode.BeginSampling();
            AnimationMode.SampleAnimationClip(tempGO, clip, clip.length);
            AnimationMode.EndSampling();

            float finalYOffset = tempGO.transform.position.y - baseY;
            resultCurve.AddKey(clip.length, finalYOffset);
        }
        finally
        {
            AnimationMode.StopAnimationMode();
            Object.DestroyImmediate(tempGO);
        }

        return resultCurve;
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    /// <summary>
    /// Attempts to find a suitable <see cref="Avatar"/> for the given clip by
    /// inspecting its model importer. Returns null if one cannot be found
    /// (the Animator will fall back to generic sampling in that case).
    /// </summary>
    private static Avatar FindAvatarForClip(AnimationClip clip)
    {
        string clipPath = AssetDatabase.GetAssetPath(clip);
        if (string.IsNullOrEmpty(clipPath))
            return null;

        // The clip may live inside an FBX/model importer that has an avatar.
        ModelImporter importer = AssetImporter.GetAtPath(clipPath) as ModelImporter;
        if (importer != null)
        {
            // Load the avatar sub-asset from the same model.
            Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(clipPath);
            foreach (Object sub in subAssets)
            {
                if (sub is Avatar av)
                    return av;
            }
        }

        return null;
    }
}
#endif