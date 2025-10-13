using System;
using UnityEngine;
using UnityEngine.UI;

public class CardButtonUI : MonoBehaviour
{
    [SerializeField] Image artImage;  // drag the Image that shows the card
    [SerializeField] Button button;   // drag the Button on the root

    void Reset()
    {
        if (!artImage) artImage = GetComponentInChildren<Image>();
        if (!button) button = GetComponent<Button>();
    }

    public void Setup(Sprite art, Action onClick)
    {
        // lazy fallback in case fields weren’t wired
        if (!artImage) artImage = GetComponentInChildren<Image>();
        if (!button) button = GetComponent<Button>();

        if (artImage) artImage.sprite = art;  // art can be null; Image accepts null
        if (button)
        {
            button.onClick.RemoveAllListeners();
            if (onClick != null) button.onClick.AddListener(() => onClick());
        }
        else
        {
            Debug.LogWarning("[CardButtonUI] Button missing on prefab.", this);
        }
    }
}
