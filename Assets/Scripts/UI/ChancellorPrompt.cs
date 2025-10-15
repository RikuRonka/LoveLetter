using System;
using UnityEngine;
using UnityEngine.UI;

public class ChancellorPrompt : MonoBehaviour
{
    public static ChancellorPrompt Instance;

    [Header("UI")]
    [SerializeField] GameObject panel;
    [SerializeField] Transform optionsRoot;
    [SerializeField] GameObject optionButtonPrefab;
    [SerializeField] Transform cardRoot;
    [SerializeField] GameObject cardButtonPrefab;

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
        var i = Instance ?? FindFirstObjectByType<ChancellorPrompt>();
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

            var ui = go.GetComponent<CardButtonUI>();
            if (ui != null)
            {
                ui.Setup(CardDB.Sprite(card), () => Choose(card), true);
            }
            else
            {
                var img = go.GetComponentInChildren<Image>(true);
                if (img) img.sprite = CardDB.Sprite(card);

                var btn = go.GetComponent<Button>();
                if (btn) btn.onClick.AddListener(() => Choose(card));
            }
        }
    }

    void Choose(CardType keep)
    {
        _onKeep?.Invoke(keep);

        if (cardRoot && cardButtonPrefab)
        {
            Clear(cardRoot);
            var go = Instantiate(cardButtonPrefab, cardRoot);
            var ui = go.GetComponent<CardButtonUI>();
            if (ui != null)
            {
                ui.Setup(CardDB.Sprite(keep), null, false);
            }
            else
            {
                var img = go.GetComponentInChildren<Image>(true);
                if (img) img.sprite = CardDB.Sprite(keep);
                var btn = go.GetComponent<Button>();
                if (btn) btn.interactable = false;
            }
        }

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
