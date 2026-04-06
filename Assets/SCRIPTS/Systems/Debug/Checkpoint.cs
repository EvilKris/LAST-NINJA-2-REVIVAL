using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Snapshot of all player state recorded when the player crosses a checkpoint.
/// Restored in full by <see cref="GameManager"/> on death.
/// </summary>
[System.Serializable]
public struct CheckpointSnapshot
{
    /// <summary>World position where the player will be placed on respawn.</summary>
    public Vector3 position;

    /// <summary>World rotation the player faced when they crossed the checkpoint.</summary>
    public Quaternion rotation;

    /// <summary>Health value at the moment of capture.</summary>
    public float health;

    /// <summary>Lives remaining at the moment of capture.</summary>
    public int lives;

    /// <summary>Copy of the player's weapon list at the moment of capture.</summary>
    public List<ItemData> weapons;

    /// <summary>Copy of the player's item list at the moment of capture.</summary>
    public List<ItemData> items;

    /// <summary>Active weapon slot index at the moment of capture.</summary>
    public int weaponIndex;

    /// <summary>Active item slot index at the moment of capture.</summary>
    public int itemIndex;
}

/// <summary>
/// Invisible trigger checkpoint. Place these on empty GameObjects around the level.
/// When only the player (matched by layer and tag) walks through, it snapshots their
/// full state and registers itself as the active checkpoint with <see cref="GameManager"/>.
/// On death, <see cref="GameManager"/> restores the player to the last activated checkpoint.
/// </summary>
[RequireComponent(typeof(Collider))]
public class Checkpoint : MonoBehaviour
{
    [Header("Player Detection")]
    [Tooltip("Layer the player GameObject must be on to activate this checkpoint.")]
    [SerializeField] private LayerMask playerLayer;
    [Tooltip("Tag the player GameObject must have to activate this checkpoint.")]
    [SerializeField] private string playerTag = "Player";

    [Header("Debug")]
    [Tooltip("Draw a visible gizmo in the Scene view so you can see where this checkpoint sits.")]
    [SerializeField] private Color gizmoColor = new Color(1f, 0.85f, 0f, 0.4f);

    /// <summary>The most recently captured state. Valid only after the checkpoint has been activated at least once.</summary>
    public CheckpointSnapshot Snapshot { get; private set; }

    /// <summary>True once the player has crossed this checkpoint at least once.</summary>
    public bool IsActivated { get; private set; }

    private void Awake()
    {
        // Ensure the collider is a trigger — checkpoints must never block movement
        Collider col = GetComponent<Collider>();
        if (!col.isTrigger)
        {
            col.isTrigger = true;
            Debug.LogWarning($"Checkpoint '{name}': Collider was not a trigger. Fixed automatically.");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayer(other.gameObject)) return;

        CaptureSnapshot(other.gameObject);

        MasterSingleton.Instance.GameManager.RegisterCheckpoint(this);

        Debug.Log($"Checkpoint '{name}' activated. Lives: {Snapshot.lives}, " +
                  $"Health: {Snapshot.health:F0}, " +
                  $"Weapons: {Snapshot.weapons.Count}, Items: {Snapshot.items.Count}");
    }

    private bool IsPlayer(GameObject obj)
    {
        return ((1 << obj.layer) & playerLayer) != 0 && obj.CompareTag(playerTag);
    }

    private void CaptureSnapshot(GameObject player)
    {
        IsActivated = true;

        // Position and rotation
        Vector3 pos = player.transform.position;
        Quaternion rot = player.transform.rotation;

        // Health
        float health = 0f;
        if (player.TryGetComponent<HealthComponent>(out var healthComp))
            health = healthComp.CurrentHealth;

        // Lives
        int lives = MasterSingleton.Instance.GameDataManager.Lives;

        // Inventory — take a copy of both lists and current indices
        List<ItemData> weapons = new List<ItemData>();
        List<ItemData> items = new List<ItemData>();
        int weaponIndex = 0;
        int itemIndex = 0;

        InventoryManager inv = MasterSingleton.Instance.InventoryManager;
        if (inv != null)
        {
            weapons = new List<ItemData>(inv.ownedWeapons);
            items = new List<ItemData>(inv.ownedItems);
            weaponIndex = inv.currentWeaponIndex;
            itemIndex = inv.currentItemIndex;
        }

        Snapshot = new CheckpointSnapshot
        {
            position    = pos,
            rotation    = rot,
            health      = health,
            lives       = lives,
            weapons     = weapons,
            items       = items,
            weaponIndex = weaponIndex,
            itemIndex   = itemIndex,
        };
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = IsActivated
            ? new Color(0f, 1f, 0.2f, 0.5f)   // green once activated
            : gizmoColor;                        // yellow-orange by default

        // Draw the collider bounds as a wire cube so you can see coverage in the Scene view
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            if (col is BoxCollider box)
                Gizmos.DrawWireCube(box.center, box.size);
            else
                Gizmos.DrawWireSphere(Vector3.zero, 1f);
        }

        Gizmos.matrix = Matrix4x4.identity;
        Gizmos.color = IsActivated ? Color.green : Color.yellow;
        Gizmos.DrawIcon(transform.position, "d_Prefab Icon", true);
    }
}
