using UnityEngine;

public class MeleeController : MonoBehaviour
{
    public FightingStyle currentStyle;
    private int comboIndex = 0;
    private float lastAttackTime;

    public void PerformAttack(bool isHeavy = false)
    {
        CombatMove move = isHeavy ? currentStyle.heavyAttack : currentStyle.lightAttacks[comboIndex];

        // Play the animation
        GetComponent<Animator>().SetTrigger(move.animTrigger);

        // Logic for combo chaining
        comboIndex = (comboIndex + 1) % currentStyle.lightAttacks.Length;
        lastAttackTime = Time.time;
    }
}