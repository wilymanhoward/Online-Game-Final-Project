using System.Collections;
using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// Manages the intro cinematic: hides players, disables input,
/// plays the timeline, then restores everything when it finishes.
/// </summary>
public class CinematicController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The PlayableDirector that plays the Timeline asset.")]
    [SerializeField] private PlayableDirector playableDirector;

    [Header("Player Hiding")]
    [Tooltip("Names of root GameObjects to hide during the cinematic.")]
    [SerializeField] private string[] playerNames = { "Player1", "Player2" };

    [Header("Cinemachine Camera")]
    [Tooltip("The Camera with CinemachineBrain – enabled during cinematic, then disabled when done.")]
    [SerializeField] private GameObject cinematicCamera;

    // Runtime-found players
    private GameObject[] hiddenPlayers;

    private void Start()
    {
        if (playableDirector == null)
            playableDirector = GetComponent<PlayableDirector>();

        // Subscribe to timeline finish event
        if (playableDirector != null)
        {
            playableDirector.stopped += OnTimelineFinished;
            StartCinematic();
        }
    }

    private void OnDestroy()
    {
        if (playableDirector != null)
            playableDirector.stopped -= OnTimelineFinished;
    }

    // ──────────────── Cinematic Start ────────────────

    private void StartCinematic()
    {
        // 1. Find and hide players
        var found = new System.Collections.Generic.List<GameObject>();
        foreach (string pName in playerNames)
        {
            GameObject p = GameObject.Find(pName);
            if (p != null)
            {
                p.SetActive(false);
                found.Add(p);
            }
        }
        hiddenPlayers = found.ToArray();

        // 2. Disable all FirstPersonController & PlayerInteract (also disables CharacterController movement)
        SetPlayersInputEnabled(false);

        // 3. Show the cinematic camera
        if (cinematicCamera != null)
            cinematicCamera.SetActive(true);

        // 4. Play the timeline
        if (playableDirector != null)
        {
            playableDirector.time = 0;
            playableDirector.Play();
        }

        // 5. Lock cursor during cinematic
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // ──────────────── Cinematic End ────────────────

    private void OnTimelineFinished(PlayableDirector _)
    {
        // 1. Restore hidden players
        if (hiddenPlayers != null)
        {
            foreach (var p in hiddenPlayers)
            {
                if (p != null) p.SetActive(true);
            }
        }

        // 2. Re-enable player input
        SetPlayersInputEnabled(true);

        // 3. Hide the cinematic camera so CinemachineBrain no longer overrides the player camera
        if (cinematicCamera != null)
            cinematicCamera.SetActive(false);

        // 4. Destroy ourselves – cinematic is done
        Destroy(gameObject);
    }

    // ──────────────── Helpers ────────────────

    private void SetPlayersInputEnabled(bool enabled)
    {
        // Find all FirstPersonControllers in the scene (handles both players)
        FirstPersonController[] fpcs = FindObjectsOfType<FirstPersonController>(true);
        foreach (var fpc in fpcs)
        {
            fpc.enabled = enabled;
        }

        // Also enable/disable CharacterControllers to prevent any ghost movement
        CharacterController[] ccs = FindObjectsOfType<CharacterController>(true);
        foreach (var cc in ccs)
        {
            cc.enabled = enabled;
        }
    }
}
