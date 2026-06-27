using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Target Settings")]
    public Transform target;

    [Header("Follow Settings")]
    public Vector3 offset = new Vector3(0f, 4f, -6f);
    public float smoothSpeed = 5f;

    void Start()
    {
        // If no target is set, try to find the mummy
        if (target == null)
        {
            GameObject mummy = GameObject.Find("SM_Chr_Mummy_01");
            if (mummy != null)
            {
                target = mummy.transform;
            }
        }
    }

    void LateUpdate()
    {
        if (target == null) return;

        // Calculate desired position
        Vector3 desiredPosition = target.position + offset;
        
        // Smoothly interpolate to desired position
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
        transform.position = smoothedPosition;

        // Smoothly look at target center (hips/chest area, slightly above feet)
        Vector3 lookTarget = target.position + Vector3.up * 1f;
        transform.LookAt(lookTarget);
    }
}
