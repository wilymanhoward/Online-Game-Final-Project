using UnityEngine;

public class PharaohHitWallTrigger : MonoBehaviour
{
    public EnemyStateMachineController controller;

    private void OnTriggerEnter(Collider other)
    {
        CheckHit(other);
    }

    private void OnCollisionEnter(Collision collision)
    {
        CheckHit(collision.collider);
    }

    private void CheckHit(Collider other)
    {
        if (controller == null) return;

        // Avoid self-collision and player/throwables collision
        if (other.transform.IsChildOf(controller.transform) ||
            other.CompareTag("Player") ||
            other.CompareTag("throwable") ||
            other.CompareTag("Throwable"))
        {
            return;
        }

        // Only register wall hits if we are currently in the Fall state
        if (controller.currentStateEnum == EnemyStateMachineController.GiantState.Fall)
        {
            controller.OnHitWall();
        }
    }
}
