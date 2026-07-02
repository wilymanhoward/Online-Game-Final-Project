using UnityEngine;
using UnityEditor;
using Photon.Pun;

public class CreateThrowablePrefab : EditorWindow
{
    [MenuItem("Tools/Create Throwable Prefab")]
    public static void CreatePrefab()
    {
        // 1. Create a temporary GameObject
        GameObject rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        rock.name = "ThrowableRock";
        rock.transform.localScale = new Vector3(0.25f, 0.25f, 0.25f); // Make it rock-sized

        // 2. Add Rigidbody
        Rigidbody rb = rock.GetComponent<Rigidbody>();
        if (rb == null) rb = rock.AddComponent<Rigidbody>();
        rb.mass = 1.0f;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        // 3. Setup Material
        Renderer renderer = rock.GetComponent<Renderer>();
        if (renderer != null)
        {
            // Give it a stone-like dark gray color
            Material rockMat = new Material(Shader.Find("Standard"));
            rockMat.color = new Color(0.3f, 0.3f, 0.3f);
            renderer.sharedMaterial = rockMat;
            AssetDatabase.CreateAsset(rockMat, "Assets/Resources/ThrowableRockMaterial.mat");
        }

        // 4. Add Photon View
        PhotonView pv = rock.AddComponent<PhotonView>();
        
        // 5. Add Photon Transform View to sync movement
        PhotonTransformView ptv = rock.AddComponent<PhotonTransformView>();
        // Add to observed components list
        pv.ObservedComponents = new System.Collections.Generic.List<Component> { ptv };

        // 6. Add ThrowableObject script
        rock.AddComponent<ThrowableObject>();

        // 7. Ensure Resources folder exists
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
        {
            AssetDatabase.CreateFolder("Assets", "Resources");
        }

        // 8. Save as Prefab
        string localPath = "Assets/Resources/ThrowableRock.prefab";
        localPath = AssetDatabase.GenerateUniqueAssetPath(localPath);
        
        PrefabUtility.SaveAsPrefabAssetAndConnect(rock, localPath, InteractionMode.UserAction);
        
        // Clean up scene object
        DestroyImmediate(rock);

        Debug.Log("Successfully created ThrowableRock prefab at: " + localPath);
    }
}
