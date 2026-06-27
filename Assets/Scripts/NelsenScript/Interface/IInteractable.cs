using UnityEngine;

public interface IInteractable
{ 
    void Interact();
    void Activate();
    void Deactivate();
    void OnActivate();
    void OnDeactivate();
}