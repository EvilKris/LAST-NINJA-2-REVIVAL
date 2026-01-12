using UnityEngine;

/// <summary>
/// Defines the faction/team alignment for game entities.
/// Used for target selection, AI behavior, and combat interactions.
/// </summary>
public enum Faction
{
    Player,      // Player-controlled character (only one per game)
    Neutral,     // Non-hostile NPCs, civilians
    Enemy,       // Hostile enemies
    Companion,   // Friendly AI allies
    Environment  // Destructible objects, traps
}

/// <summary>
/// Interface for entities that can be targeted by the player's lock-on system or AI.
/// Implement this on any GameObject that should be selectable as a combat target
/// (enemies, breakable objects, NPCs, etc.).
/// </summary>
public interface ITargetable
{
    /// <summary>
    /// Returns the Transform that the camera/reticle should focus on when locked onto this target.
    /// Typically returns a child Transform positioned at the target's center or head.
    /// </summary>
    /// <returns>Transform representing the visual lock-on point (camera focus point)</returns>
    Transform GetLockOnPoint();

    /// <summary>
    /// Checks if this entity is currently a valid target for lock-on.
    /// Should return false if the target is dead, invulnerable, too far away, or behind cover.
    /// </summary>
    /// <returns>True if the entity can be targeted, false otherwise</returns>
    bool IsValidTarget();

    /// <summary>
    /// Returns the faction/team alignment of this entity.
    /// Used to determine friend-or-foe relationships and AI behavior.
    /// </summary>
    /// <returns>Faction enum value representing the entity's allegiance</returns>
    Faction GetFaction();
}