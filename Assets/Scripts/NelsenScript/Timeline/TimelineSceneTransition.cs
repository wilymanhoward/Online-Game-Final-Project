using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using Photon.Pun;

public class TimelineSceneTransition : MonoBehaviour
{
    [Tooltip("The Playable Director playing the timeline.")]
    public PlayableDirector director;

    [Tooltip("The name of the scene to load when the timeline finishes.")]
    public string nextSceneName;

    private void OnEnable()
    {
        if (director != null)
        {
            director.stopped += OnTimelineStopped;
        }
    }

    private void OnDisable()
    {
        if (director != null)
        {
            director.stopped -= OnTimelineStopped;
        }
    }

    private void OnTimelineStopped(PlayableDirector obj)
    {
        if (string.IsNullOrEmpty(nextSceneName))
        {
            Debug.LogWarning("[TimelineSceneTransition] Next scene name is empty!");
            return;
        }

        Debug.Log($"[TimelineSceneTransition] Timeline finished. Loading scene: {nextSceneName}");

        if (PhotonNetwork.IsConnected && PhotonNetwork.InRoom)
        {
            // In Photon multiplayer, only the Master Client (host) should load the level.
            // PhotonNetwork.LoadLevel automatically loads it on all other clients in the room.
            if (PhotonNetwork.IsMasterClient)
            {
                PhotonNetwork.LoadLevel(nextSceneName);
            }
        }
        else
        {
            // Offline/Singleplayer fallback
            SceneManager.LoadScene(nextSceneName);
        }
    }
}
