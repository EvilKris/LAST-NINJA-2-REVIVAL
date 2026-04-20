using System;
using UnityEngine;

/// <summary>
/// Manages the charge/tier system (Spike Out style consecutive special moves).
/// Extracted from <see cref="CombatHandler"/> to reduce its responsibility count.
/// </summary>
public class ChargeHandler
{
    private readonly CombatHandler _combat;

    private float _currentChargeTimer;
    private bool _isCharging;
    private int _cachedMaxCharges = -1;
    private int _cachedCurrentTier = -1;
    private float _cachedChargeProgress = -1f;
    private int _lastPlayedTierSfx = -1;

    public event Action<int> OnMaxChargesChanged;
    public event Action<int, float> OnChargeStateChanged;

    public bool IsCharging => _isCharging;
    public int MaxCharges => _combat.currentStyle != null && _combat.currentStyle.chargedAttacks != null
        ? _combat.currentStyle.chargedAttacks.Count : 0;
    public int CurrentTier => Mathf.FloorToInt(_currentChargeTimer);
    public float ChargeProgress => _currentChargeTimer % 1.0f;

    public ChargeHandler(CombatHandler combat)
    {
        _combat = combat;
    }

    /// <summary>
    /// Call from <see cref="CombatHandler.Update"/> every frame to tick timers and fire events.
    /// </summary>
    public void Tick()
    {
        int maxCharges = MaxCharges;
        if (maxCharges != _cachedMaxCharges)
        {
            _cachedMaxCharges = maxCharges;
            OnMaxChargesChanged?.Invoke(maxCharges);
        }

        if (_isCharging)
        {
            _currentChargeTimer = Mathf.Min(_currentChargeTimer + Time.deltaTime, MaxCharges);

            int currentTier = CurrentTier;
            float chargeProgress = ChargeProgress;
            if (currentTier != _cachedCurrentTier || Mathf.Abs(chargeProgress - _cachedChargeProgress) > 0.01f)
            {
                _cachedCurrentTier    = currentTier;
                _cachedChargeProgress = chargeProgress;
                OnChargeStateChanged?.Invoke(currentTier, chargeProgress);
            }

            if (currentTier > 0 && currentTier != _lastPlayedTierSfx)
            {
                _lastPlayedTierSfx = currentTier;
                JSAM.AudioManager.PlaySound(MasterSingleton.Instance.PrefabBankManager.Charge_Drive_Strike_Tier_Complete);
            }
        }
    }

    public void StartCharging()
    {
        _isCharging          = true;
        _currentChargeTimer  = 0f;
        _cachedCurrentTier   = 0;
        _cachedChargeProgress = 0f;
        _lastPlayedTierSfx   = -1;
        OnChargeStateChanged?.Invoke(0, 0f);
    }

    /// <summary>
    /// Releases the current charge. Returns the tier that was reached so the caller
    /// can decide which attack to execute.
    /// </summary>
    public int Release()
    {
        _isCharging = false;
        int tier = CurrentTier;

        _currentChargeTimer   = 0f;
        _cachedCurrentTier    = 0;
        _cachedChargeProgress = 0f;
        _lastPlayedTierSfx    = -1;
        OnChargeStateChanged?.Invoke(0, 0f);

        return tier;
    }

    /// <summary>
    /// Resets the charge state without returning a tier (e.g. clinch interrupt).
    /// </summary>
    public void Cancel()
    {
        _isCharging = false;
        _currentChargeTimer = 0f;
        _cachedCurrentTier  = 0;
        _cachedChargeProgress = 0f;
        _lastPlayedTierSfx = -1;
        OnChargeStateChanged?.Invoke(0, 0f);
    }
}
