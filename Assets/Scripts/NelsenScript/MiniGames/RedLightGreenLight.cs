using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Photon.Pun;

public class RedLightGreenLight : MonoBehaviour, IGames
{
    public enum GameState
    {
        Inactive,
        GreenLight,
        RedLight
    }

    [Header("State Settings")]
    [SerializeField] private GameState currentState = GameState.Inactive;

    [Header("Timer Configurations")]
    [SerializeField] private float minGreenDuration = 3f;
    [SerializeField] private float maxGreenDuration = 10f;
    [SerializeField] private float redDuration = 5f;

    [Header("Audio Settings")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip warningSound;

    [Header("Events")]
    [SerializeField] private UnityEvent OnGameStart = new UnityEvent();
    [SerializeField] private UnityEvent OnGameEnd = new UnityEvent();
    [SerializeField] private UnityEvent OnGameWon = new UnityEvent();
    [SerializeField] private UnityEvent OnGreenLight = new UnityEvent();
    [SerializeField] private UnityEvent OnRedLight = new UnityEvent();

    private float stateTimer = 0f;
    private float currentTargetDuration = 0f;
    private bool isGameActive = false;
    private bool warningPlayed = false;

    // Public Properties exposed for shooter script
    public GameState CurrentState => currentState;
    public bool IsGameActive => isGameActive;

    private void Start()
    {
        currentState = GameState.Inactive;
        isGameActive = false;
    }

    private void Update()
    {
        if (!isGameActive) return;

        if (!PhotonNetwork.IsConnected || PhotonNetwork.IsMasterClient)
        {
            stateTimer += Time.deltaTime;

            switch (currentState)
            {
                case GameState.GreenLight:
                    // Play warning sound 3 seconds before transitioning to Red Light
                    if (!warningPlayed && stateTimer >= (currentTargetDuration - 3f))
                    {
                        warningPlayed = true;
                        PhotonView pv = GetComponent<PhotonView>();
                        if (PhotonNetwork.IsConnected && pv != null && pv.ViewID > 0)
                        {
                            pv.RPC("PlayWarningRPC", RpcTarget.All);
                        }
                        else if (PhotonNetwork.IsConnected && PhotonNetwork.InRoom)
                        {
                            FirstPersonController localPlayer = FindLocalPlayer();
                            if (localPlayer != null)
                            {
                                localPlayer.RouteRedLightPlayWarning();
                            }
                        }
                        else
                        {
                            PlayWarningLocal();
                        }
                    }

                    if (stateTimer >= currentTargetDuration)
                    {
                        TransitionToRedLight();
                    }
                    break;

                case GameState.RedLight:
                    if (stateTimer >= redDuration)
                    {
                        TransitionToGreenLight();
                    }
                    break;
            }
        }
        else
        {
            stateTimer += Time.deltaTime;
        }
    }

    private FirstPersonController FindLocalPlayer()
    {
        var controllers = FindObjectsOfType<FirstPersonController>();
        foreach (var c in controllers)
        {
            if (c.IsLocalPlayer)
            {
                return c;
            }
        }
        return null;
    }

    private void TransitionToGreenLight()
    {
        float duration = Random.Range(minGreenDuration, maxGreenDuration);
        PhotonView pv = GetComponent<PhotonView>();
        if (PhotonNetwork.IsConnected && pv != null && pv.ViewID > 0)
        {
            pv.RPC("SyncStateRPC", RpcTarget.All, (int)GameState.GreenLight, duration);
        }
        else if (PhotonNetwork.IsConnected && PhotonNetwork.InRoom)
        {
            FirstPersonController localPlayer = FindLocalPlayer();
            if (localPlayer != null)
            {
                localPlayer.RouteRedLightSyncState((int)GameState.GreenLight, duration);
            }
        }
        else
        {
            SyncStateLocal(GameState.GreenLight, duration);
        }
    }

    private void TransitionToRedLight()
    {
        PhotonView pv = GetComponent<PhotonView>();
        if (PhotonNetwork.IsConnected && pv != null && pv.ViewID > 0)
        {
            pv.RPC("SyncStateRPC", RpcTarget.All, (int)GameState.RedLight, redDuration);
        }
        else if (PhotonNetwork.IsConnected && PhotonNetwork.InRoom)
        {
            FirstPersonController localPlayer = FindLocalPlayer();
            if (localPlayer != null)
            {
                localPlayer.RouteRedLightSyncState((int)GameState.RedLight, redDuration);
            }
        }
        else
        {
            SyncStateLocal(GameState.RedLight, redDuration);
        }
    }

    [PunRPC]
    private void SyncStateRPC(int stateVal, float duration)
    {
        SyncStateLocal((GameState)stateVal, duration);
    }

    public void SyncStateLocal(GameState state, float duration)
    {
        currentState = state;
        stateTimer = 0f;
        warningPlayed = false;
        currentTargetDuration = duration;

        if (state == GameState.GreenLight)
        {
            Debug.Log($"[RedLightGreenLight] Green Light! Duration: {duration:F2} seconds.");
            OnGreenLight?.Invoke();
        }
        else if (state == GameState.RedLight)
        {
            Debug.Log($"[RedLightGreenLight] Red Light! Duration: {duration:F2} seconds.");
            OnRedLight?.Invoke();
        }
    }

    public void PlayWarningLocal()
    {
        if (audioSource != null && warningSound != null)
        {
            audioSource.PlayOneShot(warningSound);
            Debug.Log("[RedLightGreenLight] Warning sound played! 3 seconds until Red Light.");
        }
    }

    [PunRPC]
    private void PlayWarningRPC()
    {
        PlayWarningLocal();
    }

    // Public method to be called manually (e.g. via finish line triggers or buttons) to end/win the game
    public void WinGame()
    {
        PhotonView pv = GetComponent<PhotonView>();
        if (PhotonNetwork.IsConnected && PhotonNetwork.InRoom)
        {
            if (pv != null && pv.ViewID > 0)
            {
                pv.RPC("WinGameRPC", RpcTarget.All);
            }
            else
            {
                FirstPersonController localPlayer = FindLocalPlayer();
                if (localPlayer != null)
                {
                    localPlayer.RouteRedLightWinGame();
                }
            }
        }
        else
        {
            WinGameLocal();
        }
    }

    [PunRPC]
    private void WinGameRPC()
    {
        WinGameLocal();
    }

    public void WinGameLocal()
    {
        if (!isGameActive) return;
        Debug.Log("[RedLightGreenLight] WinGame called manually. Game Won.");
        OnGameWon?.Invoke();
        EndGameLocal();
    }

    #region IGames Implementation
    public void StartGame()
    {
        PhotonView pv = GetComponent<PhotonView>();
        if (PhotonNetwork.IsConnected && PhotonNetwork.InRoom)
        {
            if (pv != null && pv.ViewID > 0)
            {
                pv.RPC("StartGameRPC", RpcTarget.All);
            }
            else
            {
                FirstPersonController localPlayer = FindLocalPlayer();
                if (localPlayer != null)
                {
                    localPlayer.RouteRedLightStartGame();
                }
            }
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

    public void StartGameLocal()
    {
        isGameActive = true;
        Debug.Log("[RedLightGreenLight] StartGame called.");
        OnGameStart?.Invoke();
        if (!PhotonNetwork.IsConnected || PhotonNetwork.IsMasterClient)
        {
            TransitionToGreenLight();
        }
    }

    public void RestartGame()
    {
        PhotonView pv = GetComponent<PhotonView>();
        if (PhotonNetwork.IsConnected && PhotonNetwork.InRoom)
        {
            if (pv != null && pv.ViewID > 0)
            {
                pv.RPC("RestartGameRPC", RpcTarget.All);
            }
            else
            {
                FirstPersonController localPlayer = FindLocalPlayer();
                if (localPlayer != null)
                {
                    localPlayer.RouteRedLightRestartGame();
                }
            }
        }
        else
        {
            RestartGameLocal();
        }
    }

    [PunRPC]
    private void RestartGameRPC()
    {
        RestartGameLocal();
    }

    public void RestartGameLocal()
    {
        Debug.Log("[RedLightGreenLight] RestartGame called.");
        isGameActive = false;
        currentState = GameState.Inactive;
        stateTimer = 0f;
        StartGameLocal();
    }

    public void EndGame()
    {
        PhotonView pv = GetComponent<PhotonView>();
        if (PhotonNetwork.IsConnected && PhotonNetwork.InRoom)
        {
            if (pv != null && pv.ViewID > 0)
            {
                pv.RPC("EndGameRPC", RpcTarget.All);
            }
            else
            {
                FirstPersonController localPlayer = FindLocalPlayer();
                if (localPlayer != null)
                {
                    localPlayer.RouteRedLightEndGame();
                }
            }
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

    public void EndGameLocal()
    {
        Debug.Log("[RedLightGreenLight] EndGame called.");
        isGameActive = false;
        currentState = GameState.Inactive;
        stateTimer = 0f;
        OnGameEnd?.Invoke();
    }
    #endregion
}
