using UnityEngine;
using UnityEngine.Video;
using UnityEngine.UI; // Required for RawImage
using System.Collections;

public class VideoFadeController : MonoBehaviour
{
    public VideoPlayer videoPlayer;
    public RawImage displayImage; // Or use 'Image' if you aren't using a Render Texture
    public float fadeDuration = 1.0f;

    void Awake()
    {
        videoPlayer.loopPointReached += OnVideoFinished;
    }

    void OnVideoFinished(VideoPlayer source)
    {
        StartCoroutine(FadeOutImage());
    }

    IEnumerator FadeOutImage()
    {
        float time = 0;
        Color startColor = displayImage.color;

        while (time < fadeDuration)
        {
            time += Time.deltaTime;
            float newAlpha = Mathf.Lerp(1.0f, 0.0f, time / fadeDuration);

            // We apply the alpha back to the image color
            displayImage.color = new Color(startColor.r, startColor.g, startColor.b, newAlpha);
            yield return null;
        }

        // Ensure it's fully invisible and disable the object
        displayImage.color = new Color(startColor.r, startColor.g, startColor.b, 0);
        gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        if (videoPlayer != null)
            videoPlayer.loopPointReached -= OnVideoFinished;
    }
}