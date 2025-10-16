using Mirror;
using System.Collections;
using System.Linq;
using UnityEngine;

public class PlayerNetwork : NetworkBehaviour
{
    public static PlayerNetwork Local;
    [SyncVar(hook = nameof(OnNameChanged))] public string PlayerName;
    [SyncVar(hook = nameof(OnHostChanged))] public bool IsHost;
    [SyncVar] public bool NameReady;

    public override void OnStartServer()
    {
        IsHost = connectionToClient != null && connectionToClient.connectionId == 0;
        // Do NOT reserve a name here; wait for the client request.
        PlayerName = ""; // placeholder until approved
    }

    public override void OnStopServer()
    {
        NameRegistry.Release(PlayerName);
    }

    public override void OnStartLocalPlayer()
    {
        CmdRequestName(LobbyUI.LocalPlayerNameOr("Player"));
    }

    [Command]
    void CmdRequestName(string requested)
    {
        // Server validates & reserves a unique name
        var gc = GameController.Instance;
        var unique = gc ? gc.ReserveUniqueName(netId, requested) : GameController.Sanitize(requested);
        PlayerName = unique;
        IsHost = connectionToClient != null && connectionToClient.connectionId == 0;
    }

    void OnNameChanged(string oldValue, string newValue)
    {
        // update any UI that shows the lobby list / local name
        LobbyUI.Instance?.RefreshNow();
    }

    void OnHostChanged(bool oldValue, bool newValue)
    {
        LobbyUI.Instance?.RefreshNow();
    }
}
