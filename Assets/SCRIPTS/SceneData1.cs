using JSAM;
using UnityEngine;

public class SceneData1 : MonoBehaviour
{
    [SerializeField] private MusicFileObject thisLevelMusic;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (MasterSingleton.Instance.GameDataManager.musicToggle)
        {
            AudioManager.PlayMusic(thisLevelMusic, true);
        }
    }

    // Update is called once per frame
    void Update()
    {

    }
}
