using UnityEngine;
using UnityEngine.UI;

public class UIWeaponDisplay : MonoBehaviour
{
    [SerializeField] private Image weaponIconImage;

    private InventoryManager inventoryManager;

    private void Start()
    {
        if (!TryGetInventoryManager(out inventoryManager))
        {
            Debug.LogWarning("UIWeaponDisplay: InventoryManager not found. UI will not update.");
            return;
        }

        inventoryManager.OnWeaponChanged += UpdateWeaponUI;
    }

    private void OnDestroy()
    {
        if (inventoryManager != null)
        {
            inventoryManager.OnWeaponChanged -= UpdateWeaponUI;
        }
    }

    private void UpdateWeaponUI(ItemData newWeapon)
    {
        if (newWeapon == null || weaponIconImage == null) return;

        weaponIconImage.sprite = newWeapon.icon;

#if UNITY_EDITOR
        Debug.Log($"UI Updated: Now showing {newWeapon.itemName}");
#endif
    }

    private bool TryGetInventoryManager(out InventoryManager manager)
    {
        manager = MasterSingleton.Instance?.InventoryManager;
        return manager != null;
    }
}