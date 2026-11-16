using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// "Clean the pet's environment": every few minutes a small mess (a poop or
// a puddle) appears on the floor beside the pet. Tapping it cleans it up and
// earns Care Points. Messes and the time of the next one are kept in the save
// file, so leaving and re-opening home can't create extra messes to farm.
public class RoomMess : MonoBehaviour
{
    [Header("References")]
    public PetCareManager care;
    [Tooltip("The pet's RectTransform; messes appear on the floor to its sides.")]
    public RectTransform petRect;
    public Sprite[] messSprites;
    [Tooltip("Messes are hidden while the clinic is open (found automatically if empty).")]
    public VetClinic clinic;

    [Header("Timing (real minutes)")]
    public Vector2 minutesBetweenMesses = new Vector2(3f, 5f);
    public int maxMesses = 2;

    [Header("Look")]
    public float messSize = 120f;
    [Tooltip("Where each mess can appear, measured from the pet's feet (x right, y up).")]
    public Vector2[] spotOffsets = { new Vector2(330f, 0f), new Vector2(-290f, 170f) };

    readonly List<RectTransform> shown = new List<RectTransform>();
    bool[] spotUsed;

    void Start()
    {
        if (clinic == null)
            clinic = FindFirstObjectByType<VetClinic>(FindObjectsInactive.Include);
        spotUsed = new bool[spotOffsets.Length];
        maxMesses = Mathf.Min(maxMesses, spotOffsets.Length);
        SaveData d = SaveSystem.Data;
        if (d.nextMessTicks <= 0)
            ScheduleNext(DateTime.UtcNow);
        d.messCount = Mathf.Clamp(d.messCount, 0, maxMesses);
        Refresh();
    }

    void Update()
    {
        // Messes belong to the room: hide them while the vet clinic is showing.
        bool inClinic = clinic != null && clinic.IsOpen;
        foreach (RectTransform m in shown)
        {
            if (m != null && m.gameObject.activeSelf == inClinic)
                m.gameObject.SetActive(!inClinic);
        }
        if (!inClinic && Time.frameCount % 30 == 0)
            Refresh();
    }

    void Refresh()
    {
        CatchUp();
        while (shown.Count < SaveSystem.Data.messCount && Array.IndexOf(spotUsed, false) >= 0)
            Show();
    }

    // Adds the messes that came due, also while the player was away (up to the maximum).
    void CatchUp()
    {
        SaveData d = SaveSystem.Data;
        bool added = false;
        while (d.messCount < maxMesses && DateTime.UtcNow.Ticks >= d.nextMessTicks)
        {
            d.messCount++;
            added = true;
            ScheduleNext(new DateTime(d.nextMessTicks, DateTimeKind.Utc));
        }
        if (d.messCount >= maxMesses && DateTime.UtcNow.Ticks >= d.nextMessTicks)
            ScheduleNext(DateTime.UtcNow); // full: the next mess waits until there's room again
        if (added)
            SaveSystem.MarkDirty();
    }

    void ScheduleNext(DateTime from)
    {
        double minutes = UnityEngine.Random.Range(minutesBetweenMesses.x, minutesBetweenMesses.y);
        SaveSystem.Data.nextMessTicks = from.AddMinutes(minutes).Ticks;
    }

    void Show()
    {
        int spot = Array.IndexOf(spotUsed, false);
        if (spot < 0 || petRect == null || messSprites == null || messSprites.Length == 0)
            return;
        spotUsed[spot] = true;

        GameObject go = new GameObject("Mess", typeof(RectTransform), typeof(Image), typeof(Button));
        RectTransform rt = (RectTransform)go.transform;
        rt.SetParent(petRect.parent, false);
        rt.SetSiblingIndex(petRect.GetSiblingIndex()); // on the floor, behind the pet
        rt.anchorMin = rt.anchorMax = petRect.anchorMin;
        rt.pivot = new Vector2(0.5f, 0f);
        rt.sizeDelta = new Vector2(messSize, messSize);
        float floor = petRect.anchoredPosition.y - petRect.rect.height * petRect.pivot.y;
        rt.anchoredPosition = new Vector2(petRect.anchoredPosition.x, floor) + spotOffsets[spot];

        Image img = go.GetComponent<Image>();
        img.sprite = messSprites[UnityEngine.Random.Range(0, messSprites.Length)];
        img.preserveAspect = true;
        go.AddComponent<ButtonJuice>();
        Button button = go.GetComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(() => Clean(rt, spot));
        shown.Add(rt);
    }

    void Clean(RectTransform mess, int spot)
    {
        if (mess == null || !shown.Remove(mess))
            return; // already cleaned (double tap)
        spotUsed[spot] = false;

        SaveData d = SaveSystem.Data;
        if (d.messCount >= maxMesses)
            ScheduleNext(DateTime.UtcNow); // the room was full: the next mess starts its wait now
        d.messCount = Mathf.Max(0, d.messCount - 1);
        if (UIBurst.Instance != null)
            UIBurst.Instance.Burst(mess.position, UIBurst.Instance.dot, 10, UIBurst.Rise, Color.white, new Color(0.8f, 0.93f, 0.68f));
        SFXManager.Play(Sfx.Bubbles);
        Destroy(mess.gameObject);

        bool earned = CarePoints.Add(CarePoints.CleanRoom, "Clean room");
        if (care != null)
            care.ShowTip("Squeaky clean! A tidy home keeps your pet healthy." + (earned ? "\n<size=80%>+" + CarePoints.CleanRoom + " Care Points</size>" : ""));
        SaveSystem.SaveIfDirty();
    }
}
