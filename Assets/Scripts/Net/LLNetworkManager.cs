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
       // if (SceneManager.GetActiveScene().name != "MainMenu")
       //     SceneManager.LoadScene("MainMenu");
        // If Offline Scene is set, Mirror will load it automatically next frame.
        // Don't try to show UI here—the menu scene isn't loaded yet.
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
