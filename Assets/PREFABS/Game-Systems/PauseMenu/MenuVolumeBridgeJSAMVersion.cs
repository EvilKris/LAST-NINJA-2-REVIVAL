using UnityEngine;
using UnityEngine.UI;
using JSAM;

[RequireComponent(typeof(Slider))]
public class MenuVolumeBridgeJSAMVersion : MonoBehaviour
{
    private Slider slider;
    private bool isInitializing = false;

    private void Awake()
    {
        slider = GetComponent<Slider>();
    }

    private void OnEnable()
    {
        if (slider != null)
        {
            isInitializing = true;
            slider.value = AudioManager.MasterVolume;
            isInitializing = false;
        }
    }

    //link up OnSliderMove to the slider's OnValueChanged event! 
    public void OnSliderMove(float val)
    {
        if (!isInitializing)
        {
            AudioManager.MasterVolume = val;
        }
    }
}