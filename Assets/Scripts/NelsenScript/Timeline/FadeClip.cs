using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[System.Serializable]
public class FadeClip : PlayableAsset, ITimelineClipAsset
{
    public Color fadeColor = Color.black;
    [Range(0f, 1f)] public float startAlpha = 0f;
    [Range(0f, 1f)] public float endAlpha = 1f;

    public ClipCaps clipCaps
    {
        get { return ClipCaps.Blending; }
    }

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var playable = ScriptPlayable<FadeBehaviour>.Create(graph);
        FadeBehaviour behaviour = playable.GetBehaviour();
        
        behaviour.fadeColor = fadeColor;
        behaviour.startAlpha = startAlpha;
        behaviour.endAlpha = endAlpha;

        return playable;
    }
}
