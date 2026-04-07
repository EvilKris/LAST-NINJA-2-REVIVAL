using UnityEngine;
using JSAM;

public class SceneData1 : MonoBehaviour
{
    [SerializeField] private MusicFileObject thisLevelMusic;
    [SerializeField] private bool playMusic;
    [SerializeField] private bool SetRetroMode = true;

    [Header("Materialize Intro")]
    [Tooltip("Duration in seconds for the materialize reveal sweep.")]
    [SerializeField] private float materializeDuration = 2f;
    [Tooltip("Tag of the GameObject to apply the materialize effect to.")]
    [SerializeField] private string materializeTag = "Player";

    void Start()
    {
        if (SetRetroMode)
            MasterSingleton.Instance.CameraManager.SetRetroMode(true);

        if (playMusic || MasterSingleton.Instance.GameDataManager.MusicEnabled)
            AudioManager.PlayMusic(thisLevelMusic, true);

        MasterSingleton.Instance.GameDataManager.PlayMaterializeIntro(materializeTag, materializeDuration);
    }

    void Update() { }
}
