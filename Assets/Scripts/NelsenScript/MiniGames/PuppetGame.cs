using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Photon.Pun;
using ExitGames.Client.Photon;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public class PuppetGame : MonoBehaviour, IGames
{
    //Time
    [SerializeField] bool StartTime = false;
    [SerializeField] float totalTime = 30f;
    [SerializeField] float currentTime = 0f;

    //Rounds
    [SerializeField] int RoundsToWin = 4;
    [SerializeField] int RoundsWon = 0;
    [SerializeField] int MaxFailAttempts = 3;
    [SerializeField] int FailCounter = 0;
    [SerializeField] private string poseStateName = "Pose";
    [SerializeField] private string idleStateName = "Idle";

    // Serializable structs so the Lists show up in the Inspector
    [System.Serializable]
    public struct ObserverPuppetData {
        public int SymbolID;
        public Animator PuppetAnimator;
    }

    [System.Serializable]
    public struct guesserSymbol {
        public GameObject symbolObject;  // The lever/object the player interacts with
        public int SymbolID;
        public bool Guessed;
    }

    // Inspector-assigned
    public List<ObserverPuppetData> ObserverPuppets = new List<ObserverPuppetData>();
    public List<AnimatorOverrideController> PosesPool = new List<AnimatorOverrideController>();
    public GameObject               guesserPuppet;   // The puppet that plays the pose animation
    public List<guesserSymbol>      GuesserSymbol   = new List<guesserSymbol>();
    public guesserSymbol            SelectedGuesserSymbol;

    // Runtime dictionary: SymbolID -> AnimatorOverrideController (built in Start)
    private Dictionary<int, AnimatorOverrideController> observerDict = new Dictionary<int, AnimatorOverrideController>();

    private int  AnswerSymbolId = 0;
    private bool hasGuessed     = false;
    private Coroutine nextRoundCoroutine;
    private Coroutine endGameCoroutine;
    private List<int> correctlyGuessedSymbols = new List<int>();

    [Header("Events")]
    [SerializeField] private UnityEvent OnGameStartEvent = new UnityEvent();
    [SerializeField] private UnityEvent OnGameWonEvent  = new UnityEvent();
    [SerializeField] private UnityEvent OnGameLostEvent = new UnityEvent();

    #region Unity Callbacks

    private void Start() {
        InitDictionaries();
        LoadStateFromRoomProperties();
    }

    private void LoadStateFromRoomProperties() {
        if (!PhotonNetwork.IsConnected || !PhotonNetwork.InRoom) return;

        var room = PhotonNetwork.CurrentRoom;
        if (room == null || room.CustomProperties == null) return;

        if (room.CustomProperties.TryGetValue("Puppet_GameStarted", out object startedObj) && (bool)startedObj) {
            Debug.Log("[PuppetGame] Resuming PuppetGame state from Room Properties...");
            
            if (room.CustomProperties.TryGetValue("Puppet_RoundsWon", out object roundsObj)) {
                RoundsWon = (int)roundsObj;
            }
            if (room.CustomProperties.TryGetValue("Puppet_FailCounter", out object failObj)) {
                FailCounter = (int)failObj;
            }
            if (room.CustomProperties.TryGetValue("Puppet_CorrectSymbols", out object symbolsObj)) {
                int[] symbols = (int[])symbolsObj;
                correctlyGuessedSymbols.Clear();
                if (symbols != null) {
                    correctlyGuessedSymbols.AddRange(symbols);
                }
            }

            RestoreGuessedLevers();

            if (PhotonNetwork.IsMasterClient && RoundsWon < RoundsToWin && FailCounter < MaxFailAttempts) {
                GenerateRandomGuess();
            }
        }
    }

    private void RestoreGuessedLevers() {
        for (int i = 0; i < GuesserSymbol.Count; i++) {
            guesserSymbol s = GuesserSymbol[i];
            if (correctlyGuessedSymbols.Contains(s.SymbolID)) {
                s.Guessed = true;
                GuesserSymbol[i] = s;

                MoveRotateObject mro = s.symbolObject != null
                    ? s.symbolObject.GetComponent<MoveRotateObject>()
                    : null;
                if (mro != null) {
                    mro.Activate(0f, true);
                }
            }
        }
    }

    private void SaveStateToRoomProperties(bool started) {
        if (!PhotonNetwork.IsConnected || !PhotonNetwork.InRoom) return;

        Hashtable props = new Hashtable();
        props["Puppet_GameStarted"] = started;
        props["Puppet_RoundsWon"] = RoundsWon;
        props["Puppet_FailCounter"] = FailCounter;
        props["Puppet_CorrectSymbols"] = correctlyGuessedSymbols.ToArray();

        PhotonNetwork.CurrentRoom?.SetCustomProperties(props);
    }

    private void InitDictionaries() {
        observerDict.Clear();
        Debug.Log($"[PuppetGame] Dicts ready — observers count: {ObserverPuppets.Count}");
    }

    private void Update() {
        if (StartTime) {
            if (!PhotonNetwork.IsConnected || PhotonNetwork.IsMasterClient) {
                currentTime += Time.deltaTime;
                if (currentTime >= totalTime) {
                    PhotonView pv = GetComponent<PhotonView>();
                    if (PhotonNetwork.IsConnected && pv != null && pv.ViewID > 0) {
                        pv.RPC("TimeUpRPC", RpcTarget.All);
                    } else if (PhotonNetwork.IsConnected && PhotonNetwork.InRoom) {
                        FirstPersonController localPlayer = FindLocalPlayer();
                        if (localPlayer != null) {
                            localPlayer.RoutePuppetTimeUp();
                        }
                    } else {
                        TimeUpLocal();
                    }
                }
            } else {
                currentTime += Time.deltaTime;
            }
        }
    }

    [PunRPC]
    private void TimeUpRPC() {
        TimeUpLocal();
    }

    public void TimeUpLocal() {
        StopTimer();
        FailCounter++;
        Debug.Log($"[PuppetGame] Time up! Fails: {FailCounter}/{MaxFailAttempts}");
        if (FailCounter >= MaxFailAttempts) {
            EndGameLocal();
        } else {
            RestartTime();
            if (!PhotonNetwork.IsConnected || PhotonNetwork.IsMasterClient) {
                SaveStateToRoomProperties(true);
                GenerateRandomGuess();
            }
        }
    }

    private FirstPersonController FindLocalPlayer() {
        var controllers = FindObjectsOfType<FirstPersonController>();
        foreach (var c in controllers) {
            if (c.IsLocalPlayer) {
                return c;
            }
        }
        return null;
    }

    #endregion

    #region IGames

    public void StartGame() {
        PhotonView pv = GetComponent<PhotonView>();
        if (PhotonNetwork.IsConnected && PhotonNetwork.InRoom) {
            if (pv != null && pv.ViewID > 0) {
                if (PhotonNetwork.IsMasterClient) {
                    pv.RPC("StartGameRPC", RpcTarget.All);
                }
            } else {
                FirstPersonController localPlayer = FindLocalPlayer();
                if (localPlayer != null && PhotonNetwork.IsMasterClient) {
                    localPlayer.RoutePuppetStartGame();
                }
            }
        } else {
            StartGameLocal();
        }
    }

    [PunRPC]
    private void StartGameRPC() {
        StartGameLocal();
    }

    public void StartGameLocal() {
        InitDictionaries();
        RestartWins();
        RestartTime();
        if (!PhotonNetwork.IsConnected || PhotonNetwork.IsMasterClient) {
            SaveStateToRoomProperties(true);
        }
        Debug.Log("[PuppetGame] StartGame called locally.");
        OnGameStartEvent?.Invoke();
        if (!PhotonNetwork.IsConnected || PhotonNetwork.IsMasterClient) {
            StopGameCoroutines();
            nextRoundCoroutine = StartCoroutine(DelayedNextRound(2.0f));
        }
    }

    public void RestartGame() {
        PhotonView pv = GetComponent<PhotonView>();
        if (PhotonNetwork.IsConnected && PhotonNetwork.InRoom) {
            if (pv != null && pv.ViewID > 0) {
                if (PhotonNetwork.IsMasterClient) {
                    pv.RPC("RestartGameRPC", RpcTarget.All);
                }
            } else {
                FirstPersonController localPlayer = FindLocalPlayer();
                if (localPlayer != null && PhotonNetwork.IsMasterClient) {
                    localPlayer.RoutePuppetRestartGame();
                }
            }
        } else {
            RestartGameLocal();
        }
    }

    [PunRPC]
    private void RestartGameRPC() {
        RestartGameLocal();
    }

    public void RestartGameLocal() {
        Debug.Log("[PuppetGame] RestartGame called locally.");
        StartTime = false;
        Restart();
        if (!PhotonNetwork.IsConnected || PhotonNetwork.IsMasterClient) {
            SaveStateToRoomProperties(true);
        }
    }

    public void EndGame() {
        PhotonView pv = GetComponent<PhotonView>();
        if (PhotonNetwork.IsConnected && PhotonNetwork.InRoom) {
            if (pv != null && pv.ViewID > 0) {
                if (PhotonNetwork.IsMasterClient) {
                    pv.RPC("EndGameRPC", RpcTarget.All);
                }
            } else {
                FirstPersonController localPlayer = FindLocalPlayer();
                if (localPlayer != null && PhotonNetwork.IsMasterClient) {
                    localPlayer.RoutePuppetEndGame();
                }
            }
        } else {
            EndGameLocal();
        }
    }

    [PunRPC]
    private void EndGameRPC() {
        EndGameLocal();
    }

    public void EndGameLocal() {
        StopTimer();
        Debug.Log("[PuppetGame] EndGame called locally.");
        ResetPuppetsToIdle();
        if (!PhotonNetwork.IsConnected || PhotonNetwork.IsMasterClient) {
            if (PhotonNetwork.InRoom) {
                Hashtable props = new Hashtable { { "Puppet_GameStarted", false } };
                PhotonNetwork.CurrentRoom.SetCustomProperties(props);
            }
        }
        if (RoundsWon >= RoundsToWin) {
            Debug.Log("[PuppetGame] Game Won!");
            OnGameWonEvent?.Invoke();
        } else {
            Debug.Log("[PuppetGame] Game Lost. Restarting.");
            OnGameLostEvent?.Invoke();
            if (!PhotonNetwork.IsConnected || PhotonNetwork.IsMasterClient) {
                RestartGame();
            }
        }
    }

    private void ResetPuppetsToIdle() {
        if (guesserPuppet != null) {
            Animator anim = guesserPuppet.GetComponent<Animator>();
            if (anim != null) {
                anim.CrossFade(idleStateName, 0.2f);
            }
        }

        foreach (ObserverPuppetData o in ObserverPuppets) {
            if (o.PuppetAnimator != null) {
                o.PuppetAnimator.CrossFade(idleStateName, 0.2f);
            }
        }
    }

    #endregion

    #region Game Functions

    private void GenerateRandomGuess() {
        if (ObserverPuppets.Count == 0) {
            Debug.LogWarning("[PuppetGame] No observer puppets assigned!");
            return;
        }

        if (PhotonNetwork.IsConnected && !PhotonNetwork.IsMasterClient) {
            return;
        }

        List<int> poseIndicesList = new List<int>();
        for (int i = 0; i < PosesPool.Count; i++) {
            poseIndicesList.Add(i);
        }
        ShuffleList(poseIndicesList);

        int[] poseIndices = new int[ObserverPuppets.Count];
        List<int> activeSymbolIDs = new List<int>();
        int activeCount = 0;

        for (int i = 0; i < ObserverPuppets.Count; i++) {
            int symbolID = ObserverPuppets[i].SymbolID;
            if (correctlyGuessedSymbols.Contains(symbolID)) {
                poseIndices[i] = -1; // -1 represents Idle / solved
            } else {
                if (poseIndicesList.Count > 0) {
                    poseIndices[i] = poseIndicesList[activeCount % poseIndicesList.Count];
                } else {
                    poseIndices[i] = -1;
                }
                activeSymbolIDs.Add(symbolID);
                activeCount++;
            }
        }

        int answerSymbolID = 0;
        if (activeSymbolIDs.Count > 0) {
            int randomIndex = Random.Range(0, activeSymbolIDs.Count);
            answerSymbolID = activeSymbolIDs[randomIndex];
        } else {
            Debug.LogWarning("[PuppetGame] No active symbols left to guess!");
        }

        PhotonView pv = GetComponent<PhotonView>();
        if (PhotonNetwork.IsConnected && pv != null && pv.ViewID > 0) {
            pv.RPC("SyncRoundState", RpcTarget.All, poseIndices, answerSymbolID);
        } else if (PhotonNetwork.IsConnected && PhotonNetwork.InRoom) {
            FirstPersonController localPlayer = FindLocalPlayer();
            if (localPlayer != null) {
                localPlayer.RoutePuppetSyncRoundState(poseIndices, answerSymbolID);
            }
        } else {
            SyncRoundStateLocal(poseIndices, answerSymbolID);
        }
    }

    [PunRPC]
    private void SyncRoundState(int[] poseIndices, int answerSymbolID) {
        SyncRoundStateLocal(poseIndices, answerSymbolID);
    }

    public void SyncRoundStateLocal(int[] poseIndices, int answerSymbolID) {
        hasGuessed = false;

        for (int i = 0; i < GuesserSymbol.Count; i++) {
            guesserSymbol s = GuesserSymbol[i];
            s.Guessed = false;
            GuesserSymbol[i] = s;
        }

        observerDict.Clear();

        for (int i = 0; i < ObserverPuppets.Count; i++) {
            if (i < poseIndices.Length) {
                int poseIndex = poseIndices[i];
                ObserverPuppetData data = ObserverPuppets[i];

                if (poseIndex == -1) {
                    // Set already guessed or unassigned puppets to Idle
                    if (data.PuppetAnimator != null) {
                        data.PuppetAnimator.CrossFade(idleStateName, 0.2f);
                    }
                } else if (poseIndex >= 0 && poseIndex < PosesPool.Count) {
                    AnimatorOverrideController newPose = PosesPool[poseIndex];

                    if (data.PuppetAnimator != null && newPose != null) {
                        data.PuppetAnimator.runtimeAnimatorController = newPose;
                        data.PuppetAnimator.CrossFade(poseStateName, 0.2f);
                    }

                    if (!observerDict.ContainsKey(data.SymbolID)) {
                        observerDict.Add(data.SymbolID, newPose);
                    }
                }
            }
        }

        AnswerSymbolId = answerSymbolID;

        if (guesserPuppet != null &&
            observerDict.TryGetValue(AnswerSymbolId, out AnimatorOverrideController pose) &&
            pose != null) {
            Animator anim = guesserPuppet.GetComponent<Animator>();
            if (anim != null) {
                anim.runtimeAnimatorController = pose;
                anim.CrossFade(poseStateName, 0.2f);
            }
        }

        Debug.Log($"[PuppetGame] New round started — Answer symbol is {AnswerSymbolId}.");
        StartTimer();
    }

    private void ShuffleList<T>(List<T> list) {
        for (int i = list.Count - 1; i > 0; i--) {
            int r = Random.Range(0, i + 1);
            T tmp = list[i];
            list[i] = list[r];
            list[r] = tmp;
        }
    }

    public void GuessSymbol(int GuessSymbolID) {
        PhotonView pv = GetComponent<PhotonView>();
        if (PhotonNetwork.IsConnected && pv != null && pv.ViewID > 0) {
            pv.RPC("GuessSymbolRPC", RpcTarget.All, GuessSymbolID);
        } else if (PhotonNetwork.IsConnected && PhotonNetwork.InRoom) {
            FirstPersonController localPlayer = FindLocalPlayer();
            if (localPlayer != null) {
                localPlayer.RoutePuppetGuessSymbol(GuessSymbolID);
            }
        } else {
            GuessSymbolLocal(GuessSymbolID);
        }
    }

    [PunRPC]
    private void GuessSymbolRPC(int GuessSymbolID) {
        GuessSymbolLocal(GuessSymbolID);
    }

    public void GuessSymbolLocal(int GuessSymbolID) {
        if (hasGuessed) return; // Already guessed this round
        hasGuessed = true;

        StopTimer();

        // Find the matching guesser symbol and mark it as guessed (by index, structs are value types)
        for (int i = 0; i < GuesserSymbol.Count; i++) {
            if (GuesserSymbol[i].SymbolID == GuessSymbolID) {
                guesserSymbol s = GuesserSymbol[i];
                s.Guessed = true;
                GuesserSymbol[i] = s;
                SelectedGuesserSymbol = s;
                break;
            }
        }

        MoveRotateObject mro = SelectedGuesserSymbol.symbolObject != null
            ? SelectedGuesserSymbol.symbolObject.GetComponent<MoveRotateObject>()
            : null;

        if (GuessSymbolID == AnswerSymbolId) {
            // Correct Guess — activate the symbol object (it stays visible)
            if (mro != null) mro.Activate();
            RoundsWon++;
            correctlyGuessedSymbols.Add(GuessSymbolID);
            
            if (!PhotonNetwork.IsConnected || PhotonNetwork.IsMasterClient) {
                SaveStateToRoomProperties(true);
            }

            Debug.Log($"[PuppetGame] You answered correctly! You guessed {GuessSymbolID}. RoundsWon: {RoundsWon}/{RoundsToWin}");
            if (RoundsWon >= RoundsToWin) {
                if (!PhotonNetwork.IsConnected || PhotonNetwork.IsMasterClient) {
                    endGameCoroutine = StartCoroutine(DelayedEndGame(2.0f));
                }
            } else {
                RestartTime();
                if (!PhotonNetwork.IsConnected || PhotonNetwork.IsMasterClient) {
                    nextRoundCoroutine = StartCoroutine(DelayedNextRound(2.0f));
                }
            }
        } else {
            // Wrong Guess — deactivate the symbol object, reset Guessed flag
            if (mro != null) mro.Deactivate();

            // Reset Guessed flag back to false for this entry
            for (int i = 0; i < GuesserSymbol.Count; i++) {
                if (GuesserSymbol[i].SymbolID == GuessSymbolID) {
                    guesserSymbol s = GuesserSymbol[i];
                    s.Guessed = false;
                    GuesserSymbol[i] = s;
                    break;
                }
            }

            FailCounter++;

            if (!PhotonNetwork.IsConnected || PhotonNetwork.IsMasterClient) {
                SaveStateToRoomProperties(true);
            }

            Debug.Log($"[PuppetGame] You answered wrong! Answer is {AnswerSymbolId} but you answered {GuessSymbolID}. Fails: {FailCounter}/{MaxFailAttempts}");
            if (FailCounter >= MaxFailAttempts) {
                if (!PhotonNetwork.IsConnected || PhotonNetwork.IsMasterClient) {
                    endGameCoroutine = StartCoroutine(DelayedEndGame(2.0f));
                }
            } else {
                RestartTime();
                if (!PhotonNetwork.IsConnected || PhotonNetwork.IsMasterClient) {
                    nextRoundCoroutine = StartCoroutine(DelayedNextRound(2.0f));
                }
            }
        }
    }

    #endregion

    #region Helper Functions

    private void Restart() {
        StopGameCoroutines();
        RestartTime();
        RestartWins();
    }

    private void StopGameCoroutines() {
        if (nextRoundCoroutine != null) {
            StopCoroutine(nextRoundCoroutine);
            nextRoundCoroutine = null;
        }
        if (endGameCoroutine != null) {
            StopCoroutine(endGameCoroutine);
            endGameCoroutine = null;
        }
    }

    private IEnumerator DelayedNextRound(float delay) {
        yield return new WaitForSeconds(delay);
        GenerateRandomGuess();
    }

    private IEnumerator DelayedEndGame(float delay) {
        yield return new WaitForSeconds(delay);
        EndGame();
    }

    private void RestartTime() {
        currentTime = 0f;
    }

    private void RestartWins() {
        RoundsWon = 0;
        FailCounter = 0;
        correctlyGuessedSymbols.Clear();
    }

    private void StartTimer(){
        StartTime = true;
        currentTime = 0;
    }

    private void StopTimer(){
        StartTime = false;
    }
    #endregion
}