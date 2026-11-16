using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The vet clinic. VET opens it over the home room: the room turns into a
// clinic with an exam table, Dr. Mia the veterinarian greets the pet and asks
// what brings you in, and the visit is played hands-on - answering the vet's
// questions, helping with the exam, giving the treatment and taking the
// after-care advice home. Visits are free; coins and bond hearts are only given
// when the visit was really needed (a sick pet, a check-up that's due, a
// vaccine that's due). The room is built at runtime; the vet and the clinic
// sign come from vetSprite and signSprite (simple drawn versions are used when empty).
public class VetClinic : MonoBehaviour
{
    [Header("References")]
    public PetCareManager care;
    public PetHealth health;
    public PetBond bond;
    [Tooltip("A care button to copy for the answer buttons, so they match the game's style.")]
    public Button choiceTemplate;
    [Tooltip("The VET button; it pulses while the pet is sick.")]
    public RectTransform medicineButton;
    [Tooltip("Hidden while the clinic is open.")]
    public GameObject[] hideWhileOpen;
    [Tooltip("Faded and not pressable while the clinic is open.")]
    public GameObject[] blockWhileOpen;

    [Header("Look")]
    public string vetName = "Dr. Mia";
    [Tooltip("Shown under the vet's name on the dialogue card.")]
    public string vetTitle = "Veterinarian";
    [Tooltip("Picture of the vet (Sprites/Vet/Veterinarian); a simple drawn vet is used when empty.")]
    public Sprite vetSprite;
    [Tooltip("Clinic sign on the wall (Sprites/Vet/VetClinicSign); a drawn paw sign is used when empty.")]
    public Sprite signSprite;
    public Sprite roundSprite;
    public Sprite panelSprite;
    public Sprite bodySprite;
    public Sprite shadowSprite;
    public Sprite closeSprite;
    public TMP_FontAsset font;
    public Color textColor = new Color(0.42f, 0.26f, 0.15f);
    public Color nameColor = new Color(0.2f, 0.52f, 0.55f);
    public Color clinicColor = new Color(0.86f, 0.95f, 0.94f, 0.95f);
    public Color cardColor = new Color(1f, 0.98f, 0.94f);
    public float charsPerSecond = 70f;

    [Header("Consultation view")]
    [Tooltip("How much the room zooms in on the vet and the pet during a visit (1 = no zoom).")]
    public float focusZoom = 1.1f;
    public float focusTime = 0.6f;
    public Color accentColor = new Color(0.36f, 0.72f, 0.72f);
    public Color headerColor = new Color(0.88f, 0.96f, 0.95f);
    [Tooltip("A solid circle, used to cut the vet's portrait round.")]
    public Sprite circleSprite;
    [Tooltip("Where the vet's face is on vetSprite (0-1) and how big the picture is drawn in the portrait.")]
    public Vector2 portraitFace = new Vector2(0.367f, 0.79f);
    public float portraitScale = 0.3f;

    [Header("Checkup icons")]
    public Sprite healthIcon;
    public Sprite foodIcon;
    public Sprite waterIcon;
    public Sprite energyIcon;
    public Sprite cleanIcon;

    [Header("Tools")]
    public Sprite stethoscopeSprite;
    public Sprite medicineSprite;
    public Sprite combSprite;
    public Sprite dropperSprite;

    [Header("Rewards (only when the visit was needed)")]
    public int checkupCoins = 15;
    public int treatmentCoins = 25;
    public int vaccineCoins = 20;
    public int visitHearts = 5;
    public int perfectVisitHearts = 3;
    public int goodAnswerHearts = 1;

    public bool IsOpen { get; private set; }

    StethoscopeInteraction stethoscope;
    InspectInteraction inspect;
    MedicineSpoonInteraction spoon;
    FleaCombInteraction fleaComb;
    TummyRubInteraction tummyRub;
    EarDropsInteraction earDrops;
    VaccineInteraction vaccine;

    bool built;
    CanvasGroup roomGroup;
    RectTransform room;
    RectTransform vet;
    RectTransform table;
    RectTransform legL;
    RectTransform legR;
    RectTransform sign;
    Vector2 vetHome;
    CanvasGroup cardGroup;
    TMP_Text body;
    RectTransform choiceRow;
    readonly List<Button> choices = new List<Button>();
    readonly List<CanvasGroup> blocked = new List<CanvasGroup>();
    readonly List<GameObject> hidden = new List<GameObject>();
    Coroutine typing;
    Coroutine fade;
    bool skipTyping;
    bool pulsing;

    // Presentation: the room zooms in on the vet and pet, the vet acts a little,
    // and the card slides in. None of it changes what happens in the visit.
    enum VetPose { Idle, Talk, Examine, Notes }
    RectTransform focusTarget;
    Vector2 focusBasePos;
    Vector3 focusBaseScale = Vector3.one;
    Coroutine focusRoutine;
    VetPose pose;
    float poseX;
    float poseAngle;
    float gesture;
    bool stepRunning;
    RectTransform cardRt;
    Vector2 cardHome;
    RectTransform portraitRing;
    RectTransform statRow;
    readonly List<StatChip> chips = new List<StatChip>();

    class StatChip
    {
        public RectTransform root;
        public CanvasGroup group;
        public Image icon;
        public RectTransform bar;
        public Image barFill;
        public TMP_Text label;
    }

    // This visit.
    int goodAnswers;
    readonly List<float> qualities = new List<float>();
    bool rewardDue;

    PetController Pet => care.pet;
    float VisitQuality
    {
        get
        {
            if (qualities.Count == 0)
                return 1f;
            float sum = 0f;
            foreach (float q in qualities)
                sum += q;
            return sum / qualities.Count;
        }
    }

    void Awake()
    {
        stethoscope = GetComponent<StethoscopeInteraction>();
        inspect = GetComponent<InspectInteraction>();
        spoon = GetComponent<MedicineSpoonInteraction>();
        fleaComb = GetComponent<FleaCombInteraction>();
        tummyRub = GetComponent<TummyRubInteraction>();
        earDrops = GetComponent<EarDropsInteraction>();
        vaccine = GetComponent<VaccineInteraction>();
        if (inspect != null)
            inspect.SpotChecked += OnSpotChecked;
    }

    void OnDestroy()
    {
        if (inspect != null)
            inspect.SpotChecked -= OnSpotChecked;
    }

    void Update()
    {
        // The VET button pulses while the pet is sick.
        bool pulse = medicineButton != null && health != null && health.IsSick && !IsOpen && !ReduceMotion.On;
        if (pulse)
            medicineButton.localScale = Vector3.one * (1f + Mathf.Sin(Time.unscaledTime * 5f) * 0.06f);
        else if (pulsing && medicineButton != null)
            medicineButton.localScale = Vector3.one;
        pulsing = pulse;

        if (IsOpen && vet != null && !ReduceMotion.On)
        {
            vet.localScale = new Vector3(1f, 1f + Mathf.Sin(Time.time * 2f) * 0.012f + (typing != null ? Mathf.Abs(Mathf.Sin(Time.time * 14f)) * 0.012f : 0f), 1f);
            Act();
        }
    }

    // Subtle acting: leans toward the pet while talking, steps in to examine,
    // leans back to check her notes. Rotates around the feet (pivot at the bottom).
    void Act()
    {
        float x = 0f, angle = 0f;
        switch (pose)
        {
            case VetPose.Talk: angle = -2.5f; break;
            case VetPose.Examine: x = 60f; angle = -6f; break;
            case VetPose.Notes: angle = 2f; break;
        }
        poseX = Mathf.Lerp(poseX, x, Time.deltaTime * 4f);
        poseAngle = Mathf.Lerp(poseAngle, angle, Time.deltaTime * 4f);
        gesture = Mathf.Max(0f, gesture - Time.deltaTime * 1.6f);
        float wave = Mathf.Sin(gesture * Mathf.PI) * 3f;
        if (fade == null)
            vet.anchoredPosition = vetHome + new Vector2(poseX, 0f);
        vet.localRotation = Quaternion.Euler(0f, 0f, poseAngle - wave);

        // The portrait gently pulses while the vet is speaking.
        if (portraitRing != null)
            portraitRing.localScale = Vector3.one * (typing != null ? 1f + Mathf.Abs(Mathf.Sin(Time.time * 6f)) * 0.035f : 1f);
    }

    Vector3 VetHead => vet.TransformPoint(new Vector3(0f, vet.rect.yMax * 0.8f, 0f));

    // ---- Open / close (hooked to the VET and close buttons) ----

    public void Open()
    {
        if (IsOpen || care == null || health == null)
            return;
        if (care.IsResting)
        {
            care.ShowTip("Your pet is napping. Let it wake up first, then visit the vet.");
            return;
        }

        if (!built)
            Build();
        Layout();
        care.StopActivity();
        IsOpen = true;
        care.PromptOverride = Say;
        HideHome(true);
        Pet.SetState(PetState.AtVet);
        pose = VetPose.Idle;
        poseX = poseAngle = 0f;
        Show(true);
        Focus(true);

        goodAnswers = 0;
        qualities.Clear();
        rewardDue = false;
        Greeting();
    }

    public void Close()
    {
        if (!IsOpen)
            return;
        IsOpen = false;
        care.StopActivity();
        care.PromptOverride = null;
        if (typing != null)
        {
            StopCoroutine(typing);
            typing = null;
        }
        HideChoices();
        ShowStats(false);
        HideHome(false);
        Pet.Watch(null);
        Pet.SetState(PetState.Idle);
        stepRunning = false;
        Show(false);
        Focus(false);
        SaveSystem.SaveIfDirty();

        if (health.IsRecovering)
            care.ShowTip("After-care for your pet: <b>" + VetDialogue.StepList(health.AfterCareLeft) + "</b>. " + VetDialogue.AfterCareAdvice(health.RecoveringFrom));
        else if (health.IsSick)
            care.ShowTip("Your pet still isn't feeling well. Visit the vet when you're ready.");
    }

    // ---- The visit ----

    void Greeting()
    {
        Pet.Say(PetProfile.Trait == Trait.Shy ? "Is this the vet...? I'm a bit scared."
            : PetProfile.IsCat ? "Mrrp? So many new smells..." : "Ooh! New smells everywhere!");

        string text = "Hello! I'm " + vetName + ", an animal doctor. And who's this? Hi, " + PetProfile.Name + "!";
        if (health.IsRecovering)
            text += "\nHow's the recovery going? Remember: " + VetDialogue.AfterCareAdvice(health.RecoveringFrom);
        text += "\nWhat brings you in today?";

        var options = new List<(string, Action)>();
        if (health.IsSick)
            options.Add(("Not feeling well", SickVisit));
        options.Add(("Pet checkup", Checkup));
        if (health.VaccineDue)
            options.Add(("Vaccine", VaccineVisit));
        options.Add(("Just saying hi", () => Say("Come back any time! Regular check-ups help catch problems early.", ("Back home", Close))));
        Say(text, options.ToArray());
    }

    void Checkup()
    {
        rewardDue = health.CheckupDue;
        Ask(VetDialogue.RandomCareQuestion(), () =>
            Step(stethoscope, PetState.AtVet, stethoscopeSprite, q =>
                Say("A strong, steady heartbeat! Now let's check the rest.", ("Okay", () => Inspect(FinishCheckup)))));
    }

    void FinishCheckup()
    {
        bool due = rewardDue;
        if (due)
        {
            // A finished check-up earns Care Points once per check-up cooldown.
            health.RecordCheckup();
            CarePoints.Add(CarePoints.VetCheckup, "Vet checkup");
        }
        if (health.IsSick)
        {
            // The check-up found something: treat it now.
            rewardDue = true;
            Say("Hmm, I found something...", ("What is it?", Diagnose));
            return;
        }

        int coins = Reward(checkupCoins);
        string end = due ? "   <size=85%>+" + CarePoints.VetCheckup + " Care Points</size>"
            : "\n<size=80%>Check-ups give a reward every " + Mathf.RoundToInt(health.checkupCooldownMinutes) + " minutes.</size>";
        Pet.Celebrate(new Line("I was so good!", LineIcon.Star));
        SayWithStats("Your pet is healthy! " + VetDialogue.HealthyAdvice(care.needs) + "\n" + RewardLine(coins) + end,
            ("Thanks, " + vetName + "!", WrapUp));
    }

    void SickVisit()
    {
        rewardDue = true;
        var options = new List<(string, Action)>();
        foreach (KeyValuePair<string, bool> choice in VetDialogue.SymptomChoices(health.Current))
        {
            bool right = choice.Value;
            options.Add((choice.Key, () =>
            {
                if (right)
                    goodAnswers++;
                Say(right ? "Good eye! Noticing changes early really helps your pet." : "Hmm, let's take a careful look to be sure.",
                    ("Let's check", () => Inspect(Diagnose)));
            }));
        }
        Say("Oh no. What have you noticed about your pet?", options.ToArray());
    }

    void Inspect(Action next)
    {
        Step(inspect, PetState.AtVet, null, q => StartCoroutine(After(1.6f, next)));
    }

    void OnSpotChecked(int spot)
    {
        if (IsOpen)
            Say(VetDialogue.Finding(spot, health.Current));
    }

    void Diagnose()
    {
        Say(VetDialogue.Diagnosis(health.Current), ("Let's treat it", Treat));
    }

    void Treat()
    {
        switch (health.Current)
        {
            case Illness.Cold: Step(spoon, PetState.Drinking, medicineSprite, Treated); break;
            case Illness.Fleas: Step(fleaComb, PetState.Grooming, combSprite, Treated); break;
            case Illness.TummyAche: Step(tummyRub, PetState.Cuddling, null, Treated); break;
            case Illness.EarInfection: Step(earDrops, PetState.Bathing, dropperSprite, Treated); break;
            default: WrapUp(); break;
        }
    }

    void Treated(float quality)
    {
        Illness was = health.Current;
        health.Treat();
        int coins = Reward(treatmentCoins);
        Pet.Celebrate(new Line("Thank you... I feel a bit better already.", LineIcon.Heart), was == Illness.Fleas);
        Say("All done! " + VetDialogue.AfterCareAdvice(was) + " Once that's done, your pet will be all better.\n" + RewardLine(coins),
            ("I'll do that!", WrapUp));
    }

    void VaccineVisit()
    {
        rewardDue = health.VaccineDue;
        Ask(VetDialogue.VaccineQuestion, () =>
            Step(vaccine, PetState.Cuddling, null, q =>
            {
                health.GiveVaccine();
                int coins = Reward(vaccineCoins);
                Sticker();
                Say("Brave pet! Here's a sticker. This vaccine helps protect your pet from serious illnesses.\n" + RewardLine(coins),
                    ("Thanks!", WrapUp));
            }));
    }

    // The health record card.
    void WrapUp()
    {
        health.RecordVisit();
        float since = health.MinutesSinceCheckup;
        string last = since == float.MaxValue ? "never" : since < 2f ? "just now" : since < 60f ? Mathf.RoundToInt(since) + " min ago" : since < 60f * 24f ? Mathf.RoundToInt(since / 60f) + " h ago" : Mathf.RoundToInt(since / 1440f) + " days ago";
        string status = health.IsSick ? "Not well yet" : health.IsRecovering ? "Recovering - to do: " + VetDialogue.StepList(health.AfterCareLeft) : "Healthy";
        string vaccines = health.Vaccines + " / " + health.maxVaccines + (health.VaccineDue ? " (next one due)" : "");
        pose = VetPose.Notes;
        SayWithStats("<b>Health record: " + PetProfile.Name + "</b>\nStatus: " + status + "\nVaccines: " + vaccines + "     Check-up: " + last + "     Visits: " + SaveSystem.Data.vetVisits,
            ("Back home", Close));
    }

    void Ask(VetQuestion question, Action next)
    {
        var answers = new List<VetAnswer>(question.answers);
        VetDialogue.Shuffle(answers);
        var options = new List<(string, Action)>();
        foreach (VetAnswer a in answers)
        {
            VetAnswer answer = a;
            options.Add((answer.text, () =>
            {
                if (answer.good)
                    goodAnswers++;
                Say(answer.reply, ("Okay", next));
            }));
        }
        Say(question.text, options.ToArray());
    }

    void Step(CareInteraction step, PetState state, Sprite tool, Action<float> done)
    {
        if (step == null)
        {
            done(1f);
            return;
        }
        Say(step.Prompt);
        stepRunning = true;
        pose = VetPose.Examine;
        Pet.Watch(null); // the step decides what the pet watches
        bool started = care.RunInteraction(step, state, tool, q =>
        {
            stepRunning = false;
            pose = VetPose.Idle;
            qualities.Add(q);
            Pet.SetState(PetState.AtVet);
            done(q);
        });
        if (!started)
        {
            stepRunning = false;
            pose = VetPose.Idle;
            done(1f);
        }
    }

    int Reward(int baseCoins)
    {
        if (!rewardDue)
            return 0;
        rewardDue = false;
        float q = VisitQuality;
        int coins = Mathf.Max(1, Mathf.RoundToInt(baseCoins * care.CoinScale(q)));
        CoinCounter.Give(coins, Pet.RewardTextPosition);
        if (bond != null)
            bond.Add(visitHearts + (q >= care.perfectAbove ? perfectVisitHearts : 0) + goodAnswers * goodAnswerHearts);
        SaveSystem.SaveIfDirty();
        return coins;
    }

    string RewardLine(int coins)
    {
        string rating = qualities.Count > 0 ? care.Rating(VisitQuality) : "";
        return rating + (coins > 0 ? "<size=85%>+" + coins + " coins</size>" : "");
    }

    void Sticker()
    {
        UIBurst fx = ReduceMotion.On ? null : UIBurst.Instance;
        if (fx != null && fx.confetti != null)
            fx.Burst(Pet.PetPoint(0.5f, 0.8f), fx.confetti, 24, UIBurst.Confetti, new Color(1f, 0.6f, 0.65f), new Color(0.55f, 0.8f, 1f), new Color(1f, 0.85f, 0.4f));
        SFXManager.Play(Sfx.Jingle);
    }

    IEnumerator After(float seconds, Action next)
    {
        yield return new WaitForSeconds(seconds);
        if (IsOpen)
            next();
    }

    // ---- Dialogue card ----

    void Say(string text)
    {
        Say(text, new (string, Action)[0]);
    }

    void Say(string text, params (string label, Action action)[] options)
    {
        Say(text, false, options);
    }

    // The same, with the pet's health and needs shown as small chips under the text.
    void SayWithStats(string text, params (string label, Action action)[] options)
    {
        Say(text, true, options);
    }

    void Say(string text, bool stats, (string label, Action action)[] options)
    {
        if (!IsOpen || body == null)
            return;
        HideChoices();
        ShowStats(stats);
        body.text = PetProfile.Fill(text);
        if (typing != null)
            StopCoroutine(typing);
        typing = StartCoroutine(Type(options));
    }

    IEnumerator Type((string label, Action action)[] options)
    {
        skipTyping = false;
        // Talking: a little gesture, and the pet looks up at the vet (unless a step is running).
        if (pose != VetPose.Notes && !stepRunning)
            pose = VetPose.Talk;
        gesture = 1f;
        if (!stepRunning)
            Pet.Watch(VetHead);
        body.ForceMeshUpdate();
        int total = body.textInfo.characterCount;
        if (!ReduceMotion.On)
        {
            float shown = 0f;
            while (shown < total && !skipTyping)
            {
                shown += Time.unscaledDeltaTime * charsPerSecond;
                body.maxVisibleCharacters = Mathf.Min(total, (int)shown);
                yield return null;
            }
        }
        body.maxVisibleCharacters = 99999;
        typing = null;
        if (pose == VetPose.Talk)
            pose = VetPose.Idle;
        ShowChoices(options);
    }

    void SkipTyping()
    {
        skipTyping = true;
    }

    void ShowChoices((string label, Action action)[] options)
    {
        for (int i = 0; i < options.Length; i++)
        {
            if (i >= choices.Count)
                choices.Add(MakeChoice());
            Button b = choices[i];
            Action action = options[i].action;
            b.GetComponentInChildren<TMP_Text>(true).text = options[i].label;
            b.onClick.RemoveAllListeners();
            b.onClick.AddListener(() =>
            {
                HideChoices();
                SFXManager.Play(Sfx.Pop, 0.6f);
                action();
            });
            b.gameObject.SetActive(true);
            StartCoroutine(PopIn(b.transform, i * 0.06f));
        }
    }

    // Answer buttons ease in one after another.
    IEnumerator PopIn(Transform t, float delay)
    {
        CanvasGroup g = t.GetComponent<CanvasGroup>();
        if (g == null)
            g = t.gameObject.AddComponent<CanvasGroup>();
        if (ReduceMotion.On)
        {
            g.alpha = 1f;
            t.localScale = Vector3.one;
            yield break;
        }
        g.alpha = 0f;
        t.localScale = Vector3.one * 0.9f;
        yield return new WaitForSecondsRealtime(delay);
        for (float k = 0f; k < 1f; k += Time.unscaledDeltaTime / 0.2f)
        {
            g.alpha = k;
            t.localScale = Vector3.one * Mathf.Lerp(0.9f, 1f, Ease.OutQuad(k));
            yield return null;
        }
        g.alpha = 1f;
        t.localScale = Vector3.one;
    }

    void HideChoices()
    {
        foreach (Button b in choices)
            b.gameObject.SetActive(false);
    }

    // ---- Showing and hiding ----

    void HideHome(bool hide)
    {
        if (hide)
        {
            hidden.Clear();
            foreach (GameObject go in hideWhileOpen)
            {
                if (go != null && go.activeSelf)
                {
                    go.SetActive(false);
                    hidden.Add(go);
                }
            }
        }
        else
        {
            foreach (GameObject go in hidden)
            {
                if (go != null)
                    go.SetActive(true);
            }
            hidden.Clear();
        }

        foreach (CanvasGroup g in blocked)
        {
            if (g == null)
                continue;
            g.interactable = !hide;
            g.blocksRaycasts = !hide;
            g.alpha = hide ? 0.35f : 1f;
        }
    }

    void Show(bool show)
    {
        if (fade != null)
            StopCoroutine(fade);
        if (show)
        {
            roomGroup.gameObject.SetActive(true);
            cardGroup.gameObject.SetActive(true);
        }
        fade = StartCoroutine(Fade(show));
    }

    IEnumerator Fade(bool show)
    {
        float from = cardGroup.alpha, to = show ? 1f : 0f;
        float time = ReduceMotion.On ? 0f : 0.3f;
        Vector2 vetOff = vetHome + new Vector2(-260f, 0f);
        Vector2 cardOff = cardHome + new Vector2(70f, 0f);
        for (float t = 0f; t < time; t += Time.unscaledDeltaTime)
        {
            float k = Mathf.Lerp(from, to, t / time);
            roomGroup.alpha = cardGroup.alpha = k;
            vet.anchoredPosition = Vector2.Lerp(vetOff, vetHome, Ease.OutQuad(k));
            cardRt.anchoredPosition = Vector2.Lerp(cardOff, cardHome, Ease.OutQuad(k));
            yield return null;
        }
        roomGroup.alpha = cardGroup.alpha = to;
        vet.anchoredPosition = vetHome;
        cardRt.anchoredPosition = cardHome;
        cardGroup.blocksRaycasts = show;
        if (!show)
        {
            roomGroup.gameObject.SetActive(false);
            cardGroup.gameObject.SetActive(false);
        }
        fade = null;
    }

    // ---- "Camera": the room eases in on the vet and pet, and back out ----

    void Focus(bool on)
    {
        if (focusTarget == null)
            return;
        if (focusRoutine != null)
            StopCoroutine(focusRoutine);
        Vector2 pos = focusBasePos;
        Vector3 scale = focusBaseScale;
        if (on)
            Framing(out pos, out scale);
        focusRoutine = StartCoroutine(FocusTo(pos, scale));
    }

    // Zooms a little and centres the vet and pet in the space left of the card,
    // keeping their feet on screen so nothing drops off the bottom.
    void Framing(out Vector2 pos, out Vector3 scale)
    {
        RectTransform parent = (RectTransform)focusTarget.parent;
        // Measured from the unzoomed room.
        Vector3 keepPos = focusTarget.anchoredPosition;
        Vector3 keepScale = focusTarget.localScale;
        focusTarget.anchoredPosition = focusBasePos;
        focusTarget.localScale = focusBaseScale;

        Vector2 vetMid = parent.InverseTransformPoint(room.TransformPoint(vetHome + new Vector2(0f, vet.sizeDelta.y * 0.5f)));
        Vector2 petMid = parent.InverseTransformPoint(care.petRect.TransformPoint(care.petRect.rect.center));
        Vector2 feet = parent.InverseTransformPoint(care.petRect.position);
        Vector2 cardLeft = parent.InverseTransformPoint(cardRt.TransformPoint(new Vector3(cardRt.rect.xMin, 0f, 0f)));
        Vector2 pivot = parent.InverseTransformPoint(focusTarget.position);

        // On very wide screens the room sits low; lift it so the feet stay in view.
        Vector2 focus = new Vector2((vetMid.x + petMid.x) * 0.5f, feet.y);
        Vector2 target = new Vector2((parent.rect.xMin + cardLeft.x) * 0.5f, Mathf.Max(feet.y, parent.rect.yMin + 40f));
        float s = Mathf.Max(1f, focusZoom);
        Vector2 offset = target - pivot - s * (focus - pivot);

        focusTarget.anchoredPosition = keepPos;
        focusTarget.localScale = keepScale;
        pos = focusBasePos + offset;
        scale = focusBaseScale * s;
    }

    IEnumerator FocusTo(Vector2 pos, Vector3 scale)
    {
        Vector2 fromPos = focusTarget.anchoredPosition;
        Vector3 fromScale = focusTarget.localScale;
        float time = ReduceMotion.On ? 0f : focusTime;
        for (float t = 0f; t < time; t += Time.unscaledDeltaTime)
        {
            float k = Ease.InOutSine(t / time);
            focusTarget.anchoredPosition = Vector2.Lerp(fromPos, pos, k);
            focusTarget.localScale = Vector3.Lerp(fromScale, scale, k);
            yield return null;
        }
        focusTarget.anchoredPosition = pos;
        focusTarget.localScale = scale;
        focusRoutine = null;
    }

    // ---- Checkup chips: health and the four needs, from the existing pet data ----

    void ShowStats(bool show)
    {
        if (statRow == null)
            return;
        statRow.gameObject.SetActive(show);
        if (!show)
            return;

        SetChip(chips[0], healthIcon, health.IsSick ? "Not well" : health.IsRecovering ? "Getting better" : "Healthy",
            health.IsSick ? 0.2f : health.IsRecovering ? 0.6f : 1f);
        SetChip(chips[1], foodIcon, "Food", care.needs.Get(Need.Hunger) / 100f);
        SetChip(chips[2], waterIcon, "Water", care.needs.Get(Need.Thirst) / 100f);
        SetChip(chips[3], energyIcon, "Energy", care.needs.Get(Need.Energy) / 100f);
        SetChip(chips[4], cleanIcon, "Clean", care.needs.Get(Need.Cleanliness) / 100f);
        for (int i = 0; i < chips.Count; i++)
            StartCoroutine(PopIn(chips[i].root, 0.1f + i * 0.05f));
    }

    void SetChip(StatChip chip, Sprite icon, string label, float value)
    {
        value = Mathf.Clamp01(value);
        chip.icon.sprite = icon;
        chip.icon.enabled = icon != null;
        chip.label.text = label;
        chip.bar.anchorMax = new Vector2(Mathf.Max(0.04f, value), 1f);
        chip.barFill.color = value < 0.25f ? new Color(0.92f, 0.45f, 0.42f) : value < 0.5f ? new Color(0.96f, 0.7f, 0.35f) : new Color(0.45f, 0.78f, 0.55f);
    }

    // ---- Building the clinic (once, at runtime) ----

    void Build()
    {
        built = true;
        RectTransform panel = (RectTransform)care.petRect.parent;
        Transform canvas = care.petRect.GetComponentInParent<Canvas>().rootCanvas.transform;
        // The room (pet, clinic, effects) is what the "camera" zooms.
        focusTarget = panel;
        focusBasePos = panel.anchoredPosition;
        focusBaseScale = panel.localScale;

        foreach (GameObject go in blockWhileOpen)
        {
            if (go == null)
                continue;
            CanvasGroup g = go.GetComponent<CanvasGroup>();
            if (g == null)
                g = go.AddComponent<CanvasGroup>();
            blocked.Add(g);
        }

        // The clinic room sits behind the pet, over the home background.
        room = Rect("VetClinicRoom", panel);
        Stretch(room);
        room.SetSiblingIndex(0);
        roomGroup = room.gameObject.AddComponent<CanvasGroup>();
        roomGroup.blocksRaycasts = false;
        roomGroup.alpha = 0f;
        // Bigger than the room, so it still fills the screen when the "camera" moves.
        RectTransform backdrop = Img("Backdrop", room, null, clinicColor, Vector2.zero, Vector2.zero, true).rectTransform;
        backdrop.offsetMin = new Vector2(-600f, -600f);
        backdrop.offsetMax = new Vector2(600f, 600f);

        // A clinic sign on the wall: a paw print on a heart (an animal clinic, not a human hospital).
        sign = Rect("Sign", room);
        if (signSprite != null)
        {
            Img("Picture", sign, signSprite, Color.white, Vector2.zero, new Vector2(190f, 190f)).preserveAspect = true;
        }
        else
        {
            Img("Back", sign, roundSprite, Color.white, Vector2.zero, new Vector2(170f, 170f));
            DrawPaw(sign, Vector2.zero, 90f, new Color(0.95f, 0.45f, 0.52f));
        }

        // The exam table under the pet.
        Color steel = new Color(0.62f, 0.68f, 0.74f);
        legL = Img("TableLegL", room, null, steel, Vector2.zero, new Vector2(18f, 150f)).rectTransform;
        legR = Img("TableLegR", room, null, steel, Vector2.zero, new Vector2(18f, 150f)).rectTransform;
        Image top = Img("Table", room, bodySprite, new Color(0.82f, 0.88f, 0.93f), Vector2.zero, Vector2.zero);
        top.type = Image.Type.Sliced;
        table = top.rectTransform;

        // The vet, beside the table.
        vet = Rect("Vet", room);
        vet.pivot = new Vector2(0.5f, 0f);
        vet.sizeDelta = new Vector2(300f, 540f);
        if (vetSprite != null)
        {
            // Match the picture's shape so its feet stand on the floor line.
            vet.sizeDelta = new Vector2(540f * vetSprite.rect.width / vetSprite.rect.height, 540f);
            Image pic = Img("Picture", vet, vetSprite, Color.white, Vector2.zero, Vector2.zero, true);
            pic.preserveAspect = true;
        }
        else
        {
            DrawVet(vet);
        }

        BuildCard(canvas);
        cardGroup.gameObject.SetActive(false);
        room.gameObject.SetActive(false);
    }

    // Fits the table, vet and sign around the pet (it gets bigger as it grows up).
    void Layout()
    {
        RectTransform petRt = care.petRect;
        Vector2 feet = room.InverseTransformPoint(petRt.position); // the pet's pivot is at its feet
        float petWidth = petRt.rect.width;

        Vector2 tableSize = new Vector2(petWidth * 1.8f, 60f);
        table.sizeDelta = tableSize;
        table.anchoredPosition = feet + new Vector2(0f, -tableSize.y * 0.5f + 8f);
        legL.anchoredPosition = table.anchoredPosition + new Vector2(-tableSize.x * 0.38f, -90f);
        legR.anchoredPosition = table.anchoredPosition + new Vector2(tableSize.x * 0.38f, -90f);

        // Beside the table; a wider vet picture (e.g. with a dog) stands further out.
        vet.anchoredPosition = vetHome = feet + new Vector2(-petWidth * 0.5f - vet.sizeDelta.x * 0.5f - 80f, -20f);
        sign.anchoredPosition = vetHome + new Vector2(0f, vet.sizeDelta.y + 150f);
    }

    // A simple friendly vet from round shapes: white coat, teal scrubs, a stethoscope.
    void DrawVet(RectTransform root)
    {
        Color skin = new Color(0.98f, 0.82f, 0.68f);
        Color hair = new Color(0.36f, 0.23f, 0.15f);
        Color dark = new Color(0.22f, 0.16f, 0.12f);
        Image trim = Img("CoatEdge", root, panelSprite, new Color(0.7f, 0.78f, 0.84f), new Vector2(0f, 160f), new Vector2(262f, 332f));
        trim.type = Image.Type.Sliced;
        Image coat = Img("Coat", root, bodySprite, new Color(0.98f, 0.99f, 1f), new Vector2(0f, 160f), new Vector2(250f, 320f));
        coat.type = Image.Type.Sliced;
        Img("Scrubs", root, null, new Color(0.35f, 0.7f, 0.72f), new Vector2(0f, 280f), new Vector2(64f, 80f));
        Img("Badge", root, roundSprite, new Color(0.35f, 0.7f, 0.72f), new Vector2(70f, 230f), new Vector2(44f, 44f));
        DrawPaw(root, new Vector2(70f, 230f), 30f, Color.white);
        Img("Tube", root, null, new Color(0.45f, 0.48f, 0.52f), new Vector2(-46f, 250f), new Vector2(10f, 90f));
        Img("Chestpiece", root, roundSprite, new Color(0.62f, 0.66f, 0.7f), new Vector2(-46f, 200f), new Vector2(34f, 34f));
        Img("Hair", root, roundSprite, hair, new Vector2(0f, 420f), new Vector2(200f, 190f));
        Img("Head", root, roundSprite, skin, new Vector2(0f, 395f), new Vector2(165f, 165f));
        Img("Fringe", root, roundSprite, hair, new Vector2(-28f, 462f), new Vector2(120f, 56f));
        Img("EyeL", root, roundSprite, dark, new Vector2(-32f, 400f), new Vector2(20f, 20f));
        Img("EyeR", root, roundSprite, dark, new Vector2(32f, 400f), new Vector2(20f, 20f));
        Img("CheekL", root, roundSprite, new Color(1f, 0.55f, 0.6f, 0.45f), new Vector2(-54f, 372f), new Vector2(30f, 22f));
        Img("CheekR", root, roundSprite, new Color(1f, 0.55f, 0.6f, 0.45f), new Vector2(54f, 372f), new Vector2(30f, 22f));
        Img("Smile", root, null, dark, new Vector2(0f, 362f), new Vector2(34f, 6f));
    }

    // A paw print from round shapes: a pad and four toes.
    void DrawPaw(RectTransform parent, Vector2 at, float size, Color color)
    {
        Img("PawPad", parent, roundSprite, color, at + new Vector2(0f, -0.15f * size), new Vector2(0.84f, 0.68f) * size);
        Vector2[] toes = { new Vector2(-0.46f, 0.28f), new Vector2(-0.17f, 0.52f), new Vector2(0.17f, 0.52f), new Vector2(0.46f, 0.28f) };
        foreach (Vector2 toe in toes)
            Img("PawToe", parent, roundSprite, color, at + toe * size, Vector2.one * 0.34f * size);
    }

    void BuildCard(Transform canvas)
    {
        RectTransform card = Rect("VetClinicCard", canvas);
        int effects = UIBurst.Instance != null && UIBurst.Instance.transform.parent == canvas ? UIBurst.Instance.transform.GetSiblingIndex() : canvas.childCount;
        card.SetSiblingIndex(Mathf.Min(effects, canvas.childCount - 1));
        // On the right, where the care buttons usually are, clear of the needs bar and the pet.
        // A compact consultation panel on the right, clear of the needs bar,
        // the vet and the pet (the room zooms toward the space on its left).
        card.anchorMin = card.anchorMax = new Vector2(1f, 0.5f);
        card.pivot = new Vector2(1f, 0.5f);
        card.sizeDelta = new Vector2(600f, 620f);
        card.anchoredPosition = cardHome = new Vector2(-36f, -30f);
        cardRt = card;
        cardGroup = card.gameObject.AddComponent<CanvasGroup>();
        cardGroup.alpha = 0f;

        Image shadow = Img("Shadow", card, shadowSprite, new Color(0.45f, 0.5f, 0.5f, 0.45f), Vector2.zero, Vector2.zero, true);
        shadow.type = Image.Type.Sliced;
        shadow.rectTransform.offsetMin = new Vector2(-12f, -20f);
        shadow.rectTransform.offsetMax = new Vector2(12f, 6f);
        Image fill = Img("Fill", card, bodySprite, cardColor, Vector2.zero, Vector2.zero, true);
        fill.type = Image.Type.Sliced;
        fill.raycastTarget = true;
        // Tapping the card shows the whole line at once.
        Button skip = fill.gameObject.AddComponent<Button>();
        skip.transition = Selectable.Transition.None;
        skip.onClick.AddListener(SkipTyping);

        // Header: soft mint band with the vet's round portrait, name and role.
        Image header = Img("Header", card, bodySprite, headerColor, Vector2.zero, Vector2.zero);
        header.type = Image.Type.Sliced;
        Place(header.rectTransform, new Vector2(14f, -14f), new Vector2(-14f, -138f));

        Sprite circle = circleSprite != null ? circleSprite : roundSprite;
        portraitRing = Img("Portrait", card, circle, accentColor, Vector2.zero, new Vector2(104f, 104f)).rectTransform;
        portraitRing.anchorMin = portraitRing.anchorMax = new Vector2(0f, 1f);
        portraitRing.anchoredPosition = new Vector2(84f, -76f);
        Image face = Img("Face", portraitRing, circle, Color.white, Vector2.zero, new Vector2(92f, 92f));
        face.gameObject.AddComponent<Mask>().showMaskGraphic = true;
        if (vetSprite != null)
        {
            Image pic = Img("Picture", face.rectTransform, vetSprite, Color.white, Vector2.zero, new Vector2(vetSprite.rect.width, vetSprite.rect.height) * portraitScale);
            pic.rectTransform.pivot = portraitFace;
        }
        else
        {
            DrawPaw(face.rectTransform, Vector2.zero, 52f, accentColor);
        }

        TMP_Text title = Text("Name", card, vetName, 36f, textColor);
        title.fontStyle = FontStyles.Bold;
        Place(title.rectTransform, new Vector2(152f, -28f), new Vector2(-110f, -74f));
        // Role tag: paw + "Veterinarian".
        Image tag = Img("Role", card, bodySprite, Color.white, Vector2.zero, new Vector2(228f, 42f));
        tag.type = Image.Type.Sliced;
        tag.pixelsPerUnitMultiplier = 2f;
        tag.rectTransform.anchorMin = tag.rectTransform.anchorMax = new Vector2(0f, 1f);
        tag.rectTransform.pivot = new Vector2(0f, 0.5f);
        tag.rectTransform.anchoredPosition = new Vector2(150f, -100f);
        DrawPaw(tag.rectTransform, new Vector2(-82f, 0f), 24f, accentColor);
        TMP_Text role = Text("Label", tag.rectTransform, vetTitle, 22f, nameColor);
        role.fontStyle = FontStyles.Bold;
        role.alignment = TextAlignmentOptions.Left;
        Stretch(role.rectTransform);
        role.rectTransform.offsetMin = new Vector2(46f, 0f);

        body = Text("Body", card, "", 28f, textColor);
        body.enableAutoSizing = true;
        body.fontSizeMin = 20f;
        body.fontSizeMax = 28f;
        body.lineSpacing = 4f;
        Place(body.rectTransform, new Vector2(36f, -156f), new Vector2(-36f, -330f));

        // Checkup chips (shown only with the checkup result and health record).
        statRow = Rect("Checkup", card);
        statRow.anchorMin = new Vector2(0f, 0f);
        statRow.anchorMax = new Vector2(1f, 0f);
        statRow.pivot = new Vector2(0.5f, 0f);
        statRow.sizeDelta = new Vector2(-40f, 76f);
        statRow.anchoredPosition = new Vector2(0f, 212f);
        var row = statRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        row.spacing = 8f;
        row.childControlWidth = row.childControlHeight = true;
        row.childForceExpandWidth = row.childForceExpandHeight = true;
        for (int i = 0; i < 5; i++)
            chips.Add(MakeChip(statRow));
        statRow.gameObject.SetActive(false);

        // Answers in two columns at the bottom.
        choiceRow = Rect("Choices", card);
        choiceRow.anchorMin = new Vector2(0f, 0f);
        choiceRow.anchorMax = new Vector2(1f, 0f);
        choiceRow.pivot = new Vector2(0.5f, 0f);
        choiceRow.sizeDelta = new Vector2(-44f, 180f);
        choiceRow.anchoredPosition = new Vector2(0f, 22f);
        var grid = choiceRow.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(262f, 80f);
        grid.spacing = new Vector2(16f, 14f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 2;
        grid.childAlignment = TextAnchor.LowerCenter;

        if (closeSprite != null)
        {
            Image x = Img("Close", card, closeSprite, Color.white, Vector2.zero, new Vector2(64f, 64f));
            x.rectTransform.anchorMin = x.rectTransform.anchorMax = new Vector2(1f, 1f);
            x.rectTransform.anchoredPosition = new Vector2(-50f, -50f);
            x.preserveAspect = true;
            x.raycastTarget = true;
            x.gameObject.AddComponent<Button>().onClick.AddListener(Close);
        }
    }

    // One small chip: icon, a short label and a soft bar.
    StatChip MakeChip(Transform parent)
    {
        var chip = new StatChip();
        Image bg = Img("Chip", parent, bodySprite, headerColor, Vector2.zero, Vector2.zero);
        bg.type = Image.Type.Sliced;
        chip.root = bg.rectTransform;
        chip.group = bg.gameObject.AddComponent<CanvasGroup>();

        chip.icon = Img("Icon", chip.root, null, Color.white, Vector2.zero, new Vector2(34f, 34f));
        chip.icon.preserveAspect = true;
        chip.icon.rectTransform.anchorMin = chip.icon.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        chip.icon.rectTransform.anchoredPosition = new Vector2(0f, -22f);

        chip.label = Text("Label", chip.root, "", 17f, textColor);
        chip.label.alignment = TextAlignmentOptions.Center;
        chip.label.enableAutoSizing = true;
        chip.label.fontSizeMin = 12f;
        chip.label.fontSizeMax = 17f;
        chip.label.textWrappingMode = TextWrappingModes.NoWrap;
        Place(chip.label.rectTransform, new Vector2(4f, -40f), new Vector2(-4f, -60f));

        Image track = Img("Track", chip.root, bodySprite, new Color(1f, 1f, 1f, 0.9f), Vector2.zero, Vector2.zero);
        track.type = Image.Type.Sliced;
        track.pixelsPerUnitMultiplier = 8f; // keeps the rounded ends small on a thin bar
        RectTransform tr = track.rectTransform;
        tr.anchorMin = new Vector2(0f, 0f);
        tr.anchorMax = new Vector2(1f, 0f);
        tr.pivot = new Vector2(0.5f, 0f);
        tr.sizeDelta = new Vector2(-18f, 9f);
        tr.anchoredPosition = new Vector2(0f, 7f);
        chip.barFill = Img("Fill", tr, bodySprite, Color.white, Vector2.zero, Vector2.zero, true);
        chip.barFill.type = Image.Type.Sliced;
        chip.barFill.pixelsPerUnitMultiplier = 8f;
        chip.bar = chip.barFill.rectTransform;
        return chip;
    }

    Button MakeChoice()
    {
        Button b;
        if (choiceTemplate != null)
        {
            b = Instantiate(choiceTemplate, choiceRow);
            b.name = "Choice";
            b.onClick = new Button.ButtonClickedEvent(); // drop the template's care action
            Transform icon = b.transform.Find("Icon");
            if (icon != null)
                icon.gameObject.SetActive(false);
            TMP_Text label = b.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                RectTransform lr = label.rectTransform;
                lr.anchorMin = Vector2.zero;
                lr.anchorMax = Vector2.one;
                lr.offsetMin = new Vector2(14f, 6f);
                lr.offsetMax = new Vector2(-14f, -6f);
                label.enableAutoSizing = true;
                label.fontSizeMin = 16f;
                label.fontSizeMax = 26f;
                label.alignment = TextAlignmentOptions.Center;
            }
        }
        else
        {
            Image bg = Img("Choice", choiceRow, bodySprite, new Color(1f, 0.8f, 0.55f), Vector2.zero, Vector2.zero);
            bg.type = Image.Type.Sliced;
            bg.raycastTarget = true;
            b = bg.gameObject.AddComponent<Button>();
            TMP_Text label = Text("Label", bg.rectTransform, "", 24f, textColor);
            label.alignment = TextAlignmentOptions.Center;
            Stretch(label.rectTransform);
        }
        LayoutElement layout = b.GetComponent<LayoutElement>();
        if (layout == null)
            layout = b.gameObject.AddComponent<LayoutElement>();
        layout.preferredWidth = 250f;
        layout.preferredHeight = 88f;
        b.gameObject.SetActive(false);
        return b;
    }

    // ---- Small UI helpers ----

    static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    // A box across the parent's width, between two offsets from its top edge
    // (x: left inset and negative right inset; y: negative distances from the top).
    static void Place(RectTransform rt, Vector2 topLeft, Vector2 bottomRight)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(topLeft.x, bottomRight.y);
        rt.offsetMax = new Vector2(bottomRight.x, topLeft.y);
    }

    static Image Img(string name, Transform parent, Sprite sprite, Color color, Vector2 at, Vector2 size, bool stretch = false)
    {
        RectTransform rt = Rect(name, parent);
        if (stretch)
            Stretch(rt);
        else
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            if (parent is RectTransform p && p.pivot == new Vector2(0.5f, 0f))
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.sizeDelta = size;
            rt.anchoredPosition = at;
        }
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    TMP_Text Text(string name, Transform parent, string text, float size, Color color)
    {
        RectTransform rt = Rect(name, parent);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null)
            t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.raycastTarget = false;
        t.alignment = TextAlignmentOptions.TopLeft;
        return t;
    }
}
