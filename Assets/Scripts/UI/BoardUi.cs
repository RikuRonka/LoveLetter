using TMPro;
using UnityEngine;

public class BoardUI : MonoBehaviour
{
    public static BoardUI Instance;
    [SerializeField] TMP_Text logText, turnText, deckText;
    PublicState lastState;

    void Awake() => Instance = this;

    public void RenderState(PublicState s)
    {
        lastState = s;
        deckText.text = $"Deck: {s.DeckCount}  •  Burned: {s.BurnedCount}";
        string cur = (s.Players.Count > 0 && s.CurrentIndex >= 0 && s.CurrentIndex < s.Players.Count)
            ? s.Players[s.CurrentIndex].Name : "?";
        turnText.text = $"Turn: {cur}";
    }

    public void Log(string msg)
    {
        if (!logText) return;
        logText.text = (logText.text.Length > 0 ? logText.text + "\n" : "") + msg;
    }

    public PublicState LastPublicState() => lastState;
}
