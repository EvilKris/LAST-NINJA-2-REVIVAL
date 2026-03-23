using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[VolumeComponentMenu("Post-processing Custom/GrayScale")]
[VolumeRequiresRendererFeatures(typeof(GrayScaleRendererFeature))]
public class GrayScaleVolume : VolumeComponent, IPostProcessComponent
{
    /// <summary>
    /// オーバーレイ計算をかけた画像を、元画像にブレンドする係数
    /// </summary>
    public ClampedFloatParameter blend = new ClampedFloatParameter(0f, 0f, 1f);

    public bool IsActive() => active && EffectActive();

    /// <summary>
    /// レンダリング判定
    /// </summary>
    private bool EffectActive() => blend.value > 0f;
}
