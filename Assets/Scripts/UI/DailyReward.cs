using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// A 7-day gift calendar shown the first time home opens each day. Days 1-6
// give coins; day 7 gives more coins and a gift item (the crown) the first
// time. Missing a day only pauses the streak: the next visit carries on.
public class DailyReward : MonoBehaviour
{
    [Serializable]
    public class DaySlot
    {
        public Image fill;
        public TMP_Text dayText;
        public TMP_Text amountText;
        public Image icon;
        public Image tick;
    }

    [Header("References")]
    public ShopUI shop;
    public PetBond bond;
    public PetController pet;

    [Header("Panel")]
    public CanvasGroup panel;
    public RectTransform card;
    public DaySlot[] days = new DaySlot[7];
    public Button claimButton;
    public TMP_Text claimText;

    [Header("Rewards")]
    public int[] coins = { 10, 15, 20, 25, 30, 40, 60 };
    [Tooltip("Shop item given on day 7 the first time. Afterwards day 7 gives the extra coins instead.")]
    public string giftItemId = "acc_crown";
    public int giftReplacementCoins = 60;
    public int bondHearts = 5;
    [Tooltip("Seconds after home opens before the calendar appears.")]
    public float showDelay = 1.2f;

    [Header("Colours")]
    public Color claimedColor = new Color(0.86f, 0.84f, 0.82f);
    public Color todayColor = new Color(1f, 0.82f, 0.5f);
    public Color laterColor = new Color(1f, 0.95f, 0.86f);

    static string Today => DateTime.Now.ToString("yyyy-MM-dd");
    public static bool ClaimedToday => SaveSystem.Data.dailyLastClaim == Today;

    bool claiming;
    float pulse;

    void Awake()
    {
        if (panel != null)
        {
            panel.alpha = 0f;
            panel.blocksRaycasts = false;
            panel.gameObject.SetActive(false);
        }
        if (claimButton != null)
            claimButton.onClick.AddListener(Claim);
    }

    void Start()
    {
        if (SaveSystem.Data.hasPet && !ClaimedToday)
            StartCoroutine(ShowLater());
    }

    IEnumerator ShowLater()
    {
        yield return new WaitForSeconds(showDelay);
        Show();
    }

    public void Show()
    {
        claiming = false;
        Refresh();
        panel.gameObject.SetActive(true);
        SFXManager.Play(Sfx.Jingle, 0.6f);
        StartCoroutine(Fade(true));
    }

    void Update()
    {
        if (panel == null || !panel.gameObject.activeSelf || claiming || ReduceMotion.On)
            return;
        // Today's slot gently pulses.
        pulse += Time.unscaledDeltaTime;
        DaySlot today = days[Mathf.Clamp(SaveSystem.Data.dailyNext, 0, days.Length - 1)];
        if (today.fill != null)
            today.fill.transform.parent.localScale = Vector3.one * (1f + Mathf.Sin(pulse * 4f) * 0.04f);
    }

    void Refresh()
    {
        int next = SaveSystem.Data.dailyNext;
        bool giftDue = !Wallet.Owns(giftItemId);
        ShopItem gift = shop != null ? shop.Find(giftItemId) : null;
        for (int i = 0; i < days.Length; i++)
        {
            DaySlot slot = days[i];
            if (slot == null)
                continue;
            bool last = i == days.Length - 1;
            if (slot.fill != null)
            {
                slot.fill.color = i < next ? claimedColor : i == next ? todayColor : laterColor;
                slot.fill.transform.parent.localScale = Vector3.one;
            }
            if (slot.dayText != null)
                slot.dayText.text = i == next ? "TODAY" : "Day " + (i + 1);
            if (slot.amountText != null)
                slot.amountText.text = last && giftDue && gift != null ? gift.title : CoinsFor(i, giftDue).ToString();
            if (slot.icon != null && last && giftDue && gift != null)
                slot.icon.sprite = gift.icon;
            if (slot.tick != null)
                slot.tick.gameObject.SetActive(i < next);
        }
        if (claimText != null)
            claimText.text = "CLAIM";
    }

    int CoinsFor(int day, bool giftDue)
    {
        int amount = coins[Mathf.Clamp(day, 0, coins.Length - 1)];
        if (day == days.Length - 1 && !giftDue)
            amount += giftReplacementCoins;
        return amount;
    }

    // Hooked to the CLAIM button.
    public void Claim()
    {
        if (claiming || ClaimedToday)
            return;
        claiming = true;

        SaveData save = SaveSystem.Data;
        int day = Mathf.Clamp(save.dailyNext, 0, days.Length - 1);
        bool giftDue = !Wallet.Owns(giftItemId);
        bool giveGift = day == days.Length - 1 && giftDue && shop != null && shop.Find(giftItemId) != null;

        Vector3 from = days[day].icon != null ? days[day].icon.transform.position : claimButton.transform.position;
        CoinCounter.Give(CoinsFor(day, giftDue), from);
        if (giveGift)
            Wallet.Own(giftItemId);
        save.dailyLastClaim = Today;
        save.dailyNext = (day + 1) % days.Length;
        SaveSystem.Save();

        if (days[day].tick != null)
            days[day].tick.gameObject.SetActive(true);
        if (days[day].fill != null)
            days[day].fill.color = claimedColor;
        UIBurst fx = UIBurst.Instance;
        if (fx != null && !ReduceMotion.On)
            fx.Burst(from, fx.confetti, 16, UIBurst.Confetti,
                new Color(1f, 0.62f, 0.55f), new Color(1f, 0.84f, 0.45f), new Color(0.6f, 0.85f, 0.7f));

        if (bond != null)
            bond.Add(bondHearts);
        StartCoroutine(CloseAfterClaim(giveGift));
    }

    IEnumerator CloseAfterClaim(bool gotGift)
    {
        yield return new WaitForSecondsRealtime(0.9f);
        yield return Fade(false);
        if (pet != null)
        {
            Line thanks = gotGift ? new Line("A crown for me?! Find it in STYLE!", LineIcon.Star)
                : new Line("Yay, you came back! See you tomorrow!", LineIcon.Heart);
            pet.Celebrate(thanks);
        }
    }

    IEnumerator Fade(bool show)
    {
        panel.blocksRaycasts = false;
        float time = show ? FeelConfig.Current.cardPopTime : FeelConfig.Current.cardCloseTime;
        for (float t = 0f; t < time; t += Time.unscaledDeltaTime)
        {
            float k = t / time;
            panel.alpha = show ? Mathf.Clamp01(k * 2f) : 1f - k;
            if (!ReduceMotion.On)
                card.localScale = Vector3.one * (show ? Ease.OutBack(k, FeelConfig.Current.cardOvershoot) : Mathf.Lerp(1f, 0.85f, k * k));
            yield return null;
        }
        panel.alpha = show ? 1f : 0f;
        card.localScale = Vector3.one;
        panel.blocksRaycasts = show;
        if (!show)
            panel.gameObject.SetActive(false);
    }
}
