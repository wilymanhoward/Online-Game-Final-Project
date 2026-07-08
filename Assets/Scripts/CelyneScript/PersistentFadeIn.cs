using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// Keeps a black-screen CanvasGroup alive across a scene load (via DontDestroyOnLoad)
// and fades it out once the target scene has had time to finish spawning/setup,
// so the player never sees a stray camera view before their own camera activates.
public class PersistentFadeIn : MonoBehaviour
{
    public float fadeDuration = 1f;
    public string targetSceneName = "Puzzle1";
    public float extraDelay = 0.5f;

    private CanvasGroup canvasGroup;
    private bool armed = false;

    public void Arm()
    {
        if (armed) return;
        armed = true;

        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();

        DontDestroyOnLoad(transform.root.gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != targetSceneName) return;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        StartCoroutine(FadeOutAndDestroy());
    }

    private IEnumerator FadeOutAndDestroy()
    {
        if (extraDelay > 0f) yield return new WaitForSeconds(extraDelay);

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = 1f - Mathf.Clamp01(elapsed / fadeDuration);
            yield return null;
        }
        canvasGroup.alpha = 0f;

        Destroy(transform.root.gameObject);
    }
}
