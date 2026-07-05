using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.UI;
using Photon.Pun;
using System.Collections;

public class SarcophagusEscape : MonoBehaviourPun
{
    [Header("Sarcophagus Elements")]
    [Tooltip("Check this if this sarcophagus is for Player 1. Uncheck for Player 2.")]
    public bool isForPlayer1 = true;
    public GameObject sarcophagusLid;
    public GameObject targetPlayer;
    public int requiredClicks = 5;
    
    [Header("Lid Physics Settings")]
    public float pushForce = 8f;
    public Vector3 pushDirection = new Vector3(0f, 1f, 3f); // Push upward and forward

    [Header("Timelines (Player 1)")]
    public PlayableDirector p1_Timeline1;
    public PlayableDirector p1_Timeline2;

    [Header("Timelines (Player 2)")]
    public PlayableDirector p2_Timeline1;
    public PlayableDirector p2_Timeline2;

    [Header("Input Options")]
    public InputReader inputReader; // Optional, can use E key fallback
    
    [Header("UI Options")]
    [Tooltip("Assign your own Press E UI here. It will hide after the first press, showing the progress bar.")]
    public GameObject customPressEUI;

    private int clickCount = 0;
    private FirstPersonController fpc;
    private bool isEscaped = false;
    private Vector3 initialLidLocalPos;

    // UI Elements
    private GameObject sequenceUIObj;
    private Text promptText;
    private Image progressBarFill;
    private CanvasGroup uiCanvasGroup;
    private bool isWaitingForSpam = false;

    private void Start()
    {
        // 1. Find the target player (Player1 or Player2)
        if (targetPlayer == null)
        {
            targetPlayer = GameObject.Find(isForPlayer1 ? "Player1" : "Player2");
        }

        if (sarcophagusLid == null)
        {
            // Try to find the user's custom sarcophagus and lid in the hierarchy first
            string sarcName = isForPlayer1 ? "Player 1 Sarcophagus" : "Player 2 Sarcophagus";
            GameObject customSarc = GameObject.Find(sarcName);
            if (customSarc != null)
            {
                Transform lidTrans = customSarc.transform.Find("Lid");
                if (lidTrans != null)
                {
                    sarcophagusLid = lidTrans.gameObject;
                }
            }

            // Fallback: search closest matching lid
            if (sarcophagusLid == null)
            {
                float closestDist = float.MaxValue;
                GameObject closestLid = null;
                Vector3 targetRefPos = targetPlayer != null ? targetPlayer.transform.position : new Vector3(-1.20f, 0.01f, -2.07f);

                GameObject[] allObjects = GameObject.FindObjectsOfType<GameObject>();
                foreach (GameObject go in allObjects)
                {
                    if (go.name == "Lid" || go.name == "SM_Prop_Sarcophagus_01_Lid_01")
                    {
                        float dist = Vector3.Distance(go.transform.position, targetRefPos);
                        if (dist < closestDist)
                        {
                            closestDist = dist;
                            closestLid = go;
                        }
                    }
                }
                sarcophagusLid = closestLid;
            }
        }

        if (sarcophagusLid != null)
        {
            initialLidLocalPos = sarcophagusLid.transform.localPosition;
        }

        // Determine if the local player is the target of THIS sarcophagus
        bool isMySarcophagus = false;
        if (!PhotonNetwork.IsConnected)
        {
            isMySarcophagus = true; // Offline testing
        }
        else
        {
            isMySarcophagus = isForPlayer1 ? PhotonNetwork.IsMasterClient : !PhotonNetwork.IsMasterClient;
        }

        // Lock the targeted player in the sarcophagus
        if (isMySarcophagus)
        {
            if (targetPlayer != null)
            {
                fpc = targetPlayer.GetComponent<FirstPersonController>();
                if (fpc != null)
                {
                    fpc.isParalyzed = true;
                    fpc.originalNearClip = 0.26f; // Set default post-escape near clip
                    // Position player inside their respective sarcophagus base
                    if (isForPlayer1)
                    {
                        targetPlayer.transform.position = new Vector3(-1.24f, 0.4f, -2.10f);
                    }
                    else
                    {
                        targetPlayer.transform.position = new Vector3(-11.531f, 0.09128681f, 15.23f);
                    }
                }
            }

            // Set camera clipping plane near to 0.15 while inside the Sarcophagus
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                mainCam.nearClipPlane = 0.15f;
            }

            CreateSpamUI();
        }

        if (inputReader != null)
        {
            inputReader.OnInteract += HandleInteractPress;
        }

        // Start the sequence
        StartCoroutine(RunSpawnSequence(isMySarcophagus));
    }

    private void OnDestroy()
    {
        if (inputReader != null)
        {
            inputReader.OnInteract -= HandleInteractPress;
        }
    }

    private IEnumerator RunSpawnSequence(bool isMySarcophagus)
    {
        // Select timelines based on who this sarcophagus is for
        PlayableDirector t1 = isForPlayer1 ? p1_Timeline1 : p2_Timeline1;
        PlayableDirector t2 = isForPlayer1 ? p1_Timeline2 : p2_Timeline2;

        // 1. Play first timeline
        if (t1 != null)
        {
            t1.gameObject.SetActive(true);
            t1.Play();
            yield return null;
            while (t1.state == PlayState.Playing)
            {
                yield return null;
            }
            
            // Wait 1 second after it finishes before removing it
            yield return new WaitForSeconds(1.0f);
            
            // Disable timeline to clear stuck subtitles
            t1.gameObject.SetActive(false);
        }

        if (isMySarcophagus)
        {
            // This client gets the Spam E UI to escape their sarcophagus
            clickCount = 0;
            UpdateUI();
            
            // Show the user's custom UI first, and make sure my UI is hidden
            if (customPressEUI != null) customPressEUI.SetActive(true);
            sequenceUIObj.SetActive(false);

            isWaitingForSpam = true;

            // Wait for the very FIRST click to swap the UIs
            while (clickCount < 1)
            {
                yield return null;
            }

            // Hide user's custom UI and fade in the progress bar UI
            if (customPressEUI != null) customPressEUI.SetActive(false);
            
            sequenceUIObj.SetActive(true);
            float fade = 0f;
            while (fade < 1f)
            {
                fade += Time.deltaTime * 3f;
                uiCanvasGroup.alpha = Mathf.Clamp01(fade);
                yield return null;
            }
            
            // Wait until the remaining clicks are met
            while (clickCount < requiredClicks)
            {
                yield return null;
            }

            isWaitingForSpam = false;

            fade = 1f;
            while (fade > 0f)
            {
                fade -= Time.deltaTime * 3f;
                uiCanvasGroup.alpha = Mathf.Clamp01(fade);
                yield return null;
            }
            sequenceUIObj.SetActive(false);

            // Escape!
            EscapeSarcophagus();
        }

        // Both players wait for the escape to be fully triggered
        // (EscapeSarcophagus sets isEscaped over RPC)
        yield return new WaitUntil(() => isEscaped);

        // Optional small delay after lid pops off before playing audio
        yield return new WaitForSeconds(0.5f);

        // 2. Play second timeline
        if (t2 != null)
        {
            t2.gameObject.SetActive(true);
            t2.Play();
            yield return null;
            while (t2.state == PlayState.Playing)
            {
                yield return null;
            }

            // Disable timeline to clear stuck subtitles
            t2.gameObject.SetActive(false);
        }
    }

    private void HandleInteractPress()
    {
        if (isWaitingForSpam && !isEscaped)
        {
            RegisterClick();
        }
    }

    private void Update()
    {
        // Fallback to KeyCode.E if inputReader is missing
        if (isWaitingForSpam && !isEscaped && inputReader == null)
        {
            if (Input.GetKeyDown(KeyCode.E))
            {
                RegisterClick();
            }
        }
    }

    private void RegisterClick()
    {
        clickCount++;
        Debug.Log($"[SarcophagusEscape] Click {clickCount}/{requiredClicks} registered.");
        
        UpdateUI();

        if (sarcophagusLid != null)
        {
            StartCoroutine(ShakeLid());
        }
        
        if (fpc != null)
        {
            fpc.TriggerCameraShake(0.15f, 0.05f);
        }
    }

    private void CreateSpamUI()
    {
        sequenceUIObj = new GameObject("SpawnSequenceUI");
        Canvas canvas = sequenceUIObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900;
        
        var scaler = sequenceUIObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        
        sequenceUIObj.AddComponent<GraphicRaycaster>();
        uiCanvasGroup = sequenceUIObj.AddComponent<CanvasGroup>();
        uiCanvasGroup.alpha = 0f;

        GameObject panelObj = new GameObject("PromptPanel");
        panelObj.transform.SetParent(sequenceUIObj.transform, false);
        var panelImage = panelObj.AddComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.7f);
        
        var rectPanel = panelObj.GetComponent<RectTransform>();
        rectPanel.anchorMin = new Vector2(0.5f, 0.2f);
        rectPanel.anchorMax = new Vector2(0.5f, 0.2f);
        rectPanel.anchoredPosition = Vector2.zero;
        rectPanel.sizeDelta = new Vector2(500f, 150f);

        GameObject textObj = new GameObject("PromptText");
        textObj.transform.SetParent(panelObj.transform, false);
        promptText = textObj.AddComponent<Text>();
        promptText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        promptText.text = "SPAM 'E' TO ESCAPE";
        promptText.fontSize = 32;
        promptText.alignment = TextAnchor.MiddleCenter;
        promptText.color = Color.white;
        
        var rectText = textObj.GetComponent<RectTransform>();
        rectText.anchorMin = new Vector2(0.5f, 1f);
        rectText.anchorMax = new Vector2(0.5f, 1f);
        rectText.anchoredPosition = new Vector2(0f, -40f);
        rectText.sizeDelta = new Vector2(480f, 50f);

        GameObject barBgObj = new GameObject("BarBackground");
        barBgObj.transform.SetParent(panelObj.transform, false);
        var barBgImage = barBgObj.AddComponent<Image>();
        barBgImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);
        
        var rectBarBg = barBgObj.GetComponent<RectTransform>();
        rectBarBg.anchorMin = new Vector2(0.5f, 0f);
        rectBarBg.anchorMax = new Vector2(0.5f, 0f);
        rectBarBg.anchoredPosition = new Vector2(0f, 40f);
        rectBarBg.sizeDelta = new Vector2(400f, 30f);

        GameObject barFillObj = new GameObject("BarFill");
        barFillObj.transform.SetParent(barBgObj.transform, false);
        progressBarFill = barFillObj.AddComponent<Image>();
        progressBarFill.color = new Color(0.9f, 0.1f, 0.1f, 1f); // Dark Red
        
        var rectBarFill = barFillObj.GetComponent<RectTransform>();
        rectBarFill.anchorMin = new Vector2(0f, 0f);
        rectBarFill.anchorMax = new Vector2(0f, 1f);
        rectBarFill.pivot = new Vector2(0f, 0.5f);
        rectBarFill.anchoredPosition = Vector2.zero;
        rectBarFill.sizeDelta = new Vector2(0f, 0f);

        sequenceUIObj.SetActive(false);
    }

    private int currentClicks => clickCount;

    private void UpdateUI()
    {
        if (progressBarFill != null)
        {
            float fillPct = Mathf.Clamp01((float)currentClicks / requiredClicks);
            var rect = progressBarFill.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(fillPct * 400f, 0f);
        }
    }

    private IEnumerator ShakeLid()
    {
        float elapsed = 0f;
        float duration = 0.1f;
        Vector3 shakeOffset = new Vector3(Random.Range(-0.02f, 0.02f), Random.Range(0.01f, 0.03f), 0f);
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            sarcophagusLid.transform.localPosition = initialLidLocalPos + shakeOffset;
            yield return null;
        }
        sarcophagusLid.transform.localPosition = initialLidLocalPos;
    }

    private void EscapeSarcophagus()
    {
        Debug.Log("[SarcophagusEscape] Escape triggered! Releasing player.");
        
        if (fpc != null)
        {
            fpc.isParalyzed = false;
        }

        // Restore camera clipping plane near to 0.25 after opening the lid
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            mainCam.nearClipPlane = 0.25f;
        }

        if (PhotonNetwork.IsConnected && PhotonNetwork.InRoom)
        {
            photonView.RPC("PushLidOffRPC", RpcTarget.AllBuffered);
        }
        else
        {
            PushLidOffRPC();
        }
    }

    [PunRPC]
    private void PushLidOffRPC()
    {
        isEscaped = true;
        Debug.Log("[SarcophagusEscape] PushLidOffRPC called. isEscaped = true");
        if (sarcophagusLid == null) return;

        StartCoroutine(RotateLidCoroutine());
    }

    private IEnumerator RotateLidCoroutine()
    {
        float duration = 1.0f;
        float elapsed = 0f;
        
        sarcophagusLid.transform.SetParent(null);

        Rigidbody rb = sarcophagusLid.GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;
        
        Vector3 startPos = sarcophagusLid.transform.position;
        Quaternion startRot = sarcophagusLid.transform.rotation;
        
        Vector3 pushOffset = targetPlayer != null ? (targetPlayer.transform.forward * 1.6f - targetPlayer.transform.up * 1.1f) : new Vector3(0f, -1.1f, 1.6f);
        Vector3 targetPos = startPos + pushOffset;
        
        Quaternion targetRot = startRot;
        if (targetPlayer != null)
        {
            targetRot = Quaternion.AngleAxis(90f, targetPlayer.transform.right) * startRot;
        }
        else
        {
            targetRot = Quaternion.Euler(90f, startRot.eulerAngles.y, startRot.eulerAngles.z);
        }

        var col = sarcophagusLid.GetComponent<Collider>();
        if (col != null) col.enabled = false;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float progress = EaseOutBounce(t);

            sarcophagusLid.transform.position = Vector3.Lerp(startPos, targetPos, progress);
            sarcophagusLid.transform.rotation = Quaternion.Slerp(startRot, targetRot, progress);
            yield return null;
        }

        sarcophagusLid.transform.position = targetPos;
        sarcophagusLid.transform.rotation = targetRot;

        if (col != null) col.enabled = true;
    }

    private float EaseOutBounce(float x)
    {
        float n1 = 7.5625f;
        float d1 = 2.75f;

        if (x < 1f / d1) return n1 * x * x;
        else if (x < 2f / d1) return n1 * (x -= 1.5f / d1) * x + 0.75f;
        else if (x < 2.5f / d1) return n1 * (x -= 2.25f / d1) * x + 0.9375f;
        else return n1 * (x -= 2.625f / d1) * x + 0.984375f;
    }
}
