using UnityEngine;
using Photon.Pun;

/// <summary>
/// Attach this to your projectile prefab (e.g. FireProjectile Variant).
/// It translates the projectile along transform.forward at a set speed.
/// Player death is handled by the HazardObject script already on the prefab.
/// Only destroys itself when hitting objects on the specified layer mask.
/// </summary>
public class RedLightProjectile : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float speed = 25f;
    [SerializeField] private float lifetime = 5f;

    [Header("Destruction Settings")]
    [Tooltip("The projectile will destroy itself when hitting objects on these layers.")]
    [SerializeField] private LayerMask destroyOnContactLayers = ~0; // Default: everything

    private void Start()
    {
        // Ensure rigidbody is kinematic so we control movement manually
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;

        // Self-destruct after lifetime to avoid clutter
        Destroy(gameObject, lifetime);
    }

    private void Update()
    {
        // Move along the forward direction (set by the shooter via Quaternion.LookRotation)
        transform.position += transform.forward * speed * Time.deltaTime;
    }

    private void OnTriggerEnter(Collider other)
    {
        // Only destroy if the hit object's layer is in the destroy mask
        if ((destroyOnContactLayers & (1 << other.gameObject.layer)) == 0) return;

        Destroy(gameObject);
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Only destroy if the hit object's layer is in the destroy mask
        if ((destroyOnContactLayers & (1 << collision.gameObject.layer)) == 0) return;

        Destroy(gameObject);
    }
}
