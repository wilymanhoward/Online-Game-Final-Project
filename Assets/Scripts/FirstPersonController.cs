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
    public Vector3 cameraOffset = new Vector3(0f, 0.12f, 0.25f); // Slightly lower and further forward to prevent clipping through the mummy's head/wrapping mesh

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

    [Header("Throw Settings (Managed by PlayerThrow)")]
    [HideInInspector] public bool isAiming = false;

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
    private Quaternion defaultRightHandRot;

    // Finger Joints & Default Rotations
    private Transform finger01L, finger02L, finger03L;
    private Transform index01L, index02L, index03L;
    private Transform thumb01L, thumb02L, thumb03L;
    private Quaternion defaultFinger01L, defaultFinger02L, defaultFinger03L;
    private Quaternion defaultIndex01L, defaultIndex02L, defaultIndex03L;
    private Quaternion defaultThumb01L, defaultThumb02L, defaultThumb03L;

    [Header("Throw Animation")]
    public float throwAnimDuration = 2.65f;
    [HideInInspector] public float throwAnimTimer = 0f;
    [HideInInspector] public bool isThrowingAnim = false;
    [HideInInspector] public float throwExitBlend = 0f;
    public float throwExitDuration = 0.2f;
    public float originalNearClip = 0.25f;
 
    [Header("Camera Aim Settings")]
    public float cameraAimBlendSpeed = 8f;
    private float cameraAimBlend = 0f;

    [Header("Checkpoint System")]
    public float deathYThreshold = -15f;
    private Vector3 activeCheckpointPosition;

    [Header("Fall Damage Settings")]
    [Tooltip("Maximum air time in seconds before fall becomes fatal upon landing.")]
    public float fatalAirTimeThreshold = 3.0f;
    private float airTimeCounter = 0f;

    [Header("Moving Platform Settings")]
    [SerializeField] private LayerMask platformLayer;
    private Transform activePlatform;
    private Vector3 localPlayerPos;

    [Header("Input Settings")]
    [SerializeField] private InputReader inputReader;
    public InputReader InputReader => inputReader;

    // Camera shake fields
    private Vector3 cameraShakeOffset = Vector3.zero;

    // Torch Settings
    private bool isHoldingTorch = false;
    public bool IsHoldingTorch => isHoldingTorch;
    private float torchHoldWeight = 0f;
    private GameObject leftHandTorchObj;

    [Header("Torch Hold Pose Offset")]
    public Vector3 torchHoldShoulderEuler = new Vector3(105f, 0f, -20f);
    public float torchHoldElbowX = -45f;
    public Vector3 torchHoldHandEuler = new Vector3(15f, 0f, 0f);

    [Header("Torch Place Pose Offset")]
    public Vector3 torchPlaceShoulderEuler = new Vector3(75f, 30f, -5f);
    public float torchPlaceElbowX = -40f;
    public Vector3 torchPlaceHandEuler = new Vector3(0f, 0f, 50f);

    // Torch placing animation state
    private bool isPlacingTorch = false;
    public bool IsPlacingTorch => isPlacingTorch;
    private float torchPlaceTimer = 0f;
    private System.Action onTorchPlacedCallback = null;

    // Death Spam Settings
    [Header("Death Spam Settings")]
    public int requiredClicksForRespawn = 5;
    private bool isDead = false;
    public bool IsDead => isDead;
    private int clickCountToRespawn = 0;
    private bool isLocalPlayer = true;
    public bool IsLocalPlayer
    {
        get
        {
            if (PhotonNetwork.IsConnected)
            {
                if (gameObject.name == "Player1")
                {
                    return PhotonNetwork.IsMasterClient;
                }
                else if (gameObject.name == "Player2")
                {
                    return !PhotonNetwork.IsMasterClient;
                }
                else
                {
                    return photonView != null && photonView.IsMine;
                }
            }
            else
            {
                // Offline fallback: only local if this instance has an active camera
                var cam = GetComponentInChildren<Camera>(true);
                if (cam != null)
                {
                    return cam.enabled && cam.gameObject.activeInHierarchy;
                }
                return isLocalPlayer;
            }
        }
    }
    public static FirstPersonController InteractingPlayer { get; set; }
    public bool isParalyzed = false;
    private GameObject deathOverlayObj;
    private UnityEngine.UI.Text deathClicksText;
    private UnityEngine.UI.Image deathProgressBarFill;
 
    // Bandage Overlay Settings
    // (Bandage/blindness fields moved to PlayerDisability)

    // Vault wall IK
    private Vector3 vaultWallContactPoint;
    private float vaultExitBlend = 0f;
    private float vaultExitDuration = 0.25f;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();

        // Automatically ensure PlayerCheckpointHandler is attached
        if (GetComponent<PlayerCheckpointHandler>() == null)
        {
            gameObject.AddComponent<PlayerCheckpointHandler>();
        }

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
        if (rightHandJoint) defaultRightHandRot = rightHandJoint.localRotation;

        // Cache left hand finger bones
        finger01L = FindDeepChild(transform, "Finger_01_L");
        finger02L = FindDeepChild(transform, "Finger_02_L");
        finger03L = FindDeepChild(transform, "Finger_03_L");
        index01L = FindDeepChild(transform, "IndexFinger_01_L");
        index02L = FindDeepChild(transform, "IndexFinger_02_L");
        index03L = FindDeepChild(transform, "IndexFinger_03_L");
        thumb01L = FindDeepChild(transform, "Thumb_01_L");
        thumb02L = FindDeepChild(transform, "Thumb_02_L");
        thumb03L = FindDeepChild(transform, "Thumb_03_L");

        if (finger01L) defaultFinger01L = finger01L.localRotation;
        if (finger02L) defaultFinger02L = finger02L.localRotation;
        if (finger03L) defaultFinger03L = finger03L.localRotation;
        if (index01L) defaultIndex01L = index01L.localRotation;
        if (index02L) defaultIndex02L = index02L.localRotation;
        if (index03L) defaultIndex03L = index03L.localRotation;
        if (thumb01L) defaultThumb01L = thumb01L.localRotation;
        if (thumb02L) defaultThumb02L = thumb02L.localRotation;
        if (thumb03L) defaultThumb03L = thumb03L.localRotation;

        // Determine if this specific player instance is local based on name and Photon role
        isLocalPlayer = true;
        if (PhotonNetwork.IsConnected)
        {
            if (gameObject.name == "Player1")
            {
                isLocalPlayer = PhotonNetwork.IsMasterClient;
            }
            else if (gameObject.name == "Player2")
            {
                isLocalPlayer = !PhotonNetwork.IsMasterClient;
            }
            else
            {
                isLocalPlayer = photonView.IsMine;
            }
        }

        // If this is a remote player, we don't control it
        if (!IsLocalPlayer)
        {
            // Disable CharacterController and FirstPersonController inputs
            if (controller != null) controller.enabled = false;
            
            // Also disable any camera or listeners attached
            var listener = GetComponentInChildren<AudioListener>();
            if (listener != null) listener.enabled = false;

            // Make sure the LineRenderer on this remote copy is disabled/destroyed so other players never see it
            var lr = GetComponent<LineRenderer>();
            if (lr != null) Destroy(lr);
            
            // Disable camera and listener on remote copy
            var cam = GetComponentInChildren<Camera>(true);
            if (cam != null) cam.gameObject.SetActive(false);
            
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



        if (leftHandJoint != null)
        {
            Transform torchTrans = leftHandJoint.Find("SM_Prop_Torch_05");
            if (torchTrans != null)
            {
                leftHandTorchObj = torchTrans.gameObject;
                leftHandTorchObj.SetActive(isHoldingTorch);
            }
        }

        if (!PhotonNetwork.IsConnected || IsLocalPlayer)
        {
            CreateDeathUI();
        }

#if UNITY_EDITOR
        if (inputReader == null)
        {
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:InputReader");
            if (guids != null && guids.Length > 0)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                inputReader = UnityEditor.AssetDatabase.LoadAssetAtPath<InputReader>(path);
            }
        }
#endif
        if (inputReader == null)
        {
            InputReader[] readers = Resources.FindObjectsOfTypeAll<InputReader>();
            if (readers != null && readers.Length > 0)
            {
                inputReader = readers[0];
            }
        }

        // Initialize moving platform layer if not set
        if (platformLayer == 0)
        {
            platformLayer = LayerMask.GetMask("Platform");
        }
    }

    void Update()
    {
        if (PhotonNetwork.IsConnected && !IsLocalPlayer) return;

        // Apply moving platform delta if standing on one
        if (activePlatform != null)
        {
            Vector3 newWorldPos = activePlatform.TransformPoint(localPlayerPos);
            Vector3 platformDelta = newWorldPos - transform.position;

            // Only follow horizontal movement if in mid-air (prevent snapping/jumping snags)
            if (!controller.isGrounded)
            {
                platformDelta.y = 0f;
            }

            if (platformDelta.sqrMagnitude > 0.0001f)
            {
                controller.Move(platformDelta);
            }
        }

        bool disableMovement = isParalyzed || (inputReader != null && (inputReader.AreInputsDisabled || inputReader.AreInputsDisabledExceptLook || inputReader.AreInputsDisabledExceptInteract));

        // [TEST] B key toggles blind overlay via PlayerDisability
        if (!disableMovement && Input.GetKeyDown(KeyCode.B) && !isDead)
        {
            PlayerDisability pd = GetComponent<PlayerDisability>();
            if (pd != null) pd.SetBlind(!pd.IsBlindActive);
        }

        if (isDead)
        {
            airTimeCounter = 0f;
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
        bool disableLook = isParalyzed || (inputReader != null && (inputReader.AreInputsDisabled || inputReader.AreInputsDisabledExceptInteract));
        float mouseX = 0f;
        float mouseY = 0f;
        if (!disableLook)
        {
            mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
            mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;
        }

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
            airTimeCounter = 0f;
            return;
        }

        // 2. Player Movement
        float moveHorizontal = 0f;
        float moveVertical = 0f;
        bool isRunning = false;

        if (!disableMovement)
        {
            moveHorizontal = Input.GetAxisRaw("Horizontal"); // Changed from GetAxis to GetAxisRaw for instant stopping response
            moveVertical = Input.GetAxisRaw("Vertical");     // Changed from GetAxis to GetAxisRaw for instant stopping response
            // Determine if running (holding Shift and moving forward)
            isRunning = (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) && (Input.GetKey(KeyCode.W) || moveVertical > 0.1f);
        }

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

            if (!disableMovement && Input.GetButtonDown("Jump"))
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

        bool wasGrounded = controller.isGrounded;

        // Move character controller
        controller.Move(move * Time.deltaTime);

        // Update Moving Platform detection
        RaycastHit platformHit;
        Vector3 platformRayStart = transform.position + Vector3.up * 0.1f;
        if (Physics.Raycast(platformRayStart, Vector3.down, out platformHit, 0.3f, platformLayer))
        {
            activePlatform = platformHit.transform;
            localPlayerPos = activePlatform.InverseTransformPoint(transform.position);
        }
        else
        {
            activePlatform = null;
        }

        // Update air time counter and check for fatal landings
        if (controller.isGrounded)
        {
            if (!wasGrounded)
            {
                // Player has just landed
                if (airTimeCounter >= fatalAirTimeThreshold)
                {
                    // Debug.Log($"[FallDamage] Player landed after {airTimeCounter:F2} seconds of air time. Fatal threshold was {fatalAirTimeThreshold}s. Respawning.");
                    Respawn();
                }
                else
                {
                    // Debug.Log($"[FallDamage] Player landed safely after {airTimeCounter:F2} seconds of air time.");
                }
                airTimeCounter = 0f;
            }
            else
            {
                airTimeCounter = 0f;
            }
        }
        else
        {
            // Player is in the air (jumping, falling, etc.)
            airTimeCounter += Time.deltaTime;
        }

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

        // Handle torch placing animation updates
        if (isPlacingTorch)
        {
            torchPlaceTimer -= Time.deltaTime;
            if (torchPlaceTimer <= 0.3f && onTorchPlacedCallback != null)
            {
                onTorchPlacedCallback.Invoke();
                onTorchPlacedCallback = null;
                SetHoldingTorch(false);
            }
            if (torchPlaceTimer <= 0f)
            {
                isPlacingTorch = false;
            }
        }

        // Smoothly blend the torch holding pose on the left arm joints in LateUpdate instead of Update
        // to prevent the Unity Animator from overwriting the custom joint rotations.
        if (isHoldingTorch)
        {
            torchHoldWeight = Mathf.MoveTowards(torchHoldWeight, 1f, Time.deltaTime * 5f);
        }
        else
        {
            torchHoldWeight = Mathf.MoveTowards(torchHoldWeight, 0f, Time.deltaTime * 5f);
        }

        if (torchHoldWeight > 0.01f)
        {
            float placeBlend = 0f;
            if (isPlacingTorch && torchPlaceTimer > 0.3f)
            {
                placeBlend = Mathf.Clamp01((0.6f - torchPlaceTimer) / 0.3f);
            }

            float armX = Mathf.Lerp(torchHoldShoulderEuler.x, torchPlaceShoulderEuler.x, placeBlend);
            float armY = Mathf.Lerp(torchHoldShoulderEuler.y, torchPlaceShoulderEuler.y, placeBlend);
            float armZ = Mathf.Lerp(torchHoldShoulderEuler.z, torchPlaceShoulderEuler.z, placeBlend);

            // Shift arm raise/lower to follow the camera's vertical look angle (pitch)
            armX -= pitch;

            float elbowX = Mathf.Lerp(torchHoldElbowX, torchPlaceElbowX, placeBlend);
            float handX = Mathf.Lerp(torchHoldHandEuler.x, torchPlaceHandEuler.x, placeBlend);
            float handY = Mathf.Lerp(torchHoldHandEuler.y, torchPlaceHandEuler.y, placeBlend);
            float handZ = Mathf.Lerp(torchHoldHandEuler.z, torchPlaceHandEuler.z, placeBlend);

            if (leftArmJoint != null)
            {
                // Bends left upper arm up-forward and slightly outward (more to the side)
                // Also tilts up/down (using parent chest-space pitch) to follow camera movement
                Quaternion targetArmRot = defaultLeftArmRot * Quaternion.Euler(armX, armY, armZ);
                leftArmJoint.localRotation = Quaternion.Slerp(leftArmJoint.localRotation, targetArmRot, torchHoldWeight);
            }
            if (leftElbowJoint != null)
            {
                // Bends elbow forward
                Quaternion targetElbowRot = defaultLeftElbowRot * Quaternion.Euler(elbowX, 0f, 0f);
                leftElbowJoint.localRotation = Quaternion.Slerp(leftElbowJoint.localRotation, targetElbowRot, torchHoldWeight);
            }
            if (leftHandJoint != null)
            {
                // Holds torch upright and tilted forward/right (towards the center)
                Quaternion targetHandRot = defaultLeftHandRot * Quaternion.Euler(handX, handY, handZ);
                leftHandJoint.localRotation = Quaternion.Slerp(leftHandJoint.localRotation, targetHandRot, torchHoldWeight);
            }

            // Grip fingers around torch handle
            if (finger01L != null) finger01L.localRotation = Quaternion.Slerp(finger01L.localRotation, defaultFinger01L * Quaternion.Euler(0f, 40f, 60f), torchHoldWeight);
            if (finger02L != null) finger02L.localRotation = Quaternion.Slerp(finger02L.localRotation, defaultFinger02L * Quaternion.Euler(0f, 40f, 60f), torchHoldWeight);
            if (finger03L != null) finger03L.localRotation = Quaternion.Slerp(finger03L.localRotation, defaultFinger03L * Quaternion.Euler(0f, 40f, 60f), torchHoldWeight);

            if (index01L != null) index01L.localRotation = Quaternion.Slerp(index01L.localRotation, defaultIndex01L * Quaternion.Euler(0f, -40f, 40f), torchHoldWeight);
            if (index02L != null) index02L.localRotation = Quaternion.Slerp(index02L.localRotation, defaultIndex02L * Quaternion.Euler(0f, -40f, 40f), torchHoldWeight);
            if (index03L != null) index03L.localRotation = Quaternion.Slerp(index03L.localRotation, defaultIndex03L * Quaternion.Euler(0f, -40f, 40f), torchHoldWeight);

            if (thumb01L != null) thumb01L.localRotation = Quaternion.Slerp(thumb01L.localRotation, defaultThumb01L * Quaternion.Euler(0f, -40f, 30f), torchHoldWeight);
            if (thumb02L != null) thumb02L.localRotation = Quaternion.Slerp(thumb02L.localRotation, defaultThumb02L * Quaternion.Euler(0f, -40f, 30f), torchHoldWeight);
            if (thumb03L != null) thumb03L.localRotation = Quaternion.Slerp(thumb03L.localRotation, defaultThumb03L * Quaternion.Euler(0f, -40f, 30f), torchHoldWeight);
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
        if (rightHandJoint) rightHandJoint.localRotation = defaultRightHandRot;

        // Reset finger joints
        if (finger01L) finger01L.localRotation = defaultFinger01L;
        if (finger02L) finger02L.localRotation = defaultFinger02L;
        if (finger03L) finger03L.localRotation = defaultFinger03L;
        if (index01L) index01L.localRotation = defaultIndex01L;
        if (index02L) index02L.localRotation = defaultIndex02L;
        if (index03L) index03L.localRotation = defaultIndex03L;
        if (thumb01L) thumb01L.localRotation = defaultThumb01L;
        if (thumb02L) thumb02L.localRotation = defaultThumb02L;
        if (thumb03L) thumb03L.localRotation = defaultThumb03L;
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



    public void SetCheckpoint(Vector3 position)
    {
        activeCheckpointPosition = position;
        Debug.Log("Checkpoint saved at: " + activeCheckpointPosition);
    }

    public void ResetAirTime()
    {
        // Only run for the local player client
        if (PhotonNetwork.IsConnected && !IsLocalPlayer) return;

        airTimeCounter = 0f;
        Debug.Log($"[FallDamage] Air time manually reset for {name}.");
    }

    public void Respawn()
    {
        // Only respawn the local player client
        if (PhotonNetwork.IsConnected && !IsLocalPlayer) return;

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
        airTimeCounter = 0f;

        if (controller != null)
        {
                    controller.enabled = true;
        }

        isDead = false;

        SetHoldingTorch(false);

        OnLocalPlayerRespawn?.Invoke(activeCheckpointPosition);
    }

    public void TriggerPlaceTorchAnimation(System.Action onPlaced)
    {
        if (isHoldingTorch && !isPlacingTorch)
        {
            isPlacingTorch = true;
            torchPlaceTimer = 0.6f;
            onTorchPlacedCallback = onPlaced;
        }
    }

    public void SetHoldingTorch(bool holding)
    {
        if (PhotonNetwork.IsConnected)
        {
            photonView.RPC("SetHoldingTorchRPC", RpcTarget.AllBuffered, holding);
        }
        else
        {
            SetHoldingTorchLocal(holding);
        }
    }

    [PunRPC]
    private void SetHoldingTorchRPC(bool holding)
    {
        SetHoldingTorchLocal(holding);
    }

    private void SetHoldingTorchLocal(bool holding)
    {
        isHoldingTorch = holding;

        if (leftHandTorchObj == null && leftHandJoint != null)
        {
            Transform torchTrans = leftHandJoint.Find("SM_Prop_Torch_05");
            if (torchTrans != null)
            {
                leftHandTorchObj = torchTrans.gameObject;
            }
        }

        if (leftHandTorchObj != null)
        {
            leftHandTorchObj.SetActive(holding);
        }
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
        if (PhotonNetwork.IsConnected && !IsLocalPlayer) return;

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

    public void RouteLeverInteract(InteractLever lever)
    {
        if (photonView != null && photonView.IsMine && PhotonNetwork.IsConnected && PhotonNetwork.InRoom)
        {
            photonView.RPC("SyncLeverInteractRPC", RpcTarget.All, GetGameObjectPath(lever.gameObject));
        }
    }

    [PunRPC]
    private void SyncLeverInteractRPC(string path)
    {
        GameObject go = GameObject.Find(path);
        if (go != null)
        {
            InteractLever lever = go.GetComponent<InteractLever>();
            if (lever != null)
            {
                PlayerInteract pi = GetComponent<PlayerInteract>();
                lever.InteractLocal(pi);
            }
        }
    }

    public void RouteBothLevers(InteractLever lever, bool active)
    {
        if (photonView != null && photonView.IsMine && PhotonNetwork.IsConnected && PhotonNetwork.InRoom)
        {
            photonView.RPC("SyncBothLeversRPC", RpcTarget.All, GetGameObjectPath(lever.gameObject), active);
        }
    }

    public void RouteResetLevers(InteractLever lever)
    {
        if (photonView != null && photonView.IsMine && PhotonNetwork.IsConnected && PhotonNetwork.InRoom)
        {
            photonView.RPC("SyncResetLeversRPC", RpcTarget.All, GetGameObjectPath(lever.gameObject));
        }
    }

    [PunRPC]
    private void SyncResetLeversRPC(string leverPath)
    {
        GameObject go = GameObject.Find(leverPath);
        if (go != null)
        {
            InteractLever lever = go.GetComponent<InteractLever>();
            if (lever != null)
            {
                lever.ResetBothLeversLocal();
            }
        }
    }

    public void RouteRedLightShoot(string shooterPath, Vector3 spawnPos, Vector3 direction)
    {
        if (photonView != null && photonView.IsMine && PhotonNetwork.IsConnected && PhotonNetwork.InRoom)
        {
            photonView.RPC("SpawnRedLightProjectileRPC", RpcTarget.All, shooterPath, spawnPos, direction);
        }
    }

    [PunRPC]
    private void SpawnRedLightProjectileRPC(string shooterPath, Vector3 spawnPos, Vector3 direction)
    {
        GameObject go = GameObject.Find(shooterPath);
        if (go != null)
        {
            RedLightShooter shooter = go.GetComponent<RedLightShooter>();
            if (shooter != null)
            {
                shooter.SpawnProjectileLocal(spawnPos, direction);
            }
        }
        else
        {
            RedLightShooter shooter = FindObjectOfType<RedLightShooter>();
            if (shooter != null)
            {
                shooter.SpawnProjectileLocal(spawnPos, direction);
            }
        }
    }

    [PunRPC]
    private void SyncBothLeversRPC(string leverPath, bool active)
    {
        GameObject go = GameObject.Find(leverPath);
        if (go != null)
        {
            InteractLever lever = go.GetComponent<InteractLever>();
            if (lever != null)
            {
                if (active)
                {
                    lever.ActivateBothLeversLocal();
                }
                else
                {
                    lever.DeactivateBothLeversLocal();
                }
            }
        }
    }

    public void RouteTriggerCross(InteractWhenCrossed trigger, GameObject player, bool enter)
    {
        if (photonView != null && photonView.IsMine && PhotonNetwork.IsConnected && PhotonNetwork.InRoom)
        {
            photonView.RPC("SyncTriggerCrossRPC", RpcTarget.All, GetGameObjectPath(trigger.gameObject), GetGameObjectPath(player), enter);
        }
    }

    [PunRPC]
    private void SyncTriggerCrossRPC(string triggerPath, string playerPath, bool enter)
    {
        GameObject goTrigger = GameObject.Find(triggerPath);
        GameObject goPlayer = GameObject.Find(playerPath);
        if (goTrigger != null && goPlayer != null)
        {
            InteractWhenCrossed trigger = goTrigger.GetComponent<InteractWhenCrossed>();
            if (trigger != null)
            {
                if (enter)
                {
                    trigger.OnEnterZoneTriggerLocal(goPlayer);
                }
                else
                {
                    trigger.OnExitZoneTriggerLocal(goPlayer);
                }
            }
        }
    }

    // --- Puppet Game Routing ---
    public void RoutePuppetStartGame()
    {
        if (photonView != null && photonView.IsMine && PhotonNetwork.IsConnected && PhotonNetwork.InRoom)
        {
            photonView.RPC("SyncPuppetStartGameRPC", RpcTarget.All);
        }
    }

    [PunRPC]
    private void SyncPuppetStartGameRPC()
    {
        PuppetGame pg = FindObjectOfType<PuppetGame>();
        if (pg != null)
        {
            pg.StartGameLocal();
        }
    }

    public void RoutePuppetRestartGame()
    {
        if (photonView != null && photonView.IsMine && PhotonNetwork.IsConnected && PhotonNetwork.InRoom)
        {
            photonView.RPC("SyncPuppetRestartGameRPC", RpcTarget.All);
        }
    }

    [PunRPC]
    private void SyncPuppetRestartGameRPC()
    {
        PuppetGame pg = FindObjectOfType<PuppetGame>();
        if (pg != null)
        {
            pg.RestartGameLocal();
        }
    }

    public void RoutePuppetEndGame()
    {
        if (photonView != null && photonView.IsMine && PhotonNetwork.IsConnected && PhotonNetwork.InRoom)
        {
            photonView.RPC("SyncPuppetEndGameRPC", RpcTarget.All);
        }
    }

    [PunRPC]
    private void SyncPuppetEndGameRPC()
    {
        PuppetGame pg = FindObjectOfType<PuppetGame>();
        if (pg != null)
        {
            pg.EndGameLocal();
        }
    }

    public void RoutePuppetSyncRoundState(int[] poseIndices, int answerSymbolID)
    {
        if (photonView != null && photonView.IsMine && PhotonNetwork.IsConnected && PhotonNetwork.InRoom)
        {
            photonView.RPC("SyncPuppetRoundStateRPC", RpcTarget.All, poseIndices, answerSymbolID);
        }
    }

    [PunRPC]
    private void SyncPuppetRoundStateRPC(int[] poseIndices, int answerSymbolID)
    {
        PuppetGame pg = FindObjectOfType<PuppetGame>();
        if (pg != null)
        {
            pg.SyncRoundStateLocal(poseIndices, answerSymbolID);
        }
    }

    public void RoutePuppetGuessSymbol(int GuessSymbolID)
    {
        if (photonView != null && photonView.IsMine && PhotonNetwork.IsConnected && PhotonNetwork.InRoom)
        {
            photonView.RPC("SyncPuppetGuessSymbolRPC", RpcTarget.All, GuessSymbolID);
        }
    }

    [PunRPC]
    private void SyncPuppetGuessSymbolRPC(int GuessSymbolID)
    {
        PuppetGame pg = FindObjectOfType<PuppetGame>();
        if (pg != null)
        {
            pg.GuessSymbolLocal(GuessSymbolID);
        }
    }

    public void RoutePuppetTimeUp()
    {
        if (photonView != null && photonView.IsMine && PhotonNetwork.IsConnected && PhotonNetwork.InRoom)
        {
            photonView.RPC("SyncPuppetTimeUpRPC", RpcTarget.All);
        }
    }

    [PunRPC]
    private void SyncPuppetTimeUpRPC()
    {
        PuppetGame pg = FindObjectOfType<PuppetGame>();
        if (pg != null)
        {
            pg.TimeUpLocal();
        }
    }

    // --- Falling Floors Routing ---
    public void RouteFallingFloorsStartGame()
    {
        if (photonView != null && photonView.IsMine && PhotonNetwork.IsConnected && PhotonNetwork.InRoom)
        {
            photonView.RPC("SyncFallingFloorsStartGameRPC", RpcTarget.All);
        }
    }

    [PunRPC]
    private void SyncFallingFloorsStartGameRPC()
    {
        FallingFloors ff = FindObjectOfType<FallingFloors>();
        if (ff != null)
        {
            ff.StartGameLocal();
        }
    }

    public void RouteFallingFloorsRestartGame()
    {
        if (photonView != null && photonView.IsMine && PhotonNetwork.IsConnected && PhotonNetwork.InRoom)
        {
            photonView.RPC("SyncFallingFloorsRestartGameRPC", RpcTarget.All);
        }
    }

    [PunRPC]
    private void SyncFallingFloorsRestartGameRPC()
    {
        FallingFloors ff = FindObjectOfType<FallingFloors>();
        if (ff != null)
        {
            ff.RestartGameLocal();
        }
    }

    public void RouteFallingFloorsEndGame()
    {
        if (photonView != null && photonView.IsMine && PhotonNetwork.IsConnected && PhotonNetwork.InRoom)
        {
            photonView.RPC("SyncFallingFloorsEndGameRPC", RpcTarget.All);
        }
    }

    [PunRPC]
    private void SyncFallingFloorsEndGameRPC()
    {
        FallingFloors ff = FindObjectOfType<FallingFloors>();
        if (ff != null)
        {
            ff.EndGameLocal();
        }
    }

    public void RouteFallingFloorsSyncTransition(int round, int nextStateVal, int[] symbols)
    {
        if (photonView != null && photonView.IsMine && PhotonNetwork.IsConnected && PhotonNetwork.InRoom)
        {
            photonView.RPC("SyncFallingFloorsTransitionRPC", RpcTarget.All, round, nextStateVal, symbols);
        }
    }

    [PunRPC]
    private void SyncFallingFloorsTransitionRPC(int round, int nextStateVal, int[] symbols)
    {
        FallingFloors ff = FindObjectOfType<FallingFloors>();
        if (ff != null)
        {
            ff.SyncTransitionLocal(round, nextStateVal, symbols);
        }
    }

    // --- Red Light Green Light Routing ---
    public void RouteRedLightStartGame()
    {
        if (photonView != null && photonView.IsMine && PhotonNetwork.IsConnected && PhotonNetwork.InRoom)
        {
            photonView.RPC("SyncRedLightStartGameRPC", RpcTarget.All);
        }
    }

    [PunRPC]
    private void SyncRedLightStartGameRPC()
    {
        RedLightGreenLight rl = FindObjectOfType<RedLightGreenLight>();
        if (rl != null)
        {
            rl.StartGameLocal();
        }
    }

    public void RouteRedLightRestartGame()
    {
        if (photonView != null && photonView.IsMine && PhotonNetwork.IsConnected && PhotonNetwork.InRoom)
        {
            photonView.RPC("SyncRedLightRestartGameRPC", RpcTarget.All);
        }
    }

    [PunRPC]
    private void SyncRedLightRestartGameRPC()
    {
        RedLightGreenLight rl = FindObjectOfType<RedLightGreenLight>();
        if (rl != null)
        {
            rl.RestartGameLocal();
        }
    }

    public void RouteRedLightEndGame()
    {
        if (photonView != null && photonView.IsMine && PhotonNetwork.IsConnected && PhotonNetwork.InRoom)
        {
            photonView.RPC("SyncRedLightEndGameRPC", RpcTarget.All);
        }
    }

    [PunRPC]
    private void SyncRedLightEndGameRPC()
    {
        RedLightGreenLight rl = FindObjectOfType<RedLightGreenLight>();
        if (rl != null)
        {
            rl.EndGameLocal();
        }
    }

    public void RouteRedLightWinGame()
    {
        if (photonView != null && photonView.IsMine && PhotonNetwork.IsConnected && PhotonNetwork.InRoom)
        {
            photonView.RPC("SyncRedLightWinGameRPC", RpcTarget.All);
        }
    }

    [PunRPC]
    private void SyncRedLightWinGameRPC()
    {
        RedLightGreenLight rl = FindObjectOfType<RedLightGreenLight>();
        if (rl != null)
        {
            rl.WinGameLocal();
        }
    }

    public void RouteRedLightSyncState(int stateVal, float duration)
    {
        if (photonView != null && photonView.IsMine && PhotonNetwork.IsConnected && PhotonNetwork.InRoom)
        {
            photonView.RPC("SyncRedLightStateRPC", RpcTarget.All, stateVal, duration);
        }
    }

    [PunRPC]
    private void SyncRedLightStateRPC(int stateVal, float duration)
    {
        RedLightGreenLight rl = FindObjectOfType<RedLightGreenLight>();
        if (rl != null)
        {
            rl.SyncStateLocal((RedLightGreenLight.GameState)stateVal, duration);
        }
    }

    public void RouteRedLightPlayWarning()
    {
        if (photonView != null && photonView.IsMine && PhotonNetwork.IsConnected && PhotonNetwork.InRoom)
        {
            photonView.RPC("SyncRedLightPlayWarningRPC", RpcTarget.All);
        }
    }

    [PunRPC]
    private void SyncRedLightPlayWarningRPC()
    {
        RedLightGreenLight rl = FindObjectOfType<RedLightGreenLight>();
        if (rl != null)
        {
            rl.PlayWarningLocal();
        }
    }

    private string GetGameObjectPath(GameObject obj)
    {
        string path = obj.name;
        while (obj.transform.parent != null)
        {
            obj = obj.transform.parent.gameObject;
            path = obj.name + "/" + path;
        }
        return path;
    }
}
