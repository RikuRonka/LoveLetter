using Mirror;
using UnityEngine.SceneManagement;

public class LoveLetterNetworkManager : NetworkManager
{
    public override void OnClientDisconnect()
    {
        base.OnClientDisconnect();
        if (SceneManager.GetActiveScene().name != "MainMenu")
            SceneManager.LoadScene("MainMenu");
    }
}
