using UnityEngine;

public class ResetAirTimeTrigger : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        FirstPersonController fpc = other.GetComponentInParent<FirstPersonController>();
        if (fpc != null)
        {
            fpc.ResetAirTime();
            Debug.Log($"[ResetAirTimeTrigger] Trigger zone entered by {other.name}. Reset air time for player: {fpc.name}");
        }
    }
}
