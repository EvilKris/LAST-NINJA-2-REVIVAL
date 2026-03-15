using UnityEngine;

/// <summary>
/// Represents a pickable item placed in the world.
/// Attach this to a prefab alongside a trigger Collider.
/// The component is purely passive — pickup is initiated by the player's <see cref="PickupDetector"/>.
/// </summary>
public class WorldItem : MonoBehaviour
{
    [Tooltip("The ItemData ScriptableObject this world object represents.")]
    public ItemData itemData;

    /// <summary>
    /// Called by <see cref="PickupDetector"/> when the player picks this item up.
    /// Removes the object from the world.
    /// </summary>
    public void Collect()
    {
        gameObject.SetActive(false);
        Destroy(gameObject);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (itemData == null)
            Debug.LogWarning($"WorldItem on '{gameObject.name}' has no ItemData assigned.", this);
    }
#endif
}
