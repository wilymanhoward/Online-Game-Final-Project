using UnityEngine;
using UnityEditor;

public class BuildBandagePrefabUI : MonoBehaviour
{
    [MenuItem("Tools/Build Mummy Bandage Prefab UI")]
    public static void BuildUI()
    {
        string prefabPath = "Assets/Resources/SM_Chr_Mummy_03.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);

        if (root == null)
        {
            Debug.LogError("Could not find SM_Chr_Mummy_03 prefab in Resources.");
            return;
        }

        // Check if canvas already exists
        Transform existing = root.transform.Find("BandageOverlayCanvas");
        if (existing != null)
        {
            Debug.LogWarning("BandageOverlayCanvas already exists on prefab. Destroying to rebuild cleanly.");
            DestroyImmediate(existing.gameObject);
        }

        // 1. Create Canvas GameObject
        GameObject canvasObj = new GameObject("BandageOverlayCanvas");
        canvasObj.transform.SetParent(root.transform, false);
        
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 998; // Just under Death UI
        
        var scaler = canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        
        canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        // 2. Create container panel
        GameObject containerObj = new GameObject("Container");
        containerObj.transform.SetParent(canvasObj.transform, false);
        var rectContainer = containerObj.AddComponent<RectTransform>();
        rectContainer.anchorMin = Vector2.zero;
        rectContainer.anchorMax = Vector2.one;
        rectContainer.sizeDelta = Vector2.zero;

        // 3. Configure 7 medium-large messy horizontal-ish wraps spanning edge-to-edge (X = 0f, Width = 3400f)
        var configs = new[]
        {
            new { name = "A1", pos = new Vector2(0f, 380f), size = new Vector2(3400f, 180f), rot = 8f, slideDir = -1f },
            new { name = "A2", pos = new Vector2(0f, -380f), size = new Vector2(3400f, 180f), rot = -6f, slideDir = 1f },
            new { name = "A3", pos = new Vector2(0f, 200f), size = new Vector2(3400f, 150f), rot = -9f, slideDir = -1f },
            new { name = "A4", pos = new Vector2(0f, -200f), size = new Vector2(3400f, 150f), rot = 7f, slideDir = 1f },
            new { name = "A5", pos = new Vector2(0f, 0f), size = new Vector2(3400f, 140f), rot = -3f, slideDir = -1f },
            new { name = "A6", pos = new Vector2(0f, 90f), size = new Vector2(3400f, 130f), rot = -15f, slideDir = 1f },
            new { name = "A7", pos = new Vector2(0f, -90f), size = new Vector2(3400f, 130f), rot = 16f, slideDir = -1f }
        };

        foreach (var cfg in configs)
        {
            // Root GameObject for Strip
            GameObject stripObj = new GameObject(cfg.name);
            stripObj.transform.SetParent(containerObj.transform, false);
            RectTransform rect = stripObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = cfg.pos;
            rect.sizeDelta = cfg.size;
            rect.localRotation = Quaternion.Euler(0f, 0f, cfg.rot);

            // Add slide direction controller script
            var stripCtrl = stripObj.AddComponent<MummyBandageStrip>();
            stripCtrl.slideDirection = cfg.slideDir;

            // Layer 1: Dark sandy shadow/outline (Dark weathered brown)
            var bgImage = stripObj.AddComponent<UnityEngine.UI.Image>();
            bgImage.color = new Color(0.28f, 0.22f, 0.17f, 0.98f);

            // Layer 2: Main bandage wrap (Ancient dusty sand beige)
            GameObject mainObj = new GameObject("MainWrap");
            mainObj.transform.SetParent(stripObj.transform, false);
            var rectMain = mainObj.AddComponent<RectTransform>();
            rectMain.anchorMin = Vector2.zero;
            rectMain.anchorMax = Vector2.one;
            rectMain.sizeDelta = new Vector2(0f, -12f); // margin top/bottom
            var mainImage = mainObj.AddComponent<UnityEngine.UI.Image>();
            mainImage.color = new Color(0.68f, 0.61f, 0.52f, 1f);

            // Layer 3: Highlight fold (Dusty cream)
            GameObject highlightObj = new GameObject("HighlightFold");
            highlightObj.transform.SetParent(mainObj.transform, false);
            var rectHighlight = highlightObj.AddComponent<RectTransform>();
            rectHighlight.anchorMin = new Vector2(0f, 0.15f);
            rectHighlight.anchorMax = new Vector2(1f, 0.35f);
            rectHighlight.sizeDelta = Vector2.zero;
            var highlightImage = highlightObj.AddComponent<UnityEngine.UI.Image>();
            highlightImage.color = new Color(0.78f, 0.72f, 0.64f, 1f);

            // Layer 4: Overlapping secondary strip (Medium weathered sand)
            GameObject overlapObj = new GameObject("OverlapStrip");
            overlapObj.transform.SetParent(mainObj.transform, false);
            var rectOverlap = overlapObj.AddComponent<RectTransform>();
            rectOverlap.anchorMin = new Vector2(0f, 0.5f);
            rectOverlap.anchorMax = new Vector2(1f, 0.95f);
            rectOverlap.sizeDelta = Vector2.zero;
            rectOverlap.localRotation = Quaternion.Euler(0f, 0f, -0.8f);
            var overlapImage = overlapObj.AddComponent<UnityEngine.UI.Image>();
            overlapImage.color = new Color(0.62f, 0.55f, 0.46f, 1f);

            // Layer 5: Procedural horizontal gauze grain lines (Fiber grain)
            float[] threadYPositions = { 0.22f, 0.42f, 0.62f, 0.82f };
            for (int i = 0; i < threadYPositions.Length; i++)
            {
                GameObject threadObj = new GameObject("ThreadLine_" + i);
                threadObj.transform.SetParent(mainObj.transform, false);
                var rectThread = threadObj.AddComponent<RectTransform>();
                rectThread.anchorMin = new Vector2(0f, threadYPositions[i]);
                rectThread.anchorMax = new Vector2(1f, threadYPositions[i]);
                rectThread.sizeDelta = new Vector2(0f, 2.5f); // 2.5px thickness
                var threadImage = threadObj.AddComponent<UnityEngine.UI.Image>();
                threadImage.color = new Color(0.28f, 0.22f, 0.17f, 0.22f); // subtle dark brown horizontal threads
            }

            // Layer 6: Procedural vertical gauze cross-stitch weave (Cross threads)
            for (int i = 1; i < 20; i++)
            {
                float xPct = i / 20.0f;
                GameObject vertObj = new GameObject("VertThread_" + i);
                vertObj.transform.SetParent(mainObj.transform, false);
                var rectVert = vertObj.AddComponent<RectTransform>();
                rectVert.anchorMin = new Vector2(xPct, 0f);
                rectVert.anchorMax = new Vector2(xPct, 1f);
                rectVert.sizeDelta = new Vector2(1.5f, 0f); // 1.5px thickness
                var vertImage = vertObj.AddComponent<UnityEngine.UI.Image>();
                vertImage.color = new Color(0.28f, 0.22f, 0.17f, 0.12f); // ultra-subtle vertical cross threads
            }

            // Left Swallowtail V-Notch Cutout (clean low-poly geometric tear)
            GameObject cutL = new GameObject("Ripped_CutL");
            cutL.transform.SetParent(mainObj.transform, false);
            var rectL = cutL.AddComponent<RectTransform>();
            rectL.anchorMin = new Vector2(0f, 0.5f);
            rectL.anchorMax = new Vector2(0f, 0.5f);
            rectL.pivot = new Vector2(0.5f, 0.5f);
            rectL.anchoredPosition = new Vector2(5f, 0f);
            rectL.sizeDelta = new Vector2(90f, 90f);
            rectL.localRotation = Quaternion.Euler(0f, 0f, 45f);
            var imgL = cutL.AddComponent<UnityEngine.UI.Image>();
            imgL.color = new Color(0.28f, 0.22f, 0.17f, 0.98f);

            // Right Swallowtail V-Notch Cutout
            GameObject cutR = new GameObject("Ripped_CutR");
            cutR.transform.SetParent(mainObj.transform, false);
            var rectR = cutR.AddComponent<RectTransform>();
            rectR.anchorMin = new Vector2(1f, 0.5f);
            rectR.anchorMax = new Vector2(1f, 0.5f);
            rectR.pivot = new Vector2(0.5f, 0.5f);
            rectR.anchoredPosition = new Vector2(-5f, 0f);
            rectR.sizeDelta = new Vector2(90f, 90f);
            rectR.localRotation = Quaternion.Euler(0f, 0f, 45f);
            var imgR = cutR.AddComponent<UnityEngine.UI.Image>();
            imgR.color = new Color(0.28f, 0.22f, 0.17f, 0.98f);

            // Frayed loose threads extending from left edge (exactly 2 clean, low-poly threads)
            GameObject threadL1 = new GameObject("FrayThread_L1");
            threadL1.transform.SetParent(mainObj.transform, false);
            var rectTL1 = threadL1.AddComponent<RectTransform>();
            rectTL1.anchorMin = new Vector2(0f, 0.25f);
            rectTL1.anchorMax = new Vector2(0f, 0.25f);
            rectTL1.pivot = new Vector2(1f, 0.5f);
            rectTL1.anchoredPosition = new Vector2(-4f, 0f);
            rectTL1.sizeDelta = new Vector2(40f, 3f);
            rectTL1.localRotation = Quaternion.Euler(0f, 0f, 12f);
            var imgTL1 = threadL1.AddComponent<UnityEngine.UI.Image>();
            imgTL1.color = new Color(0.68f, 0.61f, 0.52f, 1.0f);

            GameObject threadL2 = new GameObject("FrayThread_L2");
            threadL2.transform.SetParent(mainObj.transform, false);
            var rectTL2 = threadL2.AddComponent<RectTransform>();
            rectTL2.anchorMin = new Vector2(0f, 0.75f);
            rectTL2.anchorMax = new Vector2(0f, 0.75f);
            rectTL2.pivot = new Vector2(1f, 0.5f);
            rectTL2.anchoredPosition = new Vector2(-6f, 0f);
            rectTL2.sizeDelta = new Vector2(35f, 2.5f);
            rectTL2.localRotation = Quaternion.Euler(0f, 0f, -15f);
            var imgTL2 = threadL2.AddComponent<UnityEngine.UI.Image>();
            imgTL2.color = new Color(0.62f, 0.55f, 0.46f, 0.85f);

            // Frayed loose threads extending from right edge
            GameObject threadR1 = new GameObject("FrayThread_R1");
            threadR1.transform.SetParent(mainObj.transform, false);
            var rectTR1 = threadR1.AddComponent<RectTransform>();
            rectTR1.anchorMin = new Vector2(1f, 0.25f);
            rectTR1.anchorMax = new Vector2(1f, 0.25f);
            rectTR1.pivot = new Vector2(0f, 0.5f);
            rectTR1.anchoredPosition = new Vector2(4f, 0f);
            rectTR1.sizeDelta = new Vector2(40f, 3f);
            rectTR1.localRotation = Quaternion.Euler(0f, 0f, -12f);
            var imgTR1 = threadR1.AddComponent<UnityEngine.UI.Image>();
            imgTR1.color = new Color(0.68f, 0.61f, 0.52f, 1.0f);

            GameObject threadR2 = new GameObject("FrayThread_R2");
            threadR2.transform.SetParent(mainObj.transform, false);
            var rectTR2 = threadR2.AddComponent<RectTransform>();
            rectTR2.anchorMin = new Vector2(1f, 0.75f);
            rectTR2.anchorMax = new Vector2(1f, 0.75f);
            rectTR2.pivot = new Vector2(0f, 0.5f);
            rectTR2.anchoredPosition = new Vector2(6f, 0f);
            rectTR2.sizeDelta = new Vector2(35f, 2.5f);
            rectTR2.localRotation = Quaternion.Euler(0f, 0f, 15f);
            var imgTR2 = threadR2.AddComponent<UnityEngine.UI.Image>();
            imgTR2.color = new Color(0.62f, 0.55f, 0.46f, 0.85f);
        }

        // Save back to prefab
        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        PrefabUtility.UnloadPrefabContents(root);

        Debug.Log("Successfully created and saved BandageOverlayCanvas hierarchy inside SM_Chr_Mummy_03 prefab!");
    }

    [MenuItem("Tools/Apply Weathered Style to Existing Bandages")]
    public static void ApplyStyleToExisting()
    {
        string prefabPath = "Assets/Resources/SM_Chr_Mummy_03.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        if (root == null)
        {
            Debug.LogError("Could not find SM_Chr_Mummy_03 prefab in Resources.");
            return;
        }

        Transform canvasTrans = root.transform.Find("BandageOverlayCanvas");
        if (canvasTrans == null)
        {
            Debug.LogError("Could not find BandageOverlayCanvas under prefab root. Please make sure the canvas exists before applying style.");
            PrefabUtility.UnloadPrefabContents(root);
            return;
        }

        Transform container = canvasTrans.Find("Container");
        if (container == null) container = canvasTrans;

        foreach (Transform strip in container)
        {
            // Update outline/shadow
            var bgImage = strip.GetComponent<UnityEngine.UI.Image>();
            if (bgImage != null)
            {
                bgImage.color = new Color(0.28f, 0.22f, 0.17f, 0.98f);
            }

            Transform mainWrap = strip.Find("MainWrap");
            if (mainWrap != null)
            {
                // Main wrap color
                var mainImage = mainWrap.GetComponent<UnityEngine.UI.Image>();
                if (mainImage != null)
                {
                    mainImage.color = new Color(0.68f, 0.61f, 0.52f, 1f);
                }

                // Highlight fold
                Transform highlight = mainWrap.Find("HighlightFold");
                if (highlight != null)
                {
                    var highlightImage = highlight.GetComponent<UnityEngine.UI.Image>();
                    if (highlightImage != null)
                    {
                        highlightImage.color = new Color(0.78f, 0.72f, 0.64f, 1f);
                    }
                }

                // Overlap strip
                Transform overlap = mainWrap.Find("OverlapStrip");
                if (overlap != null)
                {
                    var overlapImage = overlap.GetComponent<UnityEngine.UI.Image>();
                    if (overlapImage != null)
                    {
                        overlapImage.color = new Color(0.62f, 0.55f, 0.46f, 1f);
                    }
                }

                // Clear any existing thread lines, cutouts, or frayed thread objects
                for (int i = mainWrap.childCount - 1; i >= 0; i--)
                {
                    Transform child = mainWrap.GetChild(i);
                    if (child.name.StartsWith("ThreadLine_") || 
                        child.name.StartsWith("VertThread_") ||
                        child.name.StartsWith("Ripped_") ||
                        child.name.StartsWith("FrayThread_"))
                    {
                        DestroyImmediate(child.gameObject);
                    }
                }

                // Add horizontal threads
                float[] threadYPositions = { 0.22f, 0.42f, 0.62f, 0.82f };
                for (int j = 0; j < threadYPositions.Length; j++)
                {
                    GameObject threadObj = new GameObject("ThreadLine_" + j);
                    threadObj.transform.SetParent(mainWrap, false);
                    var rectThread = threadObj.AddComponent<RectTransform>();
                    rectThread.anchorMin = new Vector2(0f, threadYPositions[j]);
                    rectThread.anchorMax = new Vector2(1f, threadYPositions[j]);
                    rectThread.sizeDelta = new Vector2(0f, 2.5f);
                    var threadImage = threadObj.AddComponent<UnityEngine.UI.Image>();
                    threadImage.color = new Color(0.28f, 0.22f, 0.17f, 0.22f);
                }

                // Add vertical threads
                for (int j = 1; j < 20; j++)
                {
                    float xPct = j / 20.0f;
                    GameObject vertObj = new GameObject("VertThread_" + j);
                    vertObj.transform.SetParent(mainWrap, false);
                    var rectVert = vertObj.AddComponent<RectTransform>();
                    rectVert.anchorMin = new Vector2(xPct, 0f);
                    rectVert.anchorMax = new Vector2(xPct, 1f);
                    rectVert.sizeDelta = new Vector2(1.5f, 0f);
                    var vertImage = vertObj.AddComponent<UnityEngine.UI.Image>();
                    vertImage.color = new Color(0.28f, 0.22f, 0.17f, 0.12f);
                }

                // Left Swallowtail V-Notch Cutout (clean low-poly geometric tear)
                GameObject cutL = new GameObject("Ripped_CutL");
                cutL.transform.SetParent(mainWrap, false);
                var rectL = cutL.AddComponent<RectTransform>();
                rectL.anchorMin = new Vector2(0f, 0.5f);
                rectL.anchorMax = new Vector2(0f, 0.5f);
                rectL.pivot = new Vector2(0.5f, 0.5f);
                rectL.anchoredPosition = new Vector2(5f, 0f);
                rectL.sizeDelta = new Vector2(90f, 90f);
                rectL.localRotation = Quaternion.Euler(0f, 0f, 45f);
                var imgL = cutL.AddComponent<UnityEngine.UI.Image>();
                imgL.color = new Color(0.28f, 0.22f, 0.17f, 0.98f);

                // Right Swallowtail V-Notch Cutout
                GameObject cutR = new GameObject("Ripped_CutR");
                cutR.transform.SetParent(mainWrap, false);
                var rectR = cutR.AddComponent<RectTransform>();
                rectR.anchorMin = new Vector2(1f, 0.5f);
                rectR.anchorMax = new Vector2(1f, 0.5f);
                rectR.pivot = new Vector2(0.5f, 0.5f);
                rectR.anchoredPosition = new Vector2(-5f, 0f);
                rectR.sizeDelta = new Vector2(90f, 90f);
                rectR.localRotation = Quaternion.Euler(0f, 0f, 45f);
                var imgR = cutR.AddComponent<UnityEngine.UI.Image>();
                imgR.color = new Color(0.28f, 0.22f, 0.17f, 0.98f);

                // Frayed loose threads extending from left edge (exactly 2 clean, low-poly threads)
                GameObject threadL1 = new GameObject("FrayThread_L1");
                threadL1.transform.SetParent(mainWrap, false);
                var rectTL1 = threadL1.AddComponent<RectTransform>();
                rectTL1.anchorMin = new Vector2(0f, 0.25f);
                rectTL1.anchorMax = new Vector2(0f, 0.25f);
                rectTL1.pivot = new Vector2(1f, 0.5f);
                rectTL1.anchoredPosition = new Vector2(-4f, 0f);
                rectTL1.sizeDelta = new Vector2(40f, 3f);
                rectTL1.localRotation = Quaternion.Euler(0f, 0f, 12f);
                var imgTL1 = threadL1.AddComponent<UnityEngine.UI.Image>();
                imgTL1.color = new Color(0.68f, 0.61f, 0.52f, 1.0f);

                GameObject threadL2 = new GameObject("FrayThread_L2");
                threadL2.transform.SetParent(mainWrap, false);
                var rectTL2 = threadL2.AddComponent<RectTransform>();
                rectTL2.anchorMin = new Vector2(0f, 0.75f);
                rectTL2.anchorMax = new Vector2(0f, 0.75f);
                rectTL2.pivot = new Vector2(1f, 0.5f);
                rectTL2.anchoredPosition = new Vector2(-6f, 0f);
                rectTL2.sizeDelta = new Vector2(35f, 2.5f);
                rectTL2.localRotation = Quaternion.Euler(0f, 0f, -15f);
                var imgTL2 = threadL2.AddComponent<UnityEngine.UI.Image>();
                imgTL2.color = new Color(0.62f, 0.55f, 0.46f, 0.85f);

                // Frayed loose threads extending from right edge
                GameObject threadR1 = new GameObject("FrayThread_R1");
                threadR1.transform.SetParent(mainWrap, false);
                var rectTR1 = threadR1.AddComponent<RectTransform>();
                rectTR1.anchorMin = new Vector2(1f, 0.25f);
                rectTR1.anchorMax = new Vector2(1f, 0.25f);
                rectTR1.pivot = new Vector2(0f, 0.5f);
                rectTR1.anchoredPosition = new Vector2(4f, 0f);
                rectTR1.sizeDelta = new Vector2(40f, 3f);
                rectTR1.localRotation = Quaternion.Euler(0f, 0f, -12f);
                var imgTR1 = threadR1.AddComponent<UnityEngine.UI.Image>();
                imgTR1.color = new Color(0.68f, 0.61f, 0.52f, 1.0f);

                GameObject threadR2 = new GameObject("FrayThread_R2");
                threadR2.transform.SetParent(mainWrap, false);
                var rectTR2 = threadR2.AddComponent<RectTransform>();
                rectTR2.anchorMin = new Vector2(1f, 0.75f);
                rectTR2.anchorMax = new Vector2(1f, 0.75f);
                rectTR2.pivot = new Vector2(0f, 0.5f);
                rectTR2.anchoredPosition = new Vector2(6f, 0f);
                rectTR2.sizeDelta = new Vector2(35f, 2.5f);
                rectTR2.localRotation = Quaternion.Euler(0f, 0f, 15f);
                var imgTR2 = threadR2.AddComponent<UnityEngine.UI.Image>();
                imgTR2.color = new Color(0.62f, 0.55f, 0.46f, 0.85f);
            }
        }

        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        PrefabUtility.UnloadPrefabContents(root);
        Debug.Log("Successfully applied darker style and gauze weave texture to existing bandage structures without modifying positions!");
    }
}
