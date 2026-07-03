using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

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

        stateTimer += Time.deltaTime;

        switch (currentState)
        {
            case GameState.GreenLight:
                // Play warning sound 3 seconds before transitioning to Red Light
                if (!warningPlayed && stateTimer >= (currentTargetDuration - 3f))
                {
                    warningPlayed = true;
                    PlayWarning();
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

    private void TransitionToGreenLight()
    {
        currentState = GameState.GreenLight;
        stateTimer = 0f;
        warningPlayed = false;
        currentTargetDuration = Random.Range(minGreenDuration, maxGreenDuration);
        Debug.Log($"[RedLightGreenLight] Green Light! Duration: {currentTargetDuration:F2} seconds.");
        OnGreenLight?.Invoke();
    }

    private void TransitionToRedLight()
    {
        currentState = GameState.RedLight;
        stateTimer = 0f;
        Debug.Log($"[RedLightGreenLight] Red Light! Duration: {redDuration:F2} seconds.");
        OnRedLight?.Invoke();
    }

    private void PlayWarning()
    {
        if (audioSource == null && DisabilityManager.Instance != null && DisabilityManager.Instance.BlindPlayer != null)
        {
            audioSource = DisabilityManager.Instance.BlindPlayer.GetComponent<AudioSource>();
        }

        if (audioSource != null && warningSound != null)
        {
            audioSource.PlayOneShot(warningSound);
            Debug.Log("[RedLightGreenLight] Warning sound played! 3 seconds until Red Light.");
        }
    }

    // Public method to be called manually (e.g. via finish line triggers or buttons) to end/win the game
    public void WinGame()
    {
        if (!isGameActive) return;
        Debug.Log("[RedLightGreenLight] WinGame called manually. Game Won.");
        OnGameWon?.Invoke();
        EndGame();
    }

    #region IGames Implementation
    public void StartGame()
    {
        isGameActive = true;
        Debug.Log("[RedLightGreenLight] StartGame called.");
        OnGameStart?.Invoke();
        TransitionToGreenLight();
    }

    public void RestartGame()
    {
        Debug.Log("[RedLightGreenLight] RestartGame called.");
        isGameActive = false;
        currentState = GameState.Inactive;
        stateTimer = 0f;
        StartGame();
    }

    public void EndGame()
    {
        Debug.Log("[RedLightGreenLight] EndGame called.");
        isGameActive = false;
        currentState = GameState.Inactive;
        stateTimer = 0f;
        OnGameEnd?.Invoke();
    }
    #endregion
}
