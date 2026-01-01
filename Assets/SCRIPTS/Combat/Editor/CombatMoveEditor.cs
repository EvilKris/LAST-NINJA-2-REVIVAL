using UnityEngine;
using UnityEditor;
using System.Reflection;

/// <summary>
/// Custom editor for CombatMove ScriptableObjects.
/// Displays a visual timeline showing hit windows and combo windows,
/// along with an animation preview and live frame tracking.
/// </summary>
[CustomEditor(typeof(CombatMove))]
public class CombatMoveEditor : Editor
{
    // Animation preview editor instance
    private Editor _animationPreviewEditor;
    
    // Reflection fields for reading current preview time
    private PropertyInfo _timeProperty;
    private FieldInfo _timeField;
    private object _timeTarget;

    // Cached GUIContent to avoid allocations on every repaint
    private static readonly GUIContent LivePreviewLabel = new("LIVE PREVIEW FRAME: ");
    private static readonly GUIContent HitboxLabel = new("Active Hitbox: ");
    private static readonly GUIContent ComboWindowLabel = new("Combo Window: ");
    private static readonly GUIContent NoComboLabel = new("Combo Window: No Combo!");

    // Cached colors to avoid allocations
    private static readonly Color TimelineBackgroundColor = new(0.15f, 0.15f, 0.15f);
    private static readonly Color HitWindowColor = new(1f, 0.3f, 0.3f, 0.7f);
    private static readonly Color ComboWindowColor = new(0.3f, 1f, 0.3f, 0.7f);

    public override void OnInspectorGUI()
    {
        // Draw standard inspector fields
        DrawDefaultInspector();

        CombatMove move = (CombatMove)target;
        if (move.animationClip == null) return;

        // Setup and draw custom UI
        SetupPreviewEditor(move);
        DrawTimeline(move);
        DrawPreview();
        
        // Fix Unity's animation preview timeline to show full clip duration
        // Note: This triggers a repaint internally, so no need for explicit Repaint() call
        FixPreviewEditorForAnimation(_animationPreviewEditor);
    }

    // Cached reflection fields for fixing preview timeline (static to share across all instances)
    private static FieldInfo _cachedAvatarPreviewFieldInfo;
    private static FieldInfo _cachedTimeControlFieldInfo;
    private static FieldInfo _cachedStopTimeFieldInfo;

    /// <summary>
    /// Uses reflection to fix Unity's animation preview timeline to show the full clip duration.
    /// By default, Unity may limit the preview to 60 frames.
    /// This method is called every frame but uses cached reflection info for performance.
    /// </summary>
    private static void FixPreviewEditorForAnimation(Editor editor)
    {
        // Ensure the editor target is an AnimationClip
        if (editor.target is not AnimationClip clip) return;
        
        // Fast path: if reflection fields are already cached, use them directly
        if (_cachedAvatarPreviewFieldInfo != null && _cachedTimeControlFieldInfo != null && _cachedStopTimeFieldInfo != null)
        {
            var value = _cachedAvatarPreviewFieldInfo.GetValue(editor);
            var subValue = _cachedTimeControlFieldInfo.GetValue(value);
            _cachedStopTimeFieldInfo.SetValue(subValue, clip.length);
        }
        else
        {
            // Slow path: cache reflection fields on first use
            _cachedAvatarPreviewFieldInfo ??= editor.GetType().GetField("m_AvatarPreview", BindingFlags.NonPublic | BindingFlags.Instance);
            if (_cachedAvatarPreviewFieldInfo == null) return;
            
            var value = _cachedAvatarPreviewFieldInfo.GetValue(editor);
            if (value == null) return;
            
            _cachedTimeControlFieldInfo ??= value.GetType().GetField("timeControl", BindingFlags.Public | BindingFlags.Instance);
            if (_cachedTimeControlFieldInfo == null) return;
            
            var subValue = _cachedTimeControlFieldInfo.GetValue(value);
            if (subValue == null) return;
            
            _cachedStopTimeFieldInfo ??= subValue.GetType().GetField("stopTime", BindingFlags.Public | BindingFlags.Instance);
            if (_cachedStopTimeFieldInfo == null) return;
            
            // Set the stop time to match clip length (fixes 60-frame limitation)
            _cachedStopTimeFieldInfo.SetValue(subValue, clip.length);
        }
    }

    /// <summary>
    /// Creates and configures the animation preview editor if needed.
    /// Only recreates the editor when the target animation clip changes.
    /// </summary>
    private void SetupPreviewEditor(CombatMove move)
    {
        // Only recreate if editor doesn't exist or animation clip changed
        if (_animationPreviewEditor == null || _animationPreviewEditor.target != move.animationClip)
        {
            if (_animationPreviewEditor != null) DestroyImmediate(_animationPreviewEditor);

            _animationPreviewEditor = CreateEditor(move.animationClip);

            // UNITY 6 SPECIFIC: Fetch and Reset the AvatarPreview
            // This forces the preview to recalculate its time bounds
            var avatarPreviewField = _animationPreviewEditor.GetType().GetField("m_AvatarPreview",
                BindingFlags.Instance | BindingFlags.NonPublic);

            if (avatarPreviewField != null)
            {
                object avatarPreview = avatarPreviewField.GetValue(_animationPreviewEditor);
                if (avatarPreview != null)
                {
                    // Force the preview to re-calculate its time bounds
                    var resetMethod = avatarPreview.GetType().GetMethod("Reset",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    resetMethod?.Invoke(avatarPreview, null);
                }
            }

            CacheTimeReflection();
        }
    }

    /// <summary>
    /// Draws the timeline section showing animation info, visual timeline bar, and frame data.
    /// </summary>
    private void DrawTimeline(CombatMove move)
    {
        AnimationClip clip = move.animationClip;
        int totalFrames = Mathf.RoundToInt(clip.length * clip.frameRate);

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField($"Animation Timeline Reference. Total Frames: {totalFrames}", EditorStyles.boldLabel);

        DrawTimelineBar(move);
        DrawFrameData(move, totalFrames);
    }

    /// <summary>
    /// Draws the visual timeline bar showing hit and combo windows.
    /// Red bar = Hit Window, Green bar = Combo Window.
    /// </summary>
    private void DrawTimelineBar(CombatMove move)
    {
        Rect rect = GUILayoutUtility.GetRect(10, 30, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(rect, TimelineBackgroundColor);

        // Cache rect dimensions to avoid repeated property access
        float rectWidth = rect.width;
        float rectX = rect.x;
        float rectY = rect.y;
        float rectHeight = rect.height;

        // Calculate pixel positions for windows based on normalized times
        float hitStartPx = rectX + (rectWidth * move.hitStart);
        float hitEndPx = rectX + (rectWidth * move.hitEnd);
        float comboStartPx = rectX + (rectWidth * move.comboStart);
        float comboEndPx = rectX + (rectWidth * move.comboEnd);

        // Draw Hit Window (Red)
        Rect hitRect = new Rect(
            hitStartPx,
            rectY,
            Mathf.Max(2, hitEndPx - hitStartPx), // Minimum 2px width for visibility
            rectHeight
        );
        EditorGUI.DrawRect(hitRect, HitWindowColor);
        EditorGUI.LabelField(hitRect, " HIT", EditorStyles.whiteMiniLabel);

        // Draw Combo Window (Green)
        Rect comboRect = new Rect(
            comboStartPx,
            rectY,
            Mathf.Max(2, comboEndPx - comboStartPx), // Minimum 2px width for visibility
            rectHeight
        );
        EditorGUI.DrawRect(comboRect, ComboWindowColor);
        EditorGUI.LabelField(comboRect, " COMBO", EditorStyles.whiteMiniLabel);
    }

    /// <summary>
    /// Draws the frame data box showing live frame tracking and window frame numbers.
    /// </summary>
    private void DrawFrameData(CombatMove move, int totalFrames)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        DrawLiveFrameTracking(move);
        DrawHitboxFrames(move, totalFrames);
        DrawComboFrames(move, totalFrames);

        EditorGUILayout.EndVertical();
    }

    /// <summary>
    /// Displays the current frame being previewed in the animation.
    /// Updates in real-time as the preview scrubber is moved.
    /// </summary>
    private void DrawLiveFrameTracking(CombatMove move)
    {
        float currentTime = GetCurrentPreviewTime();
        if (currentTime < 0) return;

        int currentFrame = Mathf.RoundToInt(currentTime * move.animationClip.frameRate);
        
        // Highlight current frame with cyan background
        Color originalColor = GUI.backgroundColor;
        GUI.backgroundColor = Color.cyan;
        
        try
        {
            EditorGUILayout.BeginVertical(EditorStyles.textArea);
            EditorGUILayout.LabelField($"{LivePreviewLabel.text}{currentFrame}", EditorStyles.boldLabel);
            EditorGUILayout.EndVertical();
        }
        finally
        {
            // Ensure color is always restored, even if an exception occurs
            GUI.backgroundColor = originalColor;
        }
    }

    /// <summary>
    /// Displays the frame range for the hitbox activation window.
    /// </summary>
    private void DrawHitboxFrames(CombatMove move, int totalFrames)
    {
        int hitStart = Mathf.RoundToInt(totalFrames * move.hitStart);
        int hitEnd = Mathf.RoundToInt(totalFrames * move.hitEnd);
        EditorGUILayout.LabelField($"{HitboxLabel.text}Frame {hitStart} to {hitEnd}", EditorStyles.miniBoldLabel);
    }

    /// <summary>
    /// Displays the frame range for the combo input window.
    /// Shows "No Combo!" if combo is disabled (both start and end are at 1.0).
    /// </summary>
    private void DrawComboFrames(CombatMove move, int totalFrames)
    {
        bool noCombo = move.comboStart >= 1f && move.comboEnd >= 1f;
        
        if (noCombo)
        {
            // Gray out text for disabled combo
            Color originalColor = GUI.color;
            GUI.color = Color.gray;
            
            try
            {
                EditorGUILayout.LabelField(NoComboLabel, EditorStyles.miniBoldLabel);
            }
            finally
            {
                GUI.color = originalColor;
            }
        }
        else
        {
            int comboStart = Mathf.RoundToInt(totalFrames * move.comboStart);
            int comboEnd = Mathf.RoundToInt(totalFrames * move.comboEnd);
            EditorGUILayout.LabelField($"{ComboWindowLabel.text}Frame {comboStart} to {comboEnd}", EditorStyles.miniBoldLabel);
        }
    }

    /// <summary>
    /// Draws Unity's built-in animation preview window with interactive scrubber.
    /// </summary>
    private void DrawPreview()
    {
        if (_animationPreviewEditor != null && _animationPreviewEditor.HasPreviewGUI())
        {
            Rect previewRect = GUILayoutUtility.GetRect(200, 250, GUILayout.ExpandWidth(true));
            _animationPreviewEditor.OnInteractivePreviewGUI(previewRect, EditorStyles.textArea);
        }
    }

    /// <summary>
    /// Gets the current time position of the animation preview using reflection.
    /// Returns -1 if unable to read the time.
    /// </summary>
    private float GetCurrentPreviewTime()
    {
        if (_timeTarget == null) return -1f;

        // Try field access first (faster than property)
        if (_timeField != null)
        {
            try
            {
                return (float)_timeField.GetValue(_timeTarget);
            }
            catch
            {
                // Reflection failed, try recaching once
                CacheTimeReflection();
            }
        }

        // Fallback to property access
        if (_timeProperty != null)
        {
            try
            {
                return (float)_timeProperty.GetValue(_timeTarget);
            }
            catch
            {
                // Reflection failed completely
                return -1f;
            }
        }

        return -1f;
    }

    /// <summary>
    /// Caches reflection info for accessing the animation preview's current time.
    /// Tries to access m_State.m_Time first (most reliable), falls back to direct property/field access.
    /// This is called once when the preview editor is created to improve performance.
    /// </summary>
    private void CacheTimeReflection()
    {
        if (_animationPreviewEditor == null) return;

        System.Type editorType = _animationPreviewEditor.GetType();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        // Try to get m_State field first (most reliable for time tracking)
        FieldInfo stateField = editorType.GetField("m_State", flags);
        if (stateField != null)
        {
            _timeTarget = stateField.GetValue(_animationPreviewEditor);
            if (_timeTarget != null)
            {
                _timeField = _timeTarget.GetType().GetField("m_Time", flags);
                if (_timeField != null) return;
            }
        }

        // Fallback to direct property/field access on the editor itself
        _timeTarget = _animationPreviewEditor;
        _timeProperty = editorType.GetProperty("time", flags) ?? editorType.GetProperty("currentTime", flags);
        _timeField = editorType.GetField("m_Time", flags) ?? editorType.GetField("m_PreviewTime", flags);
    }

    /// <summary>
    /// Cleanup when editor is disabled or destroyed.
    /// Destroys the animation preview editor to prevent memory leaks.
    /// </summary>
    private void OnDisable()
    {
        if (_animationPreviewEditor != null)
        {
            DestroyImmediate(_animationPreviewEditor);
            _animationPreviewEditor = null;
        }
    }
}

