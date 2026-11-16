using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Mini-Game 2: Paw Match.
// Cards start face down. Tap two to flip them: a match stays face up and
// scores, a miss flips back. The round ends when every pair is found, with a
// bonus for finishing quickly.
public class PawMatchGame : MiniGame
{
    public int columns = 4;
    public int rows = 3;
    public Vector2 cardSize = new Vector2(200f, 230f);
    public float spacing = 30f;
    public int matchPoints = 20;
    [Tooltip("Bonus = this minus seconds taken (never below 0).")]
    public int timeBonus = 90;
    public float showMissFor = 0.7f;

    public override string Id => "match";
    public override string Title => "Paw Match";
    public override string Description => "Find all the matching pairs!";
    public override string HowToPlay => "Tap two cards to find a matching pair!";
    public override Sprite Icon => Theme.paw;
    public override Color CardColor => new Color(0.83f, 0.8f, 0.96f);
    public override string ResultTitle(bool timeUp) => "ALL PAIRS FOUND!";

    class Card
    {
        public int face;
        public RectTransform rt;
        public Image back, front;
        public bool up, matched;
    }

    readonly List<Card> cards = new List<Card>();
    Card first;
    bool busy;
    int pairsLeft;

    protected override void Build()
    {
        cards.Clear();
        first = null;
        busy = false;

        List<Sprite> faces = Faces();
        int pairs = Mathf.Min(columns * rows / 2, faces.Count);
        pairsLeft = pairs;

        var deck = new List<int>();
        for (int i = 0; i < pairs; i++)
        {
            deck.Add(i);
            deck.Add(i);
        }
        for (int i = deck.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (deck[i], deck[j]) = (deck[j], deck[i]);
        }

        RectTransform board = MiniGameUI.Rect("Board", area);
        MiniGameUI.Place(board, new Vector2(0.5f, 0.5f), new Vector2(0f, -50f), Vector2.zero);

        int cols = columns, rowCount = Mathf.CeilToInt(deck.Count / (float)cols);
        float w = cols * cardSize.x + (cols - 1) * spacing;
        float h = rowCount * cardSize.y + (rowCount - 1) * spacing;
        for (int i = 0; i < deck.Count; i++)
        {
            int c = i % cols, r = i / cols;
            Vector2 pos = new Vector2(-w * 0.5f + cardSize.x * 0.5f + c * (cardSize.x + spacing),
                h * 0.5f - cardSize.y * 0.5f - r * (cardSize.y + spacing));
            cards.Add(MakeCard(board, deck[i], faces[deck[i]], pos, i));
        }
    }

    List<Sprite> Faces()
    {
        var list = new List<Sprite>();
        void Add(Sprite s)
        {
            if (s != null && !list.Contains(s))
                list.Add(s);
        }
        Add(hub.PetSprite);
        Add(hub.OtherPet(!hub.PetIsCat));   // a dog if you have a cat, and the other way round
        Add(hub.OtherPet(hub.PetIsCat));
        Add(Theme.bowl);
        Add(Theme.ball);
        Add(Theme.medicine);
        Add(Theme.bone);
        Add(Theme.comb);
        Add(Theme.heart);
        return list;
    }

    Card MakeCard(RectTransform board, int face, Sprite sprite, Vector2 pos, int index)
    {
        Image frame = MiniGameUI.Panel(board, "Card" + index, Theme.cream, 5f);
        RectTransform rt = MiniGameUI.Place(frame.rectTransform, new Vector2(0.5f, 0.5f), pos, cardSize);

        Image back = MiniGameUI.Image(rt, "Back", Theme.panel, new Color(0.62f, 0.55f, 0.9f));
        MiniGameUI.Stretch(back.rectTransform, 12f);
        Image backPaw = MiniGameUI.Image(back.transform, "Paw", Theme.paw, new Color(1f, 1f, 1f, 0.85f));
        MiniGameUI.Place(backPaw.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, cardSize * 0.45f);

        Image front = MiniGameUI.Image(rt, "Front", sprite, Color.white);
        MiniGameUI.Stretch(front.rectTransform, 22f);
        front.gameObject.SetActive(false);

        var card = new Card { face = face, rt = rt, back = back, front = front };
        MiniGamePointer tap = frame.gameObject.AddComponent<MiniGamePointer>();
        tap.down = _ => Tap(card);
        return card;
    }

    void Tap(Card card)
    {
        if (!Running || busy || card.up || card.matched)
            return;
        SFXManager.Play(Sfx.Pop);
        StartCoroutine(Flip(card, true));

        if (first == null)
        {
            first = card;
            return;
        }
        Card a = first;
        first = null;
        StartCoroutine(Check(a, card));
    }

    IEnumerator Check(Card a, Card b)
    {
        busy = true;
        yield return new WaitForSeconds(0.3f);
        if (a.face == b.face)
        {
            a.matched = b.matched = true;
            AddScore(matchPoints, b.rt.position);
            SFXManager.Play(Sfx.HappyChirp);
            if (UIBurst.Instance != null)
            {
                UIBurst.Instance.Burst(a.rt.position, UIBurst.Instance.paw, 6, UIBurst.Rise, Color.white);
                UIBurst.Instance.Burst(b.rt.position, UIBurst.Instance.paw, 6, UIBurst.Rise, Color.white);
            }
            a.rt.GetComponent<Image>().color = Theme.good;
            b.rt.GetComponent<Image>().color = Theme.good;
            busy = false;

            if (--pairsLeft <= 0)
            {
                int bonus = Mathf.Max(0, timeBonus - Mathf.FloorToInt(Elapsed));
                yield return new WaitForSeconds(0.4f);
                if (bonus > 0)
                {
                    AddScore(bonus, area.position);
                    SFXManager.Play(Sfx.Coin);
                }
                yield return new WaitForSeconds(0.6f);
                EndGame();
            }
        }
        else
        {
            yield return new WaitForSeconds(showMissFor);
            StartCoroutine(Flip(a, false));
            yield return Flip(b, false);
            busy = false;
        }
    }

    IEnumerator Flip(Card card, bool up)
    {
        card.up = up;
        const float half = 0.12f;
        if (!ReduceMotion.On)
        {
            for (float t = 0f; t < half; t += Time.deltaTime)
            {
                card.rt.localScale = new Vector3(1f - Ease(t / half), 1f, 1f);
                yield return null;
            }
        }
        card.front.gameObject.SetActive(up);
        card.back.gameObject.SetActive(!up);
        if (!ReduceMotion.On)
        {
            for (float t = 0f; t < half; t += Time.deltaTime)
            {
                card.rt.localScale = new Vector3(Ease(t / half), 1f, 1f);
                yield return null;
            }
        }
        card.rt.localScale = Vector3.one;
    }
}
