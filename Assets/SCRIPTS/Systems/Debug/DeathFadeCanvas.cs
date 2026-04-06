using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// Procedurally builds a full-screen black Canvas that fades in, fires a callback at peak opacity,
/// then fades back out and destroys itself. No prefab required.
/// Attach via <see cref="GameManager.HandlePlayerDeath"/> — do not place in the scene manually.
/// Replace the fade logic here with a shader-driven effect when ready.
/// </summary>
public class DeathFadeCanvas : MonoBehaviour
{
    /// <summary>Fired once the screen has fully faded to black, before the fade-out begins.</summary>
    public Action OnFadeComplete;

    [SerializeField] private float fadeInDuration = 0.8f;
    [SerializeField] private float holdDuration = 0.4f;
    [SerializeField] private float fadeOutDuration = 0.8f;

    private CanvasGroup _canvasGroup;

    private void Awake()
    {
        BuildCanvas();
        _canvasGroup.alpha = 0f;
    }

    private void Start()
    {
        StartCoroutine(FadeSequence());
    }

    /// <summary>
    /// Constructs the Canvas, CanvasGroup, and black Image entirely in code.
    /// </summary>
    private void BuildCanvas()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // Render on top of everything else in the scene
        canvas.sortingOrder = 999;

        gameObject.AddComponent<CanvasScaler>();
        gameObject.AddComponent<GraphicRaycaster>();

        _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        // Block input during the fade so the player can't do anything while blacked out
        _canvasGroup.blocksRaycasts = true;
        _canvasGroup.interactable = false;

        // Full-screen black panel
        GameObject panel = new GameObject("FadePanel");
        panel.transform.SetParent(transform, false);
        Image img = panel.AddComponent<Image>();
        img.color = Color.black;
        RectTransform rt = img.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
    }

    private IEnumerator FadeSequence()
    {
        // Fade to black
        yield return _canvasGroup.DOFade(1f, fadeInDuration).WaitForCompletion();

        // Hold at full black, then notify the caller (teleport happens here)
        yield return new WaitForSeconds(holdDuration);
        OnFadeComplete?.Invoke();

        // Brief pause so the scene has a frame to settle after the teleport
        yield return null;

        // Fade back to clear, then remove this object
        yield return _canvasGroup.DOFade(0f, fadeOutDuration).WaitForCompletion();
        Destroy(gameObject);
    }
}
