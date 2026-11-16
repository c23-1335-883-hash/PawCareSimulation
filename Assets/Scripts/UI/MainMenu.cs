using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Drives the start scene: fade-in, title/button entrance, the Pet Care Guide
// and Settings panels, and the Play/Exit buttons. Scene loading still goes
// through the existing SceneButton component.
public class MainMenu : MonoBehaviour
{
    [Header("Navigation")]
    public SceneButton sceneButton;

    [Header("Continue")]
    [Tooltip("Shown only when a pet has been adopted before.")]
    public RectTransform continueButton;
    public TMPro.TMP_Text playLabel;
    public Image playFill;
    public Color newPetColor = new Color(0.83f, 0.8f, 0.96f);
    public PetStageLibrary library;
    [Tooltip("Button positions (x) when Continue is shown: guide, continue, play, settings.")]
    public float[] layoutWithContinue = { -610f, -160f, 250f, 630f };

    [Header("Entrance")]
    public Image fadeOverlay;
    public float fadeTime = 0.6f;
    public RectTransform title;
    public CanvasGroup titleGroup;
    public CanvasGroup buttonsGroup;
    public RectTransform[] buttons;

    [Header("Panels")]
    public CanvasGroup guidePanel;
    public CanvasGroup settingsPanel;

    [Header("Settings")]
    public Slider musicSlider;
    public Slider sfxSlider;
    [Tooltip("Label of the Reduce Motion button; shows ON or OFF.")]
    public TMPro.TMP_Text reduceMotionLabel;
    [Tooltip("\"Start over?\" confirmation shown by the Reset Save button.")]
    public CanvasGroup resetPanel;

    bool loading;

    void Start()
    {
        guidePanel.gameObject.SetActive(false);
        settingsPanel.gameObject.SetActive(false);
        if (resetPanel != null)
            resetPanel.gameObject.SetActive(false);

        SetUpContinue();

        musicSlider.SetValueWithoutNotify(SaveSystem.Data.musicVolume);
        sfxSlider.SetValueWithoutNotify(SaveSystem.Data.sfxVolume);
        UpdateReduceMotionLabel();
        ShowVersion();

        StartCoroutine(Entrance());
    }

    // Small "v0.1.0" in the bottom-right corner, read from Player Settings > Version,
    // so every build can be identified. Sits right above the title, below the panels.
    void ShowVersion()
    {
        if (title == null)
            return;
        Canvas canvas = title.GetComponentInParent<Canvas>();
        if (canvas == null)
            return;

        GameObject go = new GameObject("VersionLabel", typeof(RectTransform));
        RectTransform rt = (RectTransform)go.transform;
        rt.SetParent(canvas.rootCanvas.transform, false);
        // Above the menu, but under the panels and fade overlay when they share this parent.
        Transform menuRoot = title;
        while (menuRoot.parent != null && menuRoot.parent != rt.parent)
            menuRoot = menuRoot.parent;
        rt.SetSiblingIndex(menuRoot.GetSiblingIndex() + 1);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 0f);
        rt.anchoredPosition = new Vector2(-28f, 18f);
        rt.sizeDelta = new Vector2(320f, 44f);

        TMPro.TextMeshProUGUI label = go.AddComponent<TMPro.TextMeshProUGUI>();
        if (playLabel != null)
            label.font = playLabel.font;
        label.text = "v" + Application.version;
        label.fontSize = 28f;
        label.fontStyle = TMPro.FontStyles.Bold;
        label.alignment = TMPro.TextAlignmentOptions.BottomRight;
        label.color = new Color(0.36f, 0.22f, 0.12f, 0.9f);
        label.outlineWidth = 0.2f;
        label.outlineColor = new Color32(255, 248, 230, 255);
        label.raycastTarget = false;
    }

    IEnumerator Entrance()
    {
        buttonsGroup.blocksRaycasts = false;
        titleGroup.alpha = 0f;
        foreach (RectTransform b in buttons)
            b.localScale = Vector3.zero;

        if (fadeOverlay != null)
        {
            fadeOverlay.gameObject.SetActive(true);
            fadeOverlay.raycastTarget = false;
        }

        // Screen fades in from black while the title drops into place.
        Vector2 titleEnd = title.anchoredPosition;
        Vector2 titleStart = titleEnd + new Vector2(0f, ReduceMotion.On ? 0f : 120f);
        for (float t = 0f; t < fadeTime; t += Time.unscaledDeltaTime)
        {
            float k = EaseOut(t / fadeTime);
            if (fadeOverlay != null)
                SetAlpha(fadeOverlay, 1f - t / fadeTime);
            title.anchoredPosition = Vector2.LerpUnclamped(titleStart, titleEnd, k);
            titleGroup.alpha = k;
            yield return null;
        }
        title.anchoredPosition = titleEnd;
        titleGroup.alpha = 1f;
        if (fadeOverlay != null)
            fadeOverlay.gameObject.SetActive(false);

        // Buttons pop in one after another.
        for (int i = 0; i < buttons.Length; i++)
        {
            StartCoroutine(PopIn(buttons[i]));
            yield return new WaitForSecondsRealtime(0.08f);
        }
        yield return new WaitForSecondsRealtime(0.25f);
        buttonsGroup.blocksRaycasts = true;
    }

    IEnumerator PopIn(RectTransform target)
    {
        const float time = 0.3f;
        for (float t = 0f; t < time && !ReduceMotion.On; t += Time.unscaledDeltaTime)
        {
            target.localScale = Vector3.one * EaseOutBack(t / time);
            yield return null;
        }
        target.localScale = Vector3.one;
    }

    public void PlayGame()
    {
        if (loading)
            return;
        loading = true;
        StartCoroutine(LoadAfterPress());
    }

    IEnumerator LoadAfterPress()
    {
        // Let the press animation and click sound finish before switching.
        yield return new WaitForSecondsRealtime(0.15f);
        sceneButton.GoSelection();
    }

    // Returns to the pet the player is already raising, keeping its growth stage.
    public void ContinueGame()
    {
        if (loading)
            return;
        PetStageLibrary.Breed breed = SavedPet();
        if (breed == null)
        {
            PlayGame();
            return;
        }

        loading = true;
        ClickImageAnimation.SelectedPetImage = breed.stages[0];
        PetGrowth.StartAsNewPet = false;
        SceneLoader.Load("home");
    }

    PetStageLibrary.Breed SavedPet()
    {
        SaveData save = SaveSystem.Data;
        if (library == null || !save.hasPet)
            return null;
        PetStageLibrary.Breed breed = library.FindById(save.petId);
        return breed != null && breed.stages.Length > 0 && breed.stages[0] != null ? breed : null;
    }

    // With a saved pet: Continue takes the centre spot and Play becomes "New Pet".
    void SetUpContinue()
    {
        if (continueButton == null)
            return;

        bool hasPet = SavedPet() != null;
        continueButton.gameObject.SetActive(hasPet);
        if (!hasPet)
            return;

        if (playLabel != null)
            playLabel.text = "NEW PET";
        if (playFill != null)
            playFill.color = newPetColor;

        // buttons[0] = play, [1] = guide, [2] = settings (see Inspector)
        RectTransform play = buttons[0], guide = buttons[1], settings = buttons[2];
        Place(guide, layoutWithContinue[0], 320f);
        Place(continueButton, layoutWithContinue[1], 420f);
        Place(play, layoutWithContinue[2], 300f);
        Place(settings, layoutWithContinue[3], 300f);
        play.sizeDelta = new Vector2(300f, 110f);
        if (playLabel != null)
            playLabel.fontSize = 36;

        var list = new System.Collections.Generic.List<RectTransform>(buttons);
        list.Insert(0, continueButton);
        buttons = list.ToArray();
    }

    static void Place(RectTransform rt, float x, float width)
    {
        rt.anchoredPosition = new Vector2(x, rt.anchoredPosition.y);
        rt.sizeDelta = new Vector2(width, rt.sizeDelta.y);
    }

    public void OpenGuide() { StartCoroutine(ShowPanel(guidePanel, true)); }
    public void CloseGuide() { StartCoroutine(ShowPanel(guidePanel, false)); }
    public void OpenSettings() { StartCoroutine(ShowPanel(settingsPanel, true)); }
    public void CloseSettings()
    {
        SaveSystem.SaveIfDirty();
        StartCoroutine(ShowPanel(settingsPanel, false));
    }

    public void OnMusicVolumeChanged(float value)
    {
        if (MusicManager.Instance != null)
            MusicManager.Instance.SetMusicVolume(value);
        else
        {
            SaveSystem.Data.musicVolume = value;
            SaveSystem.MarkDirty();
        }
    }

    public void OnSfxVolumeChanged(float value)
    {
        if (MusicManager.Instance != null)
            MusicManager.Instance.SetSfxVolume(value);
        else
        {
            SaveSystem.Data.sfxVolume = value;
            SaveSystem.MarkDirty();
        }
    }

    // Hooked to the Reduce Motion button in Settings. Saved with the game.
    public void ToggleReduceMotion()
    {
        SaveSystem.Data.reduceMotion = !SaveSystem.Data.reduceMotion;
        SaveSystem.Save();
        UpdateReduceMotionLabel();
    }

    // Reset Save: asks first, then removes the pet, coins and items (settings stay).
    public void OpenReset() { StartCoroutine(ShowPanel(resetPanel, true)); }
    public void CloseReset() { StartCoroutine(ShowPanel(resetPanel, false)); }

    public void ConfirmReset()
    {
        if (loading)
            return;
        loading = true;
        SaveSystem.ResetProgress();
        ClickImageAnimation.SelectedPetImage = null;
        SFXManager.Play(Sfx.Pop);
        // Reload the menu so Continue disappears.
        UnityEngine.SceneManagement.SceneManager.LoadScene(gameObject.scene.name);
    }

    void UpdateReduceMotionLabel()
    {
        if (reduceMotionLabel != null)
            reduceMotionLabel.text = "Reduce Motion: " + (ReduceMotion.On ? "ON" : "OFF");
    }

    public void ExitGame()
    {
#if UNITY_EDITOR
        Debug.Log("Exit pressed. Application.Quit() only works in a build.");
#else
        Application.Quit();
#endif
    }

    IEnumerator ShowPanel(CanvasGroup panel, bool show)
    {
        const float time = 0.2f;
        Transform card = panel.transform.childCount > 0 ? panel.transform.GetChild(0) : panel.transform;

        if (show)
            panel.gameObject.SetActive(true);
        panel.blocksRaycasts = false;

        float from = show ? 0f : 1f;
        float to = show ? 1f : 0f;
        for (float t = 0f; t < time; t += Time.unscaledDeltaTime)
        {
            float k = EaseOut(t / time);
            panel.alpha = Mathf.Lerp(from, to, k);
            if (!ReduceMotion.On)
                card.localScale = Vector3.one * Mathf.Lerp(show ? 0.9f : 1f, show ? 1f : 0.9f, k);
            yield return null;
        }
        panel.alpha = to;
        card.localScale = Vector3.one;

        if (show)
            panel.blocksRaycasts = true;
        else
            panel.gameObject.SetActive(false);
    }

    static void SetAlpha(Graphic graphic, float alpha)
    {
        Color c = graphic.color;
        c.a = alpha;
        graphic.color = c;
    }

    static float EaseOut(float t)
    {
        t = Mathf.Clamp01(t);
        return 1f - (1f - t) * (1f - t);
    }

    static float EaseOutBack(float t)
    {
        t = Mathf.Clamp01(t);
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }
}
