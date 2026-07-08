using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class PlayerInteract : MonoBehaviour
{
    [SerializeField] private InputReader inputReader;
    [SerializeField] private float interactRange = 3f;
    [SerializeField] private GameObject interactUI;
    [SerializeField] private GameObject freezeUI;

    private Ray ray;
    private RaycastHit hit;
    private Camera mainCamera;
    
    private bool isFrozen = false;
    private IInteractable currentInteractable = null;

    public bool IsLookingAtInteractable { get; private set; }

    private void Awake()
    {
        // Safety: Remove duplicate components on the same GameObject to prevent double-interactions
        PlayerInteract[] components = GetComponents<PlayerInteract>();
        for (int i = 1; i < components.Length; i++)
        {
            if (components[i] == this)
            {
                DestroyImmediate(this);
                return;
            }
        }
    }

    private void Start()
    {
        mainCamera = GetComponentInChildren<Camera>();
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        // Disable script if we are not the local player to prevent double-interactions from replicas
        if (!IsMine)
        {
            enabled = false;
        }
    }

    private void OnEnable()
    {
        if (inputReader == null)
        {
            FirstPersonController fpc = GetComponent<FirstPersonController>();
            if (fpc != null)
            {
                inputReader = fpc.InputReader;
            }
        }

        if (inputReader == null)
        {
            inputReader = Resources.Load<InputReader>("InputReader");
        }

        if (inputReader != null)
        {
            inputReader.OnInteract -= TryInteract; // Safety unsubscribe first
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

    private void Update()
    {
        if (!IsMine) return;

        if (isFrozen)
        {
            if (interactUI != null) interactUI.SetActive(false);
            if (freezeUI != null) freezeUI.SetActive(true);
            IsLookingAtInteractable = false;
            return;
        }

        if (freezeUI != null) freezeUI.SetActive(false);

        IInteractable targetInteractable = GetTargetInteractable();
        bool showUI = (targetInteractable != null);
        IsLookingAtInteractable = showUI;

        if (interactUI != null)
        {
            interactUI.SetActive(showUI);
        }
    }

    private IInteractable GetTargetInteractable()
    {
        if (mainCamera == null) return null;

        ray = new Ray(mainCamera.transform.position, mainCamera.transform.forward);
        RaycastHit[] hits = Physics.RaycastAll(ray, interactRange);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var candidateHit in hits)
        {
            // Ignore ourselves and our child colliders
            if (candidateHit.collider.gameObject == gameObject || candidateHit.collider.transform.IsChildOf(transform))
            {
                continue;
            }

            // Check if the object or its parent has an IInteractable component
            IInteractable interactable = candidateHit.collider.GetComponentInParent<IInteractable>();
            if (interactable != null)
            {
                hit = candidateHit; // Store hit for Gizmos drawing
                return interactable;
            }
        }

        return null;
    }

    public void TryInteract()
    {
        if (!IsMine) return;

        if (isFrozen)
        {
            if (currentInteractable != null)
            {
                // Set the static InteractingPlayer property before calling Interact
                FirstPersonController.InteractingPlayer = GetComponent<FirstPersonController>();
                currentInteractable.Interact();
            }
            return;
        }

        IInteractable targetInteractable = GetTargetInteractable();
        if (targetInteractable != null)
        {
            currentInteractable = targetInteractable;
            // Set the static InteractingPlayer property before calling Interact
            FirstPersonController.InteractingPlayer = GetComponent<FirstPersonController>();
            targetInteractable.Interact();
        }
    }

    public void FreezePlayer(bool freeze)
    {
        isFrozen = freeze;
        if (inputReader != null)
        {
            inputReader.SetInputsDisabledExceptInteract(freeze);
        }

        if (!freeze)
        {
            currentInteractable = null;
        }
    }

    public bool IsMine
    {
        get
        {
            FirstPersonController fpc = GetComponent<FirstPersonController>();
            return fpc != null && fpc.IsLocalPlayer;
        }
    }

    private void UnfreezePlayer()
    {
        FreezePlayer(false);
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
