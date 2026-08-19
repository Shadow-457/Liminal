using UnityEngine;
using System;

// Central inventory brain. Attach to a persistent "Player" or "GameManager" object.
// Slot index 0 = HAND (left-most slot in your UI image). Only ever empty or holding
// nothing picked up passively — reserved as an "active/equipped" slot.
// Slot indices 1,2,3 = the three general storage slots (shown as boxes 2,3,4 in UI).
public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance { get; private set; }

    [Header("Setup")]
    [Tooltip("Total slots including the hand. Default 4 to match a hand + 3 storage layout.")]
    public int slotCount = 4;

    [Tooltip("Index of the hand slot. Leave at 0 (left-most).")]
    public int handSlotIndex = 0;

    [Tooltip("Index reserved for the player's gun (the 4th UI slot = index 3). -1 disables the weapon slot.")]
    public int gunSlotIndex = 3;

    [Tooltip("Optional sprite shown as the gun's icon in the inventory (slot 4 + the hand slot). " +
             "Drag a sprite here to replace the default auto-generated tile.")]
    public Sprite gunIcon;

    public InventorySlot[] slots;

    /// <summary>True if a slot is reserved (hand or gun) and can't be filled by normal pickup.</summary>
    public bool IsReservedSlot(int index)
        => index == handSlotIndex || (gunSlotIndex >= 0 && index == gunSlotIndex);

    /// <summary>The reserved gun slot index, or -1 if disabled.</summary>
    public int GunSlotIndex => gunSlotIndex >= 0 && gunSlotIndex < slotCount ? gunSlotIndex : -1;

    // Fired whenever inventory contents change, so UI can refresh.
    public event Action OnInventoryChanged;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        slots = new InventorySlot[slotCount];
        for (int i = 0; i < slotCount; i++)
            slots[i] = new InventorySlot();

        // Reserve a slot for the gun so it's always present (icon shows, not stackable/droppable).
        if (GunSlotIndex >= 0 && GunSlotIndex != handSlotIndex)
            slots[GunSlotIndex].Set(CreateGunItem(), 1, null);

        OnInventoryChanged?.Invoke();
    }

    // A runtime Gun item (no asset file needed). Icon comes from the gunIcon field if set,
    // otherwise ItemIconDatabase's auto-generated fallback tile.
    private ItemData CreateGunItem()
    {
        ItemData gun = ScriptableObject.CreateInstance<ItemData>();
        gun.name = "Gun";
        gun.itemName = "Gun";
        gun.description = "Your weapon. Reserved in slot 4 — select it to equip and fire.";
        gun.isStackable = false;
        gun.maxStackSize = 1;
        gun.icon = gunIcon; // optional custom icon; null => fallback tile
        return gun;
    }

    // ---------- Public API ----------

    /// <summary>
    /// Tries to add an item picked up from the world into storage slots (never the hand slot).
    /// Returns true if it was added successfully.
    /// </summary>
    public bool AddItem(ItemData item, int amount, GameObject sourcePrefab)
    {
        if (item == null || amount <= 0) return false;

        // 1. Try to stack onto an existing matching stack in storage slots.
        if (item.isStackable)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (IsReservedSlot(i)) continue; // never auto-stack into reserved slots
                var slot = slots[i];
                if (!slot.IsEmpty && slot.item == item && slot.quantity < item.maxStackSize)
                {
                    int space = item.maxStackSize - slot.quantity;
                    int toAdd = Mathf.Min(space, amount);
                    slot.quantity += toAdd;
                    amount -= toAdd;

                    if (amount <= 0)
                    {
                        OnInventoryChanged?.Invoke();
                        return true;
                    }
                }
            }
        }

        // 2. Put remainder into the first empty storage slot.
        for (int i = 0; i < slots.Length; i++)
        {
            if (IsReservedSlot(i)) continue; // hand & gun stay reserved
            if (slots[i].IsEmpty)
            {
                slots[i].Set(item, amount, sourcePrefab);
                OnInventoryChanged?.Invoke();
                return true;
            }
        }

        // 3. No room.
        OnInventoryChanged?.Invoke();
        return false;
    }

    /// <summary>
    /// Removes one full stack from a slot (used for dropping). Returns the removed data.
    /// </summary>
    public InventorySlot RemoveSlot(int index)
    {
        if (index < 0 || index >= slots.Length) return null;
        var slot = slots[index];
        if (slot.IsEmpty) return null;

        var copy = new InventorySlot();
        copy.Set(slot.item, slot.quantity, slot.sourcePrefab);

        slot.Clear();
        OnInventoryChanged?.Invoke();
        return copy;
    }

    /// <summary>
    /// Removes a specific quantity from a slot. Returns how much was actually removed.
    /// </summary>
    public int RemoveFromSlot(int index, int amount)
    {
        if (index < 0 || index >= slots.Length) return 0;
        var slot = slots[index];
        if (slot.IsEmpty) return 0;

        int removed = Mathf.Min(amount, slot.quantity);
        slot.quantity -= removed;
        if (slot.quantity <= 0) slot.Clear();

        OnInventoryChanged?.Invoke();
        return removed;
    }

    public bool IsSlotEmpty(int index)
    {
        if (index < 0 || index >= slots.Length) return true;
        return slots[index].IsEmpty;
    }

    /// <summary>Returns index of the last non-empty storage slot (excluding hand), or -1.</summary>
    public int GetLastFilledStorageSlot()
    {
        for (int i = slots.Length - 1; i >= 0; i--)
        {
            if (i == handSlotIndex) continue;
            if (!slots[i].IsEmpty) return i;
        }
        return -1;
    }

    public bool HasFreeStorageSlot()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (i == handSlotIndex) continue;
            if (slots[i].IsEmpty) return true;
        }
        return false;
    }

    /// <summary>Returns the storage-slot index holding a stack of the given item, or -1.</summary>
    public int FindSlotContaining(ItemData item)
    {
        if (item == null) return -1;
        for (int i = 0; i < slots.Length; i++)
        {
            if (i == handSlotIndex) continue;
            if (!slots[i].IsEmpty && slots[i].item == item) return i;
        }
        return -1;
    }

    public void ForceRefreshUI() => OnInventoryChanged?.Invoke();
}