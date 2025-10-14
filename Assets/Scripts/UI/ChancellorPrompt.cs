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

    Action<CardType> onPick;

    void Awake() => Instance = this;

    // called by GameController via TargetChancellorChoice
    public static void Show(CardType[] options, Action<CardType> onKeep)
    {
        var inst = Instance;
        if (!inst) return;

        inst.gameObject.SetActive(true);

        // clear old
        foreach (Transform child in inst.cardRoot)
            Destroy(child.gameObject);

        foreach (var card in options)
        {
            var go = Instantiate(inst.cardButtonPrefab, inst.cardRoot);
            var ui = go.GetComponent<CardButtonUI>();

            // this is the missing piece: assign sprite from CardDB
            ui.Setup(CardDB.Sprite(card), () =>
            {
                onKeep?.Invoke(card);
                inst.gameObject.SetActive(false);
            }, true);
        }
    }


    void InternalShow(CardType[] options, Action<CardType> onPick)
    {
        this.onPick = onPick;
        ClearChildren();

        foreach (var ct in options)
        {
            var go = Instantiate(optionButtonPrefab, optionsRoot);
            var img = go.GetComponentInChildren<Image>();
            if (img) img.sprite = CardDB.Sprite(ct);

            var btn = go.GetComponent<Button>();
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => Pick(ct));
        }

        panel.SetActive(true);
    }

    void Pick(CardType ct)
    {
        panel.SetActive(false);
        ClearChildren();
        onPick?.Invoke(ct);
        onPick = null;
    }

    void ClearChildren()
    {
        for (int i = optionsRoot.childCount - 1; i >= 0; i--)
            Destroy(optionsRoot.GetChild(i).gameObject);
    }
}
