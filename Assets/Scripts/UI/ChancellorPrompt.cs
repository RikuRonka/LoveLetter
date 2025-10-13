using System;
using UnityEngine;
using UnityEngine.UI;

public class ChancellorPrompt : MonoBehaviour
{
    public static ChancellorPrompt Instance;

    [Header("UI")]
    [SerializeField] GameObject panel;                 // root to show/hide
    [SerializeField] Transform optionsRoot;            // where buttons are spawned
    [SerializeField] GameObject optionButtonPrefab;    // a simple Button with Image (can reuse your cardButtonPrefab)

    Action<CardType> onPick;

    void Awake() => Instance = this;

    // called by GameController via TargetChancellorChoice
    public static void Show(CardType[] options, Action<CardType> onPick)
    {
        if (Instance == null) return;
        Instance.InternalShow(options, onPick);
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
