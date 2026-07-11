using UnityEngine;

public class PharaohArenaTrigger : MonoBehaviour
{
    public EnemyStateMachineController controller;

    private void OnTriggerEnter(Collider other)
    {
        if (controller == null) return;

        FirstPersonController player = other.GetComponent<FirstPersonController>();
        if (player != null)
        {
            controller.OnPlayerEnterArena(player);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (controller == null) return;

        FirstPersonController player = other.GetComponent<FirstPersonController>();
        if (player != null)
        {
            controller.OnPlayerExitArena(player);
        }
    }
}
