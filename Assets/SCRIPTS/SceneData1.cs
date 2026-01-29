using JSAM;
using UnityEngine;

public class SceneData1 : MonoBehaviour
{
    [SerializeField] private MusicFileObject thisLevelMusic;
    [SerializeField] private bool playMusic;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (playMusic || MasterSingleton.Instance.GameDataManager.musicToggle)
        {
            AudioManager.PlayMusic(thisLevelMusic, true);
        }
    }

    // Update is called once per frame
    void Update()
    {

    }
}
