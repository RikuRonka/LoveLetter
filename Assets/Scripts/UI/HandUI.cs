using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HandUI : MonoBehaviour
{
    public static HandUI Instance;
    [SerializeField] Transform handRoot;
    [SerializeField] GameObject cardButtonPrefab;

    readonly List<CardType> localHand = new();
    bool myTurn; bool mustCountess;


    void Awake() => Instance = this;

    public void AddCard(CardType c) { localHand.Add(c); Refresh(); }
    public void ReplaceHand(List<CardType> all) { localHand.Clear(); localHand.AddRange(all); Refresh(); }
    public void BeginTurn(bool mustPlayCountess) { myTurn = true; mustCountess = mustPlayCountess; Refresh(); }

    void Refresh()
    {


        foreach (Transform t in handRoot) Destroy(t.gameObject);

        for (int i = 0; i < localHand.Count; i++)
        {
            var c = localHand[i];
            var go = Instantiate(cardButtonPrefab, handRoot);

            var view = go.GetComponent<CardButtonUI>();

            bool playable = myTurn && (!mustCountess || c == CardType.Countess);
            if (!view)
            {
                Debug.LogError("cardButtonPrefab must have CardButtonUI.", go);
                continue;
            }

            view.Setup(CardDB.Sprite(c), () =>
            {
                if (!myTurn) return;
                if (mustCountess && c != CardType.Countess) return;

                PlayerActions.Local?.PlayCard(c, 0, 0);
                myTurn = false;
                Refresh();
            }, playable);

            var btn = go.GetComponent<Button>();
            if (btn) btn.interactable = playable;
        }
    }

    public void ShowPriestPeek(string name, CardType c)
    {
        BoardUI.Instance?.Log($"(Private) {name} has {c}");
        PeekPrompt.Show(name, c);
    }

    public void EndTurn()
    {
        myTurn = false;
        Refresh();
    }
}
