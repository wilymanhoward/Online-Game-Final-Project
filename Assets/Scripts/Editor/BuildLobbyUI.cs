using UnityEngine;
using UnityEngine.UI;
using UnityEditor;

public static class BuildLobbyUI
{
    [MenuItem("Tools/Build Lobby UI")]
    public static void Execute()
    {
        // 1. Create RoomItem Prefab
        GameObject roomItemObj = new GameObject("RoomItem");
        RectTransform itemRect = roomItemObj.AddComponent<RectTransform>();
        itemRect.sizeDelta = new Vector2(280f, 40f);

        Image itemBg = roomItemObj.AddComponent<Image>();
        itemBg.color = new Color(0.2f, 0.2f, 0.28f, 0.8f);

        // RoomNameText
        GameObject nameTextObj = new GameObject("RoomNameText");
        nameTextObj.transform.SetParent(roomItemObj.transform, false);
        Text nameText = nameTextObj.AddComponent<Text>();
        nameText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        nameText.text = "Room Name";
        nameText.fontSize = 14;
        nameText.alignment = TextAnchor.MiddleLeft;
        nameText.color = Color.white;
        RectTransform nameTextRect = nameTextObj.GetComponent<RectTransform>();
        nameTextRect.anchorMin = new Vector2(0f, 0.5f);
        nameTextRect.anchorMax = new Vector2(0f, 0.5f);
        nameTextRect.pivot = new Vector2(0f, 0.5f);
        nameTextRect.anchoredPosition = new Vector2(10f, 0f);
        nameTextRect.sizeDelta = new Vector2(120f, 30f);

        // PlayerCountText
        GameObject countTextObj = new GameObject("PlayerCountText");
        countTextObj.transform.SetParent(roomItemObj.transform, false);
        Text countText = countTextObj.AddComponent<Text>();
        countText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        countText.text = "1/4";
        countText.fontSize = 12;
        countText.alignment = TextAnchor.MiddleCenter;
        countText.color = Color.yellow;
        RectTransform countTextRect = countTextObj.GetComponent<RectTransform>();
        countTextRect.anchorMin = new Vector2(0.55f, 0.5f);
        countTextRect.anchorMax = new Vector2(0.55f, 0.5f);
        countTextRect.pivot = new Vector2(0.5f, 0.5f);
        countTextRect.anchoredPosition = new Vector2(0f, 0f);
        countTextRect.sizeDelta = new Vector2(40f, 30f);

        // JoinButton
        GameObject joinButtonObj = new GameObject("JoinButton");
        joinButtonObj.transform.SetParent(roomItemObj.transform, false);
        Image btnImg = joinButtonObj.AddComponent<Image>();
        btnImg.color = new Color(0.1f, 0.6f, 0.3f);
        Button joinBtn = joinButtonObj.AddComponent<Button>();
        RectTransform btnRect = joinButtonObj.GetComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(1f, 0.5f);
        btnRect.anchorMax = new Vector2(1f, 0.5f);
        btnRect.pivot = new Vector2(1f, 0.5f);
        btnRect.anchoredPosition = new Vector2(-10f, 0f);
        btnRect.sizeDelta = new Vector2(60f, 25f);

        // ButtonText
        GameObject btnTextObj = new GameObject("ButtonText");
        btnTextObj.transform.SetParent(joinButtonObj.transform, false);
        Text btnText = btnTextObj.AddComponent<Text>();
        btnText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        btnText.text = "JOIN";
        btnText.fontSize = 11;
        btnText.alignment = TextAnchor.MiddleCenter;
        btnText.color = Color.white;
        RectTransform btnTextRect = btnTextObj.GetComponent<RectTransform>();
        btnTextRect.anchorMin = Vector2.zero;
        btnTextRect.anchorMax = Vector2.one;
        btnTextRect.sizeDelta = Vector2.zero;

        // Save as Prefab
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
        {
            AssetDatabase.CreateFolder("Assets", "Resources");
        }
        string prefabPath = "Assets/Resources/RoomItemPrefab.prefab";
        GameObject roomItemPrefab = PrefabUtility.SaveAsPrefabAsset(roomItemObj, prefabPath);
        UnityEngine.Object.DestroyImmediate(roomItemObj);


        // 2. Find LobbyManager
        LobbyManager lobbyManager = UnityEngine.Object.FindObjectOfType<LobbyManager>();
        if (lobbyManager == null)
        {
            GameObject lobbyManagerObj = new GameObject("LobbyManager");
            lobbyManager = lobbyManagerObj.AddComponent<LobbyManager>();
        }

        // Destroy any existing LobbyCanvas
        GameObject existingCanvas = GameObject.Find("LobbyCanvas");
        if (existingCanvas != null)
        {
            UnityEngine.Object.DestroyImmediate(existingCanvas);
        }

        // Create Canvas
        GameObject canvasObj = new GameObject("LobbyCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();

        // Create EventSystem if not exists
        if (UnityEngine.Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            GameObject eventSystemObj = new GameObject("EventSystem");
            eventSystemObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystemObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        // Background
        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(canvasObj.transform, false);
        Image bgImg = bgObj.AddComponent<Image>();
        bgImg.color = new Color(0.08f, 0.08f, 0.12f, 1f);
        RectTransform bgRect = bgObj.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;

        // 3. Lobby Panel
        GameObject lobbyPanelObj = new GameObject("LobbyPanel");
        lobbyPanelObj.transform.SetParent(canvasObj.transform, false);
        RectTransform lobbyPanelRect = lobbyPanelObj.AddComponent<RectTransform>();
        lobbyPanelRect.anchorMin = Vector2.zero;
        lobbyPanelRect.anchorMax = Vector2.one;
        lobbyPanelRect.sizeDelta = Vector2.zero;

        // Title
        GameObject titleObj = new GameObject("TitleText");
        titleObj.transform.SetParent(lobbyPanelObj.transform, false);
        Text titleText = titleObj.AddComponent<Text>();
        titleText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        titleText.text = "ANCIENT EGYPT MULTIPLAYER";
        titleText.fontSize = 32;
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.color = new Color(1.0f, 0.8f, 0.2f);
        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchoredPosition = new Vector2(0f, 180f);
        titleRect.sizeDelta = new Vector2(600f, 60f);

        // Status Text
        GameObject statusObj = new GameObject("StatusText");
        statusObj.transform.SetParent(lobbyPanelObj.transform, false);
        Text statusTextComp = statusObj.AddComponent<Text>();
        statusTextComp.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        statusTextComp.text = "Connecting to Photon Network...";
        statusTextComp.fontSize = 18;
        statusTextComp.alignment = TextAnchor.MiddleCenter;
        statusTextComp.color = Color.white;
        RectTransform statusRect = statusObj.GetComponent<RectTransform>();
        statusRect.anchoredPosition = new Vector2(0f, 120f);
        statusRect.sizeDelta = new Vector2(500f, 40f);

        // Nickname InputField
        GameObject nickInputObj = new GameObject("NicknameInput");
        nickInputObj.transform.SetParent(lobbyPanelObj.transform, false);
        Image nickInputImg = nickInputObj.AddComponent<Image>();
        nickInputImg.color = new Color(0.2f, 0.2f, 0.25f, 1f);
        InputField nickInput = nickInputObj.AddComponent<InputField>();
        RectTransform nickInputRect = nickInputObj.GetComponent<RectTransform>();
        nickInputRect.anchoredPosition = new Vector2(-160f, 30f);
        nickInputRect.sizeDelta = new Vector2(250f, 40f);

        GameObject nickPlaceholderObj = new GameObject("Placeholder");
        nickPlaceholderObj.transform.SetParent(nickInputObj.transform, false);
        Text nickPlaceholder = nickPlaceholderObj.AddComponent<Text>();
        nickPlaceholder.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        nickPlaceholder.text = "Enter nickname...";
        nickPlaceholder.fontSize = 16;
        nickPlaceholder.fontStyle = FontStyle.Italic;
        nickPlaceholder.color = Color.gray;
        nickPlaceholder.alignment = TextAnchor.MiddleLeft;
        RectTransform nickPlaceholderRect = nickPlaceholderObj.GetComponent<RectTransform>();
        nickPlaceholderRect.anchorMin = Vector2.zero;
        nickPlaceholderRect.anchorMax = Vector2.one;
        nickPlaceholderRect.sizeDelta = new Vector2(-10f, -10f);

        GameObject nickTextObj = new GameObject("Text");
        nickTextObj.transform.SetParent(nickInputObj.transform, false);
        Text nickText = nickTextObj.AddComponent<Text>();
        nickText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        nickText.fontSize = 16;
        nickText.color = Color.white;
        nickText.alignment = TextAnchor.MiddleLeft;
        RectTransform nickTextRect = nickTextObj.GetComponent<RectTransform>();
        nickTextRect.anchorMin = Vector2.zero;
        nickTextRect.anchorMax = Vector2.one;
        nickTextRect.sizeDelta = new Vector2(-10f, -10f);

        nickInput.placeholder = nickPlaceholder;
        nickInput.textComponent = nickText;

        // Room Name InputField
        GameObject roomInputObj = new GameObject("RoomNameInput");
        roomInputObj.transform.SetParent(lobbyPanelObj.transform, false);
        Image roomInputImg = roomInputObj.AddComponent<Image>();
        roomInputImg.color = new Color(0.2f, 0.2f, 0.25f, 1f);
        InputField roomInput = roomInputObj.AddComponent<InputField>();
        RectTransform roomInputRect = roomInputObj.GetComponent<RectTransform>();
        roomInputRect.anchoredPosition = new Vector2(-160f, -30f);
        roomInputRect.sizeDelta = new Vector2(250f, 40f);

        GameObject roomPlaceholderObj = new GameObject("Placeholder");
        roomPlaceholderObj.transform.SetParent(roomInputObj.transform, false);
        Text roomPlaceholder = roomPlaceholderObj.AddComponent<Text>();
        roomPlaceholder.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        roomPlaceholder.text = "Enter room name...";
        roomPlaceholder.fontSize = 16;
        roomPlaceholder.fontStyle = FontStyle.Italic;
        roomPlaceholder.color = Color.gray;
        roomPlaceholder.alignment = TextAnchor.MiddleLeft;
        RectTransform roomPlaceholderRect = roomPlaceholderObj.GetComponent<RectTransform>();
        roomPlaceholderRect.anchorMin = Vector2.zero;
        roomPlaceholderRect.anchorMax = Vector2.one;
        roomPlaceholderRect.sizeDelta = new Vector2(-10f, -10f);

        GameObject roomTextObj = new GameObject("Text");
        roomTextObj.transform.SetParent(roomInputObj.transform, false);
        Text roomText = roomTextObj.AddComponent<Text>();
        roomText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        roomText.fontSize = 16;
        roomText.color = Color.white;
        roomText.alignment = TextAnchor.MiddleLeft;
        RectTransform roomTextRect = roomTextObj.GetComponent<RectTransform>();
        roomTextRect.anchorMin = Vector2.zero;
        roomTextRect.anchorMax = Vector2.one;
        roomTextRect.sizeDelta = new Vector2(-10f, -10f);

        roomInput.placeholder = roomPlaceholder;
        roomInput.textComponent = roomText;

        // Create Room Button
        GameObject createButtonObj = new GameObject("CreateRoomButton");
        createButtonObj.transform.SetParent(lobbyPanelObj.transform, false);
        Image createBtnImg = createButtonObj.AddComponent<Image>();
        createBtnImg.color = new Color(0.1f, 0.6f, 0.3f);
        Button createBtn = createButtonObj.AddComponent<Button>();
        RectTransform createBtnRect = createButtonObj.GetComponent<RectTransform>();
        createBtnRect.anchoredPosition = new Vector2(-160f, -90f);
        createBtnRect.sizeDelta = new Vector2(250f, 45f);

        GameObject createBtnTextObj = new GameObject("Text");
        createBtnTextObj.transform.SetParent(createButtonObj.transform, false);
        Text createBtnText = createBtnTextObj.AddComponent<Text>();
        createBtnText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        createBtnText.text = "CREATE ROOM";
        createBtnText.fontSize = 16;
        createBtnText.alignment = TextAnchor.MiddleCenter;
        createBtnText.color = Color.white;
        RectTransform createBtnTextRect = createBtnTextObj.GetComponent<RectTransform>();
        createBtnTextRect.anchorMin = Vector2.zero;
        createBtnTextRect.anchorMax = Vector2.one;
        createBtnTextRect.sizeDelta = Vector2.zero;

        // Room List Panel
        GameObject listPanelObj = new GameObject("RoomListPanel");
        listPanelObj.transform.SetParent(lobbyPanelObj.transform, false);
        Image listPanelImg = listPanelObj.AddComponent<Image>();
        listPanelImg.color = new Color(0.12f, 0.12f, 0.18f, 0.9f);
        RectTransform listPanelRect = listPanelObj.GetComponent<RectTransform>();
        listPanelRect.anchoredPosition = new Vector2(160f, -30f);
        listPanelRect.sizeDelta = new Vector2(300f, 220f);

        // List Header Text
        GameObject headerObj = new GameObject("HeaderText");
        headerObj.transform.SetParent(listPanelObj.transform, false);
        Text headerText = headerObj.AddComponent<Text>();
        headerText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        headerText.text = "AVAILABLE ROOMS";
        headerText.fontSize = 16;
        headerText.alignment = TextAnchor.MiddleCenter;
        headerText.color = new Color(1.0f, 0.8f, 0.2f);
        RectTransform headerRect = headerObj.GetComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0f, 1f);
        headerRect.anchorMax = new Vector2(1f, 1f);
        headerRect.pivot = new Vector2(0.5f, 1f);
        headerRect.anchoredPosition = new Vector2(0f, -10f);
        headerRect.sizeDelta = new Vector2(0f, 30f);

        // ScrollRect
        GameObject scrollRectObj = new GameObject("ScrollRect");
        scrollRectObj.transform.SetParent(listPanelObj.transform, false);
        ScrollRect scrollRect = scrollRectObj.AddComponent<ScrollRect>();
        RectTransform scrollRectTrans = scrollRectObj.GetComponent<RectTransform>();
        scrollRectTrans.anchorMin = Vector2.zero;
        scrollRectTrans.anchorMax = new Vector2(1f, 0.85f);
        scrollRectTrans.sizeDelta = new Vector2(-20f, -10f);

        // Viewport
        GameObject viewportObj = new GameObject("Viewport");
        viewportObj.transform.SetParent(scrollRectObj.transform, false);
        Image vpImg = viewportObj.AddComponent<Image>();
        vpImg.color = new Color(0f, 0f, 0f, 0.1f);
        Mask vpMask = viewportObj.AddComponent<Mask>();
        vpMask.showMaskGraphic = false;
        RectTransform vpRect = viewportObj.GetComponent<RectTransform>();
        vpRect.anchorMin = Vector2.zero;
        vpRect.anchorMax = Vector2.one;
        vpRect.sizeDelta = Vector2.zero;

        // Content
        GameObject contentObj = new GameObject("Content");
        contentObj.transform.SetParent(viewportObj.transform, false);
        RectTransform contentRect = contentObj.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0f, 0f);

        VerticalLayoutGroup layoutGroup = contentObj.AddComponent<VerticalLayoutGroup>();
        layoutGroup.spacing = 5f;
        layoutGroup.childAlignment = TextAnchor.UpperCenter;
        layoutGroup.childControlWidth = true;
        layoutGroup.childControlHeight = false;
        layoutGroup.childForceExpandWidth = true;
        layoutGroup.childForceExpandHeight = false;

        ContentSizeFitter fitter = contentObj.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.viewport = vpRect;
        scrollRect.content = contentRect;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;

        // 4. Waiting Panel
        GameObject waitPanelObj = new GameObject("WaitingPanel");
        waitPanelObj.transform.SetParent(canvasObj.transform, false);
        RectTransform waitPanelRect = waitPanelObj.AddComponent<RectTransform>();
        waitPanelRect.anchorMin = Vector2.zero;
        waitPanelRect.anchorMax = Vector2.one;
        waitPanelRect.sizeDelta = Vector2.zero;

        // Waiting Text
        GameObject waitingTextObj = new GameObject("WaitingText");
        waitingTextObj.transform.SetParent(waitPanelObj.transform, false);
        Text waitingTextComp = waitingTextObj.AddComponent<Text>();
        waitingTextComp.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        waitingTextComp.text = "Waiting for Players...";
        waitingTextComp.fontSize = 24;
        waitingTextComp.alignment = TextAnchor.MiddleCenter;
        waitingTextComp.color = Color.yellow;
        RectTransform waitingTextRect = waitingTextObj.GetComponent<RectTransform>();
        waitingTextRect.anchoredPosition = new Vector2(0f, 50f);
        waitingTextRect.sizeDelta = new Vector2(500f, 200f);

        // Start Game Button
        GameObject startBtnObj = new GameObject("StartGameButton");
        startBtnObj.transform.SetParent(waitPanelObj.transform, false);
        Image startBtnImg = startBtnObj.AddComponent<Image>();
        startBtnImg.color = new Color(0.1f, 0.5f, 0.9f);
        Button startBtn = startBtnObj.AddComponent<Button>();
        RectTransform startBtnRect = startBtnObj.GetComponent<RectTransform>();
        startBtnRect.anchoredPosition = new Vector2(0f, -80f);
        startBtnRect.sizeDelta = new Vector2(250f, 50f);

        GameObject startBtnTextObj = new GameObject("Text");
        startBtnTextObj.transform.SetParent(startBtnObj.transform, false);
        Text startBtnText = startBtnTextObj.AddComponent<Text>();
        startBtnText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        startBtnText.text = "START GAME NOW";
        startBtnText.fontSize = 18;
        startBtnText.alignment = TextAnchor.MiddleCenter;
        startBtnText.color = Color.white;
        RectTransform startBtnTextRect = startBtnTextObj.GetComponent<RectTransform>();
        startBtnTextRect.anchorMin = Vector2.zero;
        startBtnTextRect.anchorMax = Vector2.one;
        startBtnTextRect.sizeDelta = Vector2.zero;

        // Leave Room Button
        GameObject leaveBtnObj = new GameObject("LeaveRoomButton");
        leaveBtnObj.transform.SetParent(waitPanelObj.transform, false);
        Image leaveBtnImg = leaveBtnObj.AddComponent<Image>();
        leaveBtnImg.color = new Color(0.8f, 0.2f, 0.2f);
        Button leaveBtn = leaveBtnObj.AddComponent<Button>();
        RectTransform leaveBtnRect = leaveBtnObj.GetComponent<RectTransform>();
        leaveBtnRect.anchoredPosition = new Vector2(0f, -145f);
        leaveBtnRect.sizeDelta = new Vector2(250f, 50f);

        GameObject leaveBtnTextObj = new GameObject("Text");
        leaveBtnTextObj.transform.SetParent(leaveBtnObj.transform, false);
        Text leaveBtnText = leaveBtnTextObj.AddComponent<Text>();
        leaveBtnText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        leaveBtnText.text = "LEAVE ROOM";
        leaveBtnText.fontSize = 18;
        leaveBtnText.alignment = TextAnchor.MiddleCenter;
        leaveBtnText.color = Color.white;
        RectTransform leaveBtnTextRect = leaveBtnTextObj.GetComponent<RectTransform>();
        leaveBtnTextRect.anchorMin = Vector2.zero;
        leaveBtnTextRect.anchorMax = Vector2.one;
        leaveBtnTextRect.sizeDelta = Vector2.zero;


        // 5. Connect LobbyManager fields
        lobbyManager.lobbyPanel = lobbyPanelObj;
        lobbyManager.waitingPanel = waitPanelObj;
        lobbyManager.nicknameInput = nickInput;
        lobbyManager.roomNameInput = roomInput;
        lobbyManager.createRoomButton = createBtn;
        lobbyManager.roomListContent = contentRect;
        lobbyManager.roomItemPrefab = roomItemPrefab;
        lobbyManager.statusText = statusTextComp;
        lobbyManager.waitingText = waitingTextComp;
        lobbyManager.startButton = startBtnObj;
        lobbyManager.leaveRoomButton = leaveBtn;

        // Add Listeners
        createBtn.onClick.AddListener(lobbyManager.OnCreateRoomButtonClicked);
        leaveBtn.onClick.AddListener(lobbyManager.OnLeaveRoomButtonClicked);
        startBtn.onClick.AddListener(lobbyManager.OnStartButtonClicked);

        // Save
        EditorUtility.SetDirty(lobbyManager);
        EditorUtility.SetDirty(canvasObj);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(lobbyManager.gameObject.scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(lobbyManager.gameObject.scene);
        
        Debug.Log("Persistent Lobby UI created in scene hierarchy and connected successfully!");
    }
}
