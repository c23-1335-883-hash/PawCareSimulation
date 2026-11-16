using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Lightweight UI particle effects (confetti, hearts, crumbs, bubbles, droplets),
// flying items, and floating "+10" text. Everything comes from small pools
// that are reused, so nothing is created or destroyed while playing.
// Put one on a full-screen object at the top of a scene's Canvas.
public class UIBurst : MonoBehaviour
{
    [Serializable]
    public struct Style
    {
        public Vector2 speed;      // min/max start speed (canvas units per second)
        public Vector2 angle;      // min/max direction in degrees (90 = straight up)
        public float gravity;      // negative pulls down
        public Vector2 life;       // min/max seconds
        public Vector2 size;       // min/max start size
        public float endSize;      // size multiplier at the end of life
        public float spin;         // max degrees per second
    }

    public static UIBurst Instance { get; private set; }

    [Header("Sprites")]
    public Sprite paw;
    public Sprite heart;
    public Sprite dot;
    public Sprite confetti;

    [Header("Text")]
    public TMP_FontAsset font;
    public Color textColor = new Color(0.42f, 0.26f, 0.15f);

    public int poolSize = 48;

    public static readonly Style Confetti = new Style { speed = new Vector2(350, 700), angle = new Vector2(50, 130), gravity = -900, life = new Vector2(0.9f, 1.4f), size = new Vector2(16, 26), endSize = 0.8f, spin = 540 };
    public static readonly Style Rise = new Style { speed = new Vector2(60, 140), angle = new Vector2(70, 110), gravity = 40, life = new Vector2(0.9f, 1.4f), size = new Vector2(26, 46), endSize = 1.15f, spin = 30 };
    public static readonly Style Drops = new Style { speed = new Vector2(200, 380), angle = new Vector2(20, 160), gravity = -1100, life = new Vector2(0.45f, 0.7f), size = new Vector2(10, 18), endSize = 0.6f, spin = 0 };

    class Particle
    {
        public RectTransform rt;
        public Image image;
        public Vector2 velocity;
        public float gravity, life, age, spin, startSize, endSize;
        public Color color;
        public bool flying;
        public Vector2 from, to;
        public Action onArrive;
    }

    class FloatText
    {
        public RectTransform rt;
        public TextMeshProUGUI label;
        public Image icon;
        public float age;
        public Vector2 start;
    }

    readonly List<Particle> pool = new List<Particle>();
    readonly List<FloatText> texts = new List<FloatText>();
    RectTransform layer;

    void Awake()
    {
        Instance = this;
        layer = (RectTransform)transform;
        for (int i = 0; i < poolSize; i++)
            pool.Add(NewParticle());
        for (int i = 0; i < 4; i++)
            texts.Add(NewText());
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // ---- Public API (positions are world positions, e.g. someRect.position) ----

    public void Burst(Vector3 worldPos, Sprite sprite, int count, Style style, params Color[] colors)
    {
        Vector2 local = ToLocal(worldPos);
        count = Mathf.Max(1, Mathf.RoundToInt(count * FeelConfig.Current.particleAmount));
        for (int i = 0; i < count; i++)
        {
            Particle p = Take();
            if (p == null)
                return;
            float angle = UnityEngine.Random.Range(style.angle.x, style.angle.y) * Mathf.Deg2Rad;
            float speed = UnityEngine.Random.Range(style.speed.x, style.speed.y);
            p.velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
            p.gravity = style.gravity;
            p.life = UnityEngine.Random.Range(style.life.x, style.life.y);
            p.startSize = UnityEngine.Random.Range(style.size.x, style.size.y);
            p.endSize = style.endSize;
            p.spin = UnityEngine.Random.Range(-style.spin, style.spin);
            p.color = colors.Length > 0 ? colors[UnityEngine.Random.Range(0, colors.Length)] : Color.white;
            Spawn(p, sprite, local);
        }
    }

    // Moves one item from one place to another along a small arc.
    public void Fly(Vector3 fromWorld, Vector3 toWorld, Sprite sprite, Color color, float size, float time, Action onArrive)
    {
        Particle p = Take();
        if (p == null)
        {
            onArrive?.Invoke();
            return;
        }
        p.flying = true;
        p.from = ToLocal(fromWorld);
        p.to = ToLocal(toWorld);
        p.life = time;
        p.startSize = size;
        p.endSize = 0.7f;
        p.color = color;
        p.onArrive = onArrive;
        Spawn(p, sprite, p.from);
    }

    // Floating text such as "+10" with an optional icon, rising and fading.
    public void PopText(Vector3 worldPos, string text, Sprite icon = null)
    {
        FloatText f = texts.Find(x => !x.rt.gameObject.activeSelf) ?? texts[0];
        f.label.text = text;
        f.icon.sprite = icon;
        f.icon.gameObject.SetActive(icon != null);
        f.start = ToLocal(worldPos);
        f.age = 0f;
        f.rt.anchoredPosition = f.start;
        f.rt.gameObject.SetActive(true);
        f.rt.SetAsLastSibling();
    }

    // ---- Simulation ----

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        foreach (Particle p in pool)
        {
            if (!p.rt.gameObject.activeSelf)
                continue;
            p.age += dt;
            float k = Mathf.Clamp01(p.age / p.life);
            if (p.flying)
            {
                Vector2 pos = Vector2.Lerp(p.from, p.to, Ease.OutQuad(k));
                pos.y += Mathf.Sin(k * Mathf.PI) * 90f;
                p.rt.anchoredPosition = pos;
            }
            else
            {
                p.velocity.y += p.gravity * dt;
                p.rt.anchoredPosition += p.velocity * dt;
                p.rt.localRotation *= Quaternion.Euler(0f, 0f, p.spin * dt);
            }
            float size = p.startSize * Mathf.Lerp(1f, p.endSize, k);
            p.rt.sizeDelta = new Vector2(size, size);
            Color c = p.color;
            c.a *= p.flying ? 1f : 1f - k * k;
            p.image.color = c;

            if (k >= 1f)
            {
                p.rt.gameObject.SetActive(false);
                Action done = p.onArrive;
                p.onArrive = null;
                done?.Invoke();
            }
        }

        foreach (FloatText f in texts)
        {
            if (!f.rt.gameObject.activeSelf)
                continue;
            f.age += dt;
            float k = Mathf.Clamp01(f.age / 1.1f);
            f.rt.anchoredPosition = f.start + new Vector2(0f, 110f * Ease.OutQuad(k));
            float pop = k < 0.15f ? Ease.OutBack(k / 0.15f, 2.5f) : 1f;
            f.rt.localScale = Vector3.one * pop;
            float alpha = k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f;
            Color c = textColor; c.a = alpha; f.label.color = c;
            f.icon.color = new Color(1f, 1f, 1f, alpha);
            if (k >= 1f)
                f.rt.gameObject.SetActive(false);
        }
    }

    // ---- Helpers ----

    Vector2 ToLocal(Vector3 worldPos)
    {
        return layer.InverseTransformPoint(worldPos);
    }

    Particle Take()
    {
        foreach (Particle p in pool)
        {
            if (!p.rt.gameObject.activeSelf)
                return p;
        }
        return null;
    }

    void Spawn(Particle p, Sprite sprite, Vector2 local)
    {
        p.age = 0f;
        p.image.sprite = sprite;
        p.rt.anchoredPosition = local;
        p.rt.localRotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(0f, 360f) * (p.flying ? 0f : 1f));
        p.rt.sizeDelta = new Vector2(p.startSize, p.startSize);
        p.image.color = p.color;
        p.rt.gameObject.SetActive(true);
        p.rt.SetAsLastSibling();
    }

    Particle NewParticle()
    {
        var go = new GameObject("Particle", typeof(RectTransform));
        go.layer = gameObject.layer;
        go.transform.SetParent(transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        var img = go.AddComponent<Image>();
        img.raycastTarget = false;
        img.preserveAspect = true;
        go.SetActive(false);
        return new Particle { rt = rt, image = img };
    }

    FloatText NewText()
    {
        var go = new GameObject("FloatText", typeof(RectTransform));
        go.layer = gameObject.layer;
        go.transform.SetParent(transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(220, 70);

        var labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.layer = go.layer;
        labelGo.transform.SetParent(rt, false);
        var lrt = (RectTransform)labelGo.transform;
        lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one; lrt.offsetMin = Vector2.zero; lrt.offsetMax = new Vector2(-60, 0);
        var label = labelGo.AddComponent<TextMeshProUGUI>();
        if (font != null) label.font = font;
        label.fontSize = 48;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.MidlineRight;
        label.outlineWidth = 0.2f;
        label.outlineColor = new Color32(255, 250, 240, 255);
        label.raycastTarget = false;

        var iconGo = new GameObject("Icon", typeof(RectTransform));
        iconGo.layer = go.layer;
        iconGo.transform.SetParent(rt, false);
        var irt = (RectTransform)iconGo.transform;
        irt.anchorMin = irt.anchorMax = new Vector2(1f, 0.5f); irt.pivot = new Vector2(1f, 0.5f);
        irt.sizeDelta = new Vector2(52, 52); irt.anchoredPosition = Vector2.zero;
        var icon = iconGo.AddComponent<Image>();
        icon.raycastTarget = false;
        icon.preserveAspect = true;

        go.SetActive(false);
        return new FloatText { rt = rt, label = label, icon = icon };
    }
}
