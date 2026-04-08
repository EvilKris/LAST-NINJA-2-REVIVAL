using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using JSAM;


/// <summary>
/// Manages all persistent runtime game state: score, lives, level progression, and settings.
/// Also owns the full player-death sequence: life deduction, fade-to-black, checkpoint
/// restore, and game-over loading.
/// Reads default values from a <see cref="GameDataSO"/> ScriptableObject assigned in the
/// Inspector, falling back to hardcoded values if none is assigned.
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

    [Header("--- GAME OVER ---")]
    [Tooltip("Name of the main menu scene to load when all lives are lost.")]
    [SerializeField] private string mainMenuScene = "1-Menu-Scene";

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

    // ═══════════════════════════════════════════════════════════════════
    // Checkpoint
    // ═══════════════════════════════════════════════════════════════════

    // The last checkpoint the player activated. Null until the player crosses one.
    private Checkpoint _activeCheckpoint;

    /// <summary>True once the player has crossed at least one checkpoint this session.</summary>
    public bool HasCheckpoint
    {
        get
        {
            return _activeCheckpoint != null && _activeCheckpoint.IsActivated;
        }
    }

    /// <summary>
    /// Called by <see cref="Checkpoint"/> when the player crosses a checkpoint.
    /// Replaces the previously stored checkpoint.
    /// </summary>
    public void RegisterCheckpoint(Checkpoint checkpoint)
    {
        _activeCheckpoint = checkpoint;
    }

    // ═══════════════════════════════════════════════════════════════════
    // Lifecycle
    // ═══════════════════════════════════════════════════════════════════

    private void Start()
    {
        ResetToDefaults();
    }

    // ═══════════════════════════════════════════════════════════════════
    // Lives
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Decrements the player's life count by one, clamped at zero, and fires <see cref="OnLivesChanged"/>.
    /// </summary>
    public void LoseLife()
    {
        Lives = Mathf.Max(0, Lives - 1);
    }

    /// <summary>
    /// Restores lives to an exact value. Used when applying a <see cref="CheckpointSnapshot"/>.
    /// </summary>
    public void SetLives(int value)
    {
        Lives = Mathf.Max(0, value);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Death Sequence
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Begins the full player-death sequence.
    /// Deducts a life, shows a fade-to-black, then either restores the player to the
    /// last activated <see cref="Checkpoint"/> or loads the main menu if no lives remain.
    /// </summary>
    public void HandlePlayerDeath(MovementComponent movementComponent)
    {
        StartCoroutine(DeathSequence(movementComponent));
    }

    private IEnumerator DeathSequence(MovementComponent movementComponent)
    {
        LoseLife();

        if (Lives <= 0)
        {
            // Game over — fade to black then load the main menu
            bool fadeDone = false;
            SpawnFadeCanvas(() => fadeDone = true);
            yield return new WaitUntil(() => fadeDone);

            ResetToDefaults();
            MasterSingleton.Instance.SceneLoader.LoadSceneWithLoadingScreen(mainMenuScene);
            yield break;
        }

        // Still have lives — fade to black, restore checkpoint, fade back in
        bool respawnReady = false;
        SpawnFadeCanvas(() => respawnReady = true);

        yield return new WaitUntil(() => respawnReady);

        RespawnPlayer(movementComponent);
    }

    private void RespawnPlayer(MovementComponent movementComponent)
    {
        if (movementComponent == null) return;

        Rigidbody rb = movementComponent.GetComponent<Rigidbody>();
        HealthComponent health = movementComponent.GetComponent<HealthComponent>();
        InventoryManager inv = MasterSingleton.Instance.InventoryManager;

        if (HasCheckpoint)
        {
            CheckpointSnapshot snap = _activeCheckpoint.Snapshot;

            if (rb != null)
            {
                rb.position = snap.position;
                rb.rotation = snap.rotation;
                rb.linearVelocity = Vector3.zero;
            }
            movementComponent.transform.SetPositionAndRotation(snap.position, snap.rotation);

            if (health != null)
            {
                health.SetHealth(snap.health);
            }

            if (inv != null)
            {
                inv.ownedWeapons = new List<ItemData>(snap.weapons);
                inv.ownedItems   = new List<ItemData>(snap.items);
                inv.currentWeaponIndex = snap.weaponIndex;
                inv.currentItemIndex   = snap.itemIndex;
            }
        }
        else
        {
            // No checkpoint yet — fall back to nearest RespawnPoint for position only
            RespawnPoint fallback = RespawnPoint.FindNearest(movementComponent.transform.position);
            if (fallback != null)
            {
                if (rb != null) { rb.position = fallback.transform.position; rb.linearVelocity = Vector3.zero; }
                movementComponent.transform.position = fallback.transform.position;
            }
            else
            {
                Debug.LogWarning("GameDataManager.RespawnPlayer: No checkpoint or RespawnPoint found. Player stays in place.");
            }

            if (health != null)
                health.Revive();
        }

        if (rb != null) { rb.useGravity = true; rb.linearVelocity = Vector3.zero; }
        movementComponent.SetEntityCollidersActive(true);
        movementComponent.RestoreMovement();

        // Re-enable X-Ray features and play the materialize effect so the respawn
        // is visually consistent with the scene-start intro.
        MasterSingleton.Instance.PlayerManager.ToggleXrayRendererFeatures(true);
        PlayMaterializeIntro();
    }

    private void SpawnFadeCanvas(System.Action onBlackCallback)
    {
        // Instantiate inactive so Awake runs but Start (and the coroutine) is deferred
        // until after OnFadeComplete is assigned, preventing any race on the callback.
        GameObject prefab = MasterSingleton.Instance.UIManager.ScreenFadePrefab;
        if (prefab == null)
        {
            Debug.LogWarning("GameDataManager.SpawnFadeCanvas: ScreenFadePrefab is not assigned in UIManager.");
            onBlackCallback?.Invoke();
            return;
        }

        GameObject go = Instantiate(prefab);
        go.SetActive(false);
        DeathFadeCanvas fade = go.GetComponent<DeathFadeCanvas>();
        fade.OnFadeComplete = onBlackCallback;
        go.SetActive(true);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Materialize Intro
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Swaps all materials on the renderers of the GameObject found with <paramref name="tag"/>
    /// with per-instance copies of <see cref="PrefabBankManager.MaterializeMat"/>,
    /// tweens <c>_Progress</c> from 0 → 1 over <paramref name="duration"/> seconds,
    /// then restores the original materials via <see cref="PlayerManager.RestoreSharedMaterials"/>.
    /// </summary>
    /// <param name="tag">The Unity tag used to locate the target GameObject (e.g. "Player").</param>
    /// <param name="duration">Tween duration in seconds.</param>
    public void PlayMaterializeIntro(string tag = "Player", float duration = 2f)
    {
        Material sourceMat = MasterSingleton.Instance.PrefabBankManager.MaterializeMat;
        if (sourceMat == null)
        {
            Debug.LogWarning("GameDataManager.PlayMaterializeIntro: MaterializeMat is not assigned in PrefabBankManager.");
            return;
        }

        GameObject target = GameObject.FindWithTag(tag);
        if (target == null)
        {
            Debug.LogWarning($"GameDataManager.PlayMaterializeIntro: No GameObject with tag '{tag}' found.");
            return;
        }

        PlayerManager pm = MasterSingleton.Instance.PlayerManager;

        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        var instances = new List<Material>();

        foreach (Renderer r in renderers)
        {
            int slotCount = r.sharedMaterials.Length;
            Material[] swapped = new Material[slotCount];
            for (int i = 0; i < slotCount; i++)
            {
                Material inst = new Material(sourceMat);
                Bounds b = r.bounds;
                inst.SetFloat("_GroundLevel", b.min.y);
                inst.SetFloat("_Height",      Mathf.Max(b.size.y + 5f, 0.01f));
                inst.SetFloat("_Progress",    0f);
                swapped[i] = inst;
                instances.Add(inst);
            }

            // Cache originals via SwapOutAllMaterials, then override with pre-configured instances
            pm.SwapOutAllMaterials(r.transform, sourceMat, true, true);
            r.materials = swapped;
        }

        if (instances.Count == 0) return;

        if (MasterSingleton.Instance.PrefabBankManager.MaterializeSound != null)
            AudioManager.PlaySound(MasterSingleton.Instance.PrefabBankManager.MaterializeSound, target.transform.position);

        int completed = 0;
        foreach (Material inst in instances)
        {
            Material captured = inst;
            DOTween.To(
                () => captured.GetFloat("_Progress"),
                v  => captured.SetFloat("_Progress", v),
                1f,
                duration)
                .SetEase(Ease.Linear)
                .OnComplete(() =>
                {
                    completed++;
                    if (completed == instances.Count)
                    {
                        pm.RestoreSharedMaterials(target.transform);
                        foreach (Material m in instances)
                            Destroy(m);
                    }
                });
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // Reset
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Resets all runtime state back to the values defined in the <see cref="GameDataSO"/> template.
    /// Also clears the active checkpoint and re-enables X-Ray renderer features.
    /// Call this on game over or when starting a new game.
    /// </summary>
    public void ResetToDefaults()
    {
        _activeCheckpoint = null;

        AudioManager.StopAllMusic();
        AudioManager.StopAllSounds();

        if (defaultData == null)
        {
            Debug.LogWarning("GameDataManager: No GameDataSO assigned — using hardcoded fallbacks.");
            Lives = 3;
            Score = 0;
            CurrentLevel = 1;
            EnemiesDefeated = 0;
            MusicEnabled = true;
            IsPauseAllowed = true;

            var masterSingleton = MasterSingleton.Instance;
            if (masterSingleton != null)
            {
                var playerManager = masterSingleton.PlayerManager;
                if (playerManager != null)
                {
                    playerManager.ToggleXrayRendererFeatures(true);
                }
            }
            return;
        }

        Lives = defaultData.startingLives;
        Score = defaultData.startingScore;
        CurrentLevel = defaultData.startingLevel;
        EnemiesDefeated = defaultData.enemiesDefeatedStart;
        MusicEnabled = defaultData.musicEnabledDefault;
        IsPauseAllowed = defaultData.pauseAllowedDefault;

        var masterSingleton2 = MasterSingleton.Instance;
        if (masterSingleton2 != null)
        {
            var playerManager = masterSingleton2.PlayerManager;
            if (playerManager != null)
            {
                playerManager.ToggleXrayRendererFeatures(true);
            }
        }
    }
}

