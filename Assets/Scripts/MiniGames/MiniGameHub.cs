using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Runs the "minigames" scene, opened by the Play button at home.
// Shows the mini-game menu (one card per MiniGame component on this object,
// plus a Ball Fetch card that goes back home), then for the chosen game:
// countdown -> play (HUD with score / timer / lives) -> result screen with
// best score and coins. Coins go into the existing Wallet; best scores are
// saved with SaveSystem. All UI is built at start from `theme`.
[RequireComponent(typeof(RectTransform))]
public class MiniGameHub : MonoBehaviour
{
    public MiniGameTheme theme = new MiniGameTheme();

    [Tooltip("Scene the Back button on the menu returns to.")]
    public string homeScene = "home";
    [Tooltip("Show the Ball Fetch card (plays the fetch care activity at home).")]
    public bool showFetchCard = true;

    public float Elapsed { get; private set; }
    public Sprite PetSprite { get; private set; }
    public bool PetIsCat { get; private set; }
    public string PetName { get; private set; } = "your pet";

    MiniGame[] games;
    MiniGame current;
    float timeLeft;
    bool leaving;

    RectTransform canvasRect;
    Image background;
    RectTransform menu, play, result;
    RectTransform cardsRow;
    RectTransform stripTrack, stripFill;
    TextMeshProUGUI stripText;
    readonly List<RectTransform> cards = new List<RectTransform>();
    readonly Dictionary<MiniGame, TextMeshProUGUI> bestLabels = new Dictionary<MiniGame, TextMeshProUGUI>();
    TextMeshProUGUI coinText;

    RectTransform area;
    TextMeshProUGUI scoreText, timerText, countdownText, hintText;
    RectTransform timerPill, livesRow;
    readonly List<Image> lifeIcons = new List<Image>();
    Coroutine countdown;

    TextMeshProUGUI resultTitle, resultScore, resultBest, resultCoins, resultCare, resultCareBar;
    RectTransform resultCareTrack, resultCareFill;
    RectTransform resultCard, newBestTag;

    // ---------------------------------------------------------------- setup

    void Awake()
    {
        MiniGameUI.Init(theme);
        canvasRect = (RectTransform)transform;
        ResolvePet();

        games = GetComponents<MiniGame>();
        foreach (MiniGame g in games)
            g.Setup(this);

        background = MiniGameUI.Image(transform, "Background", theme.menuBackground, Color.white);
        background.preserveAspect = false;
        MiniGameUI.Stretch(background.rectTransform);
        background.rectTransform.SetAsFirstSibling();

        BuildMenu();
        BuildPlay();
        BuildResult();

        // Particles and "+5" pop text draw above everything.
        UIBurst burst = GetComponentInChildren<UIBurst>(true);
        if (burst != null)
            burst.transform.SetAsLastSibling();

        Wallet.Changed += UpdateCoins;
        ShowMenu();
    }

    void OnDestroy()
    {
        Wallet.Changed -= UpdateCoins;
    }

    void ResolvePet()
    {
        SaveData save = SaveSystem.Data;
        if (theme.library != null && save.hasPet)
        {
            PetStageLibrary.Breed breed = theme.library.FindById(save.petId);
            if (breed != null && breed.stages.Length > 0)
            {
                int stage = Mathf.Clamp(save.growthStage, 0, breed.stages.Length - 1);
                PetSprite = breed.stages[stage] != null ? breed.stages[stage] : breed.stages[0];
                PetIsCat = breed.isCat;
            }
        }
        if (PetSprite == null && ClickImageAnimation.SelectedPetImage != null)
            PetSprite = ClickImageAnimation.SelectedPetImage;
        if (PetSprite == null && theme.library != null && theme.library.breeds.Length > 0)
            PetSprite = theme.library.breeds[0].stages[0];
        if (!string.IsNullOrEmpty(save.petName))
            PetName = save.petName;
    }

    // A breed different from the player's pet (for Paw Match faces).
    public Sprite OtherPet(bool cat)
    {
        if (theme.library == null)
            return null;
        foreach (PetStageLibrary.Breed b in theme.library.breeds)
        {
            if (b.isCat == cat && b.stages.Length > 0 && b.stages[0] != null && b.stages[0] != PetSprite)
                return b.stages[0];
        }
        return null;
    }

    // ----------------------------------------------------------------- menu

    void BuildMenu()
    {
        menu = MiniGameUI.Stretch(MiniGameUI.Rect("Menu", transform));

        Image tint = MiniGameUI.Image(menu, "Tint", null, new Color(1f, 0.96f, 0.86f, 0.35f));
        MiniGameUI.Stretch(tint.rectTransform);

        Button back = MiniGameUI.ClayButton(menu, "BackButton", "BACK", new Color(1f, 0.82f, 0.86f), null,
            new Vector2(190f, 86f), GoHome);
        MiniGameUI.Place((RectTransform)back.transform, new Vector2(0f, 1f), new Vector2(130f, -64f), new Vector2(190f, 86f));

        coinText = MiniGameUI.Pill(menu, "Coins", theme.coin, new Vector2(1f, 1f), new Vector2(-140f, -64f), 210f);
        UpdateCoins();

        TextMeshProUGUI title = MiniGameUI.Text(menu, "Title", "MINI-GAMES", 76f, theme.ink);
        title.fontStyle = FontStyles.Bold;
        title.outlineWidth = 0.18f;
        title.outlineColor = new Color32(255, 248, 230, 255);
        MiniGameUI.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -64f), new Vector2(900f, 100f));

        BuildPetStrip();

        cardsRow = MiniGameUI.Rect("Cards", menu);
        MiniGameUI.Place(cardsRow, new Vector2(0.5f, 0.5f), new Vector2(0f, -50f), new Vector2(1800f, 640f));

        foreach (MiniGame g in games)
        {
            MiniGame game = g;
            cards.Add(BuildCard(game.Title, game.Description, game.Icon, game.CardColor, "PLAY",
                game.RoundInfo, true, () => StartGame(game), out TextMeshProUGUI best));
            bestLabels[game] = best;
        }
        if (showFetchCard)
        {
            cards.Add(BuildCard("Ball Fetch", "Play catch at home to fill " + PetName + "'s Play need!",
                theme.ball, new Color(1f, 0.82f, 0.86f), "GO", "At home", false, PlayFetchAtHome, out _));
        }

        // Lay the cards out in one row, shrinking them if the screen is narrow.
        const float cardW = 330f, gap = 24f;
        float total = cards.Count * cardW + (cards.Count - 1) * gap;
        for (int i = 0; i < cards.Count; i++)
            cards[i].anchoredPosition = new Vector2(-total * 0.5f + cardW * 0.5f + i * (cardW + gap), 0f);
        float available = canvasRect.rect.width - 60f;
        if (available > 0f && total > available)
            cardsRow.localScale = Vector3.one * (available / total);
    }

    RectTransform BuildCard(string title, string description, Sprite icon, Color color, string buttonLabel,
        string info, bool showBest, System.Action onPlay, out TextMeshProUGUI bestLabel)
    {
        Image card = MiniGameUI.Panel(cardsRow, title + "Card", theme.cream, 6f);
        RectTransform rt = MiniGameUI.Place(card.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(330f, 640f));

        // The whole card is a big touch target, not just the PLAY button.
        Button cardButton = card.gameObject.AddComponent<Button>();
        cardButton.transition = Selectable.Transition.None;
        cardButton.onClick.AddListener(() => onPlay());
        card.gameObject.AddComponent<ButtonJuice>().playHoverSound = false;

        Image header = MiniGameUI.Image(card.transform, "Header", theme.panel, color);
        MiniGameUI.Stretch(header.rectTransform, 6f);
        header.rectTransform.anchorMin = new Vector2(0f, 1f);
        header.rectTransform.offsetMin = new Vector2(6f, -210f);

        Image glow = MiniGameUI.Image(card.transform, "IconGlow", theme.softCircle, new Color(1f, 1f, 1f, 0.55f));
        MiniGameUI.Place(glow.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(180f, 180f));
        Image ic = MiniGameUI.Image(card.transform, "Icon", icon, Color.white);
        MiniGameUI.Place(ic.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(140f, 140f));
        ic.gameObject.AddComponent<UIFloat>().phase = Random.value * 6f;

        // Same-size boxes on every card, so one- and two-line names line up.
        TextMeshProUGUI name = MiniGameUI.Text(card.transform, "Name", title, 38f, theme.ink);
        name.fontStyle = FontStyles.Bold;
        name.enableAutoSizing = true;
        name.fontSizeMin = 28f;
        name.fontSizeMax = 38f;
        name.lineSpacing = -12f;
        MiniGameUI.Place(name.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -262f), new Vector2(300f, 84f));

        TextMeshProUGUI desc = MiniGameUI.Text(card.transform, "Description", description, 24f, theme.ink);
        desc.alpha = 0.8f;
        MiniGameUI.Place(desc.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -346f), new Vector2(280f, 76f));

        // Info chips: round length, and best score ("NEW!" until played).
        RectTransform chips = MiniGameUI.Rect("Chips", card.transform);
        chips.anchorMin = chips.anchorMax = new Vector2(0.5f, 0f);
        chips.anchoredPosition = new Vector2(0f, 168f);
        HorizontalLayoutGroup h = chips.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 8f;
        h.childAlignment = TextAnchor.MiddleCenter;
        h.childControlWidth = h.childControlHeight = true;
        h.childForceExpandWidth = h.childForceExpandHeight = false;
        ContentSizeFitter fit = chips.gameObject.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        Chip(chips, null, info, Color.Lerp(color, Color.white, 0.35f));
        bestLabel = showBest ? Chip(chips, theme.star, "NEW!", new Color(1f, 0.93f, 0.7f)) : null;

        Button button = MiniGameUI.ClayButton(card.transform, "PlayButton", buttonLabel, theme.good, null,
            new Vector2(230f, 88f), onPlay, 36f);
        MiniGameUI.Place((RectTransform)button.transform, new Vector2(0.5f, 0f), new Vector2(0f, 72f), new Vector2(230f, 88f));
        button.GetComponent<ButtonJuice>().idlePulse = false;
        return rt;
    }

    // A small rounded label with an optional icon; returns its text.
    TextMeshProUGUI Chip(Transform parent, Sprite icon, string text, Color fill)
    {
        // The chip sizes to its contents; the rounded background is a stretched child
        // the layout ignores (on the chip itself the sprite would force its own size).
        RectTransform chip = MiniGameUI.Rect("Chip", parent);
        HorizontalLayoutGroup h = chip.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.padding = new RectOffset(12, 12, 7, 7);
        h.spacing = 5f;
        h.childAlignment = TextAnchor.MiddleCenter;
        h.childControlWidth = h.childControlHeight = true;
        h.childForceExpandWidth = h.childForceExpandHeight = false;

        Image bg = MiniGameUI.Image(chip, "Background", theme.panel, fill);
        bg.pixelsPerUnitMultiplier = 2.5f; // smaller rounded corners on a small pill
        MiniGameUI.Stretch(bg.rectTransform);
        bg.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

        if (icon != null)
        {
            Image ic = MiniGameUI.Image(chip, "Icon", icon, Color.white);
            LayoutElement le = ic.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = le.preferredHeight = 28f;
        }
        TextMeshProUGUI t = MiniGameUI.Text(chip, "Text", text, 22f, theme.ink);
        t.fontStyle = FontStyles.Bold;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        return t;
    }

    // Pet strip under the title: the pet, its name and its Care Points toward the next evolution.
    void BuildPetStrip()
    {
        if (!SaveSystem.Data.hasPet)
            return;
        Image strip = MiniGameUI.Panel(menu, "PetStrip", theme.cream, 5f);
        RectTransform rt = MiniGameUI.Place(strip.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(760f, 92f));
        strip.raycastTarget = false;

        Image ring = MiniGameUI.Image(rt, "AvatarBg", theme.softCircle, new Color(1f, 0.85f, 0.6f));
        MiniGameUI.Place(ring.rectTransform, new Vector2(0f, 0.5f), new Vector2(56f, 0f), new Vector2(78f, 78f));
        Image avatar = MiniGameUI.Image(ring.transform, "Pet", PetSprite, Color.white);
        MiniGameUI.Stretch(avatar.rectTransform, 4f);

        TextMeshProUGUI name = MiniGameUI.Text(rt, "Name", PetName, 32f, theme.ink, TextAlignmentOptions.Left);
        name.fontStyle = FontStyles.Bold;
        name.textWrappingMode = TextWrappingModes.NoWrap;
        name.enableAutoSizing = true;
        name.fontSizeMin = 22f;
        name.fontSizeMax = 32f;
        MiniGameUI.Place(name.rectTransform, new Vector2(0f, 0.5f), new Vector2(185f, 0f), new Vector2(150f, 50f));

        Image track = MiniGameUI.Image(rt, "CareTrack", theme.panel, new Color(theme.ink.r, theme.ink.g, theme.ink.b, 0.15f));
        MiniGameUI.Place(track.rectTransform, new Vector2(1f, 0.5f), new Vector2(-220f, 12f), new Vector2(400f, 30f));
        Image fill = MiniGameUI.Image(track.transform, "Fill", theme.panel, theme.good);
        stripFill = fill.rectTransform;
        stripFill.anchorMin = Vector2.zero;
        stripFill.anchorMax = new Vector2(0f, 1f);
        stripFill.offsetMin = new Vector2(4f, 4f);
        stripTrack = track.rectTransform;
        stripText = MiniGameUI.Text(track.transform, "Label", "", 19f, theme.ink);
        stripText.fontStyle = FontStyles.Bold;
        stripText.textWrappingMode = TextWrappingModes.NoWrap;
        MiniGameUI.Stretch(stripText.rectTransform);

        TextMeshProUGUI hint = MiniGameUI.Text(rt, "Hint", "Finish a round: +" + CarePoints.Play + " Care Points", 19f, theme.ink);
        hint.alpha = 0.75f;
        hint.textWrappingMode = TextWrappingModes.NoWrap;
        MiniGameUI.Place(hint.rectTransform, new Vector2(1f, 0.5f), new Vector2(-220f, -22f), new Vector2(400f, 28f));
    }

    void UpdatePetStrip()
    {
        if (stripTrack == null)
            return;
        int total = CarePoints.Total, goal = CarePoints.GoalFor(total);
        int shown = Mathf.Min(total, goal);
        float k = goal > 0 ? Mathf.Clamp01((float)shown / goal) : 1f;
        stripFill.offsetMax = new Vector2(4f + (stripTrack.rect.width - 8f) * k, -4f);
        stripFill.gameObject.SetActive(k > 0.001f);
        int goalStage = System.Array.IndexOf(CarePoints.StageStarts, goal);
        bool grown = goalStage == CarePoints.MaxStage && shown >= goal;
        stripText.text = "Care Points " + shown + " / " + goal + (grown ? "  - fully grown!" : "  to " + CarePoints.StageNames[goalStage]);
    }

    void ShowMenu()
    {
        current = null;
        background.sprite = theme.menuBackground;
        menu.gameObject.SetActive(true);
        play.gameObject.SetActive(false);
        result.gameObject.SetActive(false);
        foreach (KeyValuePair<MiniGame, TextMeshProUGUI> pair in bestLabels)
        {
            int best = MiniGameScores.Best(pair.Key.Id);
            pair.Value.text = best > 0 ? "Best " + best : "NEW!";
        }
        UpdateCoins();
        UpdatePetStrip();
        StartCoroutine(CardsPopIn());
    }

    IEnumerator CardsPopIn()
    {
        foreach (RectTransform c in cards)
            c.localScale = ReduceMotion.On ? Vector3.one : Vector3.zero;
        if (ReduceMotion.On)
            yield break;
        for (int i = 0; i < cards.Count; i++)
        {
            StartCoroutine(Pop(cards[i], 0.35f));
            yield return new WaitForSecondsRealtime(0.06f);
        }
    }

    void UpdateCoins()
    {
        if (coinText != null)
            coinText.text = Wallet.Coins.ToString();
    }

    void GoHome()
    {
        if (leaving)
            return;
        leaving = true;
        SaveSystem.SaveIfDirty();
        SceneLoader.Load(SaveSystem.Data.hasPet ? homeScene : "start");
    }

    void PlayFetchAtHome()
    {
        if (leaving)
            return;
        PetCareManager.PlayFetchOnStart = true;
        GoHome();
    }

    // ----------------------------------------------------------------- play

    void BuildPlay()
    {
        play = MiniGameUI.Stretch(MiniGameUI.Rect("Play", transform));
        // Soften the busy room picture so falling treats, fleas and "+10" text stand out.
        Image tint = MiniGameUI.Image(play, "Tint", null, new Color(1f, 0.97f, 0.9f, 0.45f));
        MiniGameUI.Stretch(tint.rectTransform);
        area = MiniGameUI.Stretch(MiniGameUI.Rect("PlayArea", play));

        RectTransform hud = MiniGameUI.Stretch(MiniGameUI.Rect("HUD", play));

        Button quit = MiniGameUI.ClayButton(hud, "QuitButton", theme.closeIcon == null ? "X" : null,
            new Color(1f, 0.82f, 0.86f), theme.closeIcon, new Vector2(96f, 96f), QuitToMenu, 40f);
        MiniGameUI.Place((RectTransform)quit.transform, new Vector2(0f, 1f), new Vector2(76f, -66f), new Vector2(96f, 96f));

        scoreText = MiniGameUI.Pill(hud, "Score", theme.star, new Vector2(0f, 1f), new Vector2(290f, -66f), 230f);
        timerText = MiniGameUI.Pill(hud, "Timer", null, new Vector2(0.5f, 1f), new Vector2(0f, -66f), 170f);
        timerText.alignment = TextAlignmentOptions.Center;
        timerText.rectTransform.offsetMin = new Vector2(16f, 0f);
        timerPill = (RectTransform)timerText.transform.parent;

        livesRow = MiniGameUI.Rect("Lives", hud);
        MiniGameUI.Place(livesRow, new Vector2(1f, 1f), new Vector2(-150f, -66f), new Vector2(240f, 70f));

        hintText = MiniGameUI.Text(hud, "Hint", "", 34f, theme.ink);
        hintText.fontStyle = FontStyles.Bold;
        hintText.outlineWidth = 0.2f;
        hintText.outlineColor = new Color32(255, 248, 230, 255);
        MiniGameUI.Place(hintText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -140f), new Vector2(1400f, 120f));

        countdownText = MiniGameUI.Text(hud, "Countdown", "", 200f, theme.ink);
        countdownText.fontStyle = FontStyles.Bold;
        countdownText.outlineWidth = 0.2f;
        countdownText.outlineColor = new Color32(255, 248, 230, 255);
        MiniGameUI.Place(countdownText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(800f, 260f));
    }

    public void StartGame(MiniGame game)
    {
        if (game == null || leaving)
            return;
        if (current != null)
            current.Stop();
        ClearArea();

        current = game;
        menu.gameObject.SetActive(false);
        result.gameObject.SetActive(false);
        play.gameObject.SetActive(true);
        background.sprite = theme.gameBackground;

        Elapsed = 0f;
        timeLeft = game.TimeLimit;
        SetLives(game.StartLives);
        ShowScore(0, false);
        UpdateTimer();

        game.Prepare(area);
        if (countdown != null)
            StopCoroutine(countdown);
        countdown = StartCoroutine(Countdown(game));
    }

    IEnumerator Countdown(MiniGame game)
    {
        hintText.text = game.HowToPlay;
        hintText.gameObject.SetActive(true);
        string[] steps = { "3", "2", "1", "GO!" };
        foreach (string s in steps)
        {
            countdownText.text = s;
            SFXManager.Play(s == "GO!" ? Sfx.HappyChirp : Sfx.Pop);
            StartCoroutine(Pop(countdownText.rectTransform, 0.25f));
            yield return new WaitForSeconds(s == "GO!" ? 0.5f : 0.6f);
        }
        countdownText.text = "";
        hintText.gameObject.SetActive(false);
        countdown = null;
        if (current == game)
            game.Begin();
    }

    void Update()
    {
        if (current != null && current.Running)
        {
            Elapsed += Time.deltaTime;
            if (current.TimeLimit > 0f)
            {
                timeLeft -= Time.deltaTime;
                if (timeLeft <= 0f)
                {
                    timeLeft = 0f;
                    UpdateTimer();
                    FinishGame();
                    return;
                }
            }
            UpdateTimer();
        }

#if ENABLE_INPUT_SYSTEM
        // Escape on a keyboard and the Android back button.
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            OnBack();
#endif
    }

    void OnBack()
    {
        if (result.gameObject.activeSelf)
            BackToMenu();
        else if (play.gameObject.activeSelf)
            QuitToMenu();
        else
            GoHome();
    }

    void UpdateTimer()
    {
        if (current == null)
            return;
        float t = current.TimeLimit > 0f ? Mathf.Ceil(timeLeft) : Mathf.Floor(Elapsed);
        int seconds = Mathf.Max(0, (int)t);
        timerText.text = (seconds / 60) + ":" + (seconds % 60).ToString("00");
        bool hurry = current.TimeLimit > 0f && timeLeft <= 5f && current.Running;
        timerText.color = hurry ? theme.bad : theme.ink;
        timerPill.localScale = hurry && !ReduceMotion.On ? Vector3.one * (1f + Mathf.Abs(Mathf.Sin(Time.time * 6f)) * 0.08f) : Vector3.one;
    }

    public void ShowScore(int score, bool bump)
    {
        scoreText.text = score.ToString();
        if (bump && !ReduceMotion.On)
            StartCoroutine(Pop((RectTransform)scoreText.transform.parent, 0.2f, 0.85f));
    }

    // Shows `count` full hearts out of the game's StartLives (hidden when the game has no lives).
    public void SetLives(int count)
    {
        int max = current != null ? Mathf.Max(current.StartLives, count) : count;
        while (lifeIcons.Count < max)
        {
            Image h = MiniGameUI.Image(livesRow, "Heart", theme.heart, Color.white);
            lifeIcons.Add(h);
        }
        for (int i = 0; i < lifeIcons.Count; i++)
        {
            lifeIcons[i].gameObject.SetActive(i < max);
            lifeIcons[i].color = i < count ? Color.white : new Color(0.3f, 0.3f, 0.3f, 0.35f);
            MiniGameUI.Place(lifeIcons[i].rectTransform, new Vector2(1f, 0.5f), new Vector2(-36f - i * 74f, 0f), new Vector2(64f, 64f));
        }
        livesRow.gameObject.SetActive(max > 0);
    }

    // Called when time runs out or the game ends itself (all pairs found, no lives left).
    public void FinishGame()
    {
        if (current == null || !current.Running)
            return;
        MiniGame game = current;
        game.Stop();

        int score = game.Score;
        int coins = game.CoinsFor(score);
        bool newBest = MiniGameScores.Submit(game.Id, score);
        Wallet.Add(coins);
        // Playing with the pet counts as care: one award per finished round that scored.
        // Quitting a round never reaches here, so it can't be farmed.
        int careBefore = CarePoints.Total;
        int care = score > 0 && CarePoints.Add(CarePoints.Play, "Mini-game: " + game.Title) ? CarePoints.Play : 0;
        SaveSystem.Save();

        StartCoroutine(ShowResult(game, score, MiniGameScores.Best(game.Id), coins, care, careBefore, newBest));
    }

    void QuitToMenu()
    {
        if (countdown != null)
        {
            StopCoroutine(countdown);
            countdown = null;
        }
        if (current != null)
            current.Stop();
        ClearArea();
        countdownText.text = "";
        ShowMenu();
    }

    void ClearArea()
    {
        for (int i = area.childCount - 1; i >= 0; i--)
            Destroy(area.GetChild(i).gameObject);
    }

    // --------------------------------------------------------------- result

    void BuildResult()
    {
        result = MiniGameUI.Stretch(MiniGameUI.Rect("Result", transform));
        Image dim = MiniGameUI.Image(result, "Dim", null, new Color(0.2f, 0.12f, 0.06f, 0.55f), true);
        MiniGameUI.Stretch(dim.rectTransform);

        Image card = MiniGameUI.Panel(result, "Card", theme.cream, 7f);
        resultCard = MiniGameUI.Place(card.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(720f, 680f));

        Image header = MiniGameUI.Image(card.transform, "Header", theme.panel, new Color(1f, 0.85f, 0.6f));
        MiniGameUI.Stretch(header.rectTransform, 7f);
        header.rectTransform.anchorMin = new Vector2(0f, 1f);
        header.rectTransform.offsetMin = new Vector2(7f, -130f);

        // One line that shrinks to fit, so long titles ("ALL PAIRS FOUND!") stay inside the header.
        resultTitle = MiniGameUI.Text(card.transform, "Title", "GAME OVER", 68f, theme.ink);
        resultTitle.fontStyle = FontStyles.Bold;
        resultTitle.textWrappingMode = TextWrappingModes.NoWrap;
        resultTitle.enableAutoSizing = true;
        resultTitle.fontSizeMin = 36f;
        resultTitle.fontSizeMax = 68f;
        MiniGameUI.Place(resultTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -68f), new Vector2(600f, 100f));

        resultScore = MiniGameUI.Text(card.transform, "Score", "Score: 0", 58f, theme.ink);
        resultScore.fontStyle = FontStyles.Bold;
        MiniGameUI.Place(resultScore.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -195f), new Vector2(660f, 76f));

        resultBest = MiniGameUI.Text(card.transform, "Best", "Best Score: 0", 34f, theme.ink);
        resultBest.alpha = 0.85f;
        MiniGameUI.Place(resultBest.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -254f), new Vector2(660f, 50f));

        // Sits on the header's bottom-right corner, clear of the title.
        Image tag = MiniGameUI.Panel(card.transform, "NewBest", new Color(1f, 0.88f, 0.4f), 4f);
        newBestTag = MiniGameUI.Place(tag.rectTransform, new Vector2(1f, 1f), new Vector2(-96f, -138f), new Vector2(190f, 58f));
        newBestTag.localRotation = Quaternion.Euler(0f, 0f, -10f);
        TextMeshProUGUI tagText = MiniGameUI.Text(tag.transform, "Text", "NEW BEST!", 28f, theme.ink);
        tagText.fontStyle = FontStyles.Bold;
        tagText.textWrappingMode = TextWrappingModes.NoWrap;
        MiniGameUI.Stretch(tagText.rectTransform);

        Image divider = MiniGameUI.Image(card.transform, "Divider", theme.panel, new Color(theme.ink.r, theme.ink.g, theme.ink.b, 0.15f));
        MiniGameUI.Place(divider.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -296f), new Vector2(520f, 5f));

        // Rewards: icon + text rows that stay centred whatever the numbers are.
        resultCoins = IconRow(card.transform, "Coins", theme.coin, 58f, 42f, true, -350f);
        resultCare = IconRow(card.transform, "Care", theme.heart, 42f, 32f, true, -410f);

        // Care Points bar: fills from the old total to the new one.
        Image track = MiniGameUI.Image(card.transform, "CareTrack", theme.panel, new Color(theme.ink.r, theme.ink.g, theme.ink.b, 0.15f));
        MiniGameUI.Place(track.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -462f), new Vector2(480f, 34f));
        Image fill = MiniGameUI.Image(track.transform, "Fill", theme.panel, theme.good);
        resultCareFill = fill.rectTransform;
        resultCareFill.anchorMin = Vector2.zero;
        resultCareFill.anchorMax = new Vector2(0f, 1f);
        resultCareFill.offsetMin = new Vector2(4f, 4f);
        resultCareFill.offsetMax = new Vector2(0f, -4f);
        resultCareBar = MiniGameUI.Text(track.transform, "Label", "", 21f, theme.ink);
        resultCareBar.fontStyle = FontStyles.Bold;
        resultCareBar.textWrappingMode = TextWrappingModes.NoWrap;
        MiniGameUI.Stretch(resultCareBar.rectTransform);
        resultCareTrack = track.rectTransform;

        Button again = MiniGameUI.ClayButton(card.transform, "PlayAgainButton", "PLAY AGAIN", theme.good, null,
            new Vector2(300f, 100f), () => StartGame(current), 34f);
        MiniGameUI.Place((RectTransform)again.transform, new Vector2(0.5f, 0f), new Vector2(-160f, 90f), new Vector2(300f, 100f));

        Button back = MiniGameUI.ClayButton(card.transform, "MiniGamesButton", "MINI-GAMES", new Color(0.83f, 0.8f, 0.96f), null,
            new Vector2(300f, 100f), BackToMenu, 34f);
        MiniGameUI.Place((RectTransform)back.transform, new Vector2(0.5f, 0f), new Vector2(160f, 90f), new Vector2(300f, 100f));
    }

    // A centred row: [icon] text. The row resizes to its contents, so the pair stays centred.
    TextMeshProUGUI IconRow(Transform parent, string name, Sprite icon, float iconSize, float fontSize, bool bold, float y)
    {
        RectTransform row = MiniGameUI.Rect(name + "Row", parent);
        row.anchorMin = row.anchorMax = new Vector2(0.5f, 1f);
        row.pivot = new Vector2(0.5f, 0.5f);
        row.anchoredPosition = new Vector2(0f, y);
        HorizontalLayoutGroup h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 14f;
        h.childAlignment = TextAnchor.MiddleCenter;
        h.childControlWidth = h.childControlHeight = true;
        h.childForceExpandWidth = h.childForceExpandHeight = false;
        ContentSizeFitter fit = row.gameObject.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        Image img = MiniGameUI.Image(row, "Icon", icon, Color.white);
        LayoutElement le = img.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = le.preferredHeight = iconSize;

        TextMeshProUGUI label = MiniGameUI.Text(row, "Text", "", fontSize, theme.ink, TextAlignmentOptions.Left);
        label.textWrappingMode = TextWrappingModes.NoWrap;
        if (bold)
            label.fontStyle = FontStyles.Bold;
        return label;
    }

    // Bar toward `goal` (the next evolution's total, or the last one once fully grown).
    void SetCareBar(float points, int goal)
    {
        float k = goal > 0 ? Mathf.Clamp01(points / goal) : 1f;
        float width = resultCareTrack.rect.width - 8f;
        resultCareFill.offsetMax = new Vector2(4f + width * k, -4f);
        resultCareFill.gameObject.SetActive(k > 0.001f);

        int shown = Mathf.Min(Mathf.RoundToInt(points), goal);
        int goalStage = System.Array.IndexOf(CarePoints.StageStarts, goal);
        bool grown = goalStage == CarePoints.MaxStage && shown >= goal;
        string next = grown ? "- fully grown!" : goalStage > 0 ? "to " + CarePoints.StageNames[goalStage] : "";
        resultCareBar.text = shown + " / " + goal + "  " + next;
    }

    IEnumerator ShowResult(MiniGame game, int score, int best, int coins, int care, int careBefore, bool newBest)
    {
        resultTitle.text = game.ResultTitle(game.TimeLimit > 0f && timeLeft <= 0f);
        resultBest.text = "Best Score: " + best;
        resultCoins.text = "Coins Earned: +" + coins;
        resultCare.text = care > 0 ? "Care Points: +" + care : "Score points to earn Care Points!";
        // The bar works toward the pet's next evolution (e.g. 25 -> 35 / 200 to Young;
        // a round that crosses 200 shows 195 -> 205 / 500 to Adult).
        int careAfter = CarePoints.Total;
        int goal = CarePoints.GoalFor(careAfter);
        bool hasPet = SaveSystem.Data.hasPet;
        resultCareTrack.gameObject.SetActive(hasPet);
        if (hasPet)
            SetCareBar(Mathf.Min(careBefore, goal), goal);
        resultScore.text = "Score: 0";
        newBestTag.gameObject.SetActive(false);

        yield return new WaitForSeconds(0.6f);
        result.gameObject.SetActive(true);
        result.SetAsLastSibling();
        UIBurst burst = UIBurst.Instance;
        if (burst != null)
            burst.transform.SetAsLastSibling();
        StartCoroutine(Pop(resultCard, 0.35f));
        SFXManager.Play(PetIsCat ? Sfx.Meow : Sfx.Bark);

        // Count the score up.
        const float countTime = 0.7f;
        for (float t = 0f; t < countTime && !ReduceMotion.On; t += Time.unscaledDeltaTime)
        {
            resultScore.text = "Score: " + Mathf.RoundToInt(score * t / countTime);
            yield return null;
        }
        resultScore.text = "Score: " + score;

        if (coins > 0)
        {
            SFXManager.Play(Sfx.Coin);
            if (burst != null)
                burst.Burst(resultCoins.rectTransform.position, theme.coin, 8, UIBurst.Rise, Color.white);
        }

        // Fill the Care Points bar by the points just earned.
        if (hasPet && care > 0)
        {
            const float fillTime = 0.6f;
            float from = Mathf.Min(careBefore, goal), to = Mathf.Min(careAfter, goal);
            for (float t = 0f; t < fillTime && !ReduceMotion.On; t += Time.unscaledDeltaTime)
            {
                float k = t / fillTime;
                SetCareBar(Mathf.Lerp(from, to, 1f - (1f - k) * (1f - k)), goal);
                yield return null;
            }
            SetCareBar(to, goal);
            if (burst != null)
                burst.Burst(resultCareFill.position, theme.heart, 4, UIBurst.Rise, Color.white);
        }
        if (newBest)
        {
            newBestTag.gameObject.SetActive(true);
            StartCoroutine(Pop(newBestTag, 0.3f));
            SFXManager.Play(Sfx.Jingle);
            if (burst != null)
                burst.Burst(resultCard.position + Vector3.up * 200f, burst.confetti, 30, UIBurst.Confetti,
                    new Color(1f, 0.8f, 0.3f), new Color(0.55f, 0.82f, 0.4f), new Color(0.93f, 0.5f, 0.6f), new Color(0.5f, 0.75f, 1f));
        }
    }

    void BackToMenu()
    {
        ClearArea();
        ShowMenu();
    }

    // ---------------------------------------------------------------- tween

    static IEnumerator Pop(RectTransform target, float time, float from = 0f)
    {
        if (ReduceMotion.On)
        {
            target.localScale = Vector3.one;
            yield break;
        }
        for (float t = 0f; t < time; t += Time.unscaledDeltaTime)
        {
            float k = Mathf.Clamp01(t / time);
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float e = 1f + c3 * Mathf.Pow(k - 1f, 3f) + c1 * Mathf.Pow(k - 1f, 2f);
            target.localScale = Vector3.one * Mathf.LerpUnclamped(from, 1f, e);
            yield return null;
        }
        target.localScale = Vector3.one;
    }
}
