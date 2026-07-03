using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

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
    [SerializeField] private int totalRounds = 10;

    [Header("Events")]
    [SerializeField] private UnityEvent OnGameWonEvent;

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
        if (roundStart)
        {
            currentTime += Time.deltaTime;
            if (currentTime >= currentStateTime)
            {
                currentTime = 0f;
                TransitionState();
            }
        }
    }

    private void TransitionState()
    {
        switch (currentState)
        {
            case GameState.BetweenRound:
                // Start the next round
                currentRound++;
                if (currentRound > totalRounds)
                {
                    currentState = GameState.GameEnd;
                    currentStateTime = 0f;
                    EndGame();
                }
                else
                {
                    // Generate new symbol and play sequence
                    GenerateRandomPlatformsSymbol();
                    
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
                break;

            case GameState.RoundStart:
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
                break;

            case GameState.GameEnd:
                EndGame();
                break;
        }
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
        RestartGame();
        if(audioSource == null){
            audioSource = DisabilityManager.Instance.BlindPlayer.GetComponent<AudioSource>();
        }

        roundStart = true;
        currentState = GameState.BetweenRound;
        currentStateTime = timeBetweenRounds;
        currentTime = 0f;
        
        Debug.Log("FallingFloors: StartGame called.");
    }

    public void RestartGame()
    {
        currentRound = 0;
        currentTime = 0f;
        roundStart = false;
        currentState = GameState.BetweenRound;
        
        // Make sure dictionary is initialized (in case mapping list was changed in Inspector)
        InitializeDictionary();
        ResetPlatform();

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
        if (audioSource != null)
        {
            audioSource.Stop();
        }
        Debug.Log("FallingFloors: RestartGame called.");
    }

    public void EndGame()
    {
        roundStart = false;
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
    #endregion

    private void GameLost()
    {
        RestartGame();
    }

    private void GameWon()
    {
        Debug.Log("FallingFloors: Game Won!");
        OnGameWonEvent?.Invoke();
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
                    Debug.Log($"[FallingFloors] Calling Activate (force=true) on incorrect platform: {pair.Key.name}. Current MoveRotate state - isActive: {moveRotate.isActive}, progress: {moveRotate.IsMoving}");
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
                yield return new WaitWhile(() => audioSource.isPlaying);
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
