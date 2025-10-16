using Mirror;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TargetPrompt : MonoBehaviour
{
    public static TargetPrompt Instance;

    [Header("Root")]
    [SerializeField] GameObject panel;

    [Header("Targets (players)")]
    [SerializeField] Transform targetListRoot;
    [SerializeField] GameObject targetButtonPrefab;
    [SerializeField] TMP_Text selectTargetText;

    [Header("Guesses (cards)")]
    [SerializeField] Transform guessListRoot;
    [SerializeField] GameObject guessButtonPrefab;

    [Header("Footer")]
    [SerializeField] TMP_Text footerText;

    IReadOnlyList<uint> _ids;
    IReadOnlyList<string> _names;
    IReadOnlyList<CardType> _guesses;
    Action<uint> _onTarget;
    Action<uint, CardType> _onTargetAndGuess;

    uint _selectedTarget;
    CardType _selectedGuess;

    readonly List<Button> _guessBtns = new();

    void Awake() => Instance = this;

    static TargetPrompt Ensure()
    {
        if (Instance == null) Instance = FindFirstObjectByType<TargetPrompt>();
        if (Instance == null) Debug.LogError("[TargetPrompt] No instance found in scene.");
        return Instance;
    }

    static string LiveNameFor(uint netId)
    {
        if (NetworkClient.spawned != null &&
            NetworkClient.spawned.TryGetValue(netId, out var ni) &&
            ni != null)
        {
            var pn = ni.GetComponent<PlayerNetwork>();
            if (pn != null && !string.IsNullOrWhiteSpace(pn.PlayerName))
                return pn.PlayerName;
        }
        return null;
    }
    public static void ShowTargets(IReadOnlyList<uint> ids, IReadOnlyList<string> names, Action<uint> onTarget)
    {
        var i = Ensure(); if (i == null) return;
        i.InternalShow(ids, names, null, onTarget, null);
    }

    public static void ShowTargetsAndGuesses(IReadOnlyList<uint> ids, IReadOnlyList<string> names, IReadOnlyList<CardType> guesses, Action<uint, CardType> onBoth)
    {
        var i = Ensure(); if (i == null) return;
        i.InternalShow(ids, names, guesses, null, onBoth);
    }

    void InternalShow(IReadOnlyList<uint> ids,
                  IReadOnlyList<string> names,
                  IReadOnlyList<CardType> guesses,
                  Action<uint> onTarget,
                  Action<uint, CardType> onBoth)
    {
        _ids = ids ?? Array.Empty<uint>();
        _names = names ?? Array.Empty<string>();
        _guesses = guesses;
        _onTarget = onTarget;
        _onTargetAndGuess = onBoth;

        _selectedTarget = 0;
        _selectedGuess = CardType.None;

        bool hasGuesses = _guesses != null && _guesses.Count > 0;
        bool targetsOnly = !hasGuesses;
        int targetCount = _ids.Count;

        // 2-player QoL:
        // A) targets-only & exactly one target -> auto-confirm
        if (targetsOnly && targetCount == 1)
        {
            _onTarget?.Invoke(_ids[0]);
            Close();
            return;
        }

        // B) Guard with exactly one target -> preselect & hide the target list
        bool hideTargetList = false;
        if (hasGuesses && targetCount == 1)
        {
            _selectedTarget = _ids[0];
            hideTargetList = true;
        }

        RebuildTargets();
        RebuildGuesses();

        if (selectTargetText)
        {
            selectTargetText.gameObject.SetActive(true);
            selectTargetText.text = hideTargetList
                ? $"Target: {_names[0]}"
                : (targetsOnly ? "Choose a player" : "Select target");
        }

        if (footerText)
        {
            footerText.gameObject.SetActive(hasGuesses);
            if (hasGuesses)
                footerText.text = (_selectedTarget == 0) ? "Choose a player" : "Choose a card";
        }

        if (targetListRoot)
            targetListRoot.gameObject.SetActive(!hideTargetList && targetCount > 0);

        SetGuessButtonsInteractable(hasGuesses && _selectedTarget != 0);

        if (panel) panel.SetActive(true); else gameObject.SetActive(true);
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

                if (_onTargetAndGuess != null && _selectedGuess != 0)
                    ConfirmBoth();
                else if (_onTarget != null && (_guesses == null || _guesses.Count == 0))
                    ConfirmTarget();

                bool targetsOnly = (_guesses == null || _guesses.Count == 0);
                SetGuessButtonsInteractable(!targetsOnly && _selectedTarget != 0);

                if (footerText && !targetsOnly)
                    footerText.text = (_selectedTarget == 0) ? "Choose a player" : "Choose a card";
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
            if (ct == CardType.Guard || ct == CardType.None) continue;
            var go = Instantiate(guessButtonPrefab, guessListRoot);
            var btn = go.GetComponent<Button>();
            var txt = go.GetComponentInChildren<TMP_Text>();
            if (txt) txt.text = CardDB.Title.TryGetValue(ct, out var t) ? t : ct.ToString();
            _guessBtns.Add(btn);

            var guess = ct;
            btn.onClick.AddListener(() =>
            {
                _selectedGuess = guess;

                if (_selectedTarget != 0) ConfirmBoth();
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
            Destroy(root.GetChild(i).gameObject);
    }
}
