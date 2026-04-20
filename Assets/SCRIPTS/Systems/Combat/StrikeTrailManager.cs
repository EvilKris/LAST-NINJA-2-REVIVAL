using UnityEngine;

/// <summary>
/// Manages per-limb strike trail VFX for melee combat.
/// Spawns <see cref="TrailRenderer"/> instances on humanoid bones at initialisation
/// and exposes simple enable/disable helpers consumed by <see cref="CombatHandler"/>.
/// </summary>
public class StrikeTrailManager
{
    private readonly TrailRenderer _trailRightHand;
    private readonly TrailRenderer _trailLeftHand;
    private readonly TrailRenderer _trailRightFoot;
    private readonly TrailRenderer _trailLeftFoot;

    private VFXLimb _activeTrailLimb = VFXLimb.None;

    public StrikeTrailManager(Animator animator, HealthComponent health)
    {
        if (health.characterEffects == null || health.characterEffects.strikeTrailMelee == null)
            return;

        _trailRightHand = SpawnTrailOnBone(animator, health, HumanBodyBones.RightHand);
        _trailLeftHand  = SpawnTrailOnBone(animator, health, HumanBodyBones.LeftHand);
        _trailRightFoot = SpawnTrailOnBone(animator, health, HumanBodyBones.RightFoot);
        _trailLeftFoot  = SpawnTrailOnBone(animator, health, HumanBodyBones.LeftFoot);

        DisableAll();
    }

    public void EnableForLimb(VFXLimb limb)
    {
        _activeTrailLimb = limb;
        SetEmitter(limb, true);
    }

    public void DisableAll()
    {
        SetEmitter(VFXLimb.RightHand, false);
        SetEmitter(VFXLimb.LeftHand,  false);
        SetEmitter(VFXLimb.RightFoot, false);
        SetEmitter(VFXLimb.LeftFoot,  false);
        _activeTrailLimb = VFXLimb.None;
    }

    private void SetEmitter(VFXLimb limb, bool enabled)
    {
        TrailRenderer target = limb switch
        {
            VFXLimb.RightHand => _trailRightHand,
            VFXLimb.LeftHand  => _trailLeftHand,
            VFXLimb.RightFoot => _trailRightFoot,
            VFXLimb.LeftFoot  => _trailLeftFoot,
            _                 => null
        };

        if (target == null) return;

        target.emitting = enabled;
        if (!enabled) target.Clear();
    }

    private static TrailRenderer SpawnTrailOnBone(Animator animator, HealthComponent health, HumanBodyBones bone)
    {
        Transform boneTransform = animator.GetBoneTransform(bone);
        if (boneTransform == null) return null;

        GameObject instance = Object.Instantiate(
            health.characterEffects.strikeTrailMelee,
            boneTransform);

        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;

        if (!instance.TryGetComponent<TrailRenderer>(out var trail))
            trail = instance.GetComponentInChildren<TrailRenderer>();

        if (trail != null)
        {
            trail.emitting = false;
            trail.Clear();
        }

        return trail;
    }
}
