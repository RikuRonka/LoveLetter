using System;
using UnityEngine;
using UnityEngine.UI;

public class ChancellorPrompt : MonoBehaviour
{
    public static ChancellorPrompt Instance;

    [Header("UI")]
    [SerializeField] GameObject panel;                 // root to toggle on/off
    [SerializeField] Transform optionsRoot;            // where the 3 options go (Layout Group)
    [SerializeField] GameObject optionButtonPrefab;    // usually your CardButton prefab
    [SerializeField] Transform cardRoot;               // optional: show kept card here
    [SerializeField] GameObject cardButtonPrefab;      // usually your CardButton prefab

    Action<CardType> _onKeep;

    void Awake() => Instance = this;

    void Reset()
    {
        if (!panel && transform.childCount > 0) panel = transform.GetChild(0).gameObject;
        // try to auto-find common names to reduce wiring mistakes
        if (!optionsRoot) optionsRoot = transform.Find("Panel/OptionsRoot");
        if (!cardRoot) cardRoot = transform.Find("Panel/CardRoot");
    }

    /// <summary>
    /// Opens the Chancellor choice UI with 3 cards, and invokes onKeep with the chosen card.
    /// </summary>
    public static void Show(CardType[] options, Action<CardType> onKeep)
    {
        var i = Instance ?? FindObjectOfType<ChancellorPrompt>(true);
        if (i == null) { Debug.LogError("[ChancellorPrompt] No instance in scene."); return; }
        i._onKeep = onKeep;
        i.Build(options);
    }

    void Build(CardType[] options)
    {
        if (panel) panel.SetActive(true); else gameObject.SetActive(true);

        Clear(optionsRoot);
        Clear(cardRoot);

        if (options == null || options.Length == 0)
        {
            Debug.LogWarning("[ChancellorPrompt] No options provided.");
            Close();
            return;
        }

        foreach (var card in options)
        {
            var prefab = optionButtonPrefab ? optionButtonPrefab : cardButtonPrefab;
            var go = Instantiate(prefab, optionsRoot);

            // Prefer CardButtonUI if present (your hover/highlight logic)
            var ui = go.GetComponent<CardButtonUI>();
            if (ui != null)
            {
                ui.Setup(CardDB.Sprite(card), () => Choose(card), true);
            }
            else
            {
                // Fallback if prefab doesn’t have CardButtonUI
                var img = go.GetComponentInChildren<Image>(true);
                if (img) img.sprite = CardDB.Sprite(card);

                var btn = go.GetComponent<Button>();
                if (btn) btn.onClick.AddListener(() => Choose(card));
            }
        }
    }

    void Choose(CardType keep)
    {
        // notify game logic first
        _onKeep?.Invoke(keep);

        // (Optional) show the kept card in CardRoot briefly
        if (cardRoot && cardButtonPrefab)
        {
            Clear(cardRoot);
            var go = Instantiate(cardButtonPrefab, cardRoot);
            var ui = go.GetComponent<CardButtonUI>();
            if (ui != null)
            {
                ui.Setup(CardDB.Sprite(keep), null, false);  // not playable
            }
            else
            {
                var img = go.GetComponentInChildren<Image>(true);
                if (img) img.sprite = CardDB.Sprite(keep);
                var btn = go.GetComponent<Button>();
                if (btn) btn.interactable = false;
            }
        }

        // close immediately (you can delay if you want the kept card to linger)
        Close();
    }

    public void Close()
    {
        if (panel) panel.SetActive(false); else gameObject.SetActive(false);
    }

    static void Clear(Transform root)
    {
        if (!root) return;
        for (int i = root.childCount - 1; i >= 0; i--)
            Destroy(root.GetChild(i).gameObject);
    }
}
