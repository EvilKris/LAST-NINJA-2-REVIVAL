using UnityEngine;

/// <summary>
/// Attach to any <see cref="WorldItem"/> prefab to make it periodically flash solid white,
/// making small pickups easy to spot in the world.
///
/// Assign a white URP Unlit material to <see cref="flashMaterial"/> in the Inspector.
/// This ensures the shader is included in builds (Shader.Find is Editor-only reliable).
/// Caches the originals from every child Renderer on Awake and swaps between them
/// on a configurable interval/duration cycle.
/// </summary>
public class PickupFlashEffect : MonoBehaviour
{
    [Tooltip("Seconds between the start of each flash.")]
    [SerializeField] private float flashInterval = 1.2f;

    [Tooltip("How long the white flash lasts each cycle.")]
    [SerializeField] private float flashDuration = 0.1f;

    [Tooltip("Assign a solid-white URP Unlit material here. Required for builds — Shader.Find is not build-safe.")]
    [SerializeField] private Material flashMaterial;

    // Cached renderer data so we never call GetComponentsInChildren in Update
    private struct RendererEntry
    {
        public Renderer renderer;
        public Material[] originalMaterials;
        public Material[] flashMaterials;
    }

    private RendererEntry[] _entries;
    private float _timer;
    private bool _isFlashing;

    private void Awake()
    {
        Material whiteMat = GetOrCreateWhiteMaterial();
        CacheRenderers(whiteMat);
    }

    private void OnDestroy()
    {
        // Only destroy the material if we created it at runtime (no serialized reference was assigned)
        if (_entries == null) return;
        if (flashMaterial == null && _runtimeCreatedMaterial != null)
            Destroy(_runtimeCreatedMaterial);
    }

    // Tracks a material we created at runtime so we can destroy it in OnDestroy
    private Material _runtimeCreatedMaterial;

    private void Update()
    {
        _timer += Time.deltaTime;

        if (!_isFlashing && _timer >= flashInterval)
        {
            ApplyFlash();
            _isFlashing = true;
            _timer = 0f;
        }
        else if (_isFlashing && _timer >= flashDuration)
        {
            RestoreOriginals();
            _isFlashing = false;
            _timer = 0f;
        }
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private void CacheRenderers(Material whiteMat)
    {
        if (whiteMat == null) { _entries = System.Array.Empty<RendererEntry>(); return; }

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        _entries = new RendererEntry[renderers.Length];

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            Material[] originals = r.sharedMaterials;

            // One white material instance per slot so restoring is a simple array swap
            Material[] flash = new Material[originals.Length];
            for (int s = 0; s < originals.Length; s++)
                flash[s] = whiteMat;

            _entries[i] = new RendererEntry
            {
                renderer = r,
                originalMaterials = originals,
                flashMaterials = flash
            };
        }
    }

    private void ApplyFlash()
    {
        foreach (var entry in _entries)
        {
            if (entry.renderer != null)
                entry.renderer.materials = entry.flashMaterials;
        }
    }

    private void RestoreOriginals()
    {
        foreach (var entry in _entries)
        {
            if (entry.renderer != null)
                entry.renderer.sharedMaterials = entry.originalMaterials;
        }
    }

    /// <summary>
    /// Returns the serialized <see cref="flashMaterial"/> if assigned, otherwise creates
    /// a fallback white material at runtime. The serialized path is strongly preferred
    /// because <c>Shader.Find</c> only works in builds when the shader is in
    /// "Always Included Shaders" — the serialized reference guarantees inclusion.
    /// </summary>
    private Material GetOrCreateWhiteMaterial()
    {
        if (flashMaterial != null)
            return flashMaterial;

        Debug.LogWarning("[PickupFlashEffect] No flash material assigned. Falling back to Shader.Find — this may not work in builds. Assign a white URP Unlit material in the Inspector.", this);

        Shader unlit = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
        if (unlit == null)
        {
            Debug.LogError("[PickupFlashEffect] Could not find a usable unlit shader. Flash effect will be disabled.", this);
            return null;
        }

        var mat = new Material(unlit);
        mat.name = "PickupFlash_White_Runtime";

        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", Color.white);
        if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", Color.white);

        _runtimeCreatedMaterial = mat;
        return mat;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        flashInterval = Mathf.Max(0.1f, flashInterval);
        flashDuration = Mathf.Clamp(flashDuration, 0.02f, flashInterval - 0.02f);
    }
#endif
}
