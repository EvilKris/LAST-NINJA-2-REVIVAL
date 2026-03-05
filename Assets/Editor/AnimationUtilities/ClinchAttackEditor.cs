using UnityEngine;
using UnityEditor;
using System.Reflection;

/// <summary>
/// Custom editor for ClinchAttack ScriptableObjects.
/// Displays a visual timeline showing the hitbox window,
/// along with animation previews for both attacker and victim clips.
/// </summary>
[CustomEditor(typeof(ClinchAttack))]
public class ClinchAttackEditor : Editor
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
    private static readonly GUIContent HitboxLabel = new("Active Hitbox: ");

    // Cached colors to avoid allocations
    private static readonly Color TimelineBackgroundColor = new(0.15f, 0.15f, 0.15f);
    private static readonly Color HitWindowColor = new(1f, 0.3f, 0.3f, 0.7f);
    private static readonly Color ComboWindowColor = new(0.3f, 1f, 0.3f, 0.7f);
    private static readonly GUIContent ComboWindowLabel = new("Combo Window: ");
    private static readonly GUIContent NoComboLabel = new("Combo Window: No Combo!");

    private void OnEnable()
    {
        _forceRecreation = true;

        EditorApplication.update -= OnEditorUpdate;
        EditorApplication.update += OnEditorUpdate;
    }

    private void OnEditorUpdate()
    {
        if (_attackerPreviewEditor != null || _victimPreviewEditor != null)
            Repaint();
    }

    public override void OnInspectorGUI()
    {
        EditorGUI.BeginChangeCheck();
        DrawDefaultInspector();
        EditorGUI.EndChangeCheck();

        ClinchAttack attackMove = (ClinchAttack)target;

        int currentTargetID = attackMove.GetInstanceID();
        int currentAttackerClipID = attackMove.attackerAttackClip != null ? attackMove.attackerAttackClip.GetInstanceID() : -1;
        int currentVictimClipID = attackMove.victimAttackClip != null ? attackMove.victimAttackClip.GetInstanceID() : -1;

        bool targetChanged = _lastTargetInstanceID != currentTargetID;
        bool attackerClipChanged = _lastAttackerClipInstanceID != currentAttackerClipID;
        bool victimClipChanged = _lastVictimClipInstanceID != currentVictimClipID;

        if (targetChanged || attackerClipChanged || victimClipChanged || _forceRecreation)
        {
            _lastTargetInstanceID = currentTargetID;
            _lastAttackerClipInstanceID = currentAttackerClipID;
            _lastVictimClipInstanceID = currentVictimClipID;
            _forceRecreation = false;

            if (attackerClipChanged && _attackerPreviewEditor != null)
            {
                try { DestroyImmediate(_attackerPreviewEditor); } catch { }
                _attackerPreviewEditor = null;
            }

            if (victimClipChanged && _victimPreviewEditor != null)
            {
                try { DestroyImmediate(_victimPreviewEditor); } catch { }
                _victimPreviewEditor = null;
            }
        }

        if (attackMove.attackerAttackClip == null && attackMove.victimAttackClip == null)
            return;

        if (attackMove.attackerAttackClip != null)
        {
            SetupAttackerPreviewEditor(attackMove);
            if (_attackerPreviewEditor != null)
            {
                DrawAttackerTimeline(attackMove);
                FixPreviewEditorForAnimation(_attackerPreviewEditor);
            }
        }
        else if (_attackerPreviewEditor != null)
        {
            try { DestroyImmediate(_attackerPreviewEditor); } catch { }
            _attackerPreviewEditor = null;
        }

        if (attackMove.victimAttackClip != null)
        {
            SetupVictimPreviewEditor(attackMove);
            if (_victimPreviewEditor != null)
            {
                DrawVictimTimeline(attackMove);
                FixPreviewEditorForAnimation(_victimPreviewEditor);
            }
        }
        else if (_victimPreviewEditor != null)
        {
            try { DestroyImmediate(_victimPreviewEditor); } catch { }
            _victimPreviewEditor = null;
        }

        if (attackMove.attackerAttackClip != null && attackMove.victimAttackClip != null)
            DrawSplitScreenPreview();
        else if (attackMove.attackerAttackClip != null)
            DrawAttackerPreview();
        else if (attackMove.victimAttackClip != null)
            DrawVictimPreview();
    }

    // ?????????????????????????????????????????????
    // PREVIEW FIX
    // ?????????????????????????????????????????????

    private static FieldInfo _cachedAvatarPreviewFieldInfo;
    private static FieldInfo _cachedTimeControlFieldInfo;
    private static FieldInfo _cachedStopTimeFieldInfo;

    private static void FixPreviewEditorForAnimation(Editor editor)
    {
        if (editor.target is not AnimationClip clip) return;

        if (_cachedAvatarPreviewFieldInfo != null && _cachedTimeControlFieldInfo != null && _cachedStopTimeFieldInfo != null)
        {
            var value = _cachedAvatarPreviewFieldInfo.GetValue(editor);
            if (value == null) return;
            var subValue = _cachedTimeControlFieldInfo.GetValue(value);
            if (subValue == null) return;
            _cachedStopTimeFieldInfo.SetValue(subValue, clip.length);
        }
        else
        {
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

            _cachedStopTimeFieldInfo.SetValue(subValue, clip.length);
        }
    }

    // ?????????????????????????????????????????????
    // ATTACKER PREVIEW
    // ?????????????????????????????????????????????

    private void SetupAttackerPreviewEditor(ClinchAttack attackMove)
    {
        if (attackMove.attackerAttackClip == null)
        {
            if (_attackerPreviewEditor != null)
            {
                DestroyImmediate(_attackerPreviewEditor);
                _attackerPreviewEditor = null;
            }
            return;
        }

        bool editorExists = _attackerPreviewEditor != null;
        bool targetValid = editorExists && _attackerPreviewEditor.target != null;
        bool hasPreviewGUI = targetValid && _attackerPreviewEditor.HasPreviewGUI();
        bool needsRecreation = !editorExists || !targetValid || !hasPreviewGUI ||
                               _attackerPreviewEditor.target != attackMove.attackerAttackClip;

        if (needsRecreation)
        {
            if (_attackerPreviewEditor != null)
            {
                try { DestroyImmediate(_attackerPreviewEditor); } catch { }
            }

            _attackerTimeProperty = null;
            _attackerTimeField = null;
            _attackerTimeTarget = null;

            _attackerPreviewEditor = CreateEditor(attackMove.attackerAttackClip);
            if (_attackerPreviewEditor == null) return;

            var avatarPreviewField = _attackerPreviewEditor.GetType().GetField("m_AvatarPreview",
                BindingFlags.Instance | BindingFlags.NonPublic);

            if (avatarPreviewField != null)
            {
                object avatarPreview = avatarPreviewField.GetValue(_attackerPreviewEditor);
                if (avatarPreview != null)
                {
                    var resetMethod = avatarPreview.GetType().GetMethod("Reset",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    resetMethod?.Invoke(avatarPreview, null);
                }
            }

            CacheAttackerTimeReflection();
        }
    }

    private void DrawAttackerTimeline(ClinchAttack attackMove)
    {
        AnimationClip clip = attackMove.attackerAttackClip;
        int totalFrames = Mathf.RoundToInt(clip.length * clip.frameRate);

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField($"Attacker Animation Timeline. Total Frames: {totalFrames}", EditorStyles.boldLabel);

        DrawAttackerTimelineBar(attackMove);
        DrawAttackerFrameData(attackMove, totalFrames);
    }

    private void DrawAttackerTimelineBar(ClinchAttack attackMove)
    {
        Rect rect = GUILayoutUtility.GetRect(10, 30, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(rect, TimelineBackgroundColor);

        float rectWidth = rect.width;
        float rectX = rect.x;
        float rectY = rect.y;
        float rectHeight = rect.height;

        AnimationClip clip = attackMove.attackerAttackClip;
        int totalFrames = Mathf.RoundToInt(clip.length * clip.frameRate);
        int hitStartFrame = Mathf.RoundToInt(totalFrames * attackMove.hitStart);
        int hitEndFrame = Mathf.RoundToInt(totalFrames * attackMove.hitEnd);

        float hitStartPx = rectX + (rectWidth * attackMove.hitStart);
        float hitEndPx = rectX + (rectWidth * attackMove.hitEnd);

        // Draw Hit Window (Red)
        Rect hitRect = new Rect(
            hitStartPx,
            rectY,
            Mathf.Max(2, hitEndPx - hitStartPx),
            rectHeight
        );
        EditorGUI.DrawRect(hitRect, HitWindowColor);

        string hitLabel = $"HIT {hitStartFrame}-{hitEndFrame}";
        GUIContent hitContent = new GUIContent(hitLabel);
        Vector2 hitLabelSize = EditorStyles.whiteMiniLabel.CalcSize(hitContent);

        Rect hitLabelRect = new Rect(
            hitStartPx + 2,
            rectY + (rectHeight - hitLabelSize.y) * 0.5f,
            hitLabelSize.x,
            hitLabelSize.y
        );

        EditorGUI.DrawRect(new Rect(hitLabelRect.x - 1, hitLabelRect.y, hitLabelSize.x + 2, hitLabelSize.y),
            new Color(0, 0, 0, 0.5f));
        EditorGUI.LabelField(hitLabelRect, hitContent, EditorStyles.whiteMiniLabel);

        // Draw Combo Window (Green)
        float comboStartPx = rectX + (rectWidth * attackMove.comboStart);
        float comboEndPx = rectX + (rectWidth * attackMove.comboEnd);
        int comboStartFrame = Mathf.RoundToInt(totalFrames * attackMove.comboStart);
        int comboEndFrame = Mathf.RoundToInt(totalFrames * attackMove.comboEnd);

        Rect comboRect = new Rect(
            comboStartPx,
            rectY,
            Mathf.Max(2, comboEndPx - comboStartPx),
            rectHeight
        );
        EditorGUI.DrawRect(comboRect, ComboWindowColor);

        string comboLabel = (attackMove.comboStart >= 1f && attackMove.comboEnd >= 1f)
            ? "COMBO (Off)"
            : $"COMBO {comboStartFrame}-{comboEndFrame}";
        GUIContent comboContent = new GUIContent(comboLabel);
        Vector2 comboLabelSize = EditorStyles.whiteMiniLabel.CalcSize(comboContent);

        Rect comboLabelRect = new Rect(
            comboStartPx + 2,
            rectY + (rectHeight - comboLabelSize.y) * 0.5f,
            comboLabelSize.x,
            comboLabelSize.y
        );

        EditorGUI.DrawRect(new Rect(comboLabelRect.x - 1, comboLabelRect.y, comboLabelSize.x + 2, comboLabelSize.y),
            new Color(0, 0, 0, 0.5f));
        EditorGUI.LabelField(comboLabelRect, comboContent, EditorStyles.whiteMiniLabel);
    }

    private void DrawAttackerFrameData(ClinchAttack attackMove, int totalFrames)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        DrawAttackerLiveFrameTracking(attackMove);
        DrawHitboxFrames(attackMove, totalFrames);
        DrawComboFrames(attackMove, totalFrames);

        EditorGUILayout.EndVertical();
    }

    private void DrawAttackerLiveFrameTracking(ClinchAttack attackMove)
    {
        float currentTime = GetAttackerPreviewTime();
        if (currentTime < 0) return;

        int currentFrame = Mathf.RoundToInt(currentTime * attackMove.attackerAttackClip.frameRate);

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
            GUI.backgroundColor = originalColor;
        }
    }

    private void DrawHitboxFrames(ClinchAttack attackMove, int totalFrames)
    {
        int hitStart = Mathf.RoundToInt(totalFrames * attackMove.hitStart);
        int hitEnd = Mathf.RoundToInt(totalFrames * attackMove.hitEnd);
        EditorGUILayout.LabelField($"{HitboxLabel.text}Frame {hitStart} to {hitEnd}", EditorStyles.miniBoldLabel);
    }

    private void DrawComboFrames(ClinchAttack attackMove, int totalFrames)
    {
        bool noCombo = attackMove.comboStart >= 1f && attackMove.comboEnd >= 1f;

        if (noCombo)
        {
            Color originalColor = GUI.color;
            GUI.color = Color.gray;
            try { EditorGUILayout.LabelField(NoComboLabel, EditorStyles.miniBoldLabel); }
            finally { GUI.color = originalColor; }
        }
        else
        {
            int comboStart = Mathf.RoundToInt(totalFrames * attackMove.comboStart);
            int comboEnd = Mathf.RoundToInt(totalFrames * attackMove.comboEnd);
            EditorGUILayout.LabelField($"{ComboWindowLabel.text}Frame {comboStart} to {comboEnd}", EditorStyles.miniBoldLabel);
        }
    }

    private void DrawAttackerPreview()
    {
        if (_attackerPreviewEditor != null && _attackerPreviewEditor.HasPreviewGUI())
        {
            Rect previewRect = GUILayoutUtility.GetRect(200, 250, GUILayout.ExpandWidth(true));
            _attackerPreviewEditor.OnInteractivePreviewGUI(previewRect, EditorStyles.textArea);
        }
    }

    private float GetAttackerPreviewTime()
    {
        if (_attackerTimeTarget == null) return -1f;

        if (_attackerTimeField != null)
        {
            try { return (float)_attackerTimeField.GetValue(_attackerTimeTarget); }
            catch { CacheAttackerTimeReflection(); }
        }

        if (_attackerTimeProperty != null)
        {
            try { return (float)_attackerTimeProperty.GetValue(_attackerTimeTarget); }
            catch { return -1f; }
        }

        return -1f;
    }

    private void CacheAttackerTimeReflection()
    {
        if (_attackerPreviewEditor == null) return;

        System.Type editorType = _attackerPreviewEditor.GetType();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

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

        _attackerTimeTarget = _attackerPreviewEditor;
        _attackerTimeProperty = editorType.GetProperty("time", flags) ?? editorType.GetProperty("currentTime", flags);
        _attackerTimeField = editorType.GetField("m_Time", flags) ?? editorType.GetField("m_PreviewTime", flags);
    }

    // ?????????????????????????????????????????????
    // VICTIM PREVIEW
    // ?????????????????????????????????????????????

    private void SetupVictimPreviewEditor(ClinchAttack attackMove)
    {
        if (attackMove.victimAttackClip == null)
        {
            if (_victimPreviewEditor != null)
            {
                DestroyImmediate(_victimPreviewEditor);
                _victimPreviewEditor = null;
            }
            return;
        }

        bool editorExists = _victimPreviewEditor != null;
        bool targetValid = editorExists && _victimPreviewEditor.target != null;
        bool hasPreviewGUI = targetValid && _victimPreviewEditor.HasPreviewGUI();
        bool needsRecreation = !editorExists || !targetValid || !hasPreviewGUI ||
                               _victimPreviewEditor.target != attackMove.victimAttackClip;

        if (needsRecreation)
        {
            if (_victimPreviewEditor != null)
            {
                try { DestroyImmediate(_victimPreviewEditor); } catch { }
            }

            _victimTimeProperty = null;
            _victimTimeField = null;
            _victimTimeTarget = null;

            _victimPreviewEditor = CreateEditor(attackMove.victimAttackClip);
            if (_victimPreviewEditor == null) return;

            var avatarPreviewField = _victimPreviewEditor.GetType().GetField("m_AvatarPreview",
                BindingFlags.Instance | BindingFlags.NonPublic);

            if (avatarPreviewField != null)
            {
                object avatarPreview = avatarPreviewField.GetValue(_victimPreviewEditor);
                if (avatarPreview != null)
                {
                    var resetMethod = avatarPreview.GetType().GetMethod("Reset",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    resetMethod?.Invoke(avatarPreview, null);
                }
            }

            CacheVictimTimeReflection();
        }
    }

    private void DrawVictimTimeline(ClinchAttack attackMove)
    {
        AnimationClip clip = attackMove.victimAttackClip;
        int totalFrames = Mathf.RoundToInt(clip.length * clip.frameRate);

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField($"Victim Animation Timeline. Total Frames: {totalFrames}", EditorStyles.boldLabel);

        DrawVictimFrameData(attackMove);
    }

    private void DrawVictimFrameData(ClinchAttack attackMove)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        DrawVictimLiveFrameTracking(attackMove);
        EditorGUILayout.EndVertical();
    }

    private void DrawVictimLiveFrameTracking(ClinchAttack attackMove)
    {
        float currentTime = GetVictimPreviewTime();
        if (currentTime < 0) return;

        int currentFrame = Mathf.RoundToInt(currentTime * attackMove.victimAttackClip.frameRate);

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
            GUI.backgroundColor = originalColor;
        }
    }

    private void DrawVictimPreview()
    {
        if (_victimPreviewEditor != null && _victimPreviewEditor.HasPreviewGUI())
        {
            SyncVictimPreviewToAttacker();

            bool wasEnabled = GUI.enabled;
            GUI.enabled = false;
            Rect previewRect = GUILayoutUtility.GetRect(200, 250, GUILayout.ExpandWidth(true));
            _victimPreviewEditor.OnInteractivePreviewGUI(previewRect, EditorStyles.textArea);
            GUI.enabled = wasEnabled;
        }
    }

    private void DrawSplitScreenPreview()
    {
        if (_attackerPreviewEditor == null || _victimPreviewEditor == null) return;
        if (!_attackerPreviewEditor.HasPreviewGUI() || !_victimPreviewEditor.HasPreviewGUI()) return;

        _attackerPreviewEditor.ReloadPreviewInstances();
        _victimPreviewEditor.ReloadPreviewInstances();

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("Combined Preview (Attacker | Victim)", EditorStyles.boldLabel);

        Rect fullRect = GUILayoutUtility.GetRect(400, 300, GUILayout.ExpandWidth(true));

        float gap = 4f;
        float halfWidth = (fullRect.width - gap) / 2f;
        Rect attackerRect = new Rect(fullRect.x, fullRect.y, halfWidth, fullRect.height);
        Rect victimRect = new Rect(fullRect.x + halfWidth + gap, fullRect.y, halfWidth, fullRect.height);

        _attackerPreviewEditor.OnInteractivePreviewGUI(attackerRect, EditorStyles.textArea);

        SyncVictimPreviewToAttacker();

        bool wasEnabled = GUI.enabled;
        GUI.enabled = false;
        _victimPreviewEditor.OnInteractivePreviewGUI(victimRect, EditorStyles.textArea);
        GUI.enabled = wasEnabled;

        Rect separatorRect = new Rect(fullRect.x + halfWidth + 1, fullRect.y, 2f, fullRect.height);
        EditorGUI.DrawRect(separatorRect, new Color(0.3f, 0.3f, 0.3f, 0.8f));
    }

    private void SyncVictimPreviewToAttacker()
    {
        if (_attackerPreviewEditor == null || _victimPreviewEditor == null) return;

        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        var attackerAvatarPreviewField = _attackerPreviewEditor.GetType().GetField("m_AvatarPreview", flags);
        if (attackerAvatarPreviewField == null) return;
        object attackerAvatarPreview = attackerAvatarPreviewField.GetValue(_attackerPreviewEditor);
        if (attackerAvatarPreview == null) return;

        var victimAvatarPreviewField = _victimPreviewEditor.GetType().GetField("m_AvatarPreview", flags);
        if (victimAvatarPreviewField == null) return;
        object victimAvatarPreview = victimAvatarPreviewField.GetValue(_victimPreviewEditor);
        if (victimAvatarPreview == null) return;

        System.Type avatarPreviewType = attackerAvatarPreview.GetType();

        var timeControlField = avatarPreviewType.GetField("timeControl", flags);
        if (timeControlField != null)
        {
            object attackerTimeControl = timeControlField.GetValue(attackerAvatarPreview);
            object victimTimeControl = timeControlField.GetValue(victimAvatarPreview);

            if (attackerTimeControl != null && victimTimeControl != null)
            {
                System.Type timeControlType = attackerTimeControl.GetType();

                var currentTimeField = timeControlType.GetField("currentTime", flags);
                if (currentTimeField != null)
                {
                    float attackerTime = (float)currentTimeField.GetValue(attackerTimeControl);
                    currentTimeField.SetValue(victimTimeControl, attackerTime);
                }

                var mCurrentTimeField = timeControlType.GetField("m_CurrentTime", flags);
                if (mCurrentTimeField != null)
                {
                    float attackerTime = (float)mCurrentTimeField.GetValue(attackerTimeControl);
                    mCurrentTimeField.SetValue(victimTimeControl, attackerTime);
                }

                var playingField = timeControlType.GetField("playing", flags);
                if (playingField != null)
                {
                    bool attackerPlaying = (bool)playingField.GetValue(attackerTimeControl);
                    playingField.SetValue(victimTimeControl, attackerPlaying);
                }

                var playbackSpeedField = timeControlType.GetField("playbackSpeed", flags)
                                      ?? timeControlType.GetField("m_PlaybackSpeed", flags);
                if (playbackSpeedField != null)
                {
                    float attackerSpeed = (float)playbackSpeedField.GetValue(attackerTimeControl);
                    playbackSpeedField.SetValue(victimTimeControl, attackerSpeed);
                }
            }
        }

        SyncPreviewCameraProperties(attackerAvatarPreview, victimAvatarPreview, avatarPreviewType, flags);
    }

    private void SyncPreviewCameraProperties(object attackerPreview, object victimPreview, System.Type previewType, BindingFlags flags)
    {
        var pivotField = previewType.GetField("m_PivotPositionOffset", flags)
                      ?? previewType.GetField("pivotPositionOffset", flags);
        if (pivotField != null)
        {
            try
            {
                Vector3 attackerPivot = (Vector3)pivotField.GetValue(attackerPreview);
                pivotField.SetValue(victimPreview, attackerPivot);
            }
            catch { }
        }

        var rotationField = previewType.GetField("m_Rotation", flags)
                         ?? previewType.GetField("rotation", flags)
                         ?? previewType.GetField("m_PreviewDir", flags);
        if (rotationField != null)
        {
            try
            {
                object attackerRotation = rotationField.GetValue(attackerPreview);
                if (attackerRotation is Vector2 rot2D)
                    rotationField.SetValue(victimPreview, new Vector2(rot2D.x + 180f, rot2D.y));
                else
                    rotationField.SetValue(victimPreview, attackerRotation);
            }
            catch { }
        }

        TrySyncZoom(attackerPreview, victimPreview, previewType, flags);

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
                    var cameraField = utilityType.GetField("m_Camera", flags);
                    if (cameraField != null)
                    {
                        Camera attackerCamera = (Camera)cameraField.GetValue(attackerUtility);
                        Camera victimCamera = (Camera)cameraField.GetValue(victimUtility);
                        if (attackerCamera != null && victimCamera != null)
                            SyncCameraProperties(attackerCamera, victimCamera);
                    }
                }
            }
            catch { }
        }
    }

    private void TrySyncZoom(object attackerPreview, object victimPreview, System.Type previewType, BindingFlags flags)
    {
        string[] zoomFieldNames = new[]
        {
            "m_AvatarScale", "avatarScale", "m_ZoomFactor", "zoomFactor",
            "m_CameraDistance", "cameraDistance", "m_ViewTool", "m_Zoom",
            "zoom", "m_OrthoGraphicSize", "m_Size"
        };

        foreach (string fieldName in zoomFieldNames)
        {
            var field = previewType.GetField(fieldName, flags);
            if (field != null)
            {
                try
                {
                    object attackerValue = field.GetValue(attackerPreview);
                    if (attackerValue != null)
                        field.SetValue(victimPreview, attackerValue);
                }
                catch { }
            }
        }

        string[] zoomPropertyNames = new[] { "avatarScale", "zoomFactor", "cameraDistance", "zoom" };
        foreach (string propName in zoomPropertyNames)
        {
            var property = previewType.GetProperty(propName, flags);
            if (property != null && property.CanRead && property.CanWrite)
            {
                try
                {
                    object attackerValue = property.GetValue(attackerPreview);
                    if (attackerValue != null)
                        property.SetValue(victimPreview, attackerValue);
                }
                catch { }
            }
        }
    }

    private void SyncCameraProperties(Camera attackerCamera, Camera victimCamera)
    {
        float attackerDistance = attackerCamera.transform.position.magnitude;
        Vector3 victimDirection = victimCamera.transform.position.normalized;
        victimCamera.transform.position = victimDirection * attackerDistance;
        victimCamera.fieldOfView = attackerCamera.fieldOfView;
        victimCamera.nearClipPlane = attackerCamera.nearClipPlane;
        victimCamera.farClipPlane = attackerCamera.farClipPlane;
    }

    private float GetVictimPreviewTime()
    {
        if (_victimTimeTarget == null) return -1f;

        if (_victimTimeField != null)
        {
            try { return (float)_victimTimeField.GetValue(_victimTimeTarget); }
            catch { CacheVictimTimeReflection(); }
        }

        if (_victimTimeProperty != null)
        {
            try { return (float)_victimTimeProperty.GetValue(_victimTimeTarget); }
            catch { return -1f; }
        }

        return -1f;
    }

    private void CacheVictimTimeReflection()
    {
        if (_victimPreviewEditor == null) return;

        System.Type editorType = _victimPreviewEditor.GetType();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

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

        _victimTimeTarget = _victimPreviewEditor;
        _victimTimeProperty = editorType.GetProperty("time", flags) ?? editorType.GetProperty("currentTime", flags);
        _victimTimeField = editorType.GetField("m_Time", flags) ?? editorType.GetField("m_PreviewTime", flags);
    }

    private void OnDisable()
    {
        EditorApplication.update -= OnEditorUpdate;

        _cachedAvatarPreviewFieldInfo = null;
        _cachedTimeControlFieldInfo = null;
        _cachedStopTimeFieldInfo = null;

        if (_attackerPreviewEditor != null)
        {
            try { DestroyImmediate(_attackerPreviewEditor); } catch { }
            _attackerPreviewEditor = null;
        }

        if (_victimPreviewEditor != null)
        {
            try { DestroyImmediate(_victimPreviewEditor); } catch { }
            _victimPreviewEditor = null;
        }
    }
}
