using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The growth panel on the home screen and the "grew into" popup. It only
// shows what PetGrowth reports (through its Changed and Evolved events);
// it never changes growth points or stages.
public class GrowthPanelUI : MonoBehaviour
{
    public PetGrowth growth;

    [Header("Panel")]
    public TMP_Text titleText;
    public Image progressFill;
    public TMP_Text progressText;
    public TMP_Text hintText;

    [Header("Popup")]
    public CanvasGroup evolvePopup;
    public TMP_Text evolveText;
    [Tooltip("Care Points bar inside the popup (optional). Fills up when the popup appears.")]
    public Image popupProgressFill;
    public TMP_Text popupProgressText;
    public float popupBarFillTime = 0.8f;

    [Header("Text")]
    [Tooltip("Added after the evolution name once the pet is an Adult.")]
    public string grownHint = "All grown up!";
    public string noGrowthTitle = "My Pet";

    Coroutine popupRoutine;
    int popupPoints, popupGoal;
    bool popupGrown;

    void Awake()
    {
        if (evolvePopup != null)
        {
            evolvePopup.alpha = 0f;
            evolvePopup.gameObject.SetActive(false);
        }
    }

    void OnEnable()
    {
        if (growth == null)
            return;
        growth.Changed += Refresh;
        growth.Evolved += ShowEvolved;
    }

    void OnDisable()
    {
        if (growth == null)
            return;
        growth.Changed -= Refresh;
        growth.Evolved -= ShowEvolved;
    }

    void Refresh()
    {
        if (!growth.HasBreed)
        {
            if (titleText != null)
                titleText.text = noGrowthTitle;
            return;
        }

        // Title: the pet's name. Hint: what it is, and its personality.
        string kind = growth.StageTitle;
        bool named = PetProfile.HasName && PetProfile.Name != growth.Breed.displayName;
        if (titleText != null)
            titleText.text = named ? PetProfile.Name : kind;

        // Care Points never reset: 120 / 200 as a Baby, 200 / 500 after becoming Young, 500 / 500 as an Adult.
        bool grown = growth.IsFullyGrown;
        if (progressFill != null)
            progressFill.fillAmount = Mathf.Clamp01((float)growth.Points / growth.Goal);
        if (progressText != null)
            progressText.text = "Care Points " + growth.Points + " / " + growth.Goal;
        if (hintText != null)
        {
            // Named pet: "Young Ragdoll (Evolution 2/3)". Unnamed (the title already shows the kind):
            // "Evolution 2/3: Young". Kept to one line so the personality line still fits.
            string count = "Evolution " + (growth.Stage + 1) + "/" + growth.StageCount;
            string evolution = named ? kind + " (" + count + ")"
                : count + ": " + (grown ? grownHint : growth.StageName);
            string personality = "<b>" + PetProfile.TraitName + "</b> - " + PetProfile.TraitBlurb;
            hintText.text = evolution + "\n" + personality;
        }
    }

    void ShowEvolved(int fromStage, int toStage)
    {
        if (evolveText != null)
        {
            string title = growth.StageTitle;
            string article = title.Length > 0 && "AEIOUaeiou".IndexOf(title[0]) >= 0 ? "an" : "a";
            // The Care Points progress goes in the bar below when there is one,
            // otherwise on this line, e.g. "Care Points 205 / 500" (it never resets).
            string progress = popupProgressFill != null ? "" : "  -  " + ProgressLabel(growth.Points, growth.Goal, growth.IsFullyGrown);
            evolveText.text = PetProfile.Name + " evolved into " + article + "\n<size=120%>" + title + "</size>!\n<size=70%>Evolution "
                + (toStage + 1) + "/" + growth.StageCount + ": " + growth.StageName + progress + "</size>";
        }
        popupPoints = growth.Points;
        popupGoal = growth.Goal;
        popupGrown = growth.IsFullyGrown;
        SetPopupBar(0f);
        if (evolvePopup == null)
            return;
        if (popupRoutine != null)
            StopCoroutine(popupRoutine);
        popupRoutine = StartCoroutine(ShowPopup());
    }

    IEnumerator ShowPopup()
    {
        evolvePopup.gameObject.SetActive(true);
        Transform card = evolvePopup.transform;
        for (float t = 0f; t < 0.3f; t += Time.deltaTime)
        {
            float k = t / 0.3f;
            evolvePopup.alpha = k;
            card.localScale = Vector3.one * Mathf.Lerp(0.8f, 1f, k);
            yield return null;
        }
        evolvePopup.alpha = 1f;
        card.localScale = Vector3.one;

        // Fill the Care Points bar and count the points up.
        float fillTime = ReduceMotion.On ? 0f : popupBarFillTime;
        for (float t = 0f; t < fillTime; t += Time.deltaTime)
        {
            float k = t / fillTime;
            SetPopupBar(1f - (1f - k) * (1f - k));
            yield return null;
        }
        SetPopupBar(1f);

        yield return new WaitForSeconds(Mathf.Max(1.5f, 3f - fillTime));

        for (float t = 0f; t < 0.4f; t += Time.deltaTime)
        {
            evolvePopup.alpha = 1f - t / 0.4f;
            yield return null;
        }
        evolvePopup.alpha = 0f;
        evolvePopup.gameObject.SetActive(false);
        popupRoutine = null;
    }

    // k = 0..1 of the way to the pet's current Care Points.
    void SetPopupBar(float k)
    {
        if (popupProgressFill != null)
            popupProgressFill.fillAmount = popupGoal > 0 ? Mathf.Clamp01(k * popupPoints / popupGoal) : 1f;
        if (popupProgressText != null)
            popupProgressText.text = ProgressLabel(Mathf.RoundToInt(k * popupPoints), popupGoal, popupGrown && k >= 1f);
    }

    static string ProgressLabel(int points, int goal, bool grown)
    {
        return "Care Points " + points + " / " + goal + (grown ? " - fully grown!" : "");
    }
}
