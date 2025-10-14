using TMPro;
using UnityEngine;

public class BoardUI : MonoBehaviour
{
    public static BoardUI Instance;

    [Header("Texts")]
    [SerializeField] TMP_Text turnText;   // "Turn: Alice"
    [SerializeField] TMP_Text logText;    // scrolling log or simple label

    [Header("Turn Colors")]
    [SerializeField] Color yourTurnColor = new(0.20f, 0.85f, 0.35f); // green
    [SerializeField] Color oppTurnColor = new(0.90f, 0.30f, 0.30f); // red

    void Awake() => Instance = this;

    public void RenderState(PublicState s)
    {
        if (turnText == null || s.Players == null || s.Players.Count == 0)
            return;

        // who’s turn per server
        var cur = (s.CurrentIndex >= 0 && s.CurrentIndex < s.Players.Count)
                  ? s.Players[s.CurrentIndex]
                  : null;

        // local player netId (0 if unknown yet)
        uint localId = PlayerNetwork.Local ? PlayerNetwork.Local.netId : 0;

        bool isLocalTurn = (cur != null && cur.NetId == localId);

        if (isLocalTurn)
        {
            turnText.text = "Your turn";
            turnText.color = yourTurnColor;
        }
        else
        {
            string name = cur != null ? cur.Name : "?";
            turnText.text = $"{name}'s turn";
            turnText.color = oppTurnColor;
        }

        // forward deck/burned to DeckUI (if you use it)
        DeckUI.I?.Render(s.DeckCount, s.BurnedCount);
    }

    public void Log(string msg)
    {
        if (!logText) return;
        if (string.IsNullOrEmpty(logText.text)) logText.text = msg;
        else logText.text += "\n" + msg;
    }
}
