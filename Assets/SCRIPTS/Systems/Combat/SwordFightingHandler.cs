using UnityEngine;

public class SwordFightingHandler : MonoBehaviour, IWeaponHandler
{
    private const string DRAW_CLIP_SLOT_KEY = "ReplaceableDrawWeapon";
    private const string DRAW_ANIM_STATE = "ReplaceableDrawWeapon";

    private CombatHandler _combat;
    private Animator _animator;
    private GameObject _weaponInstance;
    private TrailRenderer[] _weaponTrails;

    public void Initialize(CombatHandler combat)
    {
        _combat = combat;
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
        if (_weaponInstance == null || _combat.currentStyle == null) return;

        FightingStyle style = _combat.currentStyle;
        _weaponInstance.transform.SetLocalPositionAndRotation(style.weaponPositionOffset, Quaternion.Euler(style.weaponRotationOffset));
    }
    /*
    private void Update()
    {
        if (_weaponInstance == null || _combat.currentStyle == null) return;

        FightingStyle style = _combat.currentStyle;
        _weaponInstance.transform.localPosition = style.weaponPositionOffset;
        _weaponInstance.transform.localRotation = Quaternion.Euler(style.weaponRotationOffset);
    }*/

    private void OnHitboxOpened(CombatMove move) => SetTrailsEmitting(true);

    private void OnHitboxClosed() => SetTrailsEmitting(false);

    private void SetTrailsEmitting(bool emitting)
    {
        if (_weaponTrails == null) return;
        foreach (TrailRenderer trail in _weaponTrails)
        {
            if (trail == null) continue;
            trail.emitting = emitting;
            if (!emitting) trail.Clear();
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
