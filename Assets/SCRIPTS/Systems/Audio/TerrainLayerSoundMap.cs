using UnityEngine;

/// <summary>
/// Place this component on a Unity Terrain GameObject to map each terrain layer to footstep sounds.
/// The entry at index 0 maps to terrain layer 0, index 1 to layer 1, etc. — matching the order
/// shown in the Terrain's Paint Texture layer list.
///
/// FootstepHandler will sample the splatmap at the hit point and pick the sound for whichever
/// layer has the highest blend weight (the most dominant painted texture).
///
/// SETUP:
/// 1. Add this component to your Terrain GameObject.
/// 2. Add one entry per terrain layer, in the same order as the layers in the Terrain inspector.
/// 3. Assign a TerrainSoundData asset to each entry.
/// </summary>
public class TerrainLayerSoundMap : MonoBehaviour
{
    [Tooltip("One entry per terrain layer, in the same order as the Terrain's Paint Texture layer list.")]
    public TerrainSoundData[] layerSounds;

    // Cached terrain reference to avoid repeated GetComponent calls.
    private Terrain _terrain;

    private void Awake()
    {
        _terrain = GetComponent<Terrain>();
    }

    /// <summary>
    /// Returns the TerrainSoundData for the most dominant terrain layer at the given world position.
    /// Returns null if no layers are mapped or all entries are unassigned.
    /// </summary>
    public TerrainSoundData GetDominantLayerSound(Vector3 worldPosition)
    {
        if (_terrain == null || layerSounds == null || layerSounds.Length == 0)
            return null;

        TerrainData td = _terrain.terrainData;
        int layerCount = td.alphamapLayers;

        // Convert world position to normalised terrain UV (0..1)
        Vector3 terrainPos = _terrain.transform.position;
        int mapX = Mathf.RoundToInt((worldPosition.x - terrainPos.x) / td.size.x * (td.alphamapWidth  - 1));
        int mapZ = Mathf.RoundToInt((worldPosition.z - terrainPos.z) / td.size.z * (td.alphamapHeight - 1));

        mapX = Mathf.Clamp(mapX, 0, td.alphamapWidth  - 1);
        mapZ = Mathf.Clamp(mapZ, 0, td.alphamapHeight - 1);

        // GetAlphamaps returns [z, x, layerIndex] with a 1x1 sample
        float[,,] map = td.GetAlphamaps(mapX, mapZ, 1, 1);

        int dominantLayer = 0;
        float dominantWeight = 0f;

        int layersToCheck = Mathf.Min(layerCount, layerSounds.Length);
        for (int i = 0; i < layersToCheck; i++)
        {
            float weight = map[0, 0, i];
            if (weight > dominantWeight)
            {
                dominantWeight = weight;
                dominantLayer  = i;
            }
        }

        return layerSounds[dominantLayer];
    }
}
