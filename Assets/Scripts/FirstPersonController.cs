using UnityEngine;
using Photon.Pun;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PhotonView))]
public class FirstPersonController : MonoBehaviourPun
{
    public static event System.Action<Vector3> OnLocalPlayerRespawn;

    [Header("Movement Settings")]
    public float moveSpeed = 5.0f; // Faster walking speed (5.0f)
    public float runSpeed = 8.5f;  // Faster running speed (8.5f)
    public float gravity = 20f;
    public float jumpSpeed = 7.2f;

    [Header("Look Settings")]
    public float mouseSensitivity = 2f;
    public float minPitch = -60f;
    public float maxPitch = 60f;

    [Header("Camera Settings")]
    [Tooltip("Target transform for the head. If null, the camera will be positioned relative to the player's transform.")]
    public Transform headJoint;
    public Vector3 cameraOffset = new Vector3(0f, 0.15f, 0.15f); // Slightly forward to prevent clipping through the mummy's head/wrapping mesh

    private CharacterController controller;
    private Animator animator;
    private Camera playerCamera;
    
    private float pitch = 0f;
    private float verticalVelocity = 0f; // Track vertical velocity independently to prevent sticky grounding jitter

    private Transform hipsJoint;
    private float defaultHipsLocalY;
    private Transform armatureRootJoint;

    [Header("Vault Settings")]
    public float vaultMaxDistance = 1.5f;
    public float vaultDuration = 0.6f;
    public float vaultPeakOffset = 0.5f;

    private bool isVaulting = false;
    private float vaultTimer = 0f;
    private Vector3 vaultStartPos;
    private Vector3 vaultTargetPos;
    private float vaultPeakHeight;

    [Header("Throw Settings")]
    public string throwablePrefabName = "ThrowableRock";
    public float throwForce = 15f;
    public int trajectoryResolution = 30;
    public float trajectoryStepTime = 0.05f;

    private LineRenderer trajectoryLine;
    private GameObject landingMarker;
    private bool isAiming = false;

    private Transform leftLegJoint;
    private Transform rightLegJoint;
    private Transform rightArmJoint;
    private Transform leftArmJoint;
    private Transform leftElbowJoint;  // Elbow_L (actual bone name)
    private Transform leftHandJoint;   // Hand_L
    private Transform rightHandJoint;  // Hand_R
    private Transform leftKneeJoint;   // LowerLeg_L
    private Transform rightKneeJoint;  // LowerLeg_R
    private Transform rightElbowJoint; // Elbow_R

    private Quaternion defaultLeftLegRot;
    private Quaternion defaultRightLegRot;
    private Quaternion defaultRightArmRot;
    private Quaternion defaultLeftArmRot;
    private Quaternion defaultHipsRot;
    private Quaternion defaultLeftElbowRot;
    private Quaternion defaultLeftHandRot;
    private Quaternion defaultLeftKneeRot;
    private Quaternion defaultRightKneeRot;
    private Quaternion defaultRightElbowRot;

    [Header("Throw Animation")]
    public float throwAnimDuration = 2.65f;
    private float throwAnimTimer = 0f;
    private bool isThrowingAnim = false;
    private float throwExitBlend = 0f;
    private float throwExitDuration = 0.2f;
    private float originalNearClip = 0.3f;
 
    [Header("Camera Aim Settings")]
    public float cameraAimBlendSpeed = 8f;
    private float cameraAimBlend = 0f;

    [Header("Checkpoint System")]
    public float deathYThreshold = -15f;
    private Vector3 activeCheckpointPosition;

    // Camera shake fields
    private Vector3 cameraShakeOffset = Vector3.zero;

    // Death Spam Settings
    [Header("Death Spam Settings")]
    public int requiredClicksForRespawn = 5;
    private bool isDead = false;
    private int clickCountToRespawn = 0;
    private GameObject deathOverlayObj;
    private UnityEngine.UI.Text deathClicksText;
    private UnityEngine.UI.Image deathProgressBarFill;
 
    // Bandage Overlay Settings
    private struct BandageConfig
    {
        public string name;
        public Vector2 position;
        public Vector2 size;
        public float rotation;
        public float slideDirection; // 1 for forward along rotation axis, -1 for backward
    }
    private GameObject bandageCanvasObj;
    private System.Collections.Generic.List<RectTransform> bandageWraps = new System.Collections.Generic.List<RectTransform>();
    private System.Collections.Generic.List<Vector2> startPositions = new System.Collections.Generic.List<Vector2>();
    private System.Collections.Generic.List<Vector2> targetPositions = new System.Collections.Generic.List<Vector2>();
    private bool bandageActive = false;
    private Coroutine bandageAnimCoroutine;

    // Vault wall IK
    private Vector3 vaultWallContactPoint;
    private float vaultExitBlend = 0f;
    private float vaultExitDuration = 0.25f;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();

        // Ensure player has a kinematic Rigidbody so OnTriggerEnter is processed correctly by Unity's physics system
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }
        rb.isKinematic = true;
        rb.useGravity = false;

        // Add a trigger CapsuleCollider to handle OnTriggerEnter correctly for static triggers
        CapsuleCollider triggerCollider = GetComponent<CapsuleCollider>();
        if (triggerCollider == null)
        {
            triggerCollider = gameObject.AddComponent<CapsuleCollider>();
        }
        triggerCollider.isTrigger = true;
        if (controller != null)
        {
            triggerCollider.center = controller.center;
            triggerCollider.radius = controller.radius + 0.02f; // Slightly larger to register overlaps cleanly
            triggerCollider.height = controller.height;
        }

        // Cache the armature root and hips joints to prevent animation offset issues
        armatureRootJoint = transform.Find("Root");
        hipsJoint = FindDeepChild(transform, "Hips");
        if (hipsJoint != null)
        {
            defaultHipsLocalY = hipsJoint.localPosition.y;
            defaultHipsRot = hipsJoint.localRotation;
        }

        leftLegJoint = FindDeepChild(transform, "UpperLeg_L");
        rightLegJoint = FindDeepChild(transform, "UpperLeg_R");
        rightArmJoint = FindDeepChild(transform, "Shoulder_R");
        leftArmJoint = FindDeepChild(transform, "Shoulder_L");
        leftElbowJoint = FindDeepChild(transform, "Elbow_L");
        leftHandJoint = FindDeepChild(transform, "Hand_L");
        rightHandJoint = FindDeepChild(transform, "Hand_R");
        leftKneeJoint = FindDeepChild(transform, "LowerLeg_L");
        rightKneeJoint = FindDeepChild(transform, "LowerLeg_R");
        rightElbowJoint = FindDeepChild(transform, "Elbow_R");

        if (leftLegJoint) defaultLeftLegRot = leftLegJoint.localRotation;
        if (rightLegJoint) defaultRightLegRot = rightLegJoint.localRotation;
        if (rightArmJoint) defaultRightArmRot = rightArmJoint.localRotation;
        if (leftArmJoint) defaultLeftArmRot = leftArmJoint.localRotation;
        if (leftElbowJoint) defaultLeftElbowRot = leftElbowJoint.localRotation;
        if (leftHandJoint) defaultLeftHandRot = leftHandJoint.localRotation;
        if (leftKneeJoint) defaultLeftKneeRot = leftKneeJoint.localRotation;
        if (rightKneeJoint) defaultRightKneeRot = rightKneeJoint.localRotation;
        if (rightElbowJoint) defaultRightElbowRot = rightElbowJoint.localRotation;

        // If this is a remote player, we don't control it
        if (PhotonNetwork.IsConnected && !photonView.IsMine)
        {
            // Disable CharacterController and FirstPersonController inputs
            if (controller != null) controller.enabled = false;
            
            // Also disable any camera or listeners attached
            var listener = GetComponentInChildren<AudioListener>();
            if (listener != null) listener.enabled = false;

            // Make sure the LineRenderer on this remote copy is disabled/destroyed so other players never see it
            var lr = GetComponent<LineRenderer>();
            if (lr != null) Destroy(lr);
            
            return;
        }

        // Find the main camera
        playerCamera = Camera.main;
        if (playerCamera == null)
        {
            playerCamera = FindObjectOfType<Camera>();
        }

        if (playerCamera != null)
        {
            // Cache the original camera near clip plane
            originalNearClip = playerCamera.nearClipPlane;

            // If camera has CameraFollow, disable it so it doesn't fight this script
            var follow = playerCamera.GetComponent<CameraFollow>();
            if (follow != null)
            {
                follow.enabled = false;
            }
        }

        // Find the head joint if not assigned
        if (headJoint == null)
        {
            headJoint = FindDeepChild(transform, "Head");
        }

        // Lock cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Initialize starting position as default checkpoint fallback
        activeCheckpointPosition = transform.position;

        InitializeThrowVisuals();

        if (!PhotonNetwork.IsConnected || photonView.IsMine)
        {
            CreateDeathUI();
        }
    }

    void Update()
    {
        if (PhotonNetwork.IsConnected && !photonView.IsMine) return;

        if (Input.GetKeyDown(KeyCode.B) && !isDead)
        {
            ToggleBandageOverlay();
        }

        if (isDead)
        {
            if (Input.GetMouseButtonDown(0))
            {
                clickCountToRespawn++;
                TriggerCameraShake(0.12f, 0.15f); // slight shake feedback on click
                UpdateDeathUI();
                
                if (clickCountToRespawn >= requiredClicksForRespawn)
                {
                    ExecuteRespawn();
                }
            }

            // Lock camera movements during death, but keep positioning stable
            if (playerCamera != null)
            {
                Vector3 activeOffset = cameraOffset;
                if (headJoint != null)
                {
                    playerCamera.transform.position = headJoint.position + transform.TransformDirection(activeOffset) + cameraShakeOffset;
                }
                else
                {
                    playerCamera.transform.position = transform.position + new Vector3(activeOffset.x, 1.6f, activeOffset.z) + cameraShakeOffset;
                }
                playerCamera.transform.rotation = Quaternion.Euler(pitch, transform.eulerAngles.y, 0f);
            }
            return;
        }

        // 1. Camera Look Rotation
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        // Rotate player body horizontally via mouse look
        transform.Rotate(Vector3.up * mouseX);

        // Rotate camera vertically (pitch)
        pitch -= mouseY;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        // Update camera zoom/shoulder offset during aiming and throwing
        bool wantShoulderCam = isAiming || isThrowingAnim;
        if (wantShoulderCam)
        {
            cameraAimBlend = Mathf.MoveTowards(cameraAimBlend, 1f, Time.deltaTime * cameraAimBlendSpeed);
        }
        else
        {
            cameraAimBlend = Mathf.MoveTowards(cameraAimBlend, 0f, Time.deltaTime * cameraAimBlendSpeed);
        }

        if (playerCamera != null)
        {
            Vector3 activeOffset = cameraOffset;
            activeOffset.y += 0.1f * cameraAimBlend;
            //activeOffset.z -= 0.1f * cameraAimBlend;  // Shift camera backward
            activeOffset.x += 0.25f * cameraAimBlend; // Shift camera slightly right for shoulder view

            if (headJoint != null)
            {
                playerCamera.transform.position = headJoint.position + transform.TransformDirection(activeOffset) + cameraShakeOffset;
            }
            else
            {
                playerCamera.transform.position = transform.position + new Vector3(activeOffset.x, 1.6f, activeOffset.z) + cameraShakeOffset;
            }
            
            // Set rotation
            playerCamera.transform.rotation = Quaternion.Euler(pitch, transform.eulerAngles.y, 0f);
        }

        // Vaulting Logic
        if (isVaulting)
        {
            float t = Mathf.Clamp01(vaultTimer / vaultDuration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);
            Vector3 currentXZ = Vector3.Lerp(vaultStartPos, vaultTargetPos, smoothT);
            float baseY = Mathf.Lerp(vaultStartPos.y, vaultTargetPos.y, smoothT);
            float currentY = baseY + Mathf.Sin(smoothT * Mathf.PI) * (vaultPeakHeight - baseY);
            
            transform.position = new Vector3(currentXZ.x, currentY, currentXZ.z);

            if (animator != null && animator.enabled)
            {
                animator.SetBool("IsGrounded", false);
                animator.SetBool("OnGround", false);
            }
            return;
        }

        // 2. Player Movement
        float moveHorizontal = Input.GetAxisRaw("Horizontal"); // Changed from GetAxis to GetAxisRaw for instant stopping response
        float moveVertical = Input.GetAxisRaw("Vertical");     // Changed from GetAxis to GetAxisRaw for instant stopping response

        // Determine if running (holding Shift and moving forward)
        bool isRunning = (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) && (Input.GetKey(KeyCode.W) || moveVertical > 0.1f);
        float currentSpeed = isRunning ? runSpeed : moveSpeed;

        Vector3 inputDir = transform.right * moveHorizontal + transform.forward * moveVertical;
        Vector3 move = inputDir.normalized * currentSpeed;
        
        // Robust Vertical Physics
        if (controller.isGrounded)
        {
            // Only apply small constant grounding force if we are not moving upwards from a jump
            if (verticalVelocity < 0f)
            {
                verticalVelocity = -2f; 
            }

            if (Input.GetButtonDown("Jump"))
            {
                float obstacleHeight;
                if (CheckVault(out vaultStartPos, out vaultTargetPos, out obstacleHeight))
                {
                    float peakHeight = Mathf.Max(vaultStartPos.y, obstacleHeight) + 0.1f;
                    photonView.RPC("StartVaultRPC", RpcTarget.All, vaultStartPos, vaultTargetPos, vaultDuration, peakHeight);
                }
                else
                {
                    verticalVelocity = jumpSpeed;
                }
            }
        }
        else
        {
            // Apply gravity over time in the air
            verticalVelocity -= gravity * Time.deltaTime;
        }

        // Combine horizontal movement and vertical velocity
        move.y = verticalVelocity;

        // Move character controller
        controller.Move(move * Time.deltaTime);

        // 3. Update Animator
        if (animator != null && animator.enabled)
        {
            // Calculate actual horizontal speed relative to max speed (runSpeed)
            Vector3 horizontalVelocity = new Vector3(controller.velocity.x, 0f, controller.velocity.z);
            float speedPercent = horizontalVelocity.magnitude / runSpeed;
            animator.SetFloat("Speed", speedPercent, 0.15f, Time.deltaTime); // Maps walking to ~0.58 and running to 1.0 with 0.15s damp time
            animator.SetFloat("Forward", speedPercent, 0.15f, Time.deltaTime);
            bool groundedState = controller.isGrounded && !isThrowingAnim;
            animator.SetBool("IsGrounded", groundedState);
            animator.SetBool("OnGround", groundedState);
        }

        // Check for falling below death boundaries
        if (transform.position.y < deathYThreshold)
        {
            Debug.Log("Respawn triggered via falling check: Y=" + transform.position.y + ", deathYThreshold=" + deathYThreshold);
            Respawn();
        }

        UpdateAimingAndTrajectory();
    }

    void LateUpdate()
    {
        if (animator == null) return;

        // Procedural Throw Animation (Right Arm swing) - only active if not vaulting
        if ((isThrowingAnim || throwExitBlend > 0f) && !isVaulting)
        {
            if (isAiming)
            {
                isThrowingAnim = false;
                throwExitBlend = 0f;
            }
            else
            {
                if (isThrowingAnim)
                {
                    throwAnimTimer += Time.deltaTime;
                    throwExitBlend = 1f;
                    if (throwAnimTimer >= throwAnimDuration)
                    {
                        isThrowingAnim = false;
                        if (playerCamera != null)
                        {
                            playerCamera.nearClipPlane = originalNearClip;
                        }
                    }
                }
                else
                {
                    throwExitBlend -= Time.deltaTime / throwExitDuration;
                    if (throwExitBlend < 0f) throwExitBlend = 0f;
                }

                // Let the Goalie Throw and WalkBackward animations control all bones naturally
            }
        }

        if (isVaulting || vaultExitBlend > 0f)
        {
            if (isVaulting)
            {
                vaultTimer += Time.deltaTime;
                vaultExitBlend = 1f;
            }
            else
            {
                vaultExitBlend -= Time.deltaTime / vaultExitDuration;
                if (vaultExitBlend < 0f) vaultExitBlend = 0f;
            }

            float t = isVaulting ? Mathf.Clamp01(vaultTimer / vaultDuration) : 1f;
            float smoothT = Mathf.SmoothStep(0f, 1f, t);
            float weight = vaultExitBlend;

            // Procedural Vaulting Animation (Sideways body cross: left hand supporting, right arm raised, legs horizontal)
            float roll = Mathf.Sin(smoothT * Mathf.PI) * -60f; // Roll body left (left shoulder down, right side up)
            float yaw = Mathf.Sin(smoothT * Mathf.PI) * 60f;   // Yaw body right (chest rotates right)
            float pitch = Mathf.Sin(smoothT * Mathf.PI) * 10f; // Pitch body forward slightly

            if (hipsJoint != null)
            {
                hipsJoint.localRotation = Quaternion.Slerp(hipsJoint.localRotation, defaultHipsRot * Quaternion.Euler(pitch, yaw, roll), weight);
                Vector3 localPos = hipsJoint.localPosition;
                localPos.x = Mathf.Lerp(hipsJoint.localPosition.x, 0f, weight);
                localPos.z = Mathf.Lerp(hipsJoint.localPosition.z, 0f, weight);
                
                // Lower hips Y position during vault to bring shoulder close to the wall (so hand reaches the wall)
                float targetY = defaultHipsLocalY + (Mathf.Sin(smoothT * Mathf.PI) * -0.45f);
                localPos.y = Mathf.Lerp(hipsJoint.localPosition.y, targetY, weight);
                hipsJoint.localPosition = localPos;
            }

            if (armatureRootJoint != null)
            {
                armatureRootJoint.localPosition = Vector3.Lerp(armatureRootJoint.localPosition, Vector3.zero, weight);
                armatureRootJoint.localRotation = Quaternion.Slerp(armatureRootJoint.localRotation, Quaternion.identity, weight);
            }

            // Let the animation (Jump_Moving) drive all legs and arms, while we only procedurally position the hips and left hand IK.

            // Procedural hand-on-wall IK: aim left forearm & hand toward wall contact point
            if (leftElbowJoint != null && leftHandJoint != null)
            {
                // Weight: ease-in to 1.0 by 15%, hold until 70%, ease-out to 0.0 by 85%
                float ikWeight = 0f;
                if (smoothT < 0.15f)
                {
                    ikWeight = smoothT / 0.15f;
                }
                else if (smoothT <= 0.7f)
                {
                    ikWeight = 1.0f;
                }
                else if (smoothT < 0.85f)
                {
                    ikWeight = 1.0f - ((smoothT - 0.7f) / 0.15f);
                }
                ikWeight *= weight;

                if (isVaulting && smoothT < 0.85f && ikWeight > 0.01f)
                {
                    // Frame-by-frame raycast down to detect the wall top surface directly under/near the hand
                    Vector3 sampleOrigin = transform.position 
                        + transform.forward * Mathf.Lerp(0.5f, -0.3f, (smoothT - 0.15f) / 0.7f) 
                        + transform.right * -0.3f 
                        + Vector3.up * 2.0f;
                    
                    RaycastHit hit;
                    Vector3 targetSurface = vaultWallContactPoint;
                    if (Physics.Raycast(sampleOrigin, Vector3.down, out hit, 4f))
                    {
                        targetSurface = hit.point;
                    }

                    // Aim elbow/forearm toward the wall top surface
                    Vector3 toWall = (targetSurface - leftElbowJoint.position).normalized;
                    Vector3 localDir = leftElbowJoint.parent.InverseTransformDirection(toWall);
                    Quaternion targetElbowRot = Quaternion.LookRotation(localDir, leftElbowJoint.parent.up);
                    leftElbowJoint.localRotation = Quaternion.Slerp(leftElbowJoint.localRotation, targetElbowRot, ikWeight);

                    // Point hand flat onto the surface (palm down)
                    Vector3 handToWall = (targetSurface - leftHandJoint.position).normalized;
                    Vector3 localHandDir = leftHandJoint.parent.InverseTransformDirection(handToWall);
                    Quaternion targetHandRot = Quaternion.LookRotation(localHandDir, Vector3.up);
                    leftHandJoint.localRotation = Quaternion.Slerp(leftHandJoint.localRotation, targetHandRot, ikWeight);
                }
                else
                {
                    leftElbowJoint.localRotation = Quaternion.Slerp(leftElbowJoint.localRotation, defaultLeftElbowRot, 1f - ikWeight);
                    leftHandJoint.localRotation = Quaternion.Slerp(leftHandJoint.localRotation, defaultLeftHandRot, 1f - ikWeight);
                }
            }

            // Right Arm is driven by the animation too

            if (isVaulting && t >= 1f)
            {
                isVaulting = false;
                if (controller != null) controller.enabled = true;
            }

            if (!isVaulting && vaultExitBlend <= 0f)
            {
                ResetBones();
            }
            return;
        }

        // Check if standard vertical jumps or running leaps are active in the current state
        bool isCurrentJump = animator.GetCurrentAnimatorStateInfo(0).IsName("Jump_Start") || 
                             animator.GetCurrentAnimatorStateInfo(0).IsName("Jump_Loop") ||
                             animator.GetCurrentAnimatorStateInfo(0).IsName("Airborne") ||
                             animator.GetCurrentAnimatorStateInfo(0).IsName("Jump_Moving");
        bool isCurrentRunJump = animator.GetCurrentAnimatorStateInfo(0).IsName("Run_Jump");

        // Check if they are active in the next state during transitions
        bool isTransitioning = animator.IsInTransition(0);
        bool isNextJump = isTransitioning && (animator.GetNextAnimatorStateInfo(0).IsName("Jump_Start") || 
                                              animator.GetNextAnimatorStateInfo(0).IsName("Jump_Loop") ||
                                              animator.GetNextAnimatorStateInfo(0).IsName("Airborne") ||
                                              animator.GetNextAnimatorStateInfo(0).IsName("Jump_Moving"));
        bool isNextRunJump = isTransitioning && animator.GetNextAnimatorStateInfo(0).IsName("Run_Jump");

        bool isJumpingOrLeaping = isCurrentJump || isCurrentRunJump || isNextJump || isNextRunJump;

        if (isJumpingOrLeaping)
        {
            // 1. Lock the armature root's local position and rotation to keep the skeleton centered and straight.
            if (armatureRootJoint != null)
            {
                armatureRootJoint.localPosition = Vector3.zero;
                armatureRootJoint.localRotation = Quaternion.identity;
            }

            // 2. Lock the Hips' local position (X, Y, Z) to keep the mesh perfectly centered on the GameObject.
            if (hipsJoint != null)
            {
                Vector3 localPos = hipsJoint.localPosition;
                localPos.y = defaultHipsLocalY;
                localPos.x = 0f;
                localPos.z = 0f;
                hipsJoint.localPosition = localPos;
            }
        }
    }

    // Animation Event receiver to prevent Unity console warnings
    public void PlayJumpSound()
    {
        // Optional: Play jumping sound effects here
    }

    public void PlayLandSound()
    {
        // Optional: Play landing sound effects here
    }

    [PunRPC]
    public void StartVaultRPC(Vector3 startPos, Vector3 targetPos, float duration, float peakHeight)
    {
        isVaulting = true;
        vaultTimer = 0f;
        vaultStartPos = startPos;
        vaultTargetPos = targetPos;
        vaultDuration = duration;
        vaultPeakHeight = peakHeight;

        if (controller != null)
        {
            controller.enabled = false;
        }
        if (animator != null && animator.enabled)
        {
            animator.CrossFadeInFixedTime("Jump_Moving", 0.12f);
        }
    }

    private bool CheckVault(out Vector3 startPos, out Vector3 targetPos, out float obstacleHeight)
    {
        startPos = transform.position;
        targetPos = Vector3.zero;
        obstacleHeight = 0f;

        Vector3 forward = transform.forward;
        Vector3 originLower = transform.position + Vector3.up * 0.3f; // Shin/Knee height
        Vector3 originUpper = transform.position + Vector3.up * 1.5f; // Head height

        // Raycast forward to detect obstacle
        RaycastHit shinHit;
        if (Physics.Raycast(originLower, forward, out shinHit, vaultMaxDistance))
        {
            // Ignore triggers and other players
            if (shinHit.collider != null && !shinHit.collider.isTrigger)
            {
                if (shinHit.collider.CompareTag("Player") || shinHit.collider.GetComponentInParent<FirstPersonController>() != null)
                {
                    return false;
                }

                // Check that head height is clear
                if (!Physics.Raycast(originUpper, forward, vaultMaxDistance))
                {
                    // 1. Find the top of the obstacle by casting down near the obstacle front
                    Vector3 downObstacleOrigin = transform.position + forward * (shinHit.distance + 0.15f) + Vector3.up * 2.5f;
                    RaycastHit obstacleHit;
                    if (Physics.Raycast(downObstacleOrigin, Vector3.down, out obstacleHit, 5f))
                    {
                        obstacleHeight = obstacleHit.point.y;
                        float heightDiff = obstacleHeight - transform.position.y;

                        // Obstacle must be between 0.3m and 1.5m tall
                        if (heightDiff >= 0.3f && heightDiff <= 1.5f)
                        {
                            // 2. Find the landing ground on the other side of the obstacle
                            Vector3 downLandingOrigin = transform.position + forward * (shinHit.distance + 1.2f) + Vector3.up * 2.5f;
                            RaycastHit landingHit;
                            if (Physics.Raycast(downLandingOrigin, Vector3.down, out landingHit, 5f))
                            {
                                float landingGroundHeight = landingHit.point.y;
                                
                                startPos = transform.position;
                                targetPos = shinHit.point + forward * 1.2f;
                                targetPos.y = landingGroundHeight;

                                // Store wall contact point: top surface of the obstacle, centered on the hit
                                vaultWallContactPoint = new Vector3(shinHit.point.x, obstacleHeight, shinHit.point.z);

                                // Make sure landing area is clear of walls/obstacles
                                if (!Physics.CheckSphere(targetPos + Vector3.up * 0.9f, 0.3f))
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }
            }
        }
        return false;
    }

    private void ResetBones()
    {
        if (leftLegJoint) leftLegJoint.localRotation = defaultLeftLegRot;
        if (rightLegJoint) rightLegJoint.localRotation = defaultRightLegRot;
        if (leftKneeJoint) leftKneeJoint.localRotation = defaultLeftKneeRot;
        if (rightKneeJoint) rightKneeJoint.localRotation = defaultRightKneeRot;
        if (rightArmJoint) rightArmJoint.localRotation = defaultRightArmRot;
        if (leftArmJoint) leftArmJoint.localRotation = defaultLeftArmRot;
        if (hipsJoint) hipsJoint.localRotation = defaultHipsRot;
        if (leftElbowJoint) leftElbowJoint.localRotation = defaultLeftElbowRot;
        if (leftHandJoint) leftHandJoint.localRotation = defaultLeftHandRot;
        if (rightElbowJoint) rightElbowJoint.localRotation = defaultRightElbowRot;
    }

    private Transform FindDeepChild(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            Transform result = FindDeepChild(child, name);
            if (result != null) return result;
        }
        return null;
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
        // Aiming Logic (Hold Right-Click) - only active if not currently throwing
        if (Input.GetMouseButton(1) && !isThrowingAnim && (!PhotonNetwork.IsConnected || photonView.IsMine))
        {
            isAiming = true;
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
            if (isAiming)
            {
                isAiming = false;
                if (trajectoryLine != null) trajectoryLine.enabled = false;
                if (landingMarker != null) landingMarker.SetActive(false);

                // Cancel Aiming bool parameter to return to Grounded
                if (animator != null && animator.enabled)
                {
                    animator.SetBool("Aiming", false);
                }
            }
        }
    }

    private void ThrowObject(Vector3 origin, Vector3 velocity)
    {
        // Cancel aiming state and hide visuals instantly
        isAiming = false;
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
        isThrowingAnim = true;
        throwAnimTimer = 0f;
        throwExitBlend = 1f;
 
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

    private bool CompareSafeTag(Collider col, string tag)
    {
        if (col == null) return false;
        #if UNITY_EDITOR
        // Verify if tag is actually registered in the current editor session to prevent native console errors
        if (System.Array.IndexOf(UnityEditorInternal.InternalEditorUtility.tags, tag) < 0)
        {
            return false;
        }
        #endif
        try
        {
            return col.CompareTag(tag);
        }
        catch
        {
            return false;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Only execute checkpoint saving and death zones for the local player
        if (PhotonNetwork.IsConnected && !photonView.IsMine) return;

        if (CompareSafeTag(other, "Checkpoint"))
        {
            // Try to find a custom designated spawn point child, otherwise use the player's exact contact position
            Transform spawnPoint = other.transform.Find("SpawnPoint");
            if (spawnPoint == null) spawnPoint = other.transform.Find("Spawn");

            if (spawnPoint != null)
            {
                activeCheckpointPosition = spawnPoint.position;
            }
            else
            {
                activeCheckpointPosition = transform.position;
            }
            Debug.Log("Checkpoint saved at: " + activeCheckpointPosition);
        }
        else if (CompareSafeTag(other, "KillZone") || CompareSafeTag(other, "DeadZone"))
        {
            Debug.Log("Respawn triggered via OnTriggerEnter with: " + other.gameObject.name + ", Tag: " + other.gameObject.tag);
            Respawn();
        }
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        // Handle solid physical checkpoints and death zones
        if (PhotonNetwork.IsConnected && !photonView.IsMine) return;

        if (CompareSafeTag(hit.collider, "Checkpoint"))
        {
            // Try to find a custom designated spawn point child, otherwise use the player's exact contact position
            Transform spawnPoint = hit.collider.transform.Find("SpawnPoint");
            if (spawnPoint == null) spawnPoint = hit.collider.transform.Find("Spawn");

            if (spawnPoint != null)
            {
                activeCheckpointPosition = spawnPoint.position;
            }
            else
            {
                activeCheckpointPosition = transform.position;
            }
            Debug.Log("Checkpoint saved (via controller hit) at: " + activeCheckpointPosition);
        }
        else if (CompareSafeTag(hit.collider, "KillZone") || CompareSafeTag(hit.collider, "DeadZone"))
        {
            Debug.Log("Respawn triggered via OnControllerColliderHit with: " + hit.collider.gameObject.name + ", Tag: " + hit.collider.gameObject.tag);
            Respawn();
        }
    }

    public void Respawn()
    {
        // Only respawn the local player client
        if (PhotonNetwork.IsConnected && !photonView.IsMine) return;

        if (!isDead)
        {
            isDead = true;
            clickCountToRespawn = 0;
            
            // Reset the giant to spawn position!
            var giant = FindObjectOfType<GiantPharaohAI>();
            if (giant != null)
            {
                giant.ResetToSpawn();
            }

            // Ensure UI exists and is shown
            if (deathOverlayObj == null)
            {
                CreateDeathUI();
            }

            if (deathOverlayObj != null)
            {
                deathOverlayObj.SetActive(true);
                UpdateDeathUI();
            }
        }
    }

    private void ExecuteRespawn()
    {
        if (deathOverlayObj != null)
        {
            deathOverlayObj.SetActive(false);
        }

        Debug.Log("Player respawning at recent checkpoint after click spam: " + activeCheckpointPosition);

        // Temporarily disable CharacterController so we can modify the transform position directly
        if (controller != null)
        {
            controller.enabled = false;
        }

        transform.position = activeCheckpointPosition;
        verticalVelocity = 0f;

        if (controller != null)
        {
            controller.enabled = true;
        }

        isDead = false;

        OnLocalPlayerRespawn?.Invoke(activeCheckpointPosition);
    }

    private void CreateDeathUI()
    {
        if (deathOverlayObj != null) return;

        // Create Canvas GameObject
        deathOverlayObj = new GameObject("DeathOverlayCanvas");
        Canvas canvas = deathOverlayObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;
        
        // Add CanvasScaler
        var scaler = deathOverlayObj.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        
        // Add GraphicRaycaster
        deathOverlayObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        // 1. Dark Red Overlay Panel
        GameObject panelObj = new GameObject("BackgroundPanel");
        panelObj.transform.SetParent(deathOverlayObj.transform, false);
        var panelImage = panelObj.AddComponent<UnityEngine.UI.Image>();
        panelImage.color = new Color(0.08f, 0.01f, 0.01f, 0.85f); // Transparent dark red
        
        var rectPanel = panelObj.GetComponent<RectTransform>();
        rectPanel.anchorMin = Vector2.zero;
        rectPanel.anchorMax = Vector2.one;
        rectPanel.sizeDelta = Vector2.zero;

        // 2. Title Text "YOU DIED"
        GameObject titleObj = new GameObject("TitleText");
        titleObj.transform.SetParent(panelObj.transform, false);
        var titleText = titleObj.AddComponent<UnityEngine.UI.Text>();
        titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        titleText.text = "YOU DIED";
        titleText.fontSize = 90;
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.color = new Color(0.9f, 0.1f, 0.1f, 1f);
        
        var rectTitle = titleObj.GetComponent<RectTransform>();
        rectTitle.anchorMin = new Vector2(0.5f, 0.6f);
        rectTitle.anchorMax = new Vector2(0.5f, 0.6f);
        rectTitle.anchoredPosition = new Vector2(0f, 50f);
        rectTitle.sizeDelta = new Vector2(800f, 150f);

        // Add a soft glow shadow component
        var shadow = titleObj.AddComponent<UnityEngine.UI.Shadow>();
        shadow.effectColor = new Color(1f, 0f, 0f, 0.5f);
        shadow.effectDistance = new Vector2(4f, -4f);

        // 3. Subtitle Text "Spam Left Click to Respawn!"
        GameObject subObj = new GameObject("SubtitleText");
        subObj.transform.SetParent(panelObj.transform, false);
        var subText = subObj.AddComponent<UnityEngine.UI.Text>();
        subText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        subText.text = "Spam Left Click to Respawn!";
        subText.fontSize = 35;
        subText.alignment = TextAnchor.MiddleCenter;
        subText.color = new Color(0.8f, 0.8f, 0.8f, 1f);
        
        var rectSub = subObj.GetComponent<RectTransform>();
        rectSub.anchorMin = new Vector2(0.5f, 0.5f);
        rectSub.anchorMax = new Vector2(0.5f, 0.5f);
        rectSub.anchoredPosition = new Vector2(0f, -30f);
        rectSub.sizeDelta = new Vector2(800f, 50f);

        // 4. Progress Text "(Clicks: 0 / 5)"
        GameObject progressTextObj = new GameObject("ProgressText");
        progressTextObj.transform.SetParent(panelObj.transform, false);
        deathClicksText = progressTextObj.AddComponent<UnityEngine.UI.Text>();
        deathClicksText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        deathClicksText.text = "Clicks: 0 / " + requiredClicksForRespawn;
        deathClicksText.fontSize = 28;
        deathClicksText.alignment = TextAnchor.MiddleCenter;
        deathClicksText.color = new Color(1f, 0.3f, 0.3f, 1f);
        
        var rectProg = progressTextObj.GetComponent<RectTransform>();
        rectProg.anchorMin = new Vector2(0.5f, 0.45f);
        rectProg.anchorMax = new Vector2(0.5f, 0.45f);
        rectProg.anchoredPosition = new Vector2(0f, -80f);
        rectProg.sizeDelta = new Vector2(400f, 40f);

        // 5. Progress Bar Background
        GameObject barBgObj = new GameObject("ProgressBarBackground");
        barBgObj.transform.SetParent(panelObj.transform, false);
        var barBgImage = barBgObj.AddComponent<UnityEngine.UI.Image>();
        barBgImage.color = new Color(0.2f, 0.05f, 0.05f, 1f);
        
        var rectBarBg = barBgObj.GetComponent<RectTransform>();
        rectBarBg.anchorMin = new Vector2(0.5f, 0.4f);
        rectBarBg.anchorMax = new Vector2(0.5f, 0.4f);
        rectBarBg.anchoredPosition = new Vector2(0f, -120f);
        rectBarBg.sizeDelta = new Vector2(400f, 20f);

        // 6. Progress Bar Fill
        GameObject barFillObj = new GameObject("ProgressBarFill");
        barFillObj.transform.SetParent(barBgObj.transform, false);
        deathProgressBarFill = barFillObj.AddComponent<UnityEngine.UI.Image>();
        deathProgressBarFill.color = new Color(0.9f, 0.1f, 0.1f, 1f);
        
        var rectBarFill = barFillObj.GetComponent<RectTransform>();
        rectBarFill.anchorMin = new Vector2(0f, 0f);
        rectBarFill.anchorMax = new Vector2(0f, 1f);
        rectBarFill.pivot = new Vector2(0f, 0.5f);
        rectBarFill.anchoredPosition = Vector2.zero;
        rectBarFill.sizeDelta = new Vector2(0f, 0f);

        // Hide initially
        deathOverlayObj.SetActive(false);
    }

    private void UpdateDeathUI()
    {
        if (deathOverlayObj == null) return;
        
        if (deathClicksText != null)
        {
            deathClicksText.text = "Clicks: " + clickCountToRespawn + " / " + requiredClicksForRespawn;
        }

        if (deathProgressBarFill != null)
        {
            float fillPct = (float)clickCountToRespawn / requiredClicksForRespawn;
            var rect = deathProgressBarFill.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(fillPct * 400f, 0f);
        }
    }

    public void TriggerCameraShake(float duration, float magnitude)
    {
        // Only shake local player camera
        if (PhotonNetwork.IsConnected && !photonView.IsMine) return;

        StartCoroutine(DoCameraShake(duration, magnitude));
    }

    private System.Collections.IEnumerator DoCameraShake(float duration, float magnitude)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float currentMagnitude = magnitude * (1f - (elapsed / duration));
            cameraShakeOffset = Random.insideUnitSphere * currentMagnitude;
            yield return null;
        }
        cameraShakeOffset = Vector3.zero;
    }

    private void ToggleBandageOverlay()
    {
        if (bandageCanvasObj == null)
        {
            CreateBandageUI();
        }

        bandageActive = !bandageActive;

        if (bandageAnimCoroutine != null)
        {
            StopCoroutine(bandageAnimCoroutine);
        }
        bandageAnimCoroutine = StartCoroutine(AnimateBandages(bandageActive));
    }

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
            
            // Clear list data
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
                    float rotation = rect.localRotation.eulerAngles.z;

                    // Calculate slide starting position along the rotation axis
                    float rad = rotation * Mathf.Deg2Rad;
                    Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
                    Vector2 offset = dir * (3600f * strip.slideDirection);
                    Vector2 startPos = targetPos + offset;

                    bandageWraps.Add(rect);
                    startPositions.Add(startPos);
                    targetPositions.Add(targetPos);

                    // Set initial state to starting position
                    rect.anchoredPosition = startPos;
                }
            }

            // Hide canvas initially
            bandageCanvasObj.SetActive(false);
            return;
        }

        // Fallback: Create Canvas GameObject programmatically if missing from hierarchy
        bandageCanvasObj = new GameObject("BandageOverlayCanvas");
        Canvas canvas = bandageCanvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 998; // Just under Death UI
        
        // Add CanvasScaler
        var scaler = bandageCanvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        
        // Add GraphicRaycaster (non-blocking for gameplay)
        bandageCanvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        // Create container panel
        GameObject containerObj = new GameObject("Container");
        containerObj.transform.SetParent(bandageCanvasObj.transform, false);
        var rectContainer = containerObj.AddComponent<RectTransform>();
        rectContainer.anchorMin = Vector2.zero;
        rectContainer.anchorMax = Vector2.one;
        rectContainer.sizeDelta = Vector2.zero;

        // Clear list data
        bandageWraps.Clear();
        startPositions.Clear();
        targetPositions.Clear();

        // Configure 7 medium-large messy horizontal-ish wraps spanning edge-to-edge (X = 0f, Width = 3400f)
        var configs = new System.Collections.Generic.List<BandageConfig>
        {
            new BandageConfig { name = "A1", position = new Vector2(0f, 380f), size = new Vector2(3400f, 180f), rotation = 8f, slideDirection = -1f },
            new BandageConfig { name = "A2", position = new Vector2(0f, -380f), size = new Vector2(3400f, 180f), rotation = -6f, slideDirection = 1f },
            new BandageConfig { name = "A3", position = new Vector2(0f, 200f), size = new Vector2(3400f, 150f), rotation = -9f, slideDirection = -1f },
            new BandageConfig { name = "A4", position = new Vector2(0f, -200f), size = new Vector2(3400f, 150f), rotation = 7f, slideDirection = 1f },
            new BandageConfig { name = "A5", position = new Vector2(0f, 0f), size = new Vector2(3400f, 140f), rotation = -3f, slideDirection = -1f },

            // Diagonals crossing over to create abstract scattered peepholes
            new BandageConfig { name = "A6", position = new Vector2(0f, 90f), size = new Vector2(3400f, 130f), rotation = -15f, slideDirection = 1f },
            new BandageConfig { name = "A7", position = new Vector2(0f, -90f), size = new Vector2(3400f, 130f), rotation = 16f, slideDirection = -1f }
        };

        foreach (var cfg in configs)
        {
            // Calculate direction vector along the rotated axis
            float rad = cfg.rotation * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            Vector2 offset = dir * (3600f * cfg.slideDirection); // Larger offset to hide 3400px wide strips completely
            Vector2 startPos = cfg.position + offset;

            RectTransform rect = CreateBandageStrip(cfg.name, containerObj.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), startPos, cfg.size, cfg.rotation);
            
            bandageWraps.Add(rect);
            startPositions.Add(startPos);
            targetPositions.Add(cfg.position);
        }

        // Hide canvas initially
        bandageCanvasObj.SetActive(false);
    }

    private RectTransform CreateBandageStrip(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 startPos, Vector2 size, float rotation)
    {
        // 1. Root GameObject
        GameObject stripObj = new GameObject(name);
        stripObj.transform.SetParent(parent, false);
        RectTransform rect = stripObj.AddComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = startPos;
        rect.sizeDelta = size;
        rect.localRotation = Quaternion.Euler(0f, 0f, rotation);

        // Layer 1: Dark sandy shadow/outline
        var bgImage = stripObj.AddComponent<UnityEngine.UI.Image>();
        bgImage.color = new Color(0.42f, 0.35f, 0.28f, 0.95f);

        // Layer 2: Main bandage wrap (Beige)
        GameObject mainObj = new GameObject("MainWrap");
        mainObj.transform.SetParent(stripObj.transform, false);
        var rectMain = mainObj.AddComponent<RectTransform>();
        rectMain.anchorMin = Vector2.zero;
        rectMain.anchorMax = Vector2.one;
        rectMain.sizeDelta = new Vector2(0f, -12f); // margin top/bottom
        var mainImage = mainObj.AddComponent<UnityEngine.UI.Image>();
        mainImage.color = new Color(0.84f, 0.77f, 0.68f, 1f);

        // Layer 3: Highlight fold (Lighter cream)
        GameObject highlightObj = new GameObject("HighlightFold");
        highlightObj.transform.SetParent(mainObj.transform, false);
        var rectHighlight = highlightObj.AddComponent<RectTransform>();
        rectHighlight.anchorMin = new Vector2(0f, 0.15f);
        rectHighlight.anchorMax = new Vector2(1f, 0.35f);
        rectHighlight.sizeDelta = Vector2.zero;
        var highlightImage = highlightObj.AddComponent<UnityEngine.UI.Image>();
        highlightImage.color = new Color(0.92f, 0.87f, 0.81f, 1f);

        // Layer 4: Overlapping secondary strip for textured look (Darker beige)
        GameObject overlapObj = new GameObject("OverlapStrip");
        overlapObj.transform.SetParent(mainObj.transform, false);
        var rectOverlap = overlapObj.AddComponent<RectTransform>();
        rectOverlap.anchorMin = new Vector2(0f, 0.5f);
        rectOverlap.anchorMax = new Vector2(1f, 0.95f);
        rectOverlap.sizeDelta = Vector2.zero;
        rectOverlap.localRotation = Quaternion.Euler(0f, 0f, -0.8f);
        var overlapImage = overlapObj.AddComponent<UnityEngine.UI.Image>();
        overlapImage.color = new Color(0.80f, 0.73f, 0.64f, 1f);

        return rect;
    }

    private System.Collections.IEnumerator AnimateBandages(bool targetActive)
    {
        float duration = targetActive ? 2.5f : 0.85f; // Slower, more dramatic crawl on entry, default on exit
        float elapsed = 0f;

        var currentStartPos = new System.Collections.Generic.List<Vector2>();
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
            float t = elapsed / duration;
            float easeT = Mathf.SmoothStep(0f, 1f, t);

            for (int i = 0; i < bandageWraps.Count; i++)
            {
                Vector2 target = targetActive ? targetPositions[i] : startPositions[i];
                Vector2 basePos = Vector2.Lerp(currentStartPos[i], target, easeT);

                // Add trembling shake when wrapping onto player's face
                if (targetActive)
                {
                    // Trembling shake fades out as easeT goes to 1.0 (snug wrap)
                    float shakeStrength = (1f - easeT) * 15f;
                    float freq = 60f;
                    float shakeX = Mathf.Sin(elapsed * freq + i * 7f) * shakeStrength + UnityEngine.Random.Range(-shakeStrength * 0.3f, shakeStrength * 0.3f);
                    float shakeY = Mathf.Cos(elapsed * freq * 0.9f + i * 3f) * shakeStrength + UnityEngine.Random.Range(-shakeStrength * 0.3f, shakeStrength * 0.3f);
                    
                    basePos += new Vector2(shakeX, shakeY);
                }

                bandageWraps[i].anchoredPosition = basePos;
            }

            yield return null;
        }

        for (int i = 0; i < bandageWraps.Count; i++)
        {
            bandageWraps[i].anchoredPosition = targetActive ? targetPositions[i] : startPositions[i];
        }

        if (!targetActive && bandageCanvasObj != null)
        {
            bandageCanvasObj.SetActive(false);
        }
    }
}
