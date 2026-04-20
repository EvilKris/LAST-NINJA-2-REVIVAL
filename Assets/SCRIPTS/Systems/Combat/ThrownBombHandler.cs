using UnityEngine;

public class ThrownBombHandler : MonoBehaviour, IWeaponHandler
{
    private const string DRAW_CLIP_SLOT_KEY = "ReplaceableDrawWeapon";
    private const string DRAW_ANIM_STATE = "ReplaceableDrawWeapon";

    private const float THROW_FORCE = 4f;

    private CombatHandler _combat;
    private Animator _animator;
    private GameObject _weaponInstance;
    private TrailRenderer[] _weaponTrails;
    private ParticleSystem[] _weaponParticles;
    private Rigidbody _weaponRigidbody;
    private Collider[] _weaponColliders;
    private int _amount;

    public void Initialize(CombatHandler combat, int amount)
    {
        _combat = combat;
        _amount = amount;
        _animator = GetComponent<Animator>();

        SpawnWeapon();
        PlayDrawAnimation();

        _combat.OnHitboxOpened += OnHitboxOpened;
        _combat.OnHitboxClosed += OnHitboxClosed;
    }

    private void SpawnWeapon()
    {
        FightingStyle style = _combat.currentStyle;
        if (style == null || style.weaponPrefab == null) return;

        Transform bone = _animator.GetBoneTransform(style.weaponBone);
        if (bone == null) return;

        _weaponInstance = Instantiate(style.weaponPrefab, bone);
        _weaponInstance.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        _weaponInstance.SetActive(false);

        _weaponTrails = _weaponInstance.GetComponentsInChildren<TrailRenderer>(true);
        _weaponParticles = _weaponInstance.GetComponentsInChildren<ParticleSystem>(true);
        _weaponRigidbody = _weaponInstance.GetComponent<Rigidbody>();
        if (_weaponRigidbody != null) _weaponRigidbody.isKinematic = true;
        _weaponColliders = _weaponInstance.GetComponentsInChildren<Collider>(true);
        foreach (Collider col in _weaponColliders) col.enabled = false;
        SetTrailsEmitting(false);
    }

    public void PlayDrawAnimation()
    {
        FightingStyle style = _combat.currentStyle;
        if (style == null || style.drawWeaponClip == null) return;

        _combat.OverrideController[DRAW_CLIP_SLOT_KEY] = style.drawWeaponClip;
        _animator.CrossFade(DRAW_ANIM_STATE, 0.15f, 0, 0f);
        //_animator.Play(DRAW_ANIM_STATE, 0, 0f);
        _animator.Update(0f);
    }

    public void OnWeaponReveal()
    {
        if (_weaponInstance != null)
            _weaponInstance.SetActive(true);
    }

    /// <summary>
    /// Immediately destroys the weapon model. Called by <see cref="CombatHandler"/> before
    /// this component is destroyed so the prop vanishes without waiting for end-of-frame.
    /// </summary>
    public void Teardown()
    {
        if (_weaponInstance != null)
        {
            Destroy(_weaponInstance);
            _weaponInstance = null;
        }
    }

    private void Start()
    {
        ApplyWeaponOffset();
    }

    private void ApplyWeaponOffset()
    {
        if (_weaponInstance == null || _combat.currentStyle == null) return;

        FightingStyle style = _combat.currentStyle;
        _weaponInstance.transform.SetLocalPositionAndRotation(style.weaponPositionOffset, Quaternion.Euler(style.weaponRotationOffset));
    }
   

    private void OnHitboxOpened(CombatMove move)
    {
        if (!IsLightAttack(move)) return;
        ThrowProjectile();
    }

    private bool IsLightAttack(CombatMove move)
    {
        if (move == null || _combat.currentStyle == null || _combat.currentStyle.lightAttacks == null) return false;
        foreach (CombatMove light in _combat.currentStyle.lightAttacks)
        {
            if (light == move) return true;
        }
        return false;
    }

    private void OnHitboxClosed() => SetTrailsEmitting(false);

    private void ThrowProjectile()
    {
        if (_weaponInstance == null || _weaponRigidbody == null) return;

        SetTrailsEmitting(true);

        LayerMask floorLayer = _combat.GetComponent<HealthComponent>().floorLayer;

        ThrownProjectileBomb projectile = _weaponInstance.AddComponent<ThrownProjectileBomb>();
        projectile.Launch(transform.root, _weaponRigidbody, _weaponColliders, THROW_FORCE, floorLayer);

        _weaponInstance = null;
        _weaponTrails = null;
        _weaponParticles = null;
        _weaponRigidbody = null;
        _weaponColliders = null;

        _amount--;

        if (MasterSingleton.Instance != null)
            MasterSingleton.Instance.UIManager?.UpdateWeaponCounter(_amount);

        if (_amount > 0)
        {
            SpawnWeapon();
            OnWeaponReveal();
            ApplyWeaponOffset();
        }
        else
        {
            _combat.RevertToDefaultStyle();
        }
    }

    private void SetTrailsEmitting(bool emitting)
    {
        if (_weaponTrails != null)
        {
            foreach (TrailRenderer trail in _weaponTrails)
            {
                if (trail == null) continue;
                trail.emitting = emitting;
                if (!emitting) trail.Clear();
            }
        }

        if (_weaponParticles != null)
        {
            foreach (ParticleSystem ps in _weaponParticles)
            {
                if (ps == null) continue;
                if (emitting) ps.Play();
                else ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }
    }

    private void OnDestroy()
    {
        if (_combat != null)
        {
            _combat.OnHitboxOpened -= OnHitboxOpened;
            _combat.OnHitboxClosed -= OnHitboxClosed;
        }

        if (_weaponInstance != null)
            Destroy(_weaponInstance);
    }
}
