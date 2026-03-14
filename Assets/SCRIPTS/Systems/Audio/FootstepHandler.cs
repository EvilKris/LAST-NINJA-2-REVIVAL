using UnityEngine;
using JSAM;

/// <summary>
/// Handles footstep audio for a character, triggered by animation events on the walk/run clips.
/// Raycasts downward at each footstep to detect the surface, reads TerrainSoundData if available,
/// and falls back to the default sounds in CharacterEffects.
/// 
/// SETUP:
/// 1. Add this component to the character alongside MovementComponent and HealthComponent.
/// 2. Add Animation Events named "LeftFootstep" and "RightFootstep" to your walk/run clips
///    at the frames where each foot contacts the ground.
/// 3. Assign TerrainSoundMarker components to floor GameObjects for per-terrain audio.
/// </summary>
[RequireComponent(typeof(MovementComponent))]
public class FootstepHandler : MonoBehaviour
{
    [Header("Ground Detection")]
    [Tooltip("Layers considered as walkable ground for the footstep raycast.")]
    public LayerMask groundLayers = -1;

    [Tooltip("How far down to raycast to detect the surface underfoot.")]
    public float groundCheckDistance = 1.2f;

    [Tooltip("Origin offset upward from the transform pivot for the raycast (avoids starting inside the floor).")]
    public float raycastOriginHeight = 0.1f;

    [Header("Volume")]
    [Range(0f, 1f)]
    [Tooltip("Volume scale applied on top of the JSAM sound's own volume.")]
    public float walkVolumeScale = 0.8f;

    [Range(0f, 1f)]
    public float runVolumeScale = 1f;

    // -------------------------------------------------------------------------
    // Private state
    // -------------------------------------------------------------------------
    private MovementComponent _movement;
    private HealthComponent _health;

    private static readonly RaycastHit[] _hitBuffer = new RaycastHit[4];

    private void Awake()
    {
        _movement = GetComponent<MovementComponent>();
        _health = GetComponent<HealthComponent>();
    }

    // -------------------------------------------------------------------------
    // Animation Event Callbacks
    // These must be called by Animation Events on the walk/run animation clips.
    // -------------------------------------------------------------------------

    /// <summary>Called via Animation Event when the left foot contacts the ground.</summary>
    public void LeftFootstep() => PlayFootstep();

    /// <summary>Called via Animation Event when the right foot contacts the ground.</summary>
    public void RightFootstep() => PlayFootstep();

    // -------------------------------------------------------------------------
    // Core Logic
    // -------------------------------------------------------------------------

    private void PlayFootstep()
    {
        // Do not play footsteps while dead, in flight, or movement is immobilized
        if (_health != null && _health.IsDead) return;
        if (_movement.isInFlight) return;
        if (_movement.isImmobilized) return;

        bool isRunning = _movement.currentMoveDir.sqrMagnitude > 0.01f;

        SoundFileObject sound = GetFootstepSound(isRunning, out bool foundTerrain);

        if (sound == null) return;

        JSAM.AudioManager.PlaySound(sound);
    }

    /// <summary>
    /// Raycasts downward to find the surface. Priority order:
    ///   1. TerrainSoundMarker on the hit collider (covers mesh objects and trigger zone overrides)
    ///   2. TerrainLayerSoundMap on the hit Terrain (splatmap dominant-layer lookup)
    ///   3. CharacterEffects defaults
    /// </summary>
    private SoundFileObject GetFootstepSound(bool isRunning, out bool foundTerrain)
    {
        foundTerrain = false;

        Vector3 rayOrigin = transform.position + Vector3.up * raycastOriginHeight;
        int hitCount = Physics.RaycastNonAlloc(rayOrigin, Vector3.down, _hitBuffer, groundCheckDistance + raycastOriginHeight, groundLayers, QueryTriggerInteraction.Collide);

        TerrainSoundData terrainLayerData = null;

        for (int i = 0; i < hitCount; i++)
        {
            // Priority 1: explicit TerrainSoundMarker (mesh objects, trigger zone overrides)
            TerrainSoundMarker marker = _hitBuffer[i].collider.GetComponent<TerrainSoundMarker>();
            if (marker != null && marker.terrainSoundData != null)
            {
                foundTerrain = true;
                TerrainSoundData data = marker.terrainSoundData;
                return (isRunning && data.runFootstep != null) ? data.runFootstep : data.walkFootstep;
            }

            // Priority 2: TerrainLayerSoundMap on a Unity Terrain (splatmap dominant-layer lookup)
            if (terrainLayerData == null && _hitBuffer[i].collider is TerrainCollider)
            {
                TerrainLayerSoundMap map = _hitBuffer[i].collider.GetComponent<TerrainLayerSoundMap>();
                if (map != null)
                    terrainLayerData = map.GetDominantLayerSound(_hitBuffer[i].point);
            }
        }

        if (terrainLayerData != null)
        {
            foundTerrain = true;
            return (isRunning && terrainLayerData.runFootstep != null) ? terrainLayerData.runFootstep : terrainLayerData.walkFootstep;
        }

        // No terrain marker found — use CharacterEffects defaults
        if (_health != null && _health.characterEffects != null)
        {
            CharacterEffects fx = _health.characterEffects;
            if (isRunning && fx.sfxFootstepRun != null) return fx.sfxFootstepRun;
            if (fx.sfxFootstepWalk != null) return fx.sfxFootstepWalk;
        }

        return null;
    }
}
