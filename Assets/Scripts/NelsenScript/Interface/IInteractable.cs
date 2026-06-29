using UnityEngine;

public interface IInteractable
{ 
    public bool MultiplePeopleRequired {get;set;}
    public void Interact();
    public void Activate();
    public void Deactivate();
    public void OnActivate();
    public void OnDeactivate();
}