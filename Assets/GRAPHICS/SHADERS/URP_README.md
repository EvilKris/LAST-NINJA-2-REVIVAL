# Unity URP Materialization Shaders

Two shader systems for creating materialization effects in **Universal Render Pipeline (URP)**:

## 1. URP_MaterializeFromGround.shader
**Single object materialization from ground up (Y-axis)**

### Setup:
1. Import `URP_MaterializeFromGround.shader` into your Unity project
2. Create a new Material and assign this shader
3. Apply the material to any 3D object
4. Adjust the `Progress` slider (0 to 1) to animate

### Properties:
- **Progress**: 0 = invisible, 1 = fully materialized
- **Ground Level**: Y position where materialization starts
- **Height**: Total height of the object to materialize
- **Edge Width**: Thickness of the materialization wave front
- **Edge Color**: Color of the glowing edge
- **Effect Style**: Choose between Dissolve, Scanline, or Hologram

### Usage:
```csharp
// Animate via script
Material mat = GetComponent<Renderer>().material;
mat.SetFloat("_Progress", Mathf.Lerp(0f, 1f, time));
```

---

## 2. MaterializeWaveFeature (URP Renderer Feature)
**Full-screen Z-axis depth wave effect for URP**

### Setup (IMPORTANT - Different from Built-in):

#### Step 1: Import Files
1. Import `URP_MaterializeWaveEffect.shader`
2. Import `MaterializeWaveFeature.cs`

#### Step 2: Add to Renderer
1. Open your **URP Renderer Asset** (usually in Assets/Settings or similar)
   - If you don't have one, create it: Right-click → Create → Rendering → URP Renderer
2. In the Renderer Inspector, click **"Add Renderer Feature"**
3. Select **"Materialize Wave Feature"**
4. The shader will auto-assign (or drag `URP_MaterializeWaveEffect.shader` to the Shader field)

#### Step 3: Configure
In the Renderer Feature settings:
- **Progress**: 0 = nothing visible, 1 = everything materialized
- **Wave Width**: How thick the materialization wave is
- **Near Distance**: Starting depth for the effect
- **Far Distance**: Ending depth for the effect
- **Edge Color**: Color of the wave front glow
- **Effect Style**: Dissolve / Scanline / Hologram
- **Auto Animate**: Check to play animation automatically
- **Animation Duration**: How long the wave takes to complete

### Script Control:
```csharp
// Access the feature from code
using UnityEngine.Rendering.Universal;

// Get your URP Renderer Asset
UniversalRendererData rendererData = /* your renderer asset */;

// Find the feature (you'll need to cache this reference)
MaterializeWaveFeature waveFeature = /* cached reference */;

// Start animation
waveFeature.StartAnimation();

// Or manually control via settings
waveFeature.settings.progress = 0.5f;
```

**Note**: Since Renderer Features don't have direct scene references, you'll typically control them via:
1. Public fields in the Renderer Feature (adjusted in Inspector)
2. Shader Global properties set from a MonoBehaviour
3. Custom event system

### Alternative: Control via Global Shader Properties
Add this script to any GameObject in your scene:

```csharp
using UnityEngine;

public class MaterializeWaveController : MonoBehaviour
{
    [Range(0f, 1f)]
    public float progress = 0f;
    
    void Update()
    {
        Shader.SetGlobalFloat("_Progress", progress);
    }
}
```

Then modify the shader to use global properties if needed.

---

## URP vs Built-in Differences

### Built-in Pipeline:
- Uses `OnRenderImage()` on Camera
- Direct access to `_CameraDepthTexture`
- Simple MonoBehaviour script

### URP:
- Uses **Renderer Features** (more powerful but more setup)
- Depth accessed via `SampleSceneDepth()`
- Uses `Blitter` API for efficient rendering
- Configured in Renderer Asset, not per-camera

---

## Effect Styles

### Dissolve
- Noisy particle-like appearance
- Pixels randomly fade in
- Best for: magical/teleportation effects

### Scanline
- Horizontal scan lines
- Bright edge glow at wave front
- Best for: sci-fi/holodeck effects

### Hologram
- Flickering grid overlay
- Intense edge highlights
- Best for: futuristic HUD/projection effects

---

## Performance Notes

- **Per-Object Shader**: Cheap, runs per-object
- **Renderer Feature**: Runs once per frame as a post-process pass
- URP's depth texture is efficiently generated
- Both use early discard for optimal performance

---

## Tips for URP

1. **Depth Texture Setup**
   - URP automatically handles depth texture when Renderer Features request it
   - No manual camera setup needed

2. **Multiple Cameras**
   - Renderer Features apply to all cameras using that Renderer
   - For per-camera control, create multiple Renderer Assets

3. **Render Pass Event**
   - Default: `BeforeRenderingPostProcessing`
   - Change if you need different timing relative to post-processing

4. **HDR Colors**
   - Edge Color can exceed 1.0 for bloom/glow effects
   - Works with URP's post-processing stack

---

## Troubleshooting URP

**Feature doesn't appear in list:**
- Make sure `MaterializeWaveFeature.cs` is in the project
- Check for compile errors in Console
- Reimport the script

**Black screen:**
- Verify Progress is between 0 and 1
- Check Near Distance < Far Distance
- Ensure shader is assigned in Renderer Feature

**Effect not visible:**
- Confirm the Renderer Asset is assigned to your URP Asset
- Check the camera is using the correct Renderer
- Verify Render Pass Event timing

**Can't control from script:**
- Cache a reference to your Renderer Feature at startup
- Or use Shader.SetGlobalFloat() for simpler control
- Renderer Features aren't directly accessible via GetComponent

---

## Converting from Built-in Pipeline

If you have the built-in version and want to switch to URP:

1. **Per-Object Shader**: Use `URP_MaterializeFromGround.shader` - materials will need reassigning
2. **Post-Process**: Remove camera script, add Renderer Feature instead
3. **Shader Keywords**: URP uses HLSL instead of Cg (mostly syntax changes)
4. **Depth Access**: `_CameraDepthTexture` → `SampleSceneDepth()`

---

## File Structure

```
YourProject/
├── Shaders/
│   ├── URP_MaterializeFromGround.shader
│   └── URP_MaterializeWaveEffect.shader
└── Scripts/
    └── MaterializeWaveFeature.cs
```

---

Enjoy your URP materialization effects! 🚀✨
