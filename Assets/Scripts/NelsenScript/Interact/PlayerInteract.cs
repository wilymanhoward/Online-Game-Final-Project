using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerInteract : MonoBehaviour
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
        mainCamera = Camera.main;
        playerController = GetComponent<FirstPersonController>();
        if (inputReader != null)
        {
            inputReader.SetInputsDisabled(false);
            inputReader.SetInputsDisabledExceptLook(false);
            inputReader.SetInputsDisabledExceptInteract(false);
        }
    }

    private void OnEnable()
    {
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
        if (WaitingForTeam)
        {
            if (currentInteractable != null)
            {
                currentInteractable.Interact();
            }
            ExitWaitForTeam();
            return;
        }

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null) return;
        }

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
            }
        }
    }

    private void WaitForTeam(){
        WaitingForTeam = true;
        Debug.Log("WaitingForTeam");
        //Disable movement and look
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

    private void ExitWaitForTeam(){
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
        //Enable movement and look
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
