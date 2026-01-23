using UnityEngine;
using JSAM;

/// <summary>
/// ScriptableObject that maps HitReactionTypes to JSAM sound effects.
/// Used by HealthComponent to play appropriate hit sounds when damage is taken.
/// </summary>
[CreateAssetMenu(fileName = "CharacterAudioSO", menuName = "Combat/Character Audio Scriptable Object")]
public class CharacterAudioSO : ScriptableObject
{
    [Header("Impact Reaction Sound Effects")]
    [Tooltip("Sound played for light hits")]
    public SoundFileObject lightImpactSFX;
    [Tooltip("Sound played for medium hits")]
    public SoundFileObject medImpactSFX;
    [Tooltip("Sound played for heavy hits that stagger the character backward")]
    public SoundFileObject heavyStaggerImpactSFX;
    [Tooltip("Sound played when the character is knocked down to the ground")]
    public SoundFileObject knockdownImpactSFX;
    [Tooltip("Sound played when the character is launched into the air")]
    public SoundFileObject launchImpactSFX;

    [Header("Pain Vocals/Grunts Sound Effects/Death")]
    [Tooltip("Sound played for light pain vocals/grunts - played randomly not every instance")]
    public SoundFileObject lightPainVocalSFX;
    [Tooltip("Sound played for heavy pain vocals/grunts - always played")]
    public SoundFileObject heavyPainVocalSFX;
    [Tooltip("Sound played for death")]
    public SoundFileObject deathVocalSFX;

    [Header("Talking")]
    [Tooltip("Sound played for character talking/dialogue")]    
    public SoundFileObject vocalSFX;  


    /// <summary>
    /// Returns the appropriate sound effect for the given HitReactionType.
    /// Returns null if no sound is assigned for that type.
    /// </summary>
    /// <param name="reactionType">The type of hit reaction</param>
    /// <returns>The corresponding SoundFileObject, or null if not assigned</returns>
    public SoundFileObject GetSoundForReaction(HitReactionType reactionType)
    {
        switch (reactionType)
        {
            case HitReactionType.Light_High:
                return lightImpactSFX;
            case HitReactionType.Light_Low:
                return lightImpactSFX;
            case HitReactionType.Heavy_Back:
                return heavyStaggerImpactSFX;
            case HitReactionType.Knockdown:
                return knockdownImpactSFX;
            case HitReactionType.Launch:
                return launchImpactSFX;
            case HitReactionType.None:
            default:
                return null;
        }
    }

    /// <summary>
    /// Returns the light pain vocal sound effect randomly (1 in 5 chance) for light hit reactions.
    /// Returns null if the random check fails or if it's not a light hit reaction.
    /// </summary>
    /// <param name="reactionType">The type of hit reaction</param>
    /// <returns>The light pain vocal SoundFileObject, or null</returns>
    public SoundFileObject GetRandomPainVocal(HitReactionType reactionType)
    {
        if (reactionType == HitReactionType.Light_High || reactionType == HitReactionType.Light_Low)
        {
            if (Random.Range(1, 6) == 1)
            {
                return lightPainVocalSFX;
            }
        }
        return null;
    }
}
