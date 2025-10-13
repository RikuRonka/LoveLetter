using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HandUI : MonoBehaviour
{
    public static HandUI Instance;

    [SerializeField] Transform handRoot;            // container with HorizontalLayoutGroup
    [SerializeField] GameObject cardButtonPrefab;   // has Button + CardButtonUI (with cardImage assigned)

    readonly List<CardType> localHand = new();
    bool myTurn;
    bool mustCountess;

    void Awake() => Instance = this;

    // ---- called from GameController TargetRPCs ----
    public void AddCard(CardType c)
    {
        localHand.Add(c);
        Refresh();
    }

    public void ReplaceHand(List<CardType> all)
    {
        localHand.Clear();
        localHand.AddRange(all);
        Refresh();
    }

    public void BeginTurn(bool mustPlayCountess)
    {
        myTurn = true;
        mustCountess = mustPlayCountess;
        Refresh();
    }

    // ---- UI build ----
    void Refresh()
    {
        if (!handRoot) return;

        // clear old
        for (int i = handRoot.childCount - 1; i >= 0; i--)
            Destroy(handRoot.GetChild(i).gameObject);

        // rebuild
        for (int i = 0; i < localHand.Count; i++)
        {
            var ctype = localHand[i];
            var go = Instantiate(cardButtonPrefab, handRoot);

            // set art
            var cardUI = go.GetComponent<CardButtonUI>();
            if (cardUI != null)
                cardUI.Setup(CardDB.Sprite(ctype), () => OnCardClicked(i));
            else
            {
                // fallback: set the Button's own image if CardButtonUI not present
                var img = go.GetComponent<Image>();
                if (img) img.sprite = CardDB.Sprite(ctype);
                var btn = go.GetComponent<Button>();
                if (btn)
                {
                    int idx = i;
                    btn.onClick.AddListener(() => OnCardClicked(idx));
                }
            }

            // interactable rules
            var button = go.GetComponent<Button>();
            if (button)
                button.interactable = myTurn && (!mustCountess || ctype == CardType.Countess);
        }
    }

    // ---- click handlers ----
    void OnCardClicked(int idx)
    {
        if (!myTurn || idx < 0 || idx >= localHand.Count) return;

        var card = localHand[idx];

        // Route special input flows
        if (card == CardType.Priest)
        {
            var target = TargetPicker.FirstOtherAliveNetId();
            if (target != 0) PlayerActions.Local?.PlayCard(card, target);
        }
        else if (card == CardType.Guard)
        {
            SimpleGuardPrompt.Show(guess =>
            {
                var target = TargetPicker.FirstOtherAliveNetId();
                if (target != 0) PlayerActions.Local?.PlayCard(CardType.Guard, target, guess);
            });
        }
        else
        {
            // TODO: real target picker where needed (Baron/Prince/King/Chancellor, etc.)
            PlayerActions.Local?.PlayCard(card, 0, 0);
        }

        // block double-click spam until server advances turn
        myTurn = false;
        Refresh();
    }

    // private info (Priest)
    public void ShowPriestPeek(string name, CardType c)
    {
        BoardUI.Instance?.Log($"(Private) {name} has {c}");
    }
}
