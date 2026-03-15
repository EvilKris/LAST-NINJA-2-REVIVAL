using UnityEngine;

/// <summary>
/// Attached to the Player. Scans for nearby <see cref="WorldItem"/>s and
/// adds them to the <see cref="InventoryManager"/> when the player interacts.
/// Called by <see cref="PlayerController"/> on Block-button press when no
/// block is already active.
/// </summary>
public class PickupDetector : MonoBehaviour
{
    [Tooltip("How close the player must be to a WorldItem to pick it up.")]
    [SerializeField] private float pickupRadius = 1.2f;

    [Tooltip("Layer mask for WorldItem trigger colliders. Set this to your 'Pickup' layer in the Inspector.")]
    [SerializeField] private LayerMask pickupLayerMask;

    private static readonly Collider[] _pickupResults = new Collider[8];

    private InventoryManager _inventoryManager;

    private void Start()
    {
        _inventoryManager = MasterSingleton.Instance != null ? MasterSingleton.Instance.InventoryManager : null;

        if (_inventoryManager == null)
            Debug.LogWarning("PickupDetector: InventoryManager not found via MasterSingleton.", this);
    }

    /// <summary>
    /// Searches for the nearest <see cref="WorldItem"/> within <see cref="pickupRadius"/>,
    /// adds it to the inventory, and removes it from the world.
    /// </summary>
    /// <returns><c>true</c> if an item was picked up, <c>false</c> if nothing was in range.</returns>
    public bool TryPickup()
    {
        if (_inventoryManager == null) return false;

        int count = Physics.OverlapSphereNonAlloc(transform.position, pickupRadius, _pickupResults, pickupLayerMask);

        WorldItem nearest = null;
        float nearestDistSqr = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            if (_pickupResults[i] == null) continue;
            if (!_pickupResults[i].TryGetComponent<WorldItem>(out var candidate)) continue;
            if (candidate.itemData == null) continue;

            float distSqr = (transform.position - candidate.transform.position).sqrMagnitude;
            if (distSqr < nearestDistSqr)
            {
                nearestDistSqr = distSqr;
                nearest = candidate;
            }
        }

        if (nearest == null) return false;

        _inventoryManager.AddToInventory(nearest.itemData);
        PlayPickupSound(nearest.itemData);

#if UNITY_EDITOR
        Debug.Log($"PickupDetector: Picked up '{nearest.itemData.itemName}'.");
#endif

        nearest.Collect();
        return true;
    }

    private void PlayPickupSound(ItemData itemData)
    {
        // Prefer the per-item override; fall back to the project-wide default.
        JSAM.SoundFileObject sound = itemData.pickupSound;

        if (sound == null && MasterSingleton.Instance != null)
            sound = MasterSingleton.Instance.PrefabBankManager.DefaultPickupSound;

        if (sound != null)
            JSAM.AudioManager.PlaySound(sound);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 1f, 0.4f, 0.25f);
        Gizmos.DrawSphere(transform.position, pickupRadius);
    }
#endif
}
