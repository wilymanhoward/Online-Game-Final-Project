using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Photon.Pun;

public class FallingFloors : MonoBehaviour, IGames
{
    [System.Serializable]
    public struct PlatformSymbolMapping
    {
        public GameObject platform;
        public int[] symbol; // 3 integers, e.g., 2, 1, 3
    }

    [Header("Platform Mapping")]
    [SerializeField] private List<PlatformSymbolMapping> platformMappingsList = new List<PlatformSymbolMapping>();
    
    public Dictionary<GameObject, int[]> platformsSymbols = new Dictionary<GameObject, int[]>();

    [Header("Audio Settings")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip[] symbolClips = new AudioClip[3]; // Clip 1, 2, 3 corresponding to symbol integers 1, 2, 3

    [Header("Clock Sound Settings")]
    [SerializeField] private AudioClip clockTickSlow;
    [SerializeField] private AudioClip clockTickFast;

    private Coroutine clockCoroutine;

    [Header("Game Loop Settings")]
    [SerializeField] private float timeBetweenRounds = 5f;
    [SerializeField] private int totalRounds = 7;

    [Header("Events")]
    [SerializeField] private UnityEvent OnGameStartEvent;
    [SerializeField] private UnityEvent OnGameWonEvent;
    [SerializeField] private UnityEvent OnGameLostEvent;

    [Header("Arena Detection")]
    [SerializeField] private Collider arenaCollider;

    private enum GameState { BetweenRound, RoundStart, GameEnd }
    private GameState currentState;
    private float currentStateTime;
    private float currentTime = 0f;
    private int currentRound = 0;
    private bool roundStart = false;

    private int[] currentSymbol = new int[3];
    private Coroutine audioCoroutine;

    private void Start()
    {
        InitializeDictionary();
        RestartGame();
    }

    private void InitializeDictionary()
    {
        platformsSymbols.Clear();
        foreach (var mapping in platformMappingsList)
        {
            if (mapping.platform != null && mapping.symbol != null && mapping.symbol.Length == 3)
            {
                if (!platformsSymbols.ContainsKey(mapping.platform))
                {
                    platformsSymbols.Add(mapping.platform, mapping.symbol);
                }
            }
        }
    }

    private void Update()
    {
        if (currentState != GameState.GameEnd && roundStart)
        {
            if (!IsAnyPlayerInArena())
            {
                Debug.Log("[FallingFloors] No players left inside the arena. Ending game.");
                currentState = GameState.GameEnd;
                EndGame();
                return;
            }
        }

        if (roundStart)
        {
            if (!PhotonNetwork.IsConnected || PhotonNetwork.IsMasterClient)
            {
                currentTime += Time.deltaTime;
                if (currentTime >= currentStateTime)
                {
                    currentTime = 0f;
                    TransitionState();
                }
            }
            else
            {
                currentTime += Time.deltaTime;
            }
        }
    }

    private void TransitionState()
    {
        PhotonView pv = GetComponent<PhotonView>();
        switch (currentState)
        {
            case GameState.BetweenRound:
                // Start the next round
                currentRound++;
                if (currentRound > totalRounds)
                {
                    if (PhotonNetwork.IsConnected && pv != null && pv.ViewID > 0)
                    {
                        pv.RPC("SyncTransitionRPC", RpcTarget.All, currentRound, (int)GameState.GameEnd, currentSymbol);
                    }
                    else if (PhotonNetwork.IsConnected && PhotonNetwork.InRoom)
                    {
                        FirstPersonController localPlayer = FindLocalPlayer();
                        if (localPlayer != null)
                        {
                            localPlayer.RouteFallingFloorsSyncTransition(currentRound, (int)GameState.GameEnd, currentSymbol);
                        }
                    }
                    else
                    {
                        currentState = GameState.GameEnd;
                        currentStateTime = 0f;
                        EndGameLocal();
                    }
                }
                else
                {
                    // Generate new symbol and play sequence
                    GenerateRandomPlatformsSymbol();
                    
                    if (PhotonNetwork.IsConnected && pv != null && pv.ViewID > 0)
                    {
                        pv.RPC("SyncTransitionRPC", RpcTarget.All, currentRound, (int)GameState.RoundStart, currentSymbol);
                    }
                    else if (PhotonNetwork.IsConnected && PhotonNetwork.InRoom)
                    {
                        FirstPersonController localPlayer = FindLocalPlayer();
                        if (localPlayer != null)
                        {
                            localPlayer.RouteFallingFloorsSyncTransition(currentRound, (int)GameState.RoundStart, currentSymbol);
                        }
                    }
                    else
                    {
                        // Reset platforms so players can walk on them
                        ResetPlatform();

                        // Temporarily stop the round timer countdown while the audio is playing
                        roundStart = false;
                        currentTime = 0f;

                        if (audioCoroutine != null)
                        {
                            StopCoroutine(audioCoroutine);
                        }
                        audioCoroutine = StartCoroutine(PlaySymbolSequenceAndStartRound(currentSymbol));
                    }
                }
                break;

            case GameState.RoundStart:
                if (PhotonNetwork.IsConnected && pv != null && pv.ViewID > 0)
                {
                    pv.RPC("SyncTransitionRPC", RpcTarget.All, currentRound, (int)GameState.BetweenRound, currentSymbol);
                }
                else if (PhotonNetwork.IsConnected && PhotonNetwork.InRoom)
                {
                    FirstPersonController localPlayer = FindLocalPlayer();
                    if (localPlayer != null)
                    {
                        localPlayer.RouteFallingFloorsSyncTransition(currentRound, (int)GameState.BetweenRound, currentSymbol);
                    }
                }
                else
                {
                    // Round timer ended, drop incorrect platforms
                    DropPlatform(currentSymbol);

                    if (clockCoroutine != null)
                    {
                        StopCoroutine(clockCoroutine);
                        clockCoroutine = null;
                    }

                    if (audioSource != null)
                    {
                        audioSource.Stop(); // Stop ticking audio
                    }
                    
                    // Go to BetweenRound wait state
                    currentState = GameState.BetweenRound;
                    currentStateTime = timeBetweenRounds;
                    currentTime = 0f;
                    roundStart = true;
                }
                break;

            case GameState.GameEnd:
                if (PhotonNetwork.IsConnected && pv != null && pv.ViewID > 0)
                {
                    pv.RPC("SyncTransitionRPC", RpcTarget.All, currentRound, (int)GameState.GameEnd, currentSymbol);
                }
                else if (PhotonNetwork.IsConnected && PhotonNetwork.InRoom)
                {
                    FirstPersonController localPlayer = FindLocalPlayer();
                    if (localPlayer != null)
                    {
                        localPlayer.RouteFallingFloorsSyncTransition(currentRound, (int)GameState.GameEnd, currentSymbol);
                    }
                }
                else
                {
                    EndGameLocal();
                }
                break;
        }
    }

    [PunRPC]
    private void SyncTransitionRPC(int round, int nextStateVal, int[] symbols)
    {
        SyncTransitionLocal(round, nextStateVal, symbols);
    }

    public void SyncTransitionLocal(int round, int nextStateVal, int[] symbols)
    {
        currentRound = round;
        GameState nextState = (GameState)nextStateVal;
        if (symbols != null && currentSymbol != null && symbols.Length == currentSymbol.Length)
        {
            System.Array.Copy(symbols, currentSymbol, symbols.Length);
        }

        if (nextState == GameState.RoundStart)
        {
            // Reset platforms so players can walk on them
            ResetPlatform();

            // Temporarily stop the round timer countdown while the audio is playing
            roundStart = false;
            currentTime = 0f;

            if (audioCoroutine != null)
            {
                StopCoroutine(audioCoroutine);
            }
            audioCoroutine = StartCoroutine(PlaySymbolSequenceAndStartRound(currentSymbol));
        }
        else if (nextState == GameState.BetweenRound)
        {
            // Round timer ended, drop incorrect platforms
            DropPlatform(currentSymbol);

            if (clockCoroutine != null)
            {
                StopCoroutine(clockCoroutine);
                clockCoroutine = null;
            }

            if (audioSource != null)
            {
                audioSource.Stop(); // Stop ticking audio
            }
            
            // Go to BetweenRound wait state
            currentState = GameState.BetweenRound;
            currentStateTime = timeBetweenRounds;
            currentTime = 0f;
            roundStart = true;
        }
        else if (nextState == GameState.GameEnd)
        {
            currentState = GameState.GameEnd;
            EndGameLocal();
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

    private float GetRoundDuration(int round)
    {
        if (round <= 3) return 15f;
        if (round <= 6) return 10f;
        return 5f;
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
                    localPlayer.RouteFallingFloorsStartGame();
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
        RestartGameLocal();

        roundStart = true;
        currentState = GameState.BetweenRound;
        currentStateTime = timeBetweenRounds;
        currentTime = 0f;

        Debug.Log("FallingFloors: StartGame called.");
        OnGameStartEvent?.Invoke();
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
                    localPlayer.RouteFallingFloorsRestartGame();
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
        currentRound = 0;
        currentTime = 0f;
        roundStart = false;
        currentState = GameState.BetweenRound;
        
        InitializeDictionary();
        ResetPlatform();

        StopGameCoroutines();
        if (audioSource != null)
        {
            audioSource.Stop();
        }
        Debug.Log("FallingFloors: RestartGame called.");
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
                    localPlayer.RouteFallingFloorsEndGame();
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
        roundStart = false;
        StopGameCoroutines();
        if (audioSource != null)
        {
            audioSource.Stop();
        }
        
        if (currentRound > totalRounds)
        {
            GameWon();
        }
        else
        {
            GameLost();
        }
    }

    private void StopGameCoroutines()
    {
        if (audioCoroutine != null)
        {
            StopCoroutine(audioCoroutine);
            audioCoroutine = null;
        }
        if (clockCoroutine != null)
        {
            StopCoroutine(clockCoroutine);
            clockCoroutine = null;
        }
    }
    #endregion

    private void GameLost()
    {
        Debug.Log("[FallingFloors] Game lost — resetting and waiting for trigger.");

        // Stop all coroutines and audio
        if (audioCoroutine != null) { StopCoroutine(audioCoroutine); audioCoroutine = null; }
        if (clockCoroutine != null) { StopCoroutine(clockCoroutine); clockCoroutine = null; }
        if (audioSource != null)    { audioSource.Stop(); }

        // Reset platforms back to their original positions
        ResetPlatform();

        // Reset round counters, but keep roundStart = false so nothing runs
        // until the trigger zone calls StartGame() again.
        currentRound     = 0;
        currentTime      = 0f;
        roundStart       = false;
        currentState     = GameState.BetweenRound;
        currentStateTime = 0f;

        OnGameLostEvent?.Invoke();
    }

    private void GameWon()
    {
        Debug.Log("FallingFloors: Game Won!");
        OnGameWonEvent?.Invoke();
    }



    private bool IsAnyPlayerInArena()
    {
        if (arenaCollider == null) return true;

        FirstPersonController[] players = FindObjectsOfType<FirstPersonController>();
        foreach (var player in players)
        {
            if (player != null && !player.IsDead)
            {
                Collider playerCol = player.GetComponent<Collider>();
                if (playerCol != null)
                {
                    if (arenaCollider.bounds.Intersects(playerCol.bounds))
                    {
                        return true;
                    }
                }
                else
                {
                    if (arenaCollider.bounds.Contains(player.transform.position))
                    {
                        return true;
                    }
                }
            }
        }
        return false;
    }

    #region Platforms
    private void GenerateRandomPlatformsSymbol()
    {
        List<int> pool = new List<int> { 1, 2, 3 };
        for (int i = 0; i < 3; i++)
        {
            int index = Random.Range(0, pool.Count);
            currentSymbol[i] = pool[index];
            pool.RemoveAt(index);
        }
        Debug.Log($"[FallingFloors] Round {currentRound} generated symbol: {currentSymbol[0]}, {currentSymbol[1]}, {currentSymbol[2]}");
    }

    private void DropPlatform(int[] symbols)
    {
        Debug.Log($"[FallingFloors] Dropping all platforms except those matching: {symbols[0]}, {symbols[1]}, {symbols[2]}");
        foreach (var pair in platformsSymbols)
        {
            if (pair.Key == null) continue;

            bool matches = SymbolsMatch(pair.Value, symbols);
            Debug.Log($"[FallingFloors] Checking platform: {pair.Key.name} | Platform Symbol: {pair.Value[0]},{pair.Value[1]},{pair.Value[2]} | Target Symbol: {symbols[0]},{symbols[1]},{symbols[2]} | Match: {matches}");

            if (!matches)
            {
                MoveRotateObject moveRotate = pair.Key.GetComponent<MoveRotateObject>();
                if (moveRotate != null)
                {
                    Debug.Log($"[FallingFloors] Calling Activate (force=true) on incorrect platform: {pair.Key.name}. Current MoveRotate state - isActive: {moveRotate.IsActive}, progress: {moveRotate.IsMoving}");
                    moveRotate.Activate(0f, true); // Force drop
                }
                else
                {
                    Debug.LogWarning($"[FallingFloors] Platform {pair.Key.name} does not have a MoveRotateObject component!");
                }
            }
        }
    }

    private void ResetPlatform()
    {
        foreach (var pair in platformsSymbols)
        {
            if (pair.Key == null) continue;

            MoveRotateObject moveRotate = pair.Key.GetComponent<MoveRotateObject>();
            if (moveRotate != null)
            {
                moveRotate.Deactivate(0f, true); // Force reset
            }
        }
    }

    private bool SymbolsMatch(int[] a, int[] b)
    {
        if (a == null || b == null) return false;
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i] != b[i]) return false;
        }
        return true;
    }
    #endregion

    #region Audio Playback
    private IEnumerator PlaySymbolSequence(int[] symbol)
    {
        if (audioSource == null) yield break;

        bool originalLoop = audioSource.loop;
        audioSource.loop = false; // Disable looping for sequence playback

        foreach (int s in symbol)
        {
            int clipIndex = s - 1; // Translate 1,2,3 to 0,1,2
            if (clipIndex >= 0 && clipIndex < symbolClips.Length && symbolClips[clipIndex] != null)
            {
                audioSource.clip = symbolClips[clipIndex];
                audioSource.Play();
                
                while (audioSource.isPlaying)
                {
                    yield return null;
                }

                yield return new WaitForSeconds(0.2f); // Short pause between playbacks
            }
        }

        audioSource.Stop();
        audioSource.clip = null; // Clear clip so it does not auto-loop the last clip played
        audioSource.loop = originalLoop; // Restore original loop configuration
    }

    private IEnumerator PlaySymbolSequenceAndStartRound(int[] symbol)
    {
        yield return StartCoroutine(PlaySymbolSequence(symbol));

        // Once audio sequence has finished, start the round!
        currentState = GameState.RoundStart;
        currentStateTime = GetRoundDuration(currentRound);
        currentTime = 0f;
        roundStart = true;
        
        Debug.Log($"[FallingFloors] Audio sequence finished. Starting Round {currentRound} with duration {currentStateTime}s.");

        if (clockCoroutine != null) StopCoroutine(clockCoroutine);
        clockCoroutine = StartCoroutine(PlayClockTicks());
    }

    private IEnumerator PlayClockTicks()
    {
        if (audioSource == null) yield break;

        while (currentState == GameState.RoundStart && roundStart)
        {
            float timeLeft = currentStateTime - currentTime;
            if (timeLeft <= 0.1f) break;

            AudioClip tickClip = null;
            float interval = 1f;

            if (timeLeft > 5f)
            {
                tickClip = clockTickSlow;
                interval = 1f;
            }
            else
            {
                tickClip = clockTickFast;
                interval = 0.5f;
            }

            if (tickClip != null)
            {
                audioSource.PlayOneShot(tickClip);
            }

            yield return new WaitForSeconds(interval);
        }
    }
    #endregion
}
