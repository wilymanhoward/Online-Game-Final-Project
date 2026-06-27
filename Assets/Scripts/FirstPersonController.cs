using UnityEngine;
using Photon.Pun;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PhotonView))]
public class FirstPersonController : MonoBehaviourPun
{
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

    private Transform leftLegJoint;
    private Transform rightLegJoint;
    private Transform rightArmJoint;
    private Transform leftArmJoint;
    private Transform leftElbowJoint;  // Elbow_L (actual bone name)
    private Transform leftHandJoint;   // Hand_L
    private Transform leftKneeJoint;   // LowerLeg_L
    private Transform rightKneeJoint;  // LowerLeg_R

    private Quaternion defaultLeftLegRot;
    private Quaternion defaultRightLegRot;
    private Quaternion defaultRightArmRot;
    private Quaternion defaultLeftArmRot;
    private Quaternion defaultHipsRot;
    private Quaternion defaultLeftElbowRot;
    private Quaternion defaultLeftHandRot;
    private Quaternion defaultLeftKneeRot;
    private Quaternion defaultRightKneeRot;

    // Vault wall IK
    private Vector3 vaultWallContactPoint;
    private float vaultExitBlend = 0f;
    private float vaultExitDuration = 0.25f;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();

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
        leftKneeJoint = FindDeepChild(transform, "LowerLeg_L");
        rightKneeJoint = FindDeepChild(transform, "LowerLeg_R");

        if (leftLegJoint) defaultLeftLegRot = leftLegJoint.localRotation;
        if (rightLegJoint) defaultRightLegRot = rightLegJoint.localRotation;
        if (rightArmJoint) defaultRightArmRot = rightArmJoint.localRotation;
        if (leftArmJoint) defaultLeftArmRot = leftArmJoint.localRotation;
        if (leftElbowJoint) defaultLeftElbowRot = leftElbowJoint.localRotation;
        if (leftHandJoint) defaultLeftHandRot = leftHandJoint.localRotation;
        if (leftKneeJoint) defaultLeftKneeRot = leftKneeJoint.localRotation;
        if (rightKneeJoint) defaultRightKneeRot = rightKneeJoint.localRotation;

        // If this is a remote player, we don't control it
        if (PhotonNetwork.IsConnected && !photonView.IsMine)
        {
            // Disable CharacterController and FirstPersonController inputs
            if (controller != null) controller.enabled = false;
            
            // Also disable any camera or listeners attached
            var listener = GetComponentInChildren<AudioListener>();
            if (listener != null) listener.enabled = false;
            
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
    }

    void Update()
    {
        if (PhotonNetwork.IsConnected && !photonView.IsMine) return;

        // 1. Camera Look Rotation
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        // Rotate player body horizontally via mouse look
        transform.Rotate(Vector3.up * mouseX);

        // Rotate camera vertically (pitch)
        pitch -= mouseY;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        if (playerCamera != null)
        {
            if (headJoint != null)
            {
                // Position camera at head joint + offset
                playerCamera.transform.position = headJoint.position + transform.TransformDirection(cameraOffset);
            }
            else
            {
                // Fallback to player height
                playerCamera.transform.position = transform.position + new Vector3(0f, 1.6f, 0.15f);
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
                animator.SetBool("IsGrounded", true);
                animator.SetBool("OnGround", true);
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
            animator.SetBool("IsGrounded", controller.isGrounded);
            animator.SetBool("OnGround", controller.isGrounded);
        }
    }

    void LateUpdate()
    {
        if (animator == null) return;

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
            float roll = Mathf.Sin(smoothT * Mathf.PI) * -75f; // Roll body left (left shoulder down, right side up)
            float yaw = Mathf.Sin(smoothT * Mathf.PI) * 75f;   // Yaw body right (chest rotates right)
            float pitch = Mathf.Sin(smoothT * Mathf.PI) * 15f; // Pitch body forward slightly

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

            // Right Leg (Leading leg, extended straight and high)
            float rightLegWeight = Mathf.Sin(Mathf.Clamp01(smoothT / 0.7f) * Mathf.PI); // Peaks at 35%, down by 70%
            float rightLegPitch = rightLegWeight * -75f;
            float rightLegYaw = rightLegWeight * 15f;
            if (rightLegJoint != null) 
            {
                Quaternion targetRot = defaultRightLegRot * Quaternion.Euler(rightLegPitch, 0f, rightLegYaw);
                rightLegJoint.localRotation = Quaternion.Slerp(rightLegJoint.localRotation, targetRot, weight);
            }
            if (rightKneeJoint != null)
            {
                // Keep leading leg knee straight
                rightKneeJoint.localRotation = Quaternion.Slerp(rightKneeJoint.localRotation, defaultRightKneeRot, weight);
            }
 
            // Left Leg (Trailing leg, bent and tucked under the body)
            float leftLegWeight = 0f;
            if (smoothT > 0.3f)
            {
                leftLegWeight = Mathf.Sin(Mathf.Clamp01((smoothT - 0.3f) / 0.7f) * Mathf.PI); // Peaks at 65%, down by 100%
            }
            float leftLegPitch = leftLegWeight * -55f;
            float leftLegYaw = leftLegWeight * -20f;
            if (leftLegJoint != null) 
            {
                Quaternion targetRot = defaultLeftLegRot * Quaternion.Euler(leftLegPitch, 0f, leftLegYaw);
                leftLegJoint.localRotation = Quaternion.Slerp(leftLegJoint.localRotation, targetRot, weight);
            }
            if (leftKneeJoint != null)
            {
                // Bend trailing leg knee to tuck it under
                Quaternion targetRot = defaultLeftKneeRot * Quaternion.Euler(leftLegWeight * 95f, 0f, 0f);
                leftKneeJoint.localRotation = Quaternion.Slerp(leftKneeJoint.localRotation, targetRot, weight);
            }

            // Left Arm plants down on the wall to support the body
            float leftArmRoll = Mathf.Sin(smoothT * Mathf.PI) * 60f;
            if (leftArmJoint != null) 
            {
                Quaternion targetRot = defaultLeftArmRot * Quaternion.Euler(0f, 0f, leftArmRoll);
                leftArmJoint.localRotation = Quaternion.Slerp(leftArmJoint.localRotation, targetRot, weight);
            }

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

            // Right Arm raises up in the air for balance
            float rightArmRoll = Mathf.Sin(t * Mathf.PI) * 75f;
            if (rightArmJoint != null) 
            {
                Quaternion targetRot = defaultRightArmRot * Quaternion.Euler(0f, 0f, rightArmRoll);
                rightArmJoint.localRotation = Quaternion.Slerp(rightArmJoint.localRotation, targetRot, weight);
            }

            if (isVaulting && t >= 1f)
            {
                isVaulting = false;
                if (controller != null) controller.enabled = true;
                if (animator != null) animator.enabled = true;
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
        if (animator != null)
        {
            animator.enabled = false;
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
                if (shinHit.collider.CompareTag("Player") || shinHit.collider.GetComponentInParent<PhotonView>() != null)
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
}
