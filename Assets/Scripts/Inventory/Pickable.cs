using UnityEngine;

// Attach this to ANY object in the world (with a Collider) to make it pickable.
// Requires a Collider (set "Is Trigger" off is fine, raycast works either way).
[RequireComponent(typeof(Collider))]
public class Pickable : MonoBehaviour
{
    [Header("Item Settings")]
    public ItemData itemData;
    [Min(1)] public int quantity = 1;

    [Header("Optional")]
    [Tooltip("If off, this exact GameObject won't be destroyed after pickup (useful for pooled objects).")]
    public bool destroyOnPickup = true;

    [Tooltip("Optional VFX/SFX prefab spawned at pickup point.")]
    public GameObject pickupEffect;

    // Called by the PlayerInteractor when right-click picks this up.
    public bool TryPickup()
    {
        if (itemData == null)
        {
            Debug.LogWarning($"Pickable on {gameObject.name} has no ItemData assigned.");
            return false;
        }

        bool added = InventoryManager.Instance.AddItem(itemData, quantity, GetPrefabReference());
        if (!added) return false; // inventory full, leave item in world

        if (pickupEffect != null)
            Instantiate(pickupEffect, transform.position, Quaternion.identity);

        if (destroyOnPickup)
            Destroy(gameObject);
        else
            gameObject.SetActive(false);

        return true;
    }

    // Tries to find the original prefab asset this instance came from (editor-safe fallback: itself).
    private GameObject GetPrefabReference()
    {
        // At runtime we just store this object's own prefab-like reference via a marker component,
        // OR simplest/most robust: rely on ItemData.worldPrefab if assigned.
        return itemData.worldPrefab != null ? itemData.worldPrefab : gameObject;
    }
}