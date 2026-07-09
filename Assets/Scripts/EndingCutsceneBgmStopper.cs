using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

// Stops Puzzle1's background music ("BG AUDIO") the instant the Ending1/EndingCutscene
// PlayableDirector starts playing. Self-registers via RuntimeInitializeOnLoadMethod so it
// requires no GameObject wiring in the scene.
public static class EndingCutsceneBgmStopper
{
    private const string TargetSceneName = "Puzzle1";
    private const string BgmObjectName = "BG AUDIO";
    private const string DirectorObjectName = "Ending1";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != TargetSceneName) return;

        GameObject bgmObj = GameObject.Find(BgmObjectName);
        AudioSource bgmSource = bgmObj != null ? bgmObj.GetComponent<AudioSource>() : null;
        if (bgmSource == null) return;

        GameObject directorObj = GameObject.Find(DirectorObjectName);
        PlayableDirector director = directorObj != null ? directorObj.GetComponent<PlayableDirector>() : null;
        if (director == null) return;

        director.played += _ =>
        {
            if (bgmSource != null) bgmSource.Stop();
        };
    }
}
