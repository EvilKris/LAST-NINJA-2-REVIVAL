/// <summary>
/// Shared contract for any move that can be tracked as the active attack in CombatHandler.
/// Implemented by CombatMove and ClinchAttack.
/// </summary>
public interface IActiveCombatMove
{
    float Damage { get; }
    HitReactionType ReactionToTrigger { get; }
    AnimationAudioEvent[] AudioEvents { get; }

    bool IsInHitWindow(float normalizedTime);
    bool IsInComboWindow(float normalizedTime);
}
