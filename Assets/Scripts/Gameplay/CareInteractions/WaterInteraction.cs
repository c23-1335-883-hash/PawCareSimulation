using UnityEngine;
using UnityEngine.UI;

// Filling the water bowl: press and hold to pour, and let go when the bowl
// is nicely full. Too little and the pet asks for more; too much and it
// spills everywhere. Then the pet laps it up.
public class WaterInteraction : CareInteraction
{
    [Tooltip("Share of the bowl filled per second while pouring.")]
    public float pourSpeed = 0.45f;
    [Tooltip("The bowl counts as full from here...")]
    public float fullFrom = 0.7f;
    [Tooltip("...and spills over from here.")]
    public float spillAt = 1f;
    public float spillCost = 0.25f;
    public int laps = 3;
    public float lapEvery = 0.5f;
    public float bowlSize = 110f;
    public Vector2 bowlOffset = new Vector2(0f, -90f);
    public Color emptyColor = new Color(0.25f, 0.2f, 0.15f, 0.35f);
    public Color waterColor = new Color(0.45f, 0.72f, 1f, 0.95f);
    public Color fullColor = new Color(0.4f, 0.85f, 0.95f, 1f);

    public override CareActivity? Activity => CareActivity.Water;
    public override string Prompt => "Press and hold to pour. Let go when the bowl is nicely full.";
    protected override bool ToolFollowsFinger => false;

    Image bowl;
    Image water;
    float level;
    bool spilled;       // waits for the player to let go after a spill
    bool drinking;
    int lapped;
    float timer;

    protected override void OnBegin()
    {
        level = 0f;
        spilled = false;
        drinking = false;
        lapped = 0;
        timer = 0f;
        CreateBowl();
    }

    protected override void OnEnd()
    {
        if (bowl != null)
            Destroy(bowl.gameObject);
        bowl = water = null;
    }

    protected override void OnPress(Vector2 local)
    {
        if (!drinking && level < fullFrom)
            care.ShowPrompt(Prompt);
    }

    protected override void OnRelease(Vector2 local)
    {
        if (drinking)
            return;
        spilled = false;
        if (level >= fullFrom)
        {
            drinking = true;
            timer = 0f;
            pet.Say("Fresh water!");
            care.ShowPrompt("Your pet is drinking...");
        }
        else if (level > 0.05f)
        {
            pet.Say("A little more, please...");
        }
    }

    void Update()
    {
        if (!Running)
            return;

        if (drinking)
        {
            timer += Time.deltaTime;
            if (timer < lapEvery)
                return;
            timer = 0f;
            lapped++;
            pet.ActivityStep();
            level = Mathf.Max(0f, level - fullFrom / laps);
            Refresh();
            if (lapped >= laps)
                Finish();
            return;
        }

        if (!Holding || spilled)
            return;

        level += pourSpeed * Time.deltaTime;
        timer += Time.deltaTime;
        if (timer >= 0.12f && Tool != null)
        {
            timer = 0f;
            pet.Splash(Tool.rectTransform.position);
            SFXManager.Play(Sfx.Pop, 0.25f);
        }
        if (level >= spillAt)
        {
            spilled = true;
            level = fullFrom + (spillAt - fullFrom) * 0.5f;
            quality -= spillCost;
            for (int i = 0; i < 3; i++)
                pet.Splash(bowl.rectTransform.position + new Vector3((i - 1) * bowlSize * 0.4f, 0f, 0f));
            pet.Flinch("Whoa, it's overflowing!");
            care.ShowPrompt("Oops, it spilled! Let go to stop pouring.");
        }
        Refresh();
    }

    // A round bowl next to the jug that fills from the bottom.
    void CreateBowl()
    {
        Sprite round = UIBurst.Instance != null ? UIBurst.Instance.dot : null;
        Vector3 at = Tool != null ? Tool.rectTransform.position : pet.PetPoint(0.5f, 0f);

        var go = new GameObject("WaterBowl", typeof(RectTransform));
        go.layer = area.gameObject.layer;
        go.transform.SetParent(area, false);
        bowl = go.AddComponent<Image>();
        bowl.sprite = round;
        bowl.color = emptyColor;
        bowl.raycastTarget = false;
        bowl.rectTransform.sizeDelta = new Vector2(bowlSize, bowlSize * 0.6f);
        bowl.rectTransform.position = at;
        bowl.rectTransform.anchoredPosition += bowlOffset;

        var fillGo = new GameObject("Water", typeof(RectTransform));
        fillGo.layer = go.layer;
        fillGo.transform.SetParent(go.transform, false);
        water = fillGo.AddComponent<Image>();
        water.sprite = round;
        water.type = Image.Type.Filled;
        water.fillMethod = Image.FillMethod.Vertical;
        water.fillOrigin = (int)Image.OriginVertical.Bottom;
        water.raycastTarget = false;
        var rt = water.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        Refresh();
    }

    void Refresh()
    {
        if (water == null)
            return;
        water.fillAmount = Mathf.Clamp01(level / spillAt);
        water.color = level >= fullFrom ? fullColor : waterColor;
    }
}
