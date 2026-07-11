using System.Collections.Generic;
using UnityEngine;

public class FracturedDestructible : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The parent GameObject containing all the fractured piece/cell children.")]
    [SerializeField] private GameObject fracturedParent;

    [Header("Physics Settings")]
    [Tooltip("Force of the explosion applied to the fractured pieces.")]
    [SerializeField] private float explosionForce = 15f;

    [Tooltip("Radius of the explosion.")]
    [SerializeField] private float explosionRadius = 5f;

    [Tooltip("Upwards modifier to push fractured pieces slightly into the air.")]
    [SerializeField] private float upwardsModifier = 0.5f;

    [Tooltip("Auto-configure Rigidbody and convex MeshCollider on pieces if they are missing.")]
    [SerializeField] private bool autoAddPhysics = true;

    [Tooltip("Mass assigned to auto-created rigidbodies.")]
    [SerializeField] private float pieceMass = 1f;

    [Header("Cleanup Settings")]
    [Tooltip("If true, the fractured pieces will automatically hide after a delay to prevent clutter.")]
    [SerializeField] private bool autoHideFractures = true;

    [Tooltip("Time in seconds to wait before hiding the fractured pieces.")]
    [SerializeField] private float hideDelay = 5f;

    private struct PartState
    {
        public Transform transform;
        public Vector3 originalLocalPos;
        public Quaternion originalLocalRot;
        public Rigidbody rb;
        public Collider col;
    }

    private List<PartState> parts = new List<PartState>();
    private bool isBroken = false;
    private Coroutine hideCoroutine;

    private void Awake()
    {
        InitializeParts();
    }

    private void InitializeParts()
    {
        if (fracturedParent == null)
        {
            Debug.LogError($"[FracturedDestructible] Fractured Parent is not assigned on {gameObject.name}!", this);
            return;
        }

        parts.Clear();

        // Loop through all child objects of the fractured parent
        foreach (Transform child in fracturedParent.transform)
        {
            PartState state = new PartState();
            state.transform = child;
            state.originalLocalPos = child.localPosition;
            state.originalLocalRot = child.localRotation;

            // Find or add Rigidbody
            state.rb = child.GetComponent<Rigidbody>();
            if (state.rb == null && autoAddPhysics)
            {
                state.rb = child.gameObject.AddComponent<Rigidbody>();
                state.rb.mass = pieceMass;
            }

            // Find or add Collider (convex MeshCollider is best for fractured cells)
            state.col = child.GetComponent<Collider>();
            if (state.col == null && autoAddPhysics)
            {
                MeshFilter meshFilter = child.GetComponent<MeshFilter>();
                if (meshFilter != null && meshFilter.sharedMesh != null)
                {
                    MeshCollider meshCol = child.gameObject.AddComponent<MeshCollider>();
                    meshCol.convex = true;
                    state.col = meshCol;
                }
            }

            // Keep pieces kinematic so they stand solid, but leave colliders active so they block collision
            if (state.rb != null)
            {
                state.rb.isKinematic = true;
            }
            if (state.col != null)
            {
                state.col.enabled = true;
            }

            parts.Add(state);
        }

        // Make sure the object is initially visible
        if (fracturedParent != null)
        {
            fracturedParent.SetActive(true);
        }
    }

    /// <summary>
    /// Breaks the object, causing all child parts to explode outward.
    /// Can be linked to the Pillar's OnBreak event.
    /// </summary>
    public void Break()
    {
        if (isBroken) return;
        isBroken = true;

        if (fracturedParent != null)
        {
            fracturedParent.SetActive(true);
        }

        // Apply explosion force from the center of the object
        Vector3 explosionCenter = transform.position;

        foreach (var part in parts)
        {
            if (part.col != null)
            {
                part.col.enabled = true;
            }

            if (part.rb != null)
            {
                part.rb.isKinematic = false;
                part.rb.velocity = Vector3.zero;
                part.rb.angularVelocity = Vector3.zero;
                
                // Add a small randomized offset to the center so pieces blow outwards unevenly
                Vector3 randomizedCenter = explosionCenter + Random.insideUnitSphere * 0.1f;
                part.rb.AddExplosionForce(explosionForce, randomizedCenter, explosionRadius, upwardsModifier, ForceMode.Impulse);
            }
        }

        if (autoHideFractures)
        {
            if (hideCoroutine != null) StopCoroutine(hideCoroutine);
            hideCoroutine = StartCoroutine(HideFracturesAfterDelay(hideDelay));
        }
    }

    private System.Collections.IEnumerator HideFracturesAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (fracturedParent != null)
        {
            fracturedParent.SetActive(false);
        }
        hideCoroutine = null;
    }

    /// <summary>
    /// Repairs the object, snapping all pieces back to their original position and standing them solid again.
    /// Can be linked to the Pillar's OnRepair event.
    /// </summary>
    public void Repair()
    {
        if (!isBroken) return;
        isBroken = false;

        if (hideCoroutine != null)
        {
            StopCoroutine(hideCoroutine);
            hideCoroutine = null;
        }

        // Make the object visible again
        if (fracturedParent != null)
        {
            fracturedParent.SetActive(true);
        }

        foreach (var part in parts)
        {
            if (part.rb != null)
            {
                part.rb.isKinematic = true;
                part.rb.velocity = Vector3.zero;
                part.rb.angularVelocity = Vector3.zero;
            }

            if (part.col != null)
            {
                part.col.enabled = true;
            }

            // Snap back to original local position and rotation
            part.transform.localPosition = part.originalLocalPos;
            part.transform.localRotation = part.originalLocalRot;
        }
    }
}
