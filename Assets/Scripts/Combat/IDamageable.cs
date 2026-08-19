using UnityEngine;

// Implement this on anything you want to be shootable (enemies, targets, NPCs).
// The GunController casts one ray and hands damage to the first component on
// the hit object that implements this interface.
public interface IDamageable
{
    void TakeDamage(int amount, Vector3 hitPoint, Vector3 hitDirection);
}