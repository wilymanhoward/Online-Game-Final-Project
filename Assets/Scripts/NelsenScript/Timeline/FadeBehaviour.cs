using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.UI;

public class FadeBehaviour : PlayableBehaviour
{
    public Color fadeColor = Color.black;
    public float startAlpha = 0f;
    public float endAlpha = 1f;

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        if (playerData == null) return;

        CanvasGroup canvasGroup = playerData as CanvasGroup;
        Image image = null;

        // Fallback checks for the bound object type
        if (canvasGroup == null)
        {
            image = playerData as Image;
        }

        if (canvasGroup == null && image == null && playerData is GameObject go)
        {
            canvasGroup = go.GetComponent<CanvasGroup>();
            image = go.GetComponent<Image>();
        }

        // Calculate progress (from 0 to 1) based on clip time and duration
        float duration = (float)playable.GetDuration();
        float time = (float)playable.GetTime();
        float progress = duration > 0 ? Mathf.Clamp01(time / duration) : 1f;

        float currentAlpha = Mathf.Lerp(startAlpha, endAlpha, progress);

        // Apply alpha to bound CanvasGroup or Image
        if (canvasGroup != null)
        {
            canvasGroup.alpha = currentAlpha;
            // Prevent blocking raycasts when fully transparent
            if (canvasGroup.gameObject.activeSelf != (currentAlpha > 0f))
            {
                canvasGroup.gameObject.SetActive(currentAlpha > 0f);
            }
        }
        else if (image != null)
        {
            Color color = fadeColor;
            color.a = currentAlpha;
            image.color = color;
            // Prevent blocking raycasts when fully transparent
            if (image.gameObject.activeSelf != (currentAlpha > 0f))
            {
                image.gameObject.SetActive(currentAlpha > 0f);
            }
        }
    }
}
