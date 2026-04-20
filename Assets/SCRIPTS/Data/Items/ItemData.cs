using JSAM;
using UnityEngine;

[CreateAssetMenu(fileName = "NewItemData", menuName = "LastNinja/Inventory/ItemData")]
public class ItemData : ScriptableObject
{
    [Header("General Info")]
    public string itemName;
    public ItemCategory category;
    public Sprite icon; // To display in your WeaponContainer

    [Header("Prefab References")]
    [Tooltip("The actual 3D model to spawn in the Ninja's hand or in the world.")]
    public GameObject itemPrefab;

    [Header("Combat/Usage")]
    [Tooltip("The fighting style (moveset, clinch support, animator) applied when this weapon is equipped.")]
    public FightingStyle fightingStyle;

    [Header("Audio")]
    [Tooltip("Sound played on pickup. Leave empty to use the project-wide default in PrefabBankManager.")]
    public SoundFileObject pickupSound;
    [Tooltip("Sound played when this item/weapon is drawn. Leave empty for no sound.")]
    public SoundFileObject drawSound;

    [Header("Item Count")]
    [Tooltip("Number of this item to give the player on pickup. For stackable items like shurikens or health pickups.")]
    public int count = 0;

    [Header("Description")]
    [TextArea]
    public string description;
}