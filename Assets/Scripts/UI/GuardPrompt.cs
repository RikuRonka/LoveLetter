using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GuardPrompt : MonoBehaviour
{
    public static GuardPrompt Instance;

    [Header("Root")]
    [SerializeField] GameObject panel;                 // set active to open/close

    [Header("Targets")]
    [SerializeField] Transform targetListRoot;         // where target buttons go
    [SerializeField] GameObject targetButtonPrefab;    // Button with TMP_Text child

    [Header("Guesses")]
    [SerializeField] Transform guessListRoot;          // where guess buttons go
    [SerializeField] GameObject guessButtonPrefab;     // Button with TMP_Text child

    [Header("Controls")]
    [SerializeField] TMP_Text footerText;              // optional “Pick target + card”

    // working state
    IReadOnlyList<uint> _targetIds;
    IReadOnlyList<string> _targetNames;
    IReadOnlyList<CardType> _guessOptions;
    Action<uint, CardType> _onConfirm;

    uint _selectedTarget;
    CardType _selectedGuess;
    List<Button> _guessBtns = new();
    void Awake() => Instance = this;

    public static void Show(
        IReadOnlyList<uint> targetIds,
        IReadOnlyList<string> targetNames,
        IReadOnlyList<CardType> guessOptions,
        Action<uint, CardType> onConfirm)
    {
        if (Instance == null) return;
        Instance.InternalShow(targetIds, targetNames, guessOptions, onConfirm);
    }

    void InternalShow(
        IReadOnlyList<uint> targetIds,
        IReadOnlyList<string> targetNames,
        IReadOnlyList<CardType> guessOptions,
        Action<uint, CardType> onConfirm)
    {
        _targetIds = targetIds;
        _targetNames = targetNames;
        _guessOptions = guessOptions;
        _onConfirm = onConfirm;

        _selectedTarget = 0;
        _selectedGuess = 0;

        RebuildTargetButtons();
        RebuildGuessButtons();

        if (footerText) footerText.text = "Pick a target and a card to guess";
        if (panel) panel.SetActive(true);
        else gameObject.SetActive(true);
    }

    void Close()
    {
        if (panel) panel.SetActive(false);
        else gameObject.SetActive(false);
    }

    void RebuildTargetButtons()
    {
        ClearChildren(targetListRoot);
        for (int i = 0; i < _targetIds.Count; i++)
        {
            var go = Instantiate(targetButtonPrefab, targetListRoot);
            var btn = go.GetComponent<Button>();
            var txt = go.GetComponentInChildren<TMPro.TMP_Text>();
            if (txt) txt.text = _targetNames[i];
            uint id = _targetIds[i];
            btn.onClick.AddListener(() =>
            {
                _selectedTarget = id;
                SetGuessButtonsInteractable(true);
                if (_selectedGuess != 0) OnConfirm();
            });
        }
        // start with guesses disabled until target chosen
        SetGuessButtonsInteractable(_selectedTarget != 0);
    }

    void RebuildGuessButtons()
    {
        ClearChildren(guessListRoot);
        _guessBtns.Clear();

        foreach (var ct in _guessOptions)
        {
            if (ct == CardType.Guard || ct == 0) continue;
            var go = Instantiate(guessButtonPrefab, guessListRoot);
            var btn = go.GetComponent<Button>();
            var txt = go.GetComponentInChildren<TMPro.TMP_Text>();
            if (txt) txt.text = CardDB.Title[ct];

            var guess = ct;
            btn.onClick.AddListener(() =>
            {
                _selectedGuess = guess;
                if (_selectedTarget != 0) OnConfirm();  // auto confirm when target already chosen
            });
            _guessBtns.Add(btn);
        }
        SetGuessButtonsInteractable(_selectedTarget != 0);
    }

    void SetGuessButtonsInteractable(bool on)
    {
        foreach (var b in _guessBtns)
        {
            if (!b) continue;
            b.interactable = on;
            var cg = b.GetComponent<CanvasGroup>() ?? b.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = on ? 1f : 0.5f;
            cg.blocksRaycasts = on;
        }
    }

    void OnConfirm()
    {
        if (_selectedTarget != 0 && _selectedGuess != 0)
            _onConfirm?.Invoke(_selectedTarget, _selectedGuess);

        Close();
    }

    static void ClearChildren(Transform root)
    {
        if (!root) return;
        for (int i = root.childCount - 1; i >= 0; i--)
            GameObject.Destroy(root.GetChild(i).gameObject);
    }
}
