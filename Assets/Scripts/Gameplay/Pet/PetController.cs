using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum PetState
{
    Idle,
    Eating,
    Drinking,
    Bathing,
    Grooming,
    Playing,
    Resting,
    Cuddling,   // being stroked (a hands-on cuddle, or settling down for a nap)
    AtVet       // sitting on the exam table at the vet clinic
}

// Makes the pet feel alive: breathing and idle moves, reactions to care and
// petting, speech bubbles about its needs, and simple visual states
// (dirt spots when dirty, dimmed and "Zzz" while resting).
// The pet is a single sprite, so all motion is done by moving and scaling it.
public class PetController : MonoBehaviour
{
    [Header("Scene References")]
    public PetNeeds needs;
    public RectTransform pet;
    public Image petImage;
    public RectTransform speechBubble;
    public TMP_Text speechText;
    [Tooltip("Small icon at the right of the speech bubble (the font has no emoji).")]
    public Image speechIcon;
    public PetMood mood;
    public RectTransform heart;
    public TMP_Text sleepText;
    public Image[] dirtSpots;
    public Image activityProp;
    [Tooltip("Dark layer behind the pet that fades in while it sleeps.")]
    public Image sleepDim;
    public float sleepDimAlpha = 0.35f;

    [Header("Petting")]
    public float pettingHappiness = 4f;
    public float pettingCooldown = 2.5f;
    [Tooltip("A full hands-on cuddle gives this many times the happiness of a quick pat.")]
    public float cuddleHappinessMultiplier = 3f;

    // Breathing, idle timing and chatter timing live in FeelConfig.

    [Header("Speech Icons")]
    public Sprite iconHeart;
    public Sprite iconBall;
    public Sprite iconMoon;
    public Sprite iconSun;
    public Sprite iconFood;
    public Sprite iconWater;
    public Sprite iconBubbles;
    public Sprite iconPaw;
    public Sprite iconStar;

    [Header("Effect Colors")]
    public Color heartColor = new Color(1f, 0.55f, 0.62f);
    public Color kibbleColor = new Color(0.72f, 0.45f, 0.25f);
    public Color waterColor = new Color(0.45f, 0.72f, 1f);
    public Color bubbleColor = new Color(0.85f, 0.95f, 1f, 0.85f);
    public Color sparkleColor = new Color(1f, 0.93f, 0.6f);

    [Header("Messages")]
    public float messageTime = 2.5f;
    [Tooltip("Seconds between reminders about a low need.")]
    public float reminderInterval = 20f;

    public PetState State { get; private set; }
    // Fired when a cuddle counts (not while the petting cooldown is running).
    public event System.Action Petted;

    // Set by PetMood: 0 = upright, 1 = droopy and sad.
    public float Droop { get; set; }
    // Set by PetMood: a happy pet hops more in its idle moves.
    public bool ExtraBouncy { get; set; }

    public bool IsIdle => State == PetState.Idle && reaction == null && !growing;

    // Set by PetHealth: a sick pet looks a little pale and shows symptoms.
    public Illness Illness
    {
        get => illness;
        set
        {
            illness = value;
            RefreshLook();
        }
    }
    Illness illness;

    Vector2 basePosition;
    Vector3 baseScale;
    Color baseColor;

    // Offsets written by reaction coroutines and applied on top of the idle motion.
    float hopOffset;
    float squash;
    float tilt;
    float facing = 1f;
    float growScale = 1f;
    float droopShown;
    float blinkSquash;
    bool growing;

    // Hands-on care: the pet leans toward the item the player is holding.
    Vector3? watchTarget;
    float lean;

    // Effect positions relative to the top of the pet, so they follow it as it grows.
    Vector2 bubbleOffset;
    Vector2 heartOffset;
    Vector2 sleepOffset;
    Vector2 sleepBase;

    float nextIdleMove;
    float nextReminder;
    float nextChatter;
    Coroutine blinkRoutine;
    float textRightMargin;
    float lastPetTime = -100f;
    Coroutine reaction;
    Coroutine bubbleRoutine;
    Coroutine heartRoutine;
    CanvasGroup heartGroup;
    Vector2 heartStart;

    void Awake()
    {
        basePosition = pet.anchoredPosition;
        baseScale = pet.localScale;
        baseColor = petImage.color;
        Vector2 top = PetTop();
        if (speechBubble != null)
            bubbleOffset = speechBubble.anchoredPosition - top;
        if (heart != null)
            heartOffset = heart.anchoredPosition - top;
        if (sleepText != null)
        {
            sleepBase = sleepText.rectTransform.anchoredPosition;
            sleepOffset = sleepBase - top;
        }
        if (sleepDim != null)
        {
            SetDim(0f);
            sleepDim.raycastTarget = false;
        }
        if (heart != null)
        {
            heartStart = heart.anchoredPosition;
            heartGroup = heart.GetComponent<CanvasGroup>();
            heart.gameObject.SetActive(false);
        }
        if (speechBubble != null)
            speechBubble.gameObject.SetActive(false);
        if (speechText != null)
            textRightMargin = speechText.rectTransform.offsetMax.x;
        if (activityProp != null)
            activityProp.gameObject.SetActive(false);
        if (sleepText != null)
            sleepText.gameObject.SetActive(false);
    }

    void OnEnable()
    {
        if (needs != null)
            needs.Changed += RefreshLook;
    }

    void OnDisable()
    {
        if (needs != null)
            needs.Changed -= RefreshLook;
    }

    void Start()
    {
        ScheduleIdleMove();
        nextReminder = Time.time + 6f;
        nextChatter = Time.time + Random.Range(Config.chatterInterval.x, Config.chatterInterval.y);
        RefreshLook();

        // PetGrowth (which runs later) still knows whether this pet was just adopted.
        if (PetGrowth.StartAsNewPet)
            Say(new Line(PetProfile.HasName ? "Hi! I'm " + PetProfile.Name + "! Nice to meet you!" : "Hi! Nice to meet you!", LineIcon.Heart));
        else
            Say(PetLines.Greeting(PetProfile.IsCat, needs != null ? needs.MinutesAway : 0f));
    }

    void Update()
    {
        float t = Time.time;
        bool sleepy = State == PetState.Resting || (needs != null && needs.IsLow(Need.Energy));

        // Breathing: 100% -> 100% + breathAmount, slower while sleepy.
        float speed = sleepy ? Config.breathSpeed * 0.5f : Config.breathSpeed;
        float breath = ReduceMotion.On ? 0f : (Mathf.Sin(t * speed) * 0.5f + 0.5f) * Config.breathAmount;

        // A sad pet slumps a little: shorter, lower and leaning.
        droopShown = Mathf.MoveTowards(droopShown, ReduceMotion.On ? 0f : Droop, Time.deltaTime * 0.8f);

        pet.anchoredPosition = basePosition + new Vector2(0f, hopOffset - droopShown * 6f);
        pet.localScale = new Vector3(
            baseScale.x * facing * growScale * (1f + squash + breath * 0.4f + droopShown * 0.02f),
            baseScale.y * growScale * (1f - squash + breath - droopShown * 0.04f - blinkSquash),
            baseScale.z);
        float leanTarget = 0f;
        if (watchTarget.HasValue && !ReduceMotion.On)
        {
            float width = Mathf.Max(1f, pet.rect.width * Mathf.Abs(pet.lossyScale.x));
            leanTarget = -Mathf.Clamp((watchTarget.Value.x - pet.position.x) / width, -1f, 1f) * 6f;
        }
        lean = Mathf.MoveTowards(lean, leanTarget, Time.deltaTime * 30f);

        pet.localRotation = Quaternion.Euler(0f, 0f, tilt + lean + (sleepy ? -3f : 0f) - droopShown * 2f);

        UpdateSleep();

        if (State == PetState.Idle && reaction == null)
        {
            if (t >= nextIdleMove)
            {
                IdleMove();
                ScheduleIdleMove();
            }
            if (t >= nextReminder)
            {
                RemindAboutNeeds();
                nextReminder = t + reminderInterval;
            }
            if (t >= nextChatter)
            {
                if ((needs == null || needs.MostPressing() == null) && illness == Illness.None)
                    Say(PetLines.Chatter(PetProfile.IsCat, mood != null ? mood.Current : Mood.Content, PetProfile.Trait));
                nextChatter = t + Random.Range(Config.chatterInterval.x, Config.chatterInterval.y);
            }
        }
    }

    static FeelConfig Config => FeelConfig.Current;

    // ---- Called by PetGrowth ----

    // Shows a stage sprite at the given height, keeping the sprite's proportions.
    public void SetSprite(Sprite sprite, float height)
    {
        if (sprite == null)
            return;
        petImage.sprite = sprite;
        float aspect = sprite.rect.width / sprite.rect.height;
        pet.sizeDelta = new Vector2(height * aspect, height);

        Vector2 top = PetTop();
        if (speechBubble != null)
            speechBubble.anchoredPosition = top + bubbleOffset;
        if (heart != null)
            heartStart = heart.anchoredPosition = top + heartOffset;
        if (sleepText != null)
            sleepText.rectTransform.anchoredPosition = sleepBase = top + sleepOffset;
    }

    // Shrinks away, swaps to the new stage, and pops back in with a heart.
    public void GrowInto(Sprite sprite, float height, string message)
    {
        if (reaction != null)
            StopCoroutine(reaction);
        ResetOffsets();
        if (ReduceMotion.On)
        {
            SetSprite(sprite, height);
            Hearts(3);
            Say(message);
            return;
        }
        reaction = StartCoroutine(RunMotion(Grow(sprite, height, message)));
    }

    IEnumerator Grow(Sprite sprite, float height, string message)
    {
        growing = true;
        for (float t = 0f; t < 0.25f; t += Time.deltaTime)
        {
            growScale = Mathf.Lerp(1f, 0.3f, t / 0.25f);
            yield return null;
        }
        SetSprite(sprite, height);
        for (float t = 0f; t < 0.4f; t += Time.deltaTime)
        {
            float k = t / 0.4f;
            growScale = k < 0.7f ? Mathf.Lerp(0.3f, 1.12f, k / 0.7f) : Mathf.Lerp(1.12f, 1f, (k - 0.7f) / 0.3f);
            yield return null;
        }
        growScale = 1f;
        growing = false;
        Hearts(5);
        SFXManager.Play(Sfx.HappyChirp);
        Say(message);
    }

    Vector2 PetTop()
    {
        return basePosition + new Vector2(0f, pet.sizeDelta.y * (1f - pet.pivot.y));
    }

    // ---- Called by PetCareManager ----

    public void SetState(PetState state, Sprite prop = null)
    {
        State = state;
        if (activityProp != null)
        {
            activityProp.sprite = prop;
            activityProp.gameObject.SetActive(prop != null);
        }
        if (sleepText != null)
            sleepText.gameObject.SetActive(state == PetState.Resting);
        RefreshLook();
    }

    // One step of an activity (a bite, a scrub, a catch...). Hands-on care passes
    // where the player is touching, so bubbles and sparkles appear under the tool.
    public void ActivityStep(Vector3? at = null)
    {
        UIBurst fx = Effects;
        switch (State)
        {
            case PetState.Eating:
                // A piece of kibble flies from the bowl to the pet's mouth: chomp!
                FlyToMouth(fx != null ? fx.dot : null, kibbleColor, () =>
                {
                    SFXManager.Play(Sfx.Munch);
                    Burst(PetPoint(0.5f, 0.55f), Effects != null ? Effects.dot : null, 6, UIBurst.Drops, kibbleColor);
                    if (State == PetState.Eating)
                        Play(Nod());
                });
                break;
            case PetState.Drinking:
                FlyToMouth(fx != null ? fx.dot : null, waterColor, () =>
                {
                    SFXManager.Play(Sfx.Pop, 0.6f);
                    Burst(PetPoint(0.5f, 0.55f), Effects != null ? Effects.dot : null, 5, UIBurst.Drops, waterColor);
                    if (State == PetState.Drinking)
                        Play(Nod());
                });
                break;
            case PetState.Bathing:
                SFXManager.Play(Sfx.Bubbles);
                Burst(at ?? PetPoint(Random.Range(0.25f, 0.75f), Random.Range(0.3f, 0.7f)), fx != null ? fx.dot : null, 7, UIBurst.Rise, bubbleColor);
                Play(Wiggle());
                break;
            case PetState.Grooming:
                SFXManager.Play(Sfx.Hover);
                Burst(at ?? PetPoint(Random.Range(0.3f, 0.7f), Random.Range(0.4f, 0.8f)), fx != null ? fx.dot : null, 5, Sparkle, sparkleColor, Color.white);
                Play(Wiggle());
                break;
            case PetState.Playing:
                SFXManager.Play(Sfx.HappyChirp);
                Play(Hop(70f, 0.45f));
                break;
            default:
                Play(Hop(20f, 0.25f));
                break;
        }
    }

    // After a bath the pet shakes itself dry instead of hopping.
    // A favourite activity ("loved") gets a bigger hop, more hearts and sparkles.
    public void Celebrate(string message, bool shakeOff = false, bool loved = false)
    {
        Celebrate(new Line(message), shakeOff, loved);
    }

    public void Celebrate(Line message, bool shakeOff = false, bool loved = false)
    {
        Play(shakeOff ? ShakeOff() : loved ? Hop(90f, 0.55f) : Hop(55f, 0.45f));
        Hearts(loved ? 6 : 3);
        if (loved)
            Burst(PetPoint(0.5f, 0.7f), Effects != null ? Effects.dot : null, 10, Sparkle, sparkleColor, Color.white);
        SFXManager.Play(Sfx.HappyChirp);
        Say(message);
    }

    // Tap on the pet when no care activity is running.
    public void Pet()
    {
        if (State == PetState.Resting)
        {
            Say("Zzz... I'm resting.");
            return;
        }

        Play(Squish());
        if (Time.time - lastPetTime < pettingCooldown)
            return;

        lastPetTime = Time.time;
        Petted?.Invoke();
        bool loves = PetProfile.LovesPetting;
        if (needs != null)
            needs.Add(Need.Happiness, loves ? pettingHappiness * 2f : pettingHappiness);
        Hearts(loves ? 5 : 3);
        SFXManager.Play(Sfx.HappyChirp);
        Say(PetLines.Petting(PetProfile.IsCat, needs != null ? needs.MostPressing() : null, loves));
    }

    public void Refuse(string message)
    {
        Play(ShakeHead());
        Say(message);
    }

    // ---- Called by hands-on care (CareInteraction) ----

    // Rough handling: a quick startled pull-back.
    public void Flinch(string message)
    {
        Play(FlinchMotion());
        SFXManager.Play(Sfx.Rumble);
        Say(message);
    }

    // Water droplets at a point (rinsing).
    public void Splash(Vector3 at)
    {
        Burst(at, Effects != null ? Effects.dot : null, 5, UIBurst.Drops, waterColor);
    }

    // Bits of fur flying off a brushed-out tangle.
    public void FurPuff(Vector3 at)
    {
        Burst(at, Effects != null ? Effects.dot : null, 6, Sparkle, kibbleColor, sparkleColor);
    }

    // The pet leans toward this world position (the held item); null stops watching.
    public void Watch(Vector3? at)
    {
        watchTarget = at;
    }

    // Being stroked: a little squish and hearts under the hand (more on its favourite spot).
    public void Nuzzle(Vector3 at, bool favouriteSpot)
    {
        Play(Squish());
        UIBurst fx = Effects;
        if (fx != null && fx.heart != null)
            fx.Burst(at, fx.heart, favouriteSpot ? 3 : 1, UIBurst.Rise, heartColor, Color.white);
        if (favouriteSpot)
            SFXManager.Play(Sfx.HappyChirp, 0.6f);
    }

    // The end of a hands-on cuddle: like a quick pat but bigger, and with no cooldown.
    public void Cuddled(float quality)
    {
        lastPetTime = Time.time;
        Petted?.Invoke();
        bool loves = PetProfile.LovesPetting;
        if (needs != null)
            needs.Add(Need.Happiness, pettingHappiness * cuddleHappinessMultiplier * Mathf.Lerp(0.6f, 1f, quality) * (loves ? 2f : 1f));
        Play(Hop(loves ? 60f : 40f, 0.45f));
        Hearts(loves ? 7 : 5);
        SFXManager.Play(Sfx.HappyChirp);
        Say(PetLines.Petting(PetProfile.IsCat, needs != null ? needs.MostPressing() : null, loves));
    }

    // Getting sleepy while being settled down for a nap.
    public void SleepyYawn()
    {
        Say(PetLines.Yawn(PetProfile.IsCat));
        Play(YawnMotion());
    }

    // Poked while asleep: a grumpy little shiver.
    public void Grumble(string message)
    {
        Play(RumbleMotion());
        Say(message);
    }

    // ---- Called by PetHealth ----

    // One visible symptom of the illness, with a line about it.
    public void ShowSymptom(Illness illness)
    {
        switch (illness)
        {
            case Illness.Cold: Play(SneezeMotion()); break;
            case Illness.Fleas: Play(ScratchMotion()); break;
            case Illness.TummyAche: Play(RumbleMotion()); SFXManager.Play(Sfx.Rumble); break;
            case Illness.EarInfection: Play(ShakeHead()); break;
            default: return;
        }
        Say(PetLines.Symptom(PetProfile.IsCat, illness));
    }

    public void Say(string message)
    {
        Say(new Line(message));
    }

    public void Say(Line line)
    {
        if (speechBubble == null || speechText == null)
            return;
        if (bubbleRoutine != null)
            StopCoroutine(bubbleRoutine);
        bubbleRoutine = StartCoroutine(ShowBubble(line));
        nextReminder = Mathf.Max(nextReminder, Time.time + messageTime + 4f);
        nextChatter = Mathf.Max(nextChatter, Time.time + messageTime + 8f);
    }

    // ---- Called by PetMood ----

    // The pet is one picture, so a "blink" is a tiny quick squash.
    public void Blink(bool slow)
    {
        if (ReduceMotion.On || blinkRoutine != null || !IsIdle)
            return;
        blinkRoutine = StartCoroutine(BlinkRoutine(slow ? 0.35f : 0.14f));
    }

    public void Yawn()
    {
        if (!IsIdle)
            return;
        Say(PetLines.Yawn(PetProfile.IsCat));
        Play(YawnMotion());
    }

    public void Rumble(bool sayIt)
    {
        if (!IsIdle)
            return;
        SFXManager.Play(Sfx.Rumble);
        if (sayIt)
            Say(PetLines.Rumble);
        Play(RumbleMotion());
    }

    // ---- Looks ----

    void RefreshLook()
    {
        if (needs == null)
            return;

        float dirt = Mathf.InverseLerp(50f, 10f, needs.Get(Need.Cleanliness));
        foreach (Image spot in dirtSpots)
        {
            if (spot == null)
                continue;
            Color c = spot.color;
            c.a = dirt * 0.75f;
            spot.color = c;
        }

        Color tint = baseColor;
        if (State == PetState.Resting)
            tint *= new Color(0.75f, 0.75f, 0.9f, 1f);
        if (illness != Illness.None)
            tint *= new Color(0.9f, 0.97f, 0.84f, 1f);
        petImage.color = tint;
    }

    void RemindAboutNeeds()
    {
        if (needs == null)
            return;

        Need? pressing = needs.MostPressing();
        if (pressing == null)
            return;
        Say(PetLines.Reminder(PetProfile.IsCat, pressing.Value));
        switch (pressing.Value)
        {
            case Need.Hunger:
            case Need.Thirst: Play(LookAround()); break;
            case Need.Cleanliness: Play(Wiggle()); break;
            case Need.Happiness: Play(Hop(40f, 0.4f)); break;
        }
    }

    void IdleMove()
    {
        bool happy = needs == null || needs.Get(Need.Happiness) >= 60f;
        bool tired = needs != null && needs.IsLow(Need.Energy);
        if (tired)
            return;

        int pick = Random.Range(0, happy ? (ExtraBouncy ? 6 : 4) : 2);
        if (pick == 0) Play(LookAround());
        else if (pick == 1) Play(Tilt());
        else if (pick == 2) Play(Wiggle());
        else Play(Hop(ExtraBouncy ? 45f : 35f, 0.4f));
    }

    // ---- Sleep ----

    // Dims the room and floats the "Zzz" up in a slow loop while resting.
    void UpdateSleep()
    {
        bool resting = State == PetState.Resting;
        if (sleepDim != null)
        {
            float target = resting ? sleepDimAlpha : 0f;
            SetDim(Mathf.MoveTowards(sleepDim.color.a, target, Time.unscaledDeltaTime * sleepDimAlpha * 1.5f));
        }

        if (!resting || sleepText == null)
            return;
        const float loop = 2.4f;
        float k = Time.unscaledTime % loop / loop;
        RectTransform z = sleepText.rectTransform;
        if (ReduceMotion.On)
        {
            z.anchoredPosition = sleepBase;
            z.localScale = Vector3.one;
            sleepText.alpha = 1f;
            return;
        }
        z.anchoredPosition = sleepBase + new Vector2(Mathf.Sin(k * Mathf.PI * 2f) * 12f, 70f * k);
        z.localScale = Vector3.one * Mathf.Lerp(0.8f, 1.2f, k);
        sleepText.alpha = Mathf.Min(1f, k * 5f) * (1f - k * k);
    }

    void SetDim(float alpha)
    {
        Color c = sleepDim.color;
        c.a = alpha;
        sleepDim.color = c;
        sleepDim.enabled = alpha > 0.001f;
    }

    // ---- Particles (via the scene's UIBurst, if there is one) ----

    public static readonly UIBurst.Style Sparkle = new UIBurst.Style { speed = new Vector2(40, 120), angle = new Vector2(0, 360), gravity = 0, life = new Vector2(0.5f, 0.8f), size = new Vector2(12, 22), endSize = 0.2f, spin = 180 };

    static UIBurst Effects => ReduceMotion.On ? null : UIBurst.Instance;

    // A point on the pet in 0-1 coordinates (0,0 = bottom left), as a world position.
    public Vector3 PetPoint(float x, float y)
    {
        Rect r = pet.rect;
        return pet.TransformPoint(new Vector3(r.xMin + r.width * x, r.yMin + r.height * y, 0f));
    }

    void Burst(Vector3 at, Sprite sprite, int count, UIBurst.Style style, params Color[] colors)
    {
        UIBurst fx = Effects;
        if (fx != null && sprite != null)
            fx.Burst(at, sprite, count, style, colors);
    }

    void FlyToMouth(Sprite sprite, Color color, System.Action onArrive)
    {
        UIBurst fx = Effects;
        if (fx == null || sprite == null || activityProp == null)
        {
            onArrive();
            return;
        }
        fx.Fly(activityProp.rectTransform.position, PetPoint(0.5f, 0.55f), sprite, color, 34f, 0.32f, onArrive);
    }

    void Hearts(int count)
    {
        UIBurst fx = Effects;
        if (fx == null || fx.heart == null)
        {
            ShowHeart();
            return;
        }
        fx.Burst(PetPoint(0.5f, 0.85f), fx.heart, count, UIBurst.Rise, heartColor, Color.white);
    }

    // World position beside the pet's head (clear of the speech bubble), for floating text.
    public Vector3 RewardTextPosition => PetPoint(1.0f, 0.75f);

    void ScheduleIdleMove()
    {
        nextIdleMove = Time.time + Random.Range(Config.idleMoveInterval.x, Config.idleMoveInterval.y);
    }

    // ---- Motion ----

    void Play(IEnumerator motion)
    {
        if (growing || ReduceMotion.On)
            return; // let the growing animation finish; no bouncing with Reduce Motion
        if (reaction != null)
            StopCoroutine(reaction);
        ResetOffsets();
        reaction = StartCoroutine(RunMotion(motion));
    }

    IEnumerator RunMotion(IEnumerator motion)
    {
        yield return motion;
        ResetOffsets();
        reaction = null;
    }

    void ResetOffsets()
    {
        hopOffset = 0f;
        squash = 0f;
        tilt = 0f;
        facing = 1f;
    }

    IEnumerator Hop(float height, float time)
    {
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            float k = t / time;
            hopOffset = Mathf.Sin(k * Mathf.PI) * height;
            squash = k < 0.15f ? 0.06f * (1f - k / 0.15f) : 0f;
            yield return null;
        }
    }

    IEnumerator Squish()
    {
        const float time = 0.3f;
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            squash = Mathf.Sin(t / time * Mathf.PI) * 0.07f;
            yield return null;
        }
    }

    IEnumerator Nod()
    {
        const float time = 0.35f;
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            float k = Mathf.Sin(t / time * Mathf.PI);
            squash = k * 0.05f;
            hopOffset = -k * 10f;
            yield return null;
        }
    }

    IEnumerator Wiggle()
    {
        const float time = 0.4f;
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            tilt = Mathf.Sin(t / time * Mathf.PI * 4f) * 6f * (1f - t / time);
            yield return null;
        }
    }

    // A quick wet-dog shake that flings droplets to both sides.
    IEnumerator ShakeOff()
    {
        const float time = 0.7f;
        bool flung = false;
        Burst(PetPoint(0.2f, 0.6f), UIBurst.Instance != null ? UIBurst.Instance.dot : null, 6, UIBurst.Drops, waterColor);
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            float k = t / time;
            tilt = Mathf.Sin(k * Mathf.PI * 8f) * 10f * (1f - k);
            squash = Mathf.Sin(k * Mathf.PI * 8f) * 0.03f * (1f - k);
            if (!flung && k > 0.35f)
            {
                flung = true;
                Burst(PetPoint(0.8f, 0.6f), UIBurst.Instance != null ? UIBurst.Instance.dot : null, 6, UIBurst.Drops, waterColor);
            }
            yield return null;
        }
    }

    IEnumerator BlinkRoutine(float time)
    {
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            blinkSquash = Mathf.Sin(t / time * Mathf.PI) * 0.035f;
            yield return null;
        }
        blinkSquash = 0f;
        blinkRoutine = null;
    }

    // A big stretch upward, a little lean back, then settle.
    IEnumerator YawnMotion()
    {
        const float time = 1.2f;
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            float k = Mathf.Sin(t / time * Mathf.PI);
            squash = -k * 0.06f;
            tilt = k * 4f;
            yield return null;
        }
    }

    // A tummy rumble: a quick little shiver.
    IEnumerator RumbleMotion()
    {
        const float time = 0.5f;
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            float k = 1f - t / time;
            tilt = Mathf.Sin(t * 70f) * 1.5f * k;
            squash = Mathf.Sin(t * 50f) * 0.015f * k;
            yield return null;
        }
    }

    // Ah... ah... CHOO: lean back and stretch, then snap forward with a spray.
    IEnumerator SneezeMotion()
    {
        for (float t = 0f; t < 0.45f; t += Time.deltaTime)
        {
            float k = t / 0.45f;
            squash = -0.06f * k;
            tilt = 6f * k;
            yield return null;
        }
        SFXManager.Play(Sfx.Pop, 0.7f);
        Burst(PetPoint(0.5f, 0.72f), Effects != null ? Effects.dot : null, 6, UIBurst.Drops, new Color(0.92f, 0.96f, 1f, 0.9f));
        for (float t = 0f; t < 0.3f; t += Time.deltaTime)
        {
            float k = 1f - t / 0.3f;
            squash = 0.08f * k;
            tilt = -8f * k;
            hopOffset = -10f * k;
            yield return null;
        }
    }

    // Itchy: a quick jittery scratch.
    IEnumerator ScratchMotion()
    {
        const float time = 0.8f;
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            float k = 1f - t / time;
            tilt = Mathf.Sin(t * 45f) * 5f * k;
            hopOffset = Mathf.Abs(Mathf.Sin(t * 30f)) * 6f * k;
            yield return null;
        }
    }

    IEnumerator FlinchMotion()
    {
        const float time = 0.35f;
        float dir = Random.value < 0.5f ? -1f : 1f;
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            float k = 1f - t / time;
            tilt = 9f * dir * k * k;
            squash = 0.08f * k;
            hopOffset = -14f * k;
            yield return null;
        }
    }

    IEnumerator ShakeHead()
    {
        const float time = 0.5f;
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            tilt = Mathf.Sin(t / time * Mathf.PI * 6f) * 4f;
            yield return null;
        }
    }

    IEnumerator Tilt()
    {
        float dir = Random.value < 0.5f ? -1f : 1f;
        for (float t = 0f; t < 1.4f; t += Time.deltaTime)
        {
            tilt = Mathf.Sin(t / 1.4f * Mathf.PI) * 7f * dir;
            yield return null;
        }
    }

    IEnumerator LookAround()
    {
        facing = -1f;
        yield return new WaitForSeconds(0.9f);
        facing = 1f;
        yield return new WaitForSeconds(0.3f);
    }

    // ---- Bubbles and hearts ----

    IEnumerator ShowBubble(Line line)
    {
        speechText.text = line.text;
        Sprite icon = IconFor(line.icon);
        if (speechIcon != null)
        {
            speechIcon.sprite = icon;
            speechIcon.gameObject.SetActive(icon != null);
            Vector2 max = speechText.rectTransform.offsetMax;
            max.x = icon != null ? textRightMargin - speechIcon.rectTransform.rect.width - 8f : textRightMargin;
            speechText.rectTransform.offsetMax = max;
        }
        speechBubble.gameObject.SetActive(true);
        for (float t = 0f; t < 0.15f; t += Time.deltaTime)
        {
            speechBubble.localScale = Vector3.one * Mathf.Lerp(0.6f, 1f, t / 0.15f);
            yield return null;
        }
        speechBubble.localScale = Vector3.one;
        yield return new WaitForSeconds(messageTime);
        speechBubble.gameObject.SetActive(false);
        bubbleRoutine = null;
    }

    Sprite IconFor(LineIcon icon)
    {
        switch (icon)
        {
            case LineIcon.Heart: return iconHeart;
            case LineIcon.Ball: return iconBall;
            case LineIcon.Moon: return iconMoon;
            case LineIcon.Sun: return iconSun;
            case LineIcon.Food: return iconFood;
            case LineIcon.Water: return iconWater;
            case LineIcon.Bubbles: return iconBubbles;
            case LineIcon.Paw: return iconPaw;
            case LineIcon.Star: return iconStar;
            default: return null;
        }
    }

    void ShowHeart()
    {
        if (heart == null)
            return;
        if (heartRoutine != null)
            StopCoroutine(heartRoutine);
        heartRoutine = StartCoroutine(FloatHeart());
    }

    IEnumerator FloatHeart()
    {
        heart.gameObject.SetActive(true);
        const float time = 0.9f;
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            float k = t / time;
            heart.anchoredPosition = heartStart + new Vector2(Mathf.Sin(k * 6f) * 10f, 130f * k);
            heart.localScale = Vector3.one * Mathf.Lerp(0.6f, 1.1f, Mathf.Min(1f, k * 3f));
            if (heartGroup != null)
                heartGroup.alpha = 1f - k * k;
            yield return null;
        }
        heart.gameObject.SetActive(false);
        heartRoutine = null;
    }
}
