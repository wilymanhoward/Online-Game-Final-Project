using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Photon.Pun;

public class BossGame : MonoBehaviourPun, IGames
{
    [Header("Arena Settings")]
    [Tooltip("The trigger collider defining the boundaries of the boss arena.")]
    [SerializeField] private Collider arenaCollider;

    [Tooltip("The spawn/checkpoint location where players respawn during the boss fight.")]
    [SerializeField] private Transform spawnPoint;

    [Header("Events")]
    [SerializeField] private UnityEvent OnGameStartEvent = new UnityEvent();
    [SerializeField] private UnityEvent OnGameWonEvent = new UnityEvent();
    [SerializeField] private UnityEvent OnGameLostEvent = new UnityEvent();

    private int pillarsBrokenCount = 0;
    private bool gameStarted = false;

    private List<FirstPersonController> playersInArena = new List<FirstPersonController>();
    private Dictionary<FirstPersonController, Vector3> playerPreviousCheckpoints = new Dictionary<FirstPersonController, Vector3>();

    public bool IsGameActive => gameStarted;
    public int PillarsBrokenCount => pillarsBrokenCount;

    private void Update()
    {
        if (!gameStarted) return;

        // Clean up disconnected players
        playersInArena.RemoveAll(p => p == null);

        if (playersInArena.Count > 0)
        {
            bool allDead = true;
            foreach (var player in playersInArena)
            {
                if (!player.IsDead)
                {
                    allDead = false;
                    break;
                }
            }

            if (allDead)
            {
                // Trigger loss on master client or locally if single player to avoid double calls
                if (!PhotonNetwork.IsConnected || PhotonNetwork.IsMasterClient)
                {
                    LoseGame();
                }
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!gameStarted) return;

        FirstPersonController player = other.GetComponent<FirstPersonController>();
        if (player != null)
        {
            if (!playersInArena.Contains(player))
            {
                playersInArena.Add(player);

                // Store previous checkpoint if not already stored
                if (!playerPreviousCheckpoints.ContainsKey(player))
                {
                    playerPreviousCheckpoints[player] = player.ActiveCheckpointPosition;
                }

                // Set checkpoint to arena spawn point
                player.SetCheckpoint(spawnPoint.position);
                Debug.Log($"[BossGame] Player {player.name} entered arena. Checkpoint set to spawn point.");
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!gameStarted) return;

        FirstPersonController player = other.GetComponent<FirstPersonController>();
        if (player != null)
        {
            if (playersInArena.Contains(player))
            {
                playersInArena.Remove(player);
                Debug.Log($"[BossGame] Player {player.name} left arena.");
            }
        }
    }

    private void InitializePlayersInArena()
    {
        if (arenaCollider == null) return;

        var players = FindObjectsOfType<FirstPersonController>();
        foreach (var player in players)
        {
            if (arenaCollider.bounds.Contains(player.transform.position))
            {
                if (!playersInArena.Contains(player))
                {
                    playersInArena.Add(player);
                    if (!playerPreviousCheckpoints.ContainsKey(player))
                    {
                        playerPreviousCheckpoints[player] = player.ActiveCheckpointPosition;
                    }
                    player.SetCheckpoint(spawnPoint.position);
                    Debug.Log($"[BossGame] Player {player.name} initialized in arena. Checkpoint set to spawn point.");
                }
            }
        }
    }

    #region IGames Implementation

    public void StartGame()
    {
        if (PhotonNetwork.IsConnected && PhotonNetwork.InRoom)
        {
            photonView.RPC("StartGameRPC", RpcTarget.All);
        }
        else
        {
            StartGameLocal();
        }
    }

    [PunRPC]
    private void StartGameRPC()
    {
        StartGameLocal();
    }

    private void StartGameLocal()
    {
        gameStarted = true;
        pillarsBrokenCount = 0;
        playerPreviousCheckpoints.Clear();
        playersInArena.Clear();

        InitializePlayersInArena();

        Debug.Log("[BossGame] Game Started!");
        OnGameStartEvent?.Invoke();
    }

    public void RestartGame()
    {
        StartGame();
    }

    public void EndGame()
    {
        if (PhotonNetwork.IsConnected && PhotonNetwork.InRoom)
        {
            photonView.RPC("EndGameRPC", RpcTarget.All);
        }
        else
        {
            EndGameLocal();
        }
    }

    [PunRPC]
    private void EndGameRPC()
    {
        EndGameLocal();
    }

    private void EndGameLocal()
    {
        gameStarted = false;
        Debug.Log("[BossGame] Game Ended!");
    }

    #endregion

    public void OnPillarBroken()
    {
        if (PhotonNetwork.IsConnected && PhotonNetwork.InRoom)
        {
            photonView.RPC("OnPillarBrokenRPC", RpcTarget.All);
        }
        else
        {
            OnPillarBrokenLocal();
        }
    }

    [PunRPC]
    private void OnPillarBrokenRPC()
    {
        OnPillarBrokenLocal();
    }

    private void OnPillarBrokenLocal()
    {
        if (!gameStarted) return;

        pillarsBrokenCount++;
        Debug.Log($"[BossGame] Pillar broken! Total broken: {pillarsBrokenCount}/6");

        if (pillarsBrokenCount >= 6)
        {
            WinGame();
        }
    }

    private void WinGame()
    {
        gameStarted = false;
        Debug.Log("[BossGame] 6 pillars broken. Game Won!");
        OnGameWonEvent?.Invoke();
    }

    public void LoseGame()
    {
        if (PhotonNetwork.IsConnected && PhotonNetwork.InRoom)
        {
            photonView.RPC("LoseGameRPC", RpcTarget.All);
        }
        else
        {
            LoseGameLocal();
        }
    }

    [PunRPC]
    private void LoseGameRPC()
    {
        LoseGameLocal();
    }

    private void LoseGameLocal()
    {
        gameStarted = false;
        Debug.Log("[BossGame] All players inside arena died. Game Lost!");

        // Revert players inside the arena to their previous checkpoints
        foreach (var player in playersInArena)
        {
            if (player != null && playerPreviousCheckpoints.TryGetValue(player, out Vector3 prevCheckpoint))
            {
                player.SetCheckpoint(prevCheckpoint);
            }
        }

        playerPreviousCheckpoints.Clear();
        playersInArena.Clear();
        pillarsBrokenCount = 0;

        OnGameLostEvent?.Invoke();
    }
}
