using UnityEngine;
using Photon.Pun;

[RequireComponent(typeof(PhotonView))]
public class TorchInteractable : MonoBehaviour, IInteractable
{
    private PhotonView pv;
    private bool isPickedUp = false;

    [Tooltip("Optional reference to the specific torch mesh/object to hide. If left unassigned, it will look for a child with 'Pickup' or 'Torch' in its name, or default to this GameObject.")]
    public GameObject torchObjectToHide;

    // Interface Properties
    public bool MultiplePeopleRequired { get; set; } = false;

    private void Start()
    {
        pv = GetComponent<PhotonView>();
        
        // Auto-detect torch object to hide if not set
        if (torchObjectToHide == null)
        {
            // 1. First look for children containing "Pickup"
            foreach (Transform child in transform)
            {
                if (child.name.IndexOf("Pickup", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    torchObjectToHide = child.gameObject;
                    break;
                }
            }

            // 2. If not found, look for children containing "Torch" but NOT named "Torch" (which is the hanger mesh)
            if (torchObjectToHide == null)
            {
                foreach (Transform child in transform)
                {
                    if (child.name.IndexOf("Torch", System.StringComparison.OrdinalIgnoreCase) >= 0 && 
                        child.name != "Torch")
                    {
                        torchObjectToHide = child.gameObject;
                        break;
                    }
                }
            }

            // 3. If still null, look for any child with a Collider component
            if (torchObjectToHide == null)
            {
                foreach (Transform child in transform)
                {
                    if (child.GetComponent<Collider>() != null)
                    {
                        torchObjectToHide = child.gameObject;
                        break;
                    }
                }
            }
            
            // 4. Default to this gameObject
            if (torchObjectToHide == null)
            {
                torchObjectToHide = gameObject;
            }
        }

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
            
            // Hide/deactivate locally instantly for immediate response
            PickupTorchLocal();
            
            // Sync deactivation of the scene torch across all other clients if Photon is running
            if (PhotonNetwork.IsConnected && pv != null && pv.ViewID > 0)
            {
                pv.RPC("PickupTorchRPC", RpcTarget.OthersBuffered);
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
        if (torchObjectToHide != null)
        {
            torchObjectToHide.SetActive(false);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    // Interface placeholder methods
    public void Activate() {}
    public void Deactivate() {}
    public void OnActivate() {}
    public void OnDeactivate() {}
}
