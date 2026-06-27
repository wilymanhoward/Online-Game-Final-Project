using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using Photon.Realtime;

public class LobbyManager : MonoBehaviourPunCallbacks
{
    [Header("UI Status Text")]
    private Text statusText;
    private Button joinButton;
    private GameObject lobbyPanel;
    private GameObject waitingPanel;
    private Text waitingText;
    private GameObject startButton;

    private const string GAME_SCENE_NAME = "SampleScene";

    void Start()
    {
        // Setup automatically sync scene
        PhotonNetwork.AutomaticallySyncScene = true;

        // Build UI Dynamically to ensure it works without manual scene setups
        CreateDynamicUI();

        statusText.text = "Connecting to Photon Network...";
        joinButton.interactable = false;
        lobbyPanel.SetActive(true);
        waitingPanel.SetActive(false);

        // Connect to Photon
        if (!PhotonNetwork.IsConnected)
        {
            PhotonNetwork.ConnectUsingSettings();
        }
        else
        {
            OnConnectedToMaster();
        }
    }

    public override void OnConnectedToMaster()
    {
        statusText.text = "Connected to Master Server. Ready to Join!";
        joinButton.interactable = true;
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        statusText.text = "Disconnected: " + cause.ToString() + ". Reconnecting...";
        joinButton.interactable = false;
        lobbyPanel.SetActive(true);
        waitingPanel.SetActive(false);
        PhotonNetwork.ConnectUsingSettings();
    }

    public void OnJoinButtonClicked()
    {
        statusText.text = "Searching for a room...";
        joinButton.interactable = false;

        // Try to join any random room
        PhotonNetwork.JoinRandomRoom();
    }

    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        statusText.text = "No empty room found. Creating a new room...";
        
        // Create a room with a limit of 2 players
        RoomOptions roomOptions = new RoomOptions();
        roomOptions.MaxPlayers = 2;
        
        PhotonNetwork.CreateRoom(null, roomOptions);
    }

    public override void OnJoinedRoom()
    {
        statusText.text = "Joined Room successfully!";
        lobbyPanel.SetActive(false);
        waitingPanel.SetActive(true);
        
        UpdateWaitingStatus();
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        UpdateWaitingStatus();
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        UpdateWaitingStatus();
    }

    private void UpdateWaitingStatus()
    {
        if (PhotonNetwork.CurrentRoom != null)
        {
            int playerCount = PhotonNetwork.CurrentRoom.PlayerCount;
            int maxPlayers = PhotonNetwork.CurrentRoom.MaxPlayers;
            waitingText.text = $"Waiting for Players...\n({playerCount} / {maxPlayers})";
            
            if (startButton)
            {
                startButton.SetActive(PhotonNetwork.IsMasterClient);
            }
        }
    }

    public void OnStartButtonClicked()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            statusText.text = "Starting game...";
            PhotonNetwork.LoadLevel(GAME_SCENE_NAME);
        }
    }

    // Helper method to create canvas and UI elements at runtime
    private void CreateDynamicUI()
    {
        // 1. Create Canvas
        GameObject canvasObj = new GameObject("LobbyCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();

        // Create EventSystem if not exists
        if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            GameObject eventSystemObj = new GameObject("EventSystem");
            eventSystemObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystemObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        // Create Background Panel
        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(canvasObj.transform, false);
        Image bgImage = bgObj.AddComponent<Image>();
        bgImage.color = new Color(0.1f, 0.1f, 0.15f, 1f);
        RectTransform bgRect = bgObj.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;

        // 2. Create Lobby Panel
        lobbyPanel = new GameObject("LobbyPanel");
        lobbyPanel.transform.SetParent(canvasObj.transform, false);
        RectTransform lobbyRect = lobbyPanel.AddComponent<RectTransform>();
        lobbyRect.anchorMin = Vector2.zero;
        lobbyRect.anchorMax = Vector2.one;
        lobbyRect.sizeDelta = Vector2.zero;

        // Title Text
        GameObject titleObj = new GameObject("TitleText");
        titleObj.transform.SetParent(lobbyPanel.transform, false);
        Text titleText = titleObj.AddComponent<Text>();
        titleText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        titleText.text = "ANCIENT EGYPT MULTIPLAYER";
        titleText.fontSize = 36;
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.color = new Color(0.9f, 0.75f, 0.2f);
        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchoredPosition = new Vector2(0, 150);
        titleRect.sizeDelta = new Vector2(500, 100);

        // Status Text
        GameObject statusObj = new GameObject("StatusText");
        statusObj.transform.SetParent(lobbyPanel.transform, false);
        statusText = statusObj.AddComponent<Text>();
        statusText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        statusText.text = "Initializing...";
        statusText.fontSize = 18;
        statusText.alignment = TextAnchor.MiddleCenter;
        statusText.color = Color.white;
        RectTransform statusRect = statusObj.GetComponent<RectTransform>();
        statusRect.anchoredPosition = new Vector2(0, 50);
        statusRect.sizeDelta = new Vector2(600, 50);

        // Join Button
        GameObject buttonObj = new GameObject("JoinButton");
        buttonObj.transform.SetParent(lobbyPanel.transform, false);
        Image btnImage = buttonObj.AddComponent<Image>();
        btnImage.color = new Color(0.2f, 0.6f, 0.3f);
        joinButton = buttonObj.AddComponent<Button>();
        RectTransform btnRect = buttonObj.GetComponent<RectTransform>();
        btnRect.anchoredPosition = new Vector2(0, -50);
        btnRect.sizeDelta = new Vector2(250, 60);

        // Join Button Text
        GameObject btnTextObj = new GameObject("BtnText");
        btnTextObj.transform.SetParent(buttonObj.transform, false);
        Text btnText = btnTextObj.AddComponent<Text>();
        btnText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        btnText.text = "JOIN QUICK PLAY";
        btnText.fontSize = 20;
        btnText.alignment = TextAnchor.MiddleCenter;
        btnText.color = Color.white;
        RectTransform btnTextRect = btnTextObj.GetComponent<RectTransform>();
        btnTextRect.anchorMin = Vector2.zero;
        btnTextRect.anchorMax = Vector2.one;
        btnTextRect.sizeDelta = Vector2.zero;

        joinButton.onClick.AddListener(OnJoinButtonClicked);

        // 3. Create Waiting Panel
        waitingPanel = new GameObject("WaitingPanel");
        waitingPanel.transform.SetParent(canvasObj.transform, false);
        RectTransform waitRect = waitingPanel.AddComponent<RectTransform>();
        waitRect.anchorMin = Vector2.zero;
        waitRect.anchorMax = Vector2.one;
        waitRect.sizeDelta = Vector2.zero;

        // Waiting Text
        GameObject waitingTextObj = new GameObject("WaitingText");
        waitingTextObj.transform.SetParent(waitingPanel.transform, false);
        waitingText = waitingTextObj.AddComponent<Text>();
        waitingText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        waitingText.text = "Waiting for Players...";
        waitingText.fontSize = 28;
        waitingText.alignment = TextAnchor.MiddleCenter;
        waitingText.color = Color.yellow;
        RectTransform waitingTextRect = waitingTextObj.GetComponent<RectTransform>();
        waitingTextRect.anchoredPosition = new Vector2(0, 80);
        waitingTextRect.sizeDelta = new Vector2(500, 150);

        // Start Game Button (for Master Client)
        startButton = new GameObject("StartGameButton");
        startButton.transform.SetParent(waitingPanel.transform, false);
        Image startBtnImg = startButton.AddComponent<Image>();
        startBtnImg.color = new Color(0.2f, 0.5f, 0.8f);
        Button startBtn = startButton.AddComponent<Button>();
        RectTransform startBtnRect = startButton.GetComponent<RectTransform>();
        startBtnRect.anchoredPosition = new Vector2(0, -80);
        startBtnRect.sizeDelta = new Vector2(250, 60);

        // Start Button Text
        GameObject startBtnTextObj = new GameObject("StartBtnText");
        startBtnTextObj.transform.SetParent(startButton.transform, false);
        Text startBtnText = startBtnTextObj.AddComponent<Text>();
        startBtnText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        startBtnText.text = "START GAME NOW";
        startBtnText.fontSize = 20;
        startBtnText.alignment = TextAnchor.MiddleCenter;
        startBtnText.color = Color.white;
        RectTransform startBtnTextRect = startBtnTextObj.GetComponent<RectTransform>();
        startBtnTextRect.anchorMin = Vector2.zero;
        startBtnTextRect.anchorMax = Vector2.one;
        startBtnTextRect.sizeDelta = Vector2.zero;

        startBtn.onClick.AddListener(OnStartButtonClicked);
        startButton.SetActive(false);
    }
}
