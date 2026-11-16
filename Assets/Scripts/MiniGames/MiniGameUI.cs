using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Sprites, font and colours the mini-games use. Everything is assigned on the
// MiniGameHub in the Inspector and reuses PAWcare's existing art; the
// "optional art" slots are drawn in code when left empty.
[Serializable]
public class MiniGameTheme
{
    public TMP_FontAsset font;
    public PetStageLibrary library;

    [Header("Backgrounds")]
    public Sprite menuBackground;
    public Sprite gameBackground;

    [Header("UI pieces (same as the home screen buttons)")]
    public Sprite panel;      // RoundedPanel
    public Sprite clayBody;
    public Sprite clayGloss;
    public Sprite clayShadow;
    public Sprite softCircle;
    public Sprite closeIcon;

    [Header("Icons")]
    public Sprite coin;
    public Sprite star;
    public Sprite heart;
    public Sprite paw;
    public Sprite food;
    public Sprite bowl;
    public Sprite ball;
    public Sprite comb;
    public Sprite medicine;
    public Sprite water;

    [Header("Optional art (drawn in code when empty)")]
    public Sprite flea;
    public Sprite poop;
    public Sprite meat;
    public Sprite apple;
    public Sprite bone;
    public Sprite rock;
    public Sprite box;
    public Sprite puddle;

    [Header("Colours")]
    public Color ink = new Color(0.42f, 0.26f, 0.15f);
    public Color cream = new Color(1f, 0.97f, 0.9f);
    public Color good = new Color(0.55f, 0.82f, 0.4f);
    public Color bad = new Color(0.93f, 0.4f, 0.35f);

    public void FillMissingArt()
    {
        if (flea == null) flea = MiniGameArt.Flea();
        if (poop == null) poop = MiniGameArt.Poop();
        if (meat == null) meat = MiniGameArt.Meat();
        if (apple == null) apple = MiniGameArt.Apple();
        if (bone == null) bone = MiniGameArt.Bone();
        if (rock == null) rock = MiniGameArt.Rock();
        if (box == null) box = MiniGameArt.Box();
        if (puddle == null) puddle = MiniGameArt.Puddle();
        if (softCircle == null) softCircle = MiniGameArt.Circle();
    }
}

// Small helpers that build UI in PAWcare's look (clay buttons, cream panels,
// brown text) so the mini-game screens match the home screen.
public static class MiniGameUI
{
    public static MiniGameTheme Theme { get; private set; }

    public static void Init(MiniGameTheme theme)
    {
        Theme = theme;
        theme.FillMissingArt();
    }

    public static RectTransform Rect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = 5; // UI
        RectTransform rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }

    public static RectTransform Stretch(RectTransform rt, float inset = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
        return rt;
    }

    public static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    public static Image Image(Transform parent, string name, Sprite sprite, Color color, bool raycast = false)
    {
        RectTransform rt = Rect(name, parent);
        Image img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = raycast;
        if (sprite != null && sprite.border != Vector4.zero)
            img.type = UnityEngine.UI.Image.Type.Sliced;
        else
            img.preserveAspect = true;
        return img;
    }

    public static TextMeshProUGUI Text(Transform parent, string name, string text, float size, Color color,
        TextAlignmentOptions align = TextAlignmentOptions.Center)
    {
        RectTransform rt = Rect(name, parent);
        TextMeshProUGUI t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (Theme != null && Theme.font != null)
            t.font = Theme.font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.overflowMode = TextOverflowModes.Overflow;
        return t;
    }

    // Cream card with the brown outline used across PAWcare.
    public static Image Panel(Transform parent, string name, Color fill, float outline = 5f)
    {
        Image border = Image(parent, name, Theme.panel, Theme.ink, true);
        Image inner = Image(border.transform, "Fill", Theme.panel, fill);
        Stretch(inner.rectTransform, outline);
        return border;
    }

    // Same layers as the home screen's care buttons: shadow, outline, clay fill, gloss.
    public static Button ClayButton(Transform parent, string name, string label, Color fill, Sprite icon,
        Vector2 size, Action onClick, float fontSize = 30f)
    {
        RectTransform root = Rect(name, parent);
        root.sizeDelta = size;
        Image hit = root.gameObject.AddComponent<Image>();
        hit.color = new Color(1f, 1f, 1f, 0f);

        Image shadow = Image(root, "Shadow", Theme.clayShadow, new Color(0.44f, 0.45f, 0.31f, 0.6f));
        Stretch(shadow.rectTransform);
        shadow.rectTransform.offsetMin = new Vector2(-6f, -12f);
        shadow.rectTransform.offsetMax = new Vector2(6f, 0f);

        Image outline = Image(root, "Outline", Theme.panel, Theme.ink);
        Stretch(outline.rectTransform);

        Image body = Image(root, "Fill", Theme.clayBody != null ? Theme.clayBody : Theme.panel, fill);
        Stretch(body.rectTransform, 4f);

        if (Theme.clayGloss != null)
        {
            Image gloss = Image(body.transform, "Gloss", Theme.clayGloss, new Color(1f, 1f, 1f, 0.5f));
            Stretch(gloss.rectTransform);
            gloss.rectTransform.offsetMin = new Vector2(10f, size.y * 0.45f);
            gloss.rectTransform.offsetMax = new Vector2(-10f, -6f);
        }

        float textX = 0f;
        if (icon != null)
        {
            float s = size.y * 0.6f;
            Image ic = Image(root, "Icon", icon, Color.white);
            bool withLabel = !string.IsNullOrEmpty(label);
            Place(ic.rectTransform, new Vector2(0.5f, 0.5f),
                new Vector2(withLabel ? -size.x * 0.5f + s * 0.5f + 18f : 0f, 2f), new Vector2(s, s));
            textX = withLabel ? s * 0.5f : 0f;
        }
        if (!string.IsNullOrEmpty(label))
        {
            TextMeshProUGUI t = Text(root, "Label", label, fontSize, Theme.ink);
            t.fontStyle = FontStyles.Bold;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            Stretch(t.rectTransform);
            t.rectTransform.offsetMin = new Vector2(textX, 4f);
        }

        Button button = root.gameObject.AddComponent<Button>();
        button.targetGraphic = body;
        button.transition = Selectable.Transition.None;
        Navigation nav = button.navigation;
        nav.mode = Navigation.Mode.None;
        button.navigation = nav;
        if (onClick != null)
            button.onClick.AddListener(() => onClick());
        root.gameObject.AddComponent<ButtonJuice>();
        return button;
    }

    // Small rounded "pill" with an icon and a number, like the coin counter.
    public static TextMeshProUGUI Pill(Transform parent, string name, Sprite icon, Vector2 anchor, Vector2 pos, float width)
    {
        Image p = Panel(parent, name, Theme.cream, 4f);
        p.raycastTarget = false;
        Place(p.rectTransform, anchor, pos, new Vector2(width, 76f));
        float textLeft = 20f;
        if (icon != null)
        {
            Image ic = Image(p.transform, "Icon", icon, Color.white);
            Place(ic.rectTransform, new Vector2(0f, 0.5f), new Vector2(44f, 0f), new Vector2(54f, 54f));
            textLeft = 78f;
        }
        TextMeshProUGUI t = Text(p.transform, "Value", "0", 36f, Theme.ink, TextAlignmentOptions.Left);
        t.fontStyle = FontStyles.Bold;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        Stretch(t.rectTransform);
        t.rectTransform.offsetMin = new Vector2(textLeft, 0f);
        t.rectTransform.offsetMax = new Vector2(-16f, 0f);
        return t;
    }
}
