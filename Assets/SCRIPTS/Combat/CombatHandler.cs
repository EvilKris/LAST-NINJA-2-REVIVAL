using UnityEngine;

public class CombatHandler : MonoBehaviour
{
    [Header("Data")]
    public FightingStyle currentStyle;
    public CombatHitbox weaponHitbox;

    private BaseCreature _baseCreature;
    private Animator _animator;

    private int _comboIndex = 0;
    private float _lastAttackTime;
    private const float COMBO_RESET_TIME = 1.0f;

    private void Awake()
    {
        _baseCreature = GetComponent<BaseCreature>();
        _animator = GetComponent<Animator>();
    }

    // This is the "Universal Attack" command (Kyōtsū Kōgeki - 共通攻撃)
    public void ExecuteLightAttack()
    {
        if (!_baseCreature.isAbleToMove || IsDead()) return;

        // Combo Reset Logic
        if (Time.time - _lastAttackTime > COMBO_RESET_TIME)
        {
            _comboIndex = 0;
        }

        CombatMove move = currentStyle.lightAttacks[_comboIndex];

        // 1. Prepare the hitbox
        weaponHitbox.SetDamage(move.damage);

        // 2. Play animation
        _animator.SetTrigger(move.animTrigger);

        // 3. Advance state
        _lastAttackTime = Time.time;
        _comboIndex = (_comboIndex + 1) % currentStyle.lightAttacks.Length;
    }

    private bool IsDead() => _baseCreature is IDamageable d && d.IsDead;
}