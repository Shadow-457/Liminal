using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Minimal read-only health view used by any health bar.
/// Implemented by PlayerHealth and EnemyController.
/// </summary>
public interface IReadOnlyHealth
{
    int MaxHealth { get; }
    int CurrentHealth { get; }
    bool IsAlive { get; }
}

/// <summary>
/// A self-contained, animated health bar with NO art assets required.
///
/// Two usage modes:
///   - PLAYER HUD : call <see cref="CreatePlayerHUD"/>. It finds the scene Canvas,
///                  builds a screen-space bar (green->yellow->red, damage-trail, flash + shake).
///   - WORLD      : call <see cref="CreateWorld"/> to make a small floating bar that spawns
///                  above any tracked object (enemy/NPC) and always faces the camera.
///
/// The bar animates every frame:
///   - a fast green "fill" follows current health,
///   - a slow red "trail" shows the chunk of health you just lost,
///   - a white flash + small shake plays when you take a hit.
/// </summary>
public class HealthBarUI : MonoBehaviour
{
    [Header("Animation")]
    [Tooltip("How fast the green fill moves to the actual value.")]
    public float fillSpeed = 12f;
    [Tooltip("How fast the red damage-trail shrinks back down.")]
    public float trailSpeed = 2.4f;
    [Tooltip("Duration of the white hit-flash.")]
    public float flashDuration = 0.18f;
    [Tooltip("How hard the whole bar shakes when hit (world units).")]
    public float shakeAmount = 0.012f;
    [Tooltip("Below this health fraction the bar turns red; above that it's orange/green.")]
    [Range(0f, 1f)] public float lowThreshold = 0.29f;

    [Header("World bar (ignored for player HUD)")]
    [Tooltip("Height above the tracked object's origin the bar floats at.")]
    public float worldHeight = 2.2f;
    [Tooltip("World-space size of the bar, in metres.")]
    public Vector2 worldSize = new Vector2(2f, 0.24f);

    [Header("Manual wiring (optional)")]
    [Tooltip("Drop the HealthBarUI component on an empty object under a Canvas and drag the player's " +
             "PlayerHealth script here. It will build itself as a bottom-left HUD bar on Start.\n" +
             "Leave empty to rely on the automatic HUD bar instead.")]
    public PlayerHealth boundPlayerHealth;

    private IReadOnlyHealth _source;
    private bool _world;
    private bool _built;
    private Transform _billboardTarget;
    private Vector3 _billboardOffset;

    private RectTransform _root;
    private Image _track, _trail, _fill, _flash;
    private Text _label;

    private float _fillRatio = 1f;   // fast-moving green fill
    private float _trailRatio = 1f;  // slow-moving red trail
    private Coroutine _flashRoutine;
    private Coroutine _shakeRoutine;
    private Vector3 _baseLocalPos;
    private Vector3 _shakeOffset;
    private const float WorldScale = 0.01f; // matches CreateWorld localScale

    // If you dropped this component on a UI object under a Canvas by hand and wired
    // boundPlayerHealth in the Inspector, build it as a HUD bar automatically.
    void OnEnable()
    {
        if (_built || _world) return;
        if (boundPlayerHealth != null)
        {
            _source = boundPlayerHealth;
            BuildHUD();
            ResetBars();
            _built = true;
        }
    }

    /// <summary>True if a HUD bar already exists for <paramref name="source"/> (avoids duplicates).</summary>
    public static bool AlreadyBoundTo(IReadOnlyHealth source)
    {
        foreach (HealthBarUI hb in FindObjectsByType<HealthBarUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (ReferenceEquals(hb._source, source)) return true;
            if (source is PlayerHealth ph && ReferenceEquals(hb.boundPlayerHealth, ph)) return true;
        }
        return false;
    }

    /// <summary>
    /// Pick a canvas suitable for screen HUD. Prefers an active Screen-Space Overlay
    /// canvas (the normal HUD); falls back to any active canvas; returns null if none,
    /// in which case the caller creates one.
    /// </summary>
    public static Canvas FindHUDCanvas()
    {
        Canvas[] all = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Canvas c in all)
            if (c.isActiveAndEnabled && c.renderMode == RenderMode.ScreenSpaceOverlay)
                return c;
        foreach (Canvas c in all)
            if (c.isActiveAndEnabled)
                return c;
        return null;
    }

    // ------------------------------------------------------------------ //
    //  Static factory helpers                                            //
    // ------------------------------------------------------------------ //

    /// <summary>Build a HUD health bar for the player on the scene Canvas.</summary>
    public static void CreatePlayerHUD(IReadOnlyHealth source)
    {
        Canvas canvas = FindHUDCanvas();
        if (canvas == null)
        {
            GameObject hud = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            hud.layer = 5;
            var c = hud.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = hud.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            canvas = c;
        }

        GameObject go = new GameObject("PlayerHealthBar", typeof(RectTransform), typeof(HealthBarUI));
        go.layer = 5;
        go.transform.SetParent(canvas.transform, false);

        var bar = go.GetComponent<HealthBarUI>();
        bar._source = source;
        bar.BuildHUD();
        bar.ResetBars();
        bar._built = true;

        Debug.Log("[HealthBarUI] Player HUD bar created under '" + canvas.name + "'.");
    }

    /// <summary>Create a small floating bar above <paramref name="target"/> that faces the camera.</summary>
    public static HealthBarUI CreateWorld(IReadOnlyHealth source, Transform target, float heightOffset,
        Vector2? size = null)
    {
        GameObject canvasGO = new GameObject(target.name + "_HealthBar", typeof(RectTransform), typeof(Canvas), typeof(HealthBarUI));
        canvasGO.transform.SetParent(target, false);
        canvasGO.transform.localPosition = new Vector3(0f, heightOffset, 0f);
        canvasGO.transform.localRotation = Quaternion.identity;
        canvasGO.transform.localScale = Vector3.one * 0.01f; // 1 unit == 1 cm

        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 50;

        var bar = canvasGO.GetComponent<HealthBarUI>();
        bar._source = source;
        bar._world = true;
        bar._billboardTarget = target;
        bar._billboardOffset = canvasGO.transform.localPosition;
        bar.BuildWorld(size ?? new Vector2(2f, 0.24f));
        bar.ResetBars();
        bar._built = true;
        return bar;
    }

    // ------------------------------------------------------------------ //
    //  Building                                                          //
    // ------------------------------------------------------------------ //

    private void BuildHUD()
    {
        _root = (RectTransform)transform;
        // Pin the bar's bottom-left corner to the bottom-left of the screen.
        _root.anchorMin = Vector2.zero;
        _root.anchorMax = Vector2.zero;
        _root.pivot = Vector2.zero;                                // bar's origin = its bottom-left corner
        _root.anchoredPosition = new Vector2(20f, 20f);            // small margin from the corner
        _root.sizeDelta = new Vector2(280f, 26f);

        // Dark track (also acts as the border).
        _track = AddImage(_root, "Track", new Color(0.05f, 0.05f, 0.08f, 0.95f), 0, 0, 1, 1, 0, -0, 0, -0);
        _trail = AddImage(_root, "Trail", new Color(0.85f, 0.10f, 0.10f, 0.95f), 0, 0, 1, 1, 4, 4, -4, -4);
        _fill = AddImage(_root, "Fill", new Color(0.30f, 0.95f, 0.35f, 1f), 0, 0, 1, 1, 4, 4, -4, -4);
        _flash = AddImage(_root, "Flash", new Color(1f, 1f, 1f, 0f), 0, 0, 1, 1, 4, 4, -4, -4);

        // "HP 100/100" label above the bar.
        GameObject labelGO = new GameObject("HPText", typeof(RectTransform), typeof(Text));
        labelGO.transform.SetParent(_root, false);
        labelGO.layer = 5;
        RectTransform lrt = labelGO.GetComponent<RectTransform>();
        lrt.anchorMin = new Vector2(0f, 1f);
        lrt.anchorMax = new Vector2(1f, 1f);
        lrt.pivot = new Vector2(0f, 0f);
        lrt.anchoredPosition = new Vector2(2f, 3f);
        lrt.sizeDelta = new Vector2(0f, 16f);
        _label = labelGO.GetComponent<Text>();
        _label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _label.fontSize = 13;
        _label.color = Color.white;
        _label.alignment = TextAnchor.UpperLeft;
        _label.raycastTarget = false;
    }

    private void BuildWorld(Vector2 size)
    {
        _root = (RectTransform)transform;
        // 100 units (at a 0.01 global scale) == the requested metres.
        _root.sizeDelta = new Vector2(size.x * 100f, size.y * 100f);
        _root.anchorMin = new Vector2(0.5f, 0.5f);
        _root.anchorMax = new Vector2(0.5f, 0.5f);
        _root.pivot = new Vector2(0.5f, 0.5f);
        _root.anchoredPosition = Vector2.zero;

        // Outer outline, then the moving track/trail/fill, then hit-flash.
        Image outline = AddImage(_root, "Outline", new Color(0.02f, 0.02f, 0.04f, 0.9f), 0, 0, 1, 1, -1, -1, 1, 1);
        _track = AddImage(_root, "Track", new Color(0.08f, 0.05f, 0.05f, 0.95f), 0, 0, 1, 1, 2, 2, -2, -2);
        _trail = AddImage(_root, "Trail", new Color(0.85f, 0.10f, 0.10f, 0.95f), 0, 0, 1, 1, 3, 3, -3, -3);
        _fill = AddImage(_root, "Fill", new Color(0.30f, 0.95f, 0.35f, 1f), 0, 0, 1, 1, 3, 3, -3, -3);
        _flash = AddImage(_root, "Flash", new Color(1f, 1f, 1f, 0f), 0, 0, 1, 1, 3, 3, -3, -3);
        outline.rectTransform.SetAsFirstSibling();
    }

    private Image AddImage(RectTransform parent, string name, Color color,
        float aMinX, float aMinY, float aMaxX, float aMaxY,
        float oMinX, float oMinY, float oMaxX, float oMaxY)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(aMinX, aMinY);
        rt.anchorMax = new Vector2(aMaxX, aMaxY);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.offsetMin = new Vector2(oMinX, oMinY);
        rt.offsetMax = new Vector2(oMaxX, oMaxY);
        Image img = go.GetComponent<Image>();
        img.sprite = SharedWhite();
        img.type = Image.Type.Simple;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    // ------------------------------------------------------------------ //
    //  Runtime                                                            //
    // ------------------------------------------------------------------ //

    private void ResetBars()
    {
        _baseLocalPos = transform.localPosition;
        if (_source != null && _source.MaxHealth > 0)
        {
            float r = (float)_source.CurrentHealth / _source.MaxHealth;
            _fillRatio = r;
            _trailRatio = r;
        }
    }
private void LateUpdate()
    {
        if (_source == null) return;
        if (_world)
        {
            Billboard();
        }
        else
        {
            transform.localPosition = _baseLocalPos + _shakeOffset;
        }
        UpdateBar();
    }

    // Keep a floating bar exactly above its target and facing the camera.
    private void Billboard()
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        Vector3 basePos = _billboardTarget != null ? _billboardTarget.position + _billboardOffset : transform.position;
        transform.position = basePos + new Vector3(_shakeOffset.x, _shakeOffset.y, 0f) * WorldScale;
        Vector3 toCam = transform.position - cam.transform.position;
        if (toCam.sqrMagnitude < 1e-4f) toCam = Vector3.back;
        transform.rotation = Quaternion.LookRotation(toCam, Vector3.up);
    }

    private void UpdateBar()
    {
        if (_source.MaxHealth <= 0) return;
        float target = (float)_source.CurrentHealth / _source.MaxHealth;

        bool damaged = target < _fillRatio - 0.0005f;
        if (damaged)
        {
            if (_flash != null && _flashRoutine == null) _flashRoutine = StartCoroutine(FlashRoutine());
            if (shakeAmount > 0f && _shakeRoutine == null) _shakeRoutine = StartCoroutine(ShakeRoutine());
        }

        // Fast green fill.
        float fill = Mathf.Lerp(_fillRatio, target, fillSpeed * Time.deltaTime);
        if (Mathf.Abs(fill - target) < 0.002f) fill = target;
        _fillRatio = fill;

        // Slow red trail catches up from above.
        if (_trailRatio < _fillRatio) _trailRatio = _fillRatio;
        if (_trailRatio > target)
            _trailRatio = Mathf.Max(_fillRatio, _trailRatio - trailSpeed * Time.deltaTime);
        if (Mathf.Abs(_trailRatio - target) < 0.002f) _trailRatio = target;

        if (_fill != null)
        {
            SetRatio(_fill, _fillRatio);
            _fill.color = HealthColor(_fillRatio);
        }
        if (_trail != null) SetRatio(_trail, Mathf.Max(_trailRatio, _fillRatio));
        if (_label != null)
            _label.text = $"{Mathf.Max(0, _source.CurrentHealth)} / {_source.MaxHealth}";
    }

    private void SetRatio(Image img, float ratio)
    {
        ratio = Mathf.Clamp01(ratio);
        RectTransform rt = img.rectTransform;
        Vector2 oMin = rt.offsetMin;
        Vector2 oMax = rt.offsetMax;
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(ratio, 1f);
        rt.offsetMin = oMin;
        rt.offsetMax = oMax;
    }

    private Color HealthColor(float ratio)
    {
        if (ratio >= 0.55f)
            return Color.Lerp(new Color(0.85f, 0.85f, 0.15f, 1f), new Color(0.30f, 0.95f, 0.35f, 1f),
                Mathf.InverseLerp(0.55f, 1f, ratio));
        if (ratio >= lowThreshold)
            return new Color(0.95f, 0.62f, 0.10f, 1f);
        return new Color(0.90f, 0.12f, 0.12f, 1f);
    }

    private IEnumerator FlashRoutine()
    {
        if (_flash == null) yield break;
        Color c = _flash.color;
        float t = 0f;
        while (t < flashDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / flashDuration);
            c.a = (1f - p) * 0.85f;
            _flash.color = c;
            yield return null;
        }
        c.a = 0f;
        _flash.color = c;
        _flashRoutine = null;
    }

    private IEnumerator ShakeRoutine()
    {
        const float dur = 0.22f;
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float decay = 1f - t / dur;
            float amp = shakeAmount * 100f * decay; // ~cm in bar space
            _shakeOffset = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), 0f) * amp;
            yield return null;
        }
        _shakeOffset = Vector3.zero;
        _shakeRoutine = null;
    }

    // Shared 1-pixel white sprite for all bar Images (no import needed).
    private static Sprite _sharedWhite;
    private static Sprite SharedWhite()
    {
        if (_sharedWhite != null) return _sharedWhite;
        Texture2D tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.hideFlags = HideFlags.HideAndDontSave;
        tex.SetPixel(0, 0, Color.white);
        tex.Apply(false);
        _sharedWhite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 100f);
        _sharedWhite.hideFlags = HideFlags.HideAndDontSave;
        return _sharedWhite;
    }
}