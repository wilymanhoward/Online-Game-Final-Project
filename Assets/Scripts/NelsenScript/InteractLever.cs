using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InteractLever : MonoBehaviour, IInteractable
{
    private bool LeverActivated = false;

    [SerializeField] private bool CheckLever = false;
    [SerializeField] private InteractLever SecondLever;
    
    public bool SecondLeverActivated { get; private set; } = false;

    public event Action OnWaitSecondLever;
    public event Action OnActivateEvent;
    public event Action OnDeactivateEvent;

    private bool lastSecondLeverState = false;

#region Monobehaviour
    
    private void Start()
    {   
        Deactivate();
    }

    private void Update()
    {
        if (!CheckLever) return;

        WaitSecondLever();
    }
    
#endregion

#region Interface functions

    public void Interact(){
        CheckLever = true;
    }

     public void Activate()
    {
        LeverActivated = true;
        SecondLeverActivated = true; // Updates state so the other lever can read it
        OnActivate();
        CheckLever = false;
    }

    public void Deactivate()
    {
        LeverActivated = false;
        SecondLeverActivated = false; // Updates state so the other lever can read it
        OnDeactivate();
        CheckLever = false;
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

#region Logic functions

    private void WaitSecondLever()
    {
        bool secondLeverActive = ListenToSecondLever();

        if (secondLeverActive && !lastSecondLeverState)
        {
            if (LeverActivated)
            {
                Deactivate();
            }
            else
            {
                Activate();
            }
            
            OnWaitSecondLever?.Invoke();
        }

        lastSecondLeverState = secondLeverActive;
    }

    private bool ListenToSecondLever()
    {
        if (SecondLever == null) return false;
        return SecondLever.SecondLeverActivated;
    }

#endregion

}
