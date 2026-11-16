using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Base for a hands-on care activity. Instead of tapping the pet a few times,
// the player drags the activity's prop (bowl, sponge, brush) and the pet
// reacts to how it's done. Rough handling makes the pet flinch and lowers the
// quality (0 = rough, 1 = gentle and thorough), which scales the reward.
// Put these next to PetCareManager; it starts the one matching each activity.
public abstract class CareInteraction : MonoBehaviour
{
    [Tooltip("Drag speed (canvas units per second) above which the pet flinches.")]
    public float roughSpeed = 2600f;
    [Tooltip("Quality lost each time the pet flinches.")]
    public float flinchCost = 0.15f;
    [Tooltip("Where the prop sits relative to the finger, so the finger doesn't hide it.")]
    public Vector2 toolOffset = new Vector2(0f, 40f);

    // The care activity this replaces; null for cuddles and bedtime, which PetCareManager starts itself.
    public virtual CareActivity? Activity => null;
    public abstract string Prompt { get; }
    public bool Running { get; private set; }
    public float Quality => Mathf.Clamp01(quality);

    protected PetCareManager care;
    protected PetController pet;
    protected RectTransform area;      // the touch layer; positions below are local to it
    protected float quality;
    protected bool Holding { get; private set; }
    protected Vector2 Pointer { get; private set; }

    // Shy pets are more sensitive to rough hands.
    protected float RoughLimit => PetProfile.Trait == Trait.Shy ? roughSpeed * 0.7f : roughSpeed;
    protected float Speed => Time.unscaledTime - lastDragTime > 0.1f ? 0f : speed;
    // Recent drag direction and speed, for flicks.
    protected Vector2 Velocity => Time.unscaledTime - lastDragTime > 0.1f ? Vector2.zero : velocity;
    protected Image Tool => pet.activityProp;
    // False when the prop should stay where it is (e.g. the water jug pours in place).
    protected virtual bool ToolFollowsFinger => true;

    readonly List<Image> markers = new List<Image>();
    Vector2 toolHome;
    float speed;
    Vector2 velocity;
    float lastDragTime;
    float lastFlinch = -100f;

    public void Begin(PetCareManager care, RectTransform area)
    {
        this.care = care;
        pet = care.pet;
        this.area = area;
        if (Tool != null)
            toolHome = Tool.rectTransform.anchoredPosition;
        quality = 1f;
        Holding = false;
        Running = true;
        OnBegin();
    }

    public void Cancel()
    {
        if (!Running)
            return;
        Running = false;
        End();
    }

    protected void Finish()
    {
        Running = false;
        End();
        care.FinishInteraction(Mathf.Clamp01(quality));
    }

    void End()
    {
        OnEnd();
        Holding = false;
        foreach (Image m in markers)
        {
            if (m != null)
                Destroy(m.gameObject);
        }
        markers.Clear();
        ReturnTool();
        pet.Watch(null);
    }

    // ---- Input from CareTouch ----

    public void Press(Vector2 local)
    {
        Holding = true;
        Pointer = local;
        speed = 0f;
        velocity = Vector2.zero;
        MoveTool(local);
        OnPress(local);
    }

    public void Drag(Vector2 local)
    {
        Vector2 delta = local - Pointer;
        float dt = Mathf.Max(Time.unscaledDeltaTime, 1f / 120f);
        speed = Mathf.Lerp(speed, delta.magnitude / dt, 0.35f);
        velocity = Vector2.Lerp(velocity, delta / dt, 0.5f);
        lastDragTime = Time.unscaledTime;
        Pointer = local;
        MoveTool(local);
        pet.Watch(ToWorld(local));
        OnDrag(local, delta);
    }

    public void Release(Vector2 local)
    {
        Holding = false;
        OnRelease(local);
        if (Running)
        {
            ReturnTool();
            pet.Watch(null);
        }
    }

    protected virtual void OnBegin() { }
    protected virtual void OnEnd() { }
    protected virtual void OnPress(Vector2 local) { }
    protected virtual void OnDrag(Vector2 local, Vector2 delta) { }
    protected virtual void OnRelease(Vector2 local) { }

    // ---- Helpers ----

    // True (and the pet flinches) when the player is moving too fast.
    protected bool CheckRough(string line)
    {
        return Shaky(1f, line);
    }

    // Like CheckRough, for steps that need a steadier hand (share of the rough speed).
    protected bool Shaky(float share, string line)
    {
        if (Speed < RoughLimit * share)
            return false;
        if (Time.unscaledTime - lastFlinch > 0.8f)
        {
            lastFlinch = Time.unscaledTime;
            quality -= flinchCost;
            pet.Flinch(line);
        }
        return true;
    }

    protected Vector3 ToWorld(Vector2 local)
    {
        return area.TransformPoint(local);
    }

    protected Vector2 ToLocal(Vector3 world)
    {
        return area.InverseTransformPoint(world);
    }

    // A point on the pet in 0-1 coordinates, in touch-layer space.
    protected Vector2 PetLocal(float x, float y)
    {
        return ToLocal(pet.PetPoint(x, y));
    }

    protected bool OverPet(Vector2 local, float pad = 0f)
    {
        Vector2 min = PetLocal(0f, 0f), max = PetLocal(1f, 1f);
        return local.x >= Mathf.Min(min.x, max.x) - pad && local.x <= Mathf.Max(min.x, max.x) + pad
            && local.y >= Mathf.Min(min.y, max.y) - pad && local.y <= Mathf.Max(min.y, max.y) + pad;
    }

    // A small round mark stuck to the pet (foam, a tangle...). Removed when the activity ends.
    protected Image AddMarker(Vector2 local, Color color, float size)
    {
        var go = new GameObject("CareMarker", typeof(RectTransform));
        go.layer = pet.pet.gameObject.layer;
        go.transform.SetParent(pet.pet, false);
        var img = go.AddComponent<Image>();
        img.sprite = UIBurst.Instance != null && UIBurst.Instance.dot != null ? UIBurst.Instance.dot : Tool != null ? Tool.sprite : null;
        img.color = color;
        img.raycastTarget = false;
        img.rectTransform.sizeDelta = new Vector2(size, size);
        img.rectTransform.position = ToWorld(local);
        markers.Add(img);
        return img;
    }

    protected void RemoveMarker(Image marker)
    {
        markers.Remove(marker);
        if (marker != null)
            Destroy(marker.gameObject);
    }

    protected List<Image> Markers => markers;

    protected Vector2 MarkerLocal(Image marker)
    {
        return ToLocal(marker.rectTransform.position);
    }

    void MoveTool(Vector2 local)
    {
        if (Tool != null && ToolFollowsFinger)
            Tool.rectTransform.position = ToWorld(local + toolOffset);
    }

    void ReturnTool()
    {
        if (Tool != null)
            Tool.rectTransform.anchoredPosition = toolHome;
    }
}
