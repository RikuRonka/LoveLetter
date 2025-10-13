using kcp2k;
using Mirror;
using System.Collections;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LobbyUI : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] GameObject preHostPanel;
    [SerializeField] GameObject hostPanel;  
    [SerializeField] private TMP_InputField ipInput;
    [SerializeField] TMP_InputField nameInput;

    [Header("Host Lobby UI")]
    [SerializeField] TMP_Text statusText;
    [SerializeField] TMP_Text ipText;
    [SerializeField] Button startGameButton;
    [SerializeField] Transform playerListRoot;
    [SerializeField] GameObject playerListItemPrefab;
    [SerializeField] Button hostButton;
    [SerializeField] Button joinButton;
    [SerializeField] Button stopHostingButton;

    public static LobbyUI Instance { get; private set; }
    public static string LocalPlayerName { get; private set; } = "Player";

    Coroutine refreshCo;
    const int MIN_PLAYERS = 2;

    void Update()
    {
        bool hasName = !string.IsNullOrWhiteSpace(nameInput.text);
        hostButton.interactable = hasName;
        joinButton.interactable = hasName && !string.IsNullOrWhiteSpace(ipInput.text);
    }


    void Awake()
    {
        Instance = this;
        // load last used name
        var saved = PlayerPrefs.GetString("playerName", "");
        if (!string.IsNullOrWhiteSpace(saved)) nameInput.text = saved;
        ShowPreHost();
    }

    void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        StopRefresh();
    }

    void OnSceneLoaded(Scene s, LoadSceneMode m)
    {
        // ensure no stale coroutine keeps touching destroyed UI
        StopRefresh();
    }

    public static string LocalPlayerNameOr(string fallback)
    {
        var ui = FindObjectOfType<LobbyUI>();
        if (ui != null && ui.nameInput != null && !string.IsNullOrWhiteSpace(ui.nameInput.text))
            return ui.nameInput.text.Trim();

        return fallback;
    }

    void StopRefresh()
    {
        if (refreshCo != null)
        {
            StopCoroutine(refreshCo);
            refreshCo = null;
        }
    }

    public void QuitGame()
    {
        Application.Quit();
    }

    // --- helpers ---
    bool IsHostInstance() => NetworkServer.active && NetworkClient.active; // host = server+client
    void UpdateHostOnlyControls()
    {
        bool isHost = IsHostInstance();
        if (startGameButton) startGameButton.gameObject.SetActive(isHost);
        if (stopHostingButton) stopHostingButton.gameObject.SetActive(isHost);
    }

    // ===== Buttons =====
    public void HostGame()
    {
        // save & cache name
        LocalPlayerName = SanitizeName(nameInput.text);
        PlayerPrefs.SetString("playerName", LocalPlayerName);

        preHostPanel.SetActive(false);
        NetworkManager.singleton.StartHost();
        ShowHostLobby();
    }

    public void JoinGame()
    {
        // save & cache name
        LocalPlayerName = SanitizeName(nameInput.text);
        PlayerPrefs.SetString("playerName", LocalPlayerName);

        var ip = string.IsNullOrWhiteSpace(ipInput.text) ? "localhost" : ipInput.text.Trim();
        NetworkManager.singleton.networkAddress = ip;
        Debug.Log($"[UI] Join {ip}");
        NetworkManager.singleton.StartClient();
        ShowClientLobby();
    }

    public void StartGame()
    {
        if (NetworkServer.active)
            NetworkManager.singleton.ServerChangeScene("GameScene");
    }

    public void StopHosting()
    {
        if (NetworkServer.active) NetworkManager.singleton.StopHost();
        else if (NetworkClient.active) NetworkManager.singleton.StopClient();
        ShowPreHost();
    }
    public void ShowPreHost()
    {
        StopRefresh();
        if (preHostPanel) preHostPanel.SetActive(true);
        if (hostPanel) hostPanel.SetActive(false);
    }

    void ShowClientLobby()
    {
        if (!hostPanel || !ipText || !statusText) return;

        if (preHostPanel) preHostPanel.SetActive(false);
        hostPanel.SetActive(true);
        ipText.text = "Connected. Waiting for host…";
        statusText.text = "Joining…";

        if (startGameButton) startGameButton.gameObject.SetActive(false);
        if (stopHostingButton) stopHostingButton.gameObject.SetActive(false);

        StopRefresh();
        refreshCo = StartCoroutine(RefreshLoop());
    }



    void ShowHostLobby()
    {
        if (!hostPanel || !ipText || !statusText) return;

        if (preHostPanel) preHostPanel.SetActive(false);
        hostPanel.SetActive(true);

        ipText.text = $"Your IP: {GetLocalIPv4()}";
        statusText.text = "Hosting… waiting for players";

        UpdateHostControlsVisibility();
        RefreshNow();

        StopRefresh();
        refreshCo = StartCoroutine(RefreshLoop());
    }

    void UpdateHostControlsVisibility()
    {
        bool isHost = NetworkServer.active;
        if (startGameButton) startGameButton.gameObject.SetActive(isHost);
        if (stopHostingButton) stopHostingButton.gameObject.SetActive(isHost);
    }

    void StartRefreshLoop()
    {
        if (refreshCo != null) StopCoroutine(refreshCo);
        refreshCo = StartCoroutine(RefreshLoop());
    }

    string GetListenPort()
    {
        var t = NetworkManager.singleton.transport;
        if (t is KcpTransport kcp) return kcp.Port.ToString();
        if (t is TelepathyTransport tel) return tel.port.ToString();
        return "?";
    }

    public void ShowDisconnected(string reason)
    {
        Debug.Log($"[UI] Disconnected: {reason}");
        ShowPreHost();
        // (Optional) show 'reason' somewhere on preHostPanel
    }


    IEnumerator RefreshLoop()
    {
        while (true)
        {
            // break if UI is gone or we left networking
            if (this == null || !gameObject || !NetworkClient.active)
            {
                ShowPreHost();
                yield break;
            }

            UpdateHostControlsVisibility();

            if (NetworkServer.active) RefreshPlayerListServer();
            else RefreshPlayerListClient();

            yield return new WaitForSeconds(0.5f);
        }
    }

    public void RefreshNow()
    {
        if (NetworkServer.active) RefreshPlayerListServer();
        else RefreshPlayerListClient();
    }

    void ClearList()
    {
        if (!playerListRoot) return;
        for (int i = playerListRoot.childCount - 1; i >= 0; i--)
            Destroy(playerListRoot.GetChild(i).gameObject);
    }


    void RefreshPlayerListServer()
    {
        if (!playerListRoot || !playerListItemPrefab || !statusText || !startGameButton) return;

        ClearList();

        int count = 0;
        foreach (var kv in NetworkServer.connections)
        {
            var row = Instantiate(playerListItemPrefab, playerListRoot);
            var txt = row.GetComponent<TMP_Text>();
            if (txt) txt.text = kv.Value?.identity ?
                kv.Value.identity.GetComponent<PlayerNetwork>()?.PlayerName ?? $"Player {kv.Key}" :
                $"Player {kv.Key}";
            count++;
        }

        startGameButton.interactable = count >= 2;
        statusText.text = startGameButton.interactable ? "Ready to start" : $"Waiting for players ({count}/2)";
    }

    void RefreshPlayerListClient()
    {
        if (!playerListRoot || !playerListItemPrefab || !statusText) return;

        ClearList();

        var players = FindObjectsOfType<PlayerNetwork>();
        foreach (var p in players)
        {
            var row = Instantiate(playerListItemPrefab, playerListRoot);
            var txt = row.GetComponent<TMP_Text>();
            if (txt) txt.text = string.IsNullOrWhiteSpace(p.PlayerName) ? "Player" : p.PlayerName;
        }

        statusText.text = players.Length >= 2 ? "Ready — waiting for host" : $"Waiting for players ({players.Length}/2)";
    }

    static string SanitizeName(string raw)
    {
        var s = string.IsNullOrWhiteSpace(raw) ? "Player" : raw.Trim();
        if (s.Length > 20) s = s[..20];
        return s;
    }

    // ===== Utils =====
    static string GetLocalIPv4()
    {
        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != OperationalStatus.Up) continue;

            // Skip virtual/tunnel adapters commonly causing 172.* addresses
            var name = ni.Name.ToLower();
            if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
            if (name.Contains("virtual") || name.Contains("vEthernet".ToLower()) ||
                name.Contains("hyper-v") || name.Contains("vmware") ||
                name.Contains("virtualbox") || name.Contains("hamachi") ||
                name.Contains("tap") || name.Contains("tunnel")) continue;

            var ipProps = ni.GetIPProperties();
            // Prefer adapters that actually have a gateway (real LAN)
            bool hasGateway = ipProps.GatewayAddresses.Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork);

            foreach (var ua in ipProps.UnicastAddresses)
            {
                if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                if (IPAddress.IsLoopback(ua.Address)) continue;
                string ip = ua.Address.ToString();
                // Prefer 192.168.* or 10.* and those with a gateway
                if (hasGateway && (ip.StartsWith("192.168.") || ip.StartsWith("10.")))
                    return ip;
            }
        }
        // Fallback (not ideal)
        return "127.0.0.1";
    }
}
