using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum ShopTab
{
    Treats,
    Style,
    Rooms
}

[Serializable]
public class ShopItem
{
    [Tooltip("Saved in the save file; don't change it after release.")]
    public string id;
    public string title;
    public string blurb;
    public ShopTab tab;
    public Sprite icon;
    public Color iconColor = Color.white;
    public int price;
    [Tooltip("Bond level needed before it can be bought.")]
    public int bondLevel = 1;

    [Header("Treats (used straight away)")]
    public Need need;
    public float amount = 30f;
    public float happiness = 5f;
    public int bondHearts = 2;
    public string thanks = "Yummy, thank you!";

    [Header("Style (where it sits on the pet, 0-1)")]
    public Vector2 anchor = new Vector2(0.5f, 0.95f);
    [Tooltip("Width as a share of the pet's width.")]
    public float width = 0.3f;
    public float angle;

    [Header("Rooms")]
    [Tooltip("Multiplied with the room picture.")]
    public Color tint = Color.white;
}

// The Shop card: treats that help the pet right away, accessories it can wear,
// and colour moods for the room. Opened from the sidebar (SHOP and STYLE).
// Items that need a higher Bond show a lock. Coins are never lost on a refusal.
public class ShopUI : MonoBehaviour
{
    [Header("References")]
    public PetCareManager care;
    public PetNeeds needs;
    public PetController pet;
    public PetBond bond;
    [Tooltip("The room picture that room themes tint.")]
    public Image room;
    [Tooltip("Image on the pet that shows the worn accessory.")]
    public Image accessory;

    [Header("Panel")]
    public CanvasGroup panel;
    public RectTransform card;
    public TMP_Text message;
    public Image[] tabFills = new Image[3];
    public Color tabOn = new Color(1f, 0.8f, 0.55f);
    public Color tabOff = new Color(0.93f, 0.89f, 0.84f);
    public RectTransform grid;
    public ShopSlot slotTemplate;

    [Header("Slot Buttons")]
    public Color buyColor = new Color(1f, 0.8f, 0.5f);
    public Color useColor = new Color(0.7f, 0.9f, 0.75f);
    public Color wornColor = new Color(0.98f, 0.78f, 0.85f);
    public Color lockedColor = new Color(0.85f, 0.83f, 0.82f);

    [Header("Items")]
    public ShopItem[] items =
    {
        new ShopItem { id = "treat_biscuit", title = "Crunchy Biscuit", blurb = "+35 food", tab = ShopTab.Treats, price = 15,
            need = Need.Hunger, amount = 35f, happiness = 8f, thanks = "Crunch crunch! Yummy!" },
        new ShopItem { id = "treat_drink", title = "Cool Drink", blurb = "+40 water", tab = ShopTab.Treats, price = 10,
            need = Need.Thirst, amount = 40f, happiness = 5f, thanks = "Ahh, so refreshing!" },
        new ShopItem { id = "treat_soap", title = "Bubble Soap", blurb = "+50 clean", tab = ShopTab.Treats, price = 20, bondLevel = 2,
            need = Need.Cleanliness, amount = 50f, happiness = 5f, thanks = "So fresh and fluffy!" },
        new ShopItem { id = "treat_toy", title = "Squeaky Toy", blurb = "+30 mood", tab = ShopTab.Treats, price = 25, bondLevel = 3,
            need = Need.Happiness, amount = 30f, happiness = 0f, bondHearts = 3, thanks = "Squeak squeak! I love it!" },
        new ShopItem { id = "treat_pillow", title = "Cozy Pillow", blurb = "+40 energy", tab = ShopTab.Treats, price = 30, bondLevel = 4,
            need = Need.Energy, amount = 40f, happiness = 5f, bondHearts = 3, thanks = "So comfy... thank you!" },

        new ShopItem { id = "acc_bow", title = "Pink Bow", blurb = "Sweet and simple", tab = ShopTab.Style, price = 40,
            anchor = new Vector2(0.68f, 0.9f), width = 0.3f, angle = -15f },
        new ShopItem { id = "acc_daisy", title = "Daisy", blurb = "Fresh from the garden", tab = ShopTab.Style, price = 60, bondLevel = 2,
            anchor = new Vector2(0.32f, 0.9f), width = 0.24f, angle = 12f },
        new ShopItem { id = "acc_partyhat", title = "Party Hat", blurb = "Every day is a party", tab = ShopTab.Style, price = 80, bondLevel = 3,
            anchor = new Vector2(0.5f, 1.0f), width = 0.28f, angle = 8f },
        new ShopItem { id = "acc_star", title = "Star Clip", blurb = "You're a star!", tab = ShopTab.Style, price = 100, bondLevel = 5,
            anchor = new Vector2(0.68f, 0.9f), width = 0.22f, angle = -10f },
        new ShopItem { id = "acc_crown", title = "Golden Crown", blurb = "Also the day 7 gift", tab = ShopTab.Style, price = 200, bondLevel = 7,
            anchor = new Vector2(0.5f, 0.99f), width = 0.36f },

        new ShopItem { id = "room_cozy", title = "Cozy", blurb = "The classic look", tab = ShopTab.Rooms, price = 0,
            iconColor = new Color(0.95f, 0.8f, 0.65f) },
        new ShopItem { id = "room_sunny", title = "Sunny", blurb = "Warm and bright", tab = ShopTab.Rooms, price = 50,
            iconColor = new Color(1f, 0.85f, 0.45f), tint = new Color(1f, 0.9f, 0.68f) },
        new ShopItem { id = "room_mint", title = "Minty", blurb = "Cool and calm", tab = ShopTab.Rooms, price = 80, bondLevel = 2,
            iconColor = new Color(0.55f, 0.88f, 0.7f), tint = new Color(0.8f, 1f, 0.88f) },
        new ShopItem { id = "room_lilac", title = "Lilac", blurb = "Soft and dreamy", tab = ShopTab.Rooms, price = 100, bondLevel = 4,
            iconColor = new Color(0.75f, 0.65f, 0.95f), tint = new Color(0.86f, 0.78f, 1f) },
        new ShopItem { id = "room_sunset", title = "Sunset", blurb = "Golden evening glow", tab = ShopTab.Rooms, price = 150, bondLevel = 6,
            iconColor = new Color(1f, 0.6f, 0.5f), tint = new Color(1f, 0.76f, 0.66f) },
    };

    readonly List<ShopSlot> slots = new List<ShopSlot>();
    ShopTab tab;
    Coroutine panelRoutine;
    Coroutine messageRoutine;
    bool open;

    void Awake()
    {
        if (slotTemplate != null)
            slotTemplate.gameObject.SetActive(false);
        if (panel != null)
        {
            panel.alpha = 0f;
            panel.blocksRaycasts = false;
            panel.gameObject.SetActive(false);
        }
    }

    void OnEnable()
    {
        Wallet.Changed += RefreshIfOpen;
    }

    void OnDisable()
    {
        Wallet.Changed -= RefreshIfOpen;
    }

    void Start()
    {
        ApplyRoom();
        ApplyAccessory();
    }

    void LateUpdate()
    {
        // The pet changes size as it grows; keep the accessory in proportion.
        ShopItem worn = Find(SaveSystem.Data.accessory);
        if (accessory != null && worn != null && pet != null)
        {
            float w = pet.pet.rect.width * worn.width;
            accessory.rectTransform.sizeDelta = new Vector2(w, w);
        }
    }

    // ---- Buttons ----

    public void Open() { Open(ShopTab.Treats); }
    public void OpenStyle() { Open(ShopTab.Style); }
    public void ShowTreats() { ShowTab(ShopTab.Treats); }
    public void ShowStyle() { ShowTab(ShopTab.Style); }
    public void ShowRooms() { ShowTab(ShopTab.Rooms); }

    public void Close()
    {
        if (!open)
            return;
        open = false;
        SaveSystem.SaveIfDirty();
        RunPanel(Fade(false));
    }

    void Open(ShopTab startTab)
    {
        open = true;
        panel.gameObject.SetActive(true);
        ShowTab(startTab);
        Say("");
        RunPanel(Fade(true));
    }

    void ShowTab(ShopTab newTab)
    {
        tab = newTab;
        for (int i = 0; i < tabFills.Length; i++)
        {
            if (tabFills[i] != null)
                tabFills[i].color = i == (int)tab ? tabOn : tabOff;
        }
        Refresh();
    }

    void RefreshIfOpen()
    {
        if (open)
            Refresh();
    }

    // ---- Slots ----

    void Refresh()
    {
        int n = 0;
        foreach (ShopItem item in items)
        {
            if (item.tab != tab)
                continue;
            ShopSlot slot = SlotAt(n++);
            Fill(slot, item);
        }
        for (int i = n; i < slots.Count; i++)
            slots[i].gameObject.SetActive(false);
    }

    ShopSlot SlotAt(int index)
    {
        while (slots.Count <= index)
        {
            ShopSlot slot = Instantiate(slotTemplate, grid);
            slot.name = "Slot " + slots.Count;
            slots.Add(slot);
        }
        slots[index].gameObject.SetActive(true);
        return slots[index];
    }

    void Fill(ShopSlot slot, ShopItem item)
    {
        bool locked = PetBond.Level < item.bondLevel;
        bool owned = Owned(item);
        bool inUse = (item.tab == ShopTab.Style && SaveSystem.Data.accessory == item.id)
            || (item.tab == ShopTab.Rooms && CurrentRoom() == item);

        slot.icon.sprite = item.icon;
        slot.icon.color = locked ? Color.Lerp(item.iconColor, new Color(0.6f, 0.6f, 0.6f), 0.6f) : item.iconColor;
        slot.title.text = item.title;
        slot.blurb.text = item.blurb;
        if (slot.lockIcon != null)
            slot.lockIcon.gameObject.SetActive(locked);
        if (slot.tick != null)
            slot.tick.gameObject.SetActive(inUse);

        string label;
        Color color;
        bool showCoin = false;
        if (locked)
        {
            label = "Bond " + item.bondLevel;
            color = lockedColor;
        }
        else if (item.tab == ShopTab.Treats || !owned)
        {
            label = item.price.ToString();
            color = buyColor;
            showCoin = true;
        }
        else if (item.tab == ShopTab.Style)
        {
            label = inUse ? "TAKE OFF" : "WEAR";
            color = inUse ? wornColor : useColor;
        }
        else
        {
            label = inUse ? "IN USE" : "USE";
            color = inUse ? wornColor : useColor;
        }

        slot.buttonText.text = label;
        slot.buttonFill.color = color;
        if (slot.coin != null)
            slot.coin.gameObject.SetActive(showCoin);
        // Leave room for the coin in front of the price.
        slot.buttonText.margin = new Vector4(showCoin ? 44f : 0f, 0f, 0f, 0f);

        slot.button.onClick.RemoveAllListeners();
        slot.button.onClick.AddListener(() => Choose(item, slot));
    }

    void Choose(ShopItem item, ShopSlot slot)
    {
        if (PetBond.Level < item.bondLevel)
        {
            Say("Reach Bond " + item.bondLevel + " with " + PetProfile.Name + " to unlock this.");
            Shake(slot);
            return;
        }

        if (item.tab == ShopTab.Treats)
        {
            GiveTreat(item, slot);
            return;
        }

        if (!Owned(item))
        {
            if (!Wallet.Spend(item.price))
            {
                NotEnough(item, slot);
                return;
            }
            Wallet.Own(item.id);
            SFXManager.Play(Sfx.Coin);
        }

        if (item.tab == ShopTab.Style)
        {
            if (SaveSystem.Data.accessory == item.id)
            {
                SaveSystem.Data.accessory = "";
                ApplyAccessory();
                Say("Taken off.");
                Refresh();
            }
            else
            {
                SaveSystem.Data.accessory = item.id;
                Close();
                ApplyAccessory();
                ShowOffAccessory();
            }
        }
        else
        {
            SaveSystem.Data.room = item.id;
            Close();
            ApplyRoom();
            pet.Celebrate(new Line("I love our new room!", LineIcon.Star));
        }
        SaveSystem.MarkDirty();
        SaveSystem.SaveIfDirty();
    }

    void GiveTreat(ShopItem item, ShopSlot slot)
    {
        if (care != null && care.IsResting)
        {
            Say("Shh! " + PetProfile.Name + " is napping. Try again after the nap.");
            return;
        }
        if (needs != null && needs.Get(item.need) >= 95f)
        {
            Say(PetProfile.Name + " doesn't need that right now. Save your coins!");
            Shake(slot);
            return;
        }
        if (!Wallet.Spend(item.price))
        {
            NotEnough(item, slot);
            return;
        }

        SFXManager.Play(Sfx.Coin);
        Close();
        if (care != null)
            care.StopActivity();
        if (needs != null)
        {
            needs.Add(item.need, item.amount);
            if (item.happiness != 0f)
                needs.Add(Need.Happiness, item.happiness);
            needs.Store();
        }
        pet.Celebrate(new Line(item.thanks, LineIcon.Heart));
        if (bond != null)
            bond.Add(item.bondHearts);
        SaveSystem.SaveIfDirty();
    }

    void NotEnough(ShopItem item, ShopSlot slot)
    {
        int missing = item.price - Wallet.Coins;
        Say("You need " + missing + " more coin" + (missing == 1 ? "" : "s") + ". Caring for " + PetProfile.Name + " earns coins!");
        Shake(slot);
    }

    // ---- Accessory and room ----

    void ApplyAccessory()
    {
        if (accessory == null)
            return;
        ShopItem worn = Find(SaveSystem.Data.accessory);
        bool show = worn != null && worn.tab == ShopTab.Style && Owned(worn);
        accessory.gameObject.SetActive(show);
        if (!show)
            return;

        RectTransform rt = accessory.rectTransform;
        rt.anchorMin = rt.anchorMax = worn.anchor;
        rt.anchoredPosition = Vector2.zero;
        rt.localRotation = Quaternion.Euler(0f, 0f, worn.angle);
        accessory.sprite = worn.icon;
        accessory.color = worn.iconColor;
        accessory.preserveAspect = true;
        accessory.raycastTarget = false;
    }

    void ShowOffAccessory()
    {
        UIBurst fx = UIBurst.Instance;
        if (fx != null && !ReduceMotion.On)
            fx.Burst(accessory.rectTransform.position, fx.dot, 10, PetController.Sparkle, pet.sparkleColor, Color.white);
        pet.Celebrate(new Line(PetProfile.IsCat ? "Purr... do I look cute?" : "Do I look cute? Woof!", LineIcon.Star));
    }

    void ApplyRoom()
    {
        if (room != null)
            room.color = CurrentRoom() != null ? CurrentRoom().tint : Color.white;
    }

    ShopItem CurrentRoom()
    {
        ShopItem chosen = Find(SaveSystem.Data.room);
        if (chosen != null && chosen.tab == ShopTab.Rooms && Owned(chosen))
            return chosen;
        foreach (ShopItem item in items)
        {
            if (item.tab == ShopTab.Rooms && item.price == 0)
                return item;
        }
        return null;
    }

    public ShopItem Find(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;
        foreach (ShopItem item in items)
        {
            if (item.id == id)
                return item;
        }
        return null;
    }

    static bool Owned(ShopItem item)
    {
        return item.price == 0 || Wallet.Owns(item.id);
    }

    // ---- Feedback ----

    void Say(string text)
    {
        if (message == null)
            return;
        message.text = PetProfile.Fill(text);
        if (messageRoutine != null)
            StopCoroutine(messageRoutine);
        if (!string.IsNullOrEmpty(text))
            messageRoutine = StartCoroutine(ClearMessage());
    }

    IEnumerator ClearMessage()
    {
        yield return new WaitForSecondsRealtime(4f);
        message.text = "";
        messageRoutine = null;
    }

    void Shake(ShopSlot slot)
    {
        if (!ReduceMotion.On)
            StartCoroutine(ShakeRoutine(slot.transform));
    }

    IEnumerator ShakeRoutine(Transform target)
    {
        for (float t = 0f; t < 0.35f; t += Time.unscaledDeltaTime)
        {
            target.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 50f) * 5f * (1f - t / 0.35f));
            yield return null;
        }
        target.localRotation = Quaternion.identity;
    }

    void RunPanel(IEnumerator routine)
    {
        if (panelRoutine != null)
            StopCoroutine(panelRoutine);
        panelRoutine = StartCoroutine(routine);
    }

    IEnumerator Fade(bool show)
    {
        panel.blocksRaycasts = false;
        float from = panel.alpha;
        float to = show ? 1f : 0f;
        float time = show ? FeelConfig.Current.cardPopTime : FeelConfig.Current.cardCloseTime;
        for (float t = 0f; t < time; t += Time.unscaledDeltaTime)
        {
            float k = t / time;
            panel.alpha = Mathf.Lerp(from, to, show ? k * 2f : k);
            if (!ReduceMotion.On)
                card.localScale = Vector3.one * (show ? Ease.OutBack(k, FeelConfig.Current.cardOvershoot) : Mathf.Lerp(1f, 0.85f, k * k));
            yield return null;
        }
        panel.alpha = to;
        card.localScale = Vector3.one;
        panel.blocksRaycasts = show;
        if (!show)
            panel.gameObject.SetActive(false);
        panelRoutine = null;
    }
}
