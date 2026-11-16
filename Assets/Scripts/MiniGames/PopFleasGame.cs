using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Mini-Game 3: Pop the Fleas.
// Fleas pop up at random spots on and around the pet. Tap them before they
// hop away. Fleas come faster as the 30 seconds run down.
public class PopFleasGame : MiniGame
{
    public float roundSeconds = 30f;
    public int fleaPoints = 10;
    public float petHeight = 560f;
    [Tooltip("Size of the area around the pet where fleas appear.")]
    public Vector2 spawnRadius = new Vector2(420f, 280f);

    [Header("Difficulty (start -> end of round)")]
    public Vector2 spawnEvery = new Vector2(0.7f, 0.3f);
    public Vector2 fleaLife = new Vector2(1.9f, 1.0f);
    public int maxFleas = 7;

    public override string Id => "fleas";
    public override string Title => "Pop the Fleas";
    public override string Description => "Help your pet by removing the fleas!";
    public override string HowToPlay => "Tap the fleas as fast as you can!";
    public override Sprite Icon => Theme.flea;
    public override Color CardColor => new Color(0.8f, 0.93f, 0.68f);
    public override float TimeLimit => roundSeconds;

    class Flea
    {
        public RectTransform rt;
        public RectTransform sprite;
        public float age, life;
        public bool popped;
    }

    MiniGamePet pet;
    Vector2 centre;
    float spawnTimer;
    readonly List<Flea> fleas = new List<Flea>();

    protected override void Build()
    {
        fleas.Clear();
        pet = MiniGamePet.Create(area, hub.PetSprite, petHeight);
        pet.Rect.anchoredPosition = new Vector2(0f, -petHeight * 0.5f - 40f);
        centre = new Vector2(0f, -40f);
        spawnTimer = 0.2f;
    }

    protected override void Tick(float dt)
    {
        float k = Mathf.Clamp01(Elapsed / roundSeconds);
        spawnTimer -= dt;
        if (spawnTimer <= 0f && fleas.Count < maxFleas)
        {
            spawnTimer = Mathf.Lerp(spawnEvery.x, spawnEvery.y, k) * Random.Range(0.8f, 1.2f);
            Spawn(Mathf.Lerp(fleaLife.x, fleaLife.y, k));
        }

        for (int i = fleas.Count - 1; i >= 0; i--)
        {
            Flea f = fleas[i];
            f.age += dt;
            if (f.popped)
                continue;

            // Pop in, wiggle, then hop away when time is up.
            float appear = Mathf.Clamp01(f.age / 0.12f);
            float leave = Mathf.Clamp01((f.age - f.life) / 0.2f);
            float wiggle = ReduceMotion.On ? 0f : Mathf.Sin(f.age * 30f) * 12f;
            f.sprite.localScale = Vector3.one * appear * (1f - leave);
            f.sprite.localRotation = Quaternion.Euler(0f, 0f, wiggle);
            f.sprite.anchoredPosition = new Vector2(0f, leave * 80f);
            if (leave >= 1f)
            {
                Destroy(f.rt.gameObject);
                fleas.RemoveAt(i);
            }
        }
    }

    void Spawn(float life)
    {
        // Random point in an ellipse around the pet, kept on screen and below the HUD.
        Vector2 pos;
        int tries = 0;
        do
        {
            Vector2 r = Random.insideUnitCircle;
            pos = centre + new Vector2(r.x * spawnRadius.x, r.y * spawnRadius.y);
            tries++;
        } while (tries < 8 && TooClose(pos));
        pos.x = Mathf.Clamp(pos.x, Bounds.xMin + 90f, Bounds.xMax - 90f);
        pos.y = Mathf.Clamp(pos.y, Bounds.yMin + 90f, Bounds.yMax - 200f);

        // A bigger invisible hit area makes small fleas easy to tap with a finger.
        Image hit = MiniGameUI.Image(area, "Flea", null, new Color(1f, 1f, 1f, 0f), true);
        MiniGameUI.Place(hit.rectTransform, new Vector2(0.5f, 0.5f), pos, new Vector2(130f, 130f));
        Image img = MiniGameUI.Image(hit.transform, "Sprite", Theme.flea, Color.white);
        MiniGameUI.Place(img.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(84f, 84f));
        img.rectTransform.localScale = Vector3.zero;

        var flea = new Flea { rt = hit.rectTransform, sprite = img.rectTransform, life = life };
        MiniGamePointer tap = hit.gameObject.AddComponent<MiniGamePointer>();
        tap.down = _ => Pop(flea);
        fleas.Add(flea);
    }

    bool TooClose(Vector2 pos)
    {
        foreach (Flea f in fleas)
        {
            if ((f.rt.anchoredPosition - pos).sqrMagnitude < 120f * 120f)
                return true;
        }
        return false;
    }

    void Pop(Flea f)
    {
        if (!Running || f.popped)
            return;
        f.popped = true;
        AddScore(fleaPoints, f.rt.position);
        SFXManager.Play(Sfx.Pop);
        pet.Happy();
        if (UIBurst.Instance != null)
            UIBurst.Instance.Burst(f.rt.position, UIBurst.Instance.dot, 7, UIBurst.Drops,
                new Color(0.45f, 0.26f, 0.14f), new Color(1f, 0.9f, 0.5f));
        fleas.Remove(f);
        Destroy(f.rt.gameObject);
    }
}
