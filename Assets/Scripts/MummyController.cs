using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class MummyController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float rotationSpeed = 10f;
    public float gravity = 20f;

    [Header("Procedural Animation Settings")]
    public float walkCycleSpeed = 10f;
    public float legSwingAngle = 30f;
    public float armSwingAngle = 30f;
    public float bodyBobHeight = 0.1f;
    public float bodyRollAngle = 5f;

    private CharacterController controller;
    private Vector3 moveDirection = Vector3.zero;

    // Rig joints
    private Transform leftLeg;
    private Transform rightLeg;
    private Transform leftArm;
    private Transform rightArm;
    private Transform hips;

    // Default local rotations to restore when idle
    private Quaternion defaultLeftLegRot;
    private Quaternion defaultRightLegRot;
    private Quaternion defaultLeftArmRot;
    private Quaternion defaultRightArmRot;
    private Quaternion defaultHipsRot;
    private Vector3 defaultHipsLocalPos;

    private float walkTime = 0f;

    void Start()
    {
        // Disable Animator component to prevent it from resetting bones to (0,0,0) and making the mummy lie down
        Animator anim = GetComponent<Animator>();
        if (anim != null) anim.enabled = false;

        controller = GetComponent<CharacterController>();

        // Find reference joints
        leftLeg = FindDeepChild(transform, "UpperLeg_L");
        rightLeg = FindDeepChild(transform, "UpperLeg_R");
        leftArm = FindDeepChild(transform, "Shoulder_L");
        rightArm = FindDeepChild(transform, "Shoulder_R");
        hips = FindDeepChild(transform, "Hips");

        // Cache default positions and rotations
        if (leftLeg) defaultLeftLegRot = leftLeg.localRotation;
        if (rightLeg) defaultRightLegRot = rightLeg.localRotation;
        if (leftArm) defaultLeftArmRot = leftArm.localRotation;
        if (rightArm) defaultRightArmRot = rightArm.localRotation;
        if (hips)
        {
            defaultHipsRot = hips.localRotation;
            defaultHipsLocalPos = hips.localPosition;
        }
    }

    void Update()
    {
        // Calculate movement input
        float moveHorizontal = Input.GetAxis("Horizontal");
        float moveVertical = Input.GetAxis("Vertical");

        Vector3 inputDir = new Vector3(moveHorizontal, 0.0f, moveVertical);
        bool isMoving = inputDir.magnitude > 0.1f;

        if (controller.isGrounded)
        {
            // Move relative to camera/world coordinates
            moveDirection = inputDir.normalized * moveSpeed;
        }

        // Apply gravity
        moveDirection.y -= gravity * Time.deltaTime;

        // Move character
        controller.Move(moveDirection * Time.deltaTime);

        // Rotation
        if (isMoving)
        {
            Vector3 targetDir = new Vector3(moveDirection.x, 0, moveDirection.z);
            if (targetDir != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(targetDir);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }

            // Animate waddle walk
            walkTime += Time.deltaTime * walkCycleSpeed;
            float sin = Mathf.Sin(walkTime);
            float cos = Mathf.Cos(walkTime);

            // Swing legs (X-axis relative to defaults)
            if (leftLeg) leftLeg.localRotation = defaultLeftLegRot * Quaternion.AngleAxis(sin * legSwingAngle, Vector3.up);
            if (rightLeg) rightLeg.localRotation = defaultRightLegRot * Quaternion.AngleAxis(-sin * legSwingAngle, Vector3.up);

            // Swing arms (opposite to legs)
            if (leftArm) leftArm.localRotation = defaultLeftArmRot * Quaternion.AngleAxis(-sin * armSwingAngle, Vector3.up);
            if (rightArm) rightArm.localRotation = defaultRightArmRot * Quaternion.AngleAxis(sin * armSwingAngle, Vector3.up);

            // Bob body up and down and roll side to side
            if (hips)
            {
                hips.localPosition = defaultHipsLocalPos + new Vector3(0, Mathf.Abs(sin) * bodyBobHeight, 0);
                hips.localRotation = defaultHipsRot * Quaternion.AngleAxis(cos * bodyRollAngle, Vector3.forward);
            }
        }
        else
        {
            // Reset to idle pose smoothly
            walkTime = 0f;
            if (leftLeg) leftLeg.localRotation = Quaternion.Slerp(leftLeg.localRotation, defaultLeftLegRot, 10f * Time.deltaTime);
            if (rightLeg) rightLeg.localRotation = Quaternion.Slerp(rightLeg.localRotation, defaultRightLegRot, 10f * Time.deltaTime);
            if (leftArm) leftArm.localRotation = Quaternion.Slerp(leftArm.localRotation, defaultLeftArmRot, 10f * Time.deltaTime);
            if (rightArm) rightArm.localRotation = Quaternion.Slerp(rightArm.localRotation, defaultRightArmRot, 10f * Time.deltaTime);
            if (hips)
            {
                hips.localPosition = Vector3.Lerp(hips.localPosition, defaultHipsLocalPos, 10f * Time.deltaTime);
                hips.localRotation = Quaternion.Slerp(hips.localRotation, defaultHipsRot, 10f * Time.deltaTime);
            }
        }
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
