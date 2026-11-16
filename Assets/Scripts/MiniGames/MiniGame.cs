using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Base for every mini-game. A mini-game is a light component on the
// MiniGameHub object: the hub owns the menu, HUD, countdown, timer, result
// screen and rewards; the game only builds its objects inside `area`,
// reacts to input and calls AddScore / EndGame.
public abstract class MiniGame : MonoBehaviour
{
    // Save key for the best score; never change it once players have scores.
    public abstract string Id { get; }
    public abstract string Title { get; }
    public abstract string Description { get; }
    public abstract Sprite Icon { get; }
    public virtual Color CardColor => new Color(1f, 0.85f, 0.6f);
    public virtual string HowToPlay => Description;
    public virtual string ResultTitle(bool timeUp) => timeUp ? "TIME'S UP!" : "GAME OVER";
    // Short "how long is a round" chip on the menu card.
    public virtual string RoundInfo => TimeLimit > 0f ? Mathf.RoundToInt(TimeLimit) + " sec"
        : StartLives > 0 ? StartLives + " lives" : "No timer";

    // Seconds per round; 0 means no countdown timer (the HUD shows time played).
    public virtual float TimeLimit => 0f;
    // Lives shown in the HUD; 0 hides the hearts.
    public virtual int StartLives => 0;

    [Tooltip("Score needed for each coin earned.")]
    public int scorePerCoin = 10;
    [Tooltip("Most coins one round can give.")]
    public int maxCoins = 20;

    public int Score { get; private set; }
    public bool Running { get; private set; }

    protected MiniGameHub hub;
    protected RectTransform area;
    protected MiniGameTheme Theme => hub.theme;
    protected Rect Bounds => area.rect;
    protected float Elapsed => hub.Elapsed;

    public virtual int CoinsFor(int score)
    {
        if (score <= 0)
            return 0;
        return Mathf.Clamp(Mathf.CeilToInt(score / (float)Mathf.Max(1, scorePerCoin)), 1, maxCoins);
    }

    public void Setup(MiniGameHub owner)
    {
        hub = owner;
        enabled = false;
    }

    // Called by the hub before the countdown: build the playfield.
    public void Prepare(RectTransform playArea)
    {
        area = playArea;
        Score = 0;
        Running = false;
        Build();
    }

    // Called by the hub after "GO!".
    public void Begin()
    {
        Running = true;
        enabled = true;
        OnBegin();
    }

    // Called by the hub when time runs out, the game ends itself, or the player quits.
    public void Stop()
    {
        if (!Running)
            return;
        Running = false;
        enabled = false;
        StopAllCoroutines();
        OnStop();
    }

    void Update()
    {
        if (Running)
            Tick(Time.deltaTime);
    }

    protected abstract void Build();
    protected virtual void OnBegin() { }
    protected virtual void Tick(float dt) { }
    protected virtual void OnStop() { }

    protected void AddScore(int delta, Vector3 worldPos, bool popText = true)
    {
        if (!Running || delta == 0)
            return;
        Score = Mathf.Max(0, Score + delta);
        hub.ShowScore(Score, popText && delta > 0);
        if (popText && UIBurst.Instance != null)
            UIBurst.Instance.PopText(worldPos, delta > 0 ? "+" + delta : delta.ToString());
    }

    protected void EndGame()
    {
        if (Running)
            hub.FinishGame();
    }

    protected Vector2 ToLocal(PointerEventData e)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(area, e.position, e.pressEventCamera, out Vector2 local);
        return local;
    }

    protected static float Ease(float t)
    {
        t = Mathf.Clamp01(t);
        return 1f - (1f - t) * (1f - t);
    }
}

// Forwards pointer (mouse or touch) events to plain callbacks.
public class MiniGamePointer : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    public Action<PointerEventData> down;
    public Action<PointerEventData> drag;
    public Action<PointerEventData> up;

    public void OnPointerDown(PointerEventData e) { down?.Invoke(e); }
    public void OnDrag(PointerEventData e) { drag?.Invoke(e); }
    public void OnPointerUp(PointerEventData e) { up?.Invoke(e); }

    // A transparent image filling `parent` that catches touches.
    public static MiniGamePointer TouchPad(RectTransform parent)
    {
        Image img = MiniGameUI.Image(parent, "TouchPad", null, new Color(1f, 1f, 1f, 0f), true);
        MiniGameUI.Stretch(img.rectTransform);
        return img.gameObject.AddComponent<MiniGamePointer>();
    }
}

// The player's pet inside a mini-game: idle breathing, a squash when happy,
// a shake when hurt, a bounce while running, and facing left/right.
// Motion is skipped when Reduce Motion is on.
public class MiniGamePet : MonoBehaviour
{
    public RectTransform Rect { get; private set; }
    public float Width { get; private set; }
    public float Height { get; private set; }
    public bool running;

    RectTransform body;
    Image image;
    float squash;
    float hurt;
    float facing = 1f;
    float seed;

    public static MiniGamePet Create(RectTransform parent, Sprite sprite, float height)
    {
        RectTransform root = MiniGameUI.Rect("Pet", parent);
        root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
        root.pivot = new Vector2(0.5f, 0f);
        float aspect = sprite != null ? sprite.rect.width / sprite.rect.height : 1f;
        root.sizeDelta = new Vector2(height * aspect, height);

        Image shadow = MiniGameUI.Image(root, "Shadow", MiniGameUI.Theme.softCircle, new Color(0f, 0f, 0f, 0.18f));
        MiniGameUI.Place(shadow.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 4f), new Vector2(height * aspect * 0.8f, 34f));
        shadow.preserveAspect = false;

        Image img = MiniGameUI.Image(root, "Body", sprite, Color.white);
        RectTransform b = img.rectTransform;
        MiniGameUI.Stretch(b);
        b.pivot = new Vector2(0.5f, 0f);

        MiniGamePet pet = root.gameObject.AddComponent<MiniGamePet>();
        pet.Rect = root;
        pet.body = b;
        pet.image = img;
        pet.Width = height * aspect;
        pet.Height = height;
        pet.seed = UnityEngine.Random.value * 10f;
        return pet;
    }

    public void Happy() { squash = -0.12f; }
    public void Hurt() { hurt = 0.45f; squash = 0.1f; }
    public void Face(float direction)
    {
        if (Mathf.Abs(direction) > 0.01f)
            facing = Mathf.Sign(direction);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        squash = Mathf.MoveTowards(squash, 0f, dt * 0.6f);
        hurt = Mathf.Max(0f, hurt - dt);
        image.color = Color.Lerp(Color.white, new Color(1f, 0.55f, 0.55f), hurt > 0f ? Mathf.PingPong(hurt * 8f, 1f) : 0f);

        if (ReduceMotion.On)
        {
            body.localScale = new Vector3(facing, 1f, 1f);
            body.anchoredPosition = Vector2.zero;
            return;
        }

        float t = Time.time + seed;
        float breathe = 1f + Mathf.Sin(t * 2.2f) * 0.025f;
        float bob = running ? Mathf.Abs(Mathf.Sin(t * 11f)) * 16f : 0f;
        float shake = hurt > 0f ? Mathf.Sin(t * 70f) * 10f * hurt : 0f;
        body.localScale = new Vector3(facing * (1f + squash), (1f - squash) * breathe, 1f);
        body.anchoredPosition = new Vector2(shake, bob);
        body.localRotation = Quaternion.Euler(0f, 0f, running ? Mathf.Sin(t * 11f) * 4f : 0f);
    }
}

// A thing moving across a playfield (treats, obstacles, collectibles).
public class MiniGameItem
{
    public RectTransform rt;
    public Image image;
    public int kind;
    public int lane;
    public float speed;
    public float spin;
    public bool done;
}

// Keeps a list of MiniGameItems and removes the finished ones.
public class MiniGameItemList : List<MiniGameItem>
{
    public void Sweep()
    {
        for (int i = Count - 1; i >= 0; i--)
        {
            if (this[i].done)
            {
                if (this[i].rt != null)
                    UnityEngine.Object.Destroy(this[i].rt.gameObject);
                RemoveAt(i);
            }
        }
    }
}
