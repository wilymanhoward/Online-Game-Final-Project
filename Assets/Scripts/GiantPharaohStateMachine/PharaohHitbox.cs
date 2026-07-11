using UnityEngine;

public class PharaohHitbox : MonoBehaviour
{
    public EnemyStateMachineController controller;

    private void OnCollisionEnter(Collision collision)
    {
        CheckHit(collision.gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        CheckHit(other.gameObject);
    }

    private void CheckHit(GameObject obj)
    {
        if (controller == null) return;

        // Check if hit by a throwable object (tag-based or component-based)
        if (obj.CompareTag("throwable") || obj.CompareTag("Throwable") || obj.GetComponent<ThrowableObject>() != null)
        {
            controller.OnHitByThrowable(obj);
        }
    }
}
