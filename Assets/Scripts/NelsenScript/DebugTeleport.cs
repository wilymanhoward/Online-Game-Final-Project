using UnityEngine;
using Photon.Pun;

public class DebugTeleport : MonoBehaviourPun
{
    [System.Serializable]
    public struct TeleportConfig
    {
        public string label;
        public KeyCode key;
        public Transform teleportPoint;
    }

    [Header("Teleport Configurations")]
    [SerializeField] private TeleportConfig[] teleportConfigs;

    private void Update()
    {
        // Only allow teleporting the local player
        if (PhotonNetwork.IsConnected && photonView != null && !photonView.IsMine) return;

        if (teleportConfigs == null) return;

        foreach (var config in teleportConfigs)
        {
            if (Input.GetKeyDown(config.key))
            {
                TriggerTeleport(config);
                break;
            }
        }
    }

    private void TriggerTeleport(TeleportConfig config)
    {
        if (config.teleportPoint != null)
        {
            TeleportTo(config.teleportPoint);
        }
        else
        {
            Debug.LogWarning($"[DebugTeleport] No valid teleport point assigned for key {config.key}!");
        }
    }

    private void TeleportTo(Transform target)
    {
        Debug.Log($"[DebugTeleport] Teleporting to {target.name} at {target.position}");

        // Temporarily disable CharacterController to allow position changes to apply immediately
        CharacterController cc = GetComponent<CharacterController>();
        if (cc != null)
        {
            cc.enabled = false;
        }

        transform.position = target.position;
        transform.rotation = target.rotation;

        if (cc != null)
        {
            cc.enabled = true;
        }
    }
}
