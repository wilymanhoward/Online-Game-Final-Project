using UnityEngine;

public class PharaohFootTrigger : MonoBehaviour
{
    [HideInInspector]
    public EnemyStateMachineController stateMachineController;

    private void OnTriggerEnter(Collider other)
    {
        // Self-heal reference if null
        if (stateMachineController == null)
        {
            stateMachineController = GetComponentInParent<EnemyStateMachineController>();
        }

        if (stateMachineController == null || !stateMachineController.IsStompingActive()) return;

        FirstPersonController player = other.GetComponent<FirstPersonController>();
        if (player != null)
        {
            player.Respawn();
        }
    }

    private void OnTriggerStay(Collider other)
    {
        OnTriggerEnter(other);
    }
}
