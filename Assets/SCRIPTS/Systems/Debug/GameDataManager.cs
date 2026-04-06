using UnityEngine;

/// <summary>
/// Manages all persistent runtime game state: score, lives, level progression, and settings.
/// Reads default values from a <see cref="GameDataSO"/> ScriptableObject assigned in the Inspector,
/// falling back to hardcoded values if none is assigned.
/// Exposes events so other systems (e.g. UIManager) can react to state changes without polling.
/// </summary>
public class GameDataManager : MonoBehaviour
{
    [Header("--- DEFAULT TEMPLATE ---")]
    [Tooltip("ScriptableObject that defines the starting values for a new game. If unassigned, hardcoded fallbacks are used.")]
    [SerializeField] private GameDataSO defaultData;

    [Header("--- RUNTIME STATE ---")]
    [Tooltip("Player's current score. Incremented by combat and collectibles.")]
    public int Score;
    [Tooltip("The level the player is currently on.")]
    public int CurrentLevel;
    [Tooltip("Total enemies defeated this session.")]
    public int EnemiesDefeated;
    [Tooltip("Whether background music is enabled.")]
    public bool MusicEnabled;
    [Tooltip("Whether the pause menu can be opened. Disabled during cutscenes and the main menu.")]
    public bool IsPauseAllowed;

    /// <summary>Fired whenever <see cref="Lives"/> changes. Passes the new live count.</summary>
    public event System.Action<int> OnLivesChanged;

    private int _lives;

    /// <summary>
    /// Current number of lives remaining. Setting this fires <see cref="OnLivesChanged"/>.
    /// Use <see cref="LoseLife"/> to decrement safely rather than setting this directly.
    /// </summary>
    public int Lives
    {
        get => _lives;
        private set
        {
            _lives = value;
            OnLivesChanged?.Invoke(_lives);
        }
    }

    private void Start()
    {
        ResetToDefaults();
    }

    /// <summary>
    /// Decrements the player's life count by one, clamped at zero, and fires <see cref="OnLivesChanged"/>.
    /// Called by the drowning system and any other death source.
    /// </summary>
    public void LoseLife()
    {
        Lives = Mathf.Max(0, Lives - 1);
    }

    /// <summary>
    /// Restores lives to an exact value. Used by <see cref="GameManager"/> when
    /// applying a <see cref="CheckpointSnapshot"/>.
    /// </summary>
    public void SetLives(int value)
    {
        Lives = Mathf.Max(0, value);
    }

    /// <summary>
    /// Resets all runtime state back to the values defined in the <see cref="GameDataSO"/> template.
    /// Also re-enables the X-Ray renderer features so they are on by default at game start.
    /// Call this on game over or when starting a new game.
    /// </summary>
    public void ResetToDefaults()
    {
        if (defaultData == null)
        {
            // No SO assigned — warn and apply safe hardcoded values so the game still runs
            Debug.LogWarning("GameDataManager: No GameDataSO assigned — using hardcoded fallbacks.");
            Lives = 3;
            Score = 0;
            CurrentLevel = 1;
            EnemiesDefeated = 0;
            MusicEnabled = true;
            IsPauseAllowed = true;

            // Guard: MasterSingleton / PlayerManager may not be ready on the very first Start() frame
            if (MasterSingleton.Instance != null && MasterSingleton.Instance.PlayerManager != null)
            {
                MasterSingleton.Instance.PlayerManager.ToggleXrayRendererFeatures(true);
            }
            return;
        }

        // Apply values from the ScriptableObject template
        Lives = defaultData.startingLives;
        Score = defaultData.startingScore;
        CurrentLevel = defaultData.startingLevel;
        EnemiesDefeated = defaultData.enemiesDefeatedStart;
        MusicEnabled = defaultData.musicEnabledDefault;
        IsPauseAllowed = defaultData.pauseAllowedDefault;

        // Guard: MasterSingleton / PlayerManager may not be ready on the very first Start() frame
        if (MasterSingleton.Instance != null && MasterSingleton.Instance.PlayerManager != null)
        {
            MasterSingleton.Instance.PlayerManager.ToggleXrayRendererFeatures(true);
        }
    }
}

