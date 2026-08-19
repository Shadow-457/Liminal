using UnityEngine;
using UnityEngine.UI;

// Editor-time helper: right-click this component in the Inspector and choose
// "Build Inventory UI" (Context Menu) to auto-generate a modern hotbar: a dark
// translucent panel behind the slots, a thin gold accent bar, key-number labels
// and a gold border on the currently selected slot.
//
// Usage:
// 1. Create an empty GameObject under your Canvas, name it "InventoryUI".
// 2. Add this component to it.
// 3. Right-click the component header -> "Build Inventory UI".
// 4. It creates slots wired to InventoryManager (assumes InventoryManager exists in scene).
//
// Tip: if you want the built-in colors, right-click the component and press
// "Reset" first, then build.
public class InventoryUIBuilder : MonoBehaviour
{
    [Header("Layout")]
    public int slotCountToBuild = 4;
    public Vector2 slotSize = new Vector2(90, 90);
    public float spacing = 6f;
    public float borderThickness = 4f;

    [Header("Slot Colors")]
    [Tooltip("Frame/border of each slot. Tinted gold via selectedColor while active.")]
    public Color borderColor = new Color(0.16f, 0.17f, 0.20f, 0.95f); // slate frame
    [Tooltip("Inner fill of each slot.")]
    public Color slotColor = new Color(0.07f, 0.08f, 0.11f, 0.96f);   // dark fill

    [Tooltip("Border color given to the slot that is currently selected/active.")]
    public Color selectedColor = new Color(1f, 0.84f, 0f, 1f); // gold frame

    [Header("Panel")]
    public bool showInventoryPanel = true;
    public float panelPadding = 14f;
    public Color panelColor = new Color(0.02f, 0.03f, 0.05f, 0.82f);
    public bool showAccentBar = true;
    public float accentBarHeight = 3f;
    public Color accentBarColor = new Color(1f, 0.84f, 0f, 0.55f);

    [Header("Labels")]
    public bool showSlotNumbers = true;
    public Color slotNumberColor = new Color(0.58f, 0.61f, 0.66f, 0.85f);

    [Header("Player Wiring")]
    public PlayerInteractor playerInteractor;

    [ContextMenu("Build Inventory UI")]
    public void BuildUI()
    {
        // Clear existing children first.
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
#if UNITY_EDITOR
            DestroyImmediate(transform.GetChild(i).gameObject);
#else
            Destroy(transform.GetChild(i).gameObject);
#endif
        }

        var rowLayout = gameObject.GetComponent<HorizontalLayoutGroup>();
        if (rowLayout == null) rowLayout = gameObject.AddComponent<HorizontalLayoutGroup>();
        rowLayout.spacing = spacing;
        rowLayout.childForceExpandWidth = false;
        rowLayout.childForceExpandHeight = false;
        rowLayout.childAlignment = TextAnchor.MiddleCenter;

        var fitter = gameObject.GetComponent<ContentSizeFitter>();
        if (fitter == null) fitter = gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        for (int i = 0; i < slotCountToBuild; i++)
        {
            CreateSlot(i);
        }

        if (showInventoryPanel) CreateInventoryPanel();
    }

    // A dark translucent panel behind the slots, with an optional accent line.
    private void CreateInventoryPanel()
    {
        GameObject panelGO = new GameObject("InventoryPanel", typeof(RectTransform), typeof(Image));
        panelGO.transform.SetParent(transform, false);

        RectTransform panelRT = panelGO.GetComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(0.5f, 0.5f);
        panelRT.anchorMax = new Vector2(0.5f, 0.5f);
        panelRT.anchoredPosition = Vector2.zero;
        panelRT.sizeDelta = new Vector2(
            slotCountToBuild * slotSize.x + (slotCountToBuild - 1) * spacing + panelPadding * 2f,
            slotSize.y + panelPadding * 2f);

        // Keep it out of the horizontal layout so it never becomes a slot.
        var panelLayout = panelGO.AddComponent<LayoutElement>();
        panelLayout.ignoreLayout = true;

        Image panelImg = panelGO.GetComponent<Image>();
        panelImg.color = panelColor;
        panelImg.raycastTarget = false;

        panelGO.transform.SetAsFirstSibling(); // render behind the slots

        if (showAccentBar)
        {
            GameObject barGO = new GameObject("Accent", typeof(RectTransform), typeof(Image));
            barGO.transform.SetParent(panelGO.transform, false);
            RectTransform barRT = barGO.GetComponent<RectTransform>();
            barRT.anchorMin = new Vector2(0, 0);
            barRT.anchorMax = new Vector2(1, 0);
            barRT.pivot = new Vector2(0.5f, 0.5f);
            barRT.anchoredPosition = Vector2.zero;
            barRT.sizeDelta = new Vector2(0f, accentBarHeight);
            Image barImg = barGO.GetComponent<Image>();
            barImg.color = accentBarColor;
            barImg.raycastTarget = false;
        }
    }

    private void CreateSlot(int index)
    {
        // --- Frame (outer; clickable, and doubles as the gold selection border) ---
        GameObject frameGO = new GameObject($"Slot_{index}", typeof(RectTransform), typeof(Image));
        frameGO.transform.SetParent(transform, false);
        RectTransform frameRT = frameGO.GetComponent<RectTransform>();
        frameRT.sizeDelta = slotSize;
        Image frameImg = frameGO.GetComponent<Image>();
        frameImg.color = borderColor;
        frameImg.raycastTarget = true;

        var le = frameGO.AddComponent<LayoutElement>();
        le.preferredWidth = slotSize.x;
        le.preferredHeight = slotSize.y;

        // --- Fill (inner dark slot) ---
        GameObject fillGO = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fillGO.transform.SetParent(frameGO.transform, false);
        RectTransform fillRT = fillGO.GetComponent<RectTransform>();
        fillRT.anchorMin = Vector2.zero;
        fillRT.anchorMax = Vector2.one;
        fillRT.offsetMin = new Vector2(borderThickness, borderThickness);
        fillRT.offsetMax = new Vector2(-borderThickness, -borderThickness);
        Image fillImg = fillGO.GetComponent<Image>();
        fillImg.color = slotColor;
        fillImg.raycastTarget = false;

        // --- Icon ---
        GameObject iconGO = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconGO.transform.SetParent(fillGO.transform, false);
        RectTransform iconRT = iconGO.GetComponent<RectTransform>();
        iconRT.anchorMin = new Vector2(0.1f, 0.1f);
        iconRT.anchorMax = new Vector2(0.9f, 0.9f);
        iconRT.offsetMin = Vector2.zero;
        iconRT.offsetMax = Vector2.zero;
        Image iconImg = iconGO.GetComponent<Image>();
        iconImg.enabled = false;
        iconImg.preserveAspect = true;
        iconImg.raycastTarget = false;

        // --- Quantity Text (bottom-right) ---
        GameObject textGO = new GameObject("Quantity", typeof(RectTransform), typeof(Text));
        textGO.transform.SetParent(fillGO.transform, false);
        RectTransform textRT = textGO.GetComponent<RectTransform>();
        textRT.anchorMin = new Vector2(0.05f, 0.05f);
        textRT.anchorMax = new Vector2(0.95f, 0.32f);
        textRT.offsetMin = Vector2.zero;
        textRT.offsetMax = Vector2.zero;
        Text qtyText = textGO.GetComponent<Text>();
        qtyText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        qtyText.alignment = TextAnchor.LowerRight;
        qtyText.color = Color.white;
        qtyText.fontSize = Mathf.Max(14, Mathf.RoundToInt(slotSize.y * 0.22f));
        qtyText.text = "";

        // --- Key number label (top-left, e.g. "1" "2" "3" "4") ---
        if (showSlotNumbers)
        {
            GameObject numGO = new GameObject("KeyNumber", typeof(RectTransform), typeof(Text));
            numGO.transform.SetParent(frameGO.transform, false);
            RectTransform numRT = numGO.GetComponent<RectTransform>();
            numRT.anchorMin = new Vector2(0, 1);
            numRT.anchorMax = new Vector2(0, 1);
            numRT.pivot = new Vector2(0, 1);
            numRT.anchoredPosition = new Vector2(slotSize.x * 0.04f, -slotSize.y * 0.05f);
            numRT.sizeDelta = new Vector2(slotSize.x * 0.3f, slotSize.y * 0.22f);
            Text numText = numGO.GetComponent<Text>();
            numText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            numText.alignment = TextAnchor.UpperLeft;
            numText.color = slotNumberColor;
            numText.fontSize = Mathf.Max(11, Mathf.RoundToInt(slotSize.y * 0.2f));
            numText.text = (index + 1).ToString();
            numText.raycastTarget = false;
        }

        // --- Wire up the InventorySlotUI script on the frame ---
        InventorySlotUI slotUI = frameGO.AddComponent<InventorySlotUI>();
        slotUI.slotIndex = index;
        slotUI.iconImage = iconImg;
        slotUI.quantityText = qtyText;
        slotUI.playerInteractor = playerInteractor;
        slotUI.selectedBorderColor = selectedColor;
    }
}