using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using Photon.Realtime;
using System.Collections.Generic;

public class LobbyManager : MonoBehaviourPunCallbacks
{
    [Header("UI Panels")]
    public GameObject lobbyPanel;
    public GameObject waitingPanel;

    [Header("Lobby Controls")]
    public InputField nicknameInput;
    public InputField roomNameInput;
    public Button createRoomButton;
    public Transform roomListContent;
    public GameObject roomItemPrefab;
    public Text statusText;

    [Header("Waiting Room Controls")]
    public Text waitingText;
    public GameObject startButton;
    public Button leaveRoomButton;

    private const string GAME_SCENE_NAME = "SampleScene";
    private Dictionary<string, RoomInfo> cachedRoomList = new Dictionary<string, RoomInfo>();

    void Start()
    {
        PhotonNetwork.AutomaticallySyncScene = true;

        // Bind button listeners dynamically at runtime to ensure they are persistently registered
        if (createRoomButton != null)
        {
            createRoomButton.onClick.RemoveAllListeners();
            createRoomButton.onClick.AddListener(OnCreateRoomButtonClicked);
        }
        if (leaveRoomButton != null)
        {
            leaveRoomButton.onClick.RemoveAllListeners();
            leaveRoomButton.onClick.AddListener(OnLeaveRoomButtonClicked);
        }
        if (startButton != null)
        {
            Button startBtn = startButton.GetComponent<Button>();
            if (startBtn != null)
            {
                startBtn.onClick.RemoveAllListeners();
                startBtn.onClick.AddListener(OnStartButtonClicked);
            }
        }

        if (statusText != null) statusText.text = "Connecting to Photon Network...";
        if (createRoomButton != null) createRoomButton.interactable = false;
        
        if (lobbyPanel != null) lobbyPanel.SetActive(true);
        if (waitingPanel != null) waitingPanel.SetActive(false);

        // Load cached nickname
        if (nicknameInput != null)
        {
            string nickName = PlayerPrefs.GetString("PlayerNickname", "Player" + Random.Range(1000, 9999));
            nicknameInput.text = nickName;
        }

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
        if (statusText != null) statusText.text = "Connected to Master. Joining Lobby...";
        PhotonNetwork.JoinLobby();
    }

    public override void OnJoinedLobby()
    {
        if (statusText != null) statusText.text = "Joined Lobby. Ready to create/join rooms!";
        if (createRoomButton != null) createRoomButton.interactable = true;
        cachedRoomList.Clear();
        UpdateRoomListView();
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        if (statusText != null) statusText.text = "Disconnected: " + cause.ToString() + ". Reconnecting...";
        if (createRoomButton != null) createRoomButton.interactable = false;
        if (lobbyPanel != null) lobbyPanel.SetActive(true);
        if (waitingPanel != null) waitingPanel.SetActive(false);
        PhotonNetwork.ConnectUsingSettings();
    }

    public void OnCreateRoomButtonClicked()
    {
        string roomName = roomNameInput != null ? roomNameInput.text : "";
        if (string.IsNullOrEmpty(roomName))
        {
            if (statusText != null) statusText.text = "Room Name cannot be empty!";
            return;
        }

        // Set nickname
        string nickname = nicknameInput != null ? nicknameInput.text : "Player";
        PhotonNetwork.NickName = nickname;
        PlayerPrefs.SetString("PlayerNickname", nickname);

        if (statusText != null) statusText.text = "Creating room: " + roomName + "...";
        RoomOptions roomOptions = new RoomOptions { MaxPlayers = 2 };
        PhotonNetwork.CreateRoom(roomName, roomOptions);
    }

    public void JoinRoom(string roomName)
    {
        string nickname = nicknameInput != null ? nicknameInput.text : "Player";
        PhotonNetwork.NickName = nickname;
        PlayerPrefs.SetString("PlayerNickname", nickname);

        if (statusText != null) statusText.text = "Joining room: " + roomName + "...";
        PhotonNetwork.JoinRoom(roomName);
    }

    public override void OnJoinedRoom()
    {
        if (statusText != null) statusText.text = "Joined Room: " + PhotonNetwork.CurrentRoom.Name;
        if (lobbyPanel != null) lobbyPanel.SetActive(false);
        if (waitingPanel != null) waitingPanel.SetActive(true);
        UpdateWaitingStatus();
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        if (statusText != null) statusText.text = "Failed to create room: " + message;
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        if (statusText != null) statusText.text = "Failed to join room: " + message;
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        UpdateWaitingStatus();
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        UpdateWaitingStatus();
    }

    public void OnLeaveRoomButtonClicked()
    {
        if (statusText != null) statusText.text = "Leaving room...";
        PhotonNetwork.LeaveRoom();
    }

    public override void OnLeftRoom()
    {
        if (statusText != null) statusText.text = "Returned to Lobby.";
        if (lobbyPanel != null) lobbyPanel.SetActive(true);
        if (waitingPanel != null) waitingPanel.SetActive(false);
        PhotonNetwork.JoinLobby();
    }

    private void UpdateWaitingStatus()
    {
        if (PhotonNetwork.CurrentRoom != null)
        {
            string playersInfo = "Players in Room:\n";
            foreach (var kvp in PhotonNetwork.CurrentRoom.Players)
            {
                playersInfo += $"- {kvp.Value.NickName} {(kvp.Value.IsLocal ? "(You)" : "")}\n";
            }
            
            int playerCount = PhotonNetwork.CurrentRoom.PlayerCount;
            int maxPlayers = PhotonNetwork.CurrentRoom.MaxPlayers;
            
            if (waitingText != null)
            {
                waitingText.text = $"Room: {PhotonNetwork.CurrentRoom.Name}\n({playerCount} / {maxPlayers})\n\n{playersInfo}";
            }

            if (startButton != null)
            {
                startButton.SetActive(PhotonNetwork.IsMasterClient);
            }
        }
    }

    public void OnStartButtonClicked()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            if (statusText != null) statusText.text = "Starting game...";
            PhotonNetwork.LoadLevel(GAME_SCENE_NAME);
        }
    }

    public override void OnRoomListUpdate(List<RoomInfo> roomList)
    {
        foreach (RoomInfo room in roomList)
        {
            if (room.RemovedFromList)
            {
                cachedRoomList.Remove(room.Name);
            }
            else
            {
                cachedRoomList[room.Name] = room;
            }
        }
        UpdateRoomListView();
    }

    private void UpdateRoomListView()
    {
        if (roomListContent == null || roomItemPrefab == null) return;

        // Clear existing room list items
        foreach (Transform child in roomListContent)
        {
            Destroy(child.gameObject);
        }

        // Populate room list items
        foreach (var entry in cachedRoomList)
        {
            RoomInfo room = entry.Value;
            if (room.IsOpen && room.IsVisible && room.PlayerCount > 0)
            {
                GameObject itemObj = Instantiate(roomItemPrefab, roomListContent);
                
                // Set name text
                Text roomNameText = itemObj.transform.Find("RoomNameText")?.GetComponent<Text>();
                if (roomNameText != null)
                {
                    roomNameText.text = room.Name;
                }

                // Set players count text
                Text playerCountText = itemObj.transform.Find("PlayerCountText")?.GetComponent<Text>();
                if (playerCountText != null)
                {
                    playerCountText.text = $"{room.PlayerCount}/{room.MaxPlayers}";
                }

                // Set join button listener
                Button joinBtn = itemObj.transform.Find("JoinButton")?.GetComponent<Button>();
                if (joinBtn != null)
                {
                    joinBtn.onClick.AddListener(() => JoinRoom(room.Name));
                }
            }
        }
    }
}
