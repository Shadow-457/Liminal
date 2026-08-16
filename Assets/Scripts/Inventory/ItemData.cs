using UnityEngine;

// Create items via: Right-click in Project window -> Create -> Inventory -> Item
[CreateAssetMenu(fileName = "New Item", menuName = "Inventory/Item")]
public class ItemData : ScriptableObject
{
    [Header("Info")]
    public string itemName = "New Item";
    [TextArea] public string description;
    public Sprite icon;

    [Header("Stacking")]
    public bool isStackable = true;
    public int maxStackSize = 99;

    [Header("World Object")]
    // The prefab that gets spawned in the world when this item is dropped.
    // If left empty, the system will drop whatever prefab the item was picked up as.
    public GameObject worldPrefab;
}