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
    
    private bool WaitingForTeam = false;
    private Camera mainCamera;

    private void Start()
    {
        mainCamera = Camera.main;
    }

    private void OnEnable()
    {
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

        ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out hit, interactRange))
        {
            if (hit.collider.TryGetComponent<IInteractable>(out IInteractable interactable))
            {
                currentInteractable = interactable;
                currentInteractable.Interact();
                
                if (interactable is InteractLever lever && lever.WaitingForTeam)
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

        if (currentInteractable is InteractLever lever)
        {
            lever.OnActivateEvent.AddListener(ExitWaitForTeam);
            lever.OnDeactivateEvent.AddListener(ExitWaitForTeam);
        }
    }

    private void ExitWaitForTeam(){
        WaitingForTeam = false;
        
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
