using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections.Generic;

public class PlayerManager : MonoBehaviour
{
    // Stores original sharedMaterials per renderer so RestoreSharedMaterials can
    // put back the real assets even after Unity overwrites sharedMaterials when
    // .materials is assigned.
    private readonly Dictionary<Renderer, Material[]> _originalMaterialsCache = new Dictionary<Renderer, Material[]>();

    // Stores the active state each ScriptableRendererFeature had before we first
    // touched it. Restored on quit and destroy to prevent SO asset mutation persisting
    // across Editor play sessions or between builds.
    private readonly Dictionary<ScriptableRendererFeature, bool> _featureOriginalStates
        = new Dictionary<ScriptableRendererFeature, bool>();

    // ???????????????????????????????????????????????????????????????????
    // Renderer Feature Toggle
    // ???????????????????????????????????????????????????????????????????

    /// <summary>
    /// Enables or disables a URP ScriptableRendererFeature by name across all renderer data entries.
    /// The original active state is snapshotted on first call per feature and restored automatically
    /// when the application quits or this component is destroyed, preventing SO asset mutation
    /// from persisting across Editor play sessions.
    /// </summary>
    /// <param name="featureName">The exact name of the renderer feature as shown in the Inspector.</param>
    /// <param name="active">True to enable, false to disable.</param>
    public void SetRendererFeatureActive(string featureName, bool active)
    {
        if (GraphicsSettings.defaultRenderPipeline is not UniversalRenderPipelineAsset urpAsset)
        {
            Debug.LogWarning("PlayerManager.SetRendererFeatureActive: Current render pipeline is not URP.");
            return;
        }

        bool found = false;
        foreach (ScriptableRendererData rendererData in urpAsset.rendererDataList)
        {
            if (rendererData == null) continue;
            foreach (ScriptableRendererFeature feature in rendererData.rendererFeatures)
            {
                if (feature == null || feature.name != featureName) continue;

                // Snapshot the original state the very first time we touch this feature
                // so we can restore it exactly when the session ends.
                if (!_featureOriginalStates.ContainsKey(feature))
                    _featureOriginalStates[feature] = feature.isActive;

                feature.SetActive(active);
                found = true;
            }
        }

        if (!found)
            Debug.LogWarning($"PlayerManager.SetRendererFeatureActive: No renderer feature named '{featureName}' was found.");
    }

    /// <summary>
    /// Restores every <see cref="ScriptableRendererFeature"/> that was modified this session
    /// back to the state it was in before <see cref="SetRendererFeatureActive"/> first touched it.
    /// Called automatically on application quit and component destroy.
    /// </summary>
    private void RestoreRendererFeatureStates()
    {
        foreach (var kvp in _featureOriginalStates)
        {
            if (kvp.Key != null)
                kvp.Key.SetActive(kvp.Value);
        }
        _featureOriginalStates.Clear();
    }

    private void OnApplicationQuit()
    {
        RestoreRendererFeatureStates();
    }

    private void OnDestroy()
    {
        RestoreRendererFeatureStates();
    }

    // ???????????????????????????????????????????????????????????????????
    // Material Swap / Restore (moved from PrefabBankManager)
    // ???????????????????????????????????????????????????????????????????

    /// <summary>
    /// Swaps materials on all renderers in a target GameObject's hierarchy.
    /// </summary>
    /// <param name="target">Target Transform to swap materials on (null = no-op).</param>
    /// <param name="newMat">Material to apply to renderers.</param>
    /// <param name="includeInactive">Include inactive GameObjects in search.</param>
    /// <param name="createInstances">Create material instances instead of using shared material.</param>
    /// <param name="materialSlotIndex">Specific material slot to replace (-1 for all slots).</param>
    /// <param name="preserveProperties">Copy properties from original materials to new material.</param>
    public void SwapOutAllMaterials(
        Transform target,
        Material newMat,
        bool includeInactive = true,
        bool createInstances = true,
        int materialSlotIndex = -1,
        bool preserveProperties = false)
    {
        if (newMat == null)
        {
            Debug.LogWarning("PlayerManager.SwapOutAllMaterials: Cannot swap to null material!");
            return;
        }

        Transform targetTransform = target != null ? target : null;
        if (targetTransform == null)
        {
            Debug.LogWarning("PlayerManager.SwapOutAllMaterials: Target is null.");
            return;
        }

        Renderer[] renderers = targetTransform.GetComponentsInChildren<Renderer>(includeInactive);

        if (renderers.Length == 0)
        {
            Debug.LogWarning($"PlayerManager.SwapOutAllMaterials: No renderers found on {targetTransform.name} or its children.");
            return;
        }

        foreach (Renderer renderer in renderers)
        {
            // Cache the originals before overwriting so RestoreSharedMaterials can
            // recover them (assigning .materials corrupts .sharedMaterials in Unity).
            if (!_originalMaterialsCache.ContainsKey(renderer))
                _originalMaterialsCache[renderer] = renderer.sharedMaterials;

            int matCount = renderer.sharedMaterials.Length;
            Material[] newMats = new Material[matCount];

            for (int i = 0; i < matCount; i++)
            {
                if (materialSlotIndex == -1 || i == materialSlotIndex)
                {
                    Material matToUse = createInstances ? new Material(newMat) : newMat;

                    if (preserveProperties && renderer.sharedMaterials[i] != null)
                    {
                        Material originalMat = renderer.sharedMaterials[i];

                        if (originalMat.HasProperty("_MainTex") && matToUse.HasProperty("_MainTexture"))
                            matToUse.SetTexture("_MainTexture", originalMat.mainTexture);

                        if (originalMat.HasProperty("_Color") && matToUse.HasProperty("_EmissionColor"))
                            matToUse.SetColor("_EmissionColor", originalMat.color);
                    }

                    newMats[i] = matToUse;
                }
                else
                {
                    newMats[i] = renderer.sharedMaterials[i];
                }
            }

            renderer.materials = newMats;
        }
    }

    /// <summary>
    /// GameObject overload for convenience.
    /// </summary>
    public void SwapOutAllMaterials(
        GameObject target,
        Material newMat,
        bool includeInactive = true,
        bool createInstances = true,
        int materialSlotIndex = -1,
        bool preserveProperties = false)
    {
        SwapOutAllMaterials(
            target != null ? target.transform : null,
            newMat,
            includeInactive,
            createInstances,
            materialSlotIndex,
            preserveProperties);
    }

    /// <summary>
    /// Simplified overload � swaps all material slots on the target with shared-material instances.
    /// </summary>
    public void SwapOutAllMaterials(GameObject target, Material newMat, bool includeInactive)
    {
        SwapOutAllMaterials(target != null ? target.transform : null, newMat, includeInactive, true, -1, false);
    }

    /// <summary>
    /// Restores all renderers on target to their original shared materials (removes instances).
    /// </summary>
    public void RestoreSharedMaterials(Transform target = null)
    {
        if (target == null) return;

        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);

        foreach (Renderer renderer in renderers)
        {
            if (_originalMaterialsCache.TryGetValue(renderer, out Material[] originals))
            {
                renderer.materials = originals;
                _originalMaterialsCache.Remove(renderer);
            }
            else
            {
                renderer.materials = renderer.sharedMaterials;
            }
        }
    }

    /// <summary>
    /// GameObject overload for RestoreSharedMaterials.
    /// </summary>
    public void RestoreSharedMaterials(GameObject target)
    {
        RestoreSharedMaterials(target != null ? target.transform : null);
    }

    public void ToggleXrayRendererFeatures(bool v)
    {
        // These features are used by the player X-ray shader graph to show/hide the player model when the shader is active.
        // Get the spelling right! Check the LastNinja URP Data in the project for reference if you want to toggle these on/off.
        //SetRendererFeatureActive("PlayerVisible", v);
        SetRendererFeatureActive("PlayerHidden", v);
    }
}
