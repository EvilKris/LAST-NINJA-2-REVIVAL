using UnityEngine;

[CreateAssetMenu(fileName = "NewMove", menuName = "Combat/Move")]
public class CombatMove : ScriptableObject
{
    public string animTrigger;      // The trigger in the Animator
    public float damage;            // Damage value
    public float staminaCost;       // For the Balance/Ki system
    public float attackRange;       // How close to be to use this

    public float recoveryTime; // Use this instead of totalDuration for AI "pause"
    //Enemy AI metadata only
    [Range(0, 1)]
    public float weight; // Omomi (重み) - AI priority slider
    public float idealRange; // How close the AI should be to pick this
}

