using TMPro;
using UnityEngine;
using UnityEngine.UI;

// One item card in the Shop. ShopUI copies a template slot and fills it in.
public class ShopSlot : MonoBehaviour
{
    public Image icon;
    public TMP_Text title;
    public TMP_Text blurb;
    public Button button;
    public Image buttonFill;
    public TMP_Text buttonText;
    [Tooltip("Coin shown in front of the price.")]
    public Image coin;
    [Tooltip("Lock shown over the icon while the item needs a higher Bond.")]
    public Image lockIcon;
    [Tooltip("Tick shown when the item is worn or in use.")]
    public Image tick;
}
