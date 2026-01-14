using DG.Tweening;
using JSAM;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Manages the main menu functionality including audio playback, UI transitions, and scene loading.
/// </summary>
public class MainMenuManager : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField] private SoundFileObject clickSound;
    [SerializeField] private SoundFileObject overSound;
    [SerializeField] private MusicFileObject myMusic;

    [Header("UI Transition")]
    [SerializeField] private CanvasGroup mainMenuCanvasGroup;
    [SerializeField] private float fadeOutDuration = 1f;
    [SerializeField] private AnimationCurve fadeOutCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private RectTransform uiCanvasRect;

    /// <summary>
    /// Plays the UI hover sound effect.
    /// Called by UI button hover events.
    /// </summary>
    public void OnOverSound()
    {
        JSAM.AudioManager.PlaySound(overSound);
    }

    private void Start()
    {
        JSAM.AudioManager.PlayMusic(myMusic, true);
    }

    /// <summary>
    /// Initiates the game start sequence:
    /// 1. Plays click sound
    /// 2. Triggers UI camera shake effect
    /// 3. Fades out the menu
    /// 4. Loads the next scene
    /// </summary>
    public void StartGame()
    {
        JSAM.AudioManager.PlaySound(clickSound);

        MasterSingleton.Instance.UIManager.UICamShake(uiCanvasRect, 2f, 30f, 5);

        mainMenuCanvasGroup.DOFade(0f, fadeOutDuration)
            .SetEase(fadeOutCurve)
            .OnComplete(() =>
            {
                int nextSceneIndex = SceneManager.GetActiveScene().buildIndex + 1;
                SceneManager.LoadScene(nextSceneIndex);
            });
    }
}