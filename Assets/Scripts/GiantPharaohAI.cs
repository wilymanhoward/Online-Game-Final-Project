using UnityEngine;
using System.Collections;

public class GiantPharaohAI : MonoBehaviour
{
    public enum AIState { Chasing, Attacking, Cooldown }
    public AIState currentState = AIState.Chasing;

    [Header("Movement")]
    public float walkSpeed = 6.0f;
    public float rotationSpeed = 8.5f;
    public float gravity = 20f;

    [Header("Combat Settings")]
    public float attackRange = 4.5f;        // Distance at which Pharaoh starts attack
    public float stompDamageRadius = 4f;   // Damage area of the stomp
    public float attackCooldown = 0.8f;
    public float jumpAttackRange = 2.2f;    // Distance under Pharaoh that triggers Jump Attack
    public float jumpAttackRadius = 5.5f;   // Shockwave kill radius for Jump Attack

    [Header("Procedural Stomp Angles")]
    [Tooltip("Degrees added to the X euler angle of UpperLeg_R (same as the Inspector X slider)")]
    public float stompThighLiftAngle = -85f;
    [Tooltip("How many degrees the knee bends during stomp")]
    public float stompKneeBendAngle = 80f;

    [Header("Visual References")]
    public ParticleSystem stompParticles;  // Optional dust particle effect on stomp

    [Header("Procedural Attack Bones")]
    public Transform rightThigh;
    public Transform rightKnee;
    public Transform rightAnkle;
    public Transform headBone;
    public Transform spine01;

    [Header("Procedural Arm Bones")]
    public Transform rightShoulder;
    public Transform rightElbow;
    public Transform rightHand;

    private CharacterController controller;
    private Animator animator;
    private float cooldownTimer = 0f;
    private Vector3 moveDirection = Vector3.zero;

    // Procedural Animation States
    private bool isStomping = false;
    private float stompProgress = 0f;

    private Quaternion baseThighRot;
    private Quaternion baseKneeRot;
    private Quaternion baseHeadRot;
    private Quaternion baseSpineRot;
    private bool baseRotCaptured = false;  // guard: don't attack before idle is snapshotting
    private FirstPersonController currentAttackTarget;
    private float smoothKneeTuckedY = 195f;
    private System.Collections.Generic.List<Transform> rightFingers = new System.Collections.Generic.List<Transform>();
    private Vector3 spawnPosition;
    private Quaternion spawnRotation;

    void Start()
    {
        spawnPosition = transform.position;
        spawnRotation = transform.rotation;

        controller = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();

        if (animator != null)
        {
            animator.applyRootMotion = false; // Disable root motion to allow CharacterController to move the Pharaoh
        }

        // Find leg bones recursively if not assigned
        if (rightThigh == null) rightThigh = FindDeepChild(transform, "UpperLeg_R");
        if (rightKnee == null) rightKnee = FindDeepChild(transform, "LowerLeg_R");
        if (rightAnkle == null) rightAnkle = FindDeepChild(transform, "Ankle_R");
        if (headBone == null) headBone = FindDeepChild(transform, "Head");
        if (spine01 == null) spine01 = FindDeepChild(transform, "Spine_01");
        if (rightShoulder == null) rightShoulder = FindDeepChild(transform, "Shoulder_R");
        if (rightElbow == null) rightElbow = FindDeepChild(transform, "Elbow_R");
        if (rightHand == null) rightHand = FindDeepChild(transform, "Hand_R");

        // Find all finger bones under Hand_R
        if (rightHand != null)
        {
            foreach (Transform child in rightHand.GetComponentsInChildren<Transform>())
            {
                if (child != rightHand && (child.name.Contains("Finger") || child.name.Contains("Thumb")))
                {
                    rightFingers.Add(child);
                }
            }
        }

        // Find toes recursively and attach PharaohFootTrigger component + BoxCollider
        Transform toes = FindDeepChild(transform, "Toes_R");
        if (toes != null)
        {
            BoxCollider box = toes.gameObject.GetComponent<BoxCollider>();
            if (box == null)
            {
                box = toes.gameObject.AddComponent<BoxCollider>();
            }
            box.isTrigger = true;
            
            // Default size if newly created or uninitialized
            if (box.size == Vector3.one || box.size == Vector3.zero)
            {
                box.center = new Vector3(0f, 0f, 0.05f);
                box.size = new Vector3(0.25f, 0.2f, 0.35f);
            }

            PharaohFootTrigger footTrigger = toes.gameObject.GetComponent<PharaohFootTrigger>();
            if (footTrigger == null)
            {
                footTrigger = toes.gameObject.AddComponent<PharaohFootTrigger>();
            }
            footTrigger.pharaohAI = this;
        }

        // Reduce CharacterController radius to allow players to walk under/around the giant
        if (controller != null)
        {
            controller.radius = 0.12f;
        }

        // Setup a simple dust particle effect if not assigned
        CreateDefaultStompParticles();

        // Ensure right hand is holding the Giant Stick prefab at runtime
        Transform handR = FindDeepChild(transform, "Hand_R");
        if (handR != null)
        {
            Transform existingStick = handR.Find("Giant Stick");
            if (existingStick == null)
            {
                GameObject stickPrefab = Resources.Load<GameObject>("Giant Stick");
                if (stickPrefab != null)
                {
                    GameObject stickInstance = Instantiate(stickPrefab, handR);
                    stickInstance.name = "Giant Stick";
                    stickInstance.transform.localPosition = new Vector3(0f, -1.0f, 0f);
                    stickInstance.transform.localRotation = Quaternion.Euler(180f, 0f, 0f);
                    stickInstance.transform.localScale = new Vector3(2.98f, 3.29f, 1.74f);
                }
                else
                {
                    Debug.LogWarning("Giant Stick prefab could not be loaded from Resources!");
                }
            }
        }

        // Capture the idle bone rotations ONCE after the Animator has fully settled.
        // We do this in a coroutine so we wait for the first few Animator updates.
        StartCoroutine(CaptureIdleBoneRotations());
    }

    private IEnumerator CaptureIdleBoneRotations()
    {
        // Wait 3 frames: Animator needs at least 1 frame to evaluate the idle clip,
        // and a couple more to finish any entry-state blending.
        yield return null;
        yield return null;
        yield return null;

        if (rightThigh != null) baseThighRot = rightThigh.localRotation;
        if (rightKnee  != null) baseKneeRot  = rightKnee.localRotation;
        if (headBone   != null) baseHeadRot  = headBone.localRotation;
        if (spine01    != null) baseSpineRot = spine01.localRotation;
        baseRotCaptured = true;
    }

    void Update()
    {
        Vector3 finalMove = Vector3.zero;

        // Apply gravity
        if (controller != null && controller.isGrounded)
        {
            moveDirection.y = -0.5f;
        }
        else
        {
            moveDirection.y -= gravity * Time.deltaTime;
        }

        // Find nearest player
        FirstPersonController targetPlayer = GetNearestPlayer();

        switch (currentState)
        {
            case AIState.Chasing:
                if (targetPlayer != null)
                {
                    finalMove = ChaseTarget(targetPlayer);
                }
                else
                {
                    // Idle
                    if (animator != null) animator.SetFloat("Speed", 0f);
                }
                break;

            case AIState.Attacking:
                // Move forward slightly during the lift phase of the stomp to close the distance
                if (isStomping && stompProgress < 0.5f)
                {
                    if (animator != null) animator.SetFloat("Speed", walkSpeed * 0.4f);
                    finalMove = transform.forward * (walkSpeed * 0.4f);
                }
                else
                {
                    if (animator != null) animator.SetFloat("Speed", 0f);
                }
                break;

            case AIState.Cooldown:
                // Maintain idle and wait
                if (animator != null) animator.SetFloat("Speed", 0f);
                
                cooldownTimer -= Time.deltaTime;
                if (cooldownTimer <= 0)
                {
                    currentState = AIState.Chasing;
                }
                break;
        }

        // Add gravity to final movement vector
        finalMove.y = moveDirection.y;

        // Apply final combined movement once per frame
        if (controller != null && controller.enabled)
        {
            controller.Move(finalMove * Time.deltaTime);
        }
    }

    void LateUpdate()
    {
        // Override Animator every frame during stomp
        if (currentState == AIState.Attacking && isStomping)
        {
            // --- DYNAMIC AI STEP DISTANCE CALCULATION ---
            // Calculate horizontal distance between giant and target player
            float d = 4.5f; // default fallback reach distance
            if (currentAttackTarget != null)
            {
                d = Vector3.Distance(
                    new Vector3(transform.position.x, 0f, transform.position.z),
                    new Vector3(currentAttackTarget.transform.position.x, 0f, currentAttackTarget.transform.position.z)
                );
            }

            // Map distance 1.5m to 6.5m to a 0.0 to 1.0 fraction
            float t_dist = Mathf.Clamp01((d - 1.5f) / 5.0f);

            float kneeTuckedY = Mathf.Lerp(245f, 152f, t_dist);

            // Smooth the raw targets over time to eliminate jitter from player movements
            smoothKneeTuckedY = Mathf.Lerp(smoothKneeTuckedY, kneeTuckedY, 12f * Time.deltaTime);

            // --- UPPER LEG (UpperLeg_R) ---
            // Exact target rotation from user's manual pose (Rotation: X=10.591, Y=100.292, Z=-86.834)
            Quaternion targetRaisedThighRot = Quaternion.Euler(10.591f, 100.292f, -86.834f);
            
            // Scale the peak raise amount based on distance (lifts less at close range)
            float maxThighLiftWeight = Mathf.Lerp(0.65f, 1.0f, t_dist);
            Quaternion targetThighPeakRot = Quaternion.Slerp(baseThighRot, targetRaisedThighRot, maxThighLiftWeight);

            // --- LOWER LEG (LowerLeg_R) ---
            const float kneeIdleY  = 152f;
            const float kneeStampY = 152f; // foot straight on landing

            Quaternion targetThighRot;
            float targetKneeY;

            if (stompProgress < 0.5f)
            {
                // Lift phase: thigh rises forward, knee folds up (50% of total time)
                float t     = stompProgress / 0.5f;
                float easeT = Mathf.SmoothStep(0f, 1f, t);
                targetThighRot = Quaternion.Slerp(baseThighRot, targetThighPeakRot, easeT);
                targetKneeY  = Mathf.Lerp(kneeIdleY,   smoothKneeTuckedY, easeT);
            }
            else
            {
                // Slam phase: thigh crashes down, knee extends to stamp (50% of total time, slow-to-fast curve)
                float t      = (stompProgress - 0.5f) / 0.5f;
                float easeT  = t * t * t;  // cubic = starts very slow and accelerates to fast
                targetThighRot = Quaternion.Slerp(targetThighPeakRot, baseThighRot, easeT);
                targetKneeY  = Mathf.Lerp(smoothKneeTuckedY, kneeStampY, easeT);
            }

            // Capture the Animator's natural rotations before we override them
            Quaternion animatorThighRot = rightThigh != null ? rightThigh.localRotation : Quaternion.identity;
            Quaternion animatorKneeRot  = rightKnee != null ? rightKnee.localRotation : Quaternion.identity;

            // Generate procedural rotations for knee (keeping local X/Z stable from idle baseline)
            Quaternion targetKneeRot  = Quaternion.Euler(baseKneeRot.eulerAngles.x, targetKneeY, baseKneeRot.eulerAngles.z);

            // Smoothly blend out back to the Animator at the end of the stomp to prevent snapping
            float proceduralWeight = 1f;
            if (stompProgress > 0.9f)
            {
                float t_blend = (stompProgress - 0.9f) / 0.1f;
                proceduralWeight = Mathf.SmoothStep(1f, 0f, t_blend);
            }

            // Apply blended rotations
            if (rightThigh != null)
            {
                rightThigh.localRotation = Quaternion.Slerp(animatorThighRot, targetThighRot, proceduralWeight);
            }

            if (rightKnee != null)
            {
                rightKnee.localRotation = Quaternion.Slerp(animatorKneeRot, targetKneeRot, proceduralWeight);
            }
        }

        // --- SPINE BENDING (Spine_01) ---
        if (baseRotCaptured && spine01 != null)
        {
            if (currentState == AIState.Attacking && isStomping)
            {
                float spineTilt = 0f;
                if (stompProgress < 0.5f)
                {
                    float t = stompProgress / 0.5f;
                    spineTilt = Mathf.Lerp(0f, 20f, Mathf.SmoothStep(0f, 1f, t));
                }
                else
                {
                    float t = (stompProgress - 0.5f) / 0.5f;
                    if (t < 0.3f)
                    {
                        float t_impact = t / 0.3f;
                        spineTilt = Mathf.Lerp(20f, 25f, t_impact);
                    }
                    else
                    {
                        float t_recover = (t - 0.3f) / 0.7f;
                        spineTilt = Mathf.Lerp(25f, 0f, Mathf.SmoothStep(0f, 1f, t_recover));
                    }
                }

                // Smoothly blend out back to the Animator at the end of the stomp to prevent snapping
                float proceduralWeight = 1f;
                if (stompProgress > 0.9f)
                {
                    float t_blend = (stompProgress - 0.9f) / 0.1f;
                    proceduralWeight = Mathf.SmoothStep(1f, 0f, t_blend);
                }

                Quaternion targetSpineRot = baseSpineRot * Quaternion.Euler(0f, 0f, spineTilt);
                spine01.localRotation = Quaternion.Slerp(spine01.localRotation, targetSpineRot, proceduralWeight);
            }
        }

        // Always track player with head look down
        if (baseRotCaptured && headBone != null)
        {
            FirstPersonController targetPlayer = GetNearestPlayer();
            if (targetPlayer != null)
            {
                // Calculate vector from head bone to player's head/chest area
                Vector3 playerLookPos = targetPlayer.transform.position + Vector3.up * 1.5f;
                Vector3 headWorldPos = headBone.position;

                // Calculate tilt angle: Atan2 of vertical difference over horizontal distance
                float dy = headWorldPos.y - playerLookPos.y;
                float dx = Vector3.Distance(
                    new Vector3(headWorldPos.x, 0f, headWorldPos.z),
                    new Vector3(playerLookPos.x, 0f, playerLookPos.z)
                );

                if (dx > 0.1f)
                {
                    float targetLookDownAngle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                    
                    // Clamp to natural look limits (0 to 65 degrees look down)
                    targetLookDownAngle = Mathf.Clamp(targetLookDownAngle, 0f, 65f);
                    
                    // Rotate the head on X axis to look down
                    headBone.localRotation = baseHeadRot * Quaternion.Euler(targetLookDownAngle, 0f, 0f);
                }
            }
        }

        // Majestic Royal Scepter hold pose on the right arm (natural, strong grip)
        if (baseRotCaptured && rightShoulder != null && rightElbow != null && rightHand != null)
        {
            // Keep arm and hand rotations close to their natural idle/walk, 
            // bending the elbow forward by 80 degrees and keeping the wrist straight.
            rightShoulder.localRotation = Quaternion.Euler(11.33f, 33.18f, 55.00f);
            rightElbow.localRotation    = Quaternion.Euler(344.98f, 80.00f, 357.57f);
            rightHand.localRotation     = Quaternion.Euler(344.70f, 351.57f, 356.31f);

            // Procedurally curl fingers to grip the stick strongly
            foreach (var finger in rightFingers)
            {
                if (finger.name.Contains("Thumb"))
                {
                    finger.localRotation = finger.localRotation * Quaternion.Euler(0f, 0f, 35f);
                }
                else
                {
                    finger.localRotation = finger.localRotation * Quaternion.Euler(0f, 0f, 55f);
                }
            }
        }
    }

    private FirstPersonController GetNearestPlayer()
    {
        FirstPersonController[] players = FindObjectsOfType<FirstPersonController>();
        FirstPersonController nearest = null;
        float minDist = float.MaxValue;

        foreach (var player in players)
        {
            float dist = Vector3.Distance(transform.position, player.transform.position);
            if (dist < minDist)
            {
                minDist = dist;
                nearest = player;
            }
        }
        return nearest;
    }

    private Vector3 ChaseTarget(FirstPersonController player)
    {
        Vector3 targetPos = player.transform.position;
        targetPos.y = transform.position.y; // Keep level

        float dist = Vector3.Distance(transform.position, player.transform.position);

        // Face player
        Vector3 dir = (targetPos - transform.position).normalized;
        if (dir != Vector3.zero)
        {
            Quaternion targetRot = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
        }

        if (dist <= jumpAttackRange)
        {
            // Trigger jump slam attack
            StartCoroutine(JumpAttack(player));
            return Vector3.zero;
        }
        else if (dist <= attackRange)
        {
            // Trigger stomp/step attack
            StartCoroutine(StompAttack(player));
            return Vector3.zero;
        }
        else
        {
            // Move forward
            if (animator != null) animator.SetFloat("Speed", walkSpeed);
            return transform.forward * walkSpeed;
        }
    }

    private IEnumerator StompAttack(FirstPersonController target)
    {
        // Don't attack until idle baseline has been captured
        if (!baseRotCaptured) yield break;

        currentAttackTarget = target;
        currentState = AIState.Attacking;
        if (animator != null) animator.SetFloat("Speed", 0f);

        isStomping    = true;
        stompProgress = 0f;

        float totalDuration = 1.3f; // Slower, heavier step duration
        float elapsed = 0f;
        bool impactFired = false;

        while (elapsed < totalDuration)
        {
            elapsed += Time.deltaTime;
            stompProgress = Mathf.Clamp01(elapsed / totalDuration);

            // Active tracking of the player rotation during the stomp (until the slam starts descending at 50%)
            if (stompProgress < 0.5f && target != null)
            {
                Vector3 targetPos = target.transform.position;
                targetPos.y = transform.position.y; // Keep level

                // Face the player instantly
                Vector3 dir = (targetPos - transform.position).normalized;
                if (dir != Vector3.zero)
                {
                    transform.rotation = Quaternion.LookRotation(dir);
                }
            }

            // Fire impact once when the leg is slamming down (progress > 0.90)
            if (!impactFired && stompProgress >= 0.90f)
            {
                impactFired = true;
                OnStompImpact();
            }

            yield return null;
        }

        isStomping = false;
        currentAttackTarget = null;

        // Cooldown transition
        currentState  = AIState.Cooldown;
        cooldownTimer = attackCooldown;
    }

    private IEnumerator JumpAttack(FirstPersonController target)
    {
        if (!baseRotCaptured) yield break;

        currentState = AIState.Attacking;
        if (animator != null)
        {
            animator.SetFloat("Speed", 0f);
            animator.CrossFadeInFixedTime("JumpAttack", 0.15f);
        }

        // Wait for the landing/impact moment of the jump attack (around 0.85 seconds)
        yield return new WaitForSeconds(0.85f);

        // Landing Impact
        OnJumpImpact();

        // Wait for the remainder of the animation to finish (total ~1.5 seconds)
        yield return new WaitForSeconds(0.65f);

        // Cooldown transition
        currentState  = AIState.Cooldown;
        cooldownTimer = attackCooldown;
    }

    private void OnJumpImpact()
    {
        // Dust shockwave burst at the giant's center position
        if (stompParticles != null)
        {
            Vector3 particlePos = transform.position;
            particlePos.y = transform.position.y + 0.1f; // keep flat on ground level
            stompParticles.transform.position = particlePos;
            stompParticles.Play();
        }

        // Play audio if available, or just shake camera + kill players within radius
        FirstPersonController[] players = FindObjectsOfType<FirstPersonController>();
        foreach (var player in players)
        {
            float distFromGiant = Vector3.Distance(transform.position, player.transform.position);

            // Kill player if they are within the jump attack landing radius
            if (distFromGiant <= jumpAttackRadius)
            {
                player.Respawn();
            }

            // Screen Shake (based on distance from giant)
            if (distFromGiant < 50f)
            {
                float intensity = Mathf.Lerp(0.8f, 0.1f, distFromGiant / 50f);
                player.TriggerCameraShake(0.6f, intensity);
            }
        }
    }

    private void OnStompImpact()
    {
        // Find the right ankle/foot position for stomp damage location
        Vector3 stompPosition = rightAnkle != null ? rightAnkle.position : transform.position;

        // Position particle system at the foot's impact point on the ground
        if (stompParticles != null)
        {
            Vector3 particlePos = stompPosition;
            particlePos.y = transform.position.y + 0.1f; // keep flat on ground level
            stompParticles.transform.position = particlePos;
            stompParticles.Play();
        }

        // Play audio if available, or just shake camera
        FirstPersonController[] players = FindObjectsOfType<FirstPersonController>();
        foreach (var player in players)
        {
            float distFromGiant = Vector3.Distance(transform.position, player.transform.position);
            
            // Screen Shake (based on distance from giant)
            if (distFromGiant < 40f)
            {
                float intensity = Mathf.Lerp(0.5f, 0.05f, distFromGiant / 40f);
                player.TriggerCameraShake(0.4f, intensity);
            }
        }
    }

    private Transform FindDeepChild(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name.Equals(name, System.StringComparison.OrdinalIgnoreCase) || child.name.Contains(name))
            {
                return child;
            }
            Transform result = FindDeepChild(child, name);
            if (result != null) return result;
        }
        return null;
    }

    private void CreateDefaultStompParticles()
    {
        if (stompParticles != null) return;

        // Check if there is already a particle system attached
        stompParticles = GetComponentInChildren<ParticleSystem>();
        if (stompParticles != null) return;

        GameObject pObj = new GameObject("StompParticles");
        pObj.transform.SetParent(transform, false);
        pObj.transform.localPosition = new Vector3(0, 0.1f, 0);

        stompParticles = pObj.AddComponent<ParticleSystem>();
        
        var main = stompParticles.main;
        main.startLifetime = 1f;
        main.startSpeed = 15f;
        main.startSize = 0.8f;
        main.loop = false;
        main.playOnAwake = false;
        main.maxParticles = 50;

        var emission = stompParticles.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f; // Burst only

        var burst = new ParticleSystem.Burst(0f, 40);
        emission.SetBursts(new ParticleSystem.Burst[] { burst });

        var shape = stompParticles.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 2f;
        shape.rotation = new Vector3(90f, 0f, 0f); // Face flat on ground
    }

    public void ResetToSpawn()
    {
        if (controller != null)
        {
            controller.enabled = false;
        }

        transform.position = spawnPosition;
        transform.rotation = spawnRotation;

        if (controller != null)
        {
            controller.enabled = true;
        }

        // Reset AI state so he doesn't keep attacking or stomping instantly
        currentState = AIState.Chasing;
        cooldownTimer = 1.0f; // brief delay before chasing again
        currentAttackTarget = null;
        isStomping = false;
        stompProgress = 0f;

        // Reset animations
        if (animator != null)
        {
            animator.SetFloat("Speed", 0f);
            animator.Play("Idle", 0, 0f);
        }
    }

    public bool IsStompingActive()
    {
        return currentState == AIState.Attacking && isStomping;
    }
}
