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
            if (isPickedUp)
            {
                // Wall torch is empty, and player is holding a torch -> Put it back!
                if (localPlayer.IsHoldingTorch)
                {
                    // Trigger the place animation, and show the wall torch at the release point (0.3 seconds in)
                    localPlayer.TriggerPlaceTorchAnimation(() => {
                        // Set wall torch active state locally instantly
                        SetTorchStateLocal(false);
                        
                        // Sync placement back across network
                        if (PhotonNetwork.IsConnected && pv != null && pv.ViewID > 0)
                        {
                            pv.RPC("SetTorchStateRPC", RpcTarget.OthersBuffered, false);
                        }
                    });
                }
            }
            else
            {
                // Wall torch is present, and player is NOT holding a torch -> Pick it up!
                if (!localPlayer.IsHoldingTorch)
                {
                    localPlayer.SetHoldingTorch(true);
                    
                    // Set wall torch active state locally instantly
                    SetTorchStateLocal(true);
                    
                    // Sync pickup across network
                    if (PhotonNetwork.IsConnected && pv != null && pv.ViewID > 0)
                    {
                        pv.RPC("SetTorchStateRPC", RpcTarget.OthersBuffered, true);
                    }
                }
            }
        }
    }

    [PunRPC]
    private void SetTorchStateRPC(bool pickedUp)
    {
        SetTorchStateLocal(pickedUp);
    }

    private void SetTorchStateLocal(bool pickedUp)
    {
        isPickedUp = pickedUp;
        
        GameObject target = torchObjectToHide != null ? torchObjectToHide : gameObject;
        
        // Hide/show the mesh renderer of the target
        var mr = target.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            mr.enabled = !pickedUp;
        }

        // Hide/show all child objects of the target (like FX_Fire_01 and TorchLight)
        foreach (Transform child in target.transform)
        {
            child.gameObject.SetActive(!pickedUp);
        }
    }

    // Interface placeholder methods
    public void Activate() {}
    public void Deactivate() {}
    public void OnActivate() {}
    public void OnDeactivate() {}
}
