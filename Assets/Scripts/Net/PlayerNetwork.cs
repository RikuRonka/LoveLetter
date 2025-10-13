using Mirror;

public class PlayerNetwork : NetworkBehaviour
{
    [SyncVar(hook = nameof(OnNameChanged))] public string PlayerName;
    [SyncVar(hook = nameof(OnHostChanged))] public bool IsHost;

    public override void OnStartServer()
    {
        IsHost = connectionToClient != null && connectionToClient.connectionId == 0;
    }

    public override void OnStartLocalPlayer()
    {
        // take from your LobbyUI cached name
        CmdSetName(LobbyUI.LocalPlayerNameOr("Player"));
    }

    [Command]
    void CmdSetName(string n)
    {
        if (string.IsNullOrWhiteSpace(n))
            n = $"Player {connectionToClient.connectionId}";
        if (n.Length > 20) n = n[..20];
        PlayerName = n;
    }

    void OnNameChanged(string _, string __)   { LobbyUI.Instance?.RefreshNow(); }
    void OnHostChanged(bool _, bool __)       { LobbyUI.Instance?.RefreshNow(); }
}
