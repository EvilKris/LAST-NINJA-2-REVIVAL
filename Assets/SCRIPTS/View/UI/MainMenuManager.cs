using DG.Tweening;
using JSAM;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// Manages the main menu functionality including audio playback, UI transitions, and scene loading.
/// </summary>
public class MainMenuManager : MonoBehaviour
{
    // Audio configuration
    [Header("Audio")]
    [SerializeField] private SoundFileObject clickSound; // Sound played when clicking menu buttons
    [SerializeField] private SoundFileObject overSound; // Sound played when hovering over menu buttons
    [SerializeField] private MusicFileObject myMusic; // Background music for the main menu
    [SerializeField] private float musicFadeOutDuration = 1f; // Duration for music fade out (currently unused)

    // UI transition settings
    [Header("UI Transition")]
    [SerializeField] private CanvasGroup mainMenuCanvasGroup; // Canvas group for fading the entire menu
    [SerializeField] private RectTransform bgBlackUI; // Background black image for flash effect at video end
    [SerializeField] private Ease bgBlackFadeEase;
    [SerializeField] private float fadeOutDuration = 1f; // Duration of the fade out animation when starting the game
    [SerializeField] private AnimationCurve fadeOutCurve = AnimationCurve.EaseInOut(0, 0, 1, 1); // Custom easing curve for fade animation
    [SerializeField] private RectTransform uiCanvasRect; // UI canvas rect for shake animation

    [SerializeField] private RectTransform uiTextButton; // Reference to the UI canvas RectTransform for shake effects    

    [SerializeField] private VideoPlayer videoPlayer; // Reference to the VideoPlayer component for playing menu videos
    [SerializeField] private RawImage displayImage; // RawImage displaying the video — hidden when playback ends
    [SerializeField] private float flashLeadTime = 0.5f; // Seconds before video end to start the white flash (peaks at video end)

    // Runtime state
    private Tween fadeTween;      // Active fade animation tween
    private bool _flashTriggered; // True once the white flash coroutine has been started
    private UIManager uiManager;             // Cached reference to UIManager singleton
    private GameDataManager gameDataManager; // Cached reference to GameDataManager singleton
    

    /// <summary>
    /// Plays the UI hover sound effect.
    /// Called by UI button hover events.
    /// </summary>
    public void OnOverSound()
    {
        JSAM.AudioManager.PlaySound(overSound);
    }


    private void Awake()
    {
        if (uiTextButton != null)
            uiTextButton.gameObject.SetActive(false);
    }

    /// <summary>
    /// Initializes the main menu on startup.
    /// Sets up manager references, hides in-game UI, starts menu music, and disables pause functionality.
    /// </summary>
    private void Start()
    {
        // Cache singleton references for performance and safety
        if (!TryGetManagers(out uiManager, out gameDataManager))
        {
            Debug.LogError("MainMenuManager: Required managers not found!");
            return;
        }

        // Hide in-game UI elements (HUD, health bars, etc.)
        uiManager.ToggleInGameOverlay(false);

        // Subscribe then play so the event is guaranteed to be registered before the clip ends
        if (videoPlayer != null)
        {
            videoPlayer.loopPointReached += OnVideoFinished;
           //videoPlayer.Play();
        }

        // Disable pause menu functionality while in main menu
        gameDataManager.IsPauseAllowed = false;
    }
   

   

    /// <summary>
    /// Called when the component is disabled.
    /// Ensures proper cleanup of active animations.
    /// </summary>
    private void OnDisable()
    {
        CleanupTweens();
    }

    /// <summary>
    /// Called when the object is destroyed.
    /// Ensures proper cleanup of active animations to prevent memory leaks.
    /// </summary>
    private void OnDestroy()
    {
        CleanupTweens();

        if (videoPlayer != null)
            videoPlayer.loopPointReached -= OnVideoFinished;
    }

    /// <summary>
    /// Called when the VideoPlayer has finished playing.
    /// Starts the menu music and reveals the main menu buttons.
    /// </summary>
    private void OnVideoFinished(VideoPlayer vp)
    {
        if (displayImage != null)
            displayImage.gameObject.SetActive(false);

        if (bgBlackUI != null)
        {
            Image bgImage = bgBlackUI.GetComponent<Image>();
            if (bgImage != null)
                bgImage.DOFade(0f, 8f).SetEase(bgBlackFadeEase);
        }

        if (uiTextButton != null)
            uiTextButton.gameObject.SetActive(true);

        AudioManager.PlayMusic(myMusic, null);
    }

    /// <summary>
    /// Safely kills all active DOTween animations to prevent memory leaks and animation errors.
    /// </summary>
    private void CleanupTweens()
    {
        // Kill the fade animation if it's running
        if (fadeTween != null)
        {
            fadeTween.Kill();
            fadeTween = null;
        }

        // Kill any animations on the UI canvas (like shake effects)
        if (uiCanvasRect != null)
        {
            uiCanvasRect.DOKill();
        }
    }

    /// <summary>
    /// Initiates the game start sequence:
    /// 1. Plays click sound
    /// 2. Stops menu music
    /// 3. Triggers UI camera shake effect for visual impact
    /// 4. Fades out the menu
    /// 5. Loads the next scene
    /// Called by the "Start Game" button in the main menu.
    /// </summary>
    public void StartGame()
    {
        // Safety check: ensure managers are initialized before proceeding
        if (uiManager == null || gameDataManager == null)
        {
            Debug.LogError("MainMenuManager: Cannot start game - managers not initialized!");
            return;
        }

        // Play button click sound for audio feedback
        AudioManager.PlaySound(clickSound);
        
        // Stop the menu music immediately (no fade)
        AudioManager.StopMusic(myMusic, null, true);

        // Trigger a shake effect on the UI for dramatic impact
        // Parameters: target, duration (2s), strength (10), vibrato (25 shakes)
        uiManager.UICamShake(uiCanvasRect, 2f, 10f, 25);

        // Kill any existing fade animation to prevent conflicts
        if (fadeTween != null)
        {
            fadeTween.Kill();
        }

        // Fade out the main menu canvas group over the specified duration
        fadeTween = mainMenuCanvasGroup.DOFade(0f, fadeOutDuration)
            .SetEase(fadeOutCurve)
            .OnComplete(LoadNextScene);
    }

    /// <summary>
    /// Loads the next scene in the build index.
    /// Cleans up animations, enables pause functionality, and shows in-game UI.
    /// </summary>
    private void LoadNextScene()
    {
        // Kill any remaining UI animations (like shake) before scene transition
        if (uiCanvasRect != null)
            uiCanvasRect.DOKill();

        // Re-enable pause menu functionality for gameplay
        gameDataManager.IsPauseAllowed = true;

        // Wait for the loading screen to finish fading in before loading the scene,
        // so the transition is never visible mid-load
        EventManager.OnLoadingScreenShown += OnLoadingScreenReady;
        EventManager.ShowLoadingScreen();
    }

    /// <summary>
    /// Called once the loading screen has fully faded in.
    /// Triggers the actual scene load so it happens behind the overlay.
    /// </summary>
    private void OnLoadingScreenReady()
    {
        EventManager.OnLoadingScreenShown -= OnLoadingScreenReady;

        int nextSceneIndex = SceneManager.GetActiveScene().buildIndex + 1;
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.LoadScene(nextSceneIndex);
    }

    /// <summary>
    /// Called when the next scene has finished loading.
    /// Shows the in-game UI overlay (HUD, health bars, etc.).
    /// </summary>
    /// <param name="scene">The scene that was loaded</param>
    /// <param name="mode">The load mode used</param>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Unsubscribe to prevent memory leaks and duplicate calls
        SceneManager.sceneLoaded -= OnSceneLoaded;
        
        // Show in-game UI elements now that we're in the game scene
        if (uiManager != null)
        {
            uiManager.ToggleInGameOverlay(true);
        }
    }

    /// <summary>
    /// Attempts to retrieve manager references from the MasterSingleton.
    /// </summary>
    /// <param name="ui">Output parameter for UIManager reference</param>
    /// <param name="gameData">Output parameter for GameDataManager reference</param>
    /// <returns>True if both managers were successfully retrieved, false otherwise</returns>
    private bool TryGetManagers(out UIManager ui, out GameDataManager gameData)
    {
        var singleton = MasterSingleton.Instance;
        ui = singleton != null ? singleton.UIManager : null;
        gameData = singleton != null ? singleton.GameDataManager : null;
        return ui != null && gameData != null;
    }
}