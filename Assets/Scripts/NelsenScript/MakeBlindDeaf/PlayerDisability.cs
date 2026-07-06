using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerDisability : MonoBehaviour
{
    // Blind overlay (UI panel shown while player is blind)
    [Tooltip("Assign any UI panel / Image here to show as the blindness indicator.")]
    public GameObject BlindOverlay;

    // Deaf overlay
    public GameObject DeafOverlay;

    [Header("Deaf Audio Settings")]
    [Tooltip("How muffled the cutoff frequency goes (Hz). Lower = more muffled. Default 800 Hz sounds like cotton in ears.")]
    public float deafLowPassCutoff = 800f;

    [Tooltip("How much to reduce volume (0–1) while deaf. 0.35 = noticeably quieter but still audible.")]
    [Range(0f, 1f)]
    public float deafVolumeMultiplier = 0.35f;

    [Tooltip("Seconds to fade INTO the muffled state.")]
    public float deafFadeInDuration  = 0.6f;

    [Tooltip("Seconds to fade BACK to normal hearing.")]
    public float deafFadeOutDuration = 0.4f;

    public bool IsBlindActive { get; private set; }
    public bool IsDeafActive  { get; private set; }

    // ── Deaf audio internals ──────────────────────────────────────────────────
    private AudioLowPassFilter _deafFilter;  // attached to the AudioListener
    private Coroutine          _deafCoroutine;
    private const float        NormalCutoff = 22000f; // Hz – effectively no filter
    private const float        NormalVolume = 1f;

    // ── Bandage overlay state ─────────────────────────────────────────────────
    private struct BandageConfig
    {
        public string  name;
        public Vector2 position;
        public Vector2 size;
        public float   rotation;
        public float   slideDirection; // 1 = forward along rotation axis, -1 = backward
    }

    private GameObject         bandageCanvasObj;
    private List<RectTransform> bandageWraps    = new List<RectTransform>();
    private List<Vector2>       startPositions  = new List<Vector2>();
    private List<Vector2>       targetPositions = new List<Vector2>();
    private bool                bandageActive   = false;
    private Coroutine           bandageAnimCoroutine;

    // ─────────────────────────────────────────────────────────────────────────

    void Start()
    {
        // Grab (or create) the AudioLowPassFilter on the scene's AudioListener
        AudioListener listener = FindObjectOfType<AudioListener>();
        if (listener != null)
        {
            _deafFilter = listener.GetComponent<AudioLowPassFilter>();
            if (_deafFilter == null)
                _deafFilter = listener.gameObject.AddComponent<AudioLowPassFilter>();

            // Start at full normal hearing
            _deafFilter.cutoffFrequency = NormalCutoff;
            _deafFilter.enabled         = false;
        }
        else
        {
            Debug.LogWarning("[PlayerDisability] No AudioListener found in scene — deaf audio effect will not work.");
        }

        SetBlind(false);
        SetDeaf(false);

        // Make sure overlays start hidden
        if (BlindOverlay != null) BlindOverlay.SetActive(false);
        if (DeafOverlay  != null) DeafOverlay.SetActive(false);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Enable or disable the blindness effect.
    /// Toggles the BlindOverlay UI panel and plays the bandage-wrap animation.
    /// </summary>
    public void SetBlind(bool isBlind)
    {
        IsBlindActive = isBlind;

        // Toggle the blind UI indicator
        if (BlindOverlay != null)
            BlindOverlay.SetActive(isBlind);

        if (bandageCanvasObj == null)
        {
            CreateBandageUI();
        }

        // Only animate if state actually changes
        if (isBlind == bandageActive) return;
        bandageActive = isBlind;

        if (bandageAnimCoroutine != null)
        {
            StopCoroutine(bandageAnimCoroutine);
        }
        bandageAnimCoroutine = StartCoroutine(AnimateBandages(bandageActive));
    }

    /// <summary>
    /// Enable or disable the deafness effect.
    /// Toggles the DeafOverlay UI and smoothly muffles/restores all game audio
    /// using an AudioLowPassFilter on the scene's AudioListener.
    /// </summary>
    public void SetDeaf(bool isDeaf)
    {
        IsDeafActive = isDeaf;

        // Toggle the UI overlay
        if (DeafOverlay != null)
            DeafOverlay.SetActive(isDeaf);

        // Smoothly muffle / restore audio
        if (_deafCoroutine != null)
            StopCoroutine(_deafCoroutine);
        _deafCoroutine = StartCoroutine(FadeDeafAudio(isDeaf));
    }

    // ── Bandage UI creation ───────────────────────────────────────────────────

    private void CreateBandageUI()
    {
        if (bandageCanvasObj != null) return;

        // Try to find BandageOverlayCanvas in the local hierarchy first
        Transform canvasTransform = transform.Find("BandageOverlayCanvas");
        if (canvasTransform == null)
        {
            canvasTransform = FindDeepChild(transform, "BandageOverlayCanvas");
        }

        if (canvasTransform != null)
        {
            bandageCanvasObj = canvasTransform.gameObject;

            bandageWraps.Clear();
            startPositions.Clear();
            targetPositions.Clear();

            // Find Container child (fallback to canvas root if missing)
            Transform container = canvasTransform.Find("Container");
            if (container == null) container = canvasTransform;

            // Cache child bandage strips
            foreach (Transform child in container)
            {
                RectTransform rect = child.GetComponent<RectTransform>();
                if (rect != null)
                {
                    MummyBandageStrip strip = child.GetComponent<MummyBandageStrip>();
                    if (strip == null)
                    {
                        strip = child.gameObject.AddComponent<MummyBandageStrip>();
                    }

                    // Cache target position and rotation from the hierarchy design
                    Vector2 targetPos = rect.anchoredPosition;
                    float   rotation  = rect.localRotation.eulerAngles.z;

                    // Calculate slide starting position along the rotation axis
                    float   rad      = rotation * Mathf.Deg2Rad;
                    Vector2 dir      = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
                    Vector2 offset   = dir * (3600f * strip.slideDirection);
                    Vector2 startPos = targetPos + offset;

                    bandageWraps.Add(rect);
                    startPositions.Add(startPos);
                    targetPositions.Add(targetPos);

                    // Set initial state to starting position (hidden)
                    rect.anchoredPosition = startPos;
                }
            }

            bandageCanvasObj.SetActive(false);
            return;
        }

        // Fallback: Create Canvas GameObject programmatically if missing from hierarchy
        bandageCanvasObj = new GameObject("BandageOverlayCanvas");
        Canvas canvas = bandageCanvasObj.AddComponent<Canvas>();
        canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 998; // Just under Death UI

        var scaler = bandageCanvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode        = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        bandageCanvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        // Create container panel
        GameObject containerObj  = new GameObject("Container");
        containerObj.transform.SetParent(bandageCanvasObj.transform, false);
        var rectContainer        = containerObj.AddComponent<RectTransform>();
        rectContainer.anchorMin  = Vector2.zero;
        rectContainer.anchorMax  = Vector2.one;
        rectContainer.sizeDelta  = Vector2.zero;

        bandageWraps.Clear();
        startPositions.Clear();
        targetPositions.Clear();

        // Configure 7 medium-large messy horizontal-ish wraps spanning edge-to-edge
        var configs = new List<BandageConfig>
        {
            new BandageConfig { name = "A1", position = new Vector2(0f,  380f), size = new Vector2(3400f, 180f), rotation =   8f, slideDirection = -1f },
            new BandageConfig { name = "A2", position = new Vector2(0f, -380f), size = new Vector2(3400f, 180f), rotation =  -6f, slideDirection =  1f },
            new BandageConfig { name = "A3", position = new Vector2(0f,  200f), size = new Vector2(3400f, 150f), rotation =  -9f, slideDirection = -1f },
            new BandageConfig { name = "A4", position = new Vector2(0f, -200f), size = new Vector2(3400f, 150f), rotation =   7f, slideDirection =  1f },
            new BandageConfig { name = "A5", position = new Vector2(0f,    0f), size = new Vector2(3400f, 140f), rotation =  -3f, slideDirection = -1f },
            // Diagonals crossing over to create abstract scattered peepholes
            new BandageConfig { name = "A6", position = new Vector2(0f,   90f), size = new Vector2(3400f, 130f), rotation = -15f, slideDirection =  1f },
            new BandageConfig { name = "A7", position = new Vector2(0f,  -90f), size = new Vector2(3400f, 130f), rotation =  16f, slideDirection = -1f }
        };

        foreach (var cfg in configs)
        {
            float   rad      = cfg.rotation * Mathf.Deg2Rad;
            Vector2 dir      = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            Vector2 offset   = dir * (3600f * cfg.slideDirection);
            Vector2 startPos = cfg.position + offset;

            RectTransform rect = CreateBandageStrip(cfg.name, containerObj.transform,
                                                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                                    startPos, cfg.size, cfg.rotation);
            bandageWraps.Add(rect);
            startPositions.Add(startPos);
            targetPositions.Add(cfg.position);
        }

        bandageCanvasObj.SetActive(false);
    }

    private RectTransform CreateBandageStrip(string stripName, Transform parent,
                                             Vector2 anchor, Vector2 pivot,
                                             Vector2 startPos, Vector2 size, float rotation)
    {
        // Root GameObject
        GameObject stripObj = new GameObject(stripName);
        stripObj.transform.SetParent(parent, false);
        RectTransform rect = stripObj.AddComponent<RectTransform>();
        rect.anchorMin        = anchor;
        rect.anchorMax        = anchor;
        rect.pivot            = pivot;
        rect.anchoredPosition = startPos;
        rect.sizeDelta        = size;
        rect.localRotation    = Quaternion.Euler(0f, 0f, rotation);

        // Layer 1: Dark sandy shadow/outline
        var bgImage = stripObj.AddComponent<UnityEngine.UI.Image>();
        bgImage.color = new Color(0.42f, 0.35f, 0.28f, 0.95f);

        // Layer 2: Main bandage wrap (Beige)
        GameObject mainObj  = new GameObject("MainWrap");
        mainObj.transform.SetParent(stripObj.transform, false);
        var rectMain        = mainObj.AddComponent<RectTransform>();
        rectMain.anchorMin  = Vector2.zero;
        rectMain.anchorMax  = Vector2.one;
        rectMain.sizeDelta  = new Vector2(0f, -12f);
        var mainImage       = mainObj.AddComponent<UnityEngine.UI.Image>();
        mainImage.color     = new Color(0.84f, 0.77f, 0.68f, 1f);

        // Layer 3: Highlight fold (Lighter cream)
        GameObject highlightObj    = new GameObject("HighlightFold");
        highlightObj.transform.SetParent(mainObj.transform, false);
        var rectHighlight          = highlightObj.AddComponent<RectTransform>();
        rectHighlight.anchorMin    = new Vector2(0f, 0.15f);
        rectHighlight.anchorMax    = new Vector2(1f, 0.35f);
        rectHighlight.sizeDelta    = Vector2.zero;
        var highlightImage         = highlightObj.AddComponent<UnityEngine.UI.Image>();
        highlightImage.color       = new Color(0.92f, 0.87f, 0.81f, 1f);

        // Layer 4: Overlapping secondary strip for textured look (Darker beige)
        GameObject overlapObj   = new GameObject("OverlapStrip");
        overlapObj.transform.SetParent(mainObj.transform, false);
        var rectOverlap         = overlapObj.AddComponent<RectTransform>();
        rectOverlap.anchorMin   = new Vector2(0f, 0.5f);
        rectOverlap.anchorMax   = new Vector2(1f, 0.95f);
        rectOverlap.sizeDelta   = Vector2.zero;
        rectOverlap.localRotation = Quaternion.Euler(0f, 0f, -0.8f);
        var overlapImage        = overlapObj.AddComponent<UnityEngine.UI.Image>();
        overlapImage.color      = new Color(0.80f, 0.73f, 0.64f, 1f);

        return rect;
    }

    // ── Deaf audio coroutine ──────────────────────────────────────────────────

    /// <summary>
    /// Smoothly transitions the AudioLowPassFilter cutoff and AudioListener volume
    /// to create or remove the muffled / cotton-ears deaf effect.
    /// No AudioMixer setup required — works out of the box.
    /// </summary>
    private IEnumerator FadeDeafAudio(bool goDeaf)
    {
        if (_deafFilter == null) yield break;

        float duration = goDeaf ? deafFadeInDuration : deafFadeOutDuration;
        float elapsed  = 0f;

        float startCutoff = _deafFilter.cutoffFrequency;
        float startVolume = AudioListener.volume;

        float targetCutoff = goDeaf ? deafLowPassCutoff  : NormalCutoff;
        float targetVolume = goDeaf ? deafVolumeMultiplier : NormalVolume;

        // Enable the filter as soon as we start muffling
        if (goDeaf) _deafFilter.enabled = true;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float easeT = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));

            _deafFilter.cutoffFrequency = Mathf.Lerp(startCutoff, targetCutoff, easeT);
            AudioListener.volume        = Mathf.Lerp(startVolume,  targetVolume,  easeT);

            yield return null;
        }

        // Snap to finals
        _deafFilter.cutoffFrequency = targetCutoff;
        AudioListener.volume        = targetVolume;

        // Disable the filter component when not muffling (saves CPU)
        if (!goDeaf) _deafFilter.enabled = false;
    }

    // ── Animation coroutine ───────────────────────────────────────────────────

    private IEnumerator AnimateBandages(bool targetActive)
    {
        float duration = targetActive ? 2.5f : 0.85f; // Slower dramatic crawl in, faster out
        float elapsed  = 0f;

        var currentStartPos = new List<Vector2>();
        foreach (var wrap in bandageWraps)
        {
            currentStartPos.Add(wrap.anchoredPosition);
        }

        if (targetActive && bandageCanvasObj != null)
        {
            bandageCanvasObj.SetActive(true);
        }

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t     = elapsed / duration;
            float easeT = Mathf.SmoothStep(0f, 1f, t);

            for (int i = 0; i < bandageWraps.Count; i++)
            {
                Vector2 target  = targetActive ? targetPositions[i] : startPositions[i];
                Vector2 basePos = Vector2.Lerp(currentStartPos[i], target, easeT);

                // Trembling shake fades out as easeT goes to 1 (snug wrap)
                if (targetActive)
                {
                    float shakeStrength = (1f - easeT) * 15f;
                    float freq          = 60f;
                    float shakeX = Mathf.Sin(elapsed * freq + i * 7f) * shakeStrength
                                 + Random.Range(-shakeStrength * 0.3f, shakeStrength * 0.3f);
                    float shakeY = Mathf.Cos(elapsed * freq * 0.9f + i * 3f) * shakeStrength
                                 + Random.Range(-shakeStrength * 0.3f, shakeStrength * 0.3f);
                    basePos += new Vector2(shakeX, shakeY);
                }

                bandageWraps[i].anchoredPosition = basePos;
            }

            yield return null;
        }

        // Snap to final positions
        for (int i = 0; i < bandageWraps.Count; i++)
        {
            bandageWraps[i].anchoredPosition = targetActive ? targetPositions[i] : startPositions[i];
        }

        if (!targetActive && bandageCanvasObj != null)
        {
            bandageCanvasObj.SetActive(false);
        }
    }

    // ── Utility ───────────────────────────────────────────────────────────────

    private Transform FindDeepChild(Transform parent, string childName)
    {
        foreach (Transform child in parent)
        {
            if (child.name == childName) return child;
            Transform result = FindDeepChild(child, childName);
            if (result != null) return result;
        }
        return null;
    }
}
