using UnityEngine;
using Photon.Pun;

[RequireComponent(typeof(FirstPersonController))]
public class PlayerCheckpointHandler : MonoBehaviour
{
    [Header("Tag Settings")]
    [Tooltip("The tag identifying checkpoints in the scene.")]
    [SerializeField] private string checkpointTag = "Checkpoint";

    [Tooltip("The tag identifying killzones or deadzones in the scene.")]
    [SerializeField] private string killZoneTag = "KillZone";

    private FirstPersonController playerController;

    private void Awake()
    {
        playerController = GetComponent<FirstPersonController>();
    }

    private void OnTriggerEnter(Collider other)
    {
        // Only execute checkpoint saving and death zones for the local player client
        if (PhotonNetwork.IsConnected && !playerController.photonView.IsMine) return;

        if (other != null && other.gameObject.name.Contains("Torch Collider"))
        {
            if (playerController.IsHoldingTorch && !playerController.IsPlacingTorch)
            {
                Debug.Log($"[PlayerCheckpointHandler] {name} entered Torch Collider. Triggering discard animation.");
                playerController.TriggerPlaceTorchAnimation(null);
            }
        }

        if (CompareSafeTag(other, checkpointTag))
        {
            Transform checkpointTransform = GetCheckpointTransform(other);
            if (checkpointTransform != null)
            {
                // Directly use the transform position of the checkpoint collider
                Vector3 position = checkpointTransform.position;
                playerController.SetCheckpoint(position);
            }
        }
        else if (CompareSafeTag(other, killZoneTag))
        {
            Debug.Log($"[PlayerCheckpointHandler] Respawn triggered via OnTriggerEnter with: {other.gameObject.name}, Tag: {other.gameObject.tag}");
            playerController.Respawn();
        }
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        // Only execute checkpoint saving and death zones for the local player client
        if (PhotonNetwork.IsConnected && !playerController.photonView.IsMine) return;

        if (CompareSafeTag(hit.collider, checkpointTag))
        {
            Transform checkpointTransform = GetCheckpointTransform(hit.collider);
            if (checkpointTransform != null)
            {
                // Directly use the transform position of the checkpoint collider
                Vector3 position = checkpointTransform.position;
                playerController.SetCheckpoint(position);
            }
        }
        else if (CompareSafeTag(hit.collider, killZoneTag))
        {
            Debug.Log($"[PlayerCheckpointHandler] Respawn triggered via OnControllerColliderHit with: {hit.collider.gameObject.name}, Tag: {hit.collider.gameObject.tag}");
            playerController.Respawn();
        }
    }

    private Transform GetCheckpointTransform(Collider col)
    {
        if (col == null) return null;
        if (col.CompareTag(checkpointTag))
        {
            return col.transform;
        }
        if (col.transform.parent != null && col.transform.parent.CompareTag(checkpointTag))
        {
            return col.transform.parent;
        }
        return null;
    }

    private bool CompareSafeTag(Collider col, string tag)
    {
        if (col == null || string.IsNullOrEmpty(tag)) return false;
        #if UNITY_EDITOR
        // Verify if tag is actually registered in the current editor session to prevent native console errors
        if (System.Array.IndexOf(UnityEditorInternal.InternalEditorUtility.tags, tag) < 0)
        {
            return false;
        }
        #endif
        try
        {
            if (col.CompareTag(tag)) return true;
            if (col.transform.parent != null && col.transform.parent.CompareTag(tag)) return true;
            return false;
        }
        catch
        {
            return false;
        }
    }
}
