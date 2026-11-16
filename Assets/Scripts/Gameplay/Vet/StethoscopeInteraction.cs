using UnityEngine;
using UnityEngine.UI;

// Listening to the heart: hold the stethoscope still on the glowing spot on the
// pet's chest until you've heard enough heartbeats. Moving it around tickles.
public class StethoscopeInteraction : CareInteraction
{
    public int beatsNeeded = 6;
    public float beatEvery = 0.55f;
    public float chestRadius = 90f;
    [Tooltip("Share of the rough speed that counts as moving the stethoscope.")]
    public float stillShare = 0.3f;
    public Color toolColor = new Color(0.72f, 0.76f, 0.82f);
    public Color spotColor = new Color(1f, 0.5f, 0.55f, 0.6f);

    public override string Prompt => "Place the stethoscope on the glowing spot on your pet's chest and hold it still.";

    Vector2 Chest => PetLocal(0.5f, 0.4f);

    int beats;
    float timer;
    bool touched;
    Color oldColor;
    Image spot;

    protected override void OnBegin()
    {
        beats = 0;
        timer = 0f;
        touched = false;
        if (Tool != null)
        {
            oldColor = Tool.color;
            Tool.color = toolColor;
        }
        spot = AddMarker(Chest, spotColor, 70f);
    }

    protected override void OnEnd()
    {
        if (Tool != null)
            Tool.color = oldColor;
    }

    void Update()
    {
        if (!Running)
            return;
        if (spot != null)
            spot.rectTransform.localScale = Vector3.one * (1f + Mathf.Sin(Time.time * 4f) * 0.12f);
        if (!Holding || (Pointer + toolOffset - Chest).magnitude > chestRadius)
        {
            timer = 0f;
            return;
        }
        if (!touched)
        {
            touched = true;
            pet.Say("Eek, that's cold!");
        }
        if (Shaky(stillShare, "Hee hee, that tickles!"))
        {
            timer = 0f;
            return;
        }

        timer += Time.deltaTime;
        if (timer < beatEvery)
            return;
        timer = 0f;
        beats++;
        SFXManager.Play(Sfx.Pop, 0.35f);
        if (spot != null)
            spot.rectTransform.localScale = Vector3.one * 1.5f;
        UIBurst fx = ReduceMotion.On ? null : UIBurst.Instance;
        if (fx != null && fx.heart != null)
            fx.Burst(ToWorld(Chest), fx.heart, 1, UIBurst.Rise, spotColor);
        if (beats == beatsNeeded / 2)
            care.ShowPrompt("Thump-thump... keep holding still.");
        if (beats >= beatsNeeded)
            Finish();
    }
}
