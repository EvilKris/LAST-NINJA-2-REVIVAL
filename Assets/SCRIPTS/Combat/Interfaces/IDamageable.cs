public interface IDamageable
{
    // The core method to handle damage
    void TakeDamage(float amount, HitReactionType type);

    // Optional: Useful for UI or AI to check if they should stop attacking
    bool IsDead { get; }
}