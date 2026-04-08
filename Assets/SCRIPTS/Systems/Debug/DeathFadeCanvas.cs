using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// Placed on the ScreenFade prefab. Fades the child <see cref="Image"/> in to black,
/// fires a callback at peak opacity, fades back out, then destroys this GameObject.
/// Activated via <see cref="GameDataManager.SpawnFadeCanvas"/> — do not place in the scene manually.
/// </summary>
public class DeathFadeCanvas : MonoBehaviour
{
    /// <summary>Fired once the screen has fully faded to black, before the fade-out begins.</summary>
    public System.Action OnFadeComplete;

    private float fadeInDuration = 0.8f;
    private float holdDuration = 0.4f;
    private float fadeOutDuration = 0.8f;

    private Image _fadeImage;

    private void Awake()
    {
        gameObject.hideFlags = HideFlags.HideAndDontSave;

        _fadeImage = GetComponentInChildren<Image>(true);
        if (_fadeImage == null)
            Debug.LogWarning("DeathFadeCanvas: No Image found in children of ScreenFade prefab.");
        else
            _fadeImage.color = new Color(0f, 0f, 0f, 0f);
    }

    private void Start()
    {
        if (_fadeImage == null)
        {
            OnFadeComplete?.Invoke();
            Destroy(gameObject);
            return;
        }

        StartCoroutine(FadeSequence());
    }

    private void OnDisable()
    {
        if (Application.isPlaying) return;

        if (_fadeImage != null)
        {
            _fadeImage.DOKill();
            _fadeImage.color = new Color(0f, 0f, 0f, 0f);
        }
        DestroyImmediate(gameObject);
    }

    private IEnumerator FadeSequence()
    {
        // Fade to black
        yield return _fadeImage.DOFade(1f, fadeInDuration).WaitForCompletion();

        // Hold at full black, then notify the caller (teleport happens here)
        yield return new WaitForSeconds(holdDuration);
        OnFadeComplete?.Invoke();

        // Brief pause so the scene has a frame to settle after the teleport
        yield return null;

        // Fade back to clear, then destroy
        yield return _fadeImage.DOFade(0f, fadeOutDuration).WaitForCompletion();
        Destroy(gameObject);
    }
}
