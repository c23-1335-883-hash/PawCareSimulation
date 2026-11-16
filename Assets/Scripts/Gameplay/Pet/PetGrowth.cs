using System.Collections;
using UnityEngine;

// Evolves the pet through three stages from its Care Points total:
//   Evolution 1 - Baby (start), 2 - Young (200 points), 3 - Adult (500 points).
// Care Points come only from finished care (see CarePoints for the list) and
// never reset when the pet evolves. An evolution gives coins and shows the
// new stage's sprite from the breed's existing art.
//
// This class holds the growth rules only:
// - CarePoints keeps the total in the save file and says when points are earned,
// - GrowthPanelUI shows the panel and the "grew into" popup (Changed / Evolved),
// - PetProgressService stores the stage the pet is shown at.
//
// Runs after the default script order so the chosen pet sprite is already set.
[DefaultExecutionOrder(100)]
public class PetGrowth : MonoBehaviour
{
    [Header("References")]
    public PetStageLibrary library;
    public PetController pet;
    public PetCareManager care;
    public PetNeeds needs;

    [Header("Evolution")]
    [Tooltip("Coins given each time the pet evolves.")]
    public int coinsPerStage = 50;

    [Header("Evolution sparkle")]
    [Tooltip("Twinkle sprite for the sparkle burst (drawn by the scene's UIBurst effects layer).")]
    public Sprite sparkle;
    public Color[] sparkleColors =
    {
        new Color(1f, 0.85f, 0.3f), Color.white, new Color(1f, 0.72f, 0.82f), new Color(0.65f, 0.85f, 1f),
    };
    [Tooltip("Seconds after the evolution starts: PetController.GrowInto swaps the sprite at 0.25s and pops it to full size by about 0.5s.")]
    public float sparkleDelay = 0.5f;
    [Tooltip("How long the twinkles keep appearing around the pet.")]
    public float twinkleTime = 1.2f;

    // Fast ring of sparkles from the pet's centre, then slow twinkles that shrink away.
    static readonly UIBurst.Style SparkleBurst = new UIBurst.Style
    {
        speed = new Vector2(380f, 760f), angle = new Vector2(0f, 360f), gravity = -250f,
        life = new Vector2(0.75f, 1.15f), size = new Vector2(50f, 90f), endSize = 0.2f, spin = 200f,
    };
    static readonly UIBurst.Style Twinkle = new UIBurst.Style
    {
        speed = new Vector2(10f, 50f), angle = new Vector2(60f, 120f), gravity = 20f,
        life = new Vector2(0.5f, 0.85f), size = new Vector2(44f, 80f), endSize = 0.05f, spin = 120f,
    };

    [Header("Size")]
    [Tooltip("Height of the pet on screen at the baby stage.")]
    public float babyHeight = 443f;
    [Tooltip("Older stages grow up to this many times the baby height.")]
    public float maxGrowthScale = 1.45f;

    // Set by the selection screen when a pet is adopted, so it starts as a baby.
    // Continuing from the main menu leaves this false and loads the saved stage.
    public static bool StartAsNewPet;

    public int Stage { get; private set; }
    public PetStageLibrary.Breed Breed { get; private set; }
    public bool HasBreed => Breed != null;
    public bool IsFullyGrown => HasBreed && Stage >= MaxStage;
    public string StageTitle => HasBreed && library != null ? library.Title(Breed, Stage) : "";
    public string StageName => CarePoints.StageNames[Mathf.Clamp(Stage, 0, CarePoints.MaxStage)];
    public int StageCount => MaxStage + 1;

    // Care Points so far, and the total the current evolution is working toward.
    // Shown as e.g. 120 / 200, then 200 / 500 after evolving, then 500 / 500.
    public int Points => IsFullyGrown ? Goal : Mathf.Min(CarePoints.Total, Goal);
    public int Goal => CarePoints.StageStarts[Mathf.Min(Stage + 1, MaxStage)];

    // Raised whenever points or the stage change (and once at start).
    public event System.Action Changed;
    // Raised after an evolution, with the old and new stage.
    public event System.Action<int, int> Evolved;

    void OnEnable()
    {
        CarePoints.Earned += OnEarned;
    }

    void OnDisable()
    {
        CarePoints.Earned -= OnEarned;
    }

    void Start()
    {
        Sprite chosen = ClickImageAnimation.SelectedPetImage;
        string savedId = PetProgressService.SavedPetId;
        if (chosen == null && savedId != null && library != null)
        {
            // Opened without going through the selection screen: use the saved pet.
            PetStageLibrary.Breed saved = library.FindById(savedId);
            if (saved != null)
                chosen = saved.stages[0];
        }
        if (chosen == null)
            chosen = pet.petImage.sprite;
        Breed = library != null ? library.Find(chosen) : null;
        if (Breed == null)
        {
            Debug.LogWarning("PetGrowth: no growth stages found for sprite '" + (chosen ? chosen.name : "none") + "'. Growth is disabled.");
            Changed?.Invoke();
            enabled = false;
            return;
        }

        bool continuing = !StartAsNewPet && PetProgressService.HasProgressFor(Breed.id);
        StartAsNewPet = false;
        PetProgressService.RegisterPet(Breed);
        if (continuing)
        {
            Stage = Mathf.Clamp(PetProgressService.SavedStage, 0, MaxStage);
        }
        else
        {
            // A newly adopted pet always begins as a baby with no Care Points.
            Stage = 0;
            CarePoints.ResetForNewPet();
        }

        PetProgressService.SaveStage(Stage);
        ShowStage(false);
        Changed?.Invoke();

        // Points earned away from home (mini-games) may already be enough to evolve.
        if (TargetStage > Stage)
            StartCoroutine(EvolveSoon());
    }

    IEnumerator EvolveSoon()
    {
        yield return new WaitForSeconds(1f); // after the loading screen fades
        Evolve();
    }

    void OnEarned(int amount, string reason)
    {
        if (!enabled || Breed == null)
            return;
        Evolve();
        Changed?.Invoke();
    }

    int TargetStage => Mathf.Min(CarePoints.StageFor(CarePoints.Total), MaxStage);

    void Evolve()
    {
        int before = Stage;
        int target = TargetStage;
        if (target <= before)
            return;

        Stage = target;
        PetProgressService.SaveStage(Stage);
        CoinCounter.Give(coinsPerStage * (Stage - before), pet.PetPoint(0.5f, 0.9f));
        ShowStage(true);
        StartCoroutine(Sparkles());
        Evolved?.Invoke(before, Stage);
        Changed?.Invoke();
    }

    // Sparkles when the pet evolves: a burst as the new sprite appears, then
    // twinkles popping up all over the pet. With Reduce Motion: one small burst.
    IEnumerator Sparkles()
    {
        UIBurst fx = UIBurst.Instance;
        if (fx == null || sparkle == null)
            yield break;

        if (ReduceMotion.On)
        {
            fx.Burst(pet.PetPoint(0.5f, 0.55f), sparkle, 8, Twinkle, sparkleColors);
            yield break;
        }

        yield return new WaitForSeconds(sparkleDelay);
        fx.Burst(pet.PetPoint(0.5f, 0.55f), sparkle, 20, SparkleBurst, sparkleColors);
        SFXManager.Play(Sfx.Jingle, 0.7f);

        for (float t = 0f; t < twinkleTime; t += 0.15f)
        {
            for (int i = 0; i < 2; i++)
                fx.Burst(pet.PetPoint(Random.Range(0.05f, 0.95f), Random.Range(0.1f, 1.05f)), sparkle, 1, Twinkle, sparkleColors);
            yield return new WaitForSeconds(0.15f);
        }
    }

    // Highest of the three evolutions this breed has a sprite for.
    int MaxStage
    {
        get
        {
            int max = 0;
            for (int i = 1; i < Breed.stages.Length && i <= CarePoints.MaxStage; i++)
            {
                if (Breed.stages[i] != null)
                    max = i;
            }
            return max;
        }
    }

    // Falls back to an earlier stage's sprite if one is missing.
    Sprite SpriteFor(int stage)
    {
        for (int i = stage; i >= 0; i--)
        {
            if (Breed.stages[i] != null)
                return Breed.stages[i];
        }
        return pet.petImage.sprite;
    }

    float HeightFor(Sprite sprite)
    {
        Sprite baby = Breed.stages[0];
        if (baby == null || sprite == null)
            return babyHeight;
        float scale = Mathf.Clamp(sprite.rect.height / baby.rect.height, 1f, maxGrowthScale);
        return babyHeight * scale;
    }

    // Shows the current stage on the pet (PetController does the drawing).
    void ShowStage(bool animated)
    {
        Sprite sprite = SpriteFor(Stage);
        if (animated)
            pet.GrowInto(sprite, HeightFor(sprite), Stage >= MaxStage ? "I'm all grown up now!" : "Look, I'm growing up!");
        else
            pet.SetSprite(sprite, HeightFor(sprite));
    }
}
