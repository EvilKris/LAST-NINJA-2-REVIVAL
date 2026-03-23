using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

public class CRTRenderPass : ScriptableRenderPass
{
    private static readonly int MainTexture = Shader.PropertyToID("_MainTexture");
    private static readonly int SharpnessH = Shader.PropertyToID("_SharpnessH");
    private static readonly int SharpnessV = Shader.PropertyToID("_SharpnessV");
    private static readonly int MaskStrength = Shader.PropertyToID("_MaskStrength");
    private static readonly int MaskDotWidth = Shader.PropertyToID("_MaskDotWidth");
    private static readonly int MaskDotHeight = Shader.PropertyToID("_MaskDotHeight");
    private static readonly int MaskStagger = Shader.PropertyToID("_MaskStagger");
    private static readonly int MaskSize = Shader.PropertyToID("_MaskSize");
    private static readonly int ScanlineStrength = Shader.PropertyToID("_ScanlineStrength");
    private static readonly int ScanlineBeamWidthMin = Shader.PropertyToID("_ScanlineBeamWidthMin");
    private static readonly int ScanlineBeamWidthMax = Shader.PropertyToID("_ScanlineBeamWidthMax");
    private static readonly int ScanlineBrightMin = Shader.PropertyToID("_ScanlineBrightMin");
    private static readonly int ScanlineBrightMax = Shader.PropertyToID("_ScanlineBrightMax");
    private static readonly int ScanlineCutoff = Shader.PropertyToID("_ScanlineCutoff");
    private static readonly int GammaInput = Shader.PropertyToID("_GammaInput");
    private static readonly int GammaOutput = Shader.PropertyToID("_GammaOutput");
    private static readonly int BrightBoost = Shader.PropertyToID("_BrightBoost");
    private static readonly int Dilation = Shader.PropertyToID("_Dilation");
    
    private string PassName => "CRTPass";
    private Material _material;

    public CRTRenderPass(Shader shader, RenderPassEvent evt)
    {
        renderPassEvent = evt;
        if (shader != null)
            _material = CoreUtils.CreateEngineMaterial(shader);
    }

    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        // Get Volume
        var stack = VolumeManager.instance.stack;
        var volume = stack.GetComponent<CRTVolume>();
        if (volume == null || !volume.IsActive() || _material == null) return;
        
        // Get necessary info from ContextContainer
        var resources = frameData.Get<UniversalResourceData>();
        var src = resources.activeColorTexture;
        var desc = renderGraph.GetTextureDesc(src);
        var dst = renderGraph.CreateTexture(desc);

        using (var builder = renderGraph.AddRasterRenderPass<PassData>(PassName, out var data))
        {
            // Setup pass data
            data.Material = _material;
            data.Src = src;
            data.Dst = dst;
            data.Volume = volume;
            
            // Declare texture dependencies - KEY PART!
            builder.UseTexture(src, AccessFlags.Read);
            builder.SetRenderAttachment(dst, 0, AccessFlags.Write);
            
            builder.AllowGlobalStateModification(true);
            
            builder.SetRenderFunc((PassData d, RasterGraphContext ctx) =>
            {
                // Set all shader parameters
                d.Material.SetFloat(SharpnessH, d.Volume.sharpnessH.value);
                d.Material.SetFloat(SharpnessV, d.Volume.sharpnessV.value);
                d.Material.SetFloat(MaskStrength, d.Volume.maskStrength.value);
                d.Material.SetFloat(MaskDotWidth, d.Volume.maskDotWidth.value);
                d.Material.SetFloat(MaskDotHeight, d.Volume.maskDotHeight.value);
                d.Material.SetFloat(MaskStagger, d.Volume.maskStagger.value);
                d.Material.SetFloat(MaskSize, d.Volume.maskSize.value);
                d.Material.SetFloat(ScanlineStrength, d.Volume.scanlineStrength.value);
                d.Material.SetFloat(ScanlineBeamWidthMin, d.Volume.scanlineBeamWidthMin.value);
                d.Material.SetFloat(ScanlineBeamWidthMax, d.Volume.scanlineBeamWidthMax.value);
                d.Material.SetFloat(ScanlineBrightMin, d.Volume.scanlineBrightMin.value);
                d.Material.SetFloat(ScanlineBrightMax, d.Volume.scanlineBrightMax.value);
                d.Material.SetFloat(ScanlineCutoff, d.Volume.scanlineCutoff.value);
                d.Material.SetFloat(GammaInput, d.Volume.gammaInput.value);
                d.Material.SetFloat(GammaOutput, d.Volume.gammaOutput.value);
                d.Material.SetFloat(BrightBoost, d.Volume.brightBoost.value);
                d.Material.SetFloat(Dilation, d.Volume.dilation.value);
                
                // Set the source texture and draw fullscreen
                ctx.cmd.SetGlobalTexture(MainTexture, d.Src);
                CoreUtils.DrawFullScreen(ctx.cmd, d.Material);
            });
            
            // THE KEY TRICK: Replace cameraColor with dst!
            resources.cameraColor = dst;
        }
    }

    public void Dispose()
    {
        if (_material != null)
        {
            CoreUtils.Destroy(_material);
            _material = null;
        }
    }
    
    private class PassData
    {
        public TextureHandle Src, Dst;
        public Material Material;
        public CRTVolume Volume;
    }
}
