using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Photon.Pun;

public class InteractLever : MonoBehaviour, IInteractable
{
    [Header("Lever Settings")]
    public bool LeverActivated = false;
    private bool isBothLeversActive = false;
    private PlayerInteract activeInteractor = null;

    [Header("Lever References")]
    [SerializeField] private InteractLever SecondLever;
    public InteractLever GetSecondLever => SecondLever;
    
    public bool MultiplePeopleRequired => SecondLever != null;
    
    [Header("Events")]
    public UnityEvent OnActivateEvent;
    public UnityEvent OnDeactivateEvent;
    public UnityEvent OnBothActivateEvent;
    public UnityEvent OnBothDeactivateEvent;

    public void Interact()
    {
        if (PhotonNetwork.IsConnected && PhotonNetwork.InRoom)
        {
            PhotonView pv = GetComponent<PhotonView>();
            if (pv != null && pv.ViewID > 0)
            {
                pv.RPC("InteractRPC", RpcTarget.All);
            }
            else
            {
                // Route through local player's PhotonView since the lever doesn't have one
                FirstPersonController localPlayer = FirstPersonController.InteractingPlayer;
                if (localPlayer == null)
                {
                    var players = FindObjectsOfType<FirstPersonController>();
                    foreach (var p in players)
                    {
                        if (p.IsLocalPlayer)
                        {
                            localPlayer = p;
                            break;
                        }
                    }
                }

                if (localPlayer != null)
                {
                    localPlayer.RouteLeverInteract(this);
                }
            }
        }
        else
        {
            InteractLocal();
        }
    }

    [PunRPC]
    private void InteractRPC(PhotonMessageInfo info)
    {
        PlayerInteract senderInteract = null;
        if (info.Sender != null)
        {
            var players = FindObjectsOfType<PlayerInteract>();
            foreach (var pi in players)
            {
                var pv = pi.GetComponent<PhotonView>();
                if (pv != null && pv.Owner == info.Sender)
                {
                    senderInteract = pi;
                    break;
                }
            }
        }
        InteractLocal(senderInteract);
    }

    public void InteractLocal(PlayerInteract interactor = null)
    {
        if (interactor == null)
        {
            // Fallback: Find the local player
            FirstPersonController fpc = FirstPersonController.InteractingPlayer;
            if (fpc == null)
            {
                var controllers = FindObjectsOfType<FirstPersonController>();
                foreach (var c in controllers)
                {
                    if (c.IsLocalPlayer)
                    {
                        fpc = c;
                        break;
                    }
                }
            }
            if (fpc != null)
            {
                interactor = fpc.GetComponent<PlayerInteract>();
            }
        }

        if (LeverActivated)
        {
            Deactivate(interactor);
        }
        else
        {
            Activate(interactor);
        }
    }

    public void Activate(PlayerInteract interactor)
    {
        if (LeverActivated) return;
        LeverActivated = true;

        activeInteractor = interactor;

        if (activeInteractor != null && activeInteractor.IsMine)
        {
            if (MultiplePeopleRequired)
            {
                activeInteractor.FreezePlayer(true);
            }
        }

        OnActivateEvent?.Invoke();

        if (SecondLever != null)
        {
            if (SecondLever.LeverActivated)
            {
                if (!isBothLeversActive)
                {
                    ActivateBothLevers();
                }
            }
        }
    }

    public void Deactivate(PlayerInteract interactor)
    {
        if (!LeverActivated) return;
        LeverActivated = false;

        if (activeInteractor != null && activeInteractor.IsMine)
        {
            activeInteractor.FreezePlayer(false);
        }
        activeInteractor = null;

        OnDeactivateEvent?.Invoke();

        if (SecondLever != null)
        {
            if (isBothLeversActive)
            {
                DeactivateBothLevers();
            }
        }
    }

    private void ActivateBothLevers()
    {
        PhotonView pv = GetComponent<PhotonView>();
        if (PhotonNetwork.IsConnected && PhotonNetwork.InRoom)
        {
            if (pv != null && pv.ViewID > 0)
            {
                pv.RPC("SyncBothLeversRPC", RpcTarget.All, true);
            }
            else
            {
                FirstPersonController localPlayer = FirstPersonController.InteractingPlayer;
                if (localPlayer == null)
                {
                    var players = FindObjectsOfType<FirstPersonController>();
                    foreach (var p in players)
                    {
                        if (p.IsLocalPlayer)
                        {
                            localPlayer = p;
                            break;
                        }
                    }
                }
                if (localPlayer != null)
                {
                    localPlayer.RouteBothLevers(this, true);
                }
            }
        }
        else
        {
            ActivateBothLeversLocal();
        }
    }

    public void DeactivateBothLevers()
    {
        PhotonView pv = GetComponent<PhotonView>();
        if (PhotonNetwork.IsConnected && PhotonNetwork.InRoom)
        {
            if (pv != null && pv.ViewID > 0)
            {
                pv.RPC("SyncBothLeversRPC", RpcTarget.All, false);
            }
            else
            {
                FirstPersonController localPlayer = FirstPersonController.InteractingPlayer;
                if (localPlayer == null)
                {
                    var players = FindObjectsOfType<FirstPersonController>();
                    foreach (var p in players)
                    {
                        if (p.IsLocalPlayer)
                        {
                            localPlayer = p;
                            break;
                        }
                    }
                }
                if (localPlayer != null)
                {
                    localPlayer.RouteBothLevers(this, false);
                }
            }
        }
        else
        {
            DeactivateBothLeversLocal();
        }
    }

    [PunRPC]
    private void SyncBothLeversRPC(bool active)
    {
        if (active)
        {
            ActivateBothLeversLocal();
        }
        else
        {
            DeactivateBothLeversLocal();
        }
    }

    public void ActivateBothLeversLocal()
    {
        isBothLeversActive = true;

        if (activeInteractor != null)
        {
            if (activeInteractor.IsMine)
            {
                activeInteractor.FreezePlayer(false);
            }
            activeInteractor = null;
        }

        OnBothActivateEvent?.Invoke();

        if (SecondLever != null)
        {
            SecondLever.isBothLeversActive = true;

            if (SecondLever.activeInteractor != null)
            {
                if (SecondLever.activeInteractor.IsMine)
                {
                    SecondLever.activeInteractor.FreezePlayer(false);
                }
                SecondLever.activeInteractor = null;
            }

            SecondLever.OnBothActivateEvent?.Invoke();
        }
    }

    public void DeactivateBothLeversLocal()
    {
        isBothLeversActive = false;
        OnBothDeactivateEvent?.Invoke();

        if (SecondLever != null)
        {
            SecondLever.isBothLeversActive = false;
            SecondLever.OnBothDeactivateEvent?.Invoke();
        }
    }

    public void ResetBothLevers()
    {
        PhotonView pv = GetComponent<PhotonView>();
        if (PhotonNetwork.IsConnected && PhotonNetwork.InRoom)
        {
            if (pv != null && pv.ViewID > 0)
            {
                pv.RPC("SyncResetLeversRPC", RpcTarget.All);
            }
            else
            {
                FirstPersonController localPlayer = FirstPersonController.InteractingPlayer;
                if (localPlayer == null)
                {
                    var players = FindObjectsOfType<FirstPersonController>();
                    foreach (var p in players)
                    {
                        if (p.IsLocalPlayer)
                        {
                            localPlayer = p;
                            break;
                        }
                    }
                }
                if (localPlayer != null)
                {
                    localPlayer.RouteResetLevers(this);
                }
            }
        }
        else
        {
            ResetBothLeversLocal();
        }
    }

    [PunRPC]
    private void SyncResetLeversRPC()
    {
        ResetBothLeversLocal();
    }

    /// <summary>
    /// Fully resets both levers back to their initial unactivated states,
    /// firing deactivation events and unfreezing any active interactors.
    /// </summary>
    public void ResetBothLeversLocal()
    {
        // Reset this lever
        LeverActivated = false;
        isBothLeversActive = false;
        if (activeInteractor != null)
        {
            if (activeInteractor.IsMine) activeInteractor.FreezePlayer(false);
            activeInteractor = null;
        }
        OnDeactivateEvent?.Invoke();
        OnBothDeactivateEvent?.Invoke();
        
        // Reset the second lever
        if (SecondLever != null)
        {
            SecondLever.LeverActivated = false;
            SecondLever.isBothLeversActive = false;
            if (SecondLever.activeInteractor != null)
            {
                if (SecondLever.activeInteractor.IsMine) SecondLever.activeInteractor.FreezePlayer(false);
                SecondLever.activeInteractor = null;
            }
            SecondLever.OnDeactivateEvent?.Invoke();
            SecondLever.OnBothDeactivateEvent?.Invoke();
        }
    }
}
