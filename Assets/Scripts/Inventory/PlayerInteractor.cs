using UnityEngine;
using System;

// Put this on your Player (or Main Camera). Handles:
//  - Right Click: raycast forward, if it hits a Pickable, pick it up.
//  - E: drop the item in the currently ACTIVE slot in front of the player.
//  - Number keys 1-4: select that hotbar slot directly (matches UI highlight).
//
// Selection: number keys or clicking a UI slot both set SelectedSlotIndex.
// Drop only affects the selected (active) slot — you can never drop from any
// other slot.
public class PlayerInteractor : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Camera used for the pickup raycast AND the drop origin. Defaults to Camera.main if empty.")]
    public Camera playerCamera;

    [Tooltip("Where dropped items spawn from. Defaults to the Camera if set, otherwise this transform.")]
    public Transform dropPoint;

    [Header("Pickup Settings")]
    public float interactRange = 3f;
    public LayerMask pickupLayerMask = ~0; // everything by default

    [Header("Drop Settings")]
    public float dropForwardOffset = 1.2f;
    public float dropUpOffset = 1f;
    public float dropThrowForce = 2f;

    [Header("Hotbar")]
    [Tooltip("If true, pressing 1-4 selects that slot (index = key-1).")]
    public bool allowNumberKeySelection = true;

    // Set from UI clicks or number keys. -1 means "no manual selection yet,
    // use last filled slot" as a fallback for Drop.
    [HideInInspector] public int SelectedSlotIndex = -1;

    // Fired whenever the selected slot changes, so UI (highlight) and a
    // held-item display can react without polling every frame.
    public event Action<int> OnSelectedSlotChanged;

    void Awake()
    {
        if (playerCamera == null) playerCamera = Camera.main;
        if (dropPoint == null)
            dropPoint = playerCamera != null ? playerCamera.transform : transform;
    }

    void Start()
    {
        // Auto-equip the reserved gun slot so the gun is ready to fire immediately.
        if (InventoryManager.Instance != null && InventoryManager.Instance.GunSlotIndex >= 0 &&
            SelectedSlotIndex < 0)
        {
            SelectSlot(InventoryManager.Instance.GunSlotIndex);
        }
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(1)) // Right Click
        {
            TryPickupInFrontOfPlayer();
        }

        if (Input.GetKeyDown(KeyCode.E)) // Drop the currently selected slot's item
        {
            DropSelectedItem();
        }

        if (allowNumberKeySelection)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) SelectSlot(0);
            else if (Input.GetKeyDown(KeyCode.Alpha2)) SelectSlot(1);
            else if (Input.GetKeyDown(KeyCode.Alpha3)) SelectSlot(2);
            else if (Input.GetKeyDown(KeyCode.Alpha4)) SelectSlot(3);
        }
    }

    /// <summary>
    /// Selects a slot (called by number keys or by UI clicks). Fires
    /// OnSelectedSlotChanged so the UI highlight and held-item view refresh.
    /// </summary>
    public void SelectSlot(int index)
    {
        SelectedSlotIndex = index;
        OnSelectedSlotChanged?.Invoke(index);
    }

    private void TryPickupInFrontOfPlayer()
    {
        if (playerCamera == null) return;

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, interactRange, pickupLayerMask))
        {
            Pickable pickable = hit.collider.GetComponentInParent<Pickable>();
            if (pickable != null && pickable.TryPickup())
            {
                // Auto-select the slot the item landed in so the held 3D model
                // pops up right away and E drops that item immediately.
                int slotIndex = InventoryManager.Instance.FindSlotContaining(pickable.itemData);
                if (slotIndex >= 0) SelectSlot(slotIndex);
            }
        }
    }

    private void DropSelectedItem()
    {
        int index = SelectedSlotIndex;

        // Only the currently ACTIVE slot can be dropped. If nothing has been
        // selected yet, or the selected slot is the hand slot or is empty,
        // pressing E simply does nothing.
        if (index < 0) return; // no slot selected yet -> nothing to drop
        if (InventoryManager.Instance.IsReservedSlot(index)) return; // hand & gun can't be dropped
        if (InventoryManager.Instance.IsSlotEmpty(index)) return; // active slot empty -> do nothing

        var removed = InventoryManager.Instance.RemoveSlot(index);
        if (removed == null) return;

        SpawnDroppedItem(removed);
    }

    private void SpawnDroppedItem(InventorySlot slotData)
    {
        GameObject prefabToSpawn = slotData.sourcePrefab != null
            ? slotData.sourcePrefab
            : slotData.item.worldPrefab;

        if (prefabToSpawn == null)
        {
            Debug.LogWarning($"No world prefab available to drop for item '{slotData.item.itemName}'.");
            return;
        }

        Vector3 spawnPos = dropPoint.position
            + dropPoint.forward * dropForwardOffset
            + Vector3.up * dropUpOffset;

        GameObject dropped = Instantiate(prefabToSpawn, spawnPos, Quaternion.identity);

        // Make sure it's pickable again (in case prefab wasn't already set up).
        if (dropped.GetComponent<Pickable>() == null)
        {
            var pickable = dropped.AddComponent<Pickable>();
            pickable.itemData = slotData.item;
            pickable.quantity = slotData.quantity;
        }
        else
        {
            dropped.GetComponent<Pickable>().quantity = slotData.quantity;
        }

        // Optional little toss.
        Rigidbody rb = dropped.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.AddForce(dropPoint.forward * dropThrowForce, ForceMode.Impulse);
        }
    }
}