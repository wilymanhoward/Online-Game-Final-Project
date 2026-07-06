using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

[RequireComponent(typeof(PhotonView))]
public class PlayerInteract : MonoBehaviourPun
{
    [SerializeField] private InputReader inputReader;
    [SerializeField] private float interactRange = 3f;

    private IInteractable currentInteractable;

    private Ray ray;
    private RaycastHit hit;
    
    private FirstPersonController playerController;
    private bool WaitingForTeam = false;
    private Camera mainCamera;

    private void Start()
    {
        // Only the local player's PlayerInteract should be active
        if (!photonView.IsMine)
        {
            enabled = false;
            return;
        }

        playerController = GetComponent<FirstPersonController>();
        mainCamera = Camera.main;

        if (inputReader != null)
        {
            inputReader.SetInputsDisabled(false);
            inputReader.SetInputsDisabledExceptLook(false);
            inputReader.SetInputsDisabledExceptInteract(false);
        }
    }

    private void OnEnable()
    {
        // Skip input registration for remote players
        if (!photonView.IsMine) return;

#if UNITY_EDITOR
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
            InputReader[] readers = Resources.FindObjectsOfTypeAll<InputReader>();
            if (readers != null && readers.Length > 0)
            {
                inputReader = readers[0];
            }
        }

        if (inputReader != null)
        {
            inputReader.OnInteract += TryInteract;
        }
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

        // Disable movement and look
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
        // Enable movement and look
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
