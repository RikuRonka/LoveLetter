using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class ComparePrompt : MonoBehaviour
{
    public static ComparePrompt Instance;

    [Header("Root")]
    [SerializeField] GameObject panel;          // overlay to show/hide

    [Header("Left")]
    [SerializeField] TMP_Text leftName;
    [SerializeField] Image leftCard;

    [Header("Right")]
    [SerializeField] TMP_Text rightName;
    [SerializeField] Image rightCard;

    [Header("Footer")]
    [SerializeField] TMP_Text resultText;

    [Header("Behavior")]
    [SerializeField] float autoCloseSeconds = 2f;

    void Awake()
    {
        Instance = this;
        if (panel) panel.SetActive(false);
    }

    public static void Show(string aName, CardType aCard, string bName, CardType bCard, string result)
    {
        var i = Instance ?? FindFirstObjectByType<ComparePrompt>();
        if (i == null) { Debug.LogWarning("[ComparePrompt] No instance in scene"); return; }
        i.InternalShow(aName, aCard, bName, bCard, result);
    }

    void InternalShow(string aName, CardType aCard, string bName, CardType bCard, string result)
    {
        if (leftName) leftName.text = aName;
        if (rightName) rightName.text = bName;

        if (leftCard) leftCard.sprite = CardDB.Sprite(aCard);
        if (rightCard) rightCard.sprite = CardDB.Sprite(bCard);

        if (resultText) resultText.text = result;

        if (panel) panel.SetActive(true); else gameObject.SetActive(true);

        if (autoCloseSeconds > 0f) StartCoroutine(AutoClose());
    }

    IEnumerator AutoClose()
    {
        yield return new WaitForSecondsRealtime(autoCloseSeconds);
        Close();
    }

    public void Close()
    {
        if (panel) panel.SetActive(false);
        else gameObject.SetActive(false);
    }
}
