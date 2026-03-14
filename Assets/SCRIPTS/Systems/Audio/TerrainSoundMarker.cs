using UnityEngine;

/// <summary>
/// Place this component on any floor GameObject to identify its terrain type for footstep audio.
/// FootstepHandler raycasts downward, finds this component, and uses the assigned TerrainSoundData.
/// </summary>
public class TerrainSoundMarker : MonoBehaviour
{
    [Tooltip("The sound data for this terrain surface (stone, wood, grass, etc.)")]
    public TerrainSoundData terrainSoundData;
}
