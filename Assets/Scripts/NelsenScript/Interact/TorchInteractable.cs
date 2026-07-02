using UnityEngine;
using Photon.Pun;

[RequireComponent(typeof(PhotonView))]
public class TorchInteractable : MonoBehaviour, IInteractable
{
    private PhotonView pv;
    private bool isPickedUp = false;

    // Interface Properties
    public bool MultiplePeopleRequired { get; set; } = false;

    private void Start()
    {
        pv = GetComponent<PhotonView>();
        
        // Ensure it has a collider so raycasts can hit it
        Collider col = GetComponent<Collider>();
        if (col == null)
        {
            gameObject.AddComponent<BoxCollider>();
        }
    }

    public void Interact()
    {
        if (isPickedUp) return;

        // Find the local player client triggering the interaction
        FirstPersonController localPlayer = null;
        var players = FindObjectsOfType<FirstPersonController>();
        foreach (var p in players)
        {
            if (!PhotonNetwork.IsConnected || p.photonView.IsMine)
            {
                localPlayer = p;
                break;
            }
        }

        if (localPlayer != null)
        {
            // Set player to hold torch
            localPlayer.SetHoldingTorch(true);
            
            // Sync deactivation of the scene torch across all clients
            if (PhotonNetwork.IsConnected)
            {
                pv.RPC("PickupTorchRPC", RpcTarget.AllBuffered);
            }
            else
            {
                PickupTorchLocal();
            }
        }
    }

    [PunRPC]
    private void PickupTorchRPC()
    {
        PickupTorchLocal();
    }

    private void PickupTorchLocal()
    {
        isPickedUp = true;
        gameObject.SetActive(false);
    }

    // Interface placeholder methods
    public void Activate() {}
    public void Deactivate() {}
    public void OnActivate() {}
    public void OnDeactivate() {}
}
