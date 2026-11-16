using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// How close the player and the pet are. Bond hearts come from cuddles, care
// the pet needed, its favourite activity, treats and the daily visit. Bond
// only ever goes up. Each new level gives coins and can unlock shop items.
// Bond belongs to the current pet and starts again for a newly adopted one.
public class PetBond : MonoBehaviour
{
    [Header("References")]
    public PetCareManager care;
    public PetController pet;
    [Tooltip("Optional: the bond popup waits while the growth popup is showing.")]
    public CanvasGroup growthPopup;

    [Header("UI")]
    public TMP_Text levelText;
    public Image fill;
    public CanvasGroup popup;
    public TMP_Text popupText;

    [Header("Hearts needed")]
    [Tooltip("Hearts needed to go from level 1 to 2, 2 to 3, and so on. Its length sets the top level.")]
    public int[] heartsToNext = { 30, 45, 60, 75, 90, 110, 130, 150, 175 };
    public string[] levelNames =
    {
        "New Friends", "Getting Comfy", "Pals", "Good Pals", "Buddies",
        "Close Buddies", "Best Friends", "Inseparable", "Soulmates", "Forever Friends"
    };

    [Header("Hearts earned")]
    public int neededCareHearts = 6;
    [Tooltip("Extra on top when it was the pet's favourite activity.")]
    public int favouriteHearts = 4;
    public int cuddleHearts = 2;
    [Tooltip("Cuddles only give hearts this often, so tapping fast doesn't farm them.")]
    public float cuddleHeartsEvery = 20f;
    [Tooltip("Extra for hands-on care the pet needed that was done perfectly (gently and thoroughly).")]
    public int perfectCareHearts = 3;

    [Header("Rewards")]
    [Tooltip("Coins for reaching a new level, times the level.")]
    public int coinsPerLevel = 15;

    public static int Level => Mathf.Max(1, SaveSystem.Data.bondLevel);
    public int MaxLevel => heartsToNext.Length + 1;
    public int Hearts => SaveSystem.Data.bondXP;
    public int HeartsNeeded => Level >= MaxLevel ? 0 : heartsToNext[Mathf.Clamp(Level - 1, 0, heartsToNext.Length - 1)];
    public string LevelName => levelNames.Length > 0 ? levelNames[Mathf.Clamp(Level - 1, 0, levelNames.Length - 1)] : "";

    public event Action<int> LevelUp;

    float lastCuddleHearts = -100f;
    Coroutine popupRoutine;

    void OnEnable()
    {
        if (care != null)
        {
            care.CareFinished += OnCare;
            care.CareRated += OnCareRated;
        }
        if (pet != null)
            pet.Petted += OnPetted;
    }

    void OnDisable()
    {
        if (care != null)
        {
            care.CareFinished -= OnCare;
            care.CareRated -= OnCareRated;
        }
        if (pet != null)
            pet.Petted -= OnPetted;
    }

    void Start()
    {
        if (popup != null)
        {
            popup.alpha = 0f;
            popup.gameObject.SetActive(false);
        }
        SaveSystem.Data.bondLevel = Mathf.Min(Level, MaxLevel);
        Refresh();
    }

    void OnCare(bool needed, bool loved)
    {
        int hearts = (needed ? neededCareHearts : 0) + (loved ? favouriteHearts : 0);
        Add(hearts);
    }

    void OnCareRated(float quality)
    {
        if (quality >= care.perfectAbove)
            Add(perfectCareHearts);
    }

    void OnPetted()
    {
        if (Time.time - lastCuddleHearts < cuddleHeartsEvery)
            return;
        lastCuddleHearts = Time.time;
        Add(cuddleHearts);
    }

    public void Add(int hearts)
    {
        if (hearts <= 0 || Level >= MaxLevel)
            return;

        SaveData save = SaveSystem.Data;
        save.bondXP += hearts;
        int before = Level;
        while (Level < MaxLevel && save.bondXP >= HeartsNeeded)
        {
            save.bondXP -= HeartsNeeded;
            save.bondLevel++;
        }
        if (Level >= MaxLevel)
            save.bondXP = 0;
        SaveSystem.MarkDirty();

        if (Level != before)
        {
            int coins = 0;
            for (int level = before + 1; level <= Level; level++)
                coins += coinsPerLevel * level;
            CoinCounter.Give(coins, pet != null ? pet.PetPoint(0.5f, 0.9f) : transform.position);
            SFXManager.Play(Sfx.Jingle);
            ShowPopup(coins);
            LevelUp?.Invoke(Level);
            SaveSystem.Save();
        }
        Refresh();
    }

    // Hooked to the Bond pill: explains what it is.
    public void ShowInfo()
    {
        if (care == null)
            return;
        string progress = Level >= MaxLevel ? "Your bond is as strong as it gets!" : Hearts + " / " + HeartsNeeded + " hearts to the next level.";
        care.ShowTip("<b>Bond " + Level + " - " + LevelName + "</b>\n<size=80%>" + progress + " Cuddles and caring grow your bond.</size>");
    }

    void Refresh()
    {
        if (levelText != null)
            levelText.text = Level.ToString();
        if (fill != null)
            fill.fillAmount = Level >= MaxLevel ? 1f : (float)Hearts / HeartsNeeded;
    }

    void ShowPopup(int coins)
    {
        if (popup == null || popupText == null)
            return;
        popupText.text = PetProfile.Name + " and you are now\n<size=120%>" + LevelName + "!</size> <size=80%>(Bond " + Level + ", +" + coins + " coins)</size>";
        if (popupRoutine != null)
            StopCoroutine(popupRoutine);
        popupRoutine = StartCoroutine(Popup());
    }

    IEnumerator Popup()
    {
        // Wait for a growth popup to finish first, so the two never overlap.
        while (growthPopup != null && growthPopup.gameObject.activeInHierarchy)
            yield return null;

        popup.gameObject.SetActive(true);
        Transform card = popup.transform;
        for (float t = 0f; t < 0.3f; t += Time.deltaTime)
        {
            float k = t / 0.3f;
            popup.alpha = k;
            card.localScale = ReduceMotion.On ? Vector3.one : Vector3.one * Ease.OutBack(k);
            yield return null;
        }
        popup.alpha = 1f;
        card.localScale = Vector3.one;

        yield return new WaitForSeconds(3f);

        for (float t = 0f; t < 0.4f; t += Time.deltaTime)
        {
            popup.alpha = 1f - t / 0.4f;
            yield return null;
        }
        popup.alpha = 0f;
        popup.gameObject.SetActive(false);
        popupRoutine = null;
    }
}
