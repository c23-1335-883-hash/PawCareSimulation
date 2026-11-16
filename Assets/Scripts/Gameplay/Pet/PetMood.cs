using System;
using UnityEngine;
using UnityEngine.UI;

public enum Mood
{
    Happy,
    Content,
    Hungry,
    Thirsty,
    Dirty,
    Tired,
    Sad,
    Sick
}

// Reads the pet's needs, decides its mood, and shows it:
// - Happy:   bouncier idle moves and little sparkles
// - Hungry / Thirsty: a thought icon (bowl / drop) and tummy rumbles
// - Dirty:   wavy stink lines and a fly buzzing around
// - Tired:   a moon thought icon, slow blinks and yawns
// - Sad:     droops, with a small rain cloud overhead
// - Sick:    droops, with a medicine thought icon (see PetHealth)
// The extra visuals are created at runtime next to the pet's other effects.
public class PetMood : MonoBehaviour
{
    [Header("References")]
    public PetController pet;
    public PetNeeds needs;
    [Tooltip("Optional; found in the scene when empty.")]
    public PetHealth health;

    [Header("Sprites")]
    public Sprite thoughtBubble;
    public Sprite foodIcon;
    public Sprite waterIcon;
    public Sprite moonIcon;
    public Sprite sickIcon;
    public Sprite stinkLine;
    public Sprite fly;
    public Sprite rainCloud;
    public Sprite dot;

    [Header("Rules")]
    [Tooltip("Average of all needs at or above this (with good mood) counts as happy.")]
    public float happyAbove = 70f;
    public Color stinkColor = new Color(0.62f, 0.56f, 0.28f, 0.9f);
    public Color cloudColor = new Color(0.62f, 0.66f, 0.78f);
    public Color rainColor = new Color(0.5f, 0.7f, 1f);

    public Mood Current { get; private set; } = Mood.Content;
    public event Action<Mood> MoodChanged;

    static readonly UIBurst.Style Rain = new UIBurst.Style { speed = new Vector2(60, 120), angle = new Vector2(260, 280), gravity = -900, life = new Vector2(0.35f, 0.5f), size = new Vector2(8, 13), endSize = 0.8f, spin = 0 };

    RectTransform thought;
    Image thoughtIcon;
    RectTransform[] stinks;
    RectTransform flyRt;
    RectTransform cloud;
    float thoughtScale;

    float nextCheck;
    float nextBlink;
    float nextYawn;
    float nextRumble;
    float nextSparkle;
    float nextRain;

    void Start()
    {
        if (health == null)
            health = FindFirstObjectByType<PetHealth>();
        Transform layer = pet.speechBubble != null ? pet.speechBubble.parent : pet.pet.parent;
        int below = pet.speechBubble != null ? pet.speechBubble.GetSiblingIndex() : layer.childCount;

        // The bubble art has its little tail circles at the bottom left; mirror it so
        // the tail points back at the pet, and mirror the icon back.
        thought = MakeImage("MoodThought", layer, thoughtBubble, new Vector2(104, 104), Color.white);
        RectTransform iconRt = MakeImage("Icon", thought, null, new Vector2(62, 62), Color.white);
        iconRt.localScale = new Vector3(-1f, 1f, 1f);
        thoughtIcon = iconRt.GetComponent<Image>();
        stinks = new RectTransform[3];
        for (int i = 0; i < stinks.Length; i++)
            stinks[i] = MakeImage("Stink", layer, stinkLine, new Vector2(44, 100), stinkColor);
        flyRt = MakeImage("Fly", layer, fly, new Vector2(46, 46), Color.white);
        cloud = MakeImage("RainCloud", layer, rainCloud, new Vector2(170, 100), cloudColor);

        foreach (RectTransform rt in new[] { thought, flyRt, cloud })
            rt.SetSiblingIndex(below);
        foreach (RectTransform rt in stinks)
            rt.SetSiblingIndex(below);

        HideAll();
        float now = Time.time;
        nextBlink = now + 2f;
        nextYawn = now + 6f;
        nextRumble = now + 5f;
    }

    void Update()
    {
        float now = Time.time;
        if (now >= nextCheck)
        {
            nextCheck = now + 0.5f;
            Mood mood = Decide();
            if (mood != Current)
            {
                Current = mood;
                MoodChanged?.Invoke(mood);
            }
        }

        bool resting = pet.State == PetState.Resting;
        pet.Droop = (Current == Mood.Sad || Current == Mood.Sick) && !resting ? 1f : 0f;
        pet.ExtraBouncy = Current == Mood.Happy;

        UpdateThought(resting);
        UpdateStink(resting, now);
        UpdateCloud(resting, now);
        Behave(resting, now);
    }

    Mood Decide()
    {
        if (needs == null)
            return Mood.Content;
        if (health != null && health.IsSick) return Mood.Sick;
        if (needs.IsLow(Need.Energy)) return Mood.Tired;
        if (needs.IsLow(Need.Hunger)) return Mood.Hungry;
        if (needs.IsLow(Need.Thirst)) return Mood.Thirsty;
        if (needs.IsLow(Need.Cleanliness)) return Mood.Dirty;
        if (needs.IsLow(Need.Happiness)) return Mood.Sad;

        float average = (needs.Get(Need.Hunger) + needs.Get(Need.Thirst) + needs.Get(Need.Cleanliness)
            + needs.Get(Need.Energy) + needs.Get(Need.Happiness)) / 5f;
        return average >= happyAbove && needs.Get(Need.Happiness) >= happyAbove ? Mood.Happy : Mood.Content;
    }

    // Blinks, yawns, rumbles and sparkles, depending on mood.
    void Behave(bool resting, float now)
    {
        if (resting)
            return;

        if (now >= nextBlink)
        {
            bool slow = Current == Mood.Tired;
            pet.Blink(slow);
            nextBlink = now + UnityEngine.Random.Range(FeelConfig.Current.blinkInterval.x, FeelConfig.Current.blinkInterval.y) * (slow ? 0.7f : 1f);
        }

        if (Current == Mood.Tired && now >= nextYawn && pet.IsIdle)
        {
            pet.Yawn();
            nextYawn = now + UnityEngine.Random.Range(10f, 16f);
        }

        if ((Current == Mood.Hungry || Current == Mood.Thirsty) && now >= nextRumble && pet.IsIdle)
        {
            pet.Rumble(Current == Mood.Hungry && UnityEngine.Random.value < 0.35f);
            nextRumble = now + UnityEngine.Random.Range(9f, 15f);
        }

        UIBurst fx = ReduceMotion.On ? null : UIBurst.Instance;
        if (Current == Mood.Happy && fx != null && now >= nextSparkle)
        {
            Vector3 at = pet.PetPoint(UnityEngine.Random.Range(0.15f, 0.85f), UnityEngine.Random.Range(0.5f, 0.95f));
            fx.Burst(at, fx.dot, 3, PetController.Sparkle, pet.sparkleColor, Color.white);
            nextSparkle = now + UnityEngine.Random.Range(2.5f, 4.5f);
        }
    }

    // ---- Thought bubble (hungry / thirsty / tired) ----

    void UpdateThought(bool resting)
    {
        Sprite icon = null;
        if (!resting)
        {
            if (Current == Mood.Hungry) icon = foodIcon;
            else if (Current == Mood.Thirsty) icon = waterIcon;
            else if (Current == Mood.Tired) icon = moonIcon;
            else if (Current == Mood.Sick) icon = sickIcon;
        }
        if (icon != null)
            thoughtIcon.sprite = icon;

        thoughtScale = Mathf.MoveTowards(thoughtScale, icon != null ? 1f : 0f, Time.deltaTime * 5f);
        thought.gameObject.SetActive(thoughtScale > 0.01f);
        if (!thought.gameObject.activeSelf)
            return;

        float bob = ReduceMotion.On ? 0f : Mathf.Sin(Time.time * 2f) * 6f;
        // Beside the head, on the left, clear of the speech bubble above.
        thought.position = pet.PetPoint(-0.12f, 0.82f);
        thought.anchoredPosition += new Vector2(0f, bob);
        float s = ReduceMotion.On ? 1f : Ease.OutBack(thoughtScale);
        thought.localScale = new Vector3(-s, s, 1f);
    }

    // ---- Stink lines and a fly (dirty) ----

    void UpdateStink(bool resting, float now)
    {
        bool show = Current == Mood.Dirty && !resting;
        for (int i = 0; i < stinks.Length; i++)
        {
            RectTransform s = stinks[i];
            s.gameObject.SetActive(show);
            if (!show)
                continue;
            float k = ReduceMotion.On ? 0.4f : (now * 0.45f + i / 3f) % 1f;
            s.position = pet.PetPoint(0.3f + i * 0.2f, 0.75f);
            s.anchoredPosition += new Vector2(Mathf.Sin(now * 3f + i) * 6f, 70f * k);
            Color c = stinkColor;
            c.a *= Mathf.Sin(k * Mathf.PI);
            s.GetComponent<Image>().color = c;
        }

        flyRt.gameObject.SetActive(show);
        if (!show)
            return;
        float a = ReduceMotion.On ? 0.5f : now * 2.2f;
        flyRt.position = pet.PetPoint(0.5f, 0.8f);
        flyRt.anchoredPosition += new Vector2(Mathf.Cos(a) * 130f, Mathf.Sin(a * 2f) * 35f + Mathf.Sin(now * 13f) * 3f);
        flyRt.localScale = new Vector3(Mathf.Sin(a) > 0f ? -1f : 1f, 1f, 1f);
    }

    // ---- Rain cloud (sad) ----

    void UpdateCloud(bool resting, float now)
    {
        bool show = Current == Mood.Sad && !resting;
        cloud.gameObject.SetActive(show);
        if (!show)
            return;
        cloud.position = pet.PetPoint(0.5f, 1.08f);
        cloud.anchoredPosition += new Vector2(0f, ReduceMotion.On ? 0f : Mathf.Sin(now * 1.5f) * 5f);

        UIBurst fx = ReduceMotion.On ? null : UIBurst.Instance;
        if (fx != null && now >= nextRain)
        {
            Vector3 at = cloud.TransformPoint(new Vector3(UnityEngine.Random.Range(-50f, 50f), -30f, 0f));
            fx.Burst(at, dot, 1, Rain, rainColor);
            nextRain = now + 0.18f;
        }
    }

    void HideAll()
    {
        thought.gameObject.SetActive(false);
        foreach (RectTransform s in stinks)
            s.gameObject.SetActive(false);
        flyRt.gameObject.SetActive(false);
        cloud.gameObject.SetActive(false);
    }

    RectTransform MakeImage(string name, Transform parent, Sprite sprite, Vector2 size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        var img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.preserveAspect = true;
        img.raycastTarget = false;
        return rt;
    }
}
