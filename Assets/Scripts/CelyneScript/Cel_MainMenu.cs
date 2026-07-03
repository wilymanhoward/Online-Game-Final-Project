using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

public class Cel_MainMenu : MonoBehaviourPunCallbacks
{
    [Header("Menu Objects")]
    [Tooltip("Put everything that needs to disappear here (Title, Buttons, Stone Block)")]
    public GameObject[] objectsToHide;

    [Header("Panels")]
    public GameObject panelToHide;
    public GameObject panelToShow;
    public GameObject panel2ToShow;
    public GameObject panel3ToShow;

    [Header("Stones to Show")]
    public GameObject[] stonesToShow;

    [Header("Room Text Controls")]
    public TMPro.TextMeshProUGUI headerText;
    public TMPro.TextMeshProUGUI createButtonText;
    public TMPro.TMP_InputField roomNameInput;
    public GameObject inputRoomCodeStone;
    public GameObject playerStone1;
    public GameObject playerStone2;

    [Header("Camera Movement")]
    public Transform mainCamera;
    public Transform targetCameraPosition;
    public float cameraMoveSpeed = 2f;

    private bool isMovingCamera = false;
    private CanvasGroup panelToShowCanvasGroup;
    private Vector3[] initialStonesScales;
    private Vector3[] initialStonesPositions;
    private Vector3 initialPanelPosition;
    private Vector3 playerStone1Scale;
    private Vector3 playerStone2Scale;

    private void Start()
    {
        // Initiate Photon connection
        PhotonNetwork.AutomaticallySyncScene = true;
        if (!PhotonNetwork.IsConnected)
        {
            if (headerText != null) headerText.text = "Connecting to Photon...";
            PhotonNetwork.ConnectUsingSettings();
        }
        else
        {
            if (headerText != null) headerText.text = "CREATE ROOM";
        }

        // Ensure cursor is visible and unlocked on menu load
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Configure initial states
        if (panelToHide != null)
        {
            panelToHide.SetActive(true);
        }

        if (panelToShow != null)
        {
            panelToShow.SetActive(false);
            initialPanelPosition = panelToShow.transform.localPosition;
            
            // Offset panel Z by +5f initially (further back in the scene)
            panelToShow.transform.localPosition = initialPanelPosition + new Vector3(0f, 0f, 5f);
            
            // Add or get CanvasGroup to handle smooth fading
            panelToShowCanvasGroup = panelToShow.GetComponent<CanvasGroup>();
            if (panelToShowCanvasGroup == null)
            {
                panelToShowCanvasGroup = panelToShow.AddComponent<CanvasGroup>();
            }
            panelToShowCanvasGroup.alpha = 0f;
        }

        // Hide and offset all stones initially, caching their original transforms
        if (stonesToShow != null)
        {
            initialStonesScales = new Vector3[stonesToShow.Length];
            initialStonesPositions = new Vector3[stonesToShow.Length];
            for (int i = 0; i < stonesToShow.Length; i++)
            {
                GameObject stone = stonesToShow[i];
                if (stone != null)
                {
                    initialStonesScales[i] = stone.transform.localScale;
                    initialStonesPositions[i] = stone.transform.localPosition;
                    
                    // Offset stone Z by +5f (further back in the scene) and scale to 0
                    stone.transform.localPosition = initialStonesPositions[i] + new Vector3(0f, 0f, 5f);
                    stone.transform.localScale = Vector3.zero;
                    stone.SetActive(true); // Keep active so it can receive scale/position animation
                }
            }
        }

        // Cache and hide player stones
        if (playerStone1 != null)
        {
            playerStone1Scale = playerStone1.transform.localScale;
            playerStone1.transform.localScale = Vector3.zero;
            playerStone1.SetActive(false);
        }
        if (playerStone2 != null)
        {
            playerStone2Scale = playerStone2.transform.localScale;
            playerStone2.transform.localScale = Vector3.zero;
            playerStone2.SetActive(false);
        }
    }

    public void OnPlayClicked()
    {
        // Hide all specified objects
        foreach (GameObject obj in objectsToHide)
        {
            if (obj != null)
                obj.SetActive(false);
        }

        // Hide Panel (1) - the main intro panel
        if (panelToHide != null)
        {
            panelToHide.SetActive(false);
        }

        // Hide the former 'panelToShow' (old lobby UI) if assigned
        if (panelToShow != null)
        {
            panelToShow.SetActive(false);
        }

        // Show Panel (2) (player name input)
        if (panel2ToShow != null)
        {
            panel2ToShow.SetActive(true);
        }

        // Show Panel (3) (Create Game / Join Game / Exit buttons)
        if (panel3ToShow != null)
        {
            panel3ToShow.SetActive(true);
        }

        // Start movement and reveal animations
        isMovingCamera = true;
        StartCoroutine(AnimateStonesAndUI());
    }

    private IEnumerator AnimateStonesAndUI()
    {
        float duration = 0.8f;
        float elapsed = 0f;

        // Cache initial positions/scales or just scale up from 0 to 1
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            // Easing curves (easeOutCubic for smooth, calm scaling and positioning)
            float animProgress = EaseOutCubic(t);
            float uiAlpha = EaseOutQuad(t);

            // Calculate the Z-axis offset (starts at +5f and goes down to 0f)
            float zOffset = Mathf.Lerp(5f, 0f, animProgress);

            // Position and scale the stones relative to their initial states
            if (stonesToShow != null && initialStonesScales != null && initialStonesPositions != null)
            {
                for (int i = 0; i < stonesToShow.Length; i++)
                {
                    GameObject stone = stonesToShow[i];
                    if (stone != null && i < initialStonesScales.Length && i < initialStonesPositions.Length)
                    {
                        stone.transform.localPosition = initialStonesPositions[i] + new Vector3(0f, 0f, zOffset);
                        stone.transform.localScale = initialStonesScales[i] * animProgress;
                    }
                }
            }

            // Position and fade in the Panel (1) UI
            if (panelToShow != null)
            {
                panelToShow.transform.localPosition = initialPanelPosition + new Vector3(0f, 0f, zOffset);
            }
            if (panelToShowCanvasGroup != null)
            {
                panelToShowCanvasGroup.alpha = uiAlpha;
            }

            yield return null;
        }

        // Ensure final values are set perfectly
        if (stonesToShow != null && initialStonesScales != null && initialStonesPositions != null)
        {
            for (int i = 0; i < stonesToShow.Length; i++)
            {
                GameObject stone = stonesToShow[i];
                if (stone != null && i < initialStonesScales.Length && i < initialStonesPositions.Length)
                {
                    stone.transform.localPosition = initialStonesPositions[i];
                    stone.transform.localScale = initialStonesScales[i];
                }
            }
        }

        if (panelToShow != null)
        {
            panelToShow.transform.localPosition = initialPanelPosition;
        }
        if (panelToShowCanvasGroup != null)
        {
            panelToShowCanvasGroup.alpha = 1f;
        }
    }

    // Ease Out Cubic curve for a smooth, calm transition
    private float EaseOutCubic(float x)
    {
        return 1f - Mathf.Pow(1f - x, 3f);
    }

    // Ease Out Quad curve for smooth fade
    private float EaseOutQuad(float x)
    {
        return 1f - (1f - x) * (1f - x);
    }

    public override void OnConnectedToMaster()
    {
        if (headerText != null) headerText.text = "Connected. Joining Lobby...";
        PhotonNetwork.JoinLobby();
    }

    public override void OnJoinedLobby()
    {
        if (headerText != null) headerText.text = "CREATE ROOM";
    }

    public void OnCreateRoomClicked()
    {
        if (createButtonText != null && createButtonText.text == "Start")
        {
            if (PhotonNetwork.IsMasterClient)
            {
                if (headerText != null) headerText.text = "Starting Game...";
                
                // Hide Input Room Code Stone when clicking start
                if (inputRoomCodeStone != null)
                {
                    inputRoomCodeStone.SetActive(false);
                }
                
                PhotonNetwork.LoadLevel("SampleScene");
            }
            return;
        }

        string roomName = roomNameInput != null ? roomNameInput.text : "";
        if (string.IsNullOrEmpty(roomName))
        {
            if (headerText != null) headerText.text = "Room Name Empty!";
            return;
        }

        // Hide InputField (TMP) on successful click/creation
        if (roomNameInput != null)
        {
            roomNameInput.gameObject.SetActive(false);
        }

        PhotonNetwork.NickName = "Player_" + Random.Range(1000, 9999);
        if (headerText != null) headerText.text = "Creating room...";
        RoomOptions roomOptions = new RoomOptions { MaxPlayers = 2 };
        PhotonNetwork.CreateRoom(roomName, roomOptions);
    }

    public void OnJoinGameClicked()
    {
        string roomName = roomNameInput != null ? roomNameInput.text : "";
        if (string.IsNullOrEmpty(roomName))
        {
            if (headerText != null) headerText.text = "Room Name Empty!";
            return;
        }

        // Hide InputField (TMP) on successful click/join
        if (roomNameInput != null)
        {
            roomNameInput.gameObject.SetActive(false);
        }

        PhotonNetwork.NickName = "Player_" + Random.Range(1000, 9999);
        if (headerText != null) headerText.text = "Joining room...";
        PhotonNetwork.JoinRoom(roomName);
    }

    public override void OnJoinedRoom()
    {
        if (headerText != null)
        {
            headerText.text = "Player In room";
        }

        // Hide Input Room Code Stone when entering the room
        if (inputRoomCodeStone != null)
        {
            inputRoomCodeStone.SetActive(false);
        }

        // Show player stones with scale animation
        StartCoroutine(RevealPlayerStones());

        if (createButtonText != null)
        {
            if (PhotonNetwork.IsMasterClient)
            {
                createButtonText.text = "Start";
            }
            else
            {
                createButtonText.text = "Waiting...";
            }
        }
    }

    private IEnumerator RevealPlayerStones()
    {
        if (playerStone1 != null) playerStone1.SetActive(true);
        if (playerStone2 != null) playerStone2.SetActive(true);

        float duration = 0.8f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float progress = EaseOutCubic(t);

            if (playerStone1 != null)
            {
                playerStone1.transform.localScale = playerStone1Scale * progress;
            }
            if (playerStone2 != null)
            {
                playerStone2.transform.localScale = playerStone2Scale * progress;
            }

            yield return null;
        }

        if (playerStone1 != null) playerStone1.transform.localScale = playerStone1Scale;
        if (playerStone2 != null) playerStone2.transform.localScale = playerStone2Scale;
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        if (headerText != null) headerText.text = "Create Failed: " + message;
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        if (headerText != null) headerText.text = "Join Failed: " + message;
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        // Highlight status when player joins
        if (headerText != null)
        {
            headerText.text = "Player In room";
        }
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        // Revert status if player leaves
        if (headerText != null)
        {
            headerText.text = PhotonNetwork.IsMasterClient ? "CREATE ROOM" : "Player In room";
        }
        if (createButtonText != null)
        {
            createButtonText.text = PhotonNetwork.IsMasterClient ? "CREATE" : "Waiting...";
        }
    }

    public void OnExitClicked()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    void Update()
    {
        if (isMovingCamera && mainCamera != null && targetCameraPosition != null)
        {
            // Smoothly move the camera to the target position and rotation
            mainCamera.position = Vector3.Lerp(mainCamera.position, targetCameraPosition.position, Time.deltaTime * cameraMoveSpeed);
            mainCamera.rotation = Quaternion.Slerp(mainCamera.rotation, targetCameraPosition.rotation, Time.deltaTime * cameraMoveSpeed);
            
            // Stop moving if close enough
            if (Vector3.Distance(mainCamera.position, targetCameraPosition.position) < 0.01f)
            {
                mainCamera.position = targetCameraPosition.position;
                mainCamera.rotation = targetCameraPosition.rotation;
                isMovingCamera = false;
            }
        }
    }
}
