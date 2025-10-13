using Mirror;
using UnityEngine;

public class PlayerNetwork : NetworkBehaviour
{
    public static PlayerNetwork Local;
    [SyncVar(hook = nameof(OnNameChanged))] public string PlayerName;
    [SyncVar(hook = nameof(OnHostChanged))] public bool IsHost;

    public override void OnStartServer()
    {
        IsHost = connectionToClient != null && connectionToClient.connectionId == 0;
        var fallback = $"Player {connectionToClient.connectionId}";
        PlayerName = LobbyUI.LocalPlayerNameOr(fallback);
    }

    public override void OnStartLocalPlayer()
    {
        Local = this;
        string n = LobbyUI.Instance ? LobbyUI.LocalPlayerName : PlayerPrefs.GetString("playerName", "Player");
        if (string.IsNullOrWhiteSpace(n)) n = $"Player {netId}";
        CmdSetName(n);
    }

    [Command] void CmdSetName(string n) => PlayerName = Sanitize(n);

    static string Sanitize(string s)
    {
        s = string.IsNullOrWhiteSpace(s) ? "Player" : s.Trim();
        if (s.Length > 20) s = s[..20];
        return s;
    }

    void OnNameChanged(string _, string __)   { LobbyUI.Instance?.RefreshNow(); }
    void OnHostChanged(bool _, bool __)       { LobbyUI.Instance?.RefreshNow(); }
}
