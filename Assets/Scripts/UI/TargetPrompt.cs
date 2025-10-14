using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TargetPrompt : MonoBehaviour
{
    public static TargetPrompt Instance;

    [Header("Root")]
    [SerializeField] GameObject panel;                 // toggle this on/off

    [Header("Targets (players)")]
    [SerializeField] Transform targetListRoot;         // Vertical/Horizontal/Grid Layout
    [SerializeField] GameObject targetButtonPrefab;    // Button (TMP_Text child)
    [SerializeField] TMP_Text selectTargetText;        // optional "Select a player"

    [Header("Guesses (cards)")]
    [SerializeField] Transform guessListRoot;          // Layout root for guesses
    [SerializeField] GameObject guessButtonPrefab;     // Button (TMP_Text child)

    [Header("Footer")]
    [SerializeField] TMP_Text footerText;              // optional hint/status

    // state
    IReadOnlyList<uint> _ids;
    IReadOnlyList<string> _names;
    IReadOnlyList<CardType> _guesses;
    Action<uint> _onTarget;                    // targets-only callback
    Action<uint, CardType> _onTargetAndGuess;  // targets+guesses callback

    uint _selectedTarget;
    CardType _selectedGuess;

    readonly List<Button> _guessBtns = new();

    void Awake() => Instance = this;

    static TargetPrompt Ensure()
    {
        if (Instance == null) Instance = FindObjectOfType<TargetPrompt>(true);
        if (Instance == null) Debug.LogError("[TargetPrompt] No instance found in scene.");
        return Instance;
    }

    // ===== Public API =====

    // Targets only (Priest / Baron / King / Prince)
    public static void ShowTargets(IReadOnlyList<uint> ids, IReadOnlyList<string> names, Action<uint> onTarget)
    {
        var i = Ensure(); if (i == null) return;
        i.InternalShow(ids, names, null, onTarget, null);
    }

    // Targets + guesses (Guard)
    public static void ShowTargetsAndGuesses(IReadOnlyList<uint> ids, IReadOnlyList<string> names, IReadOnlyList<CardType> guesses, Action<uint, CardType> onBoth)
    {
        var i = Ensure(); if (i == null) return;
        i.InternalShow(ids, names, guesses, null, onBoth);
    }

    // (If you ever need guesses only, you can add a ShowGuesses(..., Action<CardType>) overload.)

    // ===== Implementation =====
    void InternalShow(
        IReadOnlyList<uint> ids,
        IReadOnlyList<string> names,
        IReadOnlyList<CardType> guesses,
        Action<uint> onTarget,
        Action<uint, CardType> onBoth)
    {
        _ids = ids;
        _names = names;
        _guesses = guesses;
        _onTarget = onTarget;
        _onTargetAndGuess = onBoth;

        _selectedTarget = 0;
        _selectedGuess = 0;

        // build UI
        RebuildTargets();
        RebuildGuesses();

        // visibility & hints
        if (targetListRoot) targetListRoot.gameObject.SetActive(_ids != null && _ids.Count > 0);
        if (guessListRoot) guessListRoot.gameObject.SetActive(_guesses != null && _guesses.Count > 0);
        if (selectTargetText) selectTargetText.gameObject.SetActive(_ids != null && _ids.Count > 0);
        if (footerText)
        {
            if (_guesses == null || _guesses.Count == 0) footerText.text = "Choose a player";
            else footerText.text = (_selectedTarget == 0) ? "Choose a player" : "Choose a card";
        }

        // show
        if (panel) panel.SetActive(true); else gameObject.SetActive(true);

        // For Guard flow: guesses disabled until a target chosen (but allow reverse order by remembering choice)
        SetGuessButtonsInteractable(_selectedTarget != 0 || (_guesses == null || _guesses.Count == 0));
    }

    void RebuildTargets()
    {
        Clear(targetListRoot);
        if (_ids == null || targetListRoot == null || targetButtonPrefab == null) return;

        for (int i = 0; i < _ids.Count; i++)
        {
            var go = Instantiate(targetButtonPrefab, targetListRoot);
            var btn = go.GetComponent<Button>();
            var txt = go.GetComponentInChildren<TMP_Text>();
            if (txt) txt.text = _names[i];

            uint id = _ids[i];
            btn.onClick.AddListener(() =>
            {
                _selectedTarget = id;

                // if targets+guesses AND a guess is already chosen -> auto confirm
                if (_onTargetAndGuess != null && _selectedGuess != 0)
                    ConfirmBoth();
                else if (_onTarget != null && (_guesses == null || _guesses.Count == 0))
                    ConfirmTarget();

                // enable guesses after target picked (Guard UX)
                SetGuessButtonsInteractable(true);
                if (footerText) footerText.text = (_guesses == null || _guesses.Count == 0) ? "Chosen" : "Choose a card";
            });
        }
    }

    void RebuildGuesses()
    {
        Clear(guessListRoot);
        _guessBtns.Clear();

        if (_guesses == null || _guesses.Count == 0 || guessListRoot == null || guessButtonPrefab == null)
            return;

        foreach (var ct in _guesses)
        {
            if (ct == CardType.Guard || ct == 0) continue; // safety
            var go = Instantiate(guessButtonPrefab, guessListRoot);
            var btn = go.GetComponent<Button>();
            var txt = go.GetComponentInChildren<TMP_Text>();
            if (txt) txt.text = CardDB.Title.TryGetValue(ct, out var t) ? t : ct.ToString();
            _guessBtns.Add(btn);

            var guess = ct;
            btn.onClick.AddListener(() =>
            {
                _selectedGuess = guess;

                if (_selectedTarget != 0) ConfirmBoth(); // auto confirm once both chosen
                else if (footerText) footerText.text = "Choose a player";
            });
        }
    }

    void ConfirmTarget()
    {
        _onTarget?.Invoke(_selectedTarget);
        Close();
    }

    void ConfirmBoth()
    {
        _onTargetAndGuess?.Invoke(_selectedTarget, _selectedGuess);
        Close();
    }

    void Close()
    {
        if (panel) panel.SetActive(false);
        else gameObject.SetActive(false);
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
            cg.interactable = on;
        }
    }

    static void Clear(Transform root)
    {
        if (!root) return;
        for (int i = root.childCount - 1; i >= 0; i--)
            GameObject.Destroy(root.GetChild(i).gameObject);
    }
}
