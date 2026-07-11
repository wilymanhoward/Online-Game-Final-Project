using UnityEngine;
using UnityEngine.Timeline;
using UnityEngine.Playables;
using Cinemachine;
using UnityEditor;
using System.Linq;

/// <summary>
/// Run this from the Unity menu: Tools > Build Cinematic Timeline
/// It populates Timeline.playable with a 3-shot Cinemachine track:
///   vcam1 (0-8s) -> vcam2 (7-14s) -> vcam1 (13-20s)
/// and wires the CinematicController on the Timeline GameObject.
/// </summary>
public class CinematicTimelineBuilder : MonoBehaviour
{
    [MenuItem("Tools/Build Cinematic Timeline")]
    public static void Build()
    {
        // ── 1. Load / create the TimelineAsset ──────────────────────────────
        const string timelinePath = "Assets/Scenes/Timeline.playable";
        TimelineAsset timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(timelinePath);
        if (timeline == null)
        {
            timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            AssetDatabase.CreateAsset(timeline, timelinePath);
        }

        // Clear old tracks
        foreach (var t in timeline.GetRootTracks().ToList())
            timeline.DeleteTrack(t);

        // ── 2. Add Cinemachine track ─────────────────────────────────────────
        CinemachineTrack cmTrack = timeline.CreateTrack<CinemachineTrack>(null, "Cinemachine Track");

        // Shot 1 – vcam1, 0 – 8 s (ease out 1.5 s into vcam2)
        TimelineClip c1 = cmTrack.CreateClip<CinemachineShot>();
        c1.start = 0; c1.duration = 8;
        c1.displayName = "vcam1 (intro)";
        c1.easeInDuration = 0; c1.easeOutDuration = 1.5;
        CinemachineShot s1 = (CinemachineShot)c1.asset;

        // Shot 2 – vcam2, 7 – 14 s (blends each side)
        TimelineClip c2 = cmTrack.CreateClip<CinemachineShot>();
        c2.start = 7; c2.duration = 7;
        c2.displayName = "vcam2 (mid)";
        c2.easeInDuration = 1.5; c2.easeOutDuration = 1.5;
        CinemachineShot s2 = (CinemachineShot)c2.asset;

        // Shot 3 – vcam1 again, 13 – 20 s
        TimelineClip c3 = cmTrack.CreateClip<CinemachineShot>();
        c3.start = 13; c3.duration = 7;
        c3.displayName = "vcam1 (outro)";
        c3.easeInDuration = 1.5; c3.easeOutDuration = 0;
        CinemachineShot s3 = (CinemachineShot)c3.asset;

        EditorUtility.SetDirty(timeline);
        AssetDatabase.SaveAssets();

        // ── 3. Find PlayableDirector and bind ───────────────────────────────
        GameObject timelineGO = GameObject.Find("Timeline");
        if (timelineGO == null)
        {
            Debug.LogError("[CinematicTimelineBuilder] No GameObject named 'Timeline' found in scene!");
            return;
        }

        PlayableDirector director = timelineGO.GetComponent<PlayableDirector>();
        if (director == null)
        {
            Debug.LogError("[CinematicTimelineBuilder] No PlayableDirector found on 'Timeline' GameObject!");
            return;
        }

        director.playableAsset = timeline;

        // Find virtual cameras
        CinemachineVirtualCamera vcam1 = null, vcam2 = null;
        foreach (var vc in UnityEngine.Object.FindObjectsOfType<CinemachineVirtualCamera>())
        {
            if (vc.name == "CM vcam1" && vcam1 == null) vcam1 = vc;
            if (vc.name == "CM vcam2" && vcam2 == null) vcam2 = vc;
        }

        if (vcam1 == null || vcam2 == null)
        {
            Debug.LogError($"[CinematicTimelineBuilder] Could not find vcams. vcam1={vcam1}, vcam2={vcam2}");
            return;
        }

        // Assign new GUIDs and bind to director
        s1.VirtualCamera.exposedName = System.Guid.NewGuid().ToString();
        s2.VirtualCamera.exposedName = System.Guid.NewGuid().ToString();
        s3.VirtualCamera.exposedName = System.Guid.NewGuid().ToString();

        director.SetReferenceValue(s1.VirtualCamera.exposedName, vcam1);
        director.SetReferenceValue(s2.VirtualCamera.exposedName, vcam2);
        director.SetReferenceValue(s3.VirtualCamera.exposedName, vcam1);

        // ── 4. Add CinematicController to the Timeline GameObject ────────────
        CinematicController ctrl = timelineGO.GetComponent<CinematicController>();
        if (ctrl == null)
            ctrl = timelineGO.AddComponent<CinematicController>();

        // Assign the cinematicCamera field to the "Camera" object in scene
        GameObject cinematicCam = GameObject.Find("Camera");
        if (cinematicCam != null)
        {
            var so = new SerializedObject(ctrl);
            so.FindProperty("playableDirector").objectReferenceValue = director;
            so.FindProperty("cinematicCamera").objectReferenceValue = cinematicCam;
            so.ApplyModifiedProperties();
        }

        EditorUtility.SetDirty(director);
        EditorUtility.SetDirty(timelineGO);
        AssetDatabase.SaveAssets();

        // Save the scene
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();

        Debug.Log("[CinematicTimelineBuilder] ✅ Done! Timeline: vcam1(0-8s) → vcam2(7-14s) → vcam1(13-20s). CinematicController attached.");
    }
}
