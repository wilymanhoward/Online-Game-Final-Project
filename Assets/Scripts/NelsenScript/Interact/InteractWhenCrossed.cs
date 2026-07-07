using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Photon.Pun;

public class InteractWhenCrossed : MonoBehaviour
{
    [System.Serializable]
    public class PlayerCrossedEvent : UnityEvent<GameObject> {}

    [Header("Trigger Settings")]
    [SerializeField] private bool triggerOnce = false;
    [Range(1, 2)][SerializeField] private int requiredPlayers = 1;
    public bool MultiplePeopleRequired => requiredPlayers == 2;

    [Header("Trigger References")]
    [SerializeField] private GameObject enterZone;
    [SerializeField] private GameObject exitZone;
    [SerializeField] private InteractWhenCrossed secondTrigger;

    [Header("Events")]
    [SerializeField] private PlayerCrossedEvent onCross;
    [SerializeField] private PlayerCrossedEvent onExit;
    [SerializeField] private PlayerCrossedEvent onBothCross;
    [SerializeField] private PlayerCrossedEvent onBothExit;

    private bool isActivated = false;
    private bool isOnePlayerActive = false;
    private bool isBothPlayersActive = false;
    private bool hasTriggeredOnePlayer = false;
    private bool hasTriggeredBothPlayers = false;

    private List<GameObject> playersInside = new List<GameObject>();

    public bool IsActivated => isActivated;
    public bool IsPlayerEntered => playersInside.Count > 0;

    private void Start()
    {
        Collider ownCollider = GetComponent<Collider>();
        if (ownCollider != null)
        {
            ownCollider.enabled = false;
        }

        if (enterZone != null)
        {
            TriggerZoneHelper helper = enterZone.GetComponent<TriggerZoneHelper>();
            if (helper == null) helper = enterZone.AddComponent<TriggerZoneHelper>();
            helper.onTriggerEnterAction = OnEnterZoneTrigger;
            
            // Fallback for single-trigger setup (e.g. standard pressure plates)
            if (exitZone == null)
            {
                helper.onTriggerExitAction = OnExitZoneTrigger;
            }
        }
        else
        {
            Debug.LogWarning($"[InteractWhenCrossed] {gameObject.name} Enter Zone is not assigned!", this);
        }

        if (exitZone != null)
        {
            TriggerZoneHelper helper = exitZone.GetComponent<TriggerZoneHelper>();
            if (helper == null) helper = exitZone.AddComponent<TriggerZoneHelper>();
            helper.onTriggerEnterAction = OnExitZoneTrigger;
        }
    }

    private void OnEnterZoneTrigger(GameObject playerGO)
    {
        PhotonView pv = GetComponent<PhotonView>();
        if (PhotonNetwork.IsConnected && PhotonNetwork.InRoom)
        {
            if (pv != null && pv.ViewID > 0)
            {
                pv.RPC("SyncTriggerCrossRPC", RpcTarget.All, playerGO.name, true);
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
                    localPlayer.RouteTriggerCross(this, playerGO, true);
                }
            }
        }
        else
        {
            OnEnterZoneTriggerLocal(playerGO);
        }
    }

    private void OnExitZoneTrigger(GameObject playerGO)
    {
        PhotonView pv = GetComponent<PhotonView>();
        if (PhotonNetwork.IsConnected && PhotonNetwork.InRoom)
        {
            if (pv != null && pv.ViewID > 0)
            {
                pv.RPC("SyncTriggerCrossRPC", RpcTarget.All, playerGO.name, false);
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
                    localPlayer.RouteTriggerCross(this, playerGO, false);
                }
            }
        }
        else
        {
            OnExitZoneTriggerLocal(playerGO);
        }
    }

    [PunRPC]
    private void SyncTriggerCrossRPC(string playerName, bool enter)
    {
        GameObject playerGO = GameObject.Find(playerName);
        if (playerGO != null)
        {
            if (enter)
            {
                OnEnterZoneTriggerLocal(playerGO);
            }
            else
            {
                OnExitZoneTriggerLocal(playerGO);
            }
        }
    }

    public void OnEnterZoneTriggerLocal(GameObject player)
    {
        if (!playersInside.Contains(player))
        {
            playersInside.Add(player);
            Debug.Log($"[InteractWhenCrossed] {gameObject.name} Enter Zone triggered by {player.name}. Count: {playersInside.Count}/{requiredPlayers}");
        }

        // 1. Single plate activation condition met
        if (playersInside.Count >= requiredPlayers)
        {
            if (!isOnePlayerActive)
            {
                isOnePlayerActive = true;
                isActivated = true;
                if (!triggerOnce || !hasTriggeredOnePlayer)
                {
                    hasTriggeredOnePlayer = true;
                    onCross?.Invoke(player);
                }
            }

            // 2. Both plates activation condition met (if secondTrigger is assigned)
            if (secondTrigger != null)
            {
                // Check if second trigger also has enough players to be activated
                if (secondTrigger.playersInside.Count >= secondTrigger.requiredPlayers)
                {
                    if (!isBothPlayersActive)
                    {
                        if (!triggerOnce || !hasTriggeredBothPlayers)
                        {
                            ActivateBoth(player);
                        }
                    }
                }
            }
        }
    }

    public void OnExitZoneTriggerLocal(GameObject player)
    {
        if (playersInside.Contains(player))
        {
            playersInside.Remove(player);
            Debug.Log($"[InteractWhenCrossed] {gameObject.name} Exit Zone triggered by {player.name}. Count: {playersInside.Count}/{requiredPlayers}");
        }

        // 1. Single plate deactivation condition met
        if (playersInside.Count < requiredPlayers)
        {
            if (isOnePlayerActive)
            {
                isOnePlayerActive = false;
                isActivated = false;
                onExit?.Invoke(player);
            }

            // 2. Both plates deactivation condition met (if secondTrigger is assigned)
            if (secondTrigger != null)
            {
                if (isBothPlayersActive)
                {
                    DeactivateBoth(player);
                }
            }
        }
    }

    private void ActivateBoth(GameObject player)
    {
        isBothPlayersActive = true;
        hasTriggeredBothPlayers = true;
        isActivated = true;
        onBothCross?.Invoke(player);

        if (secondTrigger != null)
        {
            secondTrigger.isBothPlayersActive = true;
            secondTrigger.hasTriggeredBothPlayers = true;
            secondTrigger.isActivated = true;
            secondTrigger.onBothCross?.Invoke(player);
        }
    }

    private void DeactivateBoth(GameObject player)
    {
        isBothPlayersActive = false;
        isActivated = false;
        onBothExit?.Invoke(player);

        if (secondTrigger != null)
        {
            secondTrigger.isBothPlayersActive = false;
            secondTrigger.isActivated = false;
            secondTrigger.onBothExit?.Invoke(player);
        }
    }

    // Kept for backward compatibility with old RPCs in PlayerInteract.cs
    public void OnTriggerEnterLocal(GameObject player)
    {
        OnEnterZoneTriggerLocal(player);
    }

    public void OnTriggerExitLocal(GameObject player)
    {
        OnExitZoneTriggerLocal(player);
    }

    public void ResetTriggerState()
    {
        isActivated = false;
        isOnePlayerActive = false;
        isBothPlayersActive = false;
        hasTriggeredOnePlayer = false;
        hasTriggeredBothPlayers = false;
        playersInside.Clear();
    }
}

public class TriggerZoneHelper : MonoBehaviour
{
    public System.Action<GameObject> onTriggerEnterAction;
    public System.Action<GameObject> onTriggerExitAction;

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            onTriggerEnterAction?.Invoke(other.gameObject);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            onTriggerExitAction?.Invoke(other.gameObject);
        }
    }
}
