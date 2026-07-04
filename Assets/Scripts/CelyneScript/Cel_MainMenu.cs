using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine.Playables;

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
    public GameObject panel4ToShow;
    public GameObject panel5ToShow;

    [Header("Stones to Show")]
    public GameObject[] stonesToShow;

    [Header("Room Text Controls")]
    public TMPro.TextMeshProUGUI headerText;
    public TMPro.TextMeshProUGUI createButtonText;
    public TMPro.TMP_InputField roomNameInput;
    public TMPro.TMP_InputField playerNameInput;
    public TMPro.TextMeshProUGUI player1Text;
    public TMPro.TextMeshProUGUI player2Text;
    public GameObject inputRoomCodeStone;
    public GameObject playerStone1;
    public GameObject playerStone2;

    [Header("Camera & Transitions Settings")]
    [Tooltip("PlayableDirector that plays the Menu Timeline instead of manual camera movement")]
    public PlayableDirector menuTimeline;
    public float transitionDuration = 0.2f;

    [Header("Cutscene Settings")]
    [Tooltip("Assign the PlayableDirector that plays the start game cutscene")]
    public PlayableDirector startTimeline;
    [Header("Transition Settings")]
    [Tooltip("Drag a UI Image/Panel GameObject here that is colored Black for fade transitions")]
    public GameObject blackScreenObject;
    public float fadeDuration = 1f;

    [Header("Audio Settings")]
    public AudioSource bgmSource;
    public AudioClip bgmClip;
    public AudioSource sfxSource;
    public AudioClip buttonClickClip;

    [Header("UI Canvas")]
    [Tooltip("Drag the main UI canvas here to hide it when the game starts")]
    public GameObject mainCanvas;

    [Header("Cameras")]
    public GameObject menuCamera;
    public GameObject cutsceneCamera;

    private CanvasGroup panelToShowCanvasGroup;
    private Vector3[] initialStonesScales;
    private Vector3[] initialStonesPositions;
    private Vector3 initialPanelPosition;
    private Vector3 playerStone1Scale;
    private Vector3 playerStone2Scale;

    private string pendingRoomToJoin = "";

    private void Start()
    {
        if (bgmSource != null && bgmClip != null)
        {
            bgmSource.clip = bgmClip;
            bgmSource.loop = true;
            bgmSource.Play();
        }

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
        // Configure initial states
        if (panelToHide != null)
        {
            panelToHide.SetActive(true);
        }
        else
        {
            // Auto-fallback: check if panelToShow is Panel (1)
            if (panelToShow != null && panelToShow.name == "Panel (1)")
            {
                panelToShow.SetActive(true);
            }
        }

        // Ensure Panels 3, 4, and 5 start hidden
        if (panel3ToShow != null) panel3ToShow.SetActive(false);
        if (panel4ToShow != null) panel4ToShow.SetActive(false);
        if (panel5ToShow != null) panel5ToShow.SetActive(false);

        if (panelToShow != null && panelToShow.name != "Panel (1)")
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
                    
                    // Do NOT hide or zero-scale Panel (1) Object, as it is the starting menu object
                    if (stone.name == "Panel (1) Object")
                    {
                        stone.SetActive(true);
                    }
                    else
                    {
                        // Offset stone Z by +5f (further back in the scene) and scale to 0
                        stone.transform.localPosition = initialStonesPositions[i] + new Vector3(0f, 0f, 5f);
                        stone.transform.localScale = Vector3.zero;
                        stone.SetActive(false); // Hide initially
                    }
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

    public void PlayClickSound()
    {
        if (sfxSource != null && buttonClickClip != null)
        {
            sfxSource.PlayOneShot(buttonClickClip);
        }
    }

    public void OnPlayClicked()
    {
        PlayClickSound();

        // Hide all specified objects
        foreach (GameObject obj in objectsToHide)
        {
            if (obj != null)
                obj.SetActive(false);
        }

        if (menuTimeline != null)
        {
            menuTimeline.Play();
        }
        
        StartTransition(panelToHide != null ? panelToHide : panelToShow, "Panel (1) Object", panel2ToShow, "Panel (2) Object");
    }

    public void OnConfirmNameClicked()
    {
        PlayClickSound();

        string name = playerNameInput != null ? playerNameInput.text : "";
        if (string.IsNullOrEmpty(name))
        {
            name = "Player_" + Random.Range(1000, 9999);
            if (playerNameInput != null) playerNameInput.text = name;
        }
        PhotonNetwork.NickName = name;

        StartTransition(panel2ToShow, "Panel (2) Object", panel3ToShow, "Panel (3) Object");
    }

    public void OnCreateGameClicked()
    {
        PlayClickSound();

        // 1. Generate a random 5-digit room ID/code
        string roomCode = Random.Range(10000, 99999).ToString();
        
        // 2. Set the header to show the room ID/code
        if (headerText != null)
        {
            headerText.text = "Room ID: " + roomCode;
        }

        // 3. Create the Photon room
        RoomOptions roomOptions = new RoomOptions { MaxPlayers = 2 };
        PhotonNetwork.CreateRoom(roomCode, roomOptions);
        
        // We do NOT call StartTransition here because OnJoinedRoom will be triggered automatically
        // and handle the transition to Panel (5), preventing the UI flicker/lag.
    }

    public void OnJoinGameClicked()
    {
        PlayClickSound();

        // Transition to Panel (4) so player can enter room ID/code
        StartTransition(panel3ToShow, "Panel (3) Object", panel4ToShow, "Panel (4) Object");
    }

    private void StartTransition(GameObject hidePanel, string hideObjectName, GameObject showPanel, string showObjectName)
    {
        StartCoroutine(TransitionRoutine(hidePanel, hideObjectName, showPanel, showObjectName));
    }

    private int GetStoneIndex(string stoneName)
    {
        if (stonesToShow == null) return -1;
        for (int i = 0; i < stonesToShow.Length; i++)
        {
            if (stonesToShow[i] != null && stonesToShow[i].name == stoneName)
            {
                return i;
            }
        }
        return -1;
    }

    private IEnumerator TransitionRoutine(GameObject hidePanel, string hideObjectName, GameObject showPanel, string showObjectName)
    {
        // 1. Hide the old Panel and old Object
        if (hidePanel != null)
        {
            hidePanel.SetActive(false);
        }
        int hideIdx = GetStoneIndex(hideObjectName);
        if (hideIdx != -1 && stonesToShow[hideIdx] != null)
        {
            stonesToShow[hideIdx].SetActive(false);
        }

        // 2. Prepare the new Panel (set up scale/position/alpha BEFORE SetActive)
        CanvasGroup cg = null;
        Vector3 initialPanelPos = Vector3.zero;
        Vector3 initialPanelScale = Vector3.one;
        if (showPanel != null)
        {
            cg = showPanel.GetComponent<CanvasGroup>();
            if (cg == null) cg = showPanel.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            initialPanelPos = showPanel.transform.localPosition;
            initialPanelScale = showPanel.transform.localScale;
            
            showPanel.transform.localPosition = initialPanelPos + new Vector3(0f, 0f, 5f);
            showPanel.transform.localScale = Vector3.zero; // Start at scale 0 for synchronized scale-up growth
            
            showPanel.SetActive(true);
        }

        // 3. Prepare the new 3D Stone Object (set up scale/position BEFORE SetActive)
        int showIdx = GetStoneIndex(showObjectName);
        GameObject showStone = (showIdx != -1) ? stonesToShow[showIdx] : null;
        Vector3 targetStoneScale = Vector3.one;
        Vector3 targetStonePos = Vector3.zero;
        if (showStone != null && showIdx != -1)
        {
            targetStoneScale = initialStonesScales[showIdx];
            targetStonePos = initialStonesPositions[showIdx];
            showStone.transform.localScale = Vector3.zero;
            showStone.transform.localPosition = targetStonePos + new Vector3(0f, 0f, 5f);
            
            showStone.SetActive(true);
        }

        // 5. Synchronized Animation (customizable duration)
        float duration = transitionDuration;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float progress = EaseOutCubic(t);
            float alpha = EaseOutQuad(t);

            float zOffset = Mathf.Lerp(5f, 0f, progress);

            // Animate Panel (Sync position, alpha, and scale-up growth)
            if (showPanel != null)
            {
                showPanel.transform.localPosition = initialPanelPos + new Vector3(0f, 0f, zOffset);
                showPanel.transform.localScale = initialPanelScale * progress;
            }
            if (cg != null)
            {
                cg.alpha = alpha;
            }

            // Animate Stone
            if (showStone != null)
            {
                showStone.transform.localPosition = targetStonePos + new Vector3(0f, 0f, zOffset);
                showStone.transform.localScale = targetStoneScale * progress;
            }

            yield return null;
        }

        // 6. Ensure exact final states
        if (showPanel != null)
        {
            showPanel.transform.localPosition = initialPanelPos;
            showPanel.transform.localScale = initialPanelScale;
        }
        if (cg != null)
        {
            cg.alpha = 1f;
        }
        if (showStone != null)
        {
            showStone.transform.localPosition = targetStonePos;
            showStone.transform.localScale = targetStoneScale;
        }
    }

    private float EaseOutCubic(float x)
    {
        return 1f - Mathf.Pow(1f - x, 3f);
    }

    private float EaseOutQuad(float x)
    {
        return 1f - (1f - x) * (1f - x);
    }



    public override void OnConnectedToMaster()
    {
        if (!string.IsNullOrEmpty(pendingRoomToJoin))
        {
            string roomToJoin = pendingRoomToJoin;
            pendingRoomToJoin = "";
            if (headerText != null) headerText.text = "Joining room " + roomToJoin + "...";
            PhotonNetwork.JoinRoom(roomToJoin);
        }
        else
        {
            if (headerText != null) headerText.text = "Connected. Joining Lobby...";
            PhotonNetwork.JoinLobby();
        }
    }

    public override void OnJoinedLobby()
    {
        if (headerText != null) headerText.text = "CREATE ROOM";
    }

    public void OnCreateRoomClicked()
    {
        PlayClickSound();

        // This is called when the creator clicks "Start" to launch the game
        if (PhotonNetwork.IsMasterClient)
        {
            if (headerText != null) headerText.text = "Starting Game...";
            
            // Hide Input Room Code Stone when clicking start
            if (inputRoomCodeStone != null)
            {
                inputRoomCodeStone.SetActive(false);
            }
            
            StartCoroutine(PlayCutsceneAndTransition());
        }
    }

    private IEnumerator PlayCutsceneAndTransition()
    {
        Debug.Log("[Transition] Started PlayCutsceneAndTransition");

        // Ensure black screen image color has full alpha if an Image component exists
        if (blackScreenObject != null)
        {
            UnityEngine.UI.Image bgImage = blackScreenObject.GetComponent<UnityEngine.UI.Image>();
            if (bgImage != null)
            {
                Color c = bgImage.color;
                c.a = 1f;
                bgImage.color = c;
            }
        }

        // 0. Fade to black
        if (blackScreenObject != null)
        {
            Debug.Log("[Transition] Fading to black...");
            blackScreenObject.SetActive(true);
            CanvasGroup cg = blackScreenObject.GetComponent<CanvasGroup>();
            if (cg == null) cg = blackScreenObject.AddComponent<CanvasGroup>();
            
            float fadeElapsed = 0f;
            while (fadeElapsed < fadeDuration)
            {
                fadeElapsed += Time.deltaTime;
                cg.alpha = Mathf.Clamp01(fadeElapsed / fadeDuration);
                yield return null;
            }
            cg.alpha = 1f;
        }

        // 1. Prepare cameras for transition (Direct Cut)
        if (menuCamera != null) menuCamera.SetActive(false);
        if (cutsceneCamera != null) cutsceneCamera.SetActive(true);

        // Hide main UI Canvas
        if (mainCanvas != null) mainCanvas.SetActive(false);

        // Stop BGM
        if (bgmSource != null) bgmSource.Stop();

        // 2. Play the Cutscene Timeline
        if (startTimeline != null)
        {
            Debug.Log("[Transition] Playing timeline...");
            startTimeline.Play();
            yield return null; // Wait one frame for the timeline state to update to Playing
        }
        
        // 3. Fade back in from black
        if (blackScreenObject != null)
        {
            Debug.Log("[Transition] Fading back in...");
            CanvasGroup cg = blackScreenObject.GetComponent<CanvasGroup>();
            float fadeElapsed = 0f;
            while (fadeElapsed < fadeDuration)
            {
                fadeElapsed += Time.deltaTime;
                cg.alpha = 1f - Mathf.Clamp01(fadeElapsed / fadeDuration);
                yield return null;
            }
            cg.alpha = 0f;
            blackScreenObject.SetActive(false);
        }

        // 4. Wait for the rest of the cutscene to finish naturally
        if (startTimeline != null)
        {
            Debug.Log("[Transition] Waiting for timeline to finish...");
            float maxWaitTime = (float)(startTimeline.duration > 0 ? startTimeline.duration + 5f : 15f); 
            float waitTimer = 0f;

            // Wait until it stops playing, but add a fallback timeout in case it's looping forever
            while (startTimeline.state == PlayState.Playing && waitTimer < maxWaitTime)
            {
                waitTimer += Time.deltaTime;
                yield return null;
            }
            
            if (waitTimer >= maxWaitTime)
            {
                Debug.LogWarning("[Transition] Timeline wait timed out! Is the timeline set to Loop?");
            }
            else
            {
                Debug.Log("[Transition] Timeline finished naturally.");
            }
        }

        // 5. Instantly load the next scene without any extra delay
        Debug.Log("[Transition] Attempting to load next scene: Puzzle1");
        if (PhotonNetwork.IsMasterClient)
        {
            Debug.Log("[Transition] Calling PhotonNetwork.LoadLevel(\"Puzzle1\")...");
            PhotonNetwork.LoadLevel("Puzzle1");
        }
        else
        {
            Debug.LogWarning("[Transition] Not MasterClient, waiting for MasterClient to load level.");
        }
    }

    public void OnJoinRoomConfirmClicked()
    {
        PlayClickSound();

        if (!PhotonNetwork.IsConnectedAndReady)
        {
            if (headerText != null) headerText.text = "Not connected to Photon!";
            return;
        }

        string roomName = roomNameInput != null ? roomNameInput.text : "";
        if (string.IsNullOrEmpty(roomName))
        {
            if (headerText != null) headerText.text = "Room ID Empty!";
            return;
        }

        if (PhotonNetwork.InRoom)
        {
            if (headerText != null) headerText.text = "Leaving current room...";
            pendingRoomToJoin = roomName;
            PhotonNetwork.LeaveRoom();
            return;
        }

        if (headerText != null) headerText.text = "Joining room " + roomName + "...";
        PhotonNetwork.JoinRoom(roomName);
    }

    public override void OnJoinedRoom()
    {
        if (headerText != null)
        {
            headerText.text = "Room ID: " + PhotonNetwork.CurrentRoom.Name;
        }

        // Hide Input Room Code Stone when entering the room
        if (inputRoomCodeStone != null)
        {
            inputRoomCodeStone.SetActive(false);
        }

        // Show player stones with scale animation
        StartCoroutine(RevealPlayerStones());

        // Transition from Join Room Panel (Panel 4) to Room Lobby Panel (Panel 5)
        if (panel4ToShow != null && panel4ToShow.activeSelf)
        {
            StartTransition(panel4ToShow, "Panel (4) Object", panel5ToShow, "Panel (5) Object");
        }
        else if (panel3ToShow != null && panel3ToShow.activeSelf)
        {
            StartTransition(panel3ToShow, "Panel (3) Object", panel5ToShow, "Panel (5) Object");
        }

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

        UpdateLobbyPlayers();
    }

    private void UpdateLobbyPlayers()
    {
        if (!PhotonNetwork.InRoom) return;

        var players = PhotonNetwork.CurrentRoom.Players;
        List<Player> pList = new List<Player>(players.Values);

        if (player1Text != null)
        {
            player1Text.text = pList.Count > 0 ? pList[0].NickName : "Waiting...";
        }
        if (player2Text != null)
        {
            player2Text.text = pList.Count > 1 ? pList[1].NickName : "Waiting...";
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

    public override void OnLeftRoom()
    {
        if (headerText != null) headerText.text = "Left Room.";
        // OnConnectedToMaster will automatically trigger next and handle pendingRoomToJoin or JoinLobby
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        UpdateLobbyPlayers();
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        UpdateLobbyPlayers();
        
        if (createButtonText != null)
        {
            createButtonText.text = PhotonNetwork.IsMasterClient ? "Start" : "Waiting...";
        }
    }

    public void OnExitClicked()
    {
        PlayClickSound();

        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    private void GoToPreviousMenu()
    {
        // 1. If Panel (2) is active, go back to Panel (1)
        if (panel2ToShow != null && panel2ToShow.activeSelf)
        {
            StartTransition(panel2ToShow, "Panel (2) Object", panelToHide != null ? panelToHide : panelToShow, "Panel (1) Object");

            // Show the main intro objects again
            foreach (GameObject obj in objectsToHide)
            {
                if (obj != null) obj.SetActive(true);
            }

            // Rewind and stop the timeline when going back to start
            if (menuTimeline != null)
            {
                menuTimeline.time = 0;
                menuTimeline.Evaluate();
                menuTimeline.Stop();
            }
        }
        // 2. If Panel (3) is active, go back to Panel (2)
        else if (panel3ToShow != null && panel3ToShow.activeSelf)
        {
            StartTransition(panel3ToShow, "Panel (3) Object", panel2ToShow, "Panel (2) Object");
        }
        // 3. If Panel (4) is active, go back to Panel (3)
        else if (panel4ToShow != null && panel4ToShow.activeSelf)
        {
            StartTransition(panel4ToShow, "Panel (4) Object", panel3ToShow, "Panel (3) Object");
        }
        // 4. If Panel (5) is active, leave lobby/room and go back to Panel (3)
        else if (panel5ToShow != null && panel5ToShow.activeSelf)
        {
            if (PhotonNetwork.InRoom)
            {
                PhotonNetwork.LeaveRoom();
            }
            StartTransition(panel5ToShow, "Panel (5) Object", panel3ToShow, "Panel (3) Object");
        }
    }

    void Update()
    {
        // Handle go back with escape key
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            PlayClickSound();
            GoToPreviousMenu();
        }
    }
}
