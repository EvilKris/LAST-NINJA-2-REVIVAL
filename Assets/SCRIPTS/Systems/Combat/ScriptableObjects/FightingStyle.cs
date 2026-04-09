using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewStyle", menuName = "Combat/Style")]
public class FightingStyle : ScriptableObject
{  

    [Header("Clinch Config - Close Range Grappling Attacks + Throws")] 
    public FightingStyleType styleType; // Determines available combat mechanics (e.g. clinching)

    public CombatMove[] lightAttacks; // 1-3 move combo chain
    public CombatMove mediumAttack;
    public CombatMove heavyAttack;
    public CombatMove specialAttack;

    [Header("Acrobatics")]
    public CombatMove acrobaticFlip;

    [Header("Charge Moves - More Powerful Attacks with Charge")]
    [Tooltip("Tier 1 is index 0, Tier 2 is index 1, etc.")]
    public List<CombatMove> chargedAttacks; // This defines your Max Charges!

    [Tooltip("Don't bother with these two if no Clinching")]
    public ClinchAttack clinchLightAtk;  // Light attack in clinch
    public CombatThrow clinchThrowDefault; // Throw performed if no direction is input during throw release 


    [Header("Ignore except for unique weapons")]
    [Tooltip("IGNORE unless req for completely unique weapons! (bow/arrow etc) Defines the fighting style's unique animations. ")]
    public RuntimeAnimatorController styleAnimator; // Swappable animators!

    [Header("Defensive")]
    [Tooltip("Block animation clip swapped into the ReplaceableBlock slot at runtime.")]
    public AnimationClip blockClip;

    [Header("Weapon Only - Ignore if melee")]
    [Tooltip("Specific draw weapon clip")]
    public AnimationClip drawWeaponClip;

    [Tooltip("3D weapon model instantiated on the entity's hand when this style is equipped.")]
    public GameObject weaponPrefab;

    [Tooltip("Which bone the weapon prefab is parented to.")]
    public HumanBodyBones weaponBone = HumanBodyBones.RightHand;

    [Tooltip("Local position offset applied to the weapon relative to the bone.")]
    public Vector3 weaponPositionOffset = Vector3.zero;

    [Tooltip("Local rotation offset applied to the weapon relative to the bone (Euler angles).")]
    public Vector3 weaponRotationOffset = Vector3.zero;

}