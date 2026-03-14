using System.Collections.Generic;
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

    [Header("Charge Moves - More Powerful Attacks with Charge")]
    [Tooltip("Tier 1 is index 0, Tier 2 is index 1, etc.")]
    public List<CombatMove> chargedAttacks; // This defines your Max Charges!

    [Header("Clinch Config - Close Range Grappling Attacks + Throws")] 
    public bool supportsClinching; // The toggle for your logic
    [Tooltip("Don't bother with these two if no Clinching")]
    public ClinchAttack clinchLightAtk;  // Light attack in clinch
    public CombatThrow clinchThrowDefault; // Throw performed if no direction is input during throw release 


    [Header("Ignore except for unique weapons")]
    [Tooltip("IGNORE unless req for completely unique weapons! (bow/arrow etc) Defines the fighting style's unique animations. ")]
    public RuntimeAnimatorController styleAnimator; // Swappable animators!

    [Header("Defensive")]
    [Tooltip("Block animation clip swapped into the ReplaceableBlock slot at runtime.")]
    public AnimationClip blockClip;
}