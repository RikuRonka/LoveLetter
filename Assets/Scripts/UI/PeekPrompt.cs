using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class PeekPrompt : MonoBehaviour
{
    public static PeekPrompt Instance;

    [SerializeField] GameObject panel;
    [SerializeField] Image cardImage;
    [SerializeField] TMP_Text titleText;
    [SerializeField] Button closeButton;
    [SerializeField] float autoCloseSeconds = 0f;

    void Awake()
    {
        Instance = this;
        if (panel) panel.SetActive(false);
        if (closeButton) closeButton.onClick.AddListener(Close);
    }

    public static void Show(string playerName, CardType card)
    {
        if (Instance == null) { Debug.LogWarning("PeekPrompt not in scene"); return; }
        Instance.InternalShow(playerName, card);
    }

    void InternalShow(string playerName, CardType card)
    {
        if (titleText) titleText.text = $"{playerName} has: {CardDB.Title[card]}";
        if (cardImage) cardImage.sprite = CardDB.Sprite(card);

        gameObject.SetActive(true);
        if (panel) panel.SetActive(true);

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
        gameObject.SetActive(false);
    }
}
