using UnityEngine;

[CreateAssetMenu(fileName = "NewStyle", menuName = "Combat/Style")]
public class FightingStyle : ScriptableObject
{
    public CombatMove[] lightAttacks; // 1-3 move combo chain
    public CombatMove mediumAttack;
    public CombatMove heavyAttack;
    public CombatMove specialAttack;

    [Header("Acrobatics")]
    public CombatMove acrobaticFlip;

    [Tooltip("IGNORE unless req for completely unique weapons! (bow/arrow etc) Defines the fighting style's unique animations. ")]
    public RuntimeAnimatorController styleAnimator; // Swappable animators!
}