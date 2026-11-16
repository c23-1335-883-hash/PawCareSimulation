using System.Collections;
using UnityEngine;

// Fetch: grab the ball and flick it toward the pet. A good throw lands near
// its head and it jumps to catch it; a weak or wild throw misses, and a
// throw that's far too hard startles it.
public class PlayInteraction : CareInteraction
{
    public int catches = 3;
    [Tooltip("How close to the ball a press must be to grab it.")]
    public float grabRadius = 150f;
    [Tooltip("Slower flicks just drop the ball.")]
    public float minFlick = 500f;
    [Tooltip("Faster flicks are too hard and startle the pet.")]
    public float tooHard = 5000f;
    [Tooltip("Seconds of flick speed that become throw distance.")]
    public float flickReach = 0.3f;
    public float maxThrow = 1100f;
    public float catchRadius = 200f;
    public float missCost = 0.1f;

    public override CareActivity? Activity => CareActivity.Play;
    public override string Prompt => "Grab the ball and flick it toward your pet!";

    RectTransform ball;
    Vector2 homeLocal;
    Vector2 homeAnchored;
    bool grabbed;
    int caught;
    Coroutine flight;

    protected override void OnBegin()
    {
        ball = care.ball;
        caught = 0;
        grabbed = false;
        flight = null;
        if (ball != null)
        {
            homeAnchored = ball.anchoredPosition;
            homeLocal = ToLocal(ball.position);
        }
    }

    protected override void OnEnd()
    {
        StopAllCoroutines();
        flight = null;
        if (ball != null)
        {
            ball.anchoredPosition = homeAnchored;
            ball.localRotation = Quaternion.identity;
        }
    }

    protected override void OnPress(Vector2 local)
    {
        grabbed = ball != null && flight == null && (ToLocal(ball.position) - local).magnitude < grabRadius;
    }

    protected override void OnDrag(Vector2 local, Vector2 delta)
    {
        if (grabbed && flight == null)
            ball.position = ToWorld(local);
    }

    protected override void OnRelease(Vector2 local)
    {
        if (!grabbed || flight != null)
            return;
        grabbed = false;

        Vector2 flick = Velocity;
        if (flick.magnitude < minFlick)
        {
            care.ShowPrompt("Flick it! Swipe toward your pet and let go.");
            flight = StartCoroutine(BackHome(local, 0.4f));
            return;
        }

        Vector2 target = local + Vector2.ClampMagnitude(flick * flickReach, maxThrow);
        Vector2 head = PetLocal(0.5f, 0.9f);
        bool wild = flick.magnitude > tooHard;
        bool catchIt = !wild && (target - head).magnitude < catchRadius;
        flight = StartCoroutine(Throw(local, catchIt ? head : target, catchIt, wild));
    }

    IEnumerator Throw(Vector2 from, Vector2 to, bool catchIt, bool wild)
    {
        bool jumped = false;
        yield return Fly(from, to, 0.45f, 180f, k =>
        {
            if (catchIt && !jumped && k > 0.55f)
            {
                jumped = true;
                pet.ActivityStep();    // jump for it
            }
        });

        if (catchIt)
        {
            caught++;
            if (caught >= catches)
            {
                Finish();
                yield break;
            }
            pet.Say(caught == catches - 1 ? "One more! One more!" : "Got it!");
            care.ShowPrompt("Great throw! " + caught + "/" + catches + " catches.");
            yield return Fly(to, homeLocal, 0.45f, 120f, null);
        }
        else
        {
            quality -= missCost;
            pet.Watch(ToWorld(to));
            if (wild)
                pet.Flinch("Whoa, too hard!");
            else
                pet.Say(Random.value < 0.5f ? "Aww, missed it!" : "Hey, over here!");
            yield return new WaitForSeconds(0.3f);
            yield return Fly(to, homeLocal, 0.6f, 60f, null);
            pet.Watch(null);
        }
        ball.anchoredPosition = homeAnchored;
        flight = null;
    }

    IEnumerator BackHome(Vector2 from, float time)
    {
        yield return Fly(from, homeLocal, time, 40f, null);
        ball.anchoredPosition = homeAnchored;
        flight = null;
    }

    IEnumerator Fly(Vector2 from, Vector2 to, float time, float arc, System.Action<float> onStep)
    {
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            float k = t / time;
            ball.position = ToWorld(Vector2.Lerp(from, to, k) + new Vector2(0f, Mathf.Sin(k * Mathf.PI) * arc));
            ball.localRotation = Quaternion.Euler(0f, 0f, -360f * k);
            onStep?.Invoke(k);
            yield return null;
        }
        ball.position = ToWorld(to);
        ball.localRotation = Quaternion.identity;
    }
}
