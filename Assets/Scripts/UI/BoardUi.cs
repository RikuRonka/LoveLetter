using TMPro;
using UnityEngine;

public class BoardUI : MonoBehaviour
{
    public static BoardUI Instance;

    [Header("Texts")]
    [SerializeField] TMP_Text turnText;
    [SerializeField] TMP_Text logText;

    [Header("Turn Colors")]
    [SerializeField] Color yourTurnColor = new(0.20f, 0.85f, 0.35f);
    [SerializeField] Color oppTurnColor = new(0.90f, 0.30f, 0.30f);

    void Awake() => Instance = this;

    public void RenderState(PublicState s)
    {
        if (turnText == null || s.Players == null || s.Players.Count == 0)
            return;

        var cur = (s.CurrentIndex >= 0 && s.CurrentIndex < s.Players.Count)
                  ? s.Players[s.CurrentIndex]
                  : null;

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

        DeckUI.I?.Render(s.DeckCount, s.BurnedCount);
    }

    public void Log(string msg)
    {
        if (!logText) return;

        if (string.IsNullOrEmpty(logText.text))
            logText.text = msg;
        else
            logText.text += "\n" + msg;

    }
}
