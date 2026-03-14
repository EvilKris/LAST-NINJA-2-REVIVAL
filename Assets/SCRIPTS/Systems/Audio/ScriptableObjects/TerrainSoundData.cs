using UnityEngine;
using JSAM;

/// <summary>
/// ScriptableObject that defines the footstep sounds for a specific terrain type.
/// Attach a TerrainSoundMarker to any floor GameObject and assign this asset to it.
/// SoundFileObject already handles internal randomisation and variation.
/// </summary>
[CreateAssetMenu(fileName = "NewTerrainSoundData", menuName = "Audio/Terrain Sound Data")]
public class TerrainSoundData : ScriptableObject
{
    [Header("--- TERRAIN IDENTITY ---")]
    [Tooltip("Human-readable label for this terrain type (Stone, Wood, Grass, Metal, etc.)")]
    public string terrainLabel = "Stone";

    [Header("--- FOOTSTEP SOUNDS ---")]
    [Tooltip("Footstep sound played while walking on this terrain.")]
    public SoundFileObject walkFootstep;

    [Tooltip("Footstep sound played while running on this terrain. Falls back to walkFootstep if unassigned.")]
    public SoundFileObject runFootstep;
}
