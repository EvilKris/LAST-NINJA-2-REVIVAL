using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class GrayScaleRendererFeature : ScriptableRendererFeature
{
    [SerializeField]
    private Settings settings = new Settings();

    private string ShaderName => "Hidden/Post-processing Custom/GrayScale";
    private GrayScaleRenderPass _pass;

    private GrayScaleRenderPass CreatePass(Settings s)
    {
        if (s.shader == null)
            s.shader = Shader.Find(ShaderName);

        return new GrayScaleRenderPass(s.shader, s.injectionPoint);
    }

    public override void Create()
    {
        _pass = CreatePass(settings);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (renderingData.cameraData.isSceneViewCamera) return;
        // SceneView等のフィルタ／XR判定などがあればここで
        var v = VolumeManager.instance.stack.GetComponent<GrayScaleVolume>();
        if (v == null || !v.IsActive())
            return;

        renderer.EnqueuePass(_pass);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (_pass != null)
        {
            _pass.Dispose();
            _pass = null;
        }
    }
#if UNITY_EDITOR
    private void OnValidate()
    {
        // 旧pass破棄
        if (_pass != null)
        {
            _pass.Dispose();
            _pass = null;
        }
        // 再生成
        Create();
    }
#endif

    [System.Serializable]
    public class Settings
    {
        public Shader shader;
        public RenderPassEvent injectionPoint = RenderPassEvent.AfterRenderingPostProcessing;
    }
}
