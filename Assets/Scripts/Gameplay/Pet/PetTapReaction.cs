using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

// Small "happy" reaction when the player taps a pet on the menu:
// a quick squash-and-stretch hop plus a paw print that floats up and fades.
// The paw print is a single reused object, so nothing is instantiated.
public class PetTapReaction : MonoBehaviour, IPointerClickHandler
{
    public RectTransform popIcon;
    public float hopTime = 0.35f;

    Vector3 baseScale = Vector3.one;
    Vector2 popStart;
    CanvasGroup popGroup;
    Coroutine hop;
    Coroutine pop;

    void Awake()
    {
        baseScale = transform.localScale;
        if (popIcon != null)
        {
            popStart = popIcon.anchoredPosition;
            popGroup = popIcon.GetComponent<CanvasGroup>();
            popIcon.gameObject.SetActive(false);
        }
    }

    void OnDisable()
    {
        transform.localScale = baseScale;
        if (popIcon != null)
            popIcon.gameObject.SetActive(false);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!ReduceMotion.On)
        {
            if (hop != null)
                StopCoroutine(hop);
            hop = StartCoroutine(Hop());
        }

        if (popIcon != null)
        {
            if (pop != null)
                StopCoroutine(pop);
            pop = StartCoroutine(Pop());
        }

        SFXManager.Play(Sfx.HappyChirp);
    }

    IEnumerator Hop()
    {
        for (float t = 0f; t < hopTime; t += Time.unscaledDeltaTime)
        {
            float k = t / hopTime;
            float squash = Mathf.Sin(k * Mathf.PI * 2f) * 0.06f * (1f - k);
            transform.localScale = new Vector3(baseScale.x * (1f + squash), baseScale.y * (1f - squash), baseScale.z);
            yield return null;
        }
        transform.localScale = baseScale;
        hop = null;
    }

    IEnumerator Pop()
    {
        popIcon.gameObject.SetActive(true);
        const float time = 0.8f;
        for (float t = 0f; t < time; t += Time.unscaledDeltaTime)
        {
            float k = t / time;
            popIcon.anchoredPosition = popStart + new Vector2(0f, 120f * k);
            popIcon.localScale = Vector3.one * Mathf.Lerp(0.6f, 1.1f, Mathf.Min(1f, k * 3f));
            if (popGroup != null)
                popGroup.alpha = 1f - k * k;
            yield return null;
        }
        popIcon.gameObject.SetActive(false);
        pop = null;
    }
}
