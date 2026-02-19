using UnityEngine;
using UnityEditor;
using System.Reflection;

/// <summary>
/// Custom editor for CombatThrow ScriptableObjects.
/// Displays a visual timeline showing throw launch activation point,
/// along with animation previews for both attacker and victim clips.
/// </summary>
[CustomEditor(typeof(CombatThrow))]
public class CombatThrowEditor : Editor
{
    // Animation preview editor instances (transient - Unity may destroy these)
    private Editor _attackerPreviewEditor;
    private Editor _victimPreviewEditor;
    
    // SerializeField allows these to survive assembly reload and focus changes
    [SerializeField] private int _lastAttackerClipInstanceID = -1;
    [SerializeField] private int _lastVictimClipInstanceID = -1;
    [SerializeField] private int _lastTargetInstanceID = -1;
    
    // Reflection fields for reading current preview time (transient)
    private PropertyInfo _attackerTimeProperty;
    private FieldInfo _attackerTimeField;
    private object _attackerTimeTarget;

    private PropertyInfo _victimTimeProperty;
    private FieldInfo _victimTimeField;
    private object _victimTimeTarget;
    
    // Flag to track if we need to force recreation
    private bool _forceRecreation = false;

    // Cached GUIContent to avoid allocations on every repaint
    private static readonly GUIContent AttackerLivePreviewLabel = new("ATTACKER LIVE PREVIEW FRAME: ");
    private static readonly GUIContent VictimLivePreviewLabel = new("VICTIM LIVE PREVIEW FRAME: ");
    private static readonly GUIContent ThrowLaunchLabel = new("Throw Launch: ");

    // Cached colors to avoid allocations
    private static readonly Color TimelineBackgroundColor = new(0.15f, 0.15f, 0.15f);
    private static readonly Color ThrowLaunchColor = new(1f, 0.8f, 0.2f, 0.7f);

    private void OnEnable()
    {
        // Set flag to force recreation of previews
        // Don't destroy editors here as Unity may call OnEnable multiple times
        _forceRecreation = true;
        
        // Subscribe to editor update to ensure we repaint when needed
        EditorApplication.update -= OnEditorUpdate;
        EditorApplication.update += OnEditorUpdate;
    }
    
    private void OnEditorUpdate()
    {
        // Only repaint if we have active preview editors
        if (_attackerPreviewEditor != null || _victimPreviewEditor != null)
        {
            Repaint();
        }
    }

    public override void OnInspectorGUI()
    {
        // Use EditorGUI.BeginChangeCheck to detect when properties change
        EditorGUI.BeginChangeCheck();
        DrawDefaultInspector();
        bool propertiesChanged = EditorGUI.EndChangeCheck();

        CombatThrow throwMove = (CombatThrow)target;
        
        // Get current instance IDs
        int currentTargetID = throwMove.GetInstanceID();
        int currentAttackerClipID = throwMove.attackerThrowClip != null ? throwMove.attackerThrowClip.GetInstanceID() : -1;
        int currentVictimClipID = throwMove.victimThrowClip != null ? throwMove.victimThrowClip.GetInstanceID() : -1;
        
        // Detect changes that require recreation
        bool targetChanged = _lastTargetInstanceID != currentTargetID;
        bool attackerClipChanged = _lastAttackerClipInstanceID != currentAttackerClipID;
        bool victimClipChanged = _lastVictimClipInstanceID != currentVictimClipID;
        
        // Update tracked IDs
        if (targetChanged || attackerClipChanged || victimClipChanged || _forceRecreation)
        {
            _lastTargetInstanceID = currentTargetID;
            _lastAttackerClipInstanceID = currentAttackerClipID;
            _lastVictimClipInstanceID = currentVictimClipID;
            _forceRecreation = false;
            
            // Clean up old editors when something changed
            if (attackerClipChanged && _attackerPreviewEditor != null)
            {
                try { DestroyImmediate(_attackerPreviewEditor); }
                catch { /* Ignore */ }
                _attackerPreviewEditor = null;
            }
            
            if (victimClipChanged && _victimPreviewEditor != null)
            {
                try { DestroyImmediate(_victimPreviewEditor); }
                catch { /* Ignore */ }
                _victimPreviewEditor = null;
            }
        }

        // Early exit if no clips to preview
        if (throwMove.attackerThrowClip == null && throwMove.victimThrowClip == null)
            return;

        // Setup and draw attacker preview
        if (throwMove.attackerThrowClip != null)
        {
            SetupAttackerPreviewEditor(throwMove);
            if (_attackerPreviewEditor != null)
            {
                DrawAttackerTimeline(throwMove);
                FixPreviewEditorForAnimation(_attackerPreviewEditor);
            }
        }
        else if (_attackerPreviewEditor != null)
        {
            try { DestroyImmediate(_attackerPreviewEditor); }
            catch { /* Ignore */ }
            _attackerPreviewEditor = null;
        }

        // Setup and draw victim preview
        if (throwMove.victimThrowClip != null)
        {
            SetupVictimPreviewEditor(throwMove);
            if (_victimPreviewEditor != null)
            {
                DrawVictimTimeline(throwMove);
                FixPreviewEditorForAnimation(_victimPreviewEditor);
            }
        }
        else if (_victimPreviewEditor != null)
        {
            try { DestroyImmediate(_victimPreviewEditor); }
            catch { /* Ignore */ }
            _victimPreviewEditor = null;
        }

        // Draw the appropriate preview window(s)
        if (throwMove.attackerThrowClip != null && throwMove.victimThrowClip != null)
        {
            DrawSplitScreenPreview();
        }
        else if (throwMove.attackerThrowClip != null)
        {
            DrawAttackerPreview();
        }
        else if (throwMove.victimThrowClip != null)
        {
            DrawVictimPreview();
        }
    }

    // ?????????????????????????????????????????????
    // PREVIEW FIX (unchanged)
    // ?????????????????????????????????????????????

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

    // ?????????????????????????????????????????????
    // ATTACKER PREVIEW
    // ?????????????????????????????????????????????

    /// <summary>
    /// Creates and configures the attacker animation preview editor if needed.
    /// Only recreates the editor when the target animation clip changes.
    /// </summary>
    private void SetupAttackerPreviewEditor(CombatThrow throwMove)
    {
        if (throwMove.attackerThrowClip == null)
        {
            if (_attackerPreviewEditor != null)
            {
                DestroyImmediate(_attackerPreviewEditor);
                _attackerPreviewEditor = null;
            }
            return;
        }

        // Check if editor exists and is valid
        bool editorExists = _attackerPreviewEditor != null;
        bool targetValid = editorExists && _attackerPreviewEditor.target != null;
        bool hasPreviewGUI = targetValid && _attackerPreviewEditor.HasPreviewGUI();
        
        // Recreate if editor doesn't exist, target is null, clip changed, or preview GUI isn't available
        bool needsRecreation = !editorExists || 
                               !targetValid || 
                               !hasPreviewGUI ||
                               _attackerPreviewEditor.target != throwMove.attackerThrowClip;
        
        if (needsRecreation)
        {
            if (_attackerPreviewEditor != null)
            {
                try { DestroyImmediate(_attackerPreviewEditor); }
                catch { /* Ignore errors if already destroyed */ }
            }
            
            // Clear cached reflection info since we're creating a new editor
            _attackerTimeProperty = null;
            _attackerTimeField = null;
            _attackerTimeTarget = null;

            _attackerPreviewEditor = CreateEditor(throwMove.attackerThrowClip);
            
            if (_attackerPreviewEditor == null)
                return;

            // UNITY 6 SPECIFIC: Fetch and Reset the AvatarPreview
            // This forces the preview to recalculate its time bounds
            var avatarPreviewField = _attackerPreviewEditor.GetType().GetField("m_AvatarPreview",
                BindingFlags.Instance | BindingFlags.NonPublic);

            if (avatarPreviewField != null)
            {
                object avatarPreview = avatarPreviewField.GetValue(_attackerPreviewEditor);
                if (avatarPreview != null)
                {
                    // Force the preview to re-calculate its time bounds
                    var resetMethod = avatarPreview.GetType().GetMethod("Reset",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    resetMethod?.Invoke(avatarPreview, null);
                }
            }

            CacheAttackerTimeReflection();
        }
    }

    /// <summary>
    /// Draws the timeline section showing animation info, visual timeline bar, and frame data for the attacker.
    /// </summary>
    private void DrawAttackerTimeline(CombatThrow throwMove)
    {
        AnimationClip clip = throwMove.attackerThrowClip;
        int totalFrames = Mathf.RoundToInt(clip.length * clip.frameRate);

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField($"Attacker Animation Timeline. Total Frames: {totalFrames}", EditorStyles.boldLabel);

        DrawAttackerTimelineBar(throwMove);
        DrawAttackerFrameData(throwMove, totalFrames);
    }

    /// <summary>
    /// Draws the visual timeline bar showing throw launch activation point for the attacker.
    /// Yellow/Orange bar = Throw Launch Activation.
    /// </summary>
    private void DrawAttackerTimelineBar(CombatThrow throwMove)
    {
        Rect rect = GUILayoutUtility.GetRect(10, 30, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(rect, TimelineBackgroundColor);

        // Cache rect dimensions to avoid repeated property access
        float rectWidth = rect.width;
        float rectX = rect.x;
        float rectY = rect.y;
        float rectHeight = rect.height;

        // Calculate frame numbers for labels
        AnimationClip clip = throwMove.attackerThrowClip;
        int totalFrames = Mathf.RoundToInt(clip.length * clip.frameRate);
        int launchFrame = Mathf.RoundToInt(totalFrames * throwMove.throwLaunchActivation);

        // Calculate pixel position for launch activation marker
        float launchPx = rectX + (rectWidth * throwMove.throwLaunchActivation);

        // Draw a thin vertical line at the launch activation point
        Rect launchRect = new Rect(
            launchPx - 1, // Center the line on the activation point
            rectY,
            2, // 2px width for the vertical line
            rectHeight
        );
        EditorGUI.DrawRect(launchRect, ThrowLaunchColor);
        
        // Draw Launch label
        string launchLabel = $"LAUNCH {launchFrame}";
        GUIContent launchContent = new GUIContent(launchLabel);
        Vector2 launchLabelSize = EditorStyles.whiteMiniLabel.CalcSize(launchContent);
        
        // Position label to the right of the line if there's space, otherwise to the left
        float labelX = launchPx + 4;
        if (labelX + launchLabelSize.x > rectX + rectWidth)
        {
            labelX = launchPx - launchLabelSize.x - 4;
        }
        
        Rect launchLabelRect = new Rect(
            labelX,
            rectY + (rectHeight - launchLabelSize.y) * 0.5f, // Vertically center
            launchLabelSize.x,
            launchLabelSize.y
        );
        
        // Draw semi-transparent background behind text for readability
        EditorGUI.DrawRect(new Rect(launchLabelRect.x - 1, launchLabelRect.y, launchLabelSize.x + 2, launchLabelSize.y), 
            new Color(0, 0, 0, 0.5f));
        EditorGUI.LabelField(launchLabelRect, launchContent, EditorStyles.whiteMiniLabel);
    }

    /// <summary>
    /// Draws the frame data box showing live frame tracking for the attacker.
    /// </summary>
    private void DrawAttackerFrameData(CombatThrow throwMove, int totalFrames)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        DrawAttackerLiveFrameTracking(throwMove);
        DrawThrowLaunchFrame(throwMove, totalFrames);

        EditorGUILayout.EndVertical();
    }

    /// <summary>
    /// Displays the current frame being previewed in the attacker animation.
    /// Updates in real-time as the preview scrubber is moved.
    /// </summary>
    private void DrawAttackerLiveFrameTracking(CombatThrow throwMove)
    {
        float currentTime = GetAttackerPreviewTime();
        if (currentTime < 0) return;

        int currentFrame = Mathf.RoundToInt(currentTime * throwMove.attackerThrowClip.frameRate);
        
        // Highlight current frame with cyan background
        Color originalColor = GUI.backgroundColor;
        GUI.backgroundColor = Color.cyan;
        
        try
        {
            EditorGUILayout.BeginVertical(EditorStyles.textArea);
            EditorGUILayout.LabelField($"{AttackerLivePreviewLabel.text}{currentFrame}", EditorStyles.boldLabel);
            EditorGUILayout.EndVertical();
        }
        finally
        {
            // Ensure color is always restored, even if an exception occurs
            GUI.backgroundColor = originalColor;
        }
    }

    /// <summary>
    /// Displays the frame for throw launch activation.
    /// </summary>
    private void DrawThrowLaunchFrame(CombatThrow throwMove, int totalFrames)
    {
        int launchFrame = Mathf.RoundToInt(totalFrames * throwMove.throwLaunchActivation);
        EditorGUILayout.LabelField($"{ThrowLaunchLabel.text}Frame {launchFrame}", EditorStyles.miniBoldLabel);
    }

    /// <summary>
    /// Draws Unity's built-in animation preview window with interactive scrubber for the attacker.
    /// </summary>
    private void DrawAttackerPreview()
    {
        if (_attackerPreviewEditor != null && _attackerPreviewEditor.HasPreviewGUI())
        {
            Rect previewRect = GUILayoutUtility.GetRect(200, 250, GUILayout.ExpandWidth(true));
            _attackerPreviewEditor.OnInteractivePreviewGUI(previewRect, EditorStyles.textArea);
        }
    }

    /// <summary>
    /// Gets the current time position of the attacker animation preview using reflection.
    /// Returns -1 if unable to read the time.
    /// </summary>
    private float GetAttackerPreviewTime()
    {
        if (_attackerTimeTarget == null) return -1f;

        // Try field access first (faster than property)
        if (_attackerTimeField != null)
        {
            try
            {
                return (float)_attackerTimeField.GetValue(_attackerTimeTarget);
            }
            catch
            {
                // Reflection failed, try recaching once
                CacheAttackerTimeReflection();
            }
        }

        // Fallback to property access
        if (_attackerTimeProperty != null)
        {
            try
            {
                return (float)_attackerTimeProperty.GetValue(_attackerTimeTarget);
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
    /// Caches reflection info for accessing the attacker animation preview's current time.
    /// </summary>
    private void CacheAttackerTimeReflection()
    {
        if (_attackerPreviewEditor == null) return;

        System.Type editorType = _attackerPreviewEditor.GetType();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        // Try to get m_State field first (most reliable for time tracking)
        FieldInfo stateField = editorType.GetField("m_State", flags);
        if (stateField != null)
        {
            _attackerTimeTarget = stateField.GetValue(_attackerPreviewEditor);
            if (_attackerTimeTarget != null)
            {
                _attackerTimeField = _attackerTimeTarget.GetType().GetField("m_Time", flags);
                if (_attackerTimeField != null) return;
            }
        }

        // Fallback to direct property/field access on the editor itself
        _attackerTimeTarget = _attackerPreviewEditor;
        _attackerTimeProperty = editorType.GetProperty("time", flags) ?? editorType.GetProperty("currentTime", flags);
        _attackerTimeField = editorType.GetField("m_Time", flags) ?? editorType.GetField("m_PreviewTime", flags);
    }

    // ?????????????????????????????????????????????
    // VICTIM PREVIEW
    // ?????????????????????????????????????????????

    /// <summary>
    /// Creates and configures the victim animation preview editor if needed.
    /// Only recreates the editor when the target animation clip changes.
    /// </summary>
    private void SetupVictimPreviewEditor(CombatThrow throwMove)
    {
        if (throwMove.victimThrowClip == null)
        {
            if (_victimPreviewEditor != null)
            {
                DestroyImmediate(_victimPreviewEditor);
                _victimPreviewEditor = null;
            }
            return;
        }

        // Check if editor exists and is valid
        bool editorExists = _victimPreviewEditor != null;
        bool targetValid = editorExists && _victimPreviewEditor.target != null;
        bool hasPreviewGUI = targetValid && _victimPreviewEditor.HasPreviewGUI();
        
        // Recreate if editor doesn't exist, target is null, clip changed, or preview GUI isn't available
        bool needsRecreation = !editorExists || 
                               !targetValid || 
                               !hasPreviewGUI ||
                               _victimPreviewEditor.target != throwMove.victimThrowClip;
        
        if (needsRecreation)
        {
            if (_victimPreviewEditor != null)
            {
                try { DestroyImmediate(_victimPreviewEditor); }
                catch { /* Ignore errors if already destroyed */ }
            }
            
            // Clear cached reflection info since we're creating a new editor
            _victimTimeProperty = null;
            _victimTimeField = null;
            _victimTimeTarget = null;

            _victimPreviewEditor = CreateEditor(throwMove.victimThrowClip);
            
            if (_victimPreviewEditor == null)
                return;

            // UNITY 6 SPECIFIC: Fetch and Reset the AvatarPreview
            // This forces the preview to recalculate its time bounds
            var avatarPreviewField = _victimPreviewEditor.GetType().GetField("m_AvatarPreview",
                BindingFlags.Instance | BindingFlags.NonPublic);

            if (avatarPreviewField != null)
            {
                object avatarPreview = avatarPreviewField.GetValue(_victimPreviewEditor);
                if (avatarPreview != null)
                {
                    // Force the preview to re-calculate its time bounds
                    var resetMethod = avatarPreview.GetType().GetMethod("Reset",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    resetMethod?.Invoke(avatarPreview, null);
                }
            }

            CacheVictimTimeReflection();
        }
    }

    /// <summary>
    /// Draws the timeline section showing animation info, visual timeline bar, and frame data for the victim.
    /// </summary>
    private void DrawVictimTimeline(CombatThrow throwMove)
    {
        AnimationClip clip = throwMove.victimThrowClip;
        int totalFrames = Mathf.RoundToInt(clip.length * clip.frameRate);

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField($"Victim Animation Timeline. Total Frames: {totalFrames}", EditorStyles.boldLabel);

        DrawVictimTimelineBar(throwMove);
        DrawVictimFrameData(throwMove);
    }

    /// <summary>
    /// Draws the visual timeline bar for the victim (simpler, no special markers).
    /// </summary>
    private void DrawVictimTimelineBar(CombatThrow throwMove)
    {
        // Empty - victim doesn't need a timeline bar since it's synced to attacker
    }

    /// <summary>
    /// Draws the frame data box showing live frame tracking for the victim.
    /// </summary>
    private void DrawVictimFrameData(CombatThrow throwMove)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        DrawVictimLiveFrameTracking(throwMove);

        EditorGUILayout.EndVertical();
    }

    /// <summary>
    /// Displays the current frame being previewed in the victim animation.
    /// Updates in real-time as the preview scrubber is moved.
    /// </summary>
    private void DrawVictimLiveFrameTracking(CombatThrow throwMove)
    {
        float currentTime = GetVictimPreviewTime();
        if (currentTime < 0) return;

        int currentFrame = Mathf.RoundToInt(currentTime * throwMove.victimThrowClip.frameRate);
        
        // Highlight current frame with cyan background
        Color originalColor = GUI.backgroundColor;
        GUI.backgroundColor = Color.cyan;
        
        try
        {
            EditorGUILayout.BeginVertical(EditorStyles.textArea);
            EditorGUILayout.LabelField($"{VictimLivePreviewLabel.text}{currentFrame}", EditorStyles.boldLabel);
            EditorGUILayout.EndVertical();
        }
        finally
        {
            // Ensure color is always restored, even if an exception occurs
            GUI.backgroundColor = originalColor;
        }
    }

    /// <summary>
    /// Draws Unity's built-in animation preview window for the victim (synced to attacker).
    /// </summary>
    private void DrawVictimPreview()
    {
        if (_victimPreviewEditor != null && _victimPreviewEditor.HasPreviewGUI())
        {
            // Sync again right before drawing to ensure latest camera state
            SyncVictimPreviewToAttacker();
            
            // Disable GUI to make it non-interactive
            bool wasEnabled = GUI.enabled;
            GUI.enabled = false;
            
            Rect previewRect = GUILayoutUtility.GetRect(200, 250, GUILayout.ExpandWidth(true));
            
            // Use OnInteractivePreviewGUI but with GUI disabled so it shows but can't be controlled
            _victimPreviewEditor.OnInteractivePreviewGUI(previewRect, EditorStyles.textArea);
            
            GUI.enabled = wasEnabled;
        }
    }

    /// <summary>
    /// Draws a split-screen preview showing both attacker and victim animations side-by-side.
    /// The attacker preview is on the left (interactive), victim on the right (synced, non-interactive).
    /// </summary>
    private void DrawSplitScreenPreview()
    {
        if (_attackerPreviewEditor == null || _victimPreviewEditor == null)
            return;

        if (!_attackerPreviewEditor.HasPreviewGUI() || !_victimPreviewEditor.HasPreviewGUI())
            return;

        // Force preview initialization if needed
        _attackerPreviewEditor.ReloadPreviewInstances();
        _victimPreviewEditor.ReloadPreviewInstances();

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("Combined Preview (Attacker | Victim)", EditorStyles.boldLabel);

        // Get a larger rect for the combined preview
        Rect fullRect = GUILayoutUtility.GetRect(400, 300, GUILayout.ExpandWidth(true));

        // Calculate split rectangles with a small gap
        float gap = 4f;
        float halfWidth = (fullRect.width - gap) / 2f;

        Rect attackerRect = new Rect(fullRect.x, fullRect.y, halfWidth, fullRect.height);
        Rect victimRect = new Rect(fullRect.x + halfWidth + gap, fullRect.y, halfWidth, fullRect.height);

        // Draw attacker preview (interactive) - this must happen first to capture interactions
        _attackerPreviewEditor.OnInteractivePreviewGUI(attackerRect, EditorStyles.textArea);

        // Sync victim to attacker immediately after drawing attacker
        SyncVictimPreviewToAttacker();

        // Draw victim preview (non-interactive, synced)
        bool wasEnabled = GUI.enabled;
        GUI.enabled = false;
        _victimPreviewEditor.OnInteractivePreviewGUI(victimRect, EditorStyles.textArea);
        GUI.enabled = wasEnabled;

        // Draw separator line
        Rect separatorRect = new Rect(fullRect.x + halfWidth + 1, fullRect.y, 2f, fullRect.height);
        EditorGUI.DrawRect(separatorRect, new Color(0.3f, 0.3f, 0.3f, 0.8f));
    }

    /// <summary>
    /// Synchronizes the victim preview to match the attacker preview's time and transform properties.
    /// This makes the victim animation play in perfect sync with the attacker animation.
    /// </summary>
    private void SyncVictimPreviewToAttacker()
    {
        if (_attackerPreviewEditor == null || _victimPreviewEditor == null)
            return;

        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        // Get attacker's AvatarPreview
        var attackerAvatarPreviewField = _attackerPreviewEditor.GetType().GetField("m_AvatarPreview", flags);
        if (attackerAvatarPreviewField == null) return;
        
        object attackerAvatarPreview = attackerAvatarPreviewField.GetValue(_attackerPreviewEditor);
        if (attackerAvatarPreview == null) return;

        // Get victim's AvatarPreview
        var victimAvatarPreviewField = _victimPreviewEditor.GetType().GetField("m_AvatarPreview", flags);
        if (victimAvatarPreviewField == null) return;
        
        object victimAvatarPreview = victimAvatarPreviewField.GetValue(_victimPreviewEditor);
        if (victimAvatarPreview == null) return;

        System.Type avatarPreviewType = attackerAvatarPreview.GetType();

        // Sync timeControl (this includes current time, playing state, etc.)
        var timeControlField = avatarPreviewType.GetField("timeControl", flags);
        if (timeControlField != null)
        {
            object attackerTimeControl = timeControlField.GetValue(attackerAvatarPreview);
            object victimTimeControl = timeControlField.GetValue(victimAvatarPreview);
            
            if (attackerTimeControl != null && victimTimeControl != null)
            {
                System.Type timeControlType = attackerTimeControl.GetType();
                
                // Sync current time
                var currentTimeField = timeControlType.GetField("currentTime", flags);
                if (currentTimeField != null)
                {
                    float attackerTime = (float)currentTimeField.GetValue(attackerTimeControl);
                    currentTimeField.SetValue(victimTimeControl, attackerTime);
                }

                // Also try syncing m_CurrentTime if currentTime doesn't exist
                var mCurrentTimeField = timeControlType.GetField("m_CurrentTime", flags);
                if (mCurrentTimeField != null)
                {
                    float attackerTime = (float)mCurrentTimeField.GetValue(attackerTimeControl);
                    mCurrentTimeField.SetValue(victimTimeControl, attackerTime);
                }

                // Sync playing state
                var playingField = timeControlType.GetField("playing", flags);
                if (playingField != null)
                {
                    bool attackerPlaying = (bool)playingField.GetValue(attackerTimeControl);
                    playingField.SetValue(victimTimeControl, attackerPlaying);
                }

                // Sync playback speed
                var playbackSpeedField = timeControlType.GetField("playbackSpeed", flags);
                if (playbackSpeedField == null)
                    playbackSpeedField = timeControlType.GetField("m_PlaybackSpeed", flags);
                
                if (playbackSpeedField != null)
                {
                    float attackerSpeed = (float)playbackSpeedField.GetValue(attackerTimeControl);
                    playbackSpeedField.SetValue(victimTimeControl, attackerSpeed);
                }
            }
        }

        // Sync camera properties (rotation, distance, etc.)
        SyncPreviewCameraProperties(attackerAvatarPreview, victimAvatarPreview, avatarPreviewType, flags);

        // Force the victim preview to update
        var doAvatarPreviewMethod = avatarPreviewType.GetMethod("DoAvatarPreview", flags);
        if (doAvatarPreviewMethod == null)
        {
            // Try alternate method names
            doAvatarPreviewMethod = avatarPreviewType.GetMethod("DoPreview", flags);
        }
    }

    /// <summary>
    /// Synchronizes camera properties between attacker and victim previews.
    /// </summary>
    private void SyncPreviewCameraProperties(object attackerPreview, object victimPreview, System.Type previewType, BindingFlags flags)
    {
        // Sync pivot (camera target position)
        var pivotField = previewType.GetField("m_PivotPositionOffset", flags);
        if (pivotField == null)
            pivotField = previewType.GetField("pivotPositionOffset", flags);
        
        if (pivotField != null)
        {
            try
            {
                Vector3 attackerPivot = (Vector3)pivotField.GetValue(attackerPreview);
                pivotField.SetValue(victimPreview, attackerPivot);
            }
            catch { }
        }

        // Sync rotation - try multiple field names
        var rotationField = previewType.GetField("m_Rotation", flags);
        if (rotationField == null)
            rotationField = previewType.GetField("rotation", flags);
        if (rotationField == null)
            rotationField = previewType.GetField("m_PreviewDir", flags);
        
        if (rotationField != null)
        {
            try
            {
                object attackerRotation = rotationField.GetValue(attackerPreview);
                
                // Apply 180 degree offset to victim rotation so they face the attacker
                if (attackerRotation is Vector2 rot2D)
                {
                    // For Vector2 rotation (yaw/pitch), add 180 to the yaw (x component)
                    Vector2 victimRotation = new(rot2D.x + 180f, rot2D.y);
                    rotationField.SetValue(victimPreview, victimRotation);
                }
                else
                {
                    // Fallback: just copy if it's not Vector2
                    rotationField.SetValue(victimPreview, attackerRotation);
                }
            }
            catch { }
        }

        // Sync zoom/distance - comprehensive approach trying all possible field/property names
        TrySyncZoom(attackerPreview, victimPreview, previewType, flags);
        
        // Sync camera transform directly as a final attempt
        var previewUtilityField = previewType.GetField("m_PreviewUtility", flags);
        if (previewUtilityField != null)
        {
            try
            {
                object attackerUtility = previewUtilityField.GetValue(attackerPreview);
                object victimUtility = previewUtilityField.GetValue(victimPreview);
                
                if (attackerUtility != null && victimUtility != null)
                {
                    System.Type utilityType = attackerUtility.GetType();
                    
                    // Try to get the camera from PreviewUtility
                    var cameraProperty = utilityType.GetProperty("camera", flags);
                    if (cameraProperty == null)
                    {
                        var cameraField = utilityType.GetField("m_Camera", flags);
                        if (cameraField != null)
                        {
                            Camera attackerCamera = (Camera)cameraField.GetValue(attackerUtility);
                            Camera victimCamera = (Camera)cameraField.GetValue(victimUtility);
                            
                            if (attackerCamera != null && victimCamera != null)
                            {
                                SyncCameraProperties(attackerCamera, victimCamera);
                            }
                        }
                    }
                    else
                    {
                        Camera attackerCamera = (Camera)cameraProperty.GetValue(attackerUtility);
                        Camera victimCamera = (Camera)cameraProperty.GetValue(victimUtility);
                        
                        if (attackerCamera != null && victimCamera != null)
                        {
                            SyncCameraProperties(attackerCamera, victimCamera);
                        }
                    }
                }
            }
            catch { }
        }
    }
    
    /// <summary>
    /// Attempts to sync zoom/scale using every possible field and property name.
    /// </summary>
    private void TrySyncZoom(object attackerPreview, object victimPreview, System.Type previewType, BindingFlags flags)
    {
        // List of all possible zoom-related field names to try
        string[] zoomFieldNames = new[]
        {
            "m_AvatarScale",
            "avatarScale", 
            "m_ZoomFactor",
            "zoomFactor",
            "m_CameraDistance",
            "cameraDistance",
            "m_ViewTool",
            "m_Zoom",
            "zoom",
            "m_OrthoGraphicSize",
            "m_Size"
        };
        
        // Try each field name
        foreach (string fieldName in zoomFieldNames)
        {
            var field = previewType.GetField(fieldName, flags);
            if (field != null)
            {
                try
                {
                    object attackerValue = field.GetValue(attackerPreview);
                    if (attackerValue != null)
                    {
                        field.SetValue(victimPreview, attackerValue);
                        // Don't return - try all of them to maximize chances
                    }
                }
                catch { }
            }
        }
        
        // Try properties as well
        string[] zoomPropertyNames = new[]
        {
            "avatarScale",
            "zoomFactor",
            "cameraDistance",
            "zoom"
        };
        
        foreach (string propName in zoomPropertyNames)
        {
            var property = previewType.GetProperty(propName, flags);
            if (property != null && property.CanRead && property.CanWrite)
            {
                try
                {
                    object attackerValue = property.GetValue(attackerPreview);
                    if (attackerValue != null)
                    {
                        property.SetValue(victimPreview, attackerValue);
                    }
                }
                catch { }
            }
        }
    }

    /// <summary>
    /// Synchronizes Camera component properties directly.
    /// </summary>
    private void SyncCameraProperties(Camera attackerCamera, Camera victimCamera)
    {
        // Sync the camera's position (distance from origin)
        float attackerDistance = attackerCamera.transform.position.magnitude;
        Vector3 victimDirection = victimCamera.transform.position.normalized;
        victimCamera.transform.position = victimDirection * attackerDistance;
        
        // Sync field of view
        victimCamera.fieldOfView = attackerCamera.fieldOfView;
        
        // Sync near and far clipping planes
        victimCamera.nearClipPlane = attackerCamera.nearClipPlane;
        victimCamera.farClipPlane = attackerCamera.farClipPlane;
    }

    /// <summary>
    /// Gets the current time position of the victim animation preview using reflection.
    /// Returns -1 if unable to read the time.
    /// </summary>
    private float GetVictimPreviewTime()
    {
        if (_victimTimeTarget == null) return -1f;

        // Try field access first (faster than property)
        if (_victimTimeField != null)
        {
            try
            {
                return (float)_victimTimeField.GetValue(_victimTimeTarget);
            }
            catch
            {
                // Reflection failed, try recaching once
                CacheVictimTimeReflection();
            }
        }

        // Fallback to property access
        if (_victimTimeProperty != null)
        {
            try
            {
                return (float)_victimTimeProperty.GetValue(_victimTimeTarget);
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
    /// Caches reflection info for accessing the victim animation preview's current time.
    /// </summary>
    private void CacheVictimTimeReflection()
    {
        if (_victimPreviewEditor == null) return;

        System.Type editorType = _victimPreviewEditor.GetType();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        // Try to get m_State field first (most reliable for time tracking)
        FieldInfo stateField = editorType.GetField("m_State", flags);
        if (stateField != null)
        {
            _victimTimeTarget = stateField.GetValue(_victimPreviewEditor);
            if (_victimTimeTarget != null)
            {
                _victimTimeField = _victimTimeTarget.GetType().GetField("m_Time", flags);
                if (_victimTimeField != null) return;
            }
        }

        // Fallback to direct property/field access on the editor itself
        _victimTimeTarget = _victimPreviewEditor;
        _victimTimeProperty = editorType.GetProperty("time", flags) ?? editorType.GetProperty("currentTime", flags);
        _victimTimeField = editorType.GetField("m_Time", flags) ?? editorType.GetField("m_PreviewTime", flags);
    }

    /// <summary>
    /// Cleanup when editor is disabled or destroyed.
    /// Destroys the animation preview editors to prevent memory leaks.
    /// </summary>
    private void OnDisable()
    {
        // Unsubscribe from editor update
        EditorApplication.update -= OnEditorUpdate;
        
        // Clean up preview editors
        if (_attackerPreviewEditor != null)
        {
            try { DestroyImmediate(_attackerPreviewEditor); }
            catch { /* Ignore errors during cleanup */ }
            _attackerPreviewEditor = null;
        }

        if (_victimPreviewEditor != null)
        {
            try { DestroyImmediate(_victimPreviewEditor); }
            catch { /* Ignore errors during cleanup */ }
            _victimPreviewEditor = null;
        }
    }
}
