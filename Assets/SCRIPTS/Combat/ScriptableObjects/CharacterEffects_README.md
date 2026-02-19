# CharacterEffects ScriptableObject

## Overview
The `CharacterEffects` ScriptableObject is a comprehensive system for managing all visual (VFX) and audio (SFX) effects for characters in your fighting game. It replaces and extends the previous `CharacterAudioSO` system with a more complete solution.

## Features

### Audio Effects (SFX)
- **Damage Reactions**: Light, Medium, Heavy, and Critical hit sounds
- **Blocking**: Block impacts, perfect blocks, and guard breaks
- **Knockdown & Recovery**: Body impacts, getting up, being thrown
- **Death**: Death cries and body collapse sounds
- **Movement**: Jump, land, dash, and breathing sounds
- **Special States**: Power-ups, stun effects, healing

### Visual Effects (VFX)
- **Hit Effects**: Particle systems for different damage levels
- **Block Effects**: Sparks and flashes for blocking
- **Status Effects**: Stun stars, power-up auras, healing sparkles, low health warnings
- **Movement Effects**: Dust clouds, dash trails, ground impact ripples
- **Death Effects**: Death explosions and optional soul release effects

### Smart Helper Methods
The SO includes built-in helper methods that automatically select the appropriate effect based on damage severity:

#### `PlayHitSound(float damage, float maxHealth)`
Automatically plays the right hit sound based on damage percentage:
- **30%+ damage** ? Critical hit sound
- **15%-30% damage** ? Heavy hit sound  
- **5%-15% damage** ? Medium hit sound
- **< 5% damage** ? Light hit sound

#### `SpawnHitEffect(float damage, float maxHealth, Vector3 position, Vector3 direction)`
Spawns VFX matching the damage severity with proper positioning and orientation.

#### `PlayDeathEffects(Transform characterTransform)`
Plays both death audio and VFX at once (death cry, body collapse, death explosion, soul release).

#### `PlayBlockEffects(bool isPerfectBlock, Vector3 position, Vector3 normal)`
Handles block audio/visual feedback with special handling for perfect blocks.

## Setup Instructions

### 1. Create a CharacterEffects Asset

1. Right-click in your Project window
2. Navigate to **Create ? Combat ? Character Effects**
3. Name it appropriately (e.g., "PlayerEffects", "EnemyNinjaEffects")

### 2. Assign Effects

Open your newly created asset and populate the fields:

#### Audio (SoundFileObjects from JSAM)
- Drag your JSAM sound files into the appropriate SFX slots
- Use more intense sounds for heavier reactions

#### Visual (Prefabs)
- Assign particle system prefabs to VFX slots
- Ensure prefabs either auto-destroy or use the `defaultVfxLifetime` setting
- Use appropriate scales for different effect intensities

#### Configuration
- **vfxHeightOffset**: Default vertical spawn offset (usually around character center height)
- **hitEffectForwardOffset**: Pushes hit effects forward so they appear at contact point
- **defaultVfxLifetime**: Fallback duration for effects without self-destruct

### 3. Attach to HealthComponent

1. Select your character GameObject
2. Find the `HealthComponent` in the Inspector
3. Locate the **Effects** section
4. Drag your `CharacterEffects` asset into the `Character Effects` field

## Integration with Existing Systems

### HealthComponent Integration
The `HealthComponent` now checks for `CharacterEffects` first:
- If assigned ? Uses the new smart system (automatic severity detection)
- If not assigned ? Falls back to legacy `CharacterAudioSO` system

This means you can upgrade characters gradually without breaking existing functionality.

### Damage Example
```csharp
// When TakeDamage is called on HealthComponent:
public void TakeDamage(float damage, HitReactionType type)
{
    // ... damage calculation ...
    
    if (characterEffects != null)
    {
        // New system: Automatically picks right sound/VFX for damage amount
        characterEffects.PlayHitSound(damage, maxHealth);
        
        Vector3 hitPos = transform.position + transform.forward * characterEffects.hitEffectForwardOffset;
        characterEffects.SpawnHitEffect(damage, maxHealth, hitPos, -transform.forward);
    }
    else
    {
        // Fallback to legacy system
        // ... characterAudioSO code ...
    }
}
```

### Death Example
```csharp
private void HandleDeath()
{
    // Play death effects using CharacterEffects SO
    if (characterEffects != null)
    {
        characterEffects.PlayDeathEffects(transform);
    }
    
    // ... rest of death logic ...
}
```

## Extending the System

### Adding New Effect Categories

To add new effect types:

1. **Add fields to CharacterEffects.cs**:
```csharp
[Header("??? YOUR NEW CATEGORY ???")]
public SoundFileObject sfxNewSound;
public GameObject vfxNewEffect;
```

2. **Add helper method**:
```csharp
public void PlayNewEffect(Transform target)
{
    if (sfxNewSound != null)
        JSAM.AudioManager.PlaySound(sfxNewSound);
    
    if (vfxNewEffect != null)
    {
        GameObject instance = Instantiate(vfxNewEffect, target.position, Quaternion.identity);
        Destroy(instance, defaultVfxLifetime);
    }
}
```

3. **Call from appropriate system**:
```csharp
// In CombatHandler, HealthComponent, etc.
if (characterEffects != null)
{
    characterEffects.PlayNewEffect(transform);
}
```

## Best Practices

### Audio
- Use **shorter sounds for light hits** (quick grunts)
- **Longer, more intense sounds for heavy hits**
- Ensure sounds don't overlap badly when rapid hits occur
- Consider using JSAM's priority system for important sounds

### Visual Effects
- **Particle systems should be optimized** (limited particle count)
- Use **LOD systems** for distant effects
- Consider pooling frequently spawned effects
- Match VFX scale to character size
- Use additive blending for impact sparks

### Performance
- Keep `defaultVfxLifetime` as short as possible
- Use particle system auto-destroy when available
- Avoid spawning too many simultaneous effects
- Consider effect pooling for frequently used VFX

## Migration from CharacterAudioSO

If you have existing characters using `CharacterAudioSO`:

1. Keep the old `characterAudioSO` field populated (fallback support)
2. Create a new `CharacterEffects` asset for that character
3. Copy relevant sounds from AudioSO to CharacterEffects
4. Add VFX prefabs to the new asset
5. Assign CharacterEffects to the character
6. Test to ensure both systems work
7. Once confirmed working, you can remove the old AudioSO reference

The system is designed for **gradual migration** - both systems can coexist!

## Example Asset Structure

```
Assets/
??? ScriptableObjects/
?   ??? Effects/
?   ?   ??? PlayerEffects.asset
?   ?   ??? EnemyNinjaEffects.asset
?   ?   ??? BossEffects.asset
?   ?   ??? GenericEnemyEffects.asset
?   ??? ...
??? VFX/
?   ??? Prefabs/
?   ?   ??? HitSparks_Light.prefab
?   ?   ??? HitSparks_Heavy.prefab
?   ?   ??? BlockFlash.prefab
?   ?   ??? DeathExplosion.prefab
?   ??? ...
??? ...
```

## Troubleshooting

### Effects Not Playing
- Check if `CharacterEffects` is assigned to HealthComponent
- Verify sound/VFX prefabs are assigned in the asset
- Check JSAM AudioManager is in the scene
- Ensure particle systems have "Play On Awake" enabled

### Effects Spawning at Wrong Position
- Adjust `vfxHeightOffset` to match character center
- Adjust `hitEffectForwardOffset` for impact point accuracy
- Check if character pivot is at feet or center

### Performance Issues
- Reduce `defaultVfxLifetime`
- Simplify particle systems (fewer particles, simpler materials)
- Use particle system pooling
- Disable distant effects using LOD

## Future Enhancements

Potential additions to consider:
- Effect pooling system integration
- Layer-based effect intensity (cosmetic settings)
- Camera shake triggers on heavy impacts
- Screen flash/post-processing effects for critical hits
- Combo counter VFX for multi-hit combos
- Team-colored effects (for multiplayer)
