using NUnit.Framework.Interfaces;
using System.Collections.Generic;
using UnityEngine;

public class LevelInitializer : MonoBehaviour
{
    public bool useDebugStart;
    public Transform startPoint;
    public List<ItemData> startingWeapons;

    // List of Enemy IDs that should be "already dead"
    public List<string> beatenEnemyIDs;

    void Start()
    {
        if (!useDebugStart) return;

        foreach (var weapon in startingWeapons)
        {
            // This "feeds" the SOs to the manager at runtime
            MasterSingleton.Instance.InventoryManager.AddToInventory(weapon);
        }

        // Tells the UI to show the first weapon in the list
        MasterSingleton.Instance.InventoryManager.CycleWeapon();
        // 1. Teleport Ninja to startPoint
        // 2. Add startingWeapons to InventoryComponent
        // 3. Find enemies by ID and call HealthComponent.TakeDamage(9999) 
    }

}