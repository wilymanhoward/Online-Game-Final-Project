using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using System.Collections.Generic;

/// <summary>
/// Discord-style voice chat overlay. Drop this on any scene-level GameObject (not a player prefab).
/// It auto-discovers all PhotonVoiceChat instances and shows a fading name list on screen.
/// </summary>
public class VoiceChatOverlay : MonoBehaviour
{
    [Header("Overlay Settings")]
    [SerializeField] private Vector2 overlayStartPosition = new Vector2(40f, -80f);
    [SerializeField] private int fontSize = 28;
    [SerializeField] private float fadeSpeed = 8f;
    [SerializeField] private float defaultOpacity = 0.1f;
    [SerializeField] private float speakingOpacity = 1.0f;
    [SerializeField] private Color speakingColor = new Color(0.4f, 0.9f, 0.4f, 1f);   // Discord green
    [SerializeField] private Color silentColor = Color.white;
    [SerializeField] private float itemHeight = 40f;

    private Canvas overlayCanvas;

    private class PlayerEntry
    {
        public PhotonVoiceChat voiceChat;
        public Text nameText;
        public float alpha;
    }

    private readonly List<PlayerEntry> entries = new List<PlayerEntry>();
    private float refreshCooldown = 0f;

    private void Start()
    {
        // Singleton guard – destroy duplicates
        VoiceChatOverlay[] existing = FindObjectsOfType<VoiceChatOverlay>();
        foreach (var o in existing)
        {
            if (o != this) { Destroy(o.gameObject); }
        }

        BuildCanvas();
        RefreshList();
    }

    private void Update()
    {
        refreshCooldown -= Time.deltaTime;
        if (refreshCooldown <= 0f)
        {
            refreshCooldown = 1.5f;
            RefreshList();
        }

        UpdateVisuals();
    }

    // ─────────────────────── Canvas Setup ───────────────────────

    private void BuildCanvas()
    {
        GameObject go = new GameObject("VoiceChatOverlayCanvas");
        overlayCanvas = go.AddComponent<Canvas>();
        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.sortingOrder = 99;

        CanvasScaler scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        go.AddComponent<GraphicRaycaster>();
        DontDestroyOnLoad(go);
    }

    // ─────────────────────── Player list management ───────────────────────

    private void RefreshList()
    {
        if (overlayCanvas == null) return;

        PhotonVoiceChat[] found = FindObjectsOfType<PhotonVoiceChat>();

        // Remove stale entries
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            if (entries[i].voiceChat == null)
            {
                RemoveEntry(i);
                continue;
            }
            bool still = false;
            foreach (var vc in found) { if (vc == entries[i].voiceChat) { still = true; break; } }
            if (!still) RemoveEntry(i);
        }

        // Add new entries
        foreach (var vc in found)
        {
            bool exists = false;
            foreach (var e in entries) { if (e.voiceChat == vc) { exists = true; break; } }
            if (!exists) AddEntry(vc);
        }

        RepositionEntries();
    }

    private void AddEntry(PhotonVoiceChat vc)
    {
        GameObject row = new GameObject("VoiceRow_" + vc.gameObject.name);
        row.transform.SetParent(overlayCanvas.transform, false);

        RectTransform rect = row.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = new Vector2(500f, itemHeight);

        Text label = row.AddComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = fontSize;
        label.alignment = TextAnchor.MiddleLeft;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.color = new Color(silentColor.r, silentColor.g, silentColor.b, defaultOpacity);

        entries.Add(new PlayerEntry { voiceChat = vc, nameText = label, alpha = defaultOpacity });
    }

    private void RemoveEntry(int i)
    {
        if (entries[i].nameText != null)
            Destroy(entries[i].nameText.gameObject);
        entries.RemoveAt(i);
    }

    private void RepositionEntries()
    {
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].nameText == null) continue;
            RectTransform rect = entries[i].nameText.GetComponent<RectTransform>();
            rect.anchoredPosition = new Vector2(overlayStartPosition.x, overlayStartPosition.y - i * itemHeight);
        }
    }

    // ─────────────────────── Per-frame visuals ───────────────────────

    private void UpdateVisuals()
    {
        foreach (var entry in entries)
        {
            if (entry.voiceChat == null || entry.nameText == null) continue;

            // Resolve name
            string playerName = entry.voiceChat.gameObject.name;
            PhotonView pv = entry.voiceChat.GetComponent<PhotonView>();
            if (pv != null && pv.Owner != null && !string.IsNullOrEmpty(pv.Owner.NickName))
                playerName = pv.Owner.NickName;

            bool isMine = pv != null && pv.IsMine;
            bool isSpeaking = entry.voiceChat.IsSpeaking;
            bool isMuted = isMine && !entry.voiceChat.IsMicEnabled;

            // Text label
            if (isMuted)
                entry.nameText.text = "🔇 " + playerName + " (Muted)";
            else if (isMine)
                entry.nameText.text = "🎙️ " + playerName;
            else
                entry.nameText.text = "🔊 " + playerName;

            // Fade alpha
            float target = isSpeaking ? speakingOpacity : defaultOpacity;
            entry.alpha = Mathf.MoveTowards(entry.alpha, target, Time.deltaTime * fadeSpeed);

            // Color: green while speaking, white otherwise
            Color baseCol = isSpeaking ? speakingColor : silentColor;
            baseCol.a = entry.alpha;
            entry.nameText.color = baseCol;
        }
    }
}
