using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A teleport portal. Drop this on ANY of your portal GameObjects and give it a
/// trigger Collider (one is added automatically if you forget). Anything that
/// enters the trigger — the player, rigidbodies, dropped items, enemies — is
/// warped out in front of the linked destination portal.
///
/// Linking the two portals is easy, pick ONE of these:
///   1. MANUAL: drag the other portal's Transform into [ Destination ]. (Most reliable.)
///   2. AUTO:   give both portals the SAME [ Portal ID ] (e.g. "A"), leave
///              [ Destination ] empty, and they find each other automatically.
///
/// A short per-object cooldown stops the classic "teleport into the other portal
/// and get slammed straight back" ping-pong.
///
/// Works with:
///   - The StarterAssets FirstPersonController (CharacterController-based).
///   - Rigidbody objects (picking up / dropping, props, enemies).
/// </summary>
public class Portal : MonoBehaviour
{
    [Header("Destination")]
    [Tooltip("Drag the OTHER portal here. Leave empty to auto-link by Portal ID.")]
    public Transform destination;

    [Tooltip("Auto-link: portals sharing the same non-empty ID pair up. Ignored when Destination is set manually.")]
    public string portalID = "";

    [Header("Spawning")]
    [Tooltip("How far in front of the destination portal the arriving object appears (meters).")]
    public float exitDistance = 1.2f;

    [Header("Behaviour")]
    [Tooltip("Layers allowed to travel through this portal. Everything by default.")]
    public LayerMask teleportLayers = ~0;

    [Tooltip("Seconds BEFORE an object can be teleported again. Prevents ping-ponging between the two portals.")]
    public float cooldown = 0.5f;

    [Tooltip("Also require the object to be moving into the portal (against the portal face) to activate it. " +
             "Leave OFF for the simplest walk-through behaviour.")]
    public bool requireForwardMotion = false;

    [Header("Auto-setup")]
    [Tooltip("If the object has no trigger collider, add a BoxCollider trigger auto-sized to its bounds (or 1m if none).")]
    public bool autoAddCollider = true;

    [Header("Screen Effect")]
    [Tooltip("When the PLAYER walks through the portal, the screen snaps to black and stays down this many seconds.")]
    public float blackoutTime = 3f;

    [Tooltip("After the blackout the screen comes back up but flickers for a random duration in this range (min–max seconds).")]
    public Vector2 flickerDurationRange = new Vector2(1f, 3f);

    [Tooltip("Only non-player objects (dropped items, props, enemies) travel with no screen effect.")]
    public bool playScreenEffect = true;

    // Cooldown bookkeeping per-object so 'this portal -> back again' is suppressed.
    private readonly Dictionary<Transform, float> _cooldowns = new Dictionary<Transform, float>();

    // Runtime-built full-screen black overlay used by the teleport effect.
    private Canvas _fxCanvas;
    private Image _fxOverlay;

    private void Start()
    {
        ResolveDestination();
        EnsureTriggerCollider();
    }

    /// <summary>Resolve which portal this one leads to (manual field first, then ID pairing).</summary>
    private void ResolveDestination()
    {
        if (destination != null) return;                 // manually wired, nothing to do
        if (string.IsNullOrWhiteSpace(portalID))
        {
            Debug.LogWarning($"[Portal] '{name}' has no Destination and no Portal ID — it leads nowhere.", this);
            return;
        }

        Portal[] all = FindObjectsByType<Portal>();
        foreach (Portal p in all)
        {
            if (p == this) continue;
            if (string.Equals(p.portalID, portalID, System.StringComparison.OrdinalIgnoreCase))
            {
                destination = p.transform;
                Debug.Log($"[Portal] '{name}' auto-linked to '{p.name}' via ID '{portalID}'.", this);
                return;
            }
        }
        Debug.LogWarning($"[Portal] '{name}' could not find another portal with ID '{portalID}'.", this);
    }

    /// <summary>Add a trigger collider if the portal doesn't have one, sized to the visuals.</summary>
    private void EnsureTriggerCollider()
    {
        if (!autoAddCollider) return;
        foreach (Collider c in GetComponentsInChildren<Collider>(true))
        {
            if (c.isTrigger) return;                    // already a trigger somewhere
        }

        BoxCollider box = gameObject.AddComponent<BoxCollider>();
        box.isTrigger = true;

        // Try to size the trigger loosely around the visuals so it's actually usable.
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        if (renderers.Length > 0)
        {
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            box.center = transform.InverseTransformPoint(b.center);
            box.size = transform.InverseTransformVector(b.size) + Vector3.one * 0.2f;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        TryTeleport(other);
    }

    private void TryTeleport(Collider other)
    {
        if (destination == null) return;

        Transform root = other.attachedRigidbody != null
            ? other.attachedRigidbody.transform
            : other.transform;

        // Walk up to a root object if this collider is nested deeper in a hierarchy.
        if (root.GetComponentInParent<CharacterController>() != null || root.GetComponentInParent<Rigidbody>() != null)
        {
            Transform parent = root.parent;
            while (parent != null &&
                   (parent.GetComponentInParent<CharacterController>() != null ||
                    parent.GetComponentInParent<Rigidbody>() != null))
            {
                root = parent;
                parent = root.parent;
            }
        }

        if (((1 << root.gameObject.layer) & teleportLayers.value) == 0) return;

        // Cooldown check — stop the same object bouncing straight back.
        if (_cooldowns.TryGetValue(root, out float until) && Time.time < until) return;

        // Optional: require motion into the portal face (prevents re-trigger from our spawn side).
        if (requireForwardMotion)
        {
            Vector3 dir = other.attachedRigidbody != null ? other.attachedRigidbody.linearVelocity : Vector3.zero;
            if (dir.sqrMagnitude < 0.04f) return;                            // not moving
            if (Vector3.Dot(dir.normalized, transform.forward) < 0.1f) return; // not moving into it
        }

        Teleport(root);
    }

    private void Teleport(Transform root)
    {
        _cooldowns[root] = Time.time + cooldown;

        Vector3 exitPos = destination.position + destination.forward * exitDistance;

        // CharacterController first: it must be disabled while you move the transform
        // or Unity fights you with its own internal collision resolution.
        CharacterController cc = root.GetComponentInParent<CharacterController>();
        if (cc != null)
        {
            cc.enabled = false;
            root.position = exitPos;
            // Duplicate the object's local rotation but re-point forward along the destination face.
            root.rotation = destination.rotation * Quaternion.Inverse(transform.rotation) * root.rotation;
            cc.enabled = true;

            // Snatch the controller's internal move so it doesn't carry the old offset.
            if (cc.height > 0f)
            {
                cc.Move(Vector3.zero);
            }
        }
        else
        {
            root.position = exitPos;
            root.rotation = destination.rotation * Quaternion.Inverse(transform.rotation) * root.rotation;

            // Keep rigidbody objects from sliding/falling weirdly right after arrival.
            Rigidbody rb = root.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }

        Debug.Log($"[Portal] '{name}' sent '{root.name}' to '{destination.name}'.", root);

        // Player only: snap the screen black, then flicker it back up. Ignored for
        // dropped items / props / enemies so they don't trigger the effect.
        if (playScreenEffect && IsPlayer(root) && gameObject.activeInHierarchy)
            StartFlickerEffect();
    }

    // ------------------------------------------------------------------ //
    //  Screen effect — blackout -> flicker -> normal (player only)       //
    // ------------------------------------------------------------------ //

    /// <summary>True when the teleported root carries a Camera (i.e. the first-person player).</summary>
    private static bool IsPlayer(Transform root)
    {
        return root.GetComponentInChildren<Camera>(true) != null;
    }

    /// <summary>Snap the screen black for blackoutTime, then flicker for 1–3 s, then clear.</summary>
    private void StartFlickerEffect()
    {
        StopAllCoroutines();
        StartCoroutine(FlickerEffectCoroutine());
    }

    private IEnumerator FlickerEffectCoroutine()
    {
        EnsureFX();
        _fxCanvas.gameObject.SetActive(true);

        // Phase 1: straight to full black and hold it down.
        SetOverlayAlpha(1f);
        yield return new WaitForSeconds(Mathf.Max(0f, blackoutTime));

        // Phase 2: the screen "comes back up" but flickers — rapidly toggling between
        // black and the scene for a random 1–3 second stretch.
        float flickerEnd = Time.time + Random.Range(flickerDurationRange.x, flickerDurationRange.y);
        while (Time.time < flickerEnd)
        {
            SetOverlayAlpha(Random.value > 0.5f ? 1f : 0f);
            yield return new WaitForSeconds(Random.Range(0.02f, 0.12f));
        }

        // Phase 3: settle to fully clear.
        SetOverlayAlpha(0f);
        _fxCanvas.gameObject.SetActive(false);
    }

    private void SetOverlayAlpha(float a)
    {
        if (_fxOverlay == null) return;
        Color c = _fxOverlay.color;
        c.a = Mathf.Clamp01(a);
        _fxOverlay.color = c;
    }

    /// <summary>Create the full-screen black overlay canvas once, lazily.</summary>
    private void EnsureFX()
    {
        if (_fxCanvas != null) return;

        GameObject cvGo = new GameObject("PortalFX_Blackout", typeof(Canvas), typeof(CanvasScaler));
        cvGo.transform.SetParent(transform, false);

        _fxCanvas = cvGo.GetComponent<Canvas>();
        _fxCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _fxCanvas.sortingOrder = 1000;          // on top of the in-game HUD

        CanvasScaler scaler = cvGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600f, 900f);

        GameObject imgGo = new GameObject("Blackout",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imgGo.layer = 5;
        imgGo.transform.SetParent(cvGo.transform, false);

        RectTransform rt = (RectTransform)imgGo.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        _fxOverlay = imgGo.GetComponent<Image>();
        _fxOverlay.color = new Color(0f, 0f, 0f, 0f);
        _fxOverlay.raycastTarget = false;       // don't block clicks with the overlay

        cvGo.SetActive(false);                  // hidden until a teleport happens
    }
}
