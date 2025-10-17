using Mirror;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static GameController;

public class RoundSummaryUI : MonoBehaviour
{
    public static RoundSummaryUI Instance;

    [Header("Wiring")]
    [SerializeField] GameObject panel;
    [SerializeField] TMP_Text titleText;
    [SerializeField] Transform rowsRoot;
    [SerializeField] GameObject rowPrefab;
    [SerializeField] Button nextRoundButton;
    [SerializeField] Button playAgainButton;

    void Awake()
    {
        Instance = this;
        if (panel) panel.SetActive(false);
    }

    /// <summary>
    /// Render the server-provided summary.
    /// </summary>
    public void Show(GameController.SummaryRow[] rows, uint winnerNetId, int pointsToWin, bool isMatchOver)
    {
        if (!panel) return;

        // clear old rows
        for (int i = rowsRoot.childCount - 1; i >= 0; i--)
            Destroy(rowsRoot.GetChild(i).gameObject);

        // title
        var winner = rows.FirstOrDefault(r => r.NetId == winnerNetId);
        string winnerName = string.IsNullOrWhiteSpace(winner.Name) ? "?" : winner.Name;
        if (titleText)
            titleText.text = isMatchOver ? $"{winnerName} wins the match!" : $"{winnerName} wins the round!";

        // rows: "name — score / pointsToWin"
        foreach (var r in rows.OrderByDescending(r => r.Score).ThenBy(r => r.Name))
        {
            var go = Instantiate(rowPrefab, rowsRoot);
            var txt = go.GetComponentInChildren<TMP_Text>();
            if (txt) txt.text = $"{r.Name} — {r.Score} / {pointsToWin}";
        }

        // buttons (host-only)
        bool isHost = NetworkServer.active;
        if (nextRoundButton)
        {
            nextRoundButton.gameObject.SetActive(!isMatchOver && isHost);
            nextRoundButton.onClick.RemoveAllListeners();
            nextRoundButton.onClick.AddListener(() =>
            {
                nextRoundButton.interactable = false;
                PlayerActions.Local?.CmdNextRound();
            });
        }

        if (playAgainButton)
        {
            playAgainButton.gameObject.SetActive(isMatchOver && isHost);
            playAgainButton.onClick.RemoveAllListeners();
            playAgainButton.onClick.AddListener(() =>
            {
                // optional: implement full reset if you want
            });
        }

        panel.SetActive(true);
    }

    public void Hide()
    {
        if (panel) panel.SetActive(false);
    }
}

