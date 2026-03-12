using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Pure data and construction helper for the full-screen loading overlay.
/// Contains no MonoBehaviour logic — all coroutine/lifecycle work is handled
/// by <see cref="EventManager"/>, which owns the overlay at runtime.
/// </summary>
public static class LoadingScreenManager
{
    // -------------------------------------------------------------------------
    // Configuration — edit these defaults or drive them from EventManager
    // -------------------------------------------------------------------------

    public static Color  BackgroundColor = new Color(0.15f, 0.15f, 0.15f, 1f);
    public static string LoadingText     = "LOADING";
    public static int    FontSize        = 48;

    // -------------------------------------------------------------------------
    // Factory
    // -------------------------------------------------------------------------

    /// <summary>
    /// Builds and returns the full-screen overlay <see cref="GameObject"/> with a
    /// <see cref="Canvas"/>, background <see cref="Image"/>, label <see cref="Text"/>,
    /// and a <see cref="CanvasGroup"/> ready for alpha fading.
    /// The caller is responsible for calling <c>DontDestroyOnLoad</c> and
    /// activating/deactivating the root as needed.
    /// </summary>
    /// <returns>The root overlay GameObject, initially inactive.</returns>
    public static (GameObject root, CanvasGroup canvasGroup) BuildOverlay()
    {
        // Root GameObject — caller should DontDestroyOnLoad this
        GameObject root = new GameObject("LoadingScreenOverlay");

        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue - 1; // Above all game UI, below the white flash

        root.AddComponent<CanvasScaler>();

        // CanvasGroup drives the fade
        CanvasGroup cg = root.AddComponent<CanvasGroup>();
        cg.alpha           = 0f;
        cg.interactable    = false;
        cg.blocksRaycasts  = false;

        // Background
        GameObject bgGO = new GameObject("Background");
        bgGO.transform.SetParent(root.transform, false);
        bgGO.AddComponent<Image>().color = BackgroundColor;
        StretchToFill(bgGO.GetComponent<RectTransform>());

        // Label
        GameObject textGO = new GameObject("LoadingLabel");
        textGO.transform.SetParent(root.transform, false);
        Text label        = textGO.AddComponent<Text>();
        label.text        = LoadingText;
        label.font        = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize    = FontSize;
        label.alignment   = TextAnchor.MiddleCenter;
        label.color       = Color.white;
        StretchToFill(textGO.GetComponent<RectTransform>());

        root.SetActive(false);
        return (root, cg);
    }

    /// <summary>Anchors a <see cref="RectTransform"/> to fill its parent completely.</summary>
    public static void StretchToFill(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}

