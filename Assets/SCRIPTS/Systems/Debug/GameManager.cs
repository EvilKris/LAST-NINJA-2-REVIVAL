using System.Collections;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central game-flow manager. Handles the full player-death sequence:
/// life deduction, fade-to-black, respawn at nearest <see cref="RespawnPoint"/>,
/// and game-over loading when lives reach zero.
/// Registered on <see cref="MasterSingleton"/> and called by <see cref="MovementComponent"/>.
/// </summary>
public class GameManager : MonoBehaviour
{
    [Tooltip("Name of the main menu scene to load on game over.")]
    [SerializeField] private string mainMenuScene = "1-Menu-Scene";

    // The last checkpoint the player activated. Null until the player crosses one.
    private Checkpoint _activeCheckpoint;

    /// <summary>True once the player has crossed at least one checkpoint this session.</summary>
    public bool HasCheckpoint => _activeCheckpoint != null && _activeCheckpoint.IsActivated;

    /// <summary>
    /// Called by <see cref="Checkpoint.OnTriggerEnter"/> when the player crosses a checkpoint.
    /// Replaces the previously stored checkpoint.
    /// </summary>
    public void RegisterCheckpoint(Checkpoint checkpoint)
    {
        _activeCheckpoint = checkpoint;
    }

    /// <summary>
    /// Begins the full player-death sequence.
    /// Deducts a life, shows a fade-to-black, then either restores the player to the
    /// last activated <see cref="Checkpoint"/> or loads the main menu if no lives remain.
    /// </summary>
    /// <param name="movementComponent">The player's <see cref="MovementComponent"/>.</param>
    public void HandlePlayerDeath(MovementComponent movementComponent)
    {
        StartCoroutine(DeathSequence(movementComponent));
    }

    private IEnumerator DeathSequence(MovementComponent movementComponent)
    {
        GameDataManager gdm = MasterSingleton.Instance.GameDataManager;
        gdm.LoseLife();

        if (gdm.Lives <= 0)
        {
            // Game over — show the fade then load the main menu
            bool fadeDone = false;
            SpawnFadeCanvas(() => fadeDone = true);
            yield return new WaitUntil(() => fadeDone);

            gdm.ResetToDefaults();
            MasterSingleton.Instance.SceneLoader.LoadSceneWithLoadingScreen(mainMenuScene);
            yield break;
        }

        // Still have lives — fade to black, restore checkpoint, fade back in
        bool respawnReady = false;
        SpawnFadeCanvas(() => respawnReady = true);

        // Wait for the screen to be fully black before moving the player
        yield return new WaitUntil(() => respawnReady);

        RespawnPlayer(movementComponent);
    }

    /// <summary>
    /// Restores the player to the last activated <see cref="Checkpoint"/> snapshot:
    /// position, rotation, health, lives, and inventory.
    /// Falls back to a positional-only teleport via <see cref="RespawnPoint"/> if no
    /// checkpoint has been activated yet.
    /// </summary>
    private void RespawnPlayer(MovementComponent movementComponent)
    {
        if (movementComponent == null) return;

        Rigidbody rb = movementComponent.GetComponent<Rigidbody>();
        HealthComponent health = movementComponent.GetComponent<HealthComponent>();
        GameDataManager gdm = MasterSingleton.Instance.GameDataManager;
        InventoryManager inv = MasterSingleton.Instance.InventoryManager;

        if (HasCheckpoint)
        {
            CheckpointSnapshot snap = _activeCheckpoint.Snapshot;

            // Teleport
            if (rb != null)
            {
                rb.position = snap.position;
                rb.rotation = snap.rotation;
                rb.linearVelocity = Vector3.zero;
            }
            movementComponent.transform.SetPositionAndRotation(snap.position, snap.rotation);

            // Restore health to the value it was at the checkpoint
            health?.SetHealth(snap.health);

            // Restore lives to the value they were at the checkpoint
            gdm.SetLives(snap.lives);

            // Restore inventory
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
            // No checkpoint crossed yet — fall back to nearest RespawnPoint for position only
            RespawnPoint fallback = RespawnPoint.FindNearest(movementComponent.transform.position);
            if (fallback != null)
            {
                if (rb != null) { rb.position = fallback.transform.position; rb.linearVelocity = Vector3.zero; }
                movementComponent.transform.position = fallback.transform.position;
            }
            else
            {
                Debug.LogWarning("GameManager.RespawnPlayer: No checkpoint or RespawnPoint found. Player stays in place.");
            }

            // No snapshot — just revive health to full
            health?.Revive();
        }

        // Re-enable physics, colliders, and movement regardless of path taken
        if (rb != null) { rb.useGravity = true; rb.linearVelocity = Vector3.zero; }
        movementComponent.SetEntityCollidersActive(true);
        movementComponent.RestoreMovement();
    }

    /// <summary>
    /// Instantiates a <see cref="DeathFadeCanvas"/> at the root of the scene and
    /// wires up <paramref name="onBlackCallback"/> to fire when the screen is fully black.
    /// </summary>
    private void SpawnFadeCanvas(System.Action onBlackCallback)
    {
        GameObject go = new("DeathFadeCanvas");
        DeathFadeCanvas fade = go.AddComponent<DeathFadeCanvas>();
        fade.OnFadeComplete = onBlackCallback;
    }
}
