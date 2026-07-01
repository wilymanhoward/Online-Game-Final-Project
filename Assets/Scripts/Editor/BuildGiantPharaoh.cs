using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

public static class BuildGiantPharaoh
{
    [MenuItem("Tools/Build Giant Pharaoh")]
    public static void Execute()
    {
        // 1. Create/Retrieve PharaohAnimatorController
        string controllerPath = "Assets/Animation/PharaohAnimatorController.controller";
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        }

        // Setup States and Transitions
        var rootStateMachine = controller.layers[0].stateMachine;
        
        // Clear old states to rebuild cleanly
        for (int i = rootStateMachine.states.Length - 1; i >= 0; i--)
        {
            rootStateMachine.RemoveState(rootStateMachine.states[i].state);
        }
        
        // Clear parameters
        for (int i = controller.parameters.Length - 1; i >= 0; i--)
        {
            controller.RemoveParameter(i);
        }

        // Add speed parameter
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);

        // Load Clips
        AnimationClip idleClip = LoadClipFromFBX("Assets/Photon/PhotonUnityNetworking/Demos/Shared Assets/Animations/HumanoidIdle.fbx");
        AnimationClip walkClip = LoadClipFromFBX("Assets/Photon/PhotonUnityNetworking/Demos/Shared Assets/Animations/HumanoidWalk.fbx");
        AnimationClip jumpAttackClip = LoadClipFromFBX("Assets/Animation/Heraklios By A. Dizon@Jump Attack.fbx");
        if (jumpAttackClip == null)
        {
            jumpAttackClip = LoadClipFromFBX("Assets/Animation/Heraklios By A. Dizon@Jump.fbx");
        }

        AnimatorState idleState = rootStateMachine.AddState("Idle");
        idleState.motion = idleClip;

        AnimatorState walkState = rootStateMachine.AddState("Walk");
        walkState.motion = walkClip;

        AnimatorState jumpAttackState = rootStateMachine.AddState("JumpAttack");
        jumpAttackState.motion = jumpAttackClip;

        // Transitions
        var toWalk = idleState.AddTransition(walkState);
        toWalk.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
        toWalk.duration = 0.25f;

        var toIdle = walkState.AddTransition(idleState);
        toIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
        toIdle.duration = 0.25f;

        var toIdleFromJump = jumpAttackState.AddTransition(idleState);
        toIdleFromJump.hasExitTime = true;
        toIdleFromJump.exitTime = 1.0f;
        toIdleFromJump.duration = 0.25f;

        EditorUtility.SetDirty(controller);


        // 2. Instantiate SM_Chr_Pharaoh_01 in active scene
        string pharaohPrefabPath = "Assets/LargeAssets/Synty/PolygonAncientEgypt/Prefabs/Characters/SM_Chr_Pharaoh_01.prefab";
        GameObject pharaohPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(pharaohPrefabPath);
        if (pharaohPrefab == null)
        {
            Debug.LogError("Pharaoh prefab not found at: " + pharaohPrefabPath);
            return;
        }

        // Delete any existing Giant Pharaoh in the scene
        GameObject existingPharaoh = GameObject.Find("Giant Pharaoh Enemy");
        if (existingPharaoh != null)
        {
            Object.DestroyImmediate(existingPharaoh);
        }

        GameObject pharaohObj = (GameObject)PrefabUtility.InstantiatePrefab(pharaohPrefab);
        pharaohObj.name = "Giant Pharaoh Enemy";
        pharaohObj.transform.position = new Vector3(0f, 0.5f, 20f);
        pharaohObj.transform.localScale = new Vector3(8f, 8f, 8f);

        // Add CharacterController
        CharacterController charController = pharaohObj.GetComponent<CharacterController>();
        if (charController == null)
        {
            charController = pharaohObj.AddComponent<CharacterController>();
        }
        // Base values before scale multiplication
        charController.center = new Vector3(0f, 1f, 0f);
        charController.height = 2.0f;
        charController.radius = 0.12f;

        // Add Animator Controller reference
        Animator animator = pharaohObj.GetComponent<Animator>();
        if (animator == null)
        {
            animator = pharaohObj.AddComponent<Animator>();
        }
        animator.runtimeAnimatorController = controller;

        // Find Hand_R and attach Giant Stick if missing
        Transform handR = null;
        foreach (Transform t in pharaohObj.GetComponentsInChildren<Transform>())
        {
            if (t.name == "Hand_R")
            {
                handR = t;
                break;
            }
        }
        if (handR != null)
        {
            Transform existingStick = handR.Find("Giant Stick");
            if (existingStick == null)
            {
                GameObject stickPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Giant Stick.prefab");
                if (stickPrefab != null)
                {
                    GameObject stickInstance = (GameObject)PrefabUtility.InstantiatePrefab(stickPrefab, handR);
                    stickInstance.name = "Giant Stick";
                    stickInstance.transform.localPosition = new Vector3(0f, -1.0f, 0f);
                    stickInstance.transform.localRotation = Quaternion.Euler(180f, 0f, 0f);
                    stickInstance.transform.localScale = new Vector3(2.98f, 3.29f, 1.74f);
                }
            }
        }
        
        // Add GiantPharaohAI script
        GiantPharaohAI pharaohAI = pharaohObj.GetComponent<GiantPharaohAI>();
        if (pharaohAI == null)
        {
            pharaohAI = pharaohObj.AddComponent<GiantPharaohAI>();
        }

        // Save Scene
        EditorUtility.SetDirty(pharaohObj);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(pharaohObj.scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(pharaohObj.scene);

        Debug.Log("Giant Pharaoh built and added to SampleScene successfully!");
    }

    private static AnimationClip LoadClipFromFBX(string path)
    {
        var assets = AssetDatabase.LoadAllAssetsAtPath(path);
        foreach (var asset in assets)
        {
            if (asset is AnimationClip clip && !clip.name.Contains("__preview__"))
            {
                return clip;
            }
        }
        return null;
    }
}
