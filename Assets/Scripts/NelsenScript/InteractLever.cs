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

    [Header("Lever Settings")]
    public bool WaitingForTeam = false;
    public bool LeverActivated = false;

    [Header("Lever References")]
    [SerializeField] private InteractLever SecondLever;
    
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
    }

    public void Deactivate()
    {
        LeverActivated = false;
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
}
