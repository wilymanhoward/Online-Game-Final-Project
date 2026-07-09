using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Playables;

public class InteractCrosshair : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The PlayerInteract script to monitor. If left empty, will try to find it on the local player.")]
    [SerializeField] private PlayerInteract playerInteract;
    private FirstPersonController fpc;

    [Header("Crosshair Customization")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color interactColor = new Color(0.9f, 0.8f, 0.1f, 1f); // Warm gold/yellow for interactable
    [SerializeField] private float transitionSpeed = 10f;

    [Header("Dot Settings (Normal State)")]
    [SerializeField] private float normalDotSize = 8f;

    [Header("Ring Settings (Interactable State)")]
    [SerializeField] private float interactRingSize = 24f;
    [SerializeField] private float ringThickness = 3f;

    [Header("Manual Sprite Overrides (Optional)")]
    [SerializeField] private Sprite customNormalSprite;
    [SerializeField] private Sprite customInteractSprite;

    private Canvas canvas;
    private Image dotImage;
    private Image ringImage;

    private float currentTransition = 0f; // 0 = Normal (Dot), 1 = Interactable (Ring)

    private const string EndingDirectorObjectName = "Ending1";
    private PlayableDirector endingDirector;
    private bool endingCutscenePlaying = false;

    private void Awake()
    {
        // Safety check for multiplayer: only run on local player
        fpc = GetComponentInParent<FirstPersonController>();
        if (fpc != null && !fpc.IsLocalPlayer)
        {
            Destroy(this);
            return;
        }

        // Find local player interact script if not set
        if (playerInteract == null)
        {
            playerInteract = GetComponentInParent<PlayerInteract>();
        }

        CreateCrosshairUI();
        SubscribeToEndingDirector();
    }

    private void Start()
    {
        // Initialize UI states
        UpdateCrosshairVisuals(0f);
    }

    private void OnDestroy()
    {
        if (endingDirector != null)
        {
            endingDirector.played -= OnEndingDirectorPlayed;
            endingDirector.stopped -= OnEndingDirectorStopped;
        }
    }

    private void SubscribeToEndingDirector()
    {
        // Only the Ending1 director (plays EndingCutscene.playable) should hide the crosshair.
        GameObject directorObj = GameObject.Find(EndingDirectorObjectName);
        endingDirector = directorObj != null ? directorObj.GetComponent<PlayableDirector>() : null;
        if (endingDirector == null) return;

        endingDirector.played -= OnEndingDirectorPlayed; // safety unsubscribe first
        endingDirector.played += OnEndingDirectorPlayed;
        endingDirector.stopped -= OnEndingDirectorStopped;
        endingDirector.stopped += OnEndingDirectorStopped;

        endingCutscenePlaying = endingDirector.state == PlayState.Playing;
    }

    private void OnEndingDirectorPlayed(PlayableDirector director)
    {
        endingCutscenePlaying = true;
        SetCrosshairVisible(false);
    }

    private void OnEndingDirectorStopped(PlayableDirector director)
    {
        endingCutscenePlaying = false;
    }

    private void Update()
    {
        // Hide the crosshair only while the Ending1/EndingCutscene timeline is playing.
        if (endingDirector == null) SubscribeToEndingDirector();
        if (endingCutscenePlaying)
        {
            SetCrosshairVisible(false);
            return;
        }

        if (playerInteract == null)
        {
            FindLocalPlayerInteract();
            if (playerInteract == null) return;
        }

        // Target transition state
        float targetTransition = playerInteract.IsLookingAtInteractable ? 1f : 0f;

        // Smoothly interpolate
        currentTransition = Mathf.MoveTowards(currentTransition, targetTransition, Time.deltaTime * transitionSpeed);

        UpdateCrosshairVisuals(currentTransition);
    }

    private void SetCrosshairVisible(bool visible)
    {
        if (dotImage != null) dotImage.enabled = visible;
        if (ringImage != null) ringImage.enabled = visible;
    }

    private void FindLocalPlayerInteract()
    {
        var players = FindObjectsOfType<FirstPersonController>();
        foreach (var p in players)
        {
            if (p.IsLocalPlayer)
            {
                playerInteract = p.GetComponent<PlayerInteract>();
                break;
            }
        }
    }

    private void CreateCrosshairUI()
    {
        // 1. Create a Screen Space Canvas if this script is not already placed under one
        canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("CrosshairCanvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100; // Display on top

            canvasObj.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasObj.AddComponent<GraphicRaycaster>();
        }

        // 2. Create the Dot Image GameObject
        GameObject dotObj = new GameObject("CrosshairDot");
        dotObj.transform.SetParent(canvas.transform, false);
        dotImage = dotObj.AddComponent<Image>();
        dotImage.rectTransform.sizeDelta = new Vector2(normalDotSize, normalDotSize);
        dotImage.color = normalColor;
        
        if (customNormalSprite != null)
        {
            dotImage.sprite = customNormalSprite;
        }
        else
        {
            dotImage.sprite = CreateDotSprite();
        }

        // 3. Create the Ring Image GameObject
        GameObject ringObj = new GameObject("CrosshairRing");
        ringObj.transform.SetParent(canvas.transform, false);
        ringImage = ringObj.AddComponent<Image>();
        ringImage.rectTransform.sizeDelta = new Vector2(interactRingSize, interactRingSize);
        ringImage.color = interactColor;

        if (customInteractSprite != null)
        {
            ringImage.sprite = customInteractSprite;
        }
        else
        {
            ringImage.sprite = CreateRingSprite();
        }
    }

    private void UpdateCrosshairVisuals(float transition)
    {
        if (dotImage == null || ringImage == null) return;

        // Color transition
        Color currentColor = Color.Lerp(normalColor, interactColor, transition);

        // Dot transition (shrinks and fades out as transition goes 0 -> 1)
        float dotScale = 1f - transition;
        dotImage.rectTransform.localScale = new Vector3(dotScale, dotScale, 1f);
        dotImage.color = new Color(currentColor.r, currentColor.g, currentColor.b, 1f - transition);
        // Fully disable once faded out so no remnant renders once the ring is fully expanded
        dotImage.enabled = transition < 0.999f;

        // Ring transition (grows and fades in as transition goes 0 -> 1)
        float ringScale = transition;
        ringImage.rectTransform.localScale = new Vector3(ringScale, ringScale, 1f);
        ringImage.color = new Color(currentColor.r, currentColor.g, currentColor.b, transition);
        ringImage.enabled = transition > 0.001f;
    }

    private Sprite CreateDotSprite()
    {
        int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float center = size / 2f;
        float radius = (size / 2f) - 2f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(center, center));
                if (dist <= radius)
                {
                    // Smooth antialiasing near edge
                    float alpha = Mathf.Clamp01(radius - dist);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
                else
                {
                    texture.SetPixel(x, y, Color.clear);
                }
            }
        }
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private Sprite CreateRingSprite()
    {
        int size = 128;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float center = size / 2f;
        float outerRadius = (size / 2f) - 4f;
        float innerRadius = outerRadius - (ringThickness * (size / interactRingSize));

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(center, center));
                if (dist >= innerRadius && dist <= outerRadius)
                {
                    // Smooth antialiasing on both inner and outer edges
                    float alphaInner = Mathf.Clamp01(dist - innerRadius);
                    float alphaOuter = Mathf.Clamp01(outerRadius - dist);
                    float alpha = Mathf.Min(alphaInner, alphaOuter);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
                else
                {
                    texture.SetPixel(x, y, Color.clear);
                }
            }
        }
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }
}
