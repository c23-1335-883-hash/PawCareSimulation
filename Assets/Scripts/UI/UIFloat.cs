using UnityEngine;

// Gentle idle motion for UI elements (pets, decorations).
// Bobs up and down and sways slightly around the starting position.
public class UIFloat : MonoBehaviour
{
    public float height = 12f;
    public float speed = 1.2f;
    public float swayDegrees = 2f;
    public float phase;

    RectTransform rect;
    Vector2 basePosition;

    void Awake()
    {
        rect = (RectTransform)transform;
        basePosition = rect.anchoredPosition;
    }

    void OnDisable()
    {
        rect.anchoredPosition = basePosition;
        rect.localRotation = Quaternion.identity;
    }

    void Update()
    {
        float t = Time.unscaledTime * speed + phase;
        rect.anchoredPosition = basePosition + new Vector2(0f, Mathf.Sin(t) * height);
        rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 0.5f) * swayDegrees);
    }
}
