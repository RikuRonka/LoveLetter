using TMPro;
using UnityEngine;

public class BoardUI : MonoBehaviour
{
    public static BoardUI Instance;

    [Header("Texts")]
    [SerializeField] TMP_Text turnText;   // "Turn: Alice"
    [SerializeField] TMP_Text logText;    // scrolling log or simple label

    void Awake() => Instance = this;

    public void RenderState(PublicState s)
    {
        // turn name
        string name = "?";
        if (s.Players != null &&
            s.CurrentIndex >= 0 &&
            s.CurrentIndex < s.Players.Count)
            name = s.Players[s.CurrentIndex].Name;

        if (turnText) turnText.text = $"Turn: {name}";

        // pass deck/burned to DeckUI
        DeckUI.I?.Render(s.DeckCount, s.BurnedCount);
    }

    public void Log(string msg)
    {
        if (!logText) return;
        if (string.IsNullOrEmpty(logText.text)) logText.text = msg;
        else logText.text += "\n" + msg;
    }
}
