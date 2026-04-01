using JSAM;
using UnityEngine;

public class PrefabBankManager : MonoBehaviour
{
    [Tooltip("Material used for phantom Demon Soul-type vfx")]
    public Material PhantomMaterial;

    [Header("GhostTrail")]
    [Tooltip("Material used for Drive Strike ghost trail vfx")]
    public Material GhostTrailsMat;
    public Material GhostTrailsMatAdditive;

    [Header("Healing")]
    [Tooltip("Mat used when Healing ")]
    public Material HealingMat;

    [Tooltip("Sound commences on Tier One Drive Strike")]
    public SoundFileObject Tier_One_Drive_Strike;
    public SoundFileObject Charge_Drive_Strike_Tier_Complete;

    [Header("Pickup")]
    [Tooltip("Default sound played when any item is picked up. Can be overridden per-item in ItemData.")]
    public SoundFileObject DefaultPickupSound;

    [Header("Environment AreaZone Sounds")]
    [Tooltip("Sounds played when player enters an AreaZone")]
    public SoundFileObject AreaSoundSwamp;
    public SoundFileObject AreaSoundWater;
    public SoundFileObject AreaSoundForest;

    [Header("Environment AreaZone Music")]
    [Tooltip("AreaZone but for actual music change")]

    public MusicFileObject thisLevelMusic;
    public MusicFileObject shrineMusic;




    #region Useful Universal Functions
    /// <summary>
    /// Swaps materials on all renderers in a target GameObject's hierarchy.
    /// </summary>
    /// <param name="target">Target GameObject/Transform to swap materials on (null = this GameObject)</param>
    /// <param name="newMat">Material to apply to renderers</param>
    /// <param name="includeInactive">Include inactive GameObjects in search</param>
    /// <param name="createInstances">Create material instances instead of using shared material</param>
    /// <param name="materialSlotIndex">Specific material slot to replace (-1 for all slots)</param>
    /// <param name="preserveProperties">Copy properties from original materials to new material</param>
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
            Debug.LogWarning("Cannot swap to null material!");
            return;
        }

        // Use this GameObject if target is null
        Transform targetTransform = target != null ? target : transform;

        Renderer[] renderers = targetTransform.GetComponentsInChildren<Renderer>(includeInactive);

        if (renderers.Length == 0)
        {
            Debug.LogWarning($"No renderers found on {targetTransform.name} or its children!");
            return;
        }

        foreach (Renderer renderer in renderers)
        {
            int matCount = renderer.sharedMaterials.Length;
            Material[] newMats = new Material[matCount];

            for (int i = 0; i < matCount; i++)
            {
                // Only replace specific slot or all slots
                if (materialSlotIndex == -1 || i == materialSlotIndex)
                {
                    Material matToUse = createInstances ? new Material(newMat) : newMat;

                    // Optionally preserve color/texture from original material
                    if (preserveProperties && renderer.sharedMaterials[i] != null)
                    {
                        Material originalMat = renderer.sharedMaterials[i];
                        
                        // Preserve main texture if both materials have it
                        if (originalMat.HasProperty("_MainTex") && matToUse.HasProperty("_MainTexture"))
                        {
                            matToUse.SetTexture("_MainTexture", originalMat.mainTexture);
                        }
                        
                        // Preserve color if both materials have it
                        if (originalMat.HasProperty("_Color") && matToUse.HasProperty("_EmissionColor"))
                        {
                            Color originalColor = originalMat.color;
                            matToUse.SetColor("_EmissionColor", originalColor);
                        }
                    }

                    newMats[i] = matToUse;
                }
                else
                {
                    // Keep original material for other slots
                    newMats[i] = renderer.sharedMaterials[i];
                }
            }

            renderer.materials = newMats;
        }
    }

    /// <summary>
    /// GameObject overload for convenience
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
            preserveProperties
        );
    }

    /// <summary>
    /// Simplified overload - swap materials on this GameObject with instances
    /// </summary>
    public void SwapOutAllMaterials(Material newMat)
    {
        SwapOutAllMaterials((Transform)null, newMat, true, true, -1, false);
    }

    /// <summary>
    /// Restores all renderers on target to their original shared materials (removes instances)
    /// </summary>
    public void RestoreSharedMaterials(Transform target = null)
    {
        Transform targetTransform = target != null ? target : transform;
        Renderer[] renderers = targetTransform.GetComponentsInChildren<Renderer>(true);

        foreach (Renderer renderer in renderers)
        {
            // Replace current materials array with shared materials to clean up instances
            Material[] sharedMats = renderer.sharedMaterials;
            renderer.materials = sharedMats;
        }
    }

    /// <summary>
    /// GameObject overload for RestoreSharedMaterials
    /// </summary>
    public void RestoreSharedMaterials(GameObject target)
    {
        RestoreSharedMaterials(target != null ? target.transform : null);
    }

    #endregion
}
