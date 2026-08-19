using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// On-screen sprint stamina / cooldown bar for the player. Built at runtime
/// (no art assets), just like the health HUD (see HealthBarUI / GameManager).
///
/// The bar drains while sprinting and refills during the cooldown. While the
/// stamina is recharging the label shows "COOLDOWN xx%" so the player knows
/// sprint is on cooldown; once full it reads "SPRINT READY".
///
/// Created automatically by FirstPersonController.Start() — no setup needed.
/// </summary>
public class SprintStaminaUI : MonoBehaviour
{
    private StarterAssets.FirstPersonController _controller;
    private Image _fill;
    private Text _label;
    private CanvasGroup _group;

    /// <summary>Make sure a stamina bar exists for this controller (no duplicates).</summary>
    public static void Ensure(StarterAssets.FirstPersonController controller)
    {
        foreach (SprintStaminaUI existing in FindObjectsByType<SprintStaminaUI>(FindObjectsInactive.Include))
            if (existing._controller == controller) return;

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

        GameObject go = new GameObject("SprintStaminaBar", typeof(RectTransform), typeof(CanvasGroup), typeof(SprintStaminaUI));
        go.layer = 5;
        go.transform.SetParent(canvas.transform, false);

        SprintStaminaUI ui = go.GetComponent<SprintStaminaUI>();
        ui._controller = controller;
        ui._group = go.GetComponent<CanvasGroup>();
        ui.Build();
    }

    // Pick a suitable HUD canvas (overlay preferred, like HealthBarUI does).
    private static Canvas FindHUDCanvas()
    {
        Canvas[] all = FindObjectsByType<Canvas>(FindObjectsInactive.Include);
        foreach (Canvas c in all)
            if (c.isActiveAndEnabled && c.renderMode == RenderMode.ScreenSpaceOverlay) return c;
        foreach (Canvas c in all)
            if (c.isActiveAndEnabled) return c;
        return null;
    }

    private void Build()
    {
        RectTransform root = (RectTransform)transform;
        // Bottom-right corner (the player health HUD stays bottom-left).
        root.anchorMin = new Vector2(1f, 0f);
        root.anchorMax = new Vector2(1f, 0f);
        root.pivot = new Vector2(1f, 0f);
        root.anchoredPosition = new Vector2(-20f, 52f);
        root.sizeDelta = new Vector2(280f, 22f);

        AddImage(root, "Track", new Color(0.05f, 0.08f, 0.12f, 0.95f), 0, 0, 1, 1, 0, 0, 0, 0);
        _fill = AddImage(root, "Fill", new Color(0.30f, 0.70f, 1f, 1f), 0, 0, 1, 1, 2, 2, -2, -2);

        GameObject labelGO = new GameObject("Label", typeof(RectTransform), typeof(Text));
        labelGO.layer = 5;
        labelGO.transform.SetParent(root, false);
        RectTransform lrt = labelGO.GetComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = Vector2.zero;
        lrt.offsetMax = Vector2.zero;
        _label = labelGO.GetComponent<Text>();
        _label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _label.fontSize = 13;
        _label.alignment = TextAnchor.MiddleCenter;
        _label.color = Color.white;
        _label.raycastTarget = false;

        _group.alpha = 0f; // hidden until gameplay starts
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

    private void Update()
    {
        if (_controller == null)
        {
            Destroy(gameObject);
            return;
        }

        // Only visible during actual gameplay (hidden in the menu / when dead).
        bool playing = GameManager.IsPlaying;
        if (_group != null) _group.alpha = playing ? 1f : 0f;

        float max = _controller.StaminaMax > 0f ? _controller.StaminaMax : 1f;
        float ratio = Mathf.Clamp01(_controller.Stamina / max);
        SetFill(ratio);

        if (_label != null)
        {
            if (ratio >= 0.999f) _label.text = "SPRINT READY";
            else if (_controller.IsSprinting) _label.text = "SPRINT " + Mathf.RoundToInt(ratio * 100f) + "%";
            else _label.text = "COOLDOWN " + Mathf.RoundToInt(ratio * 100f) + "%";
        }
    }

    private void SetFill(float ratio)
    {
        RectTransform rt = _fill.rectTransform;
        Vector2 oMin = rt.offsetMin;
        Vector2 oMax = rt.offsetMax;
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(ratio, 1f);
        rt.offsetMin = oMin;
        rt.offsetMax = oMax;
        _fill.color = Color.Lerp(new Color(0.15f, 0.40f, 0.80f, 1f), new Color(0.35f, 0.75f, 1f, 1f), ratio);
    }
}

