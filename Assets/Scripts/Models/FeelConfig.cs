using UnityEngine;

// One place to tune how the game feels: button squish, card pop, pet breathing,
// sound variation and particle amounts. Edit Assets/Resources/FeelConfig.asset;
// if that asset is missing, these defaults are used.
[CreateAssetMenu(fileName = "FeelConfig", menuName = "PAWcare/Feel Config")]
public class FeelConfig : ScriptableObject
{
    [Header("Buttons")]
    public float hoverScale = 1.08f;
    public float hoverTime = 0.12f;
    public float pressedScale = 0.92f;
    [Tooltip("How much wider/shorter a button gets while pressed.")]
    public float pressSquash = 0.05f;
    public float pressTime = 0.08f;
    public float releaseTime = 0.22f;
    [Tooltip("Spring-back overshoot when a button is released.")]
    public float releaseOvershoot = 1.9f;
    public float pulseAmount = 0.05f;
    public float pulsePeriod = 1.2f;

    [Header("Pet Card (selection)")]
    public float cardPopTime = 0.35f;
    [Tooltip("Ease-out-back strength. 1.7 peaks at about 110% size.")]
    public float cardOvershoot = 1.70158f;
    public float cardCloseTime = 0.2f;
    [Range(0f, 1f)] public float cardOverlayAlpha = 0.5f;

    [Header("Pet")]
    public float breathSpeed = 2.2f;
    [Tooltip("Breathing grows the pet from 100% to 100% + this (0.03 = 103%).")]
    public float breathAmount = 0.03f;
    public Vector2 idleMoveInterval = new Vector2(4f, 8f);
    public Vector2 blinkInterval = new Vector2(3f, 6f);
    [Tooltip("Seconds between chatty lines when nothing is urgent.")]
    public Vector2 chatterInterval = new Vector2(25f, 45f);

    [Header("Sound")]
    public Vector2 pitchRange = new Vector2(0.95f, 1.05f);
    [Range(0f, 1f)] public float hoverVolume = 0.35f;

    [Header("Effects")]
    [Tooltip("Multiplies how many particles bursts use (0.5 = half).")]
    public float particleAmount = 1f;

    static FeelConfig current;

    public static FeelConfig Current
    {
        get
        {
            if (current == null)
            {
                current = Resources.Load<FeelConfig>("FeelConfig");
                if (current == null)
                    current = CreateInstance<FeelConfig>();
            }
            return current;
        }
    }
}
