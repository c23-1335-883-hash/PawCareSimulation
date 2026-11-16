using System.Collections.Generic;
using UnityEngine;

// Draws the few small pictures the mini-games need that PAWcare doesn't have
// yet (fleas, a poop, rocks, boxes, puddles, bones, a drumstick, an apple).
// Each is a 128x128 sprite with the same brown outline as the UI. Drop a real
// sprite into the matching MiniGameHub slot to replace any of them.
public static class MiniGameArt
{
    const int Size = 128;
    static readonly Color Ink = new Color(0.42f, 0.26f, 0.15f);
    static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

    enum Kind { Circle, Ellipse, Box, Capsule }

    struct Shape
    {
        public Kind kind;
        public Vector2 a, b;   // centre / half-size (ellipse, box) or end points (capsule)
        public float r;        // radius (circle, capsule) or corner radius (box)
        public Color color;
    }

    static Shape C(float x, float y, float r, Color c) => new Shape { kind = Kind.Circle, a = new Vector2(x, y), r = r, color = c };
    static Shape E(float x, float y, float rx, float ry, Color c) => new Shape { kind = Kind.Ellipse, a = new Vector2(x, y), b = new Vector2(rx, ry), color = c };
    static Shape B(float x, float y, float hx, float hy, float r, Color c) => new Shape { kind = Kind.Box, a = new Vector2(x, y), b = new Vector2(hx, hy), r = r, color = c };
    static Shape L(float x1, float y1, float x2, float y2, float r, Color c) => new Shape { kind = Kind.Capsule, a = new Vector2(x1, y1), b = new Vector2(x2, y2), r = r, color = c };

    public static Sprite Flea()
    {
        Color body = new Color(0.45f, 0.26f, 0.14f), leg = new Color(0.32f, 0.19f, 0.1f);
        return Draw("Flea", new[]
        {
            L(42, 52, 18, 30, 5, leg), L(86, 52, 110, 30, 5, leg),
            L(40, 66, 14, 74, 5, leg), L(88, 66, 114, 74, 5, leg),
            L(46, 40, 30, 12, 5, leg), L(82, 40, 98, 12, 5, leg),
            E(64, 56, 30, 34, body), C(64, 96, 18, body),
        }, new[]
        {
            E(54, 64, 8, 12, new Color(1f, 1f, 1f, 0.25f)),
            C(57, 99, 6, Color.white), C(71, 99, 6, Color.white),
            C(58, 99, 3, Ink), C(72, 99, 3, Ink),
        });
    }

    public static Sprite Poop()
    {
        Color brown = new Color(0.55f, 0.34f, 0.2f);
        return Draw("Poop", new[]
        {
            E(64, 32, 52, 22, brown), E(64, 58, 40, 18, brown), E(64, 82, 26, 15, brown), L(62, 92, 74, 110, 7, brown),
        }, new[]
        {
            C(52, 60, 9, Color.white), C(76, 60, 9, Color.white), C(54, 59, 4.5f, Ink), C(78, 59, 4.5f, Ink),
            E(46, 36, 12, 5, new Color(1f, 1f, 1f, 0.25f)),
        });
    }

    public static Sprite Meat()
    {
        Color boneC = new Color(1f, 0.96f, 0.86f), meatC = new Color(0.82f, 0.4f, 0.26f);
        return Draw("Meat", new[]
        {
            L(70, 58, 104, 24, 9, boneC), C(110, 26, 11, boneC), C(100, 14, 11, boneC),
            E(54, 72, 40, 34, meatC),
        }, new[]
        {
            E(44, 82, 16, 10, new Color(1f, 0.75f, 0.6f, 0.6f)),
        });
    }

    public static Sprite Apple()
    {
        Color red = new Color(0.93f, 0.3f, 0.3f);
        return Draw("Apple", new[]
        {
            C(50, 56, 34, red), C(78, 56, 34, red), C(64, 40, 34, red),
            L(64, 86, 68, 108, 5, new Color(0.45f, 0.28f, 0.15f)),
            E(84, 100, 16, 9, new Color(0.5f, 0.78f, 0.35f)),
        }, new[]
        {
            E(44, 70, 10, 14, new Color(1f, 1f, 1f, 0.45f)),
        });
    }

    public static Sprite Bone()
    {
        Color c = new Color(1f, 0.97f, 0.88f);
        return Draw("Bone", new[]
        {
            L(34, 64, 94, 64, 13, c), C(28, 78, 17, c), C(28, 50, 17, c), C(100, 78, 17, c), C(100, 50, 17, c),
        }, new[]
        {
            L(40, 70, 88, 70, 3, new Color(1f, 1f, 1f, 0.8f)),
        });
    }

    public static Sprite Rock()
    {
        Color c = new Color(0.62f, 0.63f, 0.68f);
        return Draw("Rock", new[]
        {
            E(64, 48, 54, 38, c), C(48, 66, 28, c), C(80, 62, 24, c),
        }, new[]
        {
            E(50, 72, 14, 8, new Color(1f, 1f, 1f, 0.35f)), E(84, 38, 10, 5, new Color(0f, 0f, 0f, 0.12f)),
        });
    }

    public static Sprite Box()
    {
        Color c = new Color(0.86f, 0.64f, 0.4f);
        return Draw("Box", new[]
        {
            B(64, 58, 50, 46, 8, c),
        }, new[]
        {
            B(64, 58, 11, 46, 2, new Color(0.96f, 0.82f, 0.58f)),
            B(64, 94, 50, 3, 1, new Color(0.62f, 0.42f, 0.22f, 0.6f)),
            B(40, 40, 12, 7, 2, new Color(1f, 1f, 1f, 0.35f)),
        });
    }

    public static Sprite Puddle()
    {
        Color c = new Color(0.47f, 0.73f, 0.97f);
        return Draw("Puddle", new[]
        {
            E(64, 46, 58, 24, c), E(36, 56, 26, 15, c), E(92, 54, 24, 14, c),
        }, new[]
        {
            E(48, 52, 16, 5, new Color(1f, 1f, 1f, 0.6f)), E(84, 42, 8, 3, new Color(1f, 1f, 1f, 0.5f)),
        }, 4f);
    }

    public static Sprite Circle()
    {
        return Draw("SoftCircle", new[] { C(64, 64, 60, Color.white) }, null, 0f);
    }

    static Sprite Draw(string name, Shape[] shapes, Shape[] details, float outline = 6f)
    {
        if (cache.TryGetValue(name, out Sprite cached) && cached != null)
            return cached;

        var pixels = new Color[Size * Size];
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                Color col = new Color(0f, 0f, 0f, 0f);

                if (outline > 0f)
                {
                    float d = float.MaxValue;
                    foreach (Shape s in shapes)
                        d = Mathf.Min(d, Distance(s, p));
                    col = Over(col, Ink, Mathf.Clamp01(0.5f - (d - outline)));
                }
                foreach (Shape s in shapes)
                    col = Over(col, s.color, Mathf.Clamp01(0.5f - Distance(s, p)));
                if (details != null)
                {
                    foreach (Shape s in details)
                        col = Over(col, s.color, Mathf.Clamp01(0.5f - Distance(s, p)));
                }
                pixels[y * Size + x] = col;
            }
        }

        var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
        {
            name = name,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };
        tex.SetPixels(pixels);
        tex.Apply(false, true);
        Sprite sprite = Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f);
        sprite.name = name;
        cache[name] = sprite;
        return sprite;
    }

    // Signed distance in pixels (negative inside).
    static float Distance(Shape s, Vector2 p)
    {
        switch (s.kind)
        {
            case Kind.Circle:
                return (p - s.a).magnitude - s.r;
            case Kind.Ellipse:
            {
                Vector2 q = new Vector2((p.x - s.a.x) / s.b.x, (p.y - s.a.y) / s.b.y);
                return (q.magnitude - 1f) * Mathf.Min(s.b.x, s.b.y);
            }
            case Kind.Box:
            {
                Vector2 q = new Vector2(Mathf.Abs(p.x - s.a.x), Mathf.Abs(p.y - s.a.y)) - s.b + new Vector2(s.r, s.r);
                Vector2 outside = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f));
                return outside.magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - s.r;
            }
            default:
            {
                Vector2 pa = p - s.a, ba = s.b - s.a;
                float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
                return (pa - ba * h).magnitude - s.r;
            }
        }
    }

    static Color Over(Color dst, Color src, float coverage)
    {
        float a = src.a * coverage;
        if (a <= 0f)
            return dst;
        float outA = a + dst.a * (1f - a);
        Color rgb = (src * a + dst * dst.a * (1f - a)) / Mathf.Max(outA, 1e-5f);
        rgb.a = outA;
        return rgb;
    }
}
