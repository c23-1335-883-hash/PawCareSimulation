using System;
using System.Collections;
using TMPro;
using UnityEngine;

public enum CareActivity
{
    Feed,
    Water,
    Clean,
    Groom,
    Play
}

// Runs the care activities. The player picks an activity, then taps the pet
// (or throws the ball for Play) a few times to carry it out. Finishing an
// activity changes the pet's needs, shows a short lesson, and awards coins
// only when the pet actually needed that care, so repeating an action
// can't farm coins.
public class PetCareManager : MonoBehaviour
{
    [Serializable]
    public class ActivitySetup
    {
        public CareActivity activity;
        public PetState petState;
        public Sprite prop;
        public int tapsRequired = 3;
        public string prompt;
        public string doneMessage;
        [TextArea] public string lesson;

        [Header("Effect on needs")]
        public Need mainNeed;
        public float mainAmount = 40f;
        public float happiness;
        public float energy;
        public float cleanliness;
        public float hunger;
        public float thirst;

        [Header("Rules")]
        [Tooltip("The pet refuses when its main need is already at or above this.")]
        public float refuseAbove = 95f;
        public string refuseMessage = "I'm okay for now, thanks!";
    }

    [Header("Scene References")]
    public PetNeeds needs;
    public PetController pet;
    public RectTransform petRect;
    public RectTransform ball;
    public TMP_Text statusText;
    public CanvasGroup statusGroup;

    [Header("Activities")]
    public ActivitySetup[] activities =
    {
        new ActivitySetup { activity = CareActivity.Feed, petState = PetState.Eating, tapsRequired = 3,
            prompt = "Tap your pet to give it food.", doneMessage = "Yum! Thank you!",
            lesson = "Pets need regular meals that suit their age and size.",
            mainNeed = Need.Hunger, mainAmount = 40f, happiness = 5f, refuseMessage = "I'm full, thank you!" },
        new ActivitySetup { activity = CareActivity.Water, petState = PetState.Drinking, tapsRequired = 2,
            prompt = "Tap your pet to fill its water bowl.", doneMessage = "Ahh, fresh water!",
            lesson = "Always give your pet clean, fresh drinking water.",
            mainNeed = Need.Thirst, mainAmount = 45f, happiness = 3f, refuseMessage = "I'm not thirsty right now." },
        new ActivitySetup { activity = CareActivity.Clean, petState = PetState.Bathing, tapsRequired = 5,
            prompt = "Tap your pet to scrub it clean.", doneMessage = "All clean and fresh!",
            lesson = "Baths and a clean sleeping area keep your pet comfortable.",
            mainNeed = Need.Cleanliness, mainAmount = 55f, energy = -3f, refuseMessage = "I'm already clean!" },
        new ActivitySetup { activity = CareActivity.Groom, petState = PetState.Grooming, tapsRequired = 5,
            prompt = "Tap your pet to brush its fur.", doneMessage = "My fur feels so nice!",
            lesson = "Regular brushing keeps your pet's coat clean and tidy.",
            mainNeed = Need.Cleanliness, mainAmount = 20f, happiness = 8f, refuseMessage = "My fur is already neat!" },
        new ActivitySetup { activity = CareActivity.Play, petState = PetState.Playing, tapsRequired = 3,
            prompt = "Tap the ball to throw it!", doneMessage = "That was so much fun!",
            lesson = "Play and gentle exercise help keep your pet healthy and happy.",
            mainNeed = Need.Happiness, mainAmount = 25f, energy = -20f, hunger = -5f, thirst = -5f, cleanliness = -8f,
            refuseAbove = 101f },
    };

    [Header("Rest")]
    public float restEnergyPerSecond = 6f;
    public float noRestAbove = 90f;
    [TextArea] public string restLesson = "Pets need a quiet, comfy place to rest.";

    [Header("Cuddles")]
    [TextArea] public string cuddleLesson = "Gentle cuddles help your pet feel safe and loved.";

    [Header("Rewards")]
    [Tooltip("Coins (and growth) are only given when the main need was below this before the activity.")]
    public float rewardBelow = 70f;
    [Tooltip("Coins for a care action the pet needed. Care Points are separate (see CarePoints).")]
    public int pointsPerCare = 10;
    [Tooltip("Play is refused when energy is below this.")]
    public float minEnergyToPlay = 20f;
    [Tooltip("Extra happiness when the pet does its favourite activity (see its personality).")]
    public float lovedHappinessBonus = 10f;

    [Header("Hands-on care (CareInteraction components on this object)")]
    [Tooltip("Quality at or above this is rated Perfect: 1.5x coins and extra bond hearts.")]
    public float perfectAbove = 0.85f;
    [Tooltip("Quality below this is rated rough: half coins.")]
    public float goodAbove = 0.5f;
    [Tooltip("Share of the need restored by the roughest care (gentle care restores it all).")]
    public float roughNeedShare = 0.6f;

    [Header("Feedback")]
    public float statusShowTime = 6f;

    public event Action<CareActivity> ActivityCompleted;
    // Fired with the points earned whenever a care action the pet needed is finished.
    public event Action<int> CarePointsEarned;
    // Fired after every finished care action or rest: (was it needed, was it the favourite).
    public event Action<bool, bool> CareFinished;
    // Fired with the quality (0-1) of a hands-on care action the pet needed.
    public event Action<float> CareRated;
    // Fired when the pet wakes up after a rest (PetHealth counts it as after-care).
    public event Action Rested;

    // While set (by the vet clinic), hands-on prompts go here instead of the status banner.
    public Action<string> PromptOverride { get; set; }

    public bool IsResting => resting;

    ActivitySetup current;
    int tapsDone;
    bool resting;
    float restStartEnergy;
    Coroutine restRoutine;
    Coroutine statusRoutine;
    Coroutine ballRoutine;
    Vector2 ballHome;
    CareInteraction[] interactions;
    CareInteraction activeInteraction;
    CuddleInteraction cuddle;
    RestInteraction bedtime;
    CareTouch touch;
    PetHealth health;
    Action<float> externalDone;

    void Awake()
    {
        if (ball != null)
            ballHome = ball.anchoredPosition;
        interactions = GetComponents<CareInteraction>();
        // Exact types: the vet's tummy rub is a CuddleInteraction too.
        foreach (CareInteraction i in interactions)
        {
            if (!i.enabled) continue;
            if (cuddle == null && i.GetType() == typeof(CuddleInteraction)) cuddle = (CuddleInteraction)i;
            if (bedtime == null && i.GetType() == typeof(RestInteraction)) bedtime = (RestInteraction)i;
        }
        health = GetComponent<PetHealth>();
        if (interactions.Length > 0 && petRect != null)
            touch = CareTouch.Create(petRect.parent);
    }

    // Set by the mini-games menu's Ball Fetch card: start fetch when home opens.
    public static bool PlayFetchOnStart;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnLaunch()
    {
        PlayFetchOnStart = false;
    }

    void Start()
    {
        ShowStatus("Check your pet's needs at the top, then pick a care activity.");
        if (PlayFetchOnStart)
        {
            PlayFetchOnStart = false;
            StartCoroutine(PlayFetchSoon());
        }
    }

    IEnumerator PlayFetchSoon()
    {
        yield return new WaitForSeconds(0.6f); // let the loading screen fade first
        SelectPlay();
    }

    // Parameterless so they can be hooked to Button.onClick in the Inspector.
    public void SelectFeed() { Select(CareActivity.Feed); }
    public void SelectWater() { Select(CareActivity.Water); }
    public void SelectClean() { Select(CareActivity.Clean); }
    public void SelectGroom() { Select(CareActivity.Groom); }
    public void SelectPlay() { Select(CareActivity.Play); }

    // Hooked to the PET button: a hands-on cuddle when CuddleInteraction is set up,
    // otherwise the same quick cuddle as tapping the pet with nothing selected.
    public void SelectPet()
    {
        if (resting)
        {
            pet.Say("Zzz... I'm resting.");
            return;
        }
        CancelActivity();
        if (cuddle != null && touch != null)
            StartHandsOn(cuddle, PetState.Cuddling);
        else
            pet.Pet();
    }

    public void ToggleRest()
    {
        if (resting)
        {
            StopRest(true);
            return;
        }
        // Pressing Rest again while settling the pet down cancels it.
        if (activeInteraction != null && activeInteraction == bedtime)
        {
            CancelActivity();
            return;
        }

        CancelActivity();
        if (needs.Get(Need.Energy) >= noRestAbove)
        {
            pet.Refuse("I'm not sleepy yet!");
            return;
        }

        // Hands-on: the player settles the pet first, and RestInteraction calls FallAsleep().
        if (bedtime != null && touch != null)
            StartHandsOn(bedtime, PetState.Cuddling);
        else
            FallAsleep();
    }

    // Starts resting: energy recovers until it's full or the player taps Rest again.
    public void FallAsleep()
    {
        if (resting)
            return;
        resting = true;
        restStartEnergy = needs.Get(Need.Energy);
        pet.SetState(PetState.Resting);
        pet.Say("Zzz...");
        bool handsOn = bedtime != null && activeInteraction == bedtime;
        ShowStatus(handsOn
            ? "Your pet is asleep. Let it rest quietly, or tap Rest to wake it up."
            : "Your pet is resting. Tap Rest again to wake it up.", !handsOn);
        restRoutine = StartCoroutine(Rest());
    }

    // Shows a short message in the status banner (used by buttons for features that aren't built yet).
    public void ShowTip(string message)
    {
        ShowStatus(message);
    }

    // Stops a care activity that is waiting for taps (used before giving a shop treat).
    public void StopActivity()
    {
        CancelActivity();
    }

    // Shows an instruction that stays up until the next message (used by hands-on care steps).
    public void ShowPrompt(string message)
    {
        if (PromptOverride != null)
            PromptOverride(message);
        else
            ShowStatus(message, false);
    }

    // Runs a hands-on step for someone else (the vet clinic) and reports its quality.
    // Returns false when hands-on care isn't set up. Any other activity is stopped first.
    public bool RunInteraction(CareInteraction interaction, PetState state, Sprite prop, Action<float> onDone)
    {
        if (touch == null || interaction == null)
            return false;
        if (resting)
            StopRest(false);
        CancelActivity();
        pet.SetState(state, prop);
        externalDone = onDone;
        activeInteraction = interaction;
        interaction.Begin(this, touch.Area);
        touch.SetActive(interaction);
        return true;
    }

    // Called by a CareInteraction when the player has finished it.
    public void FinishInteraction(float quality)
    {
        CareInteraction done = activeInteraction;
        Action<float> callback = externalDone;
        StopInteraction();
        if (callback != null)
            callback(quality);
        else if (current != null)
            Complete(quality, true);
        else if (done != null && done == cuddle)
            FinishCuddle(quality);
    }

    // Cuddles never give coins (they'd be too easy to farm); they lift mood and grow the bond.
    void FinishCuddle(float quality)
    {
        pet.SetState(PetState.Idle);
        pet.Cuddled(quality);
        ShowStatus(Rating(quality) + cuddleLesson);
    }

    // Hooked to the pet image's Button.onClick.
    public void OnPetTapped()
    {
        if (current == null)
        {
            pet.Pet();
            return;
        }

        if (current.activity == CareActivity.Play)
        {
            pet.Say("Throw the ball!");
            return;
        }

        Step();
    }

    // Hooked to the ball's Button.onClick.
    public void OnBallTapped()
    {
        if (current == null || current.activity != CareActivity.Play)
        {
            SelectPlay();
            return;
        }
        if (ballRoutine == null)
            ballRoutine = StartCoroutine(ThrowBall());
    }

    void Select(CareActivity activity)
    {
        ActivitySetup setup = FindSetup(activity);
        if (setup == null)
        {
            Debug.LogWarning("No setup found for activity " + activity);
            return;
        }

        if (resting)
            StopRest(false);
        CancelActivity();

        if (needs.Get(setup.mainNeed) >= setup.refuseAbove)
        {
            pet.Refuse(setup.refuseMessage);
            return;
        }
        if (activity == CareActivity.Play && health != null && health.IsSick)
        {
            pet.Refuse("I don't feel well enough to play...");
            return;
        }
        if (activity == CareActivity.Play && needs.Get(Need.Energy) < minEnergyToPlay)
        {
            pet.Refuse("I'm too tired to play. Can I rest first?");
            return;
        }

        current = setup;
        tapsDone = 0;
        pet.SetState(setup.petState, setup.prop);

        CareInteraction handsOn = FindInteraction(activity);
        if (handsOn != null && touch != null)
            StartHandsOn(handsOn, null);
        else
            ShowStatus(setup.prompt, false);
    }

    void StartHandsOn(CareInteraction interaction, PetState? state)
    {
        if (state.HasValue)
            pet.SetState(state.Value);
        activeInteraction = interaction;
        interaction.Begin(this, touch.Area);
        touch.SetActive(interaction);
        ShowStatus(interaction.Prompt, false);
    }

    void Step()
    {
        tapsDone++;
        pet.ActivityStep();
        if (tapsDone >= current.tapsRequired)
            Complete();
    }

    // quality is 1 for tap activities; hands-on care passes how gently and thoroughly it was done.
    void Complete(float quality = 1f, bool handsOn = false)
    {
        ActivitySetup done = current;
        current = null;

        bool needed = needs.Get(done.mainNeed) < rewardBelow;
        needs.Add(done.mainNeed, done.mainAmount * Mathf.Lerp(roughNeedShare, 1f, quality));
        if (done.happiness != 0f) needs.Add(Need.Happiness, done.happiness);
        if (done.energy != 0f) needs.Add(Need.Energy, done.energy);
        if (done.cleanliness != 0f) needs.Add(Need.Cleanliness, done.cleanliness);
        if (done.hunger != 0f) needs.Add(Need.Hunger, done.hunger);
        if (done.thirst != 0f) needs.Add(Need.Thirst, done.thirst);

        bool loved = PetProfile.Loves(done.activity);
        if (loved)
            needs.Add(Need.Happiness, lovedHappinessBonus);

        pet.SetState(PetState.Idle);
        pet.Celebrate(loved ? PetLines.Loved(PetProfile.IsCat, done.activity) : new Line(done.doneMessage),
            done.activity == CareActivity.Clean, loved);
        // Care Points follow the same rule as coins: only for finished care the pet needed.
        int carePoints = needed ? CarePointsFor(done.activity) : 0;
        Reward(needed, done.lesson, handsOn ? quality : (float?)null, carePoints);
        if (carePoints > 0)
            CarePoints.Add(carePoints, done.activity.ToString());
        ActivityCompleted?.Invoke(done.activity);
        CareFinished?.Invoke(needed, loved);
        if (handsOn && needed)
            CareRated?.Invoke(quality);
        SaveProgress();
    }

    // Checkpoint after each finished care action.
    void SaveProgress()
    {
        needs.Store();
        SaveSystem.SaveIfDirty();
    }

    // Care Points for each finished activity (Water, Groom and Rest give coins only).
    static int CarePointsFor(CareActivity activity)
    {
        switch (activity)
        {
            case CareActivity.Feed: return CarePoints.Feed;
            case CareActivity.Clean: return CarePoints.Bath;
            case CareActivity.Play: return CarePoints.Play;
            default: return 0;
        }
    }

    // quality is null for care without a rating (tap activities, rest).
    void Reward(bool needed, string lesson, float? quality = null, int carePoints = 0)
    {
        string rating = quality.HasValue ? Rating(quality.Value) : "";
        float coinScale = quality.HasValue ? CoinScale(quality.Value) : 1f;

        if (needed)
        {
            int coins = Mathf.Max(1, Mathf.RoundToInt(pointsPerCare * coinScale));
            string care = carePoints > 0 ? "   +" + carePoints + " Care Points" : "";
            ShowStatus(rating + lesson + "\n<size=80%>+" + coins + " coins" + care + "</size>");
            CoinCounter.Give(coins, pet.RewardTextPosition);
            CarePointsEarned?.Invoke(coins);
        }
        else
        {
            ShowStatus(rating + lesson + "\n<size=80%>Coins and Care Points are earned when your pet really needs the care.</size>");
        }
    }

    // Coins multiplier for a rated care action (also used by the vet).
    public float CoinScale(float quality)
    {
        if (quality >= perfectAbove) return 1.5f;
        return quality >= goodAbove ? 1f : 0.5f;
    }

    public string Rating(float quality)
    {
        if (quality >= perfectAbove) return "<b>Perfect care!</b> ";
        if (quality >= goodAbove) return "<b>Good job!</b> ";
        return "<b>A bit rough...</b> Gentle hands make a happy pet. ";
    }

    void CancelActivity()
    {
        // A cuddle or bedtime settle-down has no ActivitySetup, so reset the pet here.
        bool settling = activeInteraction != null && current == null && !resting;
        StopInteraction();
        if (settling)
            pet.SetState(PetState.Idle);
        if (current == null)
            return;
        current = null;
        pet.SetState(PetState.Idle);
    }

    void StopInteraction()
    {
        externalDone = null;
        if (activeInteraction != null)
            activeInteraction.Cancel();
        activeInteraction = null;
        if (touch != null)
            touch.SetActive(null);
    }

    // Only interactions tied to a CareActivity; cuddles and bedtime are started directly.
    CareInteraction FindInteraction(CareActivity activity)
    {
        foreach (CareInteraction i in interactions)
        {
            if (i.enabled && i.Activity == activity)
                return i;
        }
        return null;
    }

    IEnumerator Rest()
    {
        while (needs.Get(Need.Energy) < 100f)
        {
            needs.Add(Need.Energy, restEnergyPerSecond * Time.deltaTime);
            yield return null;
        }
        restRoutine = null;
        StopRest(true);
    }

    void StopRest(bool celebrate)
    {
        if (!resting)
            return;
        resting = false;
        if (restRoutine != null)
        {
            StopCoroutine(restRoutine);
            restRoutine = null;
        }

        // A hands-on bedtime is rated by how quietly the pet was left to sleep.
        float? quality = null;
        if (activeInteraction != null && activeInteraction == bedtime)
        {
            quality = bedtime.Quality;
            StopInteraction();
        }

        bool needed = restStartEnergy < rewardBelow && needs.Get(Need.Energy) - restStartEnergy >= 25f;
        bool loved = PetProfile.LovesRest && needed;
        pet.SetState(PetState.Idle);
        if (celebrate)
        {
            if (loved)
                needs.Add(Need.Happiness, lovedHappinessBonus);
            pet.Celebrate(PetLines.Woke(PetProfile.IsCat, loved), false, loved);
            Reward(needed, restLesson, quality);
            CareFinished?.Invoke(needed, loved);
            Rested?.Invoke();
            if (quality.HasValue && needed)
                CareRated?.Invoke(quality.Value);
        }
        SaveProgress();
    }

    IEnumerator ThrowBall()
    {
        Vector2 target = petRect.anchoredPosition + new Vector2(0f, petRect.rect.height * 0.6f);
        yield return MoveBall(ballHome, target, 0.45f, 220f);
        Step();
        yield return MoveBall(target, ballHome, 0.45f, 160f);
        ball.anchoredPosition = ballHome;
        ballRoutine = null;
    }

    IEnumerator MoveBall(Vector2 from, Vector2 to, float time, float arc)
    {
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            float k = t / time;
            ball.anchoredPosition = Vector2.Lerp(from, to, k) + new Vector2(0f, Mathf.Sin(k * Mathf.PI) * arc);
            ball.localRotation = Quaternion.Euler(0f, 0f, -360f * k);
            yield return null;
        }
        ball.localRotation = Quaternion.identity;
    }

    ActivitySetup FindSetup(CareActivity activity)
    {
        for (int i = 0; i < activities.Length; i++)
        {
            if (activities[i].activity == activity)
                return activities[i];
        }
        return null;
    }

    void ShowStatus(string message, bool autoHide = true)
    {
        if (statusText == null)
            return;
        statusText.text = PetProfile.Fill(message);
        if (statusRoutine != null)
            StopCoroutine(statusRoutine);
        statusRoutine = StartCoroutine(StatusFade(autoHide));
    }

    IEnumerator StatusFade(bool autoHide)
    {
        if (statusGroup != null)
            statusGroup.alpha = 1f;
        if (!autoHide)
            yield break;

        yield return new WaitForSeconds(statusShowTime);
        if (statusGroup == null)
            yield break;
        for (float t = 0f; t < 0.4f; t += Time.deltaTime)
        {
            statusGroup.alpha = 1f - t / 0.4f;
            yield return null;
        }
        statusGroup.alpha = 0f;
    }
}
