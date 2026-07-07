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
    private InteractLever currentLever = null;

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
            return;
        }

        if (freezeUI != null) freezeUI.SetActive(false);

        InteractLever targetLever = GetTargetLever();
        bool showUI = (targetLever != null);

        if (interactUI != null)
        {
            interactUI.SetActive(showUI);
        }
    }

    private InteractLever GetTargetLever()
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

            // Check tag or fallback to checking for component presence
            InteractLever lever = candidateHit.collider.GetComponentInParent<InteractLever>();
            if (lever != null && (candidateHit.collider.CompareTag("Lever") || candidateHit.collider.gameObject.name.Contains("Lever") || true))
            {
                hit = candidateHit; // Store hit for Gizmos drawing
                return lever;
            }
        }

        return null;
    }

    public void TryInteract()
    {
        if (!IsMine) return;

        if (isFrozen)
        {
            if (currentLever != null)
            {
                currentLever.Interact();
            }
            return;
        }

        InteractLever targetLever = GetTargetLever();
        if (targetLever != null)
        {
            currentLever = targetLever;
            targetLever.Interact();
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
            currentLever = null;
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
