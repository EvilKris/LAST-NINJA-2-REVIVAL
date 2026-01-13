using JSAM;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuManager : MonoBehaviour
{
    [SerializeField] private SoundFileObject clickSound;
    [SerializeField] private SoundFileObject overSound;
    [SerializeField] private MusicFileObject myMusic;

    public void OnOverSound()
    {
               JSAM.AudioManager.PlaySound(overSound); 
    }

    private void Start()
    {
        JSAM.AudioManager.PlayMusic(myMusic, true);
    }
    public void StartGame()
    {
        // 1. Play the sound effect
        JSAM.AudioManager.PlaySound(clickSound);
        

        // 2. Get the index of the current scene and add 1
        int nextSceneIndex = SceneManager.GetActiveScene().buildIndex + 1;

        // 3. Load the next scene (ensure it's in Build Settings!)
        // We use a small delay if you want the SFX to finish, 
        // but for now, we'll load immediately.
        SceneManager.LoadScene(nextSceneIndex);
    }
}