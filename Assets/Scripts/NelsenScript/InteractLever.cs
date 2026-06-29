using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Photon.Pun;

public class InteractLever : MonoBehaviour, IInteractable
{
    [SerializeField] private bool multiplePeopleRequired = true;
    public bool MultiplePeopleRequired
    {
        get => multiplePeopleRequired;
        set => multiplePeopleRequired = value;
    }

    public bool CloseOverTime = false;
    [SerializeField] private float closeDelay = 3f;

    [Header("Lever Settings")]
    public bool WaitingForTeam = false;
    public bool LeverActivated = false;

    private Coroutine closeCoroutine;

    [Header("Lever References")]
    [SerializeField] private InteractLever SecondLever;
    
    public UnityEvent OnCloseOvertimeEvent;
    public UnityEvent OnActivateEvent;
    public UnityEvent OnDeactivateEvent;

    private void Start()
    {   
        Deactivate();
    }

#region Interface functions

    public void Interact()
    {
        if (PhotonNetwork.IsConnected)
        {
            PhotonView pv = GetComponent<PhotonView>();
            if (pv != null)
            {
                pv.RPC("InteractRPC", RpcTarget.All);
                return;
            }
        }
        InteractLocal();
    }

    [PunRPC]
    private void InteractRPC()
    {
        InteractLocal();
    }

    private void InteractLocal()
    {     
        if (!MultiplePeopleRequired)
        {
            if (LeverActivated)
            {
                Deactivate();
            }
            else
            {
                Activate();
            }
            return;
        }
        
        if (WaitingForTeam)
        {
            WaitingForTeam = false;
        }
        else
        {
            WaitingForTeam = true;
        }

        if (SecondLever != null && WaitingForTeam && SecondLever.WaitingForTeam)
        {
            WaitingForTeam = false;
            SecondLever.WaitingForTeam = false;
            Activate();
            SecondLever.Activate();
        }
    }

    public void Activate()
    {
        LeverActivated = true;
        OnActivate();
        OnCloseOverTime();
    }

    public void Deactivate()
    {
        LeverActivated = false;
        if (closeCoroutine != null)
        {
            StopCoroutine(closeCoroutine);
            closeCoroutine = null;
        }
        OnDeactivate();
    }

    public void OnActivate()
    {
        // Execute Active Lever Events
        OnActivateEvent?.Invoke();
    }

    public void OnDeactivate()
    {
        // Execute Deactivate Lever Events
        OnDeactivateEvent?.Invoke();
    }

#endregion

    private void OnCloseOverTime()
    {
        if (LeverActivated && CloseOverTime)
        {
            if (closeCoroutine != null)
            {
                StopCoroutine(closeCoroutine);
            }
            closeCoroutine = StartCoroutine(CloseAfterDelay(closeDelay));
        }
    }

    private IEnumerator CloseAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        Deactivate();
        OnCloseOvertimeEvent?.Invoke();
    }
}
