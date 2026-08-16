using UnityEngine;
using UnityEngine.UI; // for the legacy Graphic safety-check below
using System.Collections;

// Shows the item in the currently SELECTED slot as a real 3D object positioned
// relative to the player camera every frame, so it looks like you're actually
// holding it from first person (a viewmodel). The old behaviour (drawing the
// 2D item icon in a UI Image) is replaced by this.
//
// Setup:
// 1. Add this component to any GameObject (e.g. the existing "HeldItemDisplay"
//    object under your Canvas still works).
// 2. Drag your PlayerInteractor into the field.
// 3. Optionally set holdAnchor (defaults to the player camera), holdOffset,
//    holdRotation and holdScale to frame the object nicely. The 3D model used
//    is the item's worldPrefab / sourcePrefab, so make sure the ItemData has a
//    worldPrefab assigned (the Cube's does).
public class HeldItemDisplay : MonoBehaviour
{
    [Header("References")]
    public PlayerInteractor playerInteractor;

    [Header("Positioning")]
    [Tooltip("Transform used as the positioning reference. Leave empty to use the player camera.")]
    public Transform holdAnchor;

    [Tooltip("World-space offset from the camera, in metres (z = forward).")]
    public Vector3 holdOffset = new Vector3(0.35f, -0.35f, 0.85f);

    [Tooltip("Extra Euler rotation applied on top of the camera's rotation.")]
    public Vector3 holdRotation = new Vector3(0f, 25f, 0f);

    [Tooltip("Extra scale multiplier. The prefab's own scale is kept and then multiplied by this.")]
    public float holdScale = 1f;

    [Tooltip("Gentle up/down bob while held. Set to 0 to disable.")]
    public float bobAmount = 0.012f;

    [Tooltip("How quickly the held object catches up to the camera each frame. Higher = snappier.")]
    public float followSmooth = 24f;

    private GameObject heldObject;
    private Coroutine _punchRoutine;

    void Awake()
    {
        // Safety net: if this component still sits on a UI object with a
        // leftover Graphic (e.g. the old 2D-icon Image from before the 3D
        // version), hide it so no stale white box/icon shows on screen.
        // The main scene already has that Image removed — this just protects
        // prefabs and any other setups.
        foreach (var graphic in GetComponents<Graphic>())
            graphic.enabled = false;

        ResolveAnchor();
    }

    void OnEnable()
    {
        if (playerInteractor != null)
            playerInteractor.OnSelectedSlotChanged += OnSelectedSlotChanged;

        Refresh();
    }

    void Start()
    {
        // Subscribe to inventory changes here (Start) rather than OnEnable so the
        // singleton is guaranteed to already exist — otherwise drops wouldn't update
        // the held viewmodel until you switch slots.
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.OnInventoryChanged += Refresh;
    }

    void OnDisable()
    {
        if (playerInteractor != null)
            playerInteractor.OnSelectedSlotChanged -= OnSelectedSlotChanged;
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.OnInventoryChanged -= Refresh;

        DestroyHeldObject();
    }

    private void OnSelectedSlotChanged(int newIndex) => Refresh();

    private void ResolveAnchor()
    {
        if (holdAnchor == null)
        {
            if (playerInteractor != null && playerInteractor.playerCamera != null)
                holdAnchor = playerInteractor.playerCamera.transform;
            else if (Camera.main != null)
                holdAnchor = Camera.main.transform;
            else
                holdAnchor = transform;
        }
    }

    private void Refresh()
    {
        ResolveAnchor();

        if (InventoryManager.Instance == null || playerInteractor == null)
        {
            DestroyHeldObject();
            return;
        }

        // Find the prefab the currently selected slot is holding, if any.
        GameObject prefab = null;
        int index = playerInteractor.SelectedSlotIndex;
        if (index >= 0 && index < InventoryManager.Instance.slots.Length)
        {
            var slot = InventoryManager.Instance.slots[index];
            if (!slot.IsEmpty)
                prefab = slot.sourcePrefab != null ? slot.sourcePrefab : slot.item.worldPrefab;
        }

        if (prefab == null)
        {
            DestroyHeldObject(); // selected slot is empty -> nothing held
            return;
        }

        // Recreate the held 3D model whenever the selection/contents change.
        if (heldObject != null) Destroy(heldObject);

        // NOTE: deliberately NOT parented to the camera. We place it in world
        // space relative to the camera every LateUpdate, which is immune to any
        // scale/rotation quirks the camera hierarchy might have.
        heldObject = Instantiate(prefab);
        heldObject.name = "Held_" + prefab.name;

        Vector3 baseScale = heldObject.transform.localScale;
        heldObject.transform.localScale = baseScale * holdScale;

        // Instantly put it in front of the camera, then keep it synced each frame.
        SyncToCamera();

        // Small "catch" punch so picking something up feels alive.
        PlaySpawnPunch();

        // Turn the clone into a pure viewmodel so it can't be re-picked up,
        // fall over, or block our own pickup raycasts.
        foreach (var col in heldObject.GetComponentsInChildren<Collider>(true))
            col.enabled = false;
        foreach (var rb in heldObject.GetComponentsInChildren<Rigidbody>(true))
            rb.isKinematic = true;
        foreach (var pick in heldObject.GetComponentsInChildren<Pickable>(true))
            pick.enabled = false;
    }

    private void LateUpdate()
    {
        // Keep the held object glued to the current view every frame.
        SyncToCamera();
    }

    // Positions the held object in world space using the camera's current
    // position/rotation plus holdOffset/holdRotation. This always lands it in
    // the right place relative to what you see, regardless of how the camera
    // is parented or scaled.
    private void SyncToCamera()
    {
        if (heldObject == null) return;
        ResolveAnchor();
        if (holdAnchor == null) return;

        Transform cam = holdAnchor;

        // Keep the viewmodel inside a sane window so a bad inspector value can never
        // push the held item off-screen (e.g. a huge +X offset). You can still tune
        // holdOffset freely within these bounds.
        Vector3 off = new Vector3(
            Mathf.Clamp(holdOffset.x, 0.05f, 0.7f),
            Mathf.Clamp(holdOffset.y, -0.6f, 0.6f),
            Mathf.Clamp(holdOffset.z, 0.3f, 1.3f));

        float bob = 0f;
        if (bobAmount > 0f)
            bob = Mathf.Sin(Time.time * 3f) * bobAmount;

        Vector3 targetPos = cam.position
            + cam.right * off.x
            + cam.up * (off.y + bob)
            + cam.forward * off.z;
        Quaternion targetRot = cam.rotation * Quaternion.Euler(holdRotation);

        // Smoothly catch up instead of snapping each frame — this removes the
        // one-frame jitter you get when the camera updates in its own LateUpdate.
        Transform t = heldObject.transform;
        float k = Mathf.Clamp01(followSmooth * Time.deltaTime);
        t.position = Vector3.Lerp(t.position, targetPos, k);
        t.rotation = Quaternion.Slerp(t.rotation, targetRot, k);
    }

    private void PlaySpawnPunch()
    {
        if (_punchRoutine != null) StopCoroutine(_punchRoutine);
        _punchRoutine = StartCoroutine(SpawnPunchRoutine());
    }

    // Quick overshoot scale punch so a freshly picked-up item feels like it
    // snaps into your hand.
    private IEnumerator SpawnPunchRoutine()
    {
        if (heldObject == null) yield break;
        Transform t = heldObject.transform;
        Vector3 baseScale = t.localScale;
        float duration = 0.3f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (heldObject == null) yield break;
            elapsed += Time.deltaTime;
            float p = Mathf.Clamp01(elapsed / duration);
            t.localScale = baseScale * (1f + 0.24f * Mathf.Sin(p * Mathf.PI));
            yield return null;
        }
        if (heldObject != null) t.localScale = baseScale;
        _punchRoutine = null;
    }

    private void DestroyHeldObject()
    {
        if (_punchRoutine != null)
        {
            StopCoroutine(_punchRoutine);
            _punchRoutine = null;
        }
        if (heldObject != null)
        {
            Destroy(heldObject);
            heldObject = null;
        }
    }
}
