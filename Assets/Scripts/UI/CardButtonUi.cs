using System;
using UnityEngine;
using UnityEngine.UI;

public class CardButtonUI : MonoBehaviour
{
    [SerializeField] Image artImage;
    [SerializeField] Button button;
    [SerializeField] PlayableHighlight highlight;   // NEW (optional)

    void Reset()
    {
        if (!artImage) artImage = GetComponentInChildren<Image>();
        if (!button) button = GetComponent<Button>();
        if (!highlight) highlight = GetComponent<PlayableHighlight>();
    }

    public void Setup(Sprite art, Action onClick, bool playable)
    {
        if (!artImage) artImage = GetComponentInChildren<Image>();
        if (!button) button = GetComponent<Button>();

        if (artImage) artImage.sprite = art;

        button.onClick.RemoveAllListeners();
        if (onClick != null) button.onClick.AddListener(() => onClick());

        // visual state
        if (highlight) highlight.SetPlayable(playable);

        // dim if not playable
        var cg = GetComponent<CanvasGroup>();
        if (!cg) cg = gameObject.AddComponent<CanvasGroup>();
        cg.interactable = playable;
        cg.blocksRaycasts = playable;
        cg.alpha = playable ? 1f : 0.75f;
    }
}
