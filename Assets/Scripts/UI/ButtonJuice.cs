using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// Shared "game feel" for buttons and tappable cards:
// - hover (mouse only): grows slightly with an ease-out and plays a soft tick
// - press: squishes like clay (a bit wider, a bit shorter)
// - release: springs back with a small overshoot and plays a pop
// Scale effects are skipped when Reduce Motion is on; sounds still play.
// The sizes and timings come from FeelConfig, so every button feels the same.
public class ButtonJuice : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
{
    public bool playHoverSound = true;
    public bool playClickSound = true;
    [Tooltip("Gently breathe in and out while idle, to draw the eye (e.g. the main call to action).")]
    public bool idlePulse;

    static FeelConfig Config => FeelConfig.Current;

    Vector3 baseScale = Vector3.one;
    Selectable selectable;
    bool hovered;
    bool pressed;
    Coroutine tween;

    void Awake()
    {
        baseScale = transform.localScale;
        selectable = GetComponent<Selectable>();
    }

    void OnDisable()
    {
        transform.localScale = baseScale;
        hovered = pressed = false;
        tween = null;
    }

    void Update()
    {
        if (!idlePulse || hovered || pressed || tween != null || ReduceMotion.On)
            return;
        float wave = Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / Config.pulsePeriod) * 0.5f + 0.5f;
        transform.localScale = baseScale * (1f + Config.pulseAmount * wave);
    }

    bool Usable => selectable == null || selectable.IsInteractable();

    Vector3 RestScale => hovered ? baseScale * Config.hoverScale : baseScale;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!Usable || IsTouch(eventData))
            return;
        hovered = true;
        if (playHoverSound)
            SFXManager.Play(Sfx.Hover);
        if (!pressed)
            Run(Tween(RestScale, Config.hoverTime, false));
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!hovered)
            return;
        hovered = false;
        if (!pressed)
            Run(Tween(RestScale, Config.hoverTime, false));
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!Usable)
            return;
        pressed = true;
        Vector3 rest = RestScale;
        Vector3 squished = new Vector3(rest.x * (Config.pressedScale + Config.pressSquash), rest.y * (Config.pressedScale - Config.pressSquash), rest.z);
        Run(Tween(squished, Config.pressTime, false));
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!pressed)
            return;
        pressed = false;
        Run(Tween(RestScale, Config.releaseTime, true));
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (playClickSound && Usable)
            SFXManager.Play(Sfx.Pop);
    }

    static bool IsTouch(PointerEventData eventData)
    {
        var extended = eventData as ExtendedPointerEventData;
        return extended != null && extended.pointerType == UIPointerType.Touch;
    }

    void Run(IEnumerator routine)
    {
        if (!isActiveAndEnabled || ReduceMotion.On)
            return;
        if (tween != null)
            StopCoroutine(tween);
        tween = StartCoroutine(routine);
    }

    IEnumerator Tween(Vector3 target, float time, bool bounce)
    {
        Vector3 start = transform.localScale;
        for (float t = 0f; t < time; t += Time.unscaledDeltaTime)
        {
            float k = bounce ? Ease.OutBack(t / time, Config.releaseOvershoot) : Ease.OutQuad(t / time);
            transform.localScale = Vector3.LerpUnclamped(start, target, k);
            yield return null;
        }
        transform.localScale = target;
        tween = null;
    }
}

// Small easing helpers shared by the UI effects.
public static class Ease
{
    public static float OutQuad(float t)
    {
        t = Mathf.Clamp01(t);
        return 1f - (1f - t) * (1f - t);
    }

    public static float InOutSine(float t)
    {
        return -(Mathf.Cos(Mathf.PI * Mathf.Clamp01(t)) - 1f) / 2f;
    }

    public static float OutBack(float t, float overshoot = 1.70158f)
    {
        t = Mathf.Clamp01(t);
        float c3 = overshoot + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + overshoot * Mathf.Pow(t - 1f, 2f);
    }
}
