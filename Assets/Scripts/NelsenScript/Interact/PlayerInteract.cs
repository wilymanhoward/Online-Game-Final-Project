using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

[RequireComponent(typeof(PhotonView))]
public class PlayerInteract : MonoBehaviourPun, IPunOwnershipCallbacks
{
    [SerializeField] private InputReader inputReader;
    [SerializeField] private float interactRange = 3f;

    private IInteractable currentInteractable;

    private Ray ray;
    private RaycastHit hit;
    
    private FirstPersonController playerController;
    private bool WaitingForTeam = false;
    private Camera mainCamera;

    private void Awake()
    {
        // Register for ownership callbacks manually so they keep firing even when
        // this component is disabled (which it will be for non-owners waiting on transfer).
        // We intentionally use Awake/OnDestroy instead of OnEnable/OnDisable so the
        // registration outlives the component's enabled state.
        PhotonNetwork.AddCallbackTarget(this);
    }

    private void Start()
    {
        playerController = GetComponent<FirstPersonController>();
        mainCamera = Camera.main;

        if (photonView.IsMine)
        {
            // We already own this player — activate immediately.
            ActivateForLocalPlayer();
        }
        else
        {
            // Ownership has not been transferred yet (common for the guest player whose
            // RequestOwnership() call is async). OnOwnershipTransferred will activate us
            // once Photon confirms the transfer.
            enabled = false;
        }
    }

    private void ActivateForLocalPlayer()
    {
        if (playerController == null)
            playerController = GetComponent<FirstPersonController>();

        if (mainCamera == null)
            mainCamera = Camera.main;

        if (inputReader != null)
        {
            inputReader.SetInputsDisabled(false);
            inputReader.SetInputsDisabledExceptLook(false);
            inputReader.SetInputsDisabledExceptInteract(false);
        }
    }

    // ---- IPunOwnershipCallbacks ------------------------------------------------
    // Registered in Awake() / removed in OnDestroy() so these fire even when
    // this component is disabled — which is intentional for the guest player.

    public void OnOwnershipRequest(PhotonView targetView, Player requestingPlayer) { }

    // NOTE: PUN2 has a typo in their interface — it is "Transfered" with one 'r'
    public void OnOwnershipTransfered(PhotonView targetView, Player previousOwner)
    {
        // Only handle ownership changes for THIS player object
        if (targetView != photonView) return;

        if (photonView.IsMine)
        {
            enabled = true; // triggers OnEnable → subscribes TryInteract
            ActivateForLocalPlayer();
        }
        else
        {
            enabled = false; // triggers OnDisable → unsubscribes TryInteract
        }
    }

    public void OnOwnershipTransferFailed(PhotonView targetView, Player senderOfFailedRequest) { }

    private void OnDestroy()
    {
        PhotonNetwork.RemoveCallbackTarget(this);
    }

    private void OnEnable()
    {
        // 1. If not assigned in Inspector, try loading from a Resources folder (works in builds)
        if (inputReader == null)
        {
            inputReader = Resources.Load<InputReader>("InputReader");
        }

#if UNITY_EDITOR
        // 2. Editor-only fallback: search entire AssetDatabase
        if (inputReader == null)
        {
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:InputReader");
            if (guids != null && guids.Length > 0)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                inputReader = UnityEditor.AssetDatabase.LoadAssetAtPath<InputReader>(path);
            }
        }
#endif

        if (inputReader == null)
        {
            Debug.LogError("[PlayerInteract] InputReader is null! Assign it in the Inspector on the Player prefab, or place the InputReader asset inside a 'Resources/' folder named 'InputReader'.", this);
            return;
        }

        inputReader.OnInteract += TryInteract;
    }

    private void OnDisable()
    {
        if (inputReader != null)
        {
            inputReader.OnInteract -= TryInteract;
        }
    }

    public void TryInteract()
    {
        // Only the local player can trigger interactions
        if (!photonView.IsMine) return;

        if (playerController == null)
        {
            playerController = GetComponent<FirstPersonController>();
        }

        FirstPersonController.InteractingPlayer = playerController;

        if (WaitingForTeam)
        {
            if (currentInteractable != null)
            {
                currentInteractable.Interact();
            }
            ExitWaitForTeam();
            return;
        }

        // 1. Raycast-based interaction (aiming directly at an object takes highest priority)
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        if (mainCamera != null)
        {
            ray = new Ray(mainCamera.transform.position, mainCamera.transform.forward);
            if (Physics.Raycast(ray, out hit, interactRange))
            {
                IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();
                if (interactable != null)
                {
                    // If it is an InteractLever and someone is already waiting on it, block interaction
                    if (interactable is InteractLever lever && lever.WaitingForTeam && !WaitingForTeam)
                    {
                        Debug.Log("Lever is already occupied by another player.");
                        return;
                    }

                    currentInteractable = interactable;

                    bool shouldWait = false;
                    if (interactable is InteractLever activeLever && activeLever.MultiplePeopleRequired && !activeLever.LeverActivated)
                    {
                        // If the second lever exists and is already waiting, this interaction will trigger both to activate,
                        // so we do not need to wait. Otherwise, we must wait.
                        if (activeLever.GetSecondLever == null || !activeLever.GetSecondLever.WaitingForTeam)
                        {
                            shouldWait = true;
                        }
                    }

                    currentInteractable.Interact();
                    
                    if (shouldWait)
                    {
                        WaitForTeam();
                    }
                    return;
                }
            }
        }

        // 2. Proximity-based interaction fallback for Torches (stand close and press E without aiming)
        TorchInteractable closestTorch = null;
        float proximityRange = 4.5f; // Generous range for comfortable proximity check
        float minDistance = proximityRange;
        var torches = FindObjectsOfType<TorchInteractable>();
        foreach (var torch in torches)
        {
            if (torch != null)
            {
                // Only consider this torch if it can actually be interacted with:
                // - Either it is on the wall (not picked up), OR
                // - It is picked up and player is holding a torch (to put it back)
                bool canInteract = !torch.IsPickedUp || (playerController != null && playerController.IsHoldingTorch);
                if (!canInteract) continue;

                // Calculate distance on the XZ plane to ignore the vertical offset of wall-mounted torches
                Vector3 playerPosXZ = new Vector3(transform.position.x, 0f, transform.position.z);
                Vector3 torchPosXZ  = new Vector3(torch.transform.position.x, 0f, torch.transform.position.z);
                float dist = Vector3.Distance(playerPosXZ, torchPosXZ);
                
                if (dist < minDistance)
                {
                    minDistance = dist;
                    closestTorch = torch;
                }
            }
        }

        if (closestTorch != null)
        {
            closestTorch.Interact();
            return;
        }
    }

    private void WaitForTeam()
    {
        WaitingForTeam = true;
        Debug.Log("WaitingForTeam");

        if (inputReader != null)
        {
            inputReader.SetInputsDisabledExceptInteract(true);
        }

        if (currentInteractable is InteractLever lever)
        {
            lever.OnActivateEvent.AddListener(ExitWaitForTeam);
            lever.OnDeactivateEvent.AddListener(ExitWaitForTeam);
        }
    }

    private void ExitWaitForTeam()
    {
        WaitingForTeam = false;
        
        if (inputReader != null)
        {
            inputReader.SetInputsDisabledExceptInteract(false);
        }

        if (currentInteractable is InteractLever lever)
        {
            lever.OnActivateEvent.RemoveListener(ExitWaitForTeam);
            lever.OnDeactivateEvent.RemoveListener(ExitWaitForTeam);
        }

        currentInteractable = null;
        Debug.Log("ExitWaitForTeam");
    }

    // ---- Network Router for Static Scene Objects without PhotonView ----
    
    public void RouteLeverInteract(InteractLever lever)
    {
        if (photonView.IsMine && PhotonNetwork.IsConnected)
        {
            photonView.RPC("SyncLeverInteractRPC", RpcTarget.All, GetGameObjectPath(lever.gameObject));
        }
    }

    public void RouteTriggerEnter(InteractWhenCrossed trigger, string playerObjectName)
    {
        if (photonView.IsMine && PhotonNetwork.IsConnected)
        {
            photonView.RPC("SyncTriggerEnterRPC", RpcTarget.All, GetGameObjectPath(trigger.gameObject), playerObjectName);
        }
    }

    public void RouteTriggerExit(InteractWhenCrossed trigger, string playerObjectName)
    {
        if (photonView.IsMine && PhotonNetwork.IsConnected)
        {
            photonView.RPC("SyncTriggerExitRPC", RpcTarget.All, GetGameObjectPath(trigger.gameObject), playerObjectName);
        }
    }

    [PunRPC]
    private void SyncLeverInteractRPC(string path)
    {
        GameObject go = GameObject.Find(path);
        if (go != null)
        {
            InteractLever lever = go.GetComponent<InteractLever>();
            if (lever != null)
            {
                lever.InteractLocal();
            }
        }
    }

    [PunRPC]
    private void SyncTriggerEnterRPC(string path, string playerObjectName)
    {
        GameObject triggerGO = GameObject.Find(path);
        GameObject playerGO = GameObject.Find(playerObjectName);
        if (triggerGO != null && playerGO != null)
        {
            InteractWhenCrossed trigger = triggerGO.GetComponent<InteractWhenCrossed>();
            if (trigger != null)
            {
                trigger.OnTriggerEnterLocal(playerGO);
            }
        }
    }

    [PunRPC]
    private void SyncTriggerExitRPC(string path, string playerObjectName)
    {
        GameObject triggerGO = GameObject.Find(path);
        GameObject playerGO = GameObject.Find(playerObjectName);
        if (triggerGO != null && playerGO != null)
        {
            InteractWhenCrossed trigger = triggerGO.GetComponent<InteractWhenCrossed>();
            if (trigger != null)
            {
                trigger.OnTriggerExitLocal(playerGO);
            }
        }
    }

    private string GetGameObjectPath(GameObject obj)
    {
        string path = obj.name;
        while (obj.transform.parent != null)
        {
            obj = obj.transform.parent.gameObject;
            path = obj.name + "/" + path;
        }
        return path;
    }

    private void OnDrawGizmos()
    {
        if (ray.direction != Vector3.zero)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(ray.origin, ray.origin + ray.direction * interactRange);
            
            if (hit.collider != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawSphere(hit.point, 0.1f);
            }
        }
    }
}
