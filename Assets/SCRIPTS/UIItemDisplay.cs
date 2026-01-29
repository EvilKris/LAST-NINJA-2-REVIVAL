using UnityEngine;
using UnityEngine.UI;

public class UIItemDisplay : MonoBehaviour
{
    [SerializeField] private Image itemIconImage;

    private InventoryManager inventoryManager;

    private void Start()
    {
        if (!TryGetInventoryManager(out inventoryManager))
        {
            Debug.LogWarning("UIItemDisplay: InventoryManager not found. UI will not update.");
            return;
        }

        inventoryManager.OnItemChanged += UpdateItemUI;
    }

    private void OnDestroy()
    {
        if (inventoryManager != null)
        {
            inventoryManager.OnItemChanged -= UpdateItemUI;
        }
    }

    private void UpdateItemUI(ItemData newItem)
    {
        if (newItem == null || itemIconImage == null) return;

        itemIconImage.sprite = newItem.icon;

#if UNITY_EDITOR
        Debug.Log($"Item UI Updated: {newItem.itemName} (Aitemu kōshin - アイテム更新)");
#endif
    }

    private bool TryGetInventoryManager(out InventoryManager manager)
    {
        manager = MasterSingleton.Instance?.InventoryManager;
        return manager != null;
    }
}