using System;
using UnityEngine;
using UnityEngine.UI;

public class SimpleGuardPrompt : MonoBehaviour
{
    public static SimpleGuardPrompt Instance;
    [SerializeField] GameObject panel;
    [SerializeField] Button priestBtn, baronBtn, handmaidBtn, princeBtn, kingBtn, countessBtn, princessBtn, spyBtn;

    Action<CardType> onChoose;

    void Awake()
    {
        Instance = this;
        panel.SetActive(false);
        spyBtn.onClick.AddListener(() => Choose(CardType.Spy));
        priestBtn.onClick.AddListener(() => Choose(CardType.Priest));
        baronBtn.onClick.AddListener(() => Choose(CardType.Baron));
        handmaidBtn.onClick.AddListener(() => Choose(CardType.Handmaid));
        princeBtn.onClick.AddListener(() => Choose(CardType.Prince));
        kingBtn.onClick.AddListener(() => Choose(CardType.King));
        countessBtn.onClick.AddListener(() => Choose(CardType.Countess));
        princessBtn.onClick.AddListener(() => Choose(CardType.Princess));
    }

    void Choose(CardType c) { panel.SetActive(false); onChoose?.Invoke(c); onChoose = null; }
    public static void Show(Action<CardType> choose) { if (Instance == null) return; Instance.onChoose = choose; Instance.panel.SetActive(true); }
}
