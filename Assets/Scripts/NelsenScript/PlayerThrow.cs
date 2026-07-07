using System.Collections;
using UnityEngine;
using Photon.Pun;

public class PlayerThrow : MonoBehaviourPun
{
    [Header("Throw Settings")]
    public string throwablePrefabName = "ThrowableRock";
    public float throwForce = 15f;
    public int trajectoryResolution = 30;
    public float trajectoryStepTime = 0.05f;

    [Header("References")]
    [SerializeField] private InputReader inputReader;

    private LineRenderer trajectoryLine;
    private GameObject landingMarker;
    private Camera playerCamera;
    private Animator animator;
    private FirstPersonController fpc;

    private bool isThrowingEnabled = true;

    private void Start()
    {
        fpc = GetComponent<FirstPersonController>();
        animator = GetComponent<Animator>();
        
        playerCamera = Camera.main;
        if (playerCamera == null)
        {
            playerCamera = FindObjectOfType<Camera>();
        }

        if (playerCamera != null && fpc != null)
        {
            fpc.originalNearClip = playerCamera.nearClipPlane;
        }

        InitializeThrowVisuals();
    }

    private void OnDestroy()
    {
        if (landingMarker != null)
        {
            Destroy(landingMarker);
        }
    }

    private void Update()
    {
        // Only run for the local player
        if (PhotonNetwork.IsConnected && photonView != null && !photonView.IsMine) return;

        if (!isThrowingEnabled)
        {
            // Make sure we cancel aiming if throwing gets disabled mid-aim
            if (fpc != null && fpc.isAiming)
            {
                CancelAiming();
            }
            return;
        }

        UpdateAimingAndTrajectory();
    }

    public void SetThrowingEnabled(bool enabled)
    {
        isThrowingEnabled = enabled;
        if (!enabled)
        {
            CancelAiming();
        }
    }

    public bool IsThrowingEnabled()
    {
        return isThrowingEnabled;
    }

    private void CancelAiming()
    {
        if (fpc != null) fpc.isAiming = false;
        if (trajectoryLine != null) trajectoryLine.enabled = false;
        if (landingMarker != null) landingMarker.SetActive(false);
        if (animator != null && animator.enabled)
        {
            animator.SetBool("Aiming", false);
        }
    }

    private void InitializeThrowVisuals()
    {
        // Setup Trajectory LineRenderer dynamically if not present
        trajectoryLine = GetComponent<LineRenderer>();
        if (trajectoryLine == null)
        {
            trajectoryLine = gameObject.AddComponent<LineRenderer>();
        }
        trajectoryLine.startWidth = 0.05f;
        trajectoryLine.endWidth = 0.05f;
        trajectoryLine.numCornerVertices = 6;
        trajectoryLine.numCapVertices = 6;
        trajectoryLine.positionCount = 0;
        trajectoryLine.enabled = false;

        // Try to assign a default transparent shader
        Shader spriteShader = Shader.Find("Sprites/Default");
        if (spriteShader != null)
        {
            trajectoryLine.material = new Material(spriteShader);
        }
        trajectoryLine.startColor = new Color(0.3f, 0.3f, 0.3f, 0.95f); // Dark Grey
        trajectoryLine.endColor = new Color(0.3f, 0.3f, 0.3f, 0.2f);

        // Setup Landing Marker dynamically (a flat circle sprite on the ground)
        landingMarker = new GameObject("ThrowLandingMarker");
        landingMarker.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        landingMarker.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);

        SpriteRenderer markerRenderer = landingMarker.AddComponent<SpriteRenderer>();
        markerRenderer.sprite = CreateCircleSprite(32);
        markerRenderer.color = new Color(0.3f, 0.3f, 0.3f, 0.8f); // Dark Grey transparent circle
        landingMarker.SetActive(false);
    }

    private Sprite CreateCircleSprite(int radius)
    {
        int size = radius * 2;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] colors = new Color[size * size];
        
        float r2 = radius * radius;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - radius + 0.5f;
                float dy = y - radius + 0.5f;
                float dist2 = dx * dx + dy * dy;
                
                int index = x + y * size;
                if (dist2 <= r2)
                {
                    // Antialiased edge
                    float dist = Mathf.Sqrt(dist2);
                    float edge = radius - dist;
                    float alpha = Mathf.Clamp01(edge);
                    colors[index] = new Color(1f, 1f, 1f, alpha);
                }
                else
                {
                    colors[index] = Color.clear;
                }
            }
        }
        
        texture.SetPixels(colors);
        texture.Apply();
        
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private void UpdateAimingAndTrajectory()
    {
        if (fpc == null) return;

        bool disableMovement = inputReader != null && (inputReader.AreInputsDisabled || inputReader.AreInputsDisabledExceptLook || inputReader.AreInputsDisabledExceptInteract);
        if (disableMovement)
        {
            CancelAiming();
            return;
        }

        // Aiming Logic (Hold Right-Click) - only active if not currently throwing
        if (Input.GetMouseButton(1) && !fpc.isThrowingAnim)
        {
            fpc.isAiming = true;
            if (trajectoryLine != null) trajectoryLine.enabled = true;

            // Set Aiming bool parameter to true to transition to Goalie Throw (1) wind-up
            if (animator != null && animator.enabled)
            {
                animator.SetBool("Aiming", true);
            }

            // Offset origin to the right (X = +0.3) and slightly down (Y = -0.2) from the camera POV to simulate throwing from the right side of the screen
            Vector3 throwOrigin = playerCamera != null 
                ? playerCamera.transform.position + playerCamera.transform.right * 0.3f + playerCamera.transform.forward * 0.5f - playerCamera.transform.up * 0.2f 
                : transform.position + transform.right * 0.3f + Vector3.up * 1.3f;
            Vector3 throwVelocity = playerCamera != null ? playerCamera.transform.forward * throwForce : transform.forward * throwForce;

            Vector3[] points = new Vector3[trajectoryResolution];
            int activePointsCount = 0;
            Vector3 currentPos = throwOrigin;
            Vector3 currentVelocity = throwVelocity;
            points[0] = currentPos;
            activePointsCount = 1;

            bool hitSomething = false;
            Vector3 hitPosition = Vector3.zero;
            Vector3 hitNormal = Vector3.up;

            for (int i = 1; i < trajectoryResolution; i++)
            {
                float t = trajectoryStepTime;
                Vector3 nextPos = currentPos + currentVelocity * t + 0.5f * Physics.gravity * t * t;
                Vector3 stepDirection = nextPos - currentPos;
                float stepDistance = stepDirection.magnitude;

                // Raycast to detect collisions along each segment
                RaycastHit hit;
                // Exclude the player from collision detection
                int playerLayerMask = ~(1 << gameObject.layer);
                if (Physics.Raycast(currentPos, stepDirection.normalized, out hit, stepDistance, playerLayerMask))
                {
                    points[i] = hit.point;
                    activePointsCount++;
                    hitSomething = true;
                    hitPosition = hit.point;
                    hitNormal = hit.normal;
                    break;
                }

                points[i] = nextPos;
                activePointsCount++;
                currentPos = nextPos;
                currentVelocity += Physics.gravity * t;
            }

            if (trajectoryLine != null)
            {
                trajectoryLine.positionCount = activePointsCount;
                for (int i = 0; i < activePointsCount; i++)
                {
                    trajectoryLine.SetPosition(i, points[i]);
                }
            }

            // Position and align landing marker
            if (landingMarker != null)
            {
                if (hitSomething)
                {
                    landingMarker.SetActive(true);
                    landingMarker.transform.position = hitPosition + hitNormal * 0.01f;
                    landingMarker.transform.rotation = Quaternion.LookRotation(hitNormal) * Quaternion.Euler(90f, 0f, 0f);
                }
                else
                {
                    landingMarker.SetActive(false);
                }
            }

            // Throw Logic (Left-Click while aiming)
            if (Input.GetMouseButtonDown(0))
            {
                ThrowObject(throwOrigin, throwVelocity);
            }
        }
        else
        {
            if (fpc.isAiming)
            {
                CancelAiming();
            }
        }
    }

    private void ThrowObject(Vector3 origin, Vector3 velocity)
    {
        if (fpc == null) return;

        // Cancel aiming state and hide visuals instantly
        fpc.isAiming = false;
        if (trajectoryLine != null) trajectoryLine.enabled = false;
        if (landingMarker != null) landingMarker.SetActive(false);

        // Set camera near clip plane to a very small value to prevent character arms/shoulders from clipping during the throw
        if (playerCamera != null)
        {
            playerCamera.nearClipPlane = 0.01f;
        }

        // Transition from Throw1 to Throw2 via trigger
        if (animator != null && animator.enabled)
        {
            animator.SetBool("Aiming", false);
            animator.SetTrigger("Throw");
        }
 
        // Trigger throw timing block (blocks aiming for the swing duration)
        fpc.isThrowingAnim = true;
        fpc.throwAnimTimer = 0f;
        fpc.throwExitBlend = 1f;
 
        // Start delayed projectile spawn to match the release point (0.1s delay after resuming)
        StartCoroutine(ThrowCoroutine(origin, velocity, 0.1f));
    }
 
    private System.Collections.IEnumerator ThrowCoroutine(Vector3 origin, Vector3 velocity, float delay)
    {
        yield return new WaitForSeconds(delay);

        // Revert spawn position to the camera POV offset origin
        Vector3 spawnPos = origin;
 
        if (PhotonNetwork.IsConnected)
        {
            // Spawn network object via PUN
            GameObject rockObj = PhotonNetwork.Instantiate(throwablePrefabName, spawnPos, Quaternion.identity);
            ThrowableObject throwable = rockObj.GetComponent<ThrowableObject>();
            if (throwable != null)
            {
                throwable.InitializeVelocity(velocity);
            }
        }
        else
        {
            // Spawn local object
            GameObject rockPrefab = Resources.Load<GameObject>(throwablePrefabName);
            if (rockPrefab != null)
            {
                GameObject rockObj = Instantiate(rockPrefab, spawnPos, Quaternion.identity);
                ThrowableObject throwable = rockObj.GetComponent<ThrowableObject>();
                if (throwable != null)
                {
                    throwable.InitializeVelocity(velocity);
                }
            }
        }
    }
}
