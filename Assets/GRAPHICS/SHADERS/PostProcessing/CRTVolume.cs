using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[VolumeComponentMenu("Post-processing Custom/CRT Effect")]
[VolumeRequiresRendererFeatures(typeof(CRTRendererFeature))]
public class CRTVolume : VolumeComponent, IPostProcessComponent
{
    [UnityEngine.Header("Sharpness")]
    public ClampedFloatParameter sharpnessH = new ClampedFloatParameter(0.5f, 0f, 1f);
    public ClampedFloatParameter sharpnessV = new ClampedFloatParameter(1.0f, 0f, 1f);

    [UnityEngine.Header("RGB Mask")]
    public ClampedFloatParameter maskStrength = new ClampedFloatParameter(0.3f, 0f, 1f);
    public ClampedFloatParameter maskDotWidth = new ClampedFloatParameter(1.0f, 1f, 100f);
    public ClampedFloatParameter maskDotHeight = new ClampedFloatParameter(1.0f, 1f, 100f);
    public ClampedFloatParameter maskStagger = new ClampedFloatParameter(0.0f, 0f, 100f);
    public ClampedFloatParameter maskSize = new ClampedFloatParameter(1.0f, 1f, 100f);

    [UnityEngine.Header("Scanlines")]
    public ClampedFloatParameter scanlineStrength = new ClampedFloatParameter(1.0f, 0f, 1f);
    public ClampedFloatParameter scanlineBeamWidthMin = new ClampedFloatParameter(1.5f, 0.5f, 5f);
    public ClampedFloatParameter scanlineBeamWidthMax = new ClampedFloatParameter(1.5f, 0.5f, 5f);
    public ClampedFloatParameter scanlineBrightMin = new ClampedFloatParameter(0.35f, 0f, 1f);
    public ClampedFloatParameter scanlineBrightMax = new ClampedFloatParameter(0.65f, 0f, 1f);
    public ClampedFloatParameter scanlineCutoff = new ClampedFloatParameter(400.0f, 1f, 1000f);

    [UnityEngine.Header("Color Correction")]
    public ClampedFloatParameter gammaInput = new ClampedFloatParameter(2.0f, 0.1f, 5f);
    public ClampedFloatParameter gammaOutput = new ClampedFloatParameter(1.8f, 0.1f, 5f);
    public ClampedFloatParameter brightBoost = new ClampedFloatParameter(1.2f, 1f, 2f);
    public ClampedFloatParameter dilation = new ClampedFloatParameter(1.0f, 0f, 1f);

    public bool IsActive() => active && EffectActive();

    private bool EffectActive() => maskStrength.value > 0f || scanlineStrength.value > 0f;
}
