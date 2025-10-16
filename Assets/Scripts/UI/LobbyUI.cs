using kcp2k;
using Mirror;
using System;
using System.Collections;
using System.Collections.Generic;
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
    [SerializeField] TMP_Text errorText;
    [SerializeField] TMP_Text playersList;
    [SerializeField] Button leaveLobbyButton;

    public static LobbyUI Instance { get; private set; }
    public string LocalPlayerName { get; private set; } = "Player";
    Coroutine refreshCo;

    void Update()
    {
        bool hasName = !string.IsNullOrWhiteSpace(nameInput.text);
        hostButton.interactable = hasName;
        joinButton.interactable = hasName && !string.IsNullOrWhiteSpace(ipInput.text);
    }


    void Awake()
    {
        ipInput.text = "localhost";
        Instance = this;

        var saved = PlayerPrefs.GetString("playerName", "");
        if (!string.IsNullOrWhiteSpace(saved)) nameInput.text = saved;

        if (leaveLobbyButton)
        {
            leaveLobbyButton.onClick.RemoveAllListeners();
            leaveLobbyButton.onClick.AddListener(LeaveLobby);
        }

        ShowPreHost();
    }

    void OnEnable() { 
        SceneManager.sceneLoaded += OnSceneLoaded;
        if (!string.IsNullOrEmpty(LLNetworkManager.LastDisconnectReason))
        {
            ShowDisconnected(LLNetworkManager.LastDisconnectReason);
            LLNetworkManager.LastDisconnectReason = null;
        }
    }
    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        StopRefresh();
    }

    // Called by PlayerNetwork when server accepts the name
    public void OnNameConfirmed(string finalName)
    {
        if (nameInput) nameInput.text = finalName;
        if (errorText) errorText.gameObject.SetActive(false);
        RefreshNow();
    }

    public void ShowNameError(string msg)
    {
        if (!errorText) return;
        errorText.text = msg;
        errorText.gameObject.SetActive(true);
    }

    public void ClearNameError()
    {
        if (!errorText) return;
        errorText.gameObject.SetActive(false);
        errorText.text = "";
    }
    void OnSceneLoaded(Scene s, LoadSceneMode m)
    {
        StopRefresh();
    }

    public static string LocalPlayerNameOr(string fallback)
    {
        var n = Instance ? Instance.nameInput?.text : null;
        if (string.IsNullOrWhiteSpace(n)) n = PlayerPrefs.GetString("playerName", fallback);
        return string.IsNullOrWhiteSpace(n) ? fallback : n;
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

    public void HostGame()
    {
        LocalPlayerName = SanitizeName(nameInput.text);
        PlayerPrefs.SetString("playerName", LocalPlayerName);

        preHostPanel.SetActive(false);
        NetworkManager.singleton.StartHost();
        ShowHostLobby();
    }

    public void JoinGame()
    {
        LocalPlayerName = SanitizeName(nameInput.text);
        PlayerPrefs.SetString("playerName", LocalPlayerName);

        var ip = string.IsNullOrWhiteSpace(ipInput.text) ? "localhost" : ipInput.text.Trim();
        NetworkManager.singleton.networkAddress = ip;
        NetworkManager.singleton.StartClient();
        ShowClientLobby();
    }

    public static void StartGame()
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
        if (leaveLobbyButton) leaveLobbyButton.gameObject.SetActive(true);

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

        if (leaveLobbyButton) leaveLobbyButton.gameObject.SetActive(false);

        UpdateHostControlsVisibility();
        RefreshNow();

        StopRefresh();
        refreshCo = StartCoroutine(RefreshLoop());
    }

    void UpdateHostControlsVisibility()
    {
        bool isHost = NetworkServer.active;
        bool isClientOnly = NetworkClient.active && !NetworkServer.active;

        if (startGameButton) startGameButton.gameObject.SetActive(isHost);
        if (stopHostingButton) stopHostingButton.gameObject.SetActive(isHost);
        if (leaveLobbyButton) leaveLobbyButton.gameObject.SetActive(isClientOnly);
    }

    public void LeaveLobby()
    {
        // Client only (non-host)
        if (NetworkClient.active && !NetworkServer.active)
        {
            // Optional: set a reason so the pre-host screen can show a message
            LLNetworkManager.LastDisconnectReason = "You left the lobby.";
            NetworkManager.singleton.StopClient();
        }

        ShowPreHost();
    }

    public void ShowDisconnected(string reason)
    {
        Debug.Log($"[UI] Disconnected: {reason}");
        if (errorText) errorText.text = reason;
        ShowPreHost();
    }


    IEnumerator RefreshLoop()
    {
        while (true)
        {
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

    IEnumerable<PlayerNetwork> SortedPlayers()
    {
        // Works on both client and host
        var all = Mirror.NetworkClient.spawned.Values
            .Select(n => n ? n.GetComponent<PlayerNetwork>() : null)
            .Where(p => p != null);

        return all
            .OrderByDescending(p => p.IsHost)                                   // host at top
            .ThenBy(p => string.IsNullOrWhiteSpace(p.PlayerName) ? "~" : p.PlayerName,
                    StringComparer.OrdinalIgnoreCase);                          // A→Z (case-insensitive)
    }

    static string RowText(PlayerNetwork pn)
        => (string.IsNullOrWhiteSpace(pn.PlayerName) ? "Player" : pn.PlayerName)
           + (pn.IsHost ? " (Host)" : "");

    public void RefreshNow()
    {
        if (!playersList) return;

        var sb = new System.Text.StringBuilder();
        foreach (var pn in SortedPlayers())
            sb.AppendLine(RowText(pn));

        playersList.text = sb.ToString();
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
        foreach (var pn in SortedPlayers())
        {
            var row = Instantiate(playerListItemPrefab, playerListRoot);
            var txt = row.GetComponent<TMP_Text>();
            if (txt) txt.text = RowText(pn);
            count++;
        }

        startGameButton.interactable = count >= 2;
        statusText.text = startGameButton.interactable ? "Ready to start" : $"Waiting for players ({count}/2)";
    }

    void RefreshPlayerListClient()
    {
        if (!playerListRoot || !playerListItemPrefab || !statusText) return;

        ClearList();

        int count = 0;
        foreach (var pn in SortedPlayers())
        {
            var row = Instantiate(playerListItemPrefab, playerListRoot);
            var txt = row.GetComponent<TMP_Text>();
            if (txt) txt.text = RowText(pn);
            count++;
        }

        statusText.text = count >= 2 ? "Ready — waiting for host" : $"Waiting for players ({count}/2)";
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
