using UnityEngine;

/// <summary>
/// ScriptableObject that defines the default game state values.
/// Assign this asset to GameDataManager. It is never mutated at runtime —
/// it serves purely as a reset template (e.g. on game over or new game).
/// </summary>
[CreateAssetMenu(fileName = "NewGameData", menuName = "Game/Game Data")]
public class GameDataSO : ScriptableObject
{
    [Header("--- PLAYER ---")]
    public int startingLives = 3;
    public int startingScore = 0;

    [Header("--- PROGRESSION ---")]
    public int startingLevel = 1;
    public int enemiesDefeatedStart = 0;

    [Header("--- SETTINGS ---")]
    public bool musicEnabledDefault = true;
    public bool pauseAllowedDefault = true;
}
