using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LLNetworkManager : NetworkManager
{
    [Header("Scenes")]
    [SerializeField] string menuSceneName = "MainMenu";
    public static string LastDisconnectReason;

    // host (server+client) pressed Stop Hosting / app closed
    public override void OnStopHost()
    {
        base.OnStopHost();
        LastDisconnectReason = "You stopped hosting.";
        LoadMenuIfNeeded();
    }

    // called on clients when the server disappears (host left)
    public override void OnClientDisconnect()
    {
        base.OnClientDisconnect();
        LastDisconnectReason = "Lost connection to host.";
        // If Offline Scene is set, Mirror will load it automatically next frame.
        // Don't try to show UI here—the menu scene isn't loaded yet.
    }


    // extra safety net for client shutdown paths
    public override void OnStopClient()
    {
        base.OnStopClient();
        LoadMenuIfNeeded();
    }

    void LoadMenuIfNeeded()
    {
        // already in menu?
        if (SceneManager.GetActiveScene().name == menuSceneName) return;

        // ensure networking is fully stopped before changing scenes
        if (NetworkClient.isConnected)
        {
            StopClient();
        }

        // load menu scene
        SceneManager.LoadScene(menuSceneName);
    }
}
