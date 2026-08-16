using UnityEngine;

// Runtime data held in one slot. Slot 0 is the "hand" slot (special rules).
[System.Serializable]
public class InventorySlot
{
    public ItemData item;
    public int quantity;

    // Stores the actual world prefab this stack came from, so dropping
    // spawns back the correct object (in case worldPrefab isn't set on ItemData).
    public GameObject sourcePrefab;

    public bool IsEmpty => item == null || quantity <= 0;

    public void Clear()
    {
        item = null;
        quantity = 0;
        sourcePrefab = null;
    }

    public void Set(ItemData newItem, int amount, GameObject prefab)
    {
        item = newItem;
        quantity = amount;
        sourcePrefab = prefab;
    }
}