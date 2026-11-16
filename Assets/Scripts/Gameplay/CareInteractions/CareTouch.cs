using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Invisible layer over the pet that catches presses and drags while a
// hands-on care activity runs and passes them on. It only blocks touches
// while an activity is active, so the pet and ball work normally otherwise.
public class CareTouch : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler, IPointerUpHandler
{
    public RectTransform Area { get; private set; }

    CareInteraction active;
    Image catcher;

    // Stretches a new layer over the parent and puts it on top of its siblings.
    public static CareTouch Create(Transform parent)
    {
        var go = new GameObject("CareTouch", typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        rt.SetAsLastSibling();

        var touch = go.AddComponent<CareTouch>();
        touch.Area = rt;
        touch.catcher = go.AddComponent<Image>();
        touch.catcher.color = Color.clear;
        touch.catcher.raycastTarget = false;
        return touch;
    }

    public void SetActive(CareInteraction interaction)
    {
        active = interaction;
        catcher.raycastTarget = interaction != null;
    }

    public void OnPointerDown(PointerEventData e)
    {
        if (active != null && active.Running)
            active.Press(Local(e));
    }

    // Needed so drags start on this layer.
    public void OnBeginDrag(PointerEventData e) { }

    public void OnDrag(PointerEventData e)
    {
        if (active != null && active.Running)
            active.Drag(Local(e));
    }

    public void OnPointerUp(PointerEventData e)
    {
        if (active != null && active.Running)
            active.Release(Local(e));
    }

    Vector2 Local(PointerEventData e)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(Area, e.position, e.pressEventCamera, out Vector2 local);
        return local;
    }
}
