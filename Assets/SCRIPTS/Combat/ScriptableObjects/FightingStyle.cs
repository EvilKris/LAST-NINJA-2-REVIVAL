using UnityEngine;

[CreateAssetMenu(fileName = "NewStyle", menuName = "Combat/Style")]
public class FightingStyle : ScriptableObject
{
    public CombatMove[] lightAttacks; // 1-3 move combo chain
    public CombatMove heavyAttack;
    public CombatMove specialAttack;
    public RuntimeAnimatorController styleAnimator; // Swappable animators!
}