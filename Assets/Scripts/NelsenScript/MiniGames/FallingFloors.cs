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
                    currentState = GameState.RoundStart;
                    currentStateTime = GetRoundDuration(currentRound);
                    
                    // Reset platforms so players can walk on them
                    ResetPlatform();

                    // Generate new symbol and play sequence
                    GenerateRandomPlatformsSymbol();
                    if (audioCoroutine != null)
                    {
                        StopCoroutine(audioCoroutine);
                    }
                    audioCoroutine = StartCoroutine(PlaySymbolSequence(currentSymbol));
                }
                break;

            case GameState.RoundStart:
                // Round timer ended, drop incorrect platforms
                DropPlatform(currentSymbol);
                
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
        for (int i = 0; i < 3; i++)
        {
            currentSymbol[i] = Random.Range(1, 4); // 1, 2, or 3
        }
        Debug.Log($"[FallingFloors] Round {currentRound} generated symbol: {currentSymbol[0]}, {currentSymbol[1]}, {currentSymbol[2]}");
    }

    private void DropPlatform(int[] symbols)
    {
        Debug.Log($"[FallingFloors] Dropping all platforms except those matching: {symbols[0]}, {symbols[1]}, {symbols[2]}");
        foreach (var pair in platformsSymbols)
        {
            if (pair.Key == null) continue;

            if (!SymbolsMatch(pair.Value, symbols))
            {
                MoveRotateObject moveRotate = pair.Key.GetComponent<MoveRotateObject>();
                if (moveRotate != null)
                {
                    moveRotate.Activate();
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
                moveRotate.Deactivate();
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
        foreach (int s in symbol)
        {
            int clipIndex = s - 1; // Translate 1,2,3 to 0,1,2
            if (clipIndex >= 0 && clipIndex < symbolClips.Length && symbolClips[clipIndex] != null && audioSource != null)
            {
                audioSource.clip = symbolClips[clipIndex];
                audioSource.Play();
                yield return new WaitWhile(() => audioSource.isPlaying);
                yield return new WaitForSeconds(0.2f); // Short pause between playbacks
            }
        }
    }
    #endregion
}
