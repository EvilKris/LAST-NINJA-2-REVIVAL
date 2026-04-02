using JSAM;
using UnityEngine;

public class SceneData1 : MonoBehaviour
{
    [SerializeField] private MusicFileObject thisLevelMusic;
    [SerializeField] private bool playMusic;
    [SerializeField] private bool SetRetroMode = true; 
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
        if(SetRetroMode)
        {
            MasterSingleton.Instance.CameraManager.SetRetroMode(true);
        }   

        if (playMusic || MasterSingleton.Instance.GameDataManager.MusicEnabled)
        {
            AudioManager.PlayMusic(thisLevelMusic, true);
        }
    }

    // Update is called once per frame
    void Update()
    {

    }
}
