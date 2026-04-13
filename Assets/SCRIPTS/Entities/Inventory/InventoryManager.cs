using UnityEngine;

using System.Collections.Generic;
using System;

public class InventoryManager : MonoBehaviour
{
    [Header("Current Inventory")]
    public List<ItemData> ownedWeapons = new List<ItemData>();
    public List<ItemData> ownedItems = new List<ItemData>();

    private CombatHandler _combatHandler;
    private Coroutine _equipDelayCoroutine;
    private const float EQUIP_DELAY = 2f;

    /// <summary>Index of the currently equipped weapon. Written by <see cref="CycleWeapon"/> and restored by <see cref="GameManager"/>.</summary>
    [SerializeField] public int currentWeaponIndex = 0;
    /// <summary>Index of the currently equipped item. Written by <see cref="CycleItem"/> and restored by <see cref="GameManager"/>.</summary>
    [SerializeField] public int currentItemIndex = 0;

    // Events for UI and Player to listen to
    public event Action<ItemData> OnWeaponChanged;
    public event Action<ItemData> OnItemChanged;

    /// <summary>
    /// Lazily resolves the player's <see cref="CombatHandler"/>. The reference is
    /// cached, but if it becomes stale (scene reload / player respawn) it is
    /// re-acquired automatically.
    /// </summary>
    private CombatHandler ResolveCombatHandler()
    {
        if (_combatHandler == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
                _combatHandler = player.GetComponent<CombatHandler>();
        }
        return _combatHandler;
    }

    public void CycleWeapon()
    {
        if (ownedWeapons.Count == 0) return;
        currentWeaponIndex = (currentWeaponIndex + 1) % ownedWeapons.Count;
        ItemData weapon = ownedWeapons[currentWeaponIndex];
        OnWeaponChanged?.Invoke(weapon);
        RestartEquipTimer(weapon);
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
        {
            ownedWeapons.Add(data);
            OnWeaponChanged?.Invoke(data);
        }
        else
        {
            ownedItems.Add(data);
        }
    }

    /// <summary>
    /// Resets the active weapon back to fists (no weapon).
    /// Fires <see cref="OnWeaponChanged"/> with <c>null</c> so the UI clears its icon.
    /// Called by <see cref="HealthComponent"/> on death.
    /// </summary>
    public void RevertToFists()
    {
        if (_equipDelayCoroutine != null)
        {
            StopCoroutine(_equipDelayCoroutine);
            _equipDelayCoroutine = null;
        }

        currentWeaponIndex = 0;
        OnWeaponChanged?.Invoke(null);
    }

    /// <summary>
    /// Cancels any pending equip and starts a fresh <see cref="EQUIP_DELAY"/> countdown.
    /// If the player stops cycling before the timer expires the style is committed.
    /// </summary>
    private void RestartEquipTimer(ItemData weapon)
    {
        if (_equipDelayCoroutine != null)
            StopCoroutine(_equipDelayCoroutine);
        _equipDelayCoroutine = StartCoroutine(EquipAfterDelay(weapon));
    }

    private System.Collections.IEnumerator EquipAfterDelay(ItemData weapon)
    {
        yield return new WaitForSeconds(EQUIP_DELAY);

        _equipDelayCoroutine = null;

        CombatHandler handler = ResolveCombatHandler();
        if (handler != null && weapon.fightingStyle != null)
            handler.EquipStyle(weapon.fightingStyle);
    }
}