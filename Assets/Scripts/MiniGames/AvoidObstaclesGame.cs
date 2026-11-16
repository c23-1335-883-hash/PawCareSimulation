using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Mini-Game 4: Avoid the Obstacles.
// The pet runs forward on its own across three lanes. Swipe up/down, or tap
// above/below the pet, to change lanes (arrow keys / W-S also work).
// Dodge boxes, rocks and puddles; collect bones, stars and hearts.
// The score grows with distance; three bumps and the run is over.
public class AvoidObstaclesGame : MiniGame
{
    public int lives = 3;
    public float laneGap = 220f;
    public float petX = -620f;
    public float petHeight = 190f;

    [Header("Speed")]
    public float startSpeed = 650f;
    public float maxSpeed = 1500f;
    [Tooltip("Speed gained per second of running.")]
    public float speedUp = 14f;
    [Tooltip("Distance (canvas units) per score point.")]
    public float distancePerPoint = 120f;

    [Header("Spawning (seconds between rows)")]
    public Vector2 spawnEvery = new Vector2(1.15f, 0.55f);
    [Range(0f, 1f)] public float doubleObstacleChance = 0.3f;
    [Range(0f, 1f)] public float collectibleChance = 0.65f;

    [Header("Pickups")]
    public int bonePoints = 5;
    public int starPoints = 20;
    public int heartPointsWhenFull = 10;

    public override string Id => "runner";
    public override string Title => "Avoid the Obstacles";
    public override string Description => "Run, dodge, and collect rewards!";
    public override string HowToPlay => "Swipe up or down to change lanes!";
    public override Sprite Icon => Theme.bone;
    public override Color CardColor => new Color(0.7f, 0.88f, 1f);
    public override int StartLives => lives;

    const int Obstacle = 0, BoneItem = 1, StarItem = 2, HeartItem = 3;
    const float SwipeDistance = 50f;

    MiniGamePet pet;
    RectTransform track;
    RectTransform[] stripes;
    int lane = 1;                // 0 = bottom, 1 = middle, 2 = top
    float petY;
    float speed;
    float distance;
    float spawnTimer;
    float invulnerable;
    int livesLeft;
    bool swiped;
    Vector2 pressAt;
    readonly MiniGameItemList items = new MiniGameItemList();

    float LaneY(int i) => (i - 1) * laneGap - 90f;

    protected override void Build()
    {
        items.Clear();
        lane = 1;
        speed = startSpeed;
        distance = 0f;
        spawnTimer = 0.6f;
        invulnerable = 0f;
        livesLeft = lives;

        // Grass track with three lanes and moving dashes.
        track = MiniGameUI.Rect("Track", area);
        MiniGameUI.Place(track, new Vector2(0.5f, 0.5f), new Vector2(0f, LaneY(1)), new Vector2(4000f, laneGap * 3f + 40f));
        Image grass = MiniGameUI.Image(track, "Grass", Theme.panel, new Color(0.62f, 0.82f, 0.45f, 0.92f));
        MiniGameUI.Stretch(grass.rectTransform);
        for (int i = 0; i < 3; i++)
        {
            Image laneImg = MiniGameUI.Image(track, "Lane" + i, Theme.panel, new Color(0.93f, 0.85f, 0.65f, 0.95f));
            MiniGameUI.Place(laneImg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, (i - 1) * laneGap), new Vector2(4000f, laneGap - 50f));
        }
        stripes = new RectTransform[16];
        for (int i = 0; i < stripes.Length; i++)
        {
            Image s = MiniGameUI.Image(track, "Dash", null, new Color(1f, 1f, 1f, 0.6f));
            int row = i % 2;
            MiniGameUI.Place(s.rectTransform, new Vector2(0.5f, 0.5f),
                new Vector2(-1900f + (i / 2) * 500f + row * 250f, (row - 0.5f) * laneGap), new Vector2(120f, 10f));
            stripes[i] = s.rectTransform;
        }

        MiniGamePointer pad = MiniGamePointer.TouchPad(area);
        pad.down = e => { pressAt = ToLocal(e); swiped = false; };
        pad.drag = OnDrag;
        pad.up = OnUp;

        pet = MiniGamePet.Create(area, hub.PetSprite, petHeight);
        petY = LaneY(lane);
        pet.Rect.anchoredPosition = new Vector2(petX, petY - petHeight * 0.35f);
        pet.Face(1f);
    }

    void OnDrag(PointerEventData e)
    {
        if (swiped || !Running)
            return;
        float dy = ToLocal(e).y - pressAt.y;
        if (Mathf.Abs(dy) > SwipeDistance)
        {
            Move(dy > 0f ? 1 : -1);
            swiped = true;
        }
    }

    void OnUp(PointerEventData e)
    {
        if (swiped || !Running)
            return;
        // A tap: move toward the side of the pet that was tapped.
        float y = ToLocal(e).y;
        float petMid = LaneY(lane);
        if (Mathf.Abs(y - petMid) > laneGap * 0.4f)
            Move(y > petMid ? 1 : -1);
    }

    void Move(int dir)
    {
        int next = Mathf.Clamp(lane + dir, 0, 2);
        if (next == lane)
            return;
        lane = next;
        SFXManager.Play(Sfx.Hover, 0.8f);
    }

    protected override void OnBegin()
    {
        pet.running = true;
    }

    protected override void Tick(float dt)
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) Move(1);
            if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) Move(-1);
        }
#endif
        speed = Mathf.Min(maxSpeed, speed + speedUp * dt);
        float step = speed * dt;

        // Score from distance.
        int before = Mathf.FloorToInt(distance / distancePerPoint);
        distance += step;
        int gained = Mathf.FloorToInt(distance / distancePerPoint) - before;
        if (gained > 0)
            AddScore(gained, pet.Rect.position, false);

        // Slide the pet between lanes, draw lower lanes in front.
        petY = Mathf.MoveTowards(petY, LaneY(lane), 1800f * dt);
        pet.Rect.anchoredPosition = new Vector2(petX, petY - petHeight * 0.35f);
        invulnerable = Mathf.Max(0f, invulnerable - dt);
        pet.gameObject.SetActive(invulnerable <= 0f || Mathf.Repeat(invulnerable, 0.2f) > 0.08f);

        foreach (RectTransform s in stripes)
        {
            Vector2 p = s.anchoredPosition;
            p.x -= step;
            if (p.x < -2000f)
                p.x += 4000f;
            s.anchoredPosition = p;
        }

        spawnTimer -= dt;
        if (spawnTimer <= 0f)
        {
            float k = Mathf.InverseLerp(startSpeed, maxSpeed, speed);
            spawnTimer = Mathf.Lerp(spawnEvery.x, spawnEvery.y, k) * Random.Range(0.85f, 1.15f);
            SpawnRow(k);
        }

        foreach (MiniGameItem it in items)
        {
            Vector2 p = it.rt.anchoredPosition;
            p.x -= step;
            it.rt.anchoredPosition = p;
            if (it.kind != Obstacle && !ReduceMotion.On)
                it.rt.localScale = Vector3.one * (1f + Mathf.Sin(Time.time * 8f) * 0.06f);

            if (it.lane == lane && Mathf.Abs(p.x - petX) < 90f)
                Hit(it);
            else if (p.x < Bounds.xMin - 200f)
                it.done = true;
        }
        items.Sweep();
        SortLanes();
    }

    // One row: an obstacle in one lane (sometimes two, never all three) and
    // maybe a pickup in a free lane, so there's always a way through.
    void SpawnRow(float difficulty)
    {
        int blocked = Random.Range(0, 3);
        Spawn(Obstacle, blocked);
        int second = -1;
        if (Random.value < doubleObstacleChance * (0.3f + difficulty))
        {
            second = (blocked + Random.Range(1, 3)) % 3;
            Spawn(Obstacle, second);
        }
        if (Random.value < collectibleChance)
        {
            int free = second >= 0 ? 3 - blocked - second : (blocked + Random.Range(1, 3)) % 3;
            float roll = Random.value;
            int kind = roll < 0.12f ? HeartItem : roll < 0.3f ? StarItem : BoneItem;
            Spawn(kind, free);
        }
    }

    void Spawn(int kind, int laneIndex)
    {
        Sprite sprite;
        Vector2 size = new Vector2(130f, 130f);
        if (kind == Obstacle)
        {
            int pick = Random.Range(0, 3);
            sprite = pick == 0 ? Theme.box : pick == 1 ? Theme.rock : Theme.puddle;
            if (pick == 2)
                size = new Vector2(170f, 110f);
        }
        else
        {
            sprite = kind == BoneItem ? Theme.bone : kind == StarItem ? Theme.star : Theme.heart;
            size = new Vector2(90f, 90f);
        }
        Image img = MiniGameUI.Image(area, (kind == Obstacle ? "Obstacle" : "Pickup") + laneIndex, sprite, Color.white);
        img.preserveAspect = false;
        MiniGameUI.Place(img.rectTransform, new Vector2(0.5f, 0.5f),
            new Vector2(Bounds.xMax + 150f, LaneY(laneIndex) + (kind == Obstacle ? -10f : 20f)), size);
        items.Add(new MiniGameItem { rt = img.rectTransform, image = img, kind = kind, lane = laneIndex });
    }

    void Hit(MiniGameItem it)
    {
        if (it.kind == Obstacle)
        {
            if (invulnerable > 0f)
                return;
            it.done = true;
            livesLeft--;
            hub.SetLives(livesLeft);
            invulnerable = 1.2f;
            pet.Hurt();
            SFXManager.Play(Sfx.Rumble);
            if (UIBurst.Instance != null)
                UIBurst.Instance.Burst(it.rt.position, UIBurst.Instance.dot, 10, UIBurst.Drops, new Color(0.86f, 0.64f, 0.4f), Color.white);
            if (livesLeft <= 0)
            {
                pet.running = false;
                EndGame();
            }
            return;
        }

        it.done = true;
        pet.Happy();
        if (it.kind == HeartItem && livesLeft < lives)
        {
            livesLeft++;
            hub.SetLives(livesLeft);
            SFXManager.Play(Sfx.HappyChirp);
            if (UIBurst.Instance != null)
                UIBurst.Instance.PopText(it.rt.position, "+1", Theme.heart);
        }
        else
        {
            int points = it.kind == StarItem ? starPoints : it.kind == HeartItem ? heartPointsWhenFull : bonePoints;
            AddScore(points, it.rt.position);
            SFXManager.Play(it.kind == StarItem ? Sfx.Coin : Sfx.Munch, 0.8f);
        }
        if (UIBurst.Instance != null)
            UIBurst.Instance.Burst(it.rt.position, it.image.sprite, 4, UIBurst.Rise, Color.white);
    }

    // Things in lower lanes are drawn in front of things in higher lanes.
    void SortLanes()
    {
        for (int l = 2; l >= 0; l--)
        {
            foreach (MiniGameItem it in items)
            {
                if (it.lane == l)
                    it.rt.SetAsLastSibling();
            }
            if (lane == l)
                pet.Rect.SetAsLastSibling();
        }
    }

    protected override void OnStop()
    {
        if (pet != null)
        {
            pet.running = false;
            pet.gameObject.SetActive(true);
        }
    }
}
