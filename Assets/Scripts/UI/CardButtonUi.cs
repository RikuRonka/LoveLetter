using UnityEngine;
using UnityEngine.UI;

public class CardButtonUI : MonoBehaviour
{
    public Image cardImage;

    public void Setup(Sprite art, System.Action onClick)
    {
        cardImage.sprite = art;

        var btn = GetComponent<Button>();
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() => onClick?.Invoke());
    }
}
