using UnityEngine;
using UnityEditor;

public class BuildPlayerTorch : MonoBehaviour
{
    [MenuItem("Tools/Attach Torch to Player Prefab")]
    public static void BuildTorch()
    {
        string prefabPath = "Assets/Resources/SM_Chr_Mummy_03.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);

        if (root == null)
        {
            Debug.LogError("Could not find SM_Chr_Mummy_03 prefab in Resources.");
            return;
        }

        // Find the hands
        Transform handL = FindDeepChild(root.transform, "Hand_L");
        Transform handR = FindDeepChild(root.transform, "Hand_R");

        if (handL == null)
        {
            Debug.LogError("Could not find Hand_L bone in player prefab.");
            PrefabUtility.UnloadPrefabContents(root);
            return;
        }

        // Clean up old right-hand torch if present
        if (handR != null)
        {
            Transform oldR = handR.Find("SM_Prop_Torch_05");
            if (oldR != null)
            {
                Debug.Log("Removing old right-hand torch.");
                DestroyImmediate(oldR.gameObject);
            }
        }

        // Clean up old left-hand torch if present
        Transform existing = handL.Find("SM_Prop_Torch_05");
        if (existing != null)
        {
            Debug.LogWarning("SM_Prop_Torch_05 already exists on player prefab under Hand_L. Destroying to rebuild cleanly.");
            DestroyImmediate(existing.gameObject);
        }

        // Load SM_Prop_Torch_05 prefab
        string torchPrefabPath = "Assets/LargeAssets/Synty/PolygonAncientEgypt/Prefabs/Props/SM_Prop_Torch_05.prefab";
        GameObject torchPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(torchPrefabPath);
        if (torchPrefab == null)
        {
            Debug.LogError("Could not load torch prefab from: " + torchPrefabPath);
            PrefabUtility.UnloadPrefabContents(root);
            return;
        }

        // Instantiate torch under Hand_L
        GameObject torchObj = PrefabUtility.InstantiatePrefab(torchPrefab) as GameObject;
        torchObj.name = "SM_Prop_Torch_05";
        torchObj.transform.SetParent(handL, false);

        // Position & align the torch handle perfectly inside the mummy's left hand palm
        torchObj.transform.localPosition = new Vector3(-0.076f, -0.02f, -0.004f);
        torchObj.transform.localRotation = Quaternion.Euler(15f, -90f, -100f);
        torchObj.transform.localScale = Vector3.one;

        // Load FX_Fire_01 prefab
        string firePrefabPath = "Assets/LargeAssets/Synty/PolygonAncientEgypt/Prefabs/FX/FX_Fire_01.prefab";
        GameObject firePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(firePrefabPath);
        if (firePrefab != null)
        {
            GameObject fireObj = PrefabUtility.InstantiatePrefab(firePrefab) as GameObject;
            fireObj.name = "FX_Fire_01";
            fireObj.transform.SetParent(torchObj.transform, false);
            fireObj.transform.localPosition = new Vector3(0f, 0.4f, 0f); // Centered inside the cup
            fireObj.transform.localRotation = Quaternion.identity;
            fireObj.transform.localScale = new Vector3(0.25f, 0.25f, 0.25f);

            // Set Simulation Space to Local and scaling mode to Hierarchy so it scales down perfectly
            var allPS = fireObj.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in allPS)
            {
                var main = ps.main;
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            }
        }
        else
        {
            Debug.LogWarning("Could not load fire particle effect from: " + firePrefabPath);
        }

        // Add a Point Light at the tip of the torch
        GameObject lightObj = new GameObject("TorchLight");
        lightObj.transform.SetParent(torchObj.transform, false);
        lightObj.transform.localPosition = new Vector3(0f, 0.45f, 0f);
        lightObj.transform.localRotation = Quaternion.identity;

        Light lightComponent = lightObj.AddComponent<Light>();
        lightComponent.type = LightType.Point;
        lightComponent.color = new Color(1f, 0.55f, 0.15f); // Warm orange flame glow
        lightComponent.range = 15f;
        lightComponent.intensity = 2.5f;
        lightComponent.shadows = LightShadows.Soft;

        // Disable the torch object by default
        torchObj.SetActive(false);

        // Save back to prefab
        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        PrefabUtility.UnloadPrefabContents(root);

        Debug.Log("Successfully attached and configured torch inside SM_Chr_Mummy_03 player prefab under Hand_L!");
    }

    private static Transform FindDeepChild(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            Transform result = FindDeepChild(child, name);
            if (result != null) return result;
        }
        return null;
    }
}
