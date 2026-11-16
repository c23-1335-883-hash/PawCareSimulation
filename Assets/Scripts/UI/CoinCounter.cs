using TMPro;
using UnityEngine;

// The coin pill at the top of the home screen. Earned coins fly from where they
// were earned into the pill; the number counts up as they land and the coin
// icon gives a little pop. Spending counts the number down. Tapping it opens the Shop.
public class CoinCounter : MonoBehaviour
{
    public static CoinCounter Instance { get; private set; }

    public TMP_Text amountText;
    [Tooltip("Coins fly to this icon.")]
    public RectTransform icon;
    public Sprite coinSprite;
    [Tooltip("Seconds the number takes to count to a new value.")]
    public float countTime = 0.4f;
    public float coinSize = 46f;

    float shown;
    int inFlight;
    float bump;

    void Awake()
    {
        Instance = this;
        shown = Wallet.Coins;
        Refresh();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // Adds coins to the wallet and, if there is a counter on screen, flies them in.
    public static void Give(int amount, Vector3 fromWorld)
    {
        if (amount <= 0)
            return;
        Wallet.Add(amount);
        if (Instance != null)
            Instance.FlyIn(amount, fromWorld);
    }

    void FlyIn(int amount, Vector3 fromWorld)
    {
        UIBurst fx = UIBurst.Instance;
        if (fx == null || ReduceMotion.On || coinSprite == null)
        {
            SFXManager.Play(Sfx.Coin);
            Bump();
            return;
        }

        // A few coins, each carrying part of the amount.
        int pieces = Mathf.Clamp(amount / 5, 3, 8);
        int left = amount;
        inFlight += amount;
        for (int i = 0; i < pieces; i++)
        {
            int share = i == pieces - 1 ? left : amount / pieces;
            left -= share;
            Vector3 from = fromWorld + (Vector3)(Random.insideUnitCircle * 30f * transform.lossyScale.x);
            fx.Fly(from, icon.position, coinSprite, Color.white, coinSize, 0.5f + i * 0.07f, () =>
            {
                inFlight -= share;
                SFXManager.Play(Sfx.Coin, 0.6f);
                Bump();
            });
        }
    }

    void Bump()
    {
        bump = 1f;
    }

    void Update()
    {
        float target = Wallet.Coins - Mathf.Max(0, inFlight);
        if (!Mathf.Approximately(shown, target))
        {
            float speed = Mathf.Max(20f, Mathf.Abs(target - shown) / Mathf.Max(0.05f, countTime));
            shown = Mathf.MoveTowards(shown, target, speed * Time.deltaTime);
            Refresh();
        }

        if (bump > 0f)
        {
            bump = Mathf.Max(0f, bump - Time.deltaTime * 5f);
            // The icon pops (the pill itself is scaled by its ButtonJuice).
            float s = ReduceMotion.On ? 1f : 1f + Mathf.Sin(bump * Mathf.PI) * 0.3f;
            icon.localScale = new Vector3(s, s, 1f);
        }
    }

    void Refresh()
    {
        if (amountText != null)
            amountText.text = Mathf.RoundToInt(shown).ToString();
    }
}
