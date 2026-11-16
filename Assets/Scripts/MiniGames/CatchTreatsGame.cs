using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Mini-Game 1: Catch the Treats.
// The pet walks along the bottom; touch/drag anywhere to move it left and
// right (arrow keys / A-D also work). Catch treats, dodge the poop.
// Things fall faster and more often as the round goes on.
public class CatchTreatsGame : MiniGame
{
    [Header("Round")]
    public float roundSeconds = 45f;
    public int treatPoints = 5;
    public int starPoints = 15;
    public int badPoints = -5;

    [Header("Difficulty (start -> end of round)")]
    public Vector2 fallSpeed = new Vector2(380f, 900f);
    public Vector2 spawnEvery = new Vector2(0.85f, 0.35f);
    public Vector2 badChance = new Vector2(0.18f, 0.35f);
    [Range(0f, 1f)] public float starChance = 0.07f;

    [Header("Pet")]
    public float petHeight = 230f;
    public float petSpeed = 2600f;
    public float keyboardSpeed = 1100f;

    public override string Id => "catch";
    public override string Title => "Catch the Treats";
    public override string Description => "Catch as many treats as you can!";
    public override string HowToPlay => "Drag to move. Catch treats, avoid the poop!";
    public override Sprite Icon => Theme.meat;
    public override Color CardColor => new Color(1f, 0.85f, 0.6f);
    public override float TimeLimit => roundSeconds;

    const int Good = 0, Star = 1, Bad = 2;

    MiniGamePet pet;
    float targetX;
    float spawnTimer;
    readonly MiniGameItemList items = new MiniGameItemList();

    protected override void Build()
    {
        items.Clear();
        MiniGamePointer pad = MiniGamePointer.TouchPad(area);
        pad.down = Steer;
        pad.drag = Steer;

        pet = MiniGamePet.Create(area, hub.PetSprite, petHeight);
        pet.Rect.anchoredPosition = new Vector2(0f, Bounds.yMin + 40f);
        targetX = 0f;
        spawnTimer = 0.3f;
    }

    void Steer(PointerEventData e)
    {
        if (Running)
            targetX = ToLocal(e).x;
    }

    protected override void Tick(float dt)
    {
        float k = Mathf.Clamp01(Elapsed / roundSeconds);

#if ENABLE_INPUT_SYSTEM
        Keyboard kb = Keyboard.current;
        if (kb != null)
        {
            float dir = (kb.rightArrowKey.isPressed || kb.dKey.isPressed ? 1f : 0f) - (kb.leftArrowKey.isPressed || kb.aKey.isPressed ? 1f : 0f);
            if (dir != 0f)
                targetX = pet.Rect.anchoredPosition.x + dir * keyboardSpeed * dt * 4f;
        }
#endif

        // Move the pet toward the finger.
        float half = pet.Width * 0.5f;
        targetX = Mathf.Clamp(targetX, Bounds.xMin + half, Bounds.xMax - half);
        Vector2 p = pet.Rect.anchoredPosition;
        float newX = Mathf.MoveTowards(p.x, targetX, petSpeed * dt);
        pet.Face(newX - p.x);
        pet.running = Mathf.Abs(newX - targetX) > 2f;
        pet.Rect.anchoredPosition = new Vector2(newX, p.y);

        // Spawn.
        spawnTimer -= dt;
        if (spawnTimer <= 0f)
        {
            spawnTimer = Mathf.Lerp(spawnEvery.x, spawnEvery.y, k) * Random.Range(0.8f, 1.2f);
            Spawn(Mathf.Lerp(fallSpeed.x, fallSpeed.y, k), Mathf.Lerp(badChance.x, badChance.y, k));
        }

        // Fall and catch.
        float mouthY = p.y + pet.Height * 0.55f;
        float catchHalfWidth = pet.Width * 0.45f;
        foreach (MiniGameItem it in items)
        {
            Vector2 pos = it.rt.anchoredPosition;
            pos.y -= it.speed * dt;
            it.rt.anchoredPosition = pos;
            it.rt.localRotation = Quaternion.Euler(0f, 0f, it.spin * Elapsed);

            if (pos.y < mouthY + 40f && pos.y > p.y + 30f && Mathf.Abs(pos.x - newX) < catchHalfWidth)
            {
                Caught(it);
                it.done = true;
            }
            else if (pos.y < Bounds.yMin - 100f)
            {
                it.done = true;
            }
        }
        items.Sweep();
    }

    void Spawn(float speed, float bad)
    {
        float roll = Random.value;
        int kind = roll < bad ? Bad : roll < bad + starChance ? Star : Good;
        Sprite sprite = kind == Bad ? Theme.poop : kind == Star ? Theme.star : PickTreat();

        var img = MiniGameUI.Image(area, kind == Bad ? "Poop" : "Treat", sprite, Color.white);
        float size = kind == Star ? 90f : 110f;
        float x = Random.Range(Bounds.xMin + 80f, Bounds.xMax - 80f);
        MiniGameUI.Place(img.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(x, Bounds.yMax + 80f), new Vector2(size, size));
        items.Add(new MiniGameItem
        {
            rt = img.rectTransform,
            image = img,
            kind = kind,
            speed = speed * Random.Range(0.9f, 1.15f),
            spin = kind == Bad ? 0f : Random.Range(-90f, 90f),
        });
    }

    Sprite PickTreat()
    {
        switch (Random.Range(0, 3))
        {
            case 0: return Theme.meat;
            case 1: return Theme.apple;
            default: return Theme.bone;
        }
    }

    void Caught(MiniGameItem it)
    {
        Vector3 at = it.rt.position;
        if (it.kind == Bad)
        {
            AddScore(badPoints, at);
            pet.Hurt();
            SFXManager.Play(Sfx.Rumble);
            if (UIBurst.Instance != null)
                UIBurst.Instance.Burst(at, Theme.softCircle, 8, UIBurst.Drops, new Color(0.55f, 0.34f, 0.2f));
            return;
        }

        AddScore(it.kind == Star ? starPoints : treatPoints, at);
        pet.Happy();
        SFXManager.Play(it.kind == Star ? Sfx.Coin : Sfx.Munch, 0.8f);
        if (UIBurst.Instance != null)
            UIBurst.Instance.Burst(at, it.kind == Star ? Theme.star : UIBurst.Instance.heart, 5, UIBurst.Rise, Color.white);
    }

    protected override void OnStop()
    {
        if (pet != null)
            pet.running = false;
    }
}
