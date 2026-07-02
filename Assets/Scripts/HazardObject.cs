using UnityEngine;

public class HazardObject : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        // Check if the overlapping object is the player
        FirstPersonController player = other.GetComponent<FirstPersonController>();
        if (player != null)
        {
            player.Respawn();
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Support solid physical hazard colliders
        FirstPersonController player = collision.collider.GetComponent<FirstPersonController>();
        if (player != null)
        {
            player.Respawn();
        }
    }
}
