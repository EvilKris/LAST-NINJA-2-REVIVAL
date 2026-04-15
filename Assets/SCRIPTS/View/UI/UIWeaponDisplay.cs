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
        if (weaponIconImage == null) return;

        // null means fists — no weapon to display
        weaponIconImage.sprite = newWeapon != null ? newWeapon.icon : null;
        weaponIconImage.enabled = newWeapon != null;

#if UNITY_EDITOR
        Debug.Log($"UI Updated: Now showing {(newWeapon != null ? newWeapon.itemName : "Fist")}");
#endif
    }

    private bool TryGetInventoryManager(out InventoryManager manager)
    {
        var instance = MasterSingleton.Instance;
        if (instance != null && instance.InventoryManager != null)
        {
            manager = instance.InventoryManager;
            return true;
        }
        manager = null;
        return false;
    }
}