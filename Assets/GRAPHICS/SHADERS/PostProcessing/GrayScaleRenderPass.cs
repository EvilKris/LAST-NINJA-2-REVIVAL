using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

public class GrayScaleRenderPass : ScriptableRenderPass
{
    private static readonly int MainTexture = Shader.PropertyToID("_MainTexture");
    private static readonly int Weight = Shader.PropertyToID("_Weight");

    private string PassName => "GrayScalePass";
    private Material _material;

    /// <summary>
    /// コンストラクタ
    /// </summary>
    /// <param name="shader">Shader</param>
    /// <param name="evt">RenderPassEvent</param>
    public GrayScaleRenderPass(Shader shader, RenderPassEvent evt)
    {
        renderPassEvent = evt;
        if (shader != null)
            _material = CoreUtils.CreateEngineMaterial(shader);
    }

    /// <summary>
    /// RenderGraphのレコード
    /// </summary>
    /// <param name="renderGraph"></param>
    /// <param name="frameData"></param>
    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        // Volumeを取得
        var stack = VolumeManager.instance.stack;
        var volume = stack.GetComponent<GrayScaleVolume>();
        if (volume == null || !volume.IsActive() || _material == null) return;

        // 必要情報をContextContainerから取得
        var resources = frameData.Get<UniversalResourceData>();
        var src = resources.activeColorTexture;
        var desc = renderGraph.GetTextureDesc(src);
        var dst = renderGraph.CreateTexture(desc);

        using (var builder = renderGraph.AddRasterRenderPass<PassData>(PassName, out var data))
        {
            // 3) ラスターパス
            data.Material = _material;
            data.Src = src;
            data.Dst = dst;
            data.Weight = volume.blend.value;

            builder.UseTexture(src, AccessFlags.Read);
            builder.SetRenderAttachment(dst, 0, AccessFlags.Write);

            builder.AllowGlobalStateModification(true);

            builder.SetRenderFunc((PassData d, RasterGraphContext ctx) =>
            {
                d.Material.SetFloat(Weight, d.Weight);
                ctx.cmd.SetGlobalTexture(MainTexture, d.Src);
                CoreUtils.DrawFullScreen(ctx.cmd, d.Material);
            });
            resources.cameraColor = dst;
        }
    }

    public virtual void Dispose()
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
        public float Weight;
    }
}
