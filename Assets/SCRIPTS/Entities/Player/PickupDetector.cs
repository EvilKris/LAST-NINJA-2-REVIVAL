using UnityEngine;

/// <summary>
/// Attached to the Player. Scans for nearby <see cref="WorldItem"/>s and
/// triggers the pickup animation when the player interacts via the block button.
/// Called by <see cref="PlayerController"/> when no block or attack is active.
/// The actual collect and inventory-add happen at 0.5 normalised time of the
/// pickup animation, mirroring how attacks gate their hit-detection.
///
/// Animator setup:
///   1. The pickup state must be reachable from Idle via the <c>t_isPickingUp</c> trigger.
///   2. The state's <b>Tag</b> (in the Inspector) must be set to <c>Pickup</c>.
///   3. Attach an <see cref="AnimationStateNotifier"/> with
///      <c>pickupDetectorEvent = EndPickup</c>.
///   4. The transition from Idle → pickup must have <c>isAction == false</c> as a condition
///      (same guard used by block/attack) so it is only available from Idle.
/// </summary>
public class PickupDetector : MonoBehaviour, IAnimationStateListener
{
    [Tooltip("How close the player must be to a WorldItem to pick it up.")]
    [SerializeField] private float pickupRadius = 1.2f;

    [Tooltip("Layer mask for WorldItem trigger colliders. Set this to your 'Pickup' layer in the Inspector.")]
    [SerializeField] private LayerMask pickupLayerMask;

    /// <summary>True while the pickup animation is playing and the item has not yet been collected.</summary>
    public bool IsPickingUp => _pendingPickup != null;

    private static readonly Collider[] _pickupResults = new Collider[8];
    private static readonly int HashPickup = Animator.StringToHash("t_isPickingUp");
    private static readonly int HashIsAction = Animator.StringToHash("isAction");

    private InventoryManager _inventoryManager;
    private Animator _animator;
    

    // The item waiting to be collected once the animation reaches 0.5 normalised time.
    private WorldItem _pendingPickup;
    private bool _collectFired;

    private void Start()
    {
        _inventoryManager = MasterSingleton.Instance != null ? MasterSingleton.Instance.InventoryManager : null;
        _animator = GetComponent<Animator>();
       
        if (_inventoryManager == null)
            Debug.LogWarning("PickupDetector: InventoryManager not found via MasterSingleton.", this);
        if (_animator == null)
            Debug.LogWarning("PickupDetector: No Animator found on this GameObject.", this);
    }

    /// <summary>
    /// Called by <see cref="AnimationStateNotifier"/> via <c>OnStateUpdate</c> once the
    /// pickup state's normalised time reaches 0.5. Commits the pending pickup exactly once.
    /// </summary>
    public void NotifyCollectWindow()
    {
        if (_collectFired) return;
        _collectFired = true;
        CommitPickup();
    }

    /// <summary>
    /// Searches for the nearest <see cref="WorldItem"/> within <see cref="pickupRadius"/>.
    /// If one is found, triggers the pickup animation and returns <c>true</c>.
    /// The inventory add and world removal are deferred to 0.5 normalised animation time.
    /// </summary>
    /// <returns><c>true</c> if a nearby item was found and the animation was triggered.</returns>
    public bool TryBeginPickup()
    {
        if (_inventoryManager == null) return false;
        if (_animator == null) return false;

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

        _pendingPickup = nearest;
        _collectFired = false;

        // Set isAction so the block's AnyState transition (gated by isAction == false)
        // cannot interrupt the pickup animation.
        _animator.SetBool(HashIsAction, true);
        _animator.SetTrigger(HashPickup);
        return true;
    }

    private void CommitPickup()
    {
        if (_pendingPickup == null) return;

        ItemData itemData = _pendingPickup.itemData;
        _inventoryManager.AddToInventory(itemData);
        PlayPickupSound(itemData);

#if UNITY_EDITOR
        Debug.Log($"PickupDetector: Picked up '{itemData.itemName}'.");
#endif

        _pendingPickup.Collect();
        _pendingPickup = null;
    }

    /// <summary>
    /// Called by <see cref="AnimationStateNotifier"/> when the pickup animation state exits.
    /// Always clears <c>isAction</c> so the Animator can transition back to Idle.
    /// </summary>
    public void OnAnimationStateExit(int layerIndex, AnimationExitEvent exitEvent)
    {
        _pendingPickup = null;
        _collectFired = false;
        _animator.SetBool(HashIsAction, false);
        
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

