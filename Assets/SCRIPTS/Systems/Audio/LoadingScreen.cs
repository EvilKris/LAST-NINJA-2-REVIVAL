using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

public class LoadingScreen : MonoBehaviour
{
    [SerializeField] private GameObject loadingScreen;
    [SerializeField] private Slider progressBar;
    [SerializeField] private Text loadingText;


    private void OnEnable()
    {
        string sceneName = MasterSingleton.Instance.SceneLoader.GetSceneToLoad();

        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("LoadingScreen: No scene name was set on SceneLoader.");
            return;
        }

        // Subscribe before starting the load so the callback fires once the target scene is active
        SceneManager.sceneLoaded += OnSceneLoaded;
        LoadScene(sceneName);
    }

    public void LoadScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("LoadingScreen: sceneName is null or empty.");
            return;
        }

        StartCoroutine(LoadSceneAsync(sceneName));
    }

    private IEnumerator LoadSceneAsync(string sceneName)
    {
        Debug.Log($"LoadingScreen: Starting async load of '{sceneName}'");
       
        if (loadingScreen != null)
            loadingScreen.SetActive(true);

        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);

        while (!operation.isDone)
        {
            float progress = Mathf.Clamp01(operation.progress / 0.9f);

            if (progressBar != null)
                progressBar.value = progress;

            if (loadingText != null)
                loadingText.text = $"Loading... {progress * 100:F0}%";

            yield return null;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Debug.Log($"LoadingScreen: Scene '{scene.name}' loaded.");

        // Only show the in-game overlay for gameplay scenes, not the main menu
        if (scene.name != "1-Menu-Scene")
            MasterSingleton.Instance.UIManager.ToggleInGameOverlay(true);
    }
}