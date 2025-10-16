using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LLNetworkManager : NetworkManager
{
    [Header("Scenes")]
    [SerializeField] string menuSceneName = "MainMenu";
    public static string LastDisconnectReason;


    public override void OnStartServer()
    {
        base.OnStartServer();
        maxConnections = 4;
    }
    public override void OnServerDisconnect(NetworkConnectionToClient conn)
    {
        var ni = conn.identity;
        if (ni)
        {
            var pn = ni.GetComponent<PlayerNetwork>();
            // Use netId so we release correctly even if PlayerName changed
            GameController.Instance?.ReleaseNameFor(ni.netId);
        }
        base.OnServerDisconnect(conn);
    }
    public override void OnStopHost()
    {
        base.OnStopHost();
        LastDisconnectReason = "You stopped hosting.";
        LoadMenuIfNeeded();
    }

    public override void OnClientDisconnect()
    {
        base.OnClientDisconnect();
        LastDisconnectReason = "Lost connection to host.";
    }

    public override void OnStopClient()
    {
        base.OnStopClient();
        LoadMenuIfNeeded();
    }

    void LoadMenuIfNeeded()
    {
        if (SceneManager.GetActiveScene().name == menuSceneName) return;

        if (NetworkClient.isConnected)
        {
            StopClient();
        }

        SceneManager.LoadScene(menuSceneName);
    }

}
