using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Central event hub that owns game-wide UI effects and broadcasts lifecycle events.
/// Holds the loading screen overlay and exposes a static API so any script can
/// trigger it without needing a direct reference.
/// Expand this class as new event types are needed.
/// </summary>
public class EventManager : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Loading screen configuration (editable in the Inspector)
    // -------------------------------------------------------------------------

    [Header("Loading Screen")]
    [SerializeField] private float fadeDuration = 0.4f; // Duration of the fade-in and fade-out

    // -------------------------------------------------------------------------
    // Static API — callable from anywhere without a reference
    // -------------------------------------------------------------------------

    /// <summary>Fades the loading screen in and begins blocking raycasts.</summary>
    public static void ShowLoadingScreen() => _instance?.Show();

    /// <summary>Fades the loading screen out. Called automatically on scene load.</summary>
    public static void HideLoadingScreen() => _instance?.Hide();

    // -------------------------------------------------------------------------
    // Events — subscribe to react to loading screen state changes
    // -------------------------------------------------------------------------

    public static event System.Action OnLoadingScreenShown;
    public static event System.Action OnLoadingScreenHidden;

    // -------------------------------------------------------------------------
    // Private state
    // -------------------------------------------------------------------------

    private static EventManager _instance;

    private GameObject  _overlayRoot;
    private CanvasGroup _canvasGroup;
    private bool        _isVisible;

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        _instance = this;

        // Build the overlay using the shared factory — no MonoBehaviour needed there
        (_overlayRoot, _canvasGroup) = LoadingScreenManager.BuildOverlay();
        DontDestroyOnLoad(_overlayRoot);

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;

        if (_overlayRoot != null)
            Destroy(_overlayRoot);

        if (_instance == this)
            _instance = null;
    }

    // -------------------------------------------------------------------------
    // Show / Hide implementation
    // -------------------------------------------------------------------------

    private void Show()
    {
        if (_isVisible) return;
        _isVisible = true;
        _overlayRoot.SetActive(true);
        StopAllCoroutines();
        StartCoroutine(Fade(0f, 1f, fadeDuration, () =>
        {
            Debug.Log("[EventManager] Loading screen shown.");
            OnLoadingScreenShown?.Invoke();
        }));
    }

    private void Hide()
    {
        if (!_isVisible) return;
        _isVisible = false;
        StopAllCoroutines();
        StartCoroutine(Fade(1f, 0f, fadeDuration, () =>
        {
            _overlayRoot.SetActive(false);
            Debug.Log("[EventManager] Loading screen hidden.");
            OnLoadingScreenHidden?.Invoke();
        }));
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Linearly fades <see cref="_canvasGroup"/> alpha from <paramref name="from"/>
    /// to <paramref name="to"/> using unscaled time so it works at any timeScale.
    /// </summary>
    private IEnumerator Fade(float from, float to, float duration, System.Action onComplete = null)
    {
        _canvasGroup.blocksRaycasts = (to > 0f); // Block input while visible

        float time = 0f;
        while (time < duration)
        {
            time += Time.unscaledDeltaTime;
            _canvasGroup.alpha = Mathf.Lerp(from, to, time / duration);
            yield return null;
        }

        _canvasGroup.alpha = to;
        onComplete?.Invoke();
    }

    /// <summary>Automatically hides the loading screen when a new scene finishes loading.</summary>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Hide();
}


