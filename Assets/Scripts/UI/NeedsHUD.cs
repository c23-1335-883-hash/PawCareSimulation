using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Shows the five need meters. Each meter has an icon, a label, a bar that
// turns green / yellow / red, and a "!" badge when the need is low, so the
// state is readable without relying on color alone.
public class NeedsHUD : MonoBehaviour
{
    [Serializable]
    public class Meter
    {
        public Need need;
        public Image fill;
        public GameObject warning;
        public RectTransform root;
    }

    public PetNeeds needs;
    public Meter[] meters;

    [Header("Colors")]
    public Color goodColor = new Color(0.47f, 0.78f, 0.42f);
    public Color okayColor = new Color(0.98f, 0.78f, 0.3f);
    public Color lowColor = new Color(0.93f, 0.4f, 0.36f);
    public float goodAbove = 60f;

    [Header("Motion")]
    [Tooltip("How quickly bars glide to their new value (higher = snappier).")]
    public float fillSharpness = 6f;
    [Tooltip("Below this the whole bar gently pulses red.")]
    public float pulseBelow = 25f;
    public Color pulseColor = new Color(1f, 0.62f, 0.58f);
    public float pulseSpeed = 4f;

    float[] shown;
    float[] lastTarget;

    void OnEnable()
    {
        if (needs != null)
            needs.Changed += Refresh;
    }

    void OnDisable()
    {
        if (needs != null)
            needs.Changed -= Refresh;
    }

    void Start()
    {
        shown = new float[meters.Length];
        lastTarget = new float[meters.Length];
        for (int i = 0; i < meters.Length; i++)
            shown[i] = lastTarget[i] = needs.Get(meters[i].need);
        Refresh();
    }

    void Update()
    {
        // Ease the bars toward their real values and pulse urgent badges.
        for (int i = 0; i < meters.Length; i++)
        {
            Meter m = meters[i];
            float target = needs.Get(m.need);
            if (target > lastTarget[i] + 1f && m.root != null)
                m.root.localScale = Vector3.one * 1.12f;
            lastTarget[i] = target;
            // Ease out: fast at first, settling gently on the value.
            shown[i] = Mathf.Lerp(shown[i], target, 1f - Mathf.Exp(-fillSharpness * Time.unscaledDeltaTime));
            if (Mathf.Abs(shown[i] - target) < 0.05f)
                shown[i] = target;
            m.fill.fillAmount = shown[i] / 100f;

            if (target < pulseBelow)
            {
                float wave = ReduceMotion.On ? 0f : Mathf.Sin(Time.unscaledTime * pulseSpeed) * 0.5f + 0.5f;
                m.fill.color = Color.Lerp(lowColor, pulseColor, wave);
            }

            if (m.warning != null && m.warning.activeSelf)
            {
                float pulse = needs.IsUrgent(m.need) ? 1f + Mathf.Sin(Time.time * 8f) * 0.15f : 1f;
                m.warning.transform.localScale = Vector3.one * pulse;
            }

            if (m.root != null)
                m.root.localScale = Vector3.MoveTowards(m.root.localScale, Vector3.one, 2f * Time.deltaTime);
        }
    }

    void Refresh()
    {
        if (shown == null)
            return;
        foreach (Meter m in meters)
        {
            float v = needs.Get(m.need);
            m.fill.color = v >= goodAbove ? goodColor : needs.IsLow(m.need) ? lowColor : okayColor;
            if (m.warning != null)
                m.warning.SetActive(needs.IsLow(m.need));
        }
    }

}
