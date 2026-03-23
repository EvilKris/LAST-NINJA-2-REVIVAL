using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class CRTRendererFeature : ScriptableRendererFeature
{
    [SerializeField]
    private Settings settings = new Settings();
    
    private string ShaderName => "Hidden/Post-processing Custom/CRT_EasyMode";
    private CRTRenderPass _pass;
    
    private CRTRenderPass CreatePass(Settings s)
    {
        if(s.shader == null)
            s.shader = Shader.Find(ShaderName);
        
        return new CRTRenderPass(s.shader, s.injectionPoint);
    }

    public override void Create()
    {
        _pass = CreatePass(settings);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (renderingData.cameraData.isSceneViewCamera) return;
        
        var v = VolumeManager.instance.stack.GetComponent<CRTVolume>();
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
        if (_pass != null)
        {
            _pass.Dispose();
            _pass = null;
        }
        Create();
    }
#endif
    
    [System.Serializable]
    public class Settings
    {
        public Shader shader;
        public RenderPassEvent injectionPoint = RenderPassEvent.BeforeRenderingPostProcessing;
    }
}
