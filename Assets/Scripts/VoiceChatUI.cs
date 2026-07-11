using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using System.Collections.Generic;

public class VoiceChatUI : MonoBehaviourPun
{
    [System.Serializable]
    public class PlayerUIItem
    {
        public PhotonVoiceChat voiceChat;
        public GameObject uiContainer;
        public Text nameText;
        public float currentAlpha;
    }

    [Header("Overlay Settings")]
    [SerializeField] private Vector2 overlayPosition = new Vector2(40f, -80f);
    [SerializeField] private int fontSize = 28;
    [SerializeField] private float fadeSpeed = 8f;
    [SerializeField] private float defaultOpacity = 0.1f;
    [SerializeField] private float speakingOpacity = 1.0f;
    [SerializeField] private Color textColor = Color.white;
    [SerializeField] private float itemSpacing = 40f;

    private Canvas overlayCanvas;
    private List<PlayerUIItem> uiItems = new List<PlayerUIItem>();
    private float refreshTimer = 0f;

    private void Awake()
    {
        // Only the local player client should create and manage the screen overlay
        if (PhotonNetwork.IsConnected && !photonView.IsMine)
        {
            Destroy(this);
            return;
        }
    }

    private void Start()
    {
        CreateOverlayCanvas();
        RefreshPlayerList();
    }

    private void Update()
    {
        // Periodically refresh the player list to handle players joining/leaving
        refreshTimer += Time.deltaTime;
        if (refreshTimer >= 1.5f)
        {
            refreshTimer = 0f;
            RefreshPlayerList();
        }

        UpdateOverlayVisuals();
    }

    private void CreateOverlayCanvas()
    {
        GameObject canvasObj = new GameObject("VoiceChatOverlayCanvas");
        overlayCanvas = canvasObj.AddComponent<Canvas>();
        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.sortingOrder = 50;

        var scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
    }

    private void RefreshPlayerList()
    {
        if (overlayCanvas == null) return;

        // Find all voice chat instances in the scene
        PhotonVoiceChat[] activeVoiceChats = FindObjectsOfType<PhotonVoiceChat>();

        // Remove UI items for players who are no longer present
        for (int i = uiItems.Count - 1; i >= 0; i--)
        {
            bool stillPresent = false;
            foreach (var vc in activeVoiceChats)
            {
                if (uiItems[i].voiceChat == vc)
                {
                    stillPresent = true;
                    break;
                }
            }

            if (!stillPresent)
            {
                if (uiItems[i].uiContainer != null)
                {
                    Destroy(uiItems[i].uiContainer);
                }
                uiItems.RemoveAt(i);
            }
        }

        // Add UI items for new players
        foreach (var vc in activeVoiceChats)
        {
            bool alreadyAdded = false;
            foreach (var item in uiItems)
            {
                if (item.voiceChat == vc)
                {
                    alreadyAdded = true;
                    break;
                }
            }

            if (!alreadyAdded)
            {
                CreatePlayerUIItem(vc);
            }
        }

        // Reposition UI items vertically
        for (int i = 0; i < uiItems.Count; i++)
        {
            RectTransform rect = uiItems[i].uiContainer.GetComponent<RectTransform>();
            rect.anchoredPosition = new Vector2(overlayPosition.x, overlayPosition.y - (i * itemSpacing));
        }
    }

    private void CreatePlayerUIItem(PhotonVoiceChat vc)
    {
        // Container
        GameObject container = new GameObject("PlayerVoiceItem_" + vc.gameObject.name);
        container.transform.SetParent(overlayCanvas.transform, false);
        
        RectTransform rect = container.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f); // Top-Left
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = new Vector2(500f, 35f);

        // Text
        GameObject textObj = new GameObject("NameText");
        textObj.transform.SetParent(container.transform, false);
        Text text = textObj.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.alignment = TextAnchor.MiddleLeft;
        text.color = new Color(textColor.r, textColor.g, textColor.b, defaultOpacity);

        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        textRect.anchoredPosition = Vector2.zero;

        PlayerUIItem newItem = new PlayerUIItem
        {
            voiceChat = vc,
            uiContainer = container,
            nameText = text,
            currentAlpha = defaultOpacity
        };

        uiItems.Add(newItem);
    }

    private void UpdateOverlayVisuals()
    {
        foreach (var item in uiItems)
        {
            if (item.voiceChat == null || item.nameText == null) continue;

            // Get player nickname or fallback to GameObject name
            string playerName = "";
            PhotonView pv = item.voiceChat.GetComponent<PhotonView>();
            if (pv != null && pv.Owner != null && !string.IsNullOrEmpty(pv.Owner.NickName))
            {
                playerName = pv.Owner.NickName;
            }
            else
            {
                playerName = item.voiceChat.gameObject.name;
            }

            // Append mute icon if appropriate
            if (item.voiceChat.photonView.IsMine)
            {
                if (item.voiceChat.IsMicEnabled)
                {
                    item.nameText.text = "🎙️ " + playerName;
                }
                else
                {
                    item.nameText.text = "🔇 (Muted) " + playerName;
                }
            }
            else
            {
                item.nameText.text = "🔊 " + playerName;
            }

            // Fade based on speaking state
            float targetAlpha = item.voiceChat.IsSpeaking ? speakingOpacity : defaultOpacity;
            item.currentAlpha = Mathf.MoveTowards(item.currentAlpha, targetAlpha, Time.deltaTime * fadeSpeed);

            Color col = textColor;
            col.a = item.currentAlpha;
            item.nameText.color = col;
        }
    }
}
