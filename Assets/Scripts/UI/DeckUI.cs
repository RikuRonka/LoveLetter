using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DeckUI : MonoBehaviour
{
    public static DeckUI I;

    [SerializeField] Image deckImage;       // card back image (TopBar/DeckImage)
    [SerializeField] TMP_Text deckCountText; // right of the card
    [SerializeField] TMP_Text burnedText;    // "Burned: N"
    [SerializeField] RectTransform deckAnchor; // for draw animations (optional)

    void Awake() => I = this;

    public void Render(int deckCount, int burned)
    {
        if (burnedText) burnedText.text = $"Burned: {burned}";
        if (deckCountText) deckCountText.text = deckCount.ToString();
        if (deckImage) deckImage.enabled = deckCount > 0;
    }

    public RectTransform Anchor => deckAnchor ? deckAnchor : (RectTransform)transform;
}
