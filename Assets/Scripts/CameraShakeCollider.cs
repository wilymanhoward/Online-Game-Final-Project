using UnityEngine;

public class CameraShakeCollider : MonoBehaviour
{
    [Header("Camera Shake Settings")]
    [Tooltip("Duration of the camera shake in seconds.")]
    public float duration = 0.5f;

    [Tooltip("Magnitude (intensity) of the camera shake.")]
    public float magnitude = 0.5f;

    private void OnTriggerEnter(Collider other)
    {
        FirstPersonController player = other.GetComponent<FirstPersonController>();
        if (player != null)
        {
            player.TriggerCameraShake(duration, magnitude);
        }
    }
}
