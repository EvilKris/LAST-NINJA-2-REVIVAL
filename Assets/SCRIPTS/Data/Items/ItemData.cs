using UnityEngine;

[CreateAssetMenu(fileName = "NewItemData", menuName = "LastNinja/Inventory/ItemData")]
public class ItemData : ScriptableObject
{
    [Header("General Info")]
    public string itemName;
    public ItemCategory category;
    public Sprite icon; // To display in your WeaponContainer

    [Header("Prefab References")]
    [Tooltip("The actual 3D model to spawn in the Ninja's hand.")]
    public GameObject weaponPrefab;

    [Header("Combat/Usage")]
    [Tooltip("If it's a weapon, link its specific moveset here.")]
    public CombatMove[] moveset; // Link back to our CombatMove SOs

    [Header("Description")]
    [TextArea]
    public string description;
}