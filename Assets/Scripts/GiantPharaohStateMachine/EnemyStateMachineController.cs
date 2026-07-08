using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Events;

public class EnemyStateMachineController : MonoBehaviour
{
    public enum GiantState
    {
        Idle,
        Walk,
        Attack,
        Hurt,
        Fall,
        Recovery
    }

    [System.Serializable]
    public struct AttackObjectTrigger
    {
        public GameObject gameObjectToActivate;
        [Range(0f, 1f)]
        public float startPercentage;
        [Range(0f, 1f)]
        public float endPercentage;
    }

    [System.Serializable]
    public class AttackAudioTrigger
    {
        public AudioClip audioClip;
        [Range(0f, 1f)]
        public float playPercentage;

        [System.NonSerialized]
        public bool hasPlayed = false;
    }

    [System.Serializable]
    public struct EnemyAttack
    {
        public string animatorStateName;
        public float distanceToAttack;
        public AttackObjectTrigger[] objectsToActivate;
        public AttackAudioTrigger[] audioTriggers;
    }

    [Header("Audio Settings")]
    public AudioSource bossAudioSource;

    [Header("State Info")]
    public GiantState currentStateEnum = GiantState.Idle;
    protected IState currentState;
    protected Dictionary<GiantState, IState> states;

    [Header("Movement")]
    public float walkSpeed = 6.0f;
    public float rotationSpeed = 8.5f;
    public float gravity = 20f;
    public float targetReachedThreshold = 0.5f;

    [Header("Combat Settings")]
    public float jumpAttackRadius = 5.5f;   // Shockwave kill radius for Jump Attack

    [Header("Attacks Setup")]
    public EnemyAttack[] enemyAttacks;

    [Header("State Durations")]
    public float hurtDuration = 1.5f;
    public float fallDuration = 2.5f;
    public float hitGroundDuration = 1.5f;
    public float hitWallDuration = 1.5f;
    public float recoveryDuration = 2.0f;

    [Header("Colliders")]
    public Collider mainHitbox;
    public Collider hitWallCollider;

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

    [Header("State Events (Inspector)")]
    public UnityEvent OnEnterIdle;
    public UnityEvent OnExitIdle;
    public UnityEvent OnEnterWalk;
    public UnityEvent OnExitWalk;
    public UnityEvent OnEnterAttack;
    public UnityEvent OnExitAttack;
    public UnityEvent OnEnterHurt;
    public UnityEvent OnExitHurt;
    public UnityEvent OnEnterFall;
    public UnityEvent OnExitFall;
    public UnityEvent OnEnterRecovery;
    public UnityEvent OnExitRecovery;

    protected CharacterController controller;
    protected Animator animator;
    protected Vector3 moveDirection = Vector3.zero;
    protected Vector3 finalMoveDirection = Vector3.zero;

    // Procedural Animation States
    [HideInInspector] public bool isStomping = false;
    [HideInInspector] public float stompProgress = 0f;
    [HideInInspector] public FirstPersonController currentAttackTarget;

    protected Quaternion baseThighRot;
    protected Quaternion baseKneeRot;
    protected Quaternion baseHeadRot;
    protected Quaternion baseSpineRot;
    protected bool baseRotCaptured = false;
    protected float smoothKneeTuckedY = 195f;
    protected List<Transform> rightFingers = new List<Transform>();
    protected Vector3 spawnPosition;
    protected Quaternion spawnRotation;

    public Vector3 idlePoint { get; set; }
    public FirstPersonController TargetPlayer { get; protected set; }
    public bool PlayerInArena { get; protected set; }
    public EnemyAttack CurrentAttack { get; set; }

    public Animator Animator => animator;
    public CharacterController CharacterController => controller;

    protected virtual void Awake()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();

        // Pre-instantiate states
        states = new Dictionary<GiantState, IState>()
        {
            { GiantState.Idle, new IdleState(this) },
            { GiantState.Walk, new WalkState(this) },
            { GiantState.Attack, new AttackState(this) },
            { GiantState.Hurt, new HurtState(this) },
            { GiantState.Fall, new FallState(this) },
            { GiantState.Recovery, new RecoveryState(this) }
        };
    }

    protected virtual void Start()
    {
        spawnPosition = transform.position;
        spawnRotation = transform.rotation;
        idlePoint = spawnPosition;

        if (animator != null)
        {
            animator.applyRootMotion = false;
        }

        // Capture bones automatically if not assigned (preserving legacy setup)
        FindBones();

        // Setup triggers / colliders references
        if (hitWallCollider != null)
        {
            hitWallCollider.enabled = false;
            var hitWallTrigger = hitWallCollider.gameObject.GetComponent<PharaohHitWallTrigger>();
            if (hitWallTrigger == null)
            {
                hitWallTrigger = hitWallCollider.gameObject.AddComponent<PharaohHitWallTrigger>();
            }
            hitWallTrigger.controller = this;
        }

        if (mainHitbox != null)
        {
            mainHitbox.enabled = true;
            var hitboxTrigger = mainHitbox.gameObject.GetComponent<PharaohHitbox>();
            if (hitboxTrigger == null)
            {
                hitboxTrigger = mainHitbox.gameObject.AddComponent<PharaohHitbox>();
            }
            hitboxTrigger.controller = this;
        }

        // Setup a simple dust particle effect if not assigned
        CreateDefaultStompParticles();

        // Setup stick gripping (preserving legacy)
        SetupScepterGrip();

        // Capture base rotations for bones
        StartCoroutine(CaptureIdleBoneRotations());

        // Initialize state machine
        TransitionToState(GiantState.Idle);
    }

    protected virtual void Update()
    {
        // Apply gravity
        if (controller != null && controller.isGrounded)
        {
            moveDirection.y = -0.5f;
        }
        else
        {
            moveDirection.y -= gravity * Time.deltaTime;
        }

        // Tick state machine
        if (currentState != null)
        {
            currentState.Update();
        }

        // Apply final combined movement
        Vector3 movement = finalMoveDirection;
        movement.y = moveDirection.y;

        if (controller != null && controller.enabled)
        {
            controller.Move(movement * Time.deltaTime);
        }
    }

    protected virtual void FixedUpdate()
    {
        if (currentState != null)
        {
            currentState.FixedUpdate();
        }
    }

    protected virtual void LateUpdate()
    {
        ApplyProceduralBones();
    }

    public void SetFinalMoveDirection(Vector3 dir)
    {
        finalMoveDirection = dir;
    }

    public void TransitionToState(GiantState newStateEnum)
    {
        if (currentState != null)
        {
            currentState.Exit();
            TriggerExitEvent(currentStateEnum);
        }

        currentStateEnum = newStateEnum;
        currentState = states[newStateEnum];

        TriggerEnterEvent(currentStateEnum);
        currentState.Enter();
    }

    private void TriggerEnterEvent(GiantState state)
    {
        switch (state)
        {
            case GiantState.Idle: OnEnterIdle?.Invoke(); break;
            case GiantState.Walk: OnEnterWalk?.Invoke(); break;
            case GiantState.Attack: OnEnterAttack?.Invoke(); break;
            case GiantState.Hurt: OnEnterHurt?.Invoke(); break;
            case GiantState.Fall: OnEnterFall?.Invoke(); break;
            case GiantState.Recovery: OnEnterRecovery?.Invoke(); break;
        }
    }

    private void TriggerExitEvent(GiantState state)
    {
        switch (state)
        {
            case GiantState.Idle: OnExitIdle?.Invoke(); break;
            case GiantState.Walk: OnExitWalk?.Invoke(); break;
            case GiantState.Attack: OnExitAttack?.Invoke(); break;
            case GiantState.Hurt: OnExitHurt?.Invoke(); break;
            case GiantState.Fall: OnExitFall?.Invoke(); break;
            case GiantState.Recovery: OnExitRecovery?.Invoke(); break;
        }
    }

    public void OnPlayerEnterArena(FirstPersonController player)
    {
        TargetPlayer = player;
        PlayerInArena = true;
    }

    public void OnPlayerExitArena(FirstPersonController player)
    {
        PlayerInArena = false;
        TargetPlayer = null;
    }

    public void OnHitByThrowable(GameObject throwable)
    {
        // Do not interrupt the Pharaoh's attack animation
        if (currentStateEnum == GiantState.Attack) return;

        if (currentStateEnum == GiantState.Idle || currentStateEnum == GiantState.Walk)
        {
            TransitionToState(GiantState.Hurt);
        }
        else if (currentStateEnum == GiantState.Hurt)
        {
            TransitionToState(GiantState.Fall);
        }
    }

    public void OnHitWall()
    {
        // Transition to Recovery is disabled here so that the Fall animation completes fully instead.
    }

    public void SetHitboxActive(bool active)
    {
        if (mainHitbox != null)
        {
            mainHitbox.enabled = active;
        }
    }

    public void SetHitWallColliderActive(bool active)
    {
        if (hitWallCollider != null)
        {
            hitWallCollider.enabled = active;
        }
    }

    public void ResetAttackStates()
    {
        isStomping = false;
        currentAttackTarget = null;
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

        // Reset variables
        TargetPlayer = null;
        PlayerInArena = false;
        isStomping = false;
        stompProgress = 0f;
        currentAttackTarget = null;

        if (animator != null)
        {
            animator.SetFloat("Speed", 0f);
            animator.Play("Idle", 0, 0f);
        }

        TransitionToState(GiantState.Idle);
    }

    public bool IsStompingActive()
    {
        return currentStateEnum == GiantState.Attack && isStomping;
    }

    private IEnumerator CaptureIdleBoneRotations()
    {
        yield return null;
        yield return null;
        yield return null;

        if (rightThigh != null) baseThighRot = rightThigh.localRotation;
        if (rightKnee != null) baseKneeRot = rightKnee.localRotation;
        if (headBone != null) baseHeadRot = headBone.localRotation;
        if (spine01 != null) baseSpineRot = spine01.localRotation;
        baseRotCaptured = true;
    }

    private void FindBones()
    {
        if (rightThigh == null) rightThigh = FindDeepChild(transform, "UpperLeg_R");
        if (rightKnee == null) rightKnee = FindDeepChild(transform, "LowerLeg_R");
        if (rightAnkle == null) rightAnkle = FindDeepChild(transform, "Ankle_R");
        if (headBone == null) headBone = FindDeepChild(transform, "Head");
        if (spine01 == null) spine01 = FindDeepChild(transform, "Spine_01");
        if (rightShoulder == null) rightShoulder = FindDeepChild(transform, "Shoulder_R");
        if (rightElbow == null) rightElbow = FindDeepChild(transform, "Elbow_R");
        if (rightHand == null) rightHand = FindDeepChild(transform, "Hand_R");

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

        Transform toes = FindDeepChild(transform, "Toes_R");
        if (toes != null)
        {
            PharaohFootTrigger footTrigger = toes.gameObject.GetComponent<PharaohFootTrigger>();
            if (footTrigger == null)
            {
                footTrigger = toes.gameObject.AddComponent<PharaohFootTrigger>();
            }
            footTrigger.stateMachineController = this;
        }

        if (controller != null)
        {
            controller.radius = 0.12f;
        }
    }

    private void SetupScepterGrip()
    {
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
            }
        }
    }

    private void ApplyProceduralBones()
    {
        // 1. Stomp Leg Procedural Overlay
        if (currentStateEnum == GiantState.Attack && isStomping)
        {
            float d = 4.5f;
            if (currentAttackTarget != null)
            {
                d = Vector3.Distance(
                    new Vector3(transform.position.x, 0f, transform.position.z),
                    new Vector3(currentAttackTarget.transform.position.x, 0f, currentAttackTarget.transform.position.z)
                );
            }

            float t_dist = Mathf.Clamp01((d - 1.5f) / 5.0f);
            float kneeTuckedY = Mathf.Lerp(245f, 152f, t_dist);
            smoothKneeTuckedY = Mathf.Lerp(smoothKneeTuckedY, kneeTuckedY, 12f * Time.deltaTime);

            Quaternion targetRaisedThighRot = Quaternion.Euler(10.591f, 100.292f, -86.834f);
            float maxThighLiftWeight = Mathf.Lerp(0.65f, 1.0f, t_dist);
            Quaternion targetThighPeakRot = Quaternion.Slerp(baseThighRot, targetRaisedThighRot, maxThighLiftWeight);

            const float kneeIdleY = 152f;
            const float kneeStampY = 152f;

            Quaternion targetThighRot;
            float targetKneeY;

            if (stompProgress < 0.5f)
            {
                float t = stompProgress / 0.5f;
                float easeT = Mathf.SmoothStep(0f, 1f, t);
                targetThighRot = Quaternion.Slerp(baseThighRot, targetThighPeakRot, easeT);
                targetKneeY = Mathf.Lerp(kneeIdleY, smoothKneeTuckedY, easeT);
            }
            else
            {
                float t = (stompProgress - 0.5f) / 0.5f;
                float easeT = t * t * t;
                targetThighRot = Quaternion.Slerp(targetThighPeakRot, baseThighRot, easeT);
                targetKneeY = Mathf.Lerp(smoothKneeTuckedY, kneeStampY, easeT);
            }

            Quaternion animatorThighRot = rightThigh != null ? rightThigh.localRotation : Quaternion.identity;
            Quaternion animatorKneeRot = rightKnee != null ? rightKnee.localRotation : Quaternion.identity;
            Quaternion targetKneeRot = Quaternion.Euler(baseKneeRot.eulerAngles.x, targetKneeY, baseKneeRot.eulerAngles.z);

            float proceduralWeight = 1f;
            if (stompProgress > 0.9f)
            {
                float t_blend = (stompProgress - 0.9f) / 0.1f;
                proceduralWeight = Mathf.SmoothStep(1f, 0f, t_blend);
            }

            if (rightThigh != null) rightThigh.localRotation = Quaternion.Slerp(animatorThighRot, targetThighRot, proceduralWeight);
            if (rightKnee != null) rightKnee.localRotation = Quaternion.Slerp(animatorKneeRot, targetKneeRot, proceduralWeight);
        }

        // 2. Spine Tilt
        if (baseRotCaptured && spine01 != null)
        {
            if (currentStateEnum == GiantState.Attack && isStomping)
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

        // 3. Head Tracking Player (Idle, Walk, Attack, Recovery)
        bool canTrackHead = currentStateEnum == GiantState.Idle || currentStateEnum == GiantState.Walk ||
                            currentStateEnum == GiantState.Attack || currentStateEnum == GiantState.Recovery;
        if (baseRotCaptured && headBone != null && canTrackHead)
        {
            FirstPersonController nearestPlayer = TargetPlayer != null ? TargetPlayer : FindObjectOfType<FirstPersonController>();
            if (nearestPlayer != null)
            {
                Vector3 playerLookPos = nearestPlayer.transform.position + Vector3.up * 1.5f;
                Vector3 headWorldPos = headBone.position;

                float dy = headWorldPos.y - playerLookPos.y;
                float dx = Vector3.Distance(
                    new Vector3(headWorldPos.x, 0f, headWorldPos.z),
                    new Vector3(playerLookPos.x, 0f, playerLookPos.z)
                );

                if (dx > 0.1f)
                {
                    float targetLookDownAngle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                    targetLookDownAngle = Mathf.Clamp(targetLookDownAngle, 0f, 65f);
                    headBone.localRotation = baseHeadRot * Quaternion.Euler(targetLookDownAngle, 0f, 0f);
                }
            }
        }

        // 4. Arm & Scepter Holding (Majestic hold pose)
        if (baseRotCaptured && rightShoulder != null && rightElbow != null && rightHand != null)
        {
            rightShoulder.localRotation = Quaternion.Euler(11.33f, 33.18f, 55.00f);
            rightElbow.localRotation = Quaternion.Euler(344.98f, 80.00f, 357.57f);
            rightHand.localRotation = Quaternion.Euler(344.70f, 351.57f, 356.31f);

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

    private void OnJumpImpact()
    {
        if (stompParticles != null)
        {
            Vector3 particlePos = transform.position;
            particlePos.y = transform.position.y + 0.1f;
            stompParticles.transform.position = particlePos;
            stompParticles.Play();
        }

        FirstPersonController[] players = FindObjectsOfType<FirstPersonController>();
        foreach (var player in players)
        {
            float distFromGiant = Vector3.Distance(transform.position, player.transform.position);

            if (distFromGiant <= jumpAttackRadius)
            {
                player.Respawn();
            }
        }
    }

    private void OnStompImpact()
    {
        Vector3 stompPosition = rightAnkle != null ? rightAnkle.position : transform.position;

        if (stompParticles != null)
        {
            Vector3 particlePos = stompPosition;
            particlePos.y = transform.position.y + 0.1f;
            stompParticles.transform.position = particlePos;
            stompParticles.Play();
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
        emission.rateOverTime = 0f;

        var burst = new ParticleSystem.Burst(0f, 40);
        emission.SetBursts(new ParticleSystem.Burst[] { burst });

        var shape = stompParticles.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 2f;
        shape.rotation = new Vector3(90f, 0f, 0f);
    }

    public float GetAttackDuration(string stateName)
    {
        if (stateName.Contains("Stomp") || stateName.Contains("stomp"))
        {
            return 1.3f;
        }
        else if (stateName.Contains("Jump") || stateName.Contains("jump"))
        {
            return 1.5f;
        }
        return 1.5f;
    }

    public void SelectRandomAttack()
    {
        if (enemyAttacks != null && enemyAttacks.Length > 0)
        {
            int index = Random.Range(0, enemyAttacks.Length);
            CurrentAttack = enemyAttacks[index];
        }
    }

    public void ClearCurrentAttack()
    {
        CurrentAttack = default;
    }
}
