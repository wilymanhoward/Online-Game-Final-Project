using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public class MainMenuSetup : EditorWindow
{
    [MenuItem("Egypt Multiplayer/Setup Main Menu Scene")]
    public static void SetupLobby()
    {
        // 1. Create a new scene for the main menu
        string scenePath = "Assets/Scenes/MainMenu.unity";
        
        // Ensure Scenes directory exists
        if (!System.IO.Directory.Exists("Assets/Scenes"))
        {
            System.IO.Directory.CreateDirectory("Assets/Scenes");
        }

        // Create the scene
        var newScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        
        // Create LobbyManager GameObject
        GameObject lobbyManagerObj = new GameObject("LobbyManager");
        lobbyManagerObj.AddComponent<LobbyManager>();

        // Save Scene
        EditorSceneManager.SaveScene(newScene, scenePath);
        AssetDatabase.SaveAssets();

        // 2. Setup Scenes in Build Settings
        string sampleScenePath = "Assets/Scenes/SampleScene.unity";
        
        // Check if scenes exist
        var scenesList = new System.Collections.Generic.List<EditorBuildSettingsScene>();
        
        if (System.IO.File.Exists(scenePath))
        {
            scenesList.Add(new EditorBuildSettingsScene(scenePath, true));
        }
        else
        {
            Debug.LogError("MainMenu scene file not found!");
        }

        if (System.IO.File.Exists(sampleScenePath))
        {
            scenesList.Add(new EditorBuildSettingsScene(sampleScenePath, true));
        }
        else
        {
            Debug.LogWarning("SampleScene not found at Assets/Scenes/SampleScene.unity. Checking other locations...");
            string[] guids = AssetDatabase.FindAssets("SampleScene t:Scene");
            if (guids.Length > 0)
            {
                string foundPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                scenesList.Add(new EditorBuildSettingsScene(foundPath, true));
                Debug.Log("Found SampleScene at: " + foundPath);
            }
        }

        EditorBuildSettings.scenes = scenesList.ToArray();

        Debug.Log("Successfully created MainMenu scene and added scenes to Build Settings!");
        EditorUtility.DisplayDialog("Setup Complete", "Successfully created MainMenu scene with LobbyManager and configured Build Settings!\n\nScenes in Build:\n1. MainMenu\n2. SampleScene", "OK");
    }
}
