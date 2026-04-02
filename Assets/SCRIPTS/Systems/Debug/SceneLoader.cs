using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{

    private string sceneToLoad;

    // Call this from anywhere in your game
    public void LoadSceneWithLoadingScreen(string sceneName)
    {
        sceneToLoad = sceneName;
        SceneManager.LoadScene("LoadingScene"); // Your loading scene name
    }

    // Call this from anywhere in your game using build index
    public void LoadSceneWithLoadingScreen(int sceneIndex)
    {
        sceneToLoad = SceneUtility.GetScenePathByBuildIndex(sceneIndex);
        SceneManager.LoadScene("LoadingScene");
    }

    // Loading scene calls this to start loading the target scene
    public string GetSceneToLoad()
    {
        return sceneToLoad;
    }
}