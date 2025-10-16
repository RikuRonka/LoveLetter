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
    public void Show(SummaryRow[] rows, uint winnerNetId, int pointsToWin, bool isMatchOver)
    {
        if (!panel || rows == null) return;

        for (int i = rowsRoot.childCount - 1; i >= 0; i--)
            Destroy(rowsRoot.GetChild(i).gameObject);


        var winRow = rows.FirstOrDefault(r => r.NetId == winnerNetId);
        var winnerName = string.IsNullOrWhiteSpace(winRow.Name) ? "?" : winRow.Name;
        titleText.text = isMatchOver ? $"{winnerName} wins the match!" : $"{winnerName} wins the round!";

        foreach (var r in rows.OrderByDescending(x => x.Score).ThenBy(x => x.Name))
        {
            var go = Instantiate(rowPrefab, rowsRoot);
            var texts = go.GetComponentsInChildren<TMP_Text>(true);
            var nameText = texts.Length > 0 ? texts[0] : null;
            var scoreText = texts.Length > 1 ? texts[1] : null;

            if (nameText) nameText.text = r.Name;
            if (scoreText) scoreText.text = $"{r.Score} / {pointsToWin}";

            if (r.NetId == winnerNetId && nameText) nameText.fontStyle = FontStyles.Bold;
        }

        bool isHost = PlayerNetwork.Local && PlayerNetwork.Local.IsHost;
        nextRoundButton.gameObject.SetActive(!isMatchOver && isHost);
        playAgainButton.gameObject.SetActive(isMatchOver && isHost);

        nextRoundButton.onClick.RemoveAllListeners();
        nextRoundButton.onClick.AddListener(() =>
        {
            nextRoundButton.interactable = false;
            PlayerActions.Local?.CmdNextRound();
        });

        playAgainButton.onClick.RemoveAllListeners();
        playAgainButton.onClick.AddListener(() =>
        {
            // TODO: add a server Command like CmdHostResetMatch() if you want a full reset
            // For example: PlayerActions.Local?.CmdHostResetMatch();
        });

        panel.SetActive(true);
    }

    public void Hide()
    {
        if (panel) panel.SetActive(false);
    }
}
