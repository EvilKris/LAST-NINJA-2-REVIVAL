using UnityEngine;

public class GameDataManager : MonoBehaviour
{
    [Header("--- DEFAULT TEMPLATE ---")]
    [SerializeField] private GameDataSO defaultData;

    [Header("--- RUNTIME STATE ---")]
    public int Score;
    public int CurrentLevel;
    public int EnemiesDefeated;
    public bool MusicEnabled;
    public bool IsPauseAllowed;

    /// <summary>Fired whenever <see cref="Lives"/> changes. Passes the new live count.</summary>
    public event System.Action<int> OnLivesChanged;

    private int _lives;
    public int Lives
    {
        get => _lives;
        private set
        {
            _lives = value;
            OnLivesChanged?.Invoke(_lives);
        }
    }

    void Start()
    {
        ResetToDefaults();
    }

    /// <summary>
    /// Decrements the player's life count by one and fires <see cref="OnLivesChanged"/>.
    /// </summary>
    public void LoseLife()
    {
        Lives = Mathf.Max(0, Lives - 1);
    }

    /// <summary>
    /// Resets all runtime state back to the values defined in the GameDataSO template.
    /// Call this on game over or new game start.
    /// </summary>
    public void ResetToDefaults()
    {
        if (defaultData == null)
        {
            Debug.LogWarning("GameDataManager: No GameDataSO assigned — using hardcoded fallbacks.");
            Lives = 3;
            Score = 0;
            CurrentLevel = 1;
            EnemiesDefeated = 0;
            MusicEnabled = true;
            IsPauseAllowed = true;
            return;
        }

        Lives = defaultData.startingLives;
        Score = defaultData.startingScore;
        CurrentLevel = defaultData.startingLevel;
        EnemiesDefeated = defaultData.enemiesDefeatedStart;
        MusicEnabled = defaultData.musicEnabledDefault;
        IsPauseAllowed = defaultData.pauseAllowedDefault;
    }
}

