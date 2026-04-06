using UnityEngine;
using UnityEngine;
using System.Collections.Generic;
using System;

public class InventoryManager : MonoBehaviour
{
    [Header("Current Inventory")]
    public List<ItemData> ownedWeapons = new List<ItemData>();
    public List<ItemData> ownedItems = new List<ItemData>();

    /// <summary>Index of the currently equipped weapon. Written by <see cref="CycleWeapon"/> and restored by <see cref="GameManager"/>.</summary>
    [SerializeField] public int currentWeaponIndex = 0;
    /// <summary>Index of the currently equipped item. Written by <see cref="CycleItem"/> and restored by <see cref="GameManager"/>.</summary>
    [SerializeField] public int currentItemIndex = 0;

    // Events for UI and Player to listen to
    public event Action<ItemData> OnWeaponChanged;
    public event Action<ItemData> OnItemChanged;

    public void CycleWeapon()
    {
        if (ownedWeapons.Count == 0) return;
        currentWeaponIndex = (currentWeaponIndex + 1) % ownedWeapons.Count;
        OnWeaponChanged?.Invoke(ownedWeapons[currentWeaponIndex]);
    }

    public void CycleItem()
    {
        if (ownedItems.Count == 0) return;
        currentItemIndex = (currentItemIndex + 1) % ownedItems.Count;
        OnItemChanged?.Invoke(ownedItems[currentItemIndex]);
    }

    // Method for Picking up Items or Debug Setup
    public void AddToInventory(ItemData data)
    {
        if (data.category == ItemCategory.Weapon)
            ownedWeapons.Add(data);
        else
            ownedItems.Add(data);
    }
}